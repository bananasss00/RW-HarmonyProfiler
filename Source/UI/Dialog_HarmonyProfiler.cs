using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Harmony;
using HarmonyProfiler.Profiler;
using HarmonyProfiler.Profiler.Core;
using HarmonyProfiler.Profiler.Extensions;
using UnityEngine;
using Verse;
using Logger = HarmonyProfiler.Profiler.Logger;

namespace HarmonyProfiler.UI
{
    public class Dialog_HarmonyProfiler : Window
    {
        private static readonly Color TitleLabelColor = Color.yellow;

        private Vector2 instancesScrollPos, modsScrollPos, customScrollPos;
        
        public override Vector2 InitialSize => new Vector2(900f, 900f);

        public Dialog_HarmonyProfiler() {
            closeOnCancel = true;
            closeOnAccept = false;
            doCloseButton = false;
            doCloseX = true;
            resizeable = true;
            draggable = true;
        }

        public override void DoWindowContents(Rect rect) {
            var lister = new Listing_Extended();
            var settings = Settings.Get();

			lister.Begin(rect);
		    lister.ColumnWidth = 400;
            lister.verticalSpacing = 1f;

            DrawInstanceProfiler(lister, settings);
            DrawModsProfiler(lister, settings);
            DrawCustomProfiler(lister, settings);
            DrawSettings(lister, settings);
            DrawOther(lister, settings);

            lister.NewColumn();

            DrawProfilerTop15(lister, settings);

		    lister.End();
        }

        private void DrawInstanceProfiler(Listing_Extended lister, Settings settings)
        {
            lister.LabelColored("Profile Harmony Instances", TitleLabelColor);
            Rect buttonRect1 = lister.GetRect(Text.LineHeight),
                buttonRect2 = buttonRect1;

            buttonRect2.width = buttonRect1.width /= 2;
            buttonRect2.x = buttonRect1.xMax;

            if (Widgets.ButtonText(buttonRect1, "Get instances"))
            {
                settings.profileInstances = String.Join("\n", HarmonyMain.GetAllHarmonyInstances().ToArray());
            }
            if (!settings.profileInstances.IsNullOrEmpty())
            {
                lister.CheckboxLabeled("Allow transpiled methods(slow patching)", ref settings.allowTranspiledMethods);
                if (Widgets.ButtonText(buttonRect2, $"Profile instances"))
                {
                    var instances = settings.profileInstances.Split(new[] {"\r\n", "\n"}, StringSplitOptions.RemoveEmptyEntries);
                    Profiler.Logger.LogOperation("PatchesProfiling", () => Patcher.ProfileHarmonyPatches(instances, true, !settings.allowTranspiledMethods));
                }
            }
            settings.profileInstances = lister.TextArea(settings.profileInstances, 5, ref instancesScrollPos);
            lister.Gap(20f);
        }

        private void DrawModsProfiler(Listing_Extended lister, Settings settings)
        {
            lister.LabelColored("Profile Mods", TitleLabelColor);

            Rect buttonRect1 = lister.GetRect(Text.LineHeight),
                buttonRect2 = buttonRect1;

            buttonRect2.width = buttonRect1.width /= 2;
            buttonRect2.x = buttonRect1.xMax;

            if (Widgets.ButtonText(buttonRect1, "Get mods"))
            {
                settings.profileMods = String.Join("\n", LoadedModManager.RunningModsListForReading.Select(x => Path.GetFileName(x.RootDir)).ToArray());
            }
            if (!settings.profileMods.IsNullOrEmpty())
            {
                lister.CheckboxLabeled("Allow Core assembly", ref settings.allowCoreAsm);
                if (Widgets.ButtonText(buttonRect2, $"Profile mods"))
                {
                    var mods = settings.profileMods.Split(new[] {"\r\n", "\n"}, StringSplitOptions.RemoveEmptyEntries);
                    Profiler.Logger.LogOperation("ModsProfiling", () => Patcher.ProfileMods(mods.ToList()));
                }
            }
            settings.profileMods = lister.TextArea(settings.profileMods, 5, ref modsScrollPos);
            lister.Gap(20f);
        }

        private void DrawCustomProfiler(Listing_Extended lister, Settings settings)
        {
            lister.LabelColored("Profile Custom(Methods,Classes,Namespaces)", TitleLabelColor);
            Rect buttonRect1 = lister.GetRect(Text.LineHeight),
                buttonRect2 = buttonRect1;

            buttonRect2.width = buttonRect1.width /= 2;
            buttonRect2.x = buttonRect1.xMax;

            if (Widgets.ButtonText(buttonRect1, "Profile all dlls"))
            {
                var methods = new HashSet<string>();
                var modDllNames = Utils.GetAllModsDll();
                //File.WriteAllLines("dsdsd", modDllNames.ToArray());
                foreach (var modDllName in modDllNames)
                {
                    //Log.Warning($"[dll] {modDllName}");
                    foreach (var @class in Utils.GetClassesFromDll(modDllName))
                    {
                        methods.AddDefMethodsAdvanced(@class, settings.allowCoreAsm, !settings.allowInheritedMethods);
                    }
                }
                Profiler.Logger.LogOperation("DllProfiling", () => Patcher.ProfileMethods(methods.ToArray()));
            }

            if (!settings.profileCustom.IsNullOrEmptyOrEqual(Settings.CustomExampleStr))
            {
                lister.CheckboxLabeled("Allow Core assembly", ref settings.allowCoreAsm);
                lister.CheckboxLabeled("Allow class inherited methods", ref settings.allowInheritedMethods);
                if (Widgets.ButtonText(buttonRect2, $"Profile custom methods"))
                {
                    var methods = new HashSet<string>();
                    var list = settings.profileCustom.Split(new[] {"\r\n", "\n"}, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var s in list)
                    {
                        // it's method
                        if (s.Contains(":"))
                        {
                            methods.Add(s);
                        }
                        else if (s.EndsWith(".dll"))
                        {
                            string dllName = s.Replace(".dll", "");
                            foreach (var @class in Utils.GetClassesFromDll(dllName))
                            {
                                methods.AddDefMethodsAdvanced(@class, settings.allowCoreAsm, !settings.allowInheritedMethods);
                            }
                        }
                        else
                        {
                            Type type = AccessTools.TypeByName(s);
                            // it's class
                            if (type?.IsClass ?? false)
                            {
                                methods.AddDefMethodsAdvanced(type, settings.allowCoreAsm, !settings.allowInheritedMethods);
                            }
                            else // parse namespace
                            {
                                var classes = Utils.GetClassesFromNamespace(s);
                                if (!classes.Any()) Log.Error($"[HarmonyProfiler] Found 0 results for namespace:'{s}'");
                                foreach (var @class in Utils.GetClassesFromNamespace(s))
                                {
                                    methods.AddDefMethodsAdvanced(@class, settings.allowCoreAsm, !settings.allowInheritedMethods);
                                }
                            }
                        }
                    }

                    Profiler.Logger.LogOperation("CustomProfiling", () => Patcher.ProfileMethods(methods.ToArray()));
                }
            }
            settings.profileCustom = lister.TextArea(settings.profileCustom, 5, ref customScrollPos);
            lister.Gap(20f);
        }

        private void DrawSettings(Listing_Extended lister, Settings settings)
        {
            lister.LabelColored("Settings", TitleLabelColor);
            // memory options
            {
                bool prevColMemUs = settings.collectMemAlloc;
                lister.CheckboxLabeled("Collect memory allocations", ref settings.collectMemAlloc);
                if (prevColMemUs != settings.collectMemAlloc)
                {
                    if (!settings.collectMemAlloc) settings.sortByMemAlloc = false;
                    PatchHandler.Reset();
                }
                if (settings.collectMemAlloc)
                {
                    lister.CheckboxLabeled("Sort by memory allocations", ref settings.sortByMemAlloc);
                }
            }
            // perfomance mode
            lister.CheckboxLabeled("Perfomance mode", ref settings.perfomanceMode);
            {
                if (settings.perfomanceMode)
                {
                    Rect rect = lister.GetRect(Text.LineHeight),
                        timeRect = rect,
                        ticksRect = rect;

                    timeRect.width = rect.width / 2 + 35;
                    Widgets.TextFieldNumericLabeled(timeRect, "clean AvgTime < ", ref settings.ruleTiming, ref settings.ruleTimingBuf);

                    ticksRect.x = timeRect.xMax;
                    ticksRect.width -= timeRect.width;
                    Widgets.TextFieldNumericLabeled(ticksRect, "and Ticks > ", ref settings.ruleTicks, ref settings.ruleTicksBuf);
                }
            }
            if (lister.ButtonText($"Stop profiling", Text.LineHeight))
            {
                Profiler.Logger.LogOperation("ResetProfiling", Patcher.UnpatchAll);
            }
        }

        private void DrawOther(Listing_Extended lister, Settings settings)
        {
            lister.LabelColored("Results", TitleLabelColor);
            
            Rect buttonRect = lister.GetRect(Text.LineHeight);
            buttonRect.width /= 3;
            if (Widgets.ButtonText(buttonRect, $"Dump to SLK"))
            {
                List<StopwatchRecord> result = PatchHandler.GetProfileRecordsSorted();
                FS.WriteAllText($"Profiler_{DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss")}.slk", result.DumpToSlk());
            }

            buttonRect.x = buttonRect.xMax;
            if (Widgets.ButtonText(buttonRect, $"Dump to CSV"))
            {
                List<StopwatchRecord> result = PatchHandler.GetProfileRecordsSorted();
                FS.WriteAllText($"Profiler_{DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss")}.csv", result.DumpToCsv());
            }
            buttonRect.x = buttonRect.xMax;
            if (Widgets.ButtonText(buttonRect, $"Reset"))
            {
                PatchHandler.Reset();
            }

            lister.LabelColored("Other", TitleLabelColor);
		    if (lister.ButtonText($"Dump all harmony patches", Text.LineHeight))
		    {
                FS.WriteAllText("HarmonyPatches.txt", HarmonyMain.AllHarmonyPatchesDump());
                FS.WriteAllText("HarmonyPatches-Conflicts.txt", HarmonyMain.CanConflictHarmonyPatchesDump());
		    }

            lister.CheckboxLabeled("CRASH DEBUG", ref settings.debug);
            lister.Gap(20f);
        }

        private void DrawProfilerTop15(Listing_Extended lister, Settings settings)
        {
            lister.LabelColored($"Top 15 (Triggered:{PatchHandler.ProfiledRecordsCount()} Methods:{Patcher.PatchedMethodsCount()})", TitleLabelColor);

            int maxRecordCount = 15;
            // update cache every 1 sec and skip update if mouse on buttons
            if (cached == null || !stopUpdate && cacheUpdateTimer.ElapsedMilliseconds > 1000L)
            {
                cached = PatchHandler.GetProfileRecordsSorted()
                    .Where(x => !hided.Any(x.MethodName.Equals))
                    //.OrderByDescending(x => x.TimeSpent)
                    .Take(maxRecordCount)
                    .ToList();
                cacheUpdateTimer = Stopwatch.StartNew();
            }

            // draw cached info
            stopUpdate = false;
            var backFont = Text.Font;
            foreach (var r in cached)
            {
                string tooltip = r.Tooltip;

                Rect buttonRect1 = lister.GetRect(Text.LineHeight),
                    buttonRect2 = buttonRect1;

                buttonRect1.width = 40;
                buttonRect2.width -= 40;
                buttonRect2.x = buttonRect1.xMax;

                if (ButtonText(buttonRect1, "Copy", tooltip + "\nPress for copy this method name", out bool button1IsMouseOver))
                {
                    if (!settings.profileCustom.Contains(r.MethodName))
                    {
                        bool addLine = !settings.profileCustom.IsNullOrEmptyOrEqual(Settings.CustomExampleStr);
                        if (addLine) settings.profileCustom += $"\n{r.MethodName}";
                        else settings.profileCustom = $"{r.MethodName}";
                    }
                }

                if (ButtonText(buttonRect2, r.MethodName, tooltip + "\nPress for hide this line", out bool button2IsMouseOver))
                {
                    hided.Add(r.MethodName);
                    cached = null;
                    break; // and redraw
                }

                if (button1IsMouseOver || button2IsMouseOver)
                {
                    stopUpdate = true;
                }

                lister.Label($"  TimeSpent:{r.TimeSpent}ms AvgTick:{r.AvgTime}ms Ticks:{r.TicksNum}");
            }
            Text.Font = backFont;

            if (hided.Count > 0 && lister.ButtonText($"Reset Hided", Text.LineHeight))
            {
                hided.Clear();
                cached = null;
            }
        }

        private bool ButtonText(Rect rect, string text, string tooltip, out bool isMouseOver)
        {
            isMouseOver = Mouse.IsOver(rect);
            TooltipHandler.TipRegion(rect, tooltip);
            return Widgets.ButtonText(rect, text);
        }

        private bool stopUpdate = false;
        private List<StopwatchRecord> cached;
        private Stopwatch cacheUpdateTimer = Stopwatch.StartNew();
        private static HashSet<string> hided = new HashSet<string>();
    }
}