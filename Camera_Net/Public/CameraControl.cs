﻿#region License

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

    using System;
    using System.Drawing;
    using System.Windows.Forms;

    #endregion

    /// <summary>
    /// The user control of <see cref="Camera"/> for video output in Windows Forms.
    /// </summary>
    /// <remarks>This class is inherited from <see cref="UserControl"/> class.</remarks>
    /// 
    /// <author> free5lot (free5lot@yandex.ru) </author>
    /// <version> 2013.10.15 </version>
    public partial class CameraControl : UserControl
    {
        // ====================================================================

        #region Public Main

        /// <summary>
        /// Default constructor for <see cref="CameraControl"/> class.
        /// </summary>
        public CameraControl()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Initializes camera, builds and runs graph for control.
        /// </summary>
        /// <param name="cam">RS (device identification) of camera.</param>
        /// <param name="resolution">Resolution of camera's output.</param>
        public void SetCamera(RSDevice cam, Resolution resolution)
        {
            // Close current if it was opened
            CloseCamera();

            if (cam == null)
                return;

            // Create camera object
            _Camera = new Camera();

            // select resolution
            //ResolutionList resolutions = Camera.GetResolutionList(moniker);

            if (resolution != null)
            {
                _Camera.Resolution = resolution;
            }

            // Initialize
            _Camera.Initialize(this, cam);

            // Build and Run graph
            _Camera.BuildGraph();
            _Camera.RunGraph();


            _Camera.OutputVideoSizeChanged += Camera_OutputVideoSizeChanged;
        }

        /// <summary>
        /// Close and dispose all camera and DirectX stuff.
        /// </summary>
        public void CloseCamera()
        {
            if (_Camera == null) return;
            _Camera.StopGraph();
            _Camera.CloseAll();
            _Camera.Dispose();
            _Camera = null;
        }

        #endregion

        // ====================================================================

        #region Public member variables

        /// <summary>
        /// Gets  a value that determines whether or not a Camera object was created.
        /// </summary>
        /// <seealso cref="Camera"/>
        public bool CameraCreated => (_Camera != null);

        /// <summary>
        /// Gets a Camera object.
        /// </summary>
        /// <seealso cref="Camera"/>
        public Camera Camera => _Camera;

        /// <summary>
        /// Gets a camera moniker (device identification).
        /// </summary> 
        public RSDevice Moniker
        {
            get
            {
                _ThrowIfCameraWasNotCreated();

                return _Camera.RsPath;
            }
        }

        /// <summary>
        /// Gets or sets a resolution of camera's output.
        /// </summary>
        /// <seealso cref="ResolutionListRGB"/>
        public Resolution Resolution
        {
            get
            {
                _ThrowIfCameraWasNotCreated();

                return _Camera.Resolution;
            }
            set
            {
                _ThrowIfCameraWasNotCreated();

                _Camera.Resolution = value;
            }
        }

        /// <summary>
        /// Gets a list of available resolutions (in RGB format).
        /// </summary>        
        public ResolutionList ResolutionListRGB
        {
            get
            {
                _ThrowIfCameraWasNotCreated();

                return _Camera.ResolutionListRgb;
            }
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

        #region Property pages (various settings dialogs)

        #endregion

        // ====================================================================

        #region Spanshot (screenshots) frame

        /// <summary>
        /// Make snapshot of output image. Slow, but includes all graph's effects.
        /// </summary>
        /// <returns>Snapshot as a Bitmap</returns>
        /// <seealso cref="SnapshotSourceImage"/>
        public Bitmap SnapshotOutputImage()
        {
            _ThrowIfCameraWasNotCreated();

            return _Camera.SnapshotOutputImage();
        }

        /// <summary>
        /// Make snapshot of source image. Much faster than SnapshotOutputImage.
        /// </summary>
        /// <returns>Snapshot as a Bitmap</returns>
        /// <seealso cref="SnapshotOutputImage"/>
        public Bitmap SnapshotSourceImage()
        {
            _ThrowIfCameraWasNotCreated();

            return _Camera.SnapshotSourceImage();
        }

        #endregion

        // ====================================================================

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
        public PointF ConvertWinToNorm(PointF p)
        {
            _ThrowIfCameraWasNotCreated();

            return _Camera.ConvertWinToNorm(p);
        }

        /// <summary>
        /// Sets camera output rect (zooms to selected rect).
        /// </summary>
        /// <param name="zoomRect">Rectangle for zooming in video coordinates.</param>
        public void ZoomToRect(Rectangle ZoomRect)
        {
            _ThrowIfCameraWasNotCreated();

            //_Camera.ZoomToRect(ZoomRect);
        }

        #endregion

        // ====================================================================

        #region Public Static functions

        /// <summary>
        /// Returns Moniker (device identification) of camera from device index.
        /// </summary>
        /// <param name="iDeviceIndex">Index (Zero-based) in list of available devices with VideoInputDevice filter category.</param>
        /// <returns>Moniker (device identification) of device</returns>
        public static RSDevice GetDeviceMoniker(int iDeviceNum)
        {
            return Camera.GetDeviceMoniker(iDeviceNum);
        }

        /// <summary>
        /// Returns available resolutions with RGB color system for device moniker.
        /// </summary>
        /// <param name="moniker">Moniker (device identification) of camera.</param>
        /// <returns>List of resolutions with RGB color system of device</returns>
        public static ResolutionList GetResolutionList(RSDevice moniker)
        {
            return Camera.GetResolutionList(moniker);
        }

        #endregion

        // ====================================================================

        #region Private stuff

        #region Private members

        /// <summary>
        /// Camera object (user control is a wrapper for it).
        /// </summary>
        private Camera _Camera = null;

        /// <summary>
        /// Message for exception when functions are called if camera not being created.
        /// </summary>
        private const string CameraWasNotCreatedMessage = @"Camera is not created.";

        /// <summary>
        /// Checks if camera is created and throws ApplicationException if not.
        /// </summary>
        private void _ThrowIfCameraWasNotCreated()
        {
            if (!CameraCreated)
                throw new Exception(CameraWasNotCreatedMessage);
        }

        #endregion

        /// <summary>
        /// Event handler for OutputVideoSizeChanged event.
        /// </summary>
        private void Camera_OutputVideoSizeChanged(object sender, EventArgs e)
        {
            // Call event handlers (External)
            if (OutputVideoSizeChanged != null)
            {
                OutputVideoSizeChanged(sender, e);
            }
        }

        #endregion

        // ====================================================================

    }
}
