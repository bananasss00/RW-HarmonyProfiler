using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace HarmonyProfiler.Profiler.Core
{
    public class PatchDisabler
    {
        public const string Id = "harmony.pirateby.profiler.disabler";
        public static readonly Harmony H = new Harmony(Id);
        private static readonly HarmonyMethod MethodPrefix =
            new HarmonyMethod(AccessTools.Method(typeof(PatchDisabler), nameof(Disabler))) {priority = int.MaxValue - 1}; // -1 set disabler after profiler prefix

        private static readonly HashSet<MethodBase> PatchedMethods = new HashSet<MethodBase>();

        public static int DisabledCount => PatchedMethods.Count;

        private static bool Disabler() => false;

        public static bool IsDisabled(MethodBase method) => PatchedMethods.Contains(method);

        public static void DisableMethod(MethodBase method)
        {
            if (!IsDisabled(method))
            {
                H.Patch(method, prefix: MethodPrefix);
                PatchedMethods.Add(method);
            }
        }

        public static void EnableMethod(MethodBase method)
        {
            if (IsDisabled(method))
            {
                H.Unpatch(method, HarmonyPatchType.Prefix, Id);
                PatchedMethods.Remove(method);
            }
        }

        public static void EnableAllDisabled()
        {
            foreach (var method in PatchedMethods)
            {
                H.Unpatch(method, HarmonyPatchType.Prefix, Id);
            }
            PatchedMethods.Clear();
        }
    }
}