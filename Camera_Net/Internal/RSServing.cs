using Intel.RealSense;
using System;
using System.Collections.Generic;
using System.Threading;

namespace Camera_NET
{
    public sealed class RsServing
    {
        private static readonly object SLock = new object();
        private static RsServing _instance = null;

        private bool _started = false;
        private int _refs;
        private Thread _thread;
        private bool _skip = false;

        private RSDevice[] _devices;
        private VideoFrame[] _frames;
        private Pipeline[] _pipeline;
        private PipelineProfile[] _pprofile;
        private Context _context;
        private Config[] _config;
        private readonly Dictionary<string, int> _serial_to_dev = new Dictionary<string, int>();

        private int _min_exp = 1;
        private int _max_exp = 200000;
        private int _exp = 100000;

        public int MinExp => _min_exp;
        public int MaxExp => _min_exp;

        public int Exp
        {
            get => _exp;
            set
            {
                lock (this)
                {

                    _exp = Math.Max(Math.Min(value, MaxExp), MinExp);
                    foreach (var profile in _pprofile)
                    {
                        foreach (var sensor in profile.Device.Sensors)
                        {
                            if (!sensor.Options.Supports(Option.Exposure)) continue;
                            sensor.Options[Option.Exposure].Value = _exp;
                        }
                    }
                }
            }
        }

        public void PushExp(int exp)
        {
            lock (this)
            {
                _skip = true;
                _exp = Math.Max(Math.Min(exp, 200000), 1);
                if (_pprofile != null)
                    foreach (var profile in _pprofile)
                    {
                        foreach (var sensor in profile.Device.Sensors)
                        {
                            if (!sensor.Options.Supports(Option.Exposure)) continue;
                            sensor.Options[Option.Exposure].Value = _exp;
                        }
                    }
            }
            Thread.Sleep(10);
            lock (this) _skip = false;
        }


        public RSDevice[] Devices => _devices;
        private RsServing()
        {
            _context = new Context();
            var devs = _context.QueryDevices();
            _config = new Config[devs.Count];
            _pipeline = new Pipeline[devs.Count];
            _devices = new RSDevice[devs.Count * 3];
            _frames = new VideoFrame[devs.Count * 3];
            _pprofile = new PipelineProfile[devs.Count];
            int i = 0;
            foreach (var dev in _context.QueryDevices())
            {
                _config[i] = new Config();
                _serial_to_dev.Add(dev.Info.GetInfo(CameraInfo.SerialNumber), i);
                _config[i].EnableDevice(dev.Info.GetInfo(CameraInfo.SerialNumber));
                _config[i].EnableStream(Stream.Color, 0, 1920, 1080, Format.Bgr8, 15);
                _config[i].EnableStream(Stream.Infrared, 1, 1920, 1080, Format.Y8, 15);
                _config[i].EnableStream(Stream.Infrared, 2, 1920, 1080, Format.Y8, 15);
                _devices[i * 3] = new RSDevice()
                {
                    isIR = false,
                    isLeft = false,
                    Name = dev.Info.GetInfo(CameraInfo.Name),
                    Serial = dev.Info.GetInfo(CameraInfo.SerialNumber)
                };
                _devices[i * 3 + 1] = new RSDevice()
                {
                    isIR = true,
                    isLeft = true,
                    Name = dev.Info.GetInfo(CameraInfo.Name),
                    Serial = dev.Info.GetInfo(CameraInfo.SerialNumber)
                };
                _devices[i * 3 + 2] = new RSDevice()
                {
                    isIR = true,
                    isLeft = false,
                    Name = dev.Info.GetInfo(CameraInfo.Name),
                    Serial = dev.Info.GetInfo(CameraInfo.SerialNumber)
                };
                _pipeline[i] = new Pipeline(_context);
                i++;
            }
        }

        public static RsServing Instance
        {
            get
            {
                if (_instance != null) return _instance;
                Monitor.Enter(SLock);
                RsServing temp = new RsServing();
                Interlocked.Exchange(ref _instance, temp);
                Monitor.Exit(SLock);
                return _instance;
            }
        }

        public event EventHandler<int> GotFrame = delegate { };

        private void OnGotFrame(Frame f)
        {
            if (_skip)
                return;
            // TODO:
            foreach (var t in f.AsFrameSet())
            {
                int ind = _serial_to_dev[t.Sensor.Info.GetInfo(CameraInfo.SerialNumber)] * 3 + t.Profile.Index;
                _frames[ind] = t.As<VideoFrame>();
                GotFrame(Instance, ind);
            }
        }

        public VideoFrame GetFrame(int index)
        {
            lock (_frames)
            {
                return _frames[index];
            }
        }

        private void ThreadRunner()
        {
            for (int i = 0; i < _pipeline.Length; i++)
            {
                _pprofile[i] = _pipeline[i].Start(_config[i]);//, OnGotFrame);

                foreach (var sensor in _pprofile[i].Device.Sensors)
                {
                    if (sensor.Options.Supports(Option.LaserPower))
                    {
                        sensor.Options[Option.LaserPower].Value = 0f; // Disable laser
                    }

                    if (sensor.Options.Supports(Option.EmitterEnabled))
                    {
                        sensor.Options[Option.EmitterEnabled].Value = 0f; // Disable emitter
                    }

                    if (sensor.Options.Supports(Option.EnableAutoExposure))
                    {
                        sensor.Options[Option.EnableAutoExposure].Value = 0f;
                    }

                    if (!sensor.Options.Supports(Option.Exposure)) continue;
                    if (_exp == -1)
                        _exp = (int)sensor.Options[Option.Exposure].Value;

                    if (_min_exp == -1)
                        _min_exp = (int)sensor.Options[Option.Exposure].Min;
                    if (_max_exp == -1)
                        _max_exp = (int)sensor.Options[Option.Exposure].Max;
                }
            }

            lock (this)
            {
                _started = true;
                _skip = false;
            }

            while (true)
            {
                if (!_started)
                    return;

                foreach (var p in _pipeline)
                {
                    var fs = p.WaitForFrames(1000);
                    foreach (var t in fs)
                    {
                        int ind = _serial_to_dev[t.Sensor.Info.GetInfo(CameraInfo.SerialNumber)] * 3 + t.Profile.Index;
                        _frames[ind] = t.As<VideoFrame>();
                        GotFrame(Instance, ind);
                    }
                }
            }
        }

        public int Start(RSDevice stream)
        {
            int res = -1;
            for (var j = 0; j < Devices.Length; j++)
            {
                if (Devices[j].DevicePath != stream.DevicePath) continue;
                res = j;
                break;
            }

            if (res == -1)
                return -1;

            lock (this)
            {
                _refs += 1;
                if (!_started && _thread == null)
                {
                    _thread = new Thread(ThreadRunner);
                    _thread.Start();
                }
            }

            return res;
        }

        public void Stop()
        {
            lock (this)
            {
                _refs -= 1;
                if (_refs == 0 && _started)
                {
                    _started = false;
                    _thread.Join(500);
                    _frames = null;
                    // TODO: faster destruction (maybe we need to dispose all of the objects?)
                    foreach (var t in _pipeline)
                    {
                        t.Stop();
                    }
                }
            }
        }
    }
}
