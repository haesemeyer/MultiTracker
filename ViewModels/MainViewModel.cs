
using System;
using System.Threading;
using System.Windows.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using System.IO;
using System.IO.Ports;
using System.Collections.Generic;






using ipp;

using MHApi.GUI;
using MHApi.Threading;
using MHApi.DrewsClasses;
using MHApi.Imaging;
using MHApi.Utilities;

using SleepTracker.Tracking;
using SleepTracker.Experiments;
using BufferAcquisition;






namespace SleepTracker.ViewModels
{
    struct TrackerFilePair
    {
        public TrackerMultiWell Tracker;
        public StreamWriter TrackWriter;

        public TrackerFilePair(TrackerMultiWell tracker, StreamWriter trackwriter)
        {
            Tracker = tracker;
            TrackWriter = trackwriter;
        }
    }

   


    public unsafe class MainViewModel : ViewModelBase
    {

        #region Fields

        /// <summary>
        /// The active com ports of the system
        /// </summary>
        string[] _activeCOMPorts;

        /// <summary>
        /// The COM port of the teensy
        /// </summary>
        string _teensyPort;

        /// <summary>
        /// The thread for image grabbing/tracking
        /// 
        /// </summary>
        Worker _grabThread;

        /// <summary>
        /// Thread for image preview
        /// </summary>
        Worker _previewThread;

        

        /// <summary>
        /// The current raw image
        /// </summary>
        EZImageSource _image;

        /// <summary>
        /// The current image after tracking or processing
        /// </summary>
        EZImageSource _imageTrack;

        /// <summary>
        /// The current experiment array
        /// </summary>
        IExperiment[] _experiments;

        /// <summary>
        /// Intermediate buffer to hold images for burst writing
        /// </summary>
        PrCoImageRingBuffer _burstBuffer;

        /// <summary>
        /// Used to synchronize access to burst  image
        /// operations
        /// </summary>
        object _burstLock = new object();

        /// <summary>
        /// This queue holds our switch indices (Item1) and the
        /// plate index we want to switch to (Item2)
        /// </summary>
        Queue<Tuple<int, int>> _switchQueue;

        

        /// <summary>
        /// Our acquisition interface - "the camera"
        /// </summary>
        private BufferAcquisition.CircularAcquisition _m_circAcq;// = new BufferAcquisition.CircularAcquisition();

        private object _mCircAcqu_lock = new object();

        /// <summary>
        /// The number of the buffer that contains the current frame (in the ring-buffer
        /// above, the "camera" will tell us which one it is
        /// </summary>
        private volatile uint _m_latestBuffer = 0;

        /// <summary>
        /// The number of frames to acquire before a burst action
        /// </summary>
        uint _burstPreFrames;

        /// <summary>
        /// The number of frames to acquire after a burst action
        /// </summary>
        uint _burstPostFrames;

        /// <summary>
        /// Are we tracking at the moment
        /// </summary>
        bool _isRunning;

        /// <summary>
        /// The current frame number
        /// </summary>
        public uint _frameIndex;

        /// <summary>
        /// The index of the plate we currently track
        /// </summary>
        int _plateIndex = 0;

        /// <summary>
        /// The number of plates in the experiment
        /// </summary>
        int _plateCount = 2;

        /// <summary>
        /// The last flip index, we use this to delay processing frames while the mirros are moving 
        /// </summary>
        uint _lastFlipFrame;


        TrackerFilePair[] _trackers;

        /// <summary>
        /// The name of the current experiment
        /// </summary>
        string _experimentName;

        /// <summary>
        /// The number of frames to acquire in the current experiment
        /// </summary>
        int _nFrames;

        /// <summary>
        /// The sound power and temperature reported by the TeensySensor
        /// </summary>
        public string SoundPower_tab_Temp = "NaN";

        /// <summary>
        /// The frame where the temperature was read
        /// </summary>
        public uint _TempFrame;

        /// <summary>
        /// The comment of the current experiment
        /// </summary>
        string _comment;

        /// <summary>
        /// The type of fish used in the current experiment
        /// </summary>
        string _fishType;
        

        /// <summary>
        /// The frame rate the camera runs at
        /// </summary>
        public const int frameRate = 560;

        /// <summary>
        /// The desired framerate during baseline
        /// </summary>
        const int baselineFrameRate = 7; 

        /// <summary>
        /// After this many milliseconds a thread that is being stopped will timeout
        /// </summary>
        const int ThreadTimeout = 10000;

        /// <summary>
        /// If set to true, we will save frameRate frames (1s) after each tap
        /// </summary>
        bool _irisMode;

        /// <summary>
        /// If set to true, we will tracker, if not, we will just run the delta pixel calculation. Set to true when running 96-well plates
        /// </summary>
        bool _TrackingON = true;

        //Image Size -> should be replaced by some code
        const int ImageWidth = 2336;

        const int ImageHeight = 1728;

        #endregion

        #region Constructor

        public MainViewModel()
        {
            ExperimentName = "Experiment01";
            Comment = "Notes???";
            FishType = "TLF_5dpf";
            //NumFrames = frameRate * 3600 * 48;
            NumFrames = 29333920 + 560*60;
            BurstPreFrames = 0;
            BurstPostFrames = frameRate;
            IrisMode = true;
                       
            if (IsInDesignMode)
                return;
            ActiveCOMPorts = SerialPort.GetPortNames();
            if (Array.IndexOf(ActiveCOMPorts, "COM13") != -1)
                TeensyPort = "COM13";
            else
                TeensyPort = ActiveCOMPorts[1];
            Image = new EZImageSource();
            ImageTrack = new EZImageSource();
            ImageTrack.CMax = 5;
            
            //start preview
            _previewThread = new Worker(PreviewThreadRun, true, 5000);

        }

        #endregion

        #region Properties

        public string[] ActiveCOMPorts
        {
            get
            {
                return _activeCOMPorts;
            }
            private set
            {
                _activeCOMPorts = value;
                RaisePropertyChanged(nameof(ActiveCOMPorts));
            }
        }

        public string TeensyPort
        {
            get
            {
                return _teensyPort;
            }
            set
            {
                _teensyPort = value;
                RaisePropertyChanged(nameof(TeensyPort));
            }
        }

        /// <summary>
        /// The number of frames to acquire prior to a burst action
        /// </summary>
        public uint BurstPreFrames
        {
            get
            {
                return _burstPreFrames;
            }
            set
            {
                _burstPreFrames = value;
                RaisePropertyChanged(nameof(BurstPreFrames));
            }
        }

        /// <summary>
        /// The number of frames to acquire after a burst action
        /// </summary>
        public uint BurstPostFrames
        {
            get
            {
                return _burstPostFrames;
            }
            set
            {
                _burstPostFrames = value;
                RaisePropertyChanged(nameof(BurstPostFrames));
            }
        }

        /// <summary>
        /// The current image
        /// </summary>
        public EZImageSource Image
        {
            get
            {
                return _image;
            }
            private set
            {
                _image = value;
                RaisePropertyChanged("Image");
            }
        }

        /// <summary>
        /// The current image after processing
        /// </summary>
        public EZImageSource ImageTrack
        {
            get
            {
                return _imageTrack;
            }
            private set
            {
                _imageTrack = value;
                RaisePropertyChanged("ImageTrack");
            }
        }

        

        object findexLoc = new object();

        /// <summary>
        /// The current frame index
        /// </summary>
        public uint FrameIndex
        {
            get
            {
                lock (findexLoc)
                {
                    return _frameIndex;
                }
            }
            private set
            {
                lock (findexLoc)
                {
                    _frameIndex = value;
                }
                RaisePropertyChanged("FrameIndex");
            }
        }

        /// <summary>
        /// Indicates whether we are running an experiment or not
        /// </summary>
        public bool IsRunning
        {
            get
            {
                return _isRunning;
            }
            private set
            {
                _isRunning = value;
                RaisePropertyChanged("IsRunning");
            }
        }

        /// <summary>
        /// The name of the current experiment
        /// </summary>
        public string ExperimentName
        {
            get
            {
                return _experimentName;
            }
            set
            {
                _experimentName = value;
                RaisePropertyChanged("ExperimentName");
            }
        }

        /// <summary>
        /// The number of frames in the current experiment
        /// </summary>
        public int NumFrames
        {
            get
            {
                return _nFrames;
            }
            set
            {
                _nFrames = value;
                RaisePropertyChanged("NumFrames");
            }
        }

        /// <summary>
        /// The comment in the current experiment
        /// </summary>
        public string Comment
        {
            get
            {
                return _comment;
            }
            set
            {
                _comment = value;
                RaisePropertyChanged("Comment");
            }
        }

        /// <summary>
        /// The type of fish used in the experiment
        /// </summary>
        public string FishType
        {
            get
            {
                return _fishType;
            }
            set
            {
                _fishType = value;
                RaisePropertyChanged("FishType");
            }
        }

        /// <summary>
        /// If true will save every frame during high-framerate
        /// period after taps
        /// </summary>
        public bool IrisMode
        {
            get
            {
                return _irisMode;
            }
            set
            {
                _irisMode = value;
                RaisePropertyChanged("IrisMode");
            }
        }


        #endregion

        #region Methods

        /// <summary>
        /// Update the current plate index if required
        /// </summary>
        /// <param name="FrameIndex">Current frame index</param>
        /// <returns>True if update occured</returns>
        private bool UpdatePlateIndex(uint FrameIndex)
        {
            if (_switchQueue.Count < 1)
                return false;
            if(_switchQueue.Peek().Item1 == FrameIndex)
            {
                Tuple<int, int> sw_pair = _switchQueue.Dequeue();
                _plateIndex = sw_pair.Item2;
                return true;
            }
            return false;
        }

        /// <summary>
        /// The method that will prepare our board for acquisition
        /// </summary>
        /// <param name="vfgIndex">The index of board / camera</param>
        /// <param name="bufferCount">The number of frames in our buffer</param>
        private void setupBoard(uint vfgIndex, uint bufferCount)
        {
            clearAcquisition();

            // Setup the acquisition.
            lock (_mCircAcqu_lock)
            {
                if (_m_circAcq == null)
                    throw new Exception("Circular acquisition null");
                _m_circAcq.Open(vfgIndex);
                _m_circAcq.SetOverwriteMethod(OverwriteMethod.Ignore);//Ensure that acquisition does not fail because we weren't fast enough removing frames
                _m_circAcq.Setup(bufferCount);
            }
        }

        /// <summary>
        /// Does the dirty cleanup work (what exactly???)
        /// </summary>
        private void clearAcquisition()
        {


            lock (_mCircAcqu_lock)
            {
                if (_m_circAcq.IsBoardSetup())
                    _m_circAcq.Cleanup();
                if (_m_circAcq.IsBoardOpen())
                    _m_circAcq.Close();
            }



        }

        /// <summary>
        /// Central function to initialize the tracker both for
        /// preview and track threads
        /// </summary>
        /// <param name="cameraWidth">The width of the image</param>
        /// <param name="cameraHeight">The height of the image</param>
        /// <param name="saver">The saver that is used to derive our track information files</param>
        private void InitializeTracker(int cameraWidth, int cameraHeight, Saver saver)
        {
            _trackers = new TrackerFilePair[_plateCount];
            for (int i = 0; i < _plateCount; i++)
            {
                var tracker = new TrackerMultiWell(cameraWidth, cameraHeight, i);
                //The number of frames in the running background
                tracker.FramesInBackground = baselineFrameRate * 25;
                //The initial number of frames before we start tracking
                tracker.FramesInitialBackground = baselineFrameRate * 120;
                //CHANGE HERE TO REQUIRE DIFFERENT MINIMUM AREA
                tracker.MinArea = 50;
                //CHANGE HERE TO REQUIRE A DIFFERENT MAXIMUM AREA
                tracker.MaxArea = 400;
                //Change here to require different threshold level
                tracker.Threshold = 30;
                //Change here to toggle between wholeImage and perWell or parallel tracking method
                tracker.TrackMethod = TrackMethods.Parallel;
                if (saver != null)
                {
                    //the writers for writing down tracking information (3 columns per well, xpos-ypos-angle)
                    var trackWriter = saver.GetStreamWriter(string.Format("_P_{0}.track", i));
                    _trackers[i] = new TrackerFilePair(tracker, trackWriter);
                }
                else
                    _trackers[i] = new TrackerFilePair(tracker, null);
            }
        }

        void CleanupTrackers()
        {
            if (_trackers != null)
            {
                foreach (var pair in _trackers)
                {
                    if (pair.Tracker != null)
                        pair.Tracker.Dispose();
                    if (pair.TrackWriter != null)
                        pair.TrackWriter.Dispose();
                }
            }
            _trackers = null;
        }


        /// <summary>
        /// Computes delta pixel sums for the wells as well as a synthetic empty well
        /// </summary>
        /// <param name="imCurrent">The current image off the camera</param>
        /// <param name="imPrevious">The previous frame</param>
        /// <param name="imDelta">The image to hold our computation</param>
        /// <param name="imNonWellMask">An image in which wells have value 0 and non-well areas have value 1</param>
        /// <param name="imMasked">Placeholder for the masked image</param>
        /// <param name="wells">The wells in use</param>
        /// <param name="deltaFile">The file to write the delta pixel values to</param>
        /// <param name="frameIndex">The index of the current frame for file purposes</param>
        void ComputeAndWriteDeltaPixels(Image8 imCurrent, Image8 imPrevious, Image8 imDelta, Image8 imNonWellMask ,Image8 imMasked, IppiROI[] wells, StreamWriter deltaFile, uint frameIndex)
        {
            deltaFile.Write("{0}\t", frameIndex);
            if (imCurrent == null || imPrevious == null)
            {
                for (int i = 0; i <= wells.Length; i++)//condition is i<=wells.Length since we also write an "empty" well
                    deltaFile.Write("NaN\t");
                deltaFile.WriteLine();
                return;
            }
            //compute a few values to use for empty well computation
            int totalImagePixels = imCurrent.Width * imCurrent.Height;
            int pixPerWell = wells[0].Width * wells[0].Height;
            int totalWellPixels = pixPerWell * wells.Length;
            int nonWellImagePixels = totalImagePixels - totalWellPixels;
            double totalVal = 0;//holds the difference without thresholding for the WHOLE IMAGE to detect taps.
            //compute difference image
            cv.ippiAbsDiff_8u_C1R(imCurrent.Image, imCurrent.Stride, imPrevious.Image, imPrevious.Stride, imDelta.Image, imDelta.Stride, imCurrent.Size);
            //mask out well regions
            IppHelper.IppCheckCall(ip.ippiMul_8u_C1RSfs(imDelta.Image,imDelta.Stride,imNonWellMask.Image,imNonWellMask.Stride,imMasked.Image,imMasked.Stride,imMasked.Size,0));
            //compute total low thresholded difference
            IppHelper.IppCheckCall(ip.ippiThreshold_LTVal_8u_C1IR(imMasked.Image, imMasked.Stride, imMasked.Size, (byte)(3), 0));//pixels will be thresholded at 2or lower for the total non-well area, this was determined to give the best signal to noise for medium taps on 2013_03_27
            ip.ippiSum_8u_C1R(imMasked.Image, imMasked.Stride, imMasked.Size, &totalVal);
            //threshold  
        
            // Alix you can play with this threshold value until non-fish stop responding ((byte(19) - currently set to 19)
            IppHelper.IppCheckCall(ip.ippiThreshold_LTVal_8u_C1IR(imDelta.Image, imDelta.Stride, imDelta.Size, (byte)(18), 0));//pixels will be thresholded at n-1 or lower - this was changed from 17 to 19 on 20131209 after new light board added
            
            double totalDeltaInWells = 0;
            double val = 0;
            for (int i = 0; i < wells.Length; i++)
            {               
                ip.ippiSum_8u_C1R(imDelta[wells[i].TopLeft], imDelta.Stride, wells[i].Size, &val);
                totalDeltaInWells += val;
                deltaFile.Write("{0}\t", val);
            }
            //compute total over whole image
            ip.ippiSum_8u_C1R(imDelta.Image, imDelta.Stride, imDelta.Size, &val);
            //write synthetic well to file
            deltaFile.Write("{0}\t", (val - totalDeltaInWells) / nonWellImagePixels * totalWellPixels);
            //write whole plate value to file
            deltaFile.Write("{0}\t", totalVal);
            deltaFile.WriteLine();
        }

        /// <summary>
        /// Checks if the queue is full and then clears the buffers if necessary
        /// </summary>
        /// <param name="fullBuffers">The number of filled buffers</param>
        /// <param name="buffer">A buffer structure</param>
        /// <returns>true if buffers needed clearing, false otherwise</returns>
        bool CheckAndClearBuffers(uint fullBuffers, ref BufferInfo buffer)
        {
            uint ccount = 0;//number of buffers we actually cleared
            if (fullBuffers > nBuffers * clearThresh)
            {
                System.Diagnostics.Debug.WriteLine("Buffer was 80% full, trying to clear stuff.");
                try
                {
                    //We clear until there is only half a second worth of frames left in the buffer
                    //the second condition in the while loop should never be met but is there to ensure
                    //we don't get stuck if the board acts up...
                    while (_m_circAcq.GetBufferQueueSize() > frameRate / 2 && ccount < nBuffers)
                    {
                        var rc = _m_circAcq.WaitForFrameDone(10, ref buffer);
                        _m_latestBuffer = buffer.m_BufferNumber;
                        _m_circAcq.SetBufferStatus(_m_latestBuffer, BufferStatus.Available);
                        FrameIndex++;
                        ccount++;
                    }
                }
                catch (TimeoutException)
                {
                    System.Diagnostics.Debug.WriteLine("The buffer was overfull and yet we timed out. WE DO NOT UNDERSTAND THIS BEHAVIOR!");
                }
                System.Diagnostics.Debug.WriteLine("Cleared {0} frames.", ccount);
                return true;
            }
            else
                return false;
        }

        /// <summary>
        /// Processes (or ignores) one frame at full frame-rate
        /// </summary>
        /// <param name="im">The current image</param>
        /// <param name="createNewWriter">Indicates that we need to create a new tiff-writer for the next burst</param>
        /// <param name="inBurst">If true, we are currently in a burst</param>
        void ProcessFullRateFrame(Image8 im, out bool createNewWriter)
        {
            createNewWriter = false;
            if (_experiments == null ||_experiments[_plateIndex] == null)
                return;
            _experiments[_plateIndex].PerformAction(FrameIndex);
            if (IrisMode && _experiments[_plateIndex].RecordFrame(FrameIndex, out createNewWriter))
            {
                //hand image off to our burst buffer
                _burstBuffer.Produce(im);
            }
        }

        /// <summary>
        /// Processes one frame at the baseline framerate
        /// </summary>
        /// <param name="im">The current image</param>
        /// <param name="trackWriter">Text writer to write tracking information to file</param>
        /// <param name="timeStamp">The timestamp of the frame given by the cam board</param>
        BlobWithMoments[] ProcessBaseRateFrame(Image8 im, double timeStamp, bool forceFullBackground)
        {
            TrackerFilePair tracktuple = _trackers[_plateIndex];
            BlobWithMoments[] allFish = null;
            //frame should be tracked and information written to file
            if (_TrackingON)
            {
                allFish = tracktuple.Tracker.TrackMultiWell(im, true, forceFullBackground);//currently update background twice per second
                if (tracktuple.TrackWriter != null)
                {
                    tracktuple.TrackWriter.Write("{0}\t", FrameIndex);
                    if (allFish != null)
                    {
                        foreach (var fish in allFish)
                        {
                            if (fish != null)
                            {
                                tracktuple.TrackWriter.Write("{0}\t{1}\t{2}\t", fish.Centroid.x, fish.Centroid.y, (int)(fish.Angle / Math.PI * 180));

                            }
                            else
                            {
                                tracktuple.TrackWriter.Write("{0}\t{1}\t{2}\t", "NaN", "NaN", "NaN");

                            }
                        }
                    }
                    else//there wasn't a single fish - I guess we never end up here
                    {
                        tracktuple.TrackWriter.Write("{0}\t{1}\t{2}\t", "NaN", "NaN", "NaN");
                    }
                    //write frame timestamp as last column
                    tracktuple.TrackWriter.Write(timeStamp);
                    tracktuple.TrackWriter.WriteLine();
                }
            }
            return allFish;
        }

        /// <summary>
        /// Communicates with the teensy in order to send it
        /// all dark flash frame indices
        /// </summary>
        /// <param name="Indices">The indices to send</param>
        public static void SendPayloadToTeensy(int[] Indices, SerialPort teensy)
        {
            //TODO: Send length of array so teensy can reserve space
            //TODO: Wait on ACK from teensy
            //TODO: Send a series of lines with each index
            //TODO: Wait on n-received from teensy
            System.Diagnostics.Debug.Write("The number of indexes is: ");
            System.Diagnostics.Debug.WriteLine(Indices.Length);

            for (int i = 0; i < Indices.Length; i++) 
                System.Diagnostics.Debug.WriteLine(Indices[i]);
            
            // send a 0
            byte[] signalBuffer = new byte[4];
            teensy.Write(signalBuffer, 0, 4);
            //Thread.Sleep(1000);


            //send the length to the teensy
            byte[] theLength = BitConverter.GetBytes(Indices.Length);
            System.Diagnostics.Debug.WriteLine(theLength[0]);
            teensy.Write(theLength, 0, 4);
            //Thread.Sleep(1000);

            //send the second 0
            teensy.Write(signalBuffer, 0, 4);
            //Thread.Sleep(1000);


            //now for the big stuff
            foreach (int i in Indices)
                teensy.Write(BitConverter.GetBytes(i), 0, 4);


            // send the third 0
            teensy.Write(signalBuffer, 0, 4);
            //Thread.Sleep(50);

            //wait for teensy to confirm
            teensy.ReadTimeout = 1000;
            try {
                int ack = teensy.ReadByte();
                if (ack != 5)
                    System.Diagnostics.Debug.WriteLine("Did not receive proper ACK from teensy");
                else
                    System.Diagnostics.Debug.WriteLine("ACK RECEIVED.");
            }
            catch (TimeoutException)
            {
                System.Diagnostics.Debug.WriteLine("Timed out waiting for ack from teensy");
            }
        }

        /// <summary>
        /// Signals to the teensy that it should set off
        /// the camera and flash triggering
        /// </summary>
        public static void SendTriggerToTeensy(SerialPort teensy)
        {
            byte[] signalBuffer = new byte[4];
            teensy.Write(signalBuffer, 0, 4);//send our first zero - wait for number of frames
        }

        #endregion

        /// <summary>
        /// The size of the acquisition buffer
        /// </summary>
        const int nBuffers = frameRate * 8;//this amount leaves as a little more than 2 seconds for the burst buffer

        /// <summary>
        /// If at least clearThresh fraction of our buffer is full
        /// we remove this many frames
        /// </summary>
        const int clearCount = 560;

        /// <summary>
        /// If this fraction of our buffer is filled
        /// we clear frames
        /// </summary>
        const double clearThresh = 0.8;



        #region ThreadProcs

        /// <summary>
        /// The thread method to run image preview
        /// </summary>
        /// <param name="stop">Signals the thread to stop</param>
        /// <param name="dispOwner">The thread's owners dispatcher</param>
        void PreviewThreadRun(AutoResetEvent stop, Dispatcher dispOwner) 
        {
            System.Diagnostics.Debug.WriteLine("Entered image preview");
            //Performance measures
            double totalTrackTime = 0;
            double frequency = System.Diagnostics.Stopwatch.Frequency;
            long trackstart = 0;
            long trackstop = 0;
            uint TTCountIndex = 0;

            //create circular acquisition object and connect to board
            _m_circAcq = new CircularAcquisition();
            setupBoard(0, nBuffers);
            
            
            FrameIndex = 0;
            BlobWithMoments[] allFish = null;
            

            try
            {
                using (Image8 image = new Image8(ImageWidth, ImageHeight, IntPtr.Zero) )//_camIccd.Width,_camIccd.Height))
                {
                    _m_circAcq.StartAcquisition(AcqControlOptions.Wait);//Start camera: Maybe want to switch options to Async later but unclear for now!

                    InitializeTracker(ImageWidth, ImageHeight, null);

                    BufferInfo buffer = new BufferInfo();

                    uint lastBufferNumber = uint.MaxValue;

                    var rc = WaitFrameDoneReturns.FrameAcquired;

                    _plateIndex = 0;//determines which plate will be tracked

                    //frame-grabbing loop
                    while (!stop.WaitOne(0))
                    {

                        try
                        {
                            rc = _m_circAcq.WaitForFrameDone(50, ref buffer);//NOTE: Originally had timeout set to 2, however that resulted in a timeout
                            CheckAndClearBuffers(_m_circAcq.GetBufferQueueSize(), ref buffer);

                        }
                        catch (System.TimeoutException)
                        {
                            System.Diagnostics.Debug.WriteLine("Timed out on acquisition, exiting.");
                            System.Diagnostics.Debug.WriteLine("ABORT!");
                            break;
                        }
                        if (rc != WaitFrameDoneReturns.FrameAcquired)
                        {
                            //something went wrong
                            System.Diagnostics.Debug.WriteLine("!! Could not acquire image. Code {0}", rc.ToString());
                            continue;
                        }

                        //frame was acquired
                        _m_latestBuffer = buffer.m_BufferNumber;
                        if (lastBufferNumber != uint.MaxValue && (lastBufferNumber + 1) % nBuffers != _m_latestBuffer)
                            System.Diagnostics.Debug.WriteLine("We have likely dropped at least one frame");
                        lastBufferNumber = _m_latestBuffer;
                        //get pointer to the start of the buffer - WAIT: How do we know where it ends???
                        //for now keep all image dimensions dividable by 4 and hope for the best
                        var pBuff = _m_circAcq.GetBufferPointer(_m_latestBuffer);
                        image.Image = (byte*)pBuff;

                        if (FrameIndex % (frameRate / baselineFrameRate) == 0)
                        {

                            trackstart = System.Diagnostics.Stopwatch.GetTimestamp();

                            allFish = ProcessBaseRateFrame(image, 0, false);

                            trackstop = System.Diagnostics.Stopwatch.GetTimestamp();
                            totalTrackTime += (trackstop - trackstart) / frequency * 1000;
                            TTCountIndex++;
                            if (TTCountIndex % 800 == 0)
                            {
                                System.Diagnostics.Debug.WriteLine("Track time in last 10s was on average: {0}ms", totalTrackTime / 800);
                                totalTrackTime = 0;
                            }

                        }
                        //draw bounding box on image to visualize tracking - draw images only once a second


                        //reset back to 50*10 when at the computer
                        if (allFish != null && FrameIndex % frameRate == 0)
                        {
                            foreach (BlobWithMoments fish in allFish)
                            {
                                if (fish != null)
                                {
                                    ip.ippiSet_8u_C1R(255, image[fish.BoundingBox.x, fish.BoundingBox.y], image.Stride, new IppiSize(fish.BoundingBox.width, 1));
                                    ip.ippiSet_8u_C1R(255, image[fish.BoundingBox.x, fish.BoundingBox.y], image.Stride, new IppiSize(1, fish.BoundingBox.height));
                                    ip.ippiSet_8u_C1R(255, image[fish.BoundingBox.x, fish.BoundingBox.y + fish.BoundingBox.height - 1], image.Stride, new IppiSize(fish.BoundingBox.width, 1));
                                    ip.ippiSet_8u_C1R(255, image[fish.BoundingBox.x + fish.BoundingBox.width - 1, fish.BoundingBox.y], image.Stride, new IppiSize(1, fish.BoundingBox.height));
                                }
                            }
                            try
                            {
                                Image.Write(image, stop);
                                ImageTrack.Write(_trackers[_plateIndex].Tracker.Foreground, stop);
                            }
                            catch (OperationCanceledException)
                            {
                                break;
                            }
                        }
                        //make buffer available and update our frame counter
                        _m_circAcq.SetBufferStatus(_m_latestBuffer, BufferStatus.Available);

                        FrameIndex++;
                    }
                }
            }
            finally
            {
                if(_m_circAcq != null)
                {
                    _m_circAcq.StopAcquisition(AcqControlOptions.Async);
                    clearAcquisition();
                    _m_circAcq = null;
                }
                CleanupTrackers();
            }
        }

        SimpleDarkFlash[] CreateSDFExperiments(Saver saver, out int[] flipIndices, out int[] flipTo)
        {
            if (_plateCount > 2)
                throw new NotImplementedException("Currently can't properly deal with more than 2 plates");
            SimpleDarkFlash[] ex_array = new SimpleDarkFlash[_plateCount];
            int DarkFlashInterval = 60 * frameRate;//the interval between dark-flashes in frames
            int StimPerBlock = 60;
            //rest time is elongated by two minutes. Mirror will flip after training of Plate1 finishes. one minute later, plate2 will start training. After mirror flips back
            //plate one will then still have one more minute of resting, and so on
            int rest_elongation =  2*60 * frameRate;
            int total_block_length = DarkFlashInterval * StimPerBlock; // 
            int RestTime = total_block_length + rest_elongation; // if I set this to frameRate + rest_elongation, switching logic fails?

            int acclimatization = 20 * 60 * frameRate;//20 minutes of plate acclimatization for plate 0
            List<int> fl_indices = new List<int>();
            List<int> fl_to = new List<int>();
            //TODO: The following is rather ugly since it is specialized for one or two plates only. Maybe rework in the future
            for(int i = 0; i < _plateCount; i++)
            {
                SimpleDarkFlash dflash = null;
                var flashWriter = saver.GetStreamWriter(string.Format("_{0}.tap", i));
                if (i == 0)
                {
                    dflash = new SimpleDarkFlash(acclimatization, flashWriter, frameRate, NumFrames);
                }
                else
                {
                    dflash = new SimpleDarkFlash(acclimatization + rest_elongation / 2 + total_block_length, flashWriter, frameRate, NumFrames);
                }
                dflash.DarkFlashStartHour = 0;//allow dark-flashes to occur day-round
                dflash.DarkFlashStopHour = 24;
                dflash.DayStart = 0;
                dflash.DayEnd = 24;
                dflash.RecordEvery = 1;//record only every Nth burst
                dflash.DarkFlashIntervalMin = DarkFlashInterval;//at most one darkflash every 60 seconds
                dflash.DarkFlashIntervalMax = DarkFlashInterval;//at least one darkflash every 60 seconds
                dflash.StimPerBlock = StimPerBlock;
                dflash.nBlocks = 7;
                dflash.RestTime = RestTime;
                dflash.RetentionTime = 12 * 60 * 60 * frameRate; // the delay before a final block of testing. 
                dflash.RetentionISI = 60 * frameRate; // the ISI used during the recall test. 
                dflash.RecordPre = BurstPreFrames;
                dflash.RecordPost = BurstPostFrames;
                dflash.Init();
                //Build flip indices from plate i to plate i+1
                int first_flip = dflash.InitialCountdown + total_block_length + frameRate;//flip 1s after block is finished
                for(int j = 0; j < dflash.nBlocks; j++)
                {
                    fl_indices.Add(first_flip + j * (total_block_length + dflash.RestTime));
                    fl_to.Add((i + 1) % _plateCount);
                }
                //Add one more flip after the first plates retention is over
                if(i==0)
                {
                    var all_flashes = dflash.FlashFrames();
                    int last_flash = all_flashes[all_flashes.Length - 2];//NOTE: Last index is total frame count *not* last real dark flash frame, hence -2 not -1!!
                    fl_indices.Add(last_flash + frameRate);
                    fl_to.Add(1);
                }
                ex_array[i] = dflash;
            }
            flipIndices = fl_indices.ToArray();
            flipTo = fl_to.ToArray();
            //sort our arrays both depending on the flip indices
            Array.Sort(flipIndices, flipTo);
            return ex_array;
        }



        /// <summary>
        /// The thread method to run an actual experiment
        /// </summary>
        /// <param name="stop">Signals the thread to stop</param>
        /// <param name="dispOwner">The thread's owners dispatcher</param>
        void TrackThreadRun(AutoResetEvent stop, Dispatcher dispOwner)
        {


            SerialPort flashTeensy = new SerialPort(TeensyPort, 57600);
            flashTeensy.Open();

            FrameIndex = 0;

            //start on the first plate
            _plateIndex = 0;

            //new saver for saving data and images with consistent file-names
            var saver = new Saver("Data", ExperimentName + "_" + FishType, true);
            //a text writer for noting down experiment information
            var infoWriter = saver.GetStreamWriter(".info");

            //the writer for writing down our dark-flash frames (and types where necessary)
            var flashWriter = saver.GetStreamWriter(".tap");

            int[] flipIndices = null;
            int[] flipTo = null;
            _experiments = CreateSDFExperiments(saver, out flipIndices, out flipTo);
            //Send our arrays one-by-one to the teensy
            //This will do the following: For each plate that exists, it will send that plates flash indices in bulk to the teensy
            //IFF there is more than one plate we will also send two more arrays: The first array contains all frames in which
            //the mirror should be flipped. The second array (of the same length) contains the plate number TO which the mirror should flip
            foreach (var e in _experiments)
                SendPayloadToTeensy(e.FlashFrames(), flashTeensy);
            if (_plateCount > 1)
            {
                SendPayloadToTeensy(flipIndices, flashTeensy);
               // SendPayloadToTeensy(flipTo, flashTeensy); for now, teensy does not need to know about the flipTo, since it just changes the plate whenever asked. 
            }
            _switchQueue = new Queue<Tuple<int, int>>();
            for(int i = 0; i < flipIndices.Length; i++)
            {
                _switchQueue.Enqueue(new Tuple<int, int>(flipIndices[i], flipTo[i]));
            }
            
            

            TiffWriter burstWriter = null;

            //We create and start a task on the thread-pool to handle burst image writing
            //we also create an event that allows us to signal it to stop
            AutoResetEvent stopWrite = new AutoResetEvent(false);
            _burstBuffer = new PrCoImageRingBuffer(ImageWidth, ImageHeight, 2 * frameRate);//2s of burst buffer
            Task burstTask = Task.Factory.StartNew(() =>
            {
                using (Image8 toWrite = new Image8(ImageWidth, ImageHeight))
                {
                    while (!stopWrite.WaitOne(0))
                    {
                        try
                        {
                            if (_burstBuffer != null)
                                _burstBuffer.Consume(toWrite, stopWrite);
                        }
                        catch (OperationCanceledException)
                        {
                            break;
                        }
                        lock (_burstLock)
                        {
                            if (burstWriter != null)
                                burstWriter.WriteFrame(toWrite);
                        }
                    }
                    System.Diagnostics.Debug.WriteLine("Left burst writing thread.");
                }
            });


            try
            {
                //write down experiment information
                infoWriter.WriteLine("Start Date: {0}", DateTime.Now);
                infoWriter.WriteLine("Experiment Name: {0}", ExperimentName);
                infoWriter.WriteLine("Fish type: {0}", FishType);
                infoWriter.WriteLine("Comment: {0}", Comment);
                infoWriter.WriteLine("TotalFrames: {0}", NumFrames);
                infoWriter.WriteLine("TrackFileFormat: <FrameNumber>TAB<well1-xPos>TAB<well1-yPos>TAB<well1-angleDeg>TAB....TAB<well24-angleDeg>TAB-NEWLINE");
                infoWriter.WriteLine("MomentFileFormat: Binary file. Continuous array of doubles. <well1-Area><well1-Eccentricity><well1-Central20><well1-Central11><well1-Central02><well1-Central30><well1-Central21><well1-Central12><well1-Central03>...<well24-Central03><well1-Area>...");
                infoWriter.WriteLine("Deltapixel file format: Text file. Each frame on new line. For each frame, delta-pixel sum for each well as well as synthetic empty well is written to file");
                


                //create circular acquisition object and connect to board
                _m_circAcq = new CircularAcquisition();
                setupBoard(0, nBuffers);

                int reInitCount = 0;//if > 0 indicates that we want to force full backgroun updates after switching plates

                double frameTimestamp = 0; // will store the timestamp of the current frame
                double lastFrameTimestamp = 0; // keeps track of the timestamp from the previous frame, we will use this to make sure the camera is acquiring images at the proper frameRate and no frames are dropped because of triggering or acquisition problems

                using (Image8 image = new Image8(ImageWidth, ImageHeight, IntPtr.Zero))
                {


                    _m_circAcq.StartAcquisition(AcqControlOptions.Wait);//Start camera: Maybe want to switch options to Async later but unclear for now!

                    InitializeTracker(ImageWidth, ImageHeight, saver);

                    BufferInfo buffer = new BufferInfo();

                    var rc = WaitFrameDoneReturns.FrameAcquired;


                    ushort retry_counter = 0;//This counter keeps track how many times IN A ROW we have timed out, to let us pull the plug if the camera just went off




                    //frame-grabbing loop
                    while (!stop.WaitOne(0))

                    {
                        if (FrameIndex == 0)
                            SendTriggerToTeensy(flashTeensy);

                        //FRAME ACQUISITION
                        try
                        {
                            //In case we needed to clear buffers, we use the same logic as after switching plates to force full
                            //background updates for 2 minutes and we also write a diagnostic image to disk
                            if (CheckAndClearBuffers(_m_circAcq.GetBufferQueueSize(), ref buffer))
                            {
                                try
                                {
                                    TiffWriter diagWriter = saver.GetTiffWriter(string.Format("_clearDiag_{0}.tif", FrameIndex), true);
                                    TrackerFilePair tracktuple = _trackers[_plateIndex];
                                    diagWriter.WriteFrame(tracktuple.Tracker.Foreground);
                                    diagWriter.Dispose();
                                }
                                catch { }//don't let a problem with writing this diagnostic tiff ruin our day...
                                reInitCount = baselineFrameRate * 120;
                            }
                            rc = _m_circAcq.WaitForFrameDone(500, ref buffer);//NOTE: Originally had timeout set to 2, however that resulted in a timeout
                        }
                        catch (TimeoutException)
                        {
                            System.Diagnostics.Debug.WriteLine("Timed out on acquisition, retry {0}.", retry_counter);
                            if (retry_counter >= 100)
                            {
                                System.Diagnostics.Debug.WriteLine("Retries unsuccessful, aborting experiment");
                                break;
                            }
                            //Increment retry counter and try to recover
                            retry_counter++;
                            continue;
                        }
                        if (rc != WaitFrameDoneReturns.FrameAcquired)
                        {
                            //something went wrong
                            System.Diagnostics.Debug.WriteLine("!! Could not acquire image. Code {0}", rc);
                            continue;
                        }
                        //END FRAME ACQUISITION
                        //frame was acquired - reset our retry counter
                        retry_counter = 0;

                        //obtain buffer number and timestamp
                        _m_latestBuffer = buffer.m_BufferNumber;
                        
                        // copy previous timestamp and update the new one
                        lastFrameTimestamp = frameTimestamp; 
                        frameTimestamp = buffer.m_TimeStamp.m_TotalSec;

                        //get pointer to the start of the buffer - WAIT: How do we know where it ends???
                        //for now keep all image dimensions dividable by 4 and hope for the best
                        var pBuff = _m_circAcq.GetBufferPointer(_m_latestBuffer);
                        //encapsulate the image at pBuff in an Image8 class
                        image.Image = (byte*)pBuff;


                        //hand every frame to our full-rate processor and every baseline frame in addition
                        //to the baseline frame processor
                        bool createNewTiffWriter;//indicates that we need to create a new tiff-writer for the next burst
                        ProcessFullRateFrame(image, out createNewTiffWriter);
                        //update the plate index (if necessary) BEFORE we create a new tiff writer
                        if (UpdatePlateIndex(FrameIndex))
                        {
                            reInitCount = baselineFrameRate * 120;//120 seconds of full updates
                            _lastFlipFrame = FrameIndex; // copy the last flip frame index
                        }
                        if (createNewTiffWriter)
                        {
                            //dispose old writer to flush, if it existed
                            //NOTE: AT this point we need to resynch with our burst writing thread (hence the lock)
                            //this means: It's better done writing those frames!!
                            lock (_burstLock)
                            {
                                if (burstWriter != null)
                                    burstWriter.Dispose();
                                //create new tiffwriter for this tap's frames
                                burstWriter = new TiffWriter(Path.Combine(saver.SavePath, saver.BaseName + "_tap_" + "_plate_" + _plateIndex.ToString() + '_' + _experiments[_plateIndex].BurstCount.ToString("D4") + ".tif"));
                            }
                            // add the current background image as the first image to our burst queue
                            if (_trackers[_plateIndex].Tracker != null && _trackers[_plateIndex].Tracker.Background != null)
                                _burstBuffer.Produce(_trackers[_plateIndex].Tracker.Background);
                            else
                                System.Diagnostics.Debug.WriteLine("Background Image not written to disk");
                        }

                        if (FrameIndex % (frameRate / baselineFrameRate) == 0 && ((FrameIndex - _lastFlipFrame) > 2*frameRate)) // process at baseline frame rate, unless we are within 2 secods of a flip
                        {

                       
                           
                                ProcessBaseRateFrame(image, frameTimestamp, reInitCount > 0);
                            
                            //lets not do that forever
                            if (reInitCount > 0)
                                reInitCount--;
                        }

                        //display only once a second
                        if (FrameIndex % frameRate == 0)
                        {
                            try
                            {

                                Image.Write(image, stop);
                                ImageTrack.Write(_trackers[_plateIndex].Tracker.Foreground, stop);

                            }
                            catch (OperationCanceledException)
                            {
                                break;
                            }
                        }

                        //mark buffer as available, increment frame index and leave experiment if we are at the end
                        _m_circAcq.SetBufferStatus(_m_latestBuffer, BufferStatus.Available);

                        // check that the camera timestamp is correct, and we didnt miss any frames and lose clock time. Convert to uint. This will round to the nearest integer, and so natural jitter of the timestamps can not exceed 1/2 the frame time. 

                        uint framesElapsed =Convert.ToUInt32(frameRate * (frameTimestamp - lastFrameTimestamp));


                        if (FrameIndex == 0) // nothing to compare to on the first image
                        {
                            FrameIndex++;
                        }
                        else if (framesElapsed > 1) // more than one frames worth of time has elapsed
                        {
                            FrameIndex = FrameIndex + framesElapsed;
                            System.Diagnostics.Debug.WriteLine("!! Frame timestamp mismatch, jumping forward {0} frames to frame: {1}", framesElapsed, FrameIndex);

                            for (int k = 0; k < framesElapsed; k++) // Temporary solution to problem of creating/deleting new tiff Writers. Copy paste code from above for full frame rate handling. run the same image again through the ProcessFullFrameRate, to make sure we dont miss an important event like disposing of or creating a TIFF writer. 

                            {
                                ProcessFullRateFrame(image, out createNewTiffWriter);

                                if (createNewTiffWriter)
                                {
                                    //dispose old writer to flush, if it existed
                                    //NOTE: AT this point we need to resynch with our burst writing thread (hence the lock)
                                    //this means: It's better done writing those frames!!
                                    lock (_burstLock)
                                    {
                                        if (burstWriter != null)
                                            burstWriter.Dispose();
                                        //create new tiffwriter for this tap's frames
                                        burstWriter = new TiffWriter(Path.Combine(saver.SavePath, saver.BaseName + "_tap_" + "_plate_" + _plateIndex.ToString() + '_' + _experiments[_plateIndex].BurstCount.ToString("D4") + ".tif"));
                                    }
                                    // add the current background image as the first image to our burst queue
                                    if (_trackers[_plateIndex].Tracker != null && _trackers[_plateIndex].Tracker.Background != null)
                                        _burstBuffer.Produce(_trackers[_plateIndex].Tracker.Background);
                                    else
                                        System.Diagnostics.Debug.WriteLine("Background Image not written to disk");
                                }
                            }


                        }
                        else // all looks good
                        {
                            FrameIndex++;
                        }



                        if (FrameIndex >= NumFrames - 1)
                        {
                            break;
                        }
                        //NO CODE SHOULD BE LEFT WITHIN THE WHILE LOOP BEYOND THIS POINT!!!
                    }
                }
            }
            finally
            {
                //signal our burst writing task to stop
                stopWrite.Set();
                if(_m_circAcq != null)
                {
                    _m_circAcq.StopAcquisition(AcqControlOptions.Async);
                    clearAcquisition();
                    _m_circAcq = null;
                }

                CleanupTrackers();
                //if our experiments need to be disposed, do it now
                foreach(var e in _experiments)
                {
                    if (e != null && e is IDisposable)
                        (e as IDisposable).Dispose();
                }
                _experiments = null;
                //before removing resources that our burst task relies on
                //wait for it to stop - but at most one second...
                if (!burstTask.Wait(1000))
                    System.Diagnostics.Debug.WriteLine("Prematurely killed burst task");
                burstTask.Dispose();
                lock (_burstLock)
                {
                    if (burstWriter != null)
                    {
                        burstWriter.Dispose();
                        burstWriter = null;
                    }
                    if (_burstBuffer != null)
                    {
                        _burstBuffer.Dispose();
                        _burstBuffer = null;
                    }
                }
                if (infoWriter != null)
                {
                    infoWriter.WriteLine("");
                    infoWriter.WriteLine("Experiment ended: {0}", DateTime.Now);
                    infoWriter.Dispose();
                }

                if (flashTeensy != null)
                    flashTeensy.Dispose();
                
            }
            DispatcherHelper.CheckBeginInvokeOnUI(Stop);
        }


        #endregion

        #region CommandSinks

        RelayCommand _startStopClick;
       

        /// <summary>
        /// Command that gets invoked if start/stop is clicked
        /// </summary>
        public ICommand StartStopClick
        {
            get
            {
                if (_startStopClick == null)
                    _startStopClick = new RelayCommand(param => { if (IsRunning) Stop(); else Start(); });
                return _startStopClick;
            }
        }

        /// <summary>
        /// Starts an experiment
        /// </summary>
        void Start()
        {
            if (_previewThread != null)
            {
                _previewThread.Dispose();
                _previewThread = null;
            }
            _grabThread = new Worker(TrackThreadRun, true, ThreadTimeout);
            IsRunning = true;
        }

        /// <summary>
        /// Stops an experiment
        /// </summary>
        void Stop()
        {
            if (_grabThread != null)
            {
                _grabThread.Dispose();
                _grabThread = null;
            }
            if(_previewThread == null)
                _previewThread = new Worker(PreviewThreadRun, true, ThreadTimeout);
            IsRunning = false;
        }

        #endregion

        #region Cleanup

        protected override void Dispose(bool disposing)
        {
            if (_grabThread != null)
            {
                _grabThread.Dispose();
                _grabThread = null;
            }
            if (_previewThread != null)
            {
                _previewThread.Dispose();
                _previewThread = null;
            }
            if(_m_circAcq != null)
            {
                System.Diagnostics.Debug.WriteLine("Some thread didn't clean up our acquisition");
                clearAcquisition();
                _m_circAcq = null;
            }
            base.Dispose(disposing);
        }


        #endregion

    }
}
