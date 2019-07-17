using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;

using ipp;

using MHApi.Imaging;
using MHApi.DrewsClasses;

namespace SleepTracker.Tracking
{
    public unsafe class Tracker90mmDish : IDisposable
    {
        #region Members

        /// <summary>
        /// Our background model
        /// </summary>
        protected SelectiveUpdateBGModel _bgModel;

        /// <summary>
        /// Internal volatile buffer used for image calculations
        /// </summary>
        protected Image8 _calc;

        /// <summary>
        /// The image after foreground subtraction
        /// </summary>
        protected Image8 _foreground;

        /// <summary>
        /// Used to store the label marker image
        /// </summary>
        protected Image8 _labelMarkers;

        /// <summary>
        /// Buffer used internally by ipp's label markers
        /// function
        /// </summary>
        protected byte* _markerBuffer;

        /// <summary>
        /// Pointer to the momentState structure used internally by ipp
        /// </summary>
        protected IppiMomentState_64s* _momentState;

        /// <summary>
        /// The current frame index
        /// </summary>
        protected int _frame;

        /// <summary>
        /// The threshold used to extract the foreground
        /// </summary>
        protected byte _threshold;

        /// <summary>
        /// The minimum area a blob can have and still
        /// be considered a fish
        /// </summary>
        protected int _minArea;

        protected int _maxArea;

        /// <summary>
        /// If a blob has an area larger than this it
        /// will be used to update knowledge about the 
        /// tracking region
        /// </summary>
        protected int _fullTrustMinArea;

        /// <summary>
        /// The number of frames to use in our background model
        /// </summary>
        protected int _framesInBackground;

        /// <summary>
        /// The number of frames before we actually start tracking
        /// </summary>
        protected int _framesInitialBackground;

        /// <summary>
        /// The fish extracted from the previous frame
        /// or null, if no trustworthy fish was found
        /// </summary>
        protected BlobWithMoments _previousFish;

        /// <summary>
        /// The ROI representing the whole image
        /// </summary>
        protected readonly IppiROI _imageROI;

        #endregion

        #region Properties

        /// <summary>
        /// The current foreground
        /// </summary>
        public Image8 Foreground
        {
            get
            {
                return _foreground;
            }
        }

        /// <summary>
        /// The current background
        /// </summary>
        public Image8 Background
        {
            get
            {
                return _bgModel.Background;
            }
        }

        /// <summary>
        /// Stores the label-marker image
        /// </summary>
        public Image8 LabelMarkers
        {
            get
            {
                return _labelMarkers;
            }
        }

        /// <summary>
        /// The threshold used to extract the foreground
        /// </summary>
        public byte Threshold
        {
            get
            {
                return _threshold;
            }
            set
            {
                _threshold = value;
            }
        }

        /// <summary>
        /// The minimum area a blob can have and
        /// still be considered a fish
        /// </summary>
        public int MinArea
        {
            get
            {
                return _minArea;
            }
            set
            {
                _minArea = value;
            }
        }

        /// <summary>
        /// The maximum area a blob can have and
        /// still be considered a fish
        /// </summary>
        public int MaxArea
        {
            get
            {
                return _maxArea;
            }
            set
            {
                _maxArea = value;
            }
        }

        /// <summary>
        /// If a blob has an area larger than this it
        /// will be used to update knowledge about the 
        /// tracking region
        /// </summary>
        public int FullTrustMinArea
        {
            get
            {
                return _fullTrustMinArea;
            }
            set
            {
                _fullTrustMinArea = value;
            }
        }

        /// <summary>
        /// The number of frames it takes for our background to approximate
        /// the foreground at 63.2% (since exponential decay of pixels
        /// into the background)
        /// It takes 4.6 times as many frames to reach 99% of the foreground!!
        /// </summary>
        public int FramesInBackground
        {
            get
            {
                return _framesInBackground;
            }
            set
            {
                if (_frame > 0)
                    throw new InvalidOperationException("Can't update frames in background during tracking");
                _framesInBackground = value;
            }
        }

        /// <summary>
        /// The number of frames before we start tracking
        /// </summary>
        public int FramesInitialBackground
        {
            get
            {
                return _framesInitialBackground;
            }
            set
            {
                if (_frame > 0)
                    throw new InvalidOperationException("Can't update initial background frame number during tracking");
                _framesInitialBackground = value;
            }
        }

        /// <summary>
        /// The current frame
        /// </summary>
        public int Frame
        {
            get
            {
                return _frame;
            }
        }

        #endregion

        #region Constructor

        public Tracker90mmDish(int imageWidth, int imageHeight)
        {
            _foreground = new Image8(imageWidth, imageHeight);
            _calc = new Image8(imageWidth, imageHeight);
            _labelMarkers = new Image8(imageWidth, imageHeight);
            int bufferSize = 0;
            IppHelper.IppCheckCall(cv.ippiLabelMarkersGetBufferSize_8u_C1R(new IppiSize(imageWidth, imageHeight), &bufferSize));
            _markerBuffer = (byte*)Marshal.AllocHGlobal(bufferSize);
            fixed (IppiMomentState_64s** ppState = &_momentState)
            {
                //let ipp decide whether to give accurate or fast results
                IppHelper.IppCheckCall(ip.ippiMomentInitAlloc_64s(ppState, IppHintAlgorithm.ippAlgHintNone));
            }
            _frame = 0;
            //populate tracking parameters with default values
            _threshold = 5;
            _minArea = 10;
            _maxArea = 300;
            _fullTrustMinArea = 20;
            _imageROI = new IppiROI(0, 0, imageWidth, imageHeight);
            //The following calculation for FramesInBackground means that after ~30s of movie
            //a stationary object will have dissappeared into the background (at 63% level)
            FramesInBackground = (int)((30 * 240));
            FramesInitialBackground = 2 * FramesInBackground;
        }

        #endregion

        #region Methods

        public BlobWithMoments Track(Image8 image)
        {
            throw new NotImplementedException("Currently this method should not be called - extract fish has been modified");

            
        }

        /// <summary>
        /// Implements a "greater than" threshold like MATLABS
        /// im2bw function
        /// </summary>
        /// <param name="im">The image to threshold</param>
        /// <param name="region">The ROI in which to perform the operation</param>
        /// <param name="threshold">The threshold to apply</param>
        protected void Im2Bw(Image8 im, IppiROI region)
        {
            IppHelper.IppCheckCall(ip.ippiThreshold_LTVal_8u_C1IR(im[region.TopLeft], im.Stride, region.Size, (byte)(_threshold+1), 0));
            IppHelper.IppCheckCall(ip.ippiThreshold_GTVal_8u_C1IR(im[region.TopLeft], im.Stride, region.Size, _threshold, 255));
        }

        /// <summary>
        /// Performs a 3x3 closing operation on an image
        /// </summary>
        /// <param name="im">The (thresholded) image to close</param>
        /// <param name="region">The ROI in which to perform the operation</param>
        protected void Close3x3(Image8 im, IppiROI region)
        {
            IppHelper.IppCheckCall(ip.ippiDilate3x3_8u_C1IR(im[region.TopLeft.x + 1, region.TopLeft.y + 1], im.Stride, new IppiSize(region.Width - 2, region.Height - 2)));
            IppHelper.IppCheckCall(ip.ippiErode3x3_8u_C1IR(im[region.TopLeft.x + 1, region.TopLeft.y + 1], im.Stride, new IppiSize(region.Width - 2, region.Height - 2)));
        }

        /// <summary>
        /// Extracts a fish (candidate) from an image by performing background subtraction, noise filtering, thresholding and closing to obtain a foreground
        /// followed by marker extraction.
        /// </summary>
        /// <param name="im">The image to extract the fish from</param>
        /// <param name="region">The ROI to search</param>
        /// <returns>The most likely fish blob or null if no suitable candidate was found</returns>
        protected BlobWithMoments ExtractFish(/*Image8 im,*/ IppiROI region)
        {
            int nMarkers = 0;
            BlobWithMoments[] blobsDetected;

            

            
            //Copy foreground to marker and label connected components
            //IppHelper.IppCheckCall(ip.ippiCopy_8u_C1R(_foreground[region.TopLeft], _foreground.Stride, _labelMarkers[region.TopLeft], _labelMarkers.Stride, region.Size));
            IppHelper.IppCheckCall(cv.ippiLabelMarkers_8u_C1IR(_foreground[region.TopLeft], _foreground.Stride, region.Size, 1, 254, IppiNorm.ippiNormInf, &nMarkers, _markerBuffer));
            //loop over returned markers and use ipp to extract blobs
            if (nMarkers > 0)
            {
                if (nMarkers > 254)
                    nMarkers = 254;
                blobsDetected = new BlobWithMoments[nMarkers];                           
                for (int i = 1; i <= nMarkers ; i++)
                {
                    //label all pixels with the current marker as 255 and others as 0
                    IppHelper.IppCheckCall(ip.ippiCompareC_8u_C1R(_foreground[region.TopLeft], _foreground.Stride, (byte)i, _calc[region.TopLeft], _calc.Stride, region.Size, IppCmpOp.ippCmpEq));
                    //calculate image moments
                    IppHelper.IppCheckCall(ip.ippiMoments64s_8u_C1R(_calc[region.TopLeft], _calc.Stride, region.Size, _momentState));
                    //retrieve moments
                    long m00 = 0;
                    long m10 = 0;
                    long m01 = 0;
                    long m20 = 0;
                    long m02 = 0;
                    long m11 = 0;
                    long m30 = 0;
                    long m03 = 0;
                    long m21 = 0;
                    long m12 = 0;
                    ip.ippiGetSpatialMoment_64s(_momentState, 0, 0, 0, new IppiPoint(region.X,region.Y), &m00, 0);
                    //since our input image is not 0s and 1s but 0s and 255s we have to divide by 255 in order to re-normalize our moments
                    System.Diagnostics.Debug.Assert(m00 % 255 == 0, "M00 was not a multiple of 255");
                    m00 /= 255;                   
                    //only retrieve other moments if this is a "fish candidate"
                    if (m00 > MinArea && m00<=MaxArea)
                    {
                        ip.ippiGetSpatialMoment_64s(_momentState, 1, 0, 0, new IppiPoint(region.X, region.Y), &m10, 0);
                        m10 /= 255;
                        ip.ippiGetSpatialMoment_64s(_momentState, 0, 1, 0, new IppiPoint(region.X, region.Y), &m01, 0);
                        m01 /= 255;
                        ip.ippiGetSpatialMoment_64s(_momentState, 2, 0, 0, new IppiPoint(region.X, region.Y), &m20, 0);
                        m20 /= 255;
                        ip.ippiGetSpatialMoment_64s(_momentState, 0, 2, 0, new IppiPoint(region.X, region.Y), &m02, 0);
                        m02 /= 255;
                        ip.ippiGetSpatialMoment_64s(_momentState, 1, 1, 0, new IppiPoint(region.X, region.Y), &m11, 0);
                        m11 /= 255;
                        ip.ippiGetSpatialMoment_64s(_momentState, 3, 0, 0, new IppiPoint(region.X, region.Y), &m30, 0);
                        m30 /= 255;
                        ip.ippiGetSpatialMoment_64s(_momentState, 0, 3, 0, new IppiPoint(region.X, region.Y), &m03, 0);
                        m03 /= 255;
                        ip.ippiGetSpatialMoment_64s(_momentState, 2, 1, 0, new IppiPoint(region.X, region.Y), &m21, 0);
                        m21 /= 255;
                        ip.ippiGetSpatialMoment_64s(_momentState, 1, 2, 0, new IppiPoint(region.X, region.Y), &m12, 0);
                        m12 /= 255;
                        blobsDetected[i - 1] = new BlobWithMoments(m00, m10, m01, m20, m11, m02, m30, m03, m21, m12);
                        //Determine bounding box of the blob. The following seems kinda retarded as Ipp must already
                        //have obtained that information before so maybe there is some way to actually retrieve it??
                        //Do linescans using ipp's sum function starting from the blobs centroid until we hit a line
                        //the sum of which is 0
                        int xStart, xEnd, yStart, yEnd;
                        double sum = 1;
                        IppiPoint centroid = blobsDetected[i - 1].Centroid;
                        xStart = centroid.x-5;
                        xEnd = centroid.x + 5;
                        yStart = centroid.y - 5;
                        yEnd = centroid.y + 5;
                        //in the following loops we PRE-increment, whence we stop the loop if we are at one coordinate short of the ends
                        //find xStart
                        while (sum > 0 && xStart > region.X+4)
                        {
                            xStart -= 5;
                            IppHelper.IppCheckCall(ip.ippiSum_8u_C1R(_calc[xStart, region.Y], _calc.Stride, new IppiSize(1, region.Height), &sum));
                        }
                        xStart += 1;//we have a sum of 0, so go back one line towards the centroid
                        //find xEnd
                        sum = 1;
                        while (sum > 0 && xEnd < region.X + region.Width-6)
                        {
                            xEnd += 5;
                            IppHelper.IppCheckCall(ip.ippiSum_8u_C1R(_calc[xEnd, region.Y], _calc.Stride, new IppiSize(1, region.Height), &sum));
                        }
                        xEnd -= 1;//we have sum of 0, so go back one line towards the centroid
                        //find yStart - we can limit our x-search-space as we already have those boundaries
                        sum = 1;
                        while (sum > 0 && yStart > region.Y+4)
                        {
                            yStart -= 5;
                            IppHelper.IppCheckCall(ip.ippiSum_8u_C1R(_calc[xStart, yStart], _calc.Stride, new IppiSize(xEnd - xStart + 1, 1), &sum));
                        }
                        yStart += 1;
                        //find yEnd - again limit summation to x-search-space
                        sum = 1;
                        while (sum > 0 && yEnd < region.Y + region.Height-6)
                        {
                            yEnd += 5;
                            IppHelper.IppCheckCall(ip.ippiSum_8u_C1R(_calc[xStart, yEnd], _calc.Stride, new IppiSize(xEnd - xStart + 1, 1), &sum));
                        }
                        yEnd -= 1;
                        blobsDetected[i - 1].BoundingBox = new IppiRect(xStart, yStart, xEnd - xStart + 1, yEnd - yStart + 1);
                    }
                    else
                        blobsDetected[i - 1] = new BlobWithMoments();                    
                }
            }
            else
                return null;
            long maxArea = 0;
            int maxIndex = -1;
            for (int i = 0; i < blobsDetected.Length; i++)
            {
                if (blobsDetected[i] == null)
                    break;
                //Simply note down the largest blob
                if (blobsDetected[i].Area > maxArea)
                {
                    maxArea = blobsDetected[i].Area;
                    maxIndex = i;
                }
            }
            
            if (maxArea<MinArea)
                return null;
            else
                return blobsDetected[maxIndex];
        }

        #endregion

        #region Cleanup

        public bool IsDisposed { get; private set; }

        public void Dispose()
        {
            if (IsDisposed)
                return;
            Dispose(true);
            IsDisposed = true;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_bgModel != null)
            {
                _bgModel.Dispose();
                _bgModel = null;
            }
            if (_calc != null)
            {
                _calc.Dispose();
                _calc = null;
            }
            if (_foreground != null)
            {
                _foreground.Dispose();
                _foreground = null;
            }
            if (_markerBuffer != null)
            {
                Marshal.FreeHGlobal((IntPtr)_markerBuffer);
                _markerBuffer = null;
            }
            if (_momentState != null)
            {
                ip.ippiMomentFree_64s(_momentState);
                _momentState = null;
            }
        }

        ~Tracker90mmDish()
        {
            if (!IsDisposed)
            {
                System.Diagnostics.Debug.WriteLine("Tracker not properly disposed");
                Dispose(false);
            }
        }

        #endregion
    }
}
