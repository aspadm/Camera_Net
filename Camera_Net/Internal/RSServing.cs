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
        private int _refs = 0;

        private RSDevice[] _devices;
        private VideoFrame[] _frames;
        private Pipeline[] _pipeline;
        private Context _context;
        private Config[] _config;
        private Dictionary<string, int> _serial_to_dev = new Dictionary<string, int>();

        public RSDevice[] Devices => _devices;
        private RsServing()
        {
            _context = new Context();
            var devs = _context.QueryDevices();
            _config = new Config[devs.Count];
            _pipeline = new Pipeline[devs.Count];
            _devices = new RSDevice[devs.Count * 3];
            _frames = new VideoFrame[devs.Count * 3];
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
                if (!_started)
                {
                    for (int i = 0; i < _pipeline.Length; i++)
                    {
                        _pipeline[i].Start(_config[i], OnGotFrame);
                    }
                    _started = true;
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
                    for (int i = 0; i < _pipeline.Length; i++)
                    {
                        _pipeline[i].Stop();
                    }
                }
            }
        }
    }
}
