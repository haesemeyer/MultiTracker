using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using System.IO;
using System.Reflection;

using System.Threading.Tasks;
//using System.Runtime.InteropServices;

using System.ComponentModel;


using ipp;

using MHApi.Imaging;
using MHApi.DrewsClasses;


namespace SleepTracker.Tracking
{
    public enum TrackMethods { PerWell, OnWholeImage, Parallel };

    public unsafe class TrackerMultiWell : Tracker90mmDish
    {

        #region Members

        /// <summary>
        /// The number of wells present on the plate
        /// </summary>
        int _wellnumber = 0;

        /// <summary>
        /// The ROI's corresponding to the wells on the plate
        /// </summary>
        IppiROI[] _wells;

        /// <summary>
        /// The typical length of fish. Used to define
        /// bounding boxes.
        /// </summary>
        int _typicalFishLength;

        /// <summary>
        /// If set to true we use the new whole
        /// image segmentation method for tracking
        /// </summary>
        TrackMethods _trackMethod;

        /// <summary>
        /// Preinitialized variable to hold fish-blobs
        /// </summary>
        BlobWithMoments[] _allFish;

        /// <summary>
        /// Our bin-boundaries. Bins are defined by including the lower bound
        /// but excluding the higher bound
        /// </summary>
        int* _histogramLevels;

        /// <summary>
        /// Array to hold the results of histogram computations
        /// </summary>
        int* _hist;

        /// <summary>
        /// Image of size wellsize which holds the results
        /// of our compare operations and which is used to
        /// calculate the image moments
        /// </summary>
        Image8 _wellCompare;

        /// <summary>
        /// The number of chunks we track
        /// in parallel
        /// </summary>
        int _parallelChunks;

        /// <summary>
        /// An array of lists with _parallelChunks elements.
        /// Each list element contains a list of all wells within
        /// the given chunk.
        /// </summary>
        List<IppiROI>[] _parallelChunkWells;

        /// <summary>
        /// For each chunk i lists the number of wells in
        /// all chunks with index less than i (i.e. the
        /// chunks start index in the results array)
        /// </summary>
        int[] _parallelCumWellCount;

        /// <summary>
        /// For parallel tracking will contain one
        /// entry for each well with the fish
        /// </summary>
        BlobWithMoments[] _parallelTrackResults;

        /// <summary>
        /// These are the regions that we track
        /// in parallel during multi-threaded tracking
        /// </summary>
        IppiROI[] _parallelImageRegions;

        /// <summary>
        /// Buffer used internally by ipp's label markers
        /// function - one buffer for each thread chunk!
        /// </summary>
        protected byte*[] _parallelMarkerBuffers;

        /// <summary>
        /// Pointer to the momentState structure used internally by ipp
        /// One buffer for each thread chunk!
        /// </summary>
        protected IppiMomentState_64s*[] _parallelMomentStates;

        

        /// <summary>
        /// The ROI's corresponding to the wells on the plate
        /// </summary>
        public IppiROI[] Wells
        {
            get
            {
                return _wells;
            }
        }

        /// <summary>
        /// The typical length of fish. Used to define
        /// bounding boxes.
        /// </summary>
        public int TypicalFishLength
        {
            get
            {
                return _typicalFishLength;
            }
            set
            {
                if (value < 1)
                    throw new ArgumentOutOfRangeException("Typical fish length has to be at least 1 pixel");
                _typicalFishLength = value;
            }
        }

        public bool DoMedianFiltering { get; set; }

        /// <summary>
        /// Determines whether we use the new whole image segmentation
        /// and labeling method to track (faster but will run into
        /// segmentation problems on noisier foregrounds)
        /// </summary>
        public TrackMethods TrackMethod
        {
            get
            {
                return _trackMethod;
            }
            set
            {
                _trackMethod = value;
            }
        }

        

        #endregion

        

        #region Constructor

        /// <summary>
        /// Constructs a new multi-well tracker
        /// </summary>
        /// <param name="imageWidth">The total width of the image we want to track</param>
        /// <param name="imageHeight">The total height of the image we want to track</param>
        /// <param name="plate_index">This index is appended to the ROI file name to load file for proper plate</param>
        /// <param name="parallelChunks">The number of parallel chunks to track</param>
        public TrackerMultiWell(int imageWidth, int imageHeight, int plate_index, int parallelChunks=2) : base(imageWidth,imageHeight)
        {
            _typicalFishLength = 30;
            _trackMethod = TrackMethods.PerWell;//default to per-well tracking method
            //Load ROIs - the following way to assess how many lines we have and hence how many
            //ROIs we are dealing with is ugly but what the heck its quick to think up
            _wellnumber = 0;
            var assembly = Assembly.GetExecutingAssembly();
            StreamReader roiStream = null;
            string fileToOpen = string.Format("SleepTracker.ROI_defs_{0}.txt", plate_index);
            
            //open for prescanning
            var s = assembly.GetManifestResourceStream(fileToOpen);
            roiStream = new StreamReader(s);
            while (!roiStream.EndOfStream)
            {
                string line = roiStream.ReadLine();
                string[] values = line.Split('\t');
                if (values.Length == 4)
                    _wellnumber++;
            }
            roiStream.Dispose();
            //reopen to load wells
            s = assembly.GetManifestResourceStream(fileToOpen);
            roiStream = new StreamReader(s);
            _wells = new IppiROI[_wellnumber];
            //we use the following hash-table to keep track of wells belonging
            //to each row. This will give us the number of rows and their boundaries
            //for later parallel chunk boundary determination as well as which wells
            //should be tracked in which chunk
            Dictionary<int, List<IppiROI>> wellsPerRow = new Dictionary<int, List<IppiROI>>();
            Dictionary<int, int> columnStarts = new Dictionary<int, int>();
            for (int i = 0; i < _wellnumber; i++)
            {
                System.Diagnostics.Debug.Assert(!roiStream.EndOfStream,"Unexpectedly reached end of ROI file");
                string line = roiStream.ReadLine();
                string[] values = line.Split('\t');
                System.Diagnostics.Debug.Assert(values.Length == 4,"Found line in ROI file that does not contain 4 tab-separated strings");
                int[] numValues = new int[4];
                for (int j = 0; j < 4;j++ )
                {
                    numValues[j] = int.Parse(values[j]);
                }
                _wells[i] = new IppiROI(numValues[1], numValues[0], numValues[2], numValues[3]);
                if (wellsPerRow.ContainsKey(_wells[i].Y))
                {
                    wellsPerRow[_wells[i].Y].Add(_wells[i]);
                }
                else
                {
                    //create a new list for this row and append our first well
                    wellsPerRow[_wells[i].Y] = new List<IppiROI>();
                    wellsPerRow[_wells[i].Y].Add(_wells[i]);
                }
                if (columnStarts.ContainsKey(_wells[i].X))
                    columnStarts[_wells[i].X]++;
                else
                    columnStarts[_wells[i].X] = 1;
            }
            roiStream.Dispose();
            //can't have more parallel regions than we have rows of wells
            if (parallelChunks > wellsPerRow.Count)
                parallelChunks = wellsPerRow.Count;
            _allFish = new BlobWithMoments[_wellnumber];
            //initialize our histogram bin boundaries (=histogram levels)
            //the following assignment will allow us to study marker values from 0 to 254
            //since: h[k] = countof(pLevels[k] <= pixels(x,y) < pLevels[k+1])
            _histogramLevels = (int*)Marshal.AllocHGlobal(sizeof(int) * 256);
            for (int i = 0; i < 256; i++)
                _histogramLevels[i] = i;
            _hist = (int*)Marshal.AllocHGlobal(sizeof(int) * 255);
            //assume all wells have the same size
            _wellCompare = new Image8(_wells[1].Size.width, _wells[1].Size.height);
            
            _parallelChunks = parallelChunks;
            
            //Initialize result array for parallel tracking
            _parallelTrackResults = new BlobWithMoments[_wellnumber];
            //Set up image regions for parallel tracking
            _parallelImageRegions = new IppiROI[_parallelChunks];
            //populate image regions and parallel buffers if _parallelChunks is larger 1
            if (_parallelChunks > 1)            
            {
                _parallelMarkerBuffers = new byte*[_parallelChunks];
                _parallelMomentStates = new IppiMomentState_64s*[_parallelChunks];
                //determine the number of rows in each chunk - integer division and last chunk get's the remainder tucked on
                int nPerChunk = wellsPerRow.Count / _parallelChunks;
                int nLastChunk = wellsPerRow.Count - (_parallelChunks - 1) * nPerChunk;
                //obtain all our row-starting coordinates and sort ascending
                var rowCoordinates = wellsPerRow.Keys.ToArray();
                Array.Sort(rowCoordinates);
                //do the same for column starting coordinates
                var colCoordinates = columnStarts.Keys.ToArray();
                Array.Sort(colCoordinates);
                //Inititalize our parallel-well list array
                _parallelChunkWells = new List<IppiROI>[_parallelChunks];
                for (int i = 0; i < _parallelChunks; i++)
                {
                    //for each chunk initialize it's well-list
                    _parallelChunkWells[i] = new List<IppiROI>();
                    int startRowInChunk = i * nPerChunk;
                    int endRowInChunk;
                    //are we dealing with the last chunk - this one potentially has a different number of rows!
                    if (i == _parallelChunks - 1)
                        endRowInChunk = startRowInChunk + nLastChunk - 1;
                    else
                        endRowInChunk = startRowInChunk + nPerChunk - 1;
                    //add all wells of this chunk to the list by looping over rows
                    //finding their start coordinate and using that to index into
                    //our dictionary. Then loop over the list in the dictionary
                    for (int j = startRowInChunk; j <= endRowInChunk; j++)
                        foreach (IppiROI wr in wellsPerRow[rowCoordinates[j]])
                            _parallelChunkWells[i].Add(wr);
                    //determine top-left corner as well as width and height of this chunk
                    int y_top = wellsPerRow[rowCoordinates[0]][0].Y;
                    int y_bottom = wellsPerRow[rowCoordinates[endRowInChunk]][0].Y + wellsPerRow[rowCoordinates[endRowInChunk]][0].Height - 1;
                    int height = y_bottom - y_top + 1;
                    System.Diagnostics.Debug.Assert(height > 1);
                    int x_left = colCoordinates[0];
                    int x_right = colCoordinates[colCoordinates.Length - 1] + wellsPerRow[rowCoordinates[0]][0].Width - 1;
                    int width = x_right - x_left + 1;
                    System.Diagnostics.Debug.Assert(width > 1);
                    _parallelImageRegions[i] = new IppiROI(x_left, y_top, width, height);
                    //Initialize marker buffer for this chunk
                    int bufferSize = 0;
                    IppHelper.IppCheckCall(cv.ippiLabelMarkersGetBufferSize_8u_C1R(_parallelImageRegions[i].Size, &bufferSize));
                    _parallelMarkerBuffers[i] = (byte*)Marshal.AllocHGlobal(bufferSize);
                    //initialize moment state for this chunk
                    fixed (IppiMomentState_64s** ppState = &_parallelMomentStates[i])
                    {
                        //let ipp decide whether to give accurate or fast results
                        IppHelper.IppCheckCall(ip.ippiMomentInitAlloc_64s(ppState, IppHintAlgorithm.ippAlgHintNone));
                    }
                }
                //determine each chunks start index in the fish output array based on the number of wells
                //in lower chunks
                _parallelCumWellCount = new int[_parallelChunks];
                for(int i = 1; i < _parallelChunks; i++)
                {
                    _parallelCumWellCount[i] = _parallelCumWellCount[i - 1] + _parallelChunkWells[i - 1].Count;
                }
            }
        }

        #endregion


        #region Methods

        
        /// <summary>
        /// Extracts for each well the most likely fish-blob or null if no suitable candidate was found
        /// </summary>
        /// <returns>All fish blobs found in the image</returns>
        private BlobWithMoments[] ExtractAll()
        {
            int nMarkers = 0;

            //we have fixed bounding boxes for each fish that are square and 1.5 times the typical fish-length
            //with the fishes centroid centered in the box
            int bbOffset = (int)(_typicalFishLength * 0.75);
            int bbSize = (int)(_typicalFishLength * 1.5);

            //Copy foreground to marker and label connected components
            //IppHelper.IppCheckCall(ip.ippiCopy_8u_C1R(_foreground.Image, _foreground.Stride, _labelMarkers.Image, _labelMarkers.Stride, _foreground.Size));
            IppHelper.IppCheckCall(cv.ippiLabelMarkers_8u_C1IR(_foreground.Image, _foreground.Stride, _foreground.Size, 1, 254, IppiNorm.ippiNormInf, &nMarkers, _markerBuffer));

            if (nMarkers > 254)
                nMarkers = 254;

            //fish are identified by looping over all wells and computing the histogram of pixel values for each well. This will effectively tell us
            //which marker is the most abundant in the given well => this would be the fish by our general detection logic
            //The maximum value that ippiLabelMarkers has used is equal to nMarkers. Therefore, when we compute our histogram, we supply
            //(nMarkers+2) as the nLevels parameter - this will give us (nMarkers+1) bins containing the counts from 0->nMarkers with 0 being our background
            
            //loop over wells
            for (int i = 0; i < _wellnumber; i++)
            {
                IppiROI well = _wells[i];
                //get well-specific maxvalue
                //byte maxVal = 0;
                //IppHelper.IppCheckCall(ip.ippiMax_8u_C1R(_labelMarkers[well.TopLeft], _labelMarkers.Stride, well.Size, &maxVal));
                //compute histogram in this well - from 0 until maxval
                IppHelper.IppCheckCall(ip.ippiHistogramRange_8u_C1R(_foreground[well.TopLeft], _foreground.Stride, well.Size, _hist, _histogramLevels, nMarkers + 2));
                //_hist now contains in position i the abundance of marker i => by looping over it from 1->nMarkers we can find the largest blob. If this blob is
                //larger than our minimum size, we compute the moments and initialize a BlobWithMoments structure for it
                int maxCount = 0;
                int maxIndex = -1;
                for (int j = 1; j <= nMarkers; j++)
                {
                    if (_hist[j] > maxCount && _hist[j] > _minArea && _hist[j]<=_maxArea)
                    {
                        maxCount = _hist[j];
                        maxIndex = j;
                    }
                }
                if (maxIndex == -1)//no suitable fish found
                    _allFish[i] = null;
                else
                {
                    //compare and compute moments
                    //label all pixels with the current marker as 255 and others as 0
                    IppHelper.IppCheckCall(ip.ippiCompareC_8u_C1R(_foreground[well.TopLeft], _foreground.Stride, (byte)maxIndex, _wellCompare.Image, _wellCompare.Stride, well.Size, IppCmpOp.ippCmpEq));
                    //calculate image moments
                    IppHelper.IppCheckCall(ip.ippiMoments64s_8u_C1R(_wellCompare.Image, _wellCompare.Stride, well.Size, _momentState));
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
                    ip.ippiGetSpatialMoment_64s(_momentState, 0, 0, 0, well.TopLeft, &m00, 0);
                    m00 /= 255;
                    ip.ippiGetSpatialMoment_64s(_momentState, 1, 0, 0, well.TopLeft, &m10, 0);
                    m10 /= 255;
                    ip.ippiGetSpatialMoment_64s(_momentState, 0, 1, 0, well.TopLeft, &m01, 0);
                    m01 /= 255;
                    ip.ippiGetSpatialMoment_64s(_momentState, 2, 0, 0, well.TopLeft, &m20, 0);
                    m20 /= 255;
                    ip.ippiGetSpatialMoment_64s(_momentState, 0, 2, 0, well.TopLeft, &m02, 0);
                    m02 /= 255;
                    ip.ippiGetSpatialMoment_64s(_momentState, 1, 1, 0, well.TopLeft, &m11, 0);
                    m11 /= 255;
                    ip.ippiGetSpatialMoment_64s(_momentState, 3, 0, 0, well.TopLeft, &m30, 0);
                    m30 /= 255;
                    ip.ippiGetSpatialMoment_64s(_momentState, 0, 3, 0, well.TopLeft, &m03, 0);
                    m03 /= 255;
                    ip.ippiGetSpatialMoment_64s(_momentState, 2, 1, 0, well.TopLeft, &m21, 0);
                    m21 /= 255;
                    ip.ippiGetSpatialMoment_64s(_momentState, 1, 2, 0, well.TopLeft, &m12, 0);
                    m12 /= 255;
                    _allFish[i] = new BlobWithMoments(m00, m10, m01, m20, m11, m02, m30, m03, m21, m12);
                    //assign bounding box
                    IppiRect bBox = new IppiRect(_allFish[i].Centroid.x - bbOffset, _allFish[i].Centroid.y - bbOffset, bbSize, bbSize);
                    if (bBox.x < well.X)
                        bBox.x = well.X;
                    if (bBox.y < well.Y)
                        bBox.y = well.Y;
                    if (bBox.x + bBox.width > well.X + well.Width)
                        bBox.width = well.X + well.Width - bBox.x;
                    if (bBox.y + bBox.height > well.Y + well.Height)
                        bBox.height = well.Y + well.Height - bBox.y;
                    _allFish[i].BoundingBox = bBox;
                }
            }//end looping over all wells

            return _allFish;
        }

        /// <summary>
        /// Extract most likely fish from a given well on parallel thread
        /// </summary>
        /// <param name="region">The well boundaries in which the fish should be found</param>
        /// <param name="chunkIndex">The index of the current chunk - in order to use appropriate marker and moment buffer!</param>
        /// <returns>The most likely fish blob</returns>
        private BlobWithMoments ExtractWellParallel(IppiROI region, int chunkIndex)
        {
            int nMarkers = 0;
            BlobWithMoments[] blobsDetected;



            //Copy foreground to marker and label connected components
            //IppHelper.IppCheckCall(ip.ippiCopy_8u_C1R(_foreground[region.TopLeft], _foreground.Stride, _labelMarkers[region.TopLeft], _labelMarkers.Stride, region.Size));
            IppHelper.IppCheckCall(cv.ippiLabelMarkers_8u_C1IR(_foreground[region.TopLeft], _foreground.Stride, region.Size, 1, 254, IppiNorm.ippiNormInf, &nMarkers, _parallelMarkerBuffers[chunkIndex]));
            //loop over returned markers and use ipp to extract blobs
            if (nMarkers > 0)
            {
                if (nMarkers > 254)
                    nMarkers = 254;
                blobsDetected = new BlobWithMoments[nMarkers];
                for (int i = 1; i <= nMarkers; i++)
                {
                    //label all pixels with the current marker as 255 and others as 0
                    IppHelper.IppCheckCall(ip.ippiCompareC_8u_C1R(_foreground[region.TopLeft], _foreground.Stride, (byte)i, _calc[region.TopLeft], _calc.Stride, region.Size, IppCmpOp.ippCmpEq));
                    //calculate image moments
                    IppHelper.IppCheckCall(ip.ippiMoments64s_8u_C1R(_calc[region.TopLeft], _calc.Stride, region.Size, _parallelMomentStates[chunkIndex]));
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
                    ip.ippiGetSpatialMoment_64s(_parallelMomentStates[chunkIndex], 0, 0, 0, new IppiPoint(region.X, region.Y), &m00, 0);
                    //since our input image is not 0s and 1s but 0s and 255s we have to divide by 255 in order to re-normalize our moments
                    System.Diagnostics.Debug.Assert(m00 % 255 == 0, "M00 was not a multiple of 255");
                    m00 /= 255;
                    //only retrieve other moments if this is a "fish candidate"
                    if (m00 > MinArea && m00<=MaxArea)
                    {
                        ip.ippiGetSpatialMoment_64s(_parallelMomentStates[chunkIndex], 1, 0, 0, new IppiPoint(region.X, region.Y), &m10, 0);
                        m10 /= 255;
                        ip.ippiGetSpatialMoment_64s(_parallelMomentStates[chunkIndex], 0, 1, 0, new IppiPoint(region.X, region.Y), &m01, 0);
                        m01 /= 255;
                        ip.ippiGetSpatialMoment_64s(_parallelMomentStates[chunkIndex], 2, 0, 0, new IppiPoint(region.X, region.Y), &m20, 0);
                        m20 /= 255;
                        ip.ippiGetSpatialMoment_64s(_parallelMomentStates[chunkIndex], 0, 2, 0, new IppiPoint(region.X, region.Y), &m02, 0);
                        m02 /= 255;
                        ip.ippiGetSpatialMoment_64s(_parallelMomentStates[chunkIndex], 1, 1, 0, new IppiPoint(region.X, region.Y), &m11, 0);
                        m11 /= 255;
                        ip.ippiGetSpatialMoment_64s(_parallelMomentStates[chunkIndex], 3, 0, 0, new IppiPoint(region.X, region.Y), &m30, 0);
                        m30 /= 255;
                        ip.ippiGetSpatialMoment_64s(_parallelMomentStates[chunkIndex], 0, 3, 0, new IppiPoint(region.X, region.Y), &m03, 0);
                        m03 /= 255;
                        ip.ippiGetSpatialMoment_64s(_parallelMomentStates[chunkIndex], 2, 1, 0, new IppiPoint(region.X, region.Y), &m21, 0);
                        m21 /= 255;
                        ip.ippiGetSpatialMoment_64s(_parallelMomentStates[chunkIndex], 1, 2, 0, new IppiPoint(region.X, region.Y), &m12, 0);
                        m12 /= 255;
                        blobsDetected[i - 1] = new BlobWithMoments(m00, m10, m01, m20, m11, m02, m30, m03, m21, m12);
                        //Determine bounding box of the blob. The following seems kinda retarded as Ipp must already
                        //have obtained that information before so maybe there is some way to actually retrieve it??
                        //Do linescans using ipp's sum function starting from the blobs centroid until we hit a line
                        //the sum of which is 0
                        int xStart, xEnd, yStart, yEnd;
                        double sum = 1;
                        IppiPoint centroid = blobsDetected[i - 1].Centroid;
                        xStart = centroid.x - 5;
                        xEnd = centroid.x + 5;
                        yStart = centroid.y - 5;
                        yEnd = centroid.y + 5;
                        //in the following loops we PRE-increment, whence we stop the loop if we are at one coordinate short of the ends
                        //find xStart
                        while (sum > 0 && xStart > region.X + 4)
                        {
                            xStart -= 5;
                            IppHelper.IppCheckCall(ip.ippiSum_8u_C1R(_calc[xStart, region.Y], _calc.Stride, new IppiSize(1, region.Height), &sum));
                        }
                        xStart += 1;//we have a sum of 0, so go back one line towards the centroid
                        //find xEnd
                        sum = 1;
                        while (sum > 0 && xEnd < region.X + region.Width - 6)
                        {
                            xEnd += 5;
                            IppHelper.IppCheckCall(ip.ippiSum_8u_C1R(_calc[xEnd, region.Y], _calc.Stride, new IppiSize(1, region.Height), &sum));
                        }
                        xEnd -= 1;//we have sum of 0, so go back one line towards the centroid
                        //find yStart - we can limit our x-search-space as we already have those boundaries
                        sum = 1;
                        while (sum > 0 && yStart > region.Y + 4)
                        {
                            yStart -= 5;
                            IppHelper.IppCheckCall(ip.ippiSum_8u_C1R(_calc[xStart, yStart], _calc.Stride, new IppiSize(xEnd - xStart + 1, 1), &sum));
                        }
                        yStart += 1;
                        //find yEnd - again limit summation to x-search-space
                        sum = 1;
                        while (sum > 0 && yEnd < region.Y + region.Height - 6)
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

            if (maxArea < MinArea)
                return null;
            else
                return blobsDetected[maxIndex];
        }

        /// <summary>
        /// Method for parallelized tracking - depending on index
        /// performs tracking on an image chunk
        /// </summary>
        /// <param name="index">The chunk's index</param>
        private void TrackChunk(int index)
        {
            //each chunk has its own image region with its own wells to take care of
            //currently, only 4 chunks on a 24-well plate are supported
            if(index>=_parallelChunks)
                System.Diagnostics.Debug.WriteLine("Fatal parallel tracking error. Tried to access non-existing chunk.");
            //do image filtering, thresholding and closing
            if (DoMedianFiltering)
            {
                IppHelper.IppCheckCall(ip.ippiFilterMedianWeightedCenter3x3_8u_C1R(_calc[_parallelImageRegions[index].X + 1, _parallelImageRegions[index].Y + 1], _calc.Stride,
                    _foreground[_parallelImageRegions[index].X + 1, _parallelImageRegions[index].Y + 1], _foreground.Stride,
                    new IppiSize(_parallelImageRegions[index].Width - 2, _parallelImageRegions[index].Height - 2), 1));
            }
            Im2Bw(_foreground, _parallelImageRegions[index]);
            Close3x3(_foreground, _parallelImageRegions[index]);
            //loop over the wells that belong to our region, extract the fish
            //and put into our result structure
            //Important: Need to put into the chunks section of the results section!
            var ourWells = _parallelChunkWells[index];
            for (int i = 0; i < ourWells.Count; i++)
                _parallelTrackResults[i+_parallelCumWellCount[index]] = ExtractWellParallel(ourWells[i],index);
        }
        

        /// <summary>
        /// Tracks fish on a multiwell plate
        /// </summary>
        /// <param name="image">The current image to track and update the background with</param>
        /// <param name="updateBackground">If true the current image will be used to update the background</param>
        /// <param name="forceFullFrame">If set to true background updates won't exclude fish locations</param>
        /// <returns>A list of fish-blobs, one for each well or null if no fish was detected</returns>
        public BlobWithMoments[] TrackMultiWell(Image8 image, bool updateBackground = true, bool forceFullFrame = false)
        {
            if (_frame == 0)
            {
                ip.ippiSet_8u_C1R(0, Foreground.Image, Foreground.Stride, Foreground.Size);
                ip.ippiSet_8u_C1R(0, _calc.Image, _calc.Stride, _calc.Size);
            }

            BlobWithMoments[] currentFish = new BlobWithMoments[_wellnumber];


            if (_frame > FramesInitialBackground && !forceFullFrame)
            {
                //do global stuff (everything except labelMarkers if we don't do parallel tracking otherwise only the inherently parallel background subtraction)

                //cache 8-bit representation of the background
                Image8 bg = _bgModel.Background;
                //Perform background subtraction
                if(DoMedianFiltering)
                    IppHelper.IppCheckCall(cv.ippiAbsDiff_8u_C1R(image.Image, image.Stride, bg.Image, bg.Stride, _calc.Image, _calc.Stride, image.Size));
                else
                    IppHelper.IppCheckCall(cv.ippiAbsDiff_8u_C1R(image.Image, image.Stride, bg.Image, bg.Stride, _foreground.Image, _foreground.Stride, image.Size));

                if (_trackMethod == TrackMethods.Parallel && _parallelChunks > 1)
                {
                    if (!Parallel.For(0, _parallelChunks, TrackChunk).IsCompleted)
                    {
                        System.Diagnostics.Debug.WriteLine("Error in parallel tracking. Not all regions completed");
                    }
                    //copy our result into currentFish
                    for (int i = 0; i < _wellnumber; i++)
                        currentFish[i] = _parallelTrackResults[i];
                }
                else
                {
                    if (DoMedianFiltering)
                    {
                        //remove noise via median filtering
                        IppHelper.IppCheckCall(ip.ippiFilterMedianWeightedCenter3x3_8u_C1R(_calc[1, 1], _calc.Stride,
                            _foreground[1, 1], _foreground.Stride, new IppiSize(image.Width - 2, image.Height - 2), 1));
                    }
                    //Threshold and close
                    Im2Bw(_foreground, new IppiROI(new IppiPoint(0, 0), image.Size));
                    Close3x3(_foreground, new IppiROI(new IppiPoint(0, 0), image.Size));

                    //label markers and extract - depending on the method selector
                    //we label components on the whole image (new) or individual wells(old)
                    if (_trackMethod == TrackMethods.OnWholeImage)
                        currentFish = ExtractAll();
                    else
                    {
                        for (int i = 0; i < _wellnumber; i++)
                            currentFish[i] = ExtractFish(_wells[i]);
                    }
                }
            }//If We are past initial background frames

            //create/update background and advance frame counter
            if (_frame == 0)
                _bgModel = new SelectiveUpdateBGModel(image, 1.0f / FramesInBackground);
            else if (updateBackground || _frame <= FramesInitialBackground)//only allow reduction of background update rate AFTER initial background has been created
            {
                if (currentFish == null || forceFullFrame)
                    _bgModel.UpdateBackground(image);
                else
                    _bgModel.UpdateBackground(image, currentFish);
            }
             

            _frame++;
            return currentFish;
        }

        #endregion

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (_histogramLevels != null)
            {
                Marshal.FreeHGlobal((IntPtr)_histogramLevels);
                _histogramLevels = null;
            }
            if (_hist != null)
            {
                Marshal.FreeHGlobal((IntPtr)_hist);
                _hist = null;
            }
            if (_wellCompare != null)
            {
                _wellCompare.Dispose();
                _wellCompare = null;
            }
            //Free memory of our parallel tracking buffers
            for (int i = 0; i < _parallelChunks; i++)
            {
                if (_parallelMarkerBuffers != null)
                {
                    if (_parallelMarkerBuffers[i] != null)
                    {
                        Marshal.FreeHGlobal((IntPtr)_parallelMarkerBuffers[i]);
                        _parallelMarkerBuffers[i] = null;
                    }
                }
                if (_parallelMomentStates != null)
                {
                    if (_parallelMomentStates[i] != null)
                    {
                        ip.ippiMomentFree_64s(_parallelMomentStates[i]);
                        _parallelMomentStates[i] = null;
                    }
                }
            }
        }

        
    }
}
