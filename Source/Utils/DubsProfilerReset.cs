using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Verse;

namespace HarmonyProfiler
{
    public static class DubsProfilerReset
    {
        private static readonly string DubId = "Dubwise.DubsPerfAnal.DubsPerformanceAnalyzer";
        private static readonly Harmony DubInst = new Harmony(DubId);

        private static bool initialized;
        private static Type beforeMainTabs, dialogAnalyzer, analyzer, profileTab;
        private static FieldInfo dialogAnalyzer_mainTabs, profileTab_Modes, dialogAnalyzer_PatchedEverything, analyzer_RequestStop;
        private static MethodInfo beforeMainTabs_PatchMe;

        public static object GetAttr(this Type t, string name)
        {
            return t.GetCustomAttributes(false).FirstOrDefault(x => x.GetType().Name.Equals("PerformancePatch"));
        }

        public static bool CanUnpatch()
        {
            if (!initialized)
            {
                beforeMainTabs = AccessTools.TypeByName("DubsAnalyzer.H_MapInterfaceOnGUI_BeforeMainTabs");
                dialogAnalyzer = AccessTools.TypeByName("DubsAnalyzer.Dialog_Analyzer");
                analyzer = AccessTools.TypeByName("DubsAnalyzer.Analyzer");
                profileTab = AccessTools.TypeByName("DubsAnalyzer.ProfileTab");
                dialogAnalyzer_mainTabs = dialogAnalyzer?.GetField("MainTabs");
                beforeMainTabs_PatchMe = beforeMainTabs?.GetMethod("PatchMe");
                profileTab_Modes = profileTab?.GetField("Modes");
                dialogAnalyzer_PatchedEverything = dialogAnalyzer?.GetField("PatchedEverything");
                analyzer_RequestStop = analyzer?.GetField("RequestStop", BindingFlags.NonPublic | BindingFlags.Static);
                //Log.Warning($"beforeMainTabs {beforeMainTabs != null}");
                //Log.Warning($"dialogAnalyzer {dialogAnalyzer != null}");
                //Log.Warning($"analyzer {analyzer != null}");
                //Log.Warning($"profileTab {profileTab != null}");
                //Log.Warning($"dialogAnalyzer_mainTabs {dialogAnalyzer_mainTabs != null}");
                //Log.Warning($"beforeMainTabs_PatchMe {beforeMainTabs_PatchMe != null}");
                //Log.Warning($"profileTab_Modes {profileTab_Modes != null}");
                //Log.Warning($"dialogAnalyzer_PatchedEverything {dialogAnalyzer_PatchedEverything != null}");
                //Log.Warning($"analyzer_RequestStop {analyzer_RequestStop != null}");
                initialized = true;
            }
            return beforeMainTabs != null
                   && dialogAnalyzer != null
                   && analyzer != null
                   && profileTab != null
                   && dialogAnalyzer_mainTabs != null
                   && beforeMainTabs_PatchMe != null
                   && profileTab_Modes != null
                   && dialogAnalyzer_PatchedEverything != null
                   && analyzer_RequestStop != null;
        }

        public static void ResetProfiler()
        {
            if (!CanUnpatch())
                return;

            // Unpatch all
            foreach (var p in HarmonyMain.GetPatches(new[] {DubId}, false))
                DubInst.Unpatch(p.Key, HarmonyPatchType.All, DubId);

            // ApplyPerfomancePatches
            beforeMainTabs_PatchMe.Invoke(null, null);
            foreach (var type in GenTypes.AllTypes.Where(x => x.GetAttr("PerformancePatch") != null))
            {
                MethodInfo methodInfo = AccessTools.Method(type, "PerformancePatch");
                methodInfo?.Invoke(null, null);
            }

            //ResetTabs
            if (dialogAnalyzer_mainTabs.GetValue(null) is IEnumerable list)
            {
                var enumerator = list.GetEnumerator();
                while (enumerator.MoveNext())
                {
                    // clear dictionary
                    var modes = profileTab_Modes.GetValue(enumerator.Current);
                    modes.GetType().GetMethod("Clear").Invoke(modes, null);
                }
            }
            dialogAnalyzer_PatchedEverything.SetValue(null, false);
            analyzer_RequestStop.SetValue(null, true);
            Log.Warning($"Unpatched: {DubId}");
        }
    }
}