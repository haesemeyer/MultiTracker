using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using IMAQdxAPI;
using MHApi.DrewsClasses;

namespace Drew.IMAQ.Cameras {

    public class CameraException : Exception { 
        public CameraException() : base() { }
        public CameraException(string message) : base(message) { }
    };

    public unsafe class IMAQCamera : IDisposable {
        uint session;

        int width;
        public int Width {
            get {
                return width;
                //uint width;
                //IMAQdx.IMAQdxGetAttribute(session, IMAQdx.IMAQdxAttributeWidth, IMAQdx.IMAQdxValueType.IMAQdxValueTypeU32, out width);
                //return (int)width;
            }
            set {
                width = (int)(Math.Ceiling(value / 4.0) * 4);
                IMAQdx.IMAQdxSetAttribute(session, IMAQdx.IMAQdxAttributeWidth, IMAQdx.IMAQdxValueType.IMAQdxValueTypeU32, width);
                bufferLength = (uint)(width * height);
            }
        }

        int height;
        public int Height {
            get {
                return height;
                //uint height;
                //IMAQdx.IMAQdxGetAttribute(session, IMAQdx.IMAQdxAttributeHeight, IMAQdx.IMAQdxValueType.IMAQdxValueTypeU32, out height);
                //return (int)height;
            }
            set {
                height = value;
                IMAQdx.IMAQdxSetAttribute(session, IMAQdx.IMAQdxAttributeHeight, IMAQdx.IMAQdxValueType.IMAQdxValueTypeU32, height);
                bufferLength = (uint)(width * height);
            }
        }

        uint bufferLength;

        public IMAQCamera(string name) {
            CheckError(IMAQdx.IMAQdxOpenCamera(name, IMAQdx.IMAQdxCameraControlMode.IMAQdxCameraControlModeController, out session));
            uint widthTemp;
            uint heightTemp;
            IMAQdx.IMAQdxGetAttribute(session, IMAQdx.IMAQdxAttributeWidth, IMAQdx.IMAQdxValueType.IMAQdxValueTypeU32, out widthTemp);
            IMAQdx.IMAQdxGetAttribute(session, IMAQdx.IMAQdxAttributeHeight, IMAQdx.IMAQdxValueType.IMAQdxValueTypeU32, out heightTemp);
            width = (int)widthTemp;
            height = (int)heightTemp;
            if (width % 4 != 0)
                Width = (int)(Math.Ceiling(width / 4.0) * 4);
            bufferLength = (uint)(width * height);
        }

        bool _isAcquiring;

        public void Start(uint bufferCount) {
            if (_isAcquiring)
                return;
            CheckError(IMAQdx.IMAQdxConfigureAcquisition(session, true, bufferCount));
            CheckError(IMAQdx.IMAQdxStartAcquisition(session));
            _isAcquiring = true;
        }

        public void Stop() {
            if (!_isAcquiring)
                return;
            CheckError(IMAQdx.IMAQdxStopAcquisition(session));
            CheckError(IMAQdx.IMAQdxUnconfigureAcquisition(session));
            _isAcquiring = false;
        }

        /*

        /// <summary>
        /// Extract the next image
        /// </summary>
        /// <returns></returns>
        public Image8 Extract() {
            var image = new Image8(width, height);
            var bufferLength = (uint)(width * height);
            uint frameIndex;
            CheckError(IMAQdx.IMAQdxGetImageData(session, image.Image, bufferLength, IMAQdx.IMAQdxBufferNumberMode.IMAQdxBufferNumberModeNext, 0, out frameIndex));
            return image;
        }

        /// <summary>
        /// Extract the next image and return its index
        /// </summary>
        /// <param name="frameIndex"></param>
        /// <returns></returns>
        public Image8 Extract(out uint frameIndex) {
            var image = new Image8(width, height);
            var bufferLength = (uint)(width * height);
            CheckError(IMAQdx.IMAQdxGetImageData(session, image.Image, bufferLength, IMAQdx.IMAQdxBufferNumberMode.IMAQdxBufferNumberModeNext, 0, out frameIndex));
            return image;
        }

        /// <summary>
        /// Extract the image with the specified index
        /// </summary>
        /// <param name="frameIndex"></param>
        /// <returns></returns>
        public Image8 Extract(uint frameIndex) {
            var image = new Image8(width, height);
            uint actualFrameIndex;
            CheckError(IMAQdx.IMAQdxGetImageData(session, image.Image, bufferLength, IMAQdx.IMAQdxBufferNumberMode.IMAQdxBufferNumberModeBufferNumber, frameIndex, out actualFrameIndex));
            if (actualFrameIndex != frameIndex)
                System.Diagnostics.Debug.WriteLine("Warning: Expected frameIndex " + frameIndex + ", got frameIndex " + actualFrameIndex);
            return image;
        }

        /// <summary>
        /// Extract the next image into an existing Image8
        /// </summary>
        /// <param name="imageToReuse"></param>
        public void Extract(Image8 imageToReuse) {
            if (imageToReuse.Width != width || imageToReuse.Height != height)
                throw new Exception("Cannot reuse image, dimensions should be " + width + " x " + height + ", not " + imageToReuse.Width + " x " + imageToReuse.Height);
            uint frameIndex;
            CheckError(IMAQdx.IMAQdxGetImageData(session, imageToReuse.Image, bufferLength, IMAQdx.IMAQdxBufferNumberMode.IMAQdxBufferNumberModeNext, 0, out frameIndex));
        }

        /// <summary>
        /// Extract the next image into an existing Image8 and return its index
        /// </summary>
        /// <param name="frameIndex"></param>
        /// <returns></returns>
        public void Extract(Image8 imageToReuse, out uint frameIndex) {
            if (imageToReuse.Width != width || imageToReuse.Height != height)
                throw new Exception("Cannot reuse image, dimensions should be " + width + " x " + height + ", not " + imageToReuse.Width + " x " + imageToReuse.Height);
            CheckError(IMAQdx.IMAQdxGetImageData(session, imageToReuse.Image, bufferLength, IMAQdx.IMAQdxBufferNumberMode.IMAQdxBufferNumberModeNext, 0, out frameIndex));
        }*/

        /// <summary>
        /// Extract the image with the specified index into an existing Image8
        /// </summary>
        /// <param name="frameIndex"></param>
        /// <returns></returns>
        public void Extract(Image8 imageToReuse, uint frameIndex) {
            uint actualFrameIndex;
            CheckError(IMAQdx.IMAQdxGetImageData(session, imageToReuse.Image, bufferLength, IMAQdx.IMAQdxBufferNumberMode.IMAQdxBufferNumberModeBufferNumber, frameIndex, out actualFrameIndex));
            if (actualFrameIndex != frameIndex)
                System.Diagnostics.Debug.WriteLine("Warning: Expected frameIndex " + frameIndex + ", got frameIndex " + actualFrameIndex);
        }

        //public void Start() {
        //    if (thread != null) return;
        //    thread=new EZThread(ThreadRun);
        //}

        //public void Stop() {
        //    if (thread == null) return;
        //    thread.Dispose();
        //}

        //void ThreadRun(AutoResetEvent stop) {
        //    CheckError(IMAQdx.IMAQdxConfigureAcquisition(session, true, 100));
        //    CheckError(IMAQdx.IMAQdxStartAcquisition(session));
        //    var width = Width;
        //    var height = Height;
        //    if (width % 4 != 0)
        //        throw new Exception("Width must be a multiple of 4");
        //    var image = new Image8(width, height);
        //    var bufferlength = (uint)(width*height);
        //    uint frameIndex = 0;
        //    while (!stop.WaitOne(0)) {
        //        IMAQdx.IMAQdxGetImageData(session, image.Image, bufferlength, IMAQdx.IMAQdxBufferNumberMode.IMAQdxBufferNumberModeBufferNumber, frameIndex, out frameIndex);

        //        //const double frameRateInterval = 0.5;
        //        //DWORD newTick = GetTickCount();
        //        //if ((double)(newTick - lastTick) / 1000.0 >= frameRateInterval) {
        //        //    double frameRate = ((double)(bufferNumber - lastBufferNumber) / (double)(newTick - lastTick)) * 1000.0;
        //        //    FrameRateString.Format("%.6f", frameRate);
        //        //    lastTick = newTick;
        //        //    lastBufferNumber = bufferNumber;
        //        //    actualBufferNumber = bufferNumber;
        //        //    PostMessage(WM_UPDATEDIALOG);

        //        //}
        //    }
        //    CheckError(IMAQdx.IMAQdxUnconfigureAcquisition(session));
        //}

        static void CheckError(IMAQdx.IMAQdxError error) {
            if (error != 0)
                throw new CameraException("Error " + error);
        }

        bool isDisposed;
        public void Dispose() {
            if (isDisposed) return;
            Stop();
            CheckError(IMAQdx.IMAQdxCloseCamera(session));
            isDisposed = true;
        }
    }
}
