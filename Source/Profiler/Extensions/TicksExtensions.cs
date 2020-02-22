using System;
using System.Diagnostics;

namespace HarmonyProfiler.Profiler.Extensions
{
    public static class TicksExtensions
    {
        public static double TicksToMs(this long ticks, int digits)
        {
            return Math.Round(((double)ticks / Stopwatch.Frequency) * 1000L, digits);
        }

        public static long TicksToMs(this long ticks)
        {
            return (long) Math.Round(((double)ticks / Stopwatch.Frequency) * 1000L);
        }

        public static long TicksToSeconds(this long ticks)
        {
            return (long) Math.Round(((double)ticks / Stopwatch.Frequency));
        }
    }
}