using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Verse;

namespace HarmonyProfiler.Profiler.Core
{
    public static class PatchHandler
    {
        private static Dictionary<int, StopwatchRecord> _profiledMethods = new Dictionary<int, StopwatchRecord>();

        public static bool IsProfilerMethod(this MethodBase method) =>
            method.MethodHandle == PatchStartMethod.MethodHandle ||
            method.MethodHandle == PatchStopMethod.MethodHandle;

        public static bool Active { get; private set; } = false;

        public static void StartCollectData() => Active = true;

        public static void StopCollectData() => Active = false;

        public static int RemoveProfiledRecords(MethodBase method) => _profiledMethods.RemoveAll(x => x.Value.Method == method);
        
        public static int ProfiledRecordsCount() => _profiledMethods.Count;

        public static MethodBase logMethod = null;

        public static void Reset()
        {
            bool oldState = Active;
            Active = false;
            _profiledMethods.Clear();
            Active = oldState;
        }

        public static readonly MethodInfo PatchStartMethod =
            AccessTools.Method(typeof(PatchHandler), nameof(PatchHandler.Patch_Start));

        public static readonly MethodInfo PatchStopMethod =
            AccessTools.Method(typeof(PatchHandler), nameof(PatchHandler.Patch_Stop));

        // Start timer
        public static void Patch_Start(MethodBase __originalMethod, ref StopwatchRecord __state)
        {
            if (!Active) return;
            
            int ptr = __originalMethod.MethodHandle.Value.ToInt32(); // faster then gethashcode?
            if (!_profiledMethods.TryGetValue(ptr, out __state))
            {
                __state = new StopwatchRecord(__originalMethod, Settings.Get().collectMemAlloc);
                _profiledMethods.Add(ptr, __state);
            }
            __state.Start();
        }

        // Stop timer and calc avg time
        public static void Patch_Stop(ref StopwatchRecord __state)
        {
            if (!Active) return;
            __state.Stop();

            // log stacktrace
            if (logMethod != null && logMethod == __state.Method)
            {
                Log.Warning(__state.MethodName);
            }
        }

        /// <summary>
        /// Get sorted profile records by TimeSpent
        /// </summary>
        /// <returns></returns>
        public static List<StopwatchRecord> GetProfileRecordsSorted()
        {
            bool bkpState = Active;
            Active = false;

            bool sortByMemAlloc = Settings.Get().sortByMemAlloc;
            List<StopwatchRecord> records = _profiledMethods.Values
                .Where(x => x.IsValid)
                .Select(x => x.Clone())
                .OrderByDescending(x => !sortByMemAlloc ? x.TimeSpent : x.AllocKB)
                .ToList();

            Active = bkpState;
            return records;
        }
    }
}