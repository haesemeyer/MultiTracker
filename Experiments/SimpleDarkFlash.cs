using System;
using System.IO;
using System.IO.Ports;

using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SleepTracker.Experiments
{
    public class SimpleDarkFlash : ExperimentBase, IDisposable
    {
        public enum ActionType { None = 0, DarkFlash = 1 };

        #region Members

        /// <summary>
        /// The frame index of the most recent dark-flash
        /// </summary>
        uint _lastFlashFrame;

        /// <summary>
        /// Counter of the number of occured bursts
        /// </summary>
        int _burstCount;

        /// <summary>
        /// Data to write to teensy to perform dark-flash
        /// </summary>
        byte[] _byteToWrite = new byte[3];//WHY?

        /// <summary>
        /// Delay until the first dark flash frame
        /// </summary>
        int _initialCountdown;

        /// <summary>
        /// Array of frames at which a dark-flash
        /// is being performed
        /// </summary>
        int[] _flashFrames;

        /// <summary>
        /// The index of the next darkflash frame
        /// in our flash frames array
        /// </summary>
        int _nextFlashIndex;

        /// <summary>
        /// Text writer to write down the framenumber
        /// in which a dark-flash occured
        /// </summary>
        StreamWriter _flashWriter;

        Random _flashInterval = new Random();

        #endregion

        /// <summary>
        /// Creates a new simple dark flash experiment
        /// </summary>
        /// <param name="teensyWhitePort">The COM port of the teensy controlling the white lights</param>
        /// <param name="initialCountdown">The countdown to the first dark-flash</param>
        /// <param name="flashWriter">The text-writer to write down dark-flash frames</param>
        public SimpleDarkFlash(int initialCountdown, StreamWriter flashWriter, int frameRate, int totalFrames) : base(frameRate, totalFrames)
        {
            _flashWriter = flashWriter;
            _lastFlashFrame = uint.MaxValue;
            _initialCountdown = initialCountdown;
            _nextFlashIndex = 0;
            _flashFrames = null;
        }

        #region Properties

        public int InitialCountdown
        {
            get
            {
                return _initialCountdown;
            }
        }

        private bool HammerTime
        {
            get
            {
                return InHourBracket(DarkFlashStartHour, DarkFlashStopHour, CurrentHour);
            }
        }

        /// <summary>
        /// The index of the last burst
        /// </summary>
        public override int BurstCount
        {
            get
            {
                return _burstCount;
            }
        }

        /// <summary>
        /// Darkflashes will only be presented btw.
        /// _darkFlashStartHour and _darkFlashStopHour
        /// </summary>
        public int DarkFlashStartHour { get; set; } = 0;

        /// <summary>
        /// Darkflashes will only be presented btw.
        /// _darkFlashStartHour and _darkFlashStopHour
        /// </summary>
        public int DarkFlashStopHour { get; set; } = 24;

        /// <summary>
        /// Record every nth tap if > 0
        /// </summary>
        public int RecordEvery { get; set; }

        /// <summary>
        /// The minimal interval between dark-flashes
        /// </summary>
        public int DarkFlashIntervalMin { get; set; }

        /// <summary>
        /// The maximum interval between dark-flashes
        /// </summary>
        public int DarkFlashIntervalMax { get; set; }

        /// <summary>
        /// The number of dark flashes per block
        /// </summary>
        public int StimPerBlock { get; set; }

        /// <summary>
        /// The rest time between blocks
        /// </summary>
        public int RestTime { get; set; }

        /// <summary>
        /// The number of training blocks
        /// </summary>
        public int nBlocks { get; set; }

        /// <summary>
        /// The delay before the final test
        /// </summary>
        public int RetentionTime { get; set; }

        /// <summary>
        /// The ISI used during the recall test
        /// </summary>
        public int RetentionISI { get; set; }

        #endregion

        #region Methods

        /// <summary>
        /// While the teensy performs a dark-flash writes frame index to file and resets countdown
        /// Also records las performed dark-flash
        /// </summary>
        /// <param name="index">Frame index</param>
        private void PerformDarkFlash(uint index)
        {
            //write frame number to file
            _flashWriter.WriteLine(index);
            //reset our counter
            //update our last flash frame and the next flash index
            _lastFlashFrame = index;
            _nextFlashIndex++;
        }

        /// <summary>
        /// Should be called after all parameters are set.
        /// Sets up the array of dark-flash frames
        /// </summary>
        public void Init()
        {
            _flashFrames = FlashFrames();
        }

        /// <summary>
        /// Reacts to the next frame
        /// </summary>
        /// <param name="index">Frame index</param>
        public override void PerformAction(uint index)
        {
            //First time around, create our array of dark flash frames if necessary
            if (_flashFrames == null)
                _flashFrames = FlashFrames();
            //First perform base-class behavior
            base.PerformAction(index);
            if (HammerTime)
            {
                //perform dark-flash related action if we are at the dark-flash frame
                if (_nextFlashIndex < _flashFrames.Length && index == _flashFrames[_nextFlashIndex])
                    PerformDarkFlash(index);
            }
        }

        /// <summary>
        /// Returns whether the current frame should be recorded or not
        /// </summary>
        /// <param name="index">The frame index</param>
        /// <param name="newRecord">Indicates that a new record should be started</param>
        /// <returns>True if the frame should be recorded</returns>
        public override bool RecordFrame(uint index, out bool newRecord)
        {
            int dFrames = _flashFrames[_nextFlashIndex] - (int)index;//the number of frames between the current frame and the next dark-flash
            if (dFrames < 0)
                dFrames = int.MaxValue;//this condition should only occur if we are at the end of the experiment after all darkflashes have occured and then we don't want to record
            newRecord = false;//by default we don't signal to start a new recording
            if (!HammerTime)
                return false;//don't bother outside our normal time-frame

            bool retval = false;
            if(RecordEvery<=1 || (_burstCount+1)%RecordEvery==0 || _burstCount <= 1) // record all if RecordEvery, also always record first, use (_burstCount+1)%RecordEvery==0 so that when record every = 2, we still get the first stim in each even numbered block. 
            {
                //this mean that either RecordEvery was set to 1 or smaller => record every event
                //or that this is the nth event in a chain and hence should be recorded IFF we are within the correct frame window
                if (dFrames - RecordPre <= 0 || index <= _lastFlashFrame + RecordPost)
                    retval = true;
                //one frame before we actually start recording, we signal that a new recording is about to start
                if (dFrames - RecordPre == 1)
                    newRecord = true;
            }
            //if we just passed the post-period, update _burstCount
            if(_lastFlashFrame < uint.MaxValue && index == _lastFlashFrame + RecordPost)
            {
                _burstCount++;
            }
            return retval;
        }

        public override int[] FlashFrames()
        {
            List<int> flashFrames = new List<int>();
            // build preliminary list of all possible frames - than filter according
            // to predicted frame time
            //flashFrames.Add(_initialCountdown);//our first dark-flash frame
            int frame = _initialCountdown;
           // do
            //{
                for (int block = 0; block < nBlocks; block++)
                {
                    if (block > 0)
                        frame = frame + RestTime; // add in delays for rest blocks
                    
                        
                    for (int i = 1; i <= StimPerBlock; i++)
                    {
                    flashFrames.Add(frame);
                    if (DarkFlashIntervalMin == DarkFlashIntervalMax)
                            frame += DarkFlashIntervalMin;
                        else
                            frame += _flashInterval.Next(DarkFlashIntervalMin, DarkFlashIntervalMax);

                        
                    }
                }

                frame = frame + RetentionTime; // can add a longer delay before a restest.

                for (int i = 1; i <= StimPerBlock; i++)
                {

                   frame += RetentionISI;

                   flashFrames.Add(frame);
                }

            flashFrames.Add(TotalFrames); // add the last frame to avoid the indexing problem when we are in the last flash frame recorded in line 197. This should be fixed with a better solution

            return flashFrames.ToArray();
            //} while (frame <= TotalFrames);

            ////NOTE: The following is rather ugly (having two lists) but otherwise would need
            ////to loop through a list that is being modified
            //List<int> confirmedFrames = new List<int>();
            //foreach(int f in flashFrames)
            //{
            //    int frameHour = (StartTime + TimeOffset(f)).Hour;
            //    if (InHourBracket(DarkFlashStartHour, DarkFlashStopHour, frameHour))
            //        confirmedFrames.Add(f);
            //}
            //return confirmedFrames.ToArray();
        }

        #endregion

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    if(_flashWriter != null)
                    {
                        _flashWriter.Dispose();
                        _flashWriter = null;
                    }
                }

                //free unmanaged resources (unmanaged objects) and override a finalizer below.

                disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~SimpleDarkFlash() {
        //   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        //   Dispose(false);
        // }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            // TODO: uncomment the following line if the finalizer is overridden above.
            // GC.SuppressFinalize(this);
        }
        #endregion
    }
}
