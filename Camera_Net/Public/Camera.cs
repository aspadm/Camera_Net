#region License

/*
Camera_NET - Camera wrapper for directshow for .NET
Copyright (C) 2013
https://github.com/free5lot/Camera_Net

This library is free software; you can redistribute it and/or
modify it under the terms of the GNU Lesser General Public
License as published by the Free Software Foundation; either
version 3.0 of the License, or (at your option) any later version.

This library is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
Lesser General Public License for more details.

You should have received a copy of the GNU LesserGeneral Public 
License along with this library. If not, see <http://www.gnu.org/licenses/>.
*/

#endregion

using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Camera_NET
{
    #region Using directives

    using System;
    using System.Drawing;
    using System.Drawing.Imaging;
    using System.Runtime.InteropServices;
    using System.Windows.Forms;
    using System.Runtime.InteropServices.ComTypes;

    // Microsoft.Win32 is used for SystemEvents namespace
    using Microsoft.Win32;

    // Use Intel.Realsense (Apache License 2.0)
    using Intel.RealSense;

    #endregion

    /// <summary>
    /// The Camera class is an main class that is a wrapper for video device.
    /// </summary>
    /// 
    /// <author> free5lot (free5lot@yandex.ru) </author>
    /// <version> 2013.12.16 </version>
    public class Camera : IDisposable
    {
        // ====================================================================

        #region Private members

        private Pipeline _pipeline = null;
        private Config _config = null;

        /// <summary>
        /// Private field. Use the public property <see cref="HostingControl"/> for access to this value.
        /// </summary>
        private Control _HostingControl = null;

        /// <summary>
        /// Private field. Use the public property <see cref="RSPath"/> for access to this value.
        /// </summary>
        private RSDevice _RSPath = null;

        /// <summary>
        /// Private field. Use the public property <see cref="Resolution"/> for access to this value.
        /// </summary>
        private Resolution _Resolution = new Resolution(1920, 1080);

        /// <summary>
        /// Private field. Use the public property <see cref="ResolutionList"/> for access to this value.
        /// </summary>
        private ResolutionList _ResolutionList = new ResolutionList { new Resolution(1920, 1080) };

        /// <summary>
        /// Private field. Use the public property <see cref="OutputVideoSize"/> for access to this value.
        /// </summary>
        private Rectangle _OutputVideoSize;

        #endregion

        // ====================================================================

        #region Internal stuff

        /// <summary>
        /// Private field. Was the graph built or not.
        /// </summary>
        internal bool _bGraphIsBuilt = false;

        /// <summary>
        /// Private field. Were handlers added or not. Needed to remove delegates
        /// </summary>
        internal bool _bHandlersAdded = false;

        internal Timer _Timer = new Timer();

        #endregion

        // ====================================================================

        #region Public properties

        /// <summary>
        /// Gets a control that is used for hosting camera's output.
        /// </summary>
        public Control HostingControl
        => _HostingControl;

        /// <summary>
        /// Gets an identifier of camera.
        /// </summary>
        public RSDevice RSPath => _RSPath;

        /// <summary>
        /// Gets or sets a resolution of camera's output.
        /// </summary>
        /// <seealso cref="ResolutionListRGB"/>
        public Resolution Resolution
        {
            get => _Resolution;
            set
            {
                // Change of resolution is not allowed after graph's built
                if (_bGraphIsBuilt)
                    throw new Exception(@"Change of resolution is not allowed after graph's built.");
                _Resolution = value;
            }
        }

        /// <summary>
        /// Gets a list of available resolutions (in RGB format).
        /// </summary>        
        public ResolutionList ResolutionListRGB => _ResolutionList;
        
        #endregion

        // ====================================================================

        #region Events

        /// <summary>
        /// Subscribe to this event to handle changing of size of video output <see cref="OutputVideoSize"/>.
        /// </summary>
        public event EventHandler OutputVideoSizeChanged;

        #endregion

        // ====================================================================

        #region Public Static functions

        /// <summary>
        /// Returns Moniker (device identification) of camera from device index.
        /// </summary>
        /// <param name="iDeviceIndex">Index (Zero-based) in list of available devices with VideoInputDevice filter category.</param>
        /// <returns>Moniker (device identification) of device</returns>
        public static RSDevice GetDeviceMoniker(int iDeviceIndex)
        {
            // TODO: list devices?
            throw new Exception(@"No video capture devices found at that index.");

            return new RSDevice();
        }

        /// <summary>
        /// Returns available resolutions with RGB color system for device moniker
        /// </summary>
        /// <param name="moniker">Moniker (device identification) of camera.</param>
        /// <returns>List of resolutions with RGB color system of device</returns>
        public static ResolutionList GetResolutionList(RSDevice moniker)
        {
            return new ResolutionList { new Resolution(1920, 1080) };
        }

        #endregion

        // ====================================================================

        #region Public member functions

        #region Create, Initialize and Dispose/Close

        /// <summary>
        /// Default constructor for <see cref="Camera"/> class.
        /// </summary>
        public Camera()
        {
        }

        /// <summary>
        /// Initializes camera and connects it to HostingControl and Moniker.
        /// </summary>
        /// <param name="hControl">Control that is used for hosting camera's output.</param>
        /// <param name="cam">RS (device identification) of camera.</param>
        /// <seealso cref="HostingControl"/>
        /// <seealso cref="RSDevice"/>
        public void Initialize(Control hControl, RSDevice cam)
        {
            _RSPath = cam ?? throw new Exception(@"Camera's id should be set.");
            _HostingControl = hControl ?? throw new Exception(@"Hosting control should be set.");
        }

        /// <summary>
        /// Destructor (disposer) for <see cref="Camera"/> class.
        /// </summary>
        ~Camera()
        {
            Dispose();
        }

        /// <summary>
        /// Dispose of <see cref="IDisposable"/> for <see cref="Camera"/> class.
        /// </summary>
        public void Dispose()
        {
            CloseAll();
        }

        /// <summary>
        /// Close and dispose all camera and DirectX stuff.
        /// </summary>
        public void CloseAll()
        {
            _bGraphIsBuilt = false;

            // stop rendering
            // TODO: stop
            StopGraph();


            if (_bHandlersAdded)
            {
                RemoveHandlers();
            }
        }

        #endregion

        #region Graph: Build, Run, Stop

        /// <summary>
        /// Builds DirectShow graph for rendering.
        /// </summary>
        public void BuildGraph()
        {
            _bGraphIsBuilt = false;

            try
            {
                _pipeline = new Pipeline();

                SetSourceParams();
                UpdateOutputVideoSize();

                if (_config == null) _config = new Config();
                _config.EnableStream(_RSPath.isIR ? Stream.Infrared : Stream.Color, _RSPath.isIR ? _RSPath.isLeft ? 1 : 2 : 0, 1920, 1080, _RSPath.isIR ? Format.Y8 : Format.Bgr8, 15);

                AddHandlers();

                // -------------------------------------------------------
                _bGraphIsBuilt = true;
                // -------------------------------------------------------
            }
            catch
            {
                CloseAll();
                throw;
            }
        }

        /// <summary>
        /// Runs DirectShow graph for rendering.
        /// </summary>
        public void RunGraph()
        {
            //var graph_guilder = (ICaptureGraphBuilder2)new CaptureGraphBuilder2();
            //int hr = graph_guilder.RenderStream(PinCategory.Preview, MediaType.Video, DX.CaptureFilter, null, DX.VMRenderer);
            //DsError.ThrowExceptionForHR(hr);

            // TODO: run
            PipelineProfile pp = _pipeline.Start(_config);
            _Timer.Interval = 1000 / 14;
            _Timer.Tick += _Timer_Tick;
            _Timer.Start();
        }

        private void _Timer_Tick(object sender, EventArgs e)
        {
            _HostingControl.Invalidate();
        }

        private Bitmap GotImage(VideoFrame frame)
        {
            byte[] outBuffer = new byte[frame.Width * frame.Height * 3];

            frame.CopyTo(outBuffer);

            if (_RSPath.isIR) // Convert grayscale to RGB
            {
                for (int i = frame.Width * frame.Height - 1; i >= 0; i--)
                {
                    outBuffer[i * 3] = outBuffer[i * 3 + 1] = outBuffer[i * 3 + 2] = outBuffer[i];
                }
            }

            //Create an image that will hold the image data
            Bitmap bmp = new Bitmap(frame.Width, frame.Height, PixelFormat.Format24bppRgb);

            //Get a reference to the images pixel data
            Rectangle dimension = new Rectangle(0, 0, bmp.Width, bmp.Height);
            BitmapData picData = bmp.LockBits(dimension, ImageLockMode.WriteOnly, bmp.PixelFormat);
            IntPtr pixelStartAddress = picData.Scan0;

            //Copy the pixel data into the bitmap structure
            System.Runtime.InteropServices.Marshal.Copy(outBuffer, 0, pixelStartAddress, outBuffer.Length);

            bmp.UnlockBits(picData);
            return bmp;
        }

        /// <summary>
        /// Stops DirectShow graph for rendering.
        /// </summary>
        public void StopGraph()
        {
            // TODO: stop
            _Timer?.Stop();
            if (_pipeline == null) return;
            if (!_bGraphIsBuilt) return;
            _pipeline.Stop();
        }
        #endregion

        #region Spanshot (screenshots) frame

        /// <summary>
        /// Compatibility. Proxy for SnapshotSourceImage.
        /// </summary>
        /// <returns>Snapshot as a Bitmap</returns>
        /// <seealso cref="SnapshotSourceImage"/>
        public Bitmap SnapshotOutputImage()
        {
            return SnapshotSourceImage();
        }

        /// <summary>
        /// Make snapshot of source image. Much faster than SnapshotOutputImage.
        /// </summary>
        /// <returns>Snapshot as a Bitmap</returns>
        /// <seealso cref="SnapshotOutputImage"/>
        public Bitmap SnapshotSourceImage()
        {
            Bitmap bitmap = GotImage(_pipeline.WaitForFrames().InfraredFrame);

            var bitmap_clone = bitmap.Clone(new Rectangle(0, 0, bitmap.Width, bitmap.Height), PixelFormat.Format24bppRgb);

            bitmap.Dispose();

            return bitmap_clone;
        }

        #endregion

        #endregion

        // ====================================================================

        #region Private members

        #region Graph building stuff

        /// <summary>
        /// Sets the Framerate, and video size.
        /// </summary>
        private void SetSourceParams()
        {
            // TODO: apply resolution, fps
            if (Resolution == null) Resolution = new Resolution(1920, 1080);
            // fps = 15

        }

        #endregion

        #region Internal event handlers for HostingControl and system

        /// <summary>
        /// Adds event handlers for hosting control.
        /// </summary>
        /// <seealso cref="HostingControl"/>
        private void AddHandlers()
        {
            if (_HostingControl == null)
                throw new Exception("Can't add handlers. Hosting control is not set.");

            // Add handlers for VMR purpose
            _HostingControl.Paint += new PaintEventHandler(HostingControl_Paint); // for WM_PAINT
            _HostingControl.Resize += new EventHandler(HostingControl_ResizeMove); // for WM_SIZE
            _HostingControl.Move += new EventHandler(HostingControl_ResizeMove); // for WM_MOVE
            SystemEvents.DisplaySettingsChanged += new EventHandler(SystemEvents_DisplaySettingsChanged); // for WM_DISPLAYCHANGE
            _bHandlersAdded = true;
        }

        /// <summary>
        /// Removes event handlers for hosting control.
        /// </summary>
        /// <seealso cref="HostingControl"/>
        private void RemoveHandlers()
        {
            if (_HostingControl == null)
                throw new Exception("Can't remove handlers. Hosting control is not set.");

            // remove handlers when they are no more needed
            _bHandlersAdded = false;
            _HostingControl.Paint -= new PaintEventHandler(HostingControl_Paint);
            _HostingControl.Resize -= new EventHandler(HostingControl_ResizeMove);
            _HostingControl.Move -= new EventHandler(HostingControl_ResizeMove);
            SystemEvents.DisplaySettingsChanged -= new EventHandler(SystemEvents_DisplaySettingsChanged);
        }


        /// <summary>
        /// Handler of Paint event of HostingControl.
        /// </summary>
        /// <seealso cref="HostingControl"/>
        private void HostingControl_Paint(object sender, PaintEventArgs e)
        {
            // TODO: paint

            if (!_bGraphIsBuilt)
                return; // Do nothing before graph was built

            if (_HostingControl == null) return;

            //Bitmap bmp = new Bitmap(10, 10);
            //lock (e.Graphics)
            {
                //IntPtr hdc = e.Graphics.GetHdc();
                try
                {
                    // TODO: real paint
                    e.Graphics.Clear(Color.Black);
                    var frames = _pipeline.WaitForFrames();
                    Bitmap bmp = GotImage(_RSPath.isIR ? frames.InfraredFrame : frames.ColorFrame  );
                    e.Graphics.DrawImage(bmp, _OutputVideoSize);


                }
                catch (System.Runtime.InteropServices.COMException ex)
                {
                }
                finally
                {
                    //e.Graphics.ReleaseHdc(hdc);
                }
            }
        }

        /// <summary>
        /// Handler of Resize and Move events of HostingControl.
        /// </summary>
        /// <seealso cref="HostingControl"/>
        private void HostingControl_ResizeMove(object sender, EventArgs e)
        {
            if (_HostingControl == null)
                return;

            //int hr = DX.WindowlessCtrl.SetVideoPosition(null, DsRect.FromRectangle(_HostingControl.ClientRectangle));

            if (!_bGraphIsBuilt)
                return; // Do nothing before graph was built

            UpdateOutputVideoSize();

            // Call event handlers (External)
            if (OutputVideoSizeChanged != null)
            {
                OutputVideoSizeChanged(sender, e);
            }
            
            //// Get the bitmap with alpha transparency
            //alphaBitmap = BitmapGenerator.GenerateAlphaBitmap(p.Width, p.Height);

            //// Create a surface from our alpha bitmap
            //surface = new Surface(device, alphaBitmap, Pool.SystemMemory);
            //// Get the unmanaged pointer
            //unmanagedSurface = surface.GetObjectByValue(DxMagicNumber);
        }

        /// <summary>
        /// Handler of SystemEvents.DisplaySettingsChanged.
        /// </summary>
        private void SystemEvents_DisplaySettingsChanged(object sender, EventArgs e)
        {
            if (!_bGraphIsBuilt)
                return; // Do nothing before graph was built

            // TODO: update display
            //_HostingControl.Invalidate(new Rectangle(0, 0, _OutputVideoSize.Width, _OutputVideoSize.Height));
        }

        #endregion

        #region Resolution lists

        #endregion

        #region Coorinate convertions

        // Information: coordinate types:
        // 0) Normalized. [0.0 .. 1.0] for video signal
        // 1) Video.   Related to video stream (e.g. can be VGA (640x480)).
        // 2) Window.  Related to _HostingControl.ClientRectangle
        // 3) Overlay. Related to pixel is the same size as Window-type, but position is related to Video position

        /// <summary>
        /// Converts window coordinates to normalized.
        /// </summary>
        /// <param name="point">Point in window coordinates.</param>
        /// <returns>Normalized coordinates</returns>
        public PointF ConvertWinToNorm(PointF point)
        {

            int window_width = _HostingControl.ClientRectangle.Width;
            int window_height = _HostingControl.ClientRectangle.Height;

            float[] video_rect =
            {
                (window_width - _OutputVideoSize.Width) / 2.0f,
                (window_height - _OutputVideoSize.Height) / 2.0f,
                window_width - (window_width - _OutputVideoSize.Width) / 2.0f,
                window_height - (window_height - _OutputVideoSize.Height) / 2.0f
            };

            return new PointF(
                    (point.X - video_rect[0]) / (video_rect[2] - video_rect[0]),
                    (point.Y - video_rect[1]) / (video_rect[3] - video_rect[1])
                );
        }

        #endregion

        #region Private Helpers

        /// <summary>
        /// Updates output of video size from HostingControl
        /// </summary>
        private void UpdateOutputVideoSize()
        {
            int w = _HostingControl.ClientRectangle.Width;
            int h = _HostingControl.ClientRectangle.Height;

            int video_width = _Resolution.Width;
            int video_height = _Resolution.Height;

            // Check size of video data, to save original ratio
            int window_width = _HostingControl.ClientRectangle.Width;
            int window_height = _HostingControl.ClientRectangle.Height;

            if (window_width == 0 || window_height == 0)
            {
                throw new Exception(@"Incorrect window size (zero).");
            }
            if (video_width == 0 || video_height == 0)
            {
                throw new Exception(@"Incorrect video size (zero).");
            }

            Size result;

            double ratio_video = (double)video_width / video_height;
            double ratio_window = (double)window_width / window_height;

            int real_video_width = Convert.ToInt32(Math.Round(window_height * ratio_video));
            int real_video_height = Convert.ToInt32(Math.Round(window_width / ratio_video));

            if (ratio_video <= ratio_window)
            {
                video_width = Math.Min(window_width, real_video_width); // Check it's not bigger than window's size
                video_height = window_height;
            }
            else
            {
                video_width = window_width;
                video_height = Math.Min(window_height, real_video_height); // Check it's not bigger than window's size
            }

            _OutputVideoSize.Width = video_width;
            _OutputVideoSize.Height = video_height;
            _OutputVideoSize.X = (window_width - video_width) >> 1;
            _OutputVideoSize.Y = (window_height - video_height) >> 1;
        }

        #endregion


        #endregion // Private

        // ====================================================================
    }
}
