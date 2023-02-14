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


namespace Camera_NET
{
    #region Using directives

    // Use Intel.Realsense (Apache License 2.0)
    using Intel.RealSense;
    // Microsoft.Win32 is used for SystemEvents namespace
    using Microsoft.Win32;
    using System;
    using System.Drawing;
    using System.Drawing.Imaging;
    using System.Windows.Forms;

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

        /// <summary>
        /// Private field. Use the public property <see cref="HostingControl"/> for access to this value.
        /// </summary>
        private Control _hostingControl = null;

        /// <summary>
        /// Private field. Use the public property <see cref="RsPath"/> for access to this value.
        /// </summary>
        private RSDevice _rsPath = null;

        /// <summary>
        /// Private field. Use the public property <see cref="Resolution"/> for access to this value.
        /// </summary>
        private Resolution _resolution = new Resolution(1920, 1080);

        /// <summary>
        /// Private field. Use the public property <see cref="ResolutionList"/> for access to this value.
        /// </summary>
        private ResolutionList _resolutionList = new ResolutionList { new Resolution(1920, 1080) };

        /// <summary>
        /// Private field. Use the public property <see cref="OutputVideoSize"/> for access to this value.
        /// </summary>
        private Rectangle _outputVideoSize;

        private static readonly RsServing VideoRsServing = RsServing.Instance;
        private int _streamIndex = -1;
        private Bitmap _outputVideo = null;

        #endregion

        // ====================================================================

        #region Internal stuff

        /// <summary>
        /// Private field. Was the graph built or not.
        /// </summary>
        internal bool BGraphIsBuilt = false;

        /// <summary>
        /// Private field. Were handlers added or not. Needed to remove delegates
        /// </summary>
        internal bool BHandlersAdded = false;

        //internal Timer _Timer = new Timer();

        #endregion

        // ====================================================================

        #region Public properties

        /// <summary>
        /// Gets a control that is used for hosting camera's output.
        /// </summary>
        public Control HostingControl
        => _hostingControl;

        /// <summary>
        /// Gets an identifier of camera.
        /// </summary>
        public RSDevice RsPath => _rsPath;

        /// <summary>
        /// Gets or sets a resolution of camera's output.
        /// </summary>
        /// <seealso cref="ResolutionListRgb"/>
        public Resolution Resolution
        {
            get => _resolution;
            set
            {
                // Change of resolution is not allowed after graph's built
                if (BGraphIsBuilt)
                    throw new Exception(@"Change of resolution is not allowed after graph's built.");
                _resolution = value;
            }
        }

        /// <summary>
        /// Gets a list of available resolutions (in RGB format).
        /// </summary>        
        public ResolutionList ResolutionListRgb => _resolutionList;

        public int MinExp => VideoRsServing.MinExp;
        public int MaxExp => VideoRsServing.MaxExp;

        public int Exp
        {
            get => VideoRsServing.Exp;
            set => VideoRsServing.Exp = value;
        }

        public void PushExp(int exp)
        {
            VideoRsServing.PushExp(exp);
        }

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
            if (iDeviceIndex >= VideoRsServing.Devices.Length)
            {
                throw new Exception(@"No video capture devices found at that index.");
            }
            return VideoRsServing.Devices[iDeviceIndex];
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
            _rsPath = cam ?? throw new Exception(@"Camera's id should be set.");
            _hostingControl = hControl ?? throw new Exception(@"Hosting control should be set.");
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
            BGraphIsBuilt = false;

            // stop rendering
            // TODO: stop
            StopGraph();

            if (BHandlersAdded)
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
            BGraphIsBuilt = false;

            try
            {
                SetSourceParams();
                UpdateOutputVideoSize();
                AddHandlers();

                // -------------------------------------------------------
                BGraphIsBuilt = true;
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
            // TODO: run
            _streamIndex = VideoRsServing.Start(_rsPath);
        }

        private void GotFrame(object sender, int e)
        {
            if (_streamIndex == -1) return;
            if (_streamIndex != e) return;

            _outputVideo = GotImage(VideoRsServing.GetFrame(_streamIndex));

            _hostingControl.Invalidate();
        }

        private Bitmap GotImage(VideoFrame frame)
        {
            byte[] outBuffer = new byte[frame.Width * frame.Height * 3];

            frame.CopyTo(outBuffer);

            if (_rsPath.isIR) // Convert grayscale to RGB
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
            if (!BGraphIsBuilt) return;
            VideoRsServing.Stop();
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
            if (!BGraphIsBuilt || _outputVideo == null) throw new Exception("Video was not initialized");
            lock (_outputVideo)
            {
                return _outputVideo.Clone(new Rectangle(0, 0, _outputVideo.Width, _outputVideo.Height),
                    PixelFormat.Format24bppRgb);
            }
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
            if (_hostingControl == null)
                throw new Exception("Can't add handlers. Hosting control is not set.");

            // Add handlers for VMR purpose
            _hostingControl.Paint += new PaintEventHandler(HostingControl_Paint); // for WM_PAINT
            _hostingControl.Resize += new EventHandler(HostingControl_ResizeMove); // for WM_SIZE
            _hostingControl.Move += new EventHandler(HostingControl_ResizeMove); // for WM_MOVE
            SystemEvents.DisplaySettingsChanged += new EventHandler(SystemEvents_DisplaySettingsChanged); // for WM_DISPLAYCHANGE
            VideoRsServing.GotFrame += new EventHandler<int>(GotFrame);
            BHandlersAdded = true;
        }

        /// <summary>
        /// Removes event handlers for hosting control.
        /// </summary>
        /// <seealso cref="HostingControl"/>
        private void RemoveHandlers()
        {
            if (_hostingControl == null)
                throw new Exception("Can't remove handlers. Hosting control is not set.");

            // remove handlers when they are no more needed
            BHandlersAdded = false;
            _hostingControl.Paint -= new PaintEventHandler(HostingControl_Paint);
            _hostingControl.Resize -= new EventHandler(HostingControl_ResizeMove);
            _hostingControl.Move -= new EventHandler(HostingControl_ResizeMove);
            SystemEvents.DisplaySettingsChanged -= new EventHandler(SystemEvents_DisplaySettingsChanged);
            VideoRsServing.GotFrame += new EventHandler<int>(GotFrame);
        }


        /// <summary>
        /// Handler of Paint event of HostingControl.
        /// </summary>
        /// <seealso cref="HostingControl"/>
        private void HostingControl_Paint(object sender, PaintEventArgs e)
        {
            // TODO: paint

            if (!BGraphIsBuilt)
                return; // Do nothing before graph was built

            if (_hostingControl == null) return;

            e.Graphics.Clear(Color.Black);
            if (_outputVideo != null)
                lock (_outputVideo)
                {
                    e.Graphics.DrawImage(_outputVideo, _outputVideoSize);
                }
        }

        /// <summary>
        /// Handler of Resize and Move events of HostingControl.
        /// </summary>
        /// <seealso cref="HostingControl"/>
        private void HostingControl_ResizeMove(object sender, EventArgs e)
        {
            if (_hostingControl == null)
                return;

            //int hr = DX.WindowlessCtrl.SetVideoPosition(null, DsRect.FromRectangle(_HostingControl.ClientRectangle));

            if (!BGraphIsBuilt)
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
            if (!BGraphIsBuilt)
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

            int windowWidth = _hostingControl.ClientRectangle.Width;
            int windowHeight = _hostingControl.ClientRectangle.Height;

            float[] videoRect =
            {
                (windowWidth - _outputVideoSize.Width) / 2.0f,
                (windowHeight - _outputVideoSize.Height) / 2.0f,
                windowWidth - (windowWidth - _outputVideoSize.Width) / 2.0f,
                windowHeight - (windowHeight - _outputVideoSize.Height) / 2.0f
            };

            return new PointF(
                    (point.X - videoRect[0]) / (videoRect[2] - videoRect[0]),
                    (point.Y - videoRect[1]) / (videoRect[3] - videoRect[1])
                );
        }

        #endregion

        #region Private Helpers

        /// <summary>
        /// Updates output of video size from HostingControl
        /// </summary>
        private void UpdateOutputVideoSize()
        {
            int w = _hostingControl.ClientRectangle.Width;
            int h = _hostingControl.ClientRectangle.Height;

            int videoWidth = _resolution.Width;
            int videoHeight = _resolution.Height;

            // Check size of video data, to save original ratio
            int windowWidth = _hostingControl.ClientRectangle.Width;
            int windowHeight = _hostingControl.ClientRectangle.Height;

            if (windowWidth == 0 || windowHeight == 0)
            {
                throw new Exception(@"Incorrect window size (zero).");
            }
            if (videoWidth == 0 || videoHeight == 0)
            {
                throw new Exception(@"Incorrect video size (zero).");
            }

            double ratioVideo = (double)videoWidth / videoHeight;
            double ratioWindow = (double)windowWidth / windowHeight;

            int realVideoWidth = Convert.ToInt32(Math.Round(windowHeight * ratioVideo));
            int realVideoHeight = Convert.ToInt32(Math.Round(windowWidth / ratioVideo));

            if (ratioVideo <= ratioWindow)
            {
                videoWidth = Math.Min(windowWidth, realVideoWidth); // Check it's not bigger than window's size
                videoHeight = windowHeight;
            }
            else
            {
                videoWidth = windowWidth;
                videoHeight = Math.Min(windowHeight, realVideoHeight); // Check it's not bigger than window's size
            }

            _outputVideoSize.Width = videoWidth;
            _outputVideoSize.Height = videoHeight;
            _outputVideoSize.X = (windowWidth - videoWidth) >> 1;
            _outputVideoSize.Y = (windowHeight - videoHeight) >> 1;
        }

        #endregion


        #endregion // Private

        // ====================================================================
    }
}
