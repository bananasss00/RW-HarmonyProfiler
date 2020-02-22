using System;
using System.Diagnostics;
using System.Text;
using Verse;

namespace HarmonyProfiler.Profiler
{
    public static class Logger
    {
        private static StringBuilder sb = new StringBuilder();

        public static void Add(string s)
        {
            //Log.Warning(s);
            lock (sb) sb.AppendLine(s);
        }

        public static void Flush(string fileName)
        {
            lock (sb) FS.AppendAllText(fileName, sb.ToString());
        }

        public static void Clear()
        {
            lock (sb) sb = new StringBuilder();
        }

        public static void LogOperation(string nameOperation, Action action)
        {
            Clear();
            Stopwatch sw = Stopwatch.StartNew();

            action();

            sw.Stop();
            string text = $"== Operation complete in {(int) Math.Round(sw.ElapsedMilliseconds / 1000f)} sec. ==";
            Add(text);
            Log.Warning(text);
            Flush($"{nameOperation}_{DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss")}.log");
        }
    }
}