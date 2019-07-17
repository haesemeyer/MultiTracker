using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SleepTracker.Experiments
{
    /// <summary>
    /// Base class of experiment types - takes care of some housekeeping
    /// at every frame and provides helper methods
    /// </summary>
    public abstract class ExperimentBase : IExperiment
    {
        /// <summary>
        /// The current hour in the day
        /// </summary>
        protected int CurrentHour { get; private set; }

        /// <summary>
        /// The current day since experiment start
        /// </summary>
        protected int CurrentDay { get; private set; } = 1;

        /// <summary>
        /// True if on last check it was day-time 
        /// </summary>
        protected bool Daytime { get; private set; }

        /// <summary>
        /// The frame-rate of the experiment to relate
        /// frame indices to time
        /// </summary>
        protected int FrameRate { get; private set; }

        /// <summary>
        /// The total number of frames in the experiment
        /// </summary>
        protected int TotalFrames { get; private set; }

        /// <summary>
        /// The start time of the experiment - the creation
        /// time of the experimental class for clock
        /// synchronization
        /// </summary>
        protected DateTime StartTime { get; private set; }

        /// <summary>
        /// The canonical start of day time
        /// </summary>
        public int DayStart { get; set; } = 9;

        /// <summary>
        /// The canonical end of day time
        /// </summary>
        public int DayEnd { get; set; } = 23;

        /// <summary>
        /// The number of frames to record before an event
        /// </summary>
        public uint RecordPre { get; set; }

        /// <summary>
        /// The number of frames to record after an event
        /// </summary>
        public uint RecordPost { get; set; }

        public ExperimentBase(int frameRate, int totalFrames)
        {
            CurrentHour = DateTime.Now.Hour;
            Daytime = InHourBracket(DayStart, DayEnd, CurrentHour);
            FrameRate = frameRate;
            TotalFrames = totalFrames;
            StartTime = DateTime.Now;
        }

        /// <summary>
        /// Determines wheter a given our is within a provided hour bracket on the clock
        /// taking into account that start and end values can be on either side of 0
        /// </summary>
        /// <param name="hourStart">The starting hour of our bracket</param>
        /// <param name="hourEnd">The ending hour of our bracket</param>
        /// <param name="currentHour">The current hour</param>
        /// <returns>True if within bracket</returns>
        protected bool InHourBracket(int hourStart, int hourEnd, int currentHour)
        {
            if (hourStart < hourEnd)
            {
                if (currentHour >= hourStart && currentHour < hourEnd)
                    return true;
            }
            else
            {
                if (currentHour >= hourStart || currentHour < hourEnd)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// For a given frame-index returns the timespan
        /// offset
        /// </summary>
        /// <param name="frameIndex">The frame index</param>
        /// <returns>Offset of that frame index with respect to experiment time</returns>
        protected TimeSpan TimeOffset(int frameIndex)
        {
            double seconds = frameIndex / FrameRate;
            return TimeSpan.FromSeconds(seconds);
        }

        public virtual void PerformAction(uint index)
        {
            //once a second update the hour of the day
            //and increment day counter if a new day started
            if (index % SleepTracker.ViewModels.MainViewModel.frameRate == 0)
            {
                CurrentHour = DateTime.Now.Hour;
                if (InHourBracket(DayStart, DayEnd, CurrentHour))
                {
                    if (!Daytime)
                        CurrentDay++;
                    Daytime = true;
                }
                else
                {
                    Daytime = false;
                }
            }
        }

        public abstract bool RecordFrame(uint index, out bool newRecord);

        public abstract int[] FlashFrames();

        public abstract int BurstCount { get; }
    }
}
