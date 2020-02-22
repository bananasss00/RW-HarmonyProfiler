using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Harmony;

namespace HarmonyProfiler.Profiler.Core
{
    public static class PatchHandler
    {
        public static bool Active { get; private set; } = false;

        public static void StartCollectData() => Active = true;

        public static void StopCollectData() => Active = false;

        public static void Reset()
        {
            bool oldState = Active;
            Active = false;
            ProfiledMethods.Clear();
            Active = oldState;
        }

        public static Dictionary<int, StopwatchRecord> ProfiledMethods { get; } = new Dictionary<int, StopwatchRecord>();

        public static readonly MethodInfo PatchStartMethod =
            AccessTools.Method(typeof(PatchHandler), nameof(PatchHandler.Patch_Start));

        public static readonly MethodInfo PatchStopMethod =
            AccessTools.Method(typeof(PatchHandler), nameof(PatchHandler.Patch_Stop));

        // Start timer
        public static void Patch_Start(MethodBase __originalMethod, ref StopwatchRecord __state)
        {
            if (!Active) return;
            
            int ptr = __originalMethod.MethodHandle.Value.ToInt32(); // faster then gethashcode?
            if (!ProfiledMethods.TryGetValue(ptr, out __state))
            {
                __state = new StopwatchRecord(__originalMethod, Settings.Get().collectMemAlloc);
                ProfiledMethods.Add(ptr, __state);
            }
            __state.Start();
        }

        // Stop timer and calc avg time
        public static void Patch_Stop(ref StopwatchRecord __state)
        {
            if (!Active) return;
            __state.Stop();
        }

        /// <summary>
        /// Get sorted profile records by TimeSpent
        /// </summary>
        /// <returns></returns>
        public static List<StopwatchRecord> GetProfileRecordsSorted()
        {
            Active = false;

            bool sortByMemAlloc = Settings.Get().sortByMemAlloc;
            List<StopwatchRecord> records = ProfiledMethods.Values
                .Where(x => x.IsValid)
                .Select(x => x.Clone())
                .OrderByDescending(x => !sortByMemAlloc ? x.TimeSpent : x.AllocKB)
                .ToList();

            Active = true;
            return records;
        }
    }
}