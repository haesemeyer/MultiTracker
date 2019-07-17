using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SleepTracker.Experiments
{
    /// <summary>
    /// Provides methods and properties shared by
    /// all experiment implementations
    /// </summary>
    public interface IExperiment
    {
        /// <summary>
        /// Determines if the frame should be written to disk
        /// </summary>
        /// <param name="index">The frame index</param>
        /// <param name="newRecord">Indicates whether a new recording should be started</param>
        /// <returns>True if the frame should be written to disk</returns>
        bool RecordFrame(uint index, out bool newRecord);

        /// <summary>
        /// Gives the experiment the opportunity to react
        /// to the frame (switch lights, do nothing, enter next phase, etc.)
        /// </summary>
        /// <param name="index">The frame index</param>
        void PerformAction(uint index);

        /// <summary>
        /// Computes an array of frames in which the lights should be toggled
        /// </summary>
        /// <param name="expFrames">The length of the experiment in frames</param>
        /// <returns>An array of dark-flash frames</returns>
        int[] FlashFrames();

        int BurstCount { get; }
    }
}
