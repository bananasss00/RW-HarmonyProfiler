using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using HarmonyLib;
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
            lister.ColumnWidth = rect.width - 420;

            DrawProfilerTop15(lister, settings);

		    lister.End();
        }

        private void DrawInstanceProfiler(Listing_Extended lister, Settings settings)
        {
            lister.LabelColored("Profile Harmony Instances", TitleLabelColor);
            Rect buttonRect1 = lister.GetRect(Text.LineHeight),
                buttonRect2 = buttonRect1,
                buttonRect3 = buttonRect1;

            buttonRect2.width = buttonRect3.width = buttonRect1.width = buttonRect1.width / 3;
            buttonRect2.x = buttonRect1.xMax;
            buttonRect3.x = buttonRect2.xMax;

            if (Widgets.ButtonText(buttonRect1, "Get instances"))
            {
                settings.profileInstances = String.Join("\n", HarmonyMain.GetAllHarmonyInstances().ToArray());
            }
            if (!settings.profileInstances.IsNullOrEmpty())
            {
                lister.CheckboxLabeled("Allow transpiled methods(slow patching)", ref settings.allowTranspiledMethods);
                if (Widgets.ButtonText(buttonRect2, $"Profile instances"))
                {
                    PatchHandler.Initialize();
                    var instances = settings.profileInstances.Split(new[] {"\r\n", "\n"}, StringSplitOptions.RemoveEmptyEntries);
                    Profiler.Logger.LogOperation("PatchesProfiling", () => Patcher.ProfileHarmonyPatches(instances, true, !settings.allowTranspiledMethods));
                }
                if (Widgets.ButtonText(buttonRect3, "Unpatch instances"))
                {
                    var instances = settings.profileInstances.Split(new[] {"\r\n", "\n"}, StringSplitOptions.RemoveEmptyEntries);
                    Profiler.Logger.LogOperation("UnpatchInstances", () => Patcher.UnpatchInstances(instances));
                }
            }
            settings.profileInstances = lister.TextAreaFocusControl("InstancesProfilerField", settings.profileInstances, 5, ref instancesScrollPos);
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
                    PatchHandler.Initialize();
                    var mods = settings.profileMods.Split(new[] {"\r\n", "\n"}, StringSplitOptions.RemoveEmptyEntries);
                    Profiler.Logger.LogOperation("ModsProfiling", () => Patcher.ProfileMods(mods.ToList()));
                }
            }
            settings.profileMods = lister.TextAreaFocusControl("ModsProfilerField", settings.profileMods, 5, ref modsScrollPos);
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
                PatchHandler.Initialize();
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

            //if (!settings.profileCustom.IsNullOrEmptyOrEqual(Settings.CustomExampleStr))
            {
                lister.CheckboxLabeled("Allow Core assembly", ref settings.allowCoreAsm);
                lister.CheckboxLabeled("Allow class inherited methods", ref settings.allowInheritedMethods);
                if (Widgets.ButtonText(buttonRect2, $"Profile custom methods"))
                {
                    PatchHandler.Initialize();
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
                                foreach (var @class in classes)
                                {
                                    methods.AddDefMethodsAdvanced(@class, settings.allowCoreAsm, !settings.allowInheritedMethods);
                                }
                            }
                        }
                    }

                    Profiler.Logger.LogOperation("CustomProfiling", () => Patcher.ProfileMethods(methods.ToArray()));
                }
            }

            settings.profileCustom = lister.TextAreaFocusControl("CustomProfilerField", settings.profileCustom, 5, ref customScrollPos);
            lister.Gap(20f);
        }

        private void DrawSettings(Listing_Extended lister, Settings settings)
        {
            lister.LabelColored("Settings", TitleLabelColor);
            lister.CheckboxLabeled("Check main thread", ref settings.checkMainThread);
            lister.CheckboxLabeled("Transpiler Mode(SLOW PATCHING/BETTER TPS)", ref settings.profilerTranspileMode);
            if (settings.profilerTranspileMode)
                lister.CheckboxLabeled("  Get original from dictionary", ref settings.getOriginalFromDict);
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
                    lister.CheckboxLabeled("  Sort by memory allocations", ref settings.sortByMemAlloc);
            }
            // perfomance mode
            Rect checkboxRect = lister.GetRect(Text.LineHeight),
                buttonRect = checkboxRect;

            if (settings.perfomanceMode)
            {
                buttonRect.width = checkboxRect.width /= 2;
                buttonRect.x = checkboxRect.xMax;
                if (Widgets.ButtonText(buttonRect, "Force optimize"))
                {
                    Patcher.UnpatchByRule(settings.ruleTiming, settings.ruleTicks);
                }
            }

            Widgets.CheckboxLabeled(checkboxRect, "Perfomance mode", ref settings.perfomanceMode);
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
                PatchHandler.ClearGetMethodByKey();
            }
        }

        private void DrawOther(Listing_Extended lister, Settings settings)
        {
            if (lister.ButtonText($"Results", Text.LineHeight))
            {
                Find.WindowStack.Add(new FloatMenu(new List<FloatMenuOption>
                {
                    new FloatMenuOption("Dump to SLK", () => {
                        List<StopwatchRecord> result = PatchHandler.GetProfileRecordsSorted();
                        FS.WriteAllText($"Profiler_{DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss")}.slk", result.DumpToSlk());
                    }),
                    new FloatMenuOption("Dump to CSV", () => {
                        List<StopwatchRecord> result = PatchHandler.GetProfileRecordsSorted();
                        FS.WriteAllText($"Profiler_{DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss")}.csv", result.DumpToCsv());
                    }),
                    new FloatMenuOption("Reset results", () => {
                        PatchHandler.Reset();
                    }),
                }));
            }
            if (lister.ButtonText($"Tools / Other", Text.LineHeight))
            {
                IEnumerable<FloatMenuOption> getOptions()
                {
                    yield return new FloatMenuOption($"Enable disabled methods: {PatchDisabler.DisabledCount}", () =>
                    {
                        PatchDisabler.EnableAllDisabled();
                    });
                    yield return new FloatMenuOption("Dump all harmony patches", () =>
                    {
                        FS.WriteAllText("HarmonyPatches.txt", HarmonyMain.AllHarmonyPatchesDump());
                        FS.WriteAllText("HarmonyPatches-Conflicts.txt", HarmonyMain.CanConflictHarmonyPatchesDump());
                    });
                    // if (DubsProfilerReset.CanUnpatch())
                    // {
                    //     yield return new FloatMenuOption("Reset DubsPerfomanceAnalyzer patches", DubsProfilerReset.ResetProfiler);
                    // }
                }
                Find.WindowStack.Add(new FloatMenu(new List<FloatMenuOption>(getOptions())));
            }

            // Method resolver. Handle string >= 3 chars. Results show after 2 sec. when str not changed. Has filter AND, example: 'pawn tick' => Verse.Pawn:Tick
            {
                var prevStr = _methodResolver;
                Rect labelRect = lister.GetRect(Text.LineHeight),
                    textEntyRect = labelRect;

                float width = labelRect.width;
                labelRect.width = width / 3;
                textEntyRect.width = 2 * width / 3;
                textEntyRect.x = labelRect.xMax;
                Widgets.Label(labelRect, $"Method resolver");
                _methodResolver = Widgets.TextArea(textEntyRect, _methodResolver);
                if (!String.IsNullOrWhiteSpace(_methodResolver) && _methodResolver.Length >= 4)
                {
                    if (!prevStr.Equals(_methodResolver))
                        _methodResolverInputTimer = Stopwatch.StartNew();
                }
                else _methodResolverInputTimer = null;

                if (_methodResolverInputTimer != null &&
                    _methodResolverInputTimer.ElapsedMilliseconds > 1500)
                {
                    var strLower = _methodResolver.ToLower();
                    var arrFilters = strLower.Split(new[] {" "}, StringSplitOptions.RemoveEmptyEntries);
                    var allMethods = Utils.GetAllMethods();

                    IEnumerable<string> filterMethods()
                    {
                        if (_methodResolverCache == null)
                        {
                            _methodResolverCache = allMethods
                                .OrderBy(x => x)
                                .Select(x => (x, x.ToLower()))
                                .ToList();
                        }
                        foreach (var m in (from tuple in _methodResolverCache
                            where arrFilters.All(x => tuple.nameLower.Contains(x))
                            select tuple))
                        {
                            yield return m.name;
                        }
                    }
                    IEnumerable<FloatMenuOption> makeVariants(int maxResult)
                    {
                        foreach (var m in filterMethods().OrderBy(x => x))
                        {
                            if (maxResult-- < 0) break;
                            yield return new FloatMenuOption(m, () => _methodResolver = m);
                        }
                    }

                    Find.WindowStack.Add(new FloatMenu(new List<FloatMenuOption>(makeVariants(20))));
                    _methodResolverInputTimer = null;
                }
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
                    buttonRect2 = buttonRect1,
                    buttonRect3 = buttonRect1,
                    buttonRect4 = buttonRect1,
                    buttonRect5 = buttonRect1;

                buttonRect1.width = buttonRect2.width = buttonRect3.width = buttonRect4.width = 40;
                
                buttonRect2.x = buttonRect1.xMax;
                buttonRect3.x = buttonRect2.xMax;
                buttonRect4.x = buttonRect3.xMax;
                
                buttonRect5.width -= 40 * 4;
                buttonRect5.x = buttonRect4.xMax;

                if (ButtonText(buttonRect1, "Copy", tooltip + "\nPress for copy this method name", out bool button1IsMouseOver))
                {
                    if (!settings.profileCustom.Contains(r.MethodName))
                    {
                        bool addLine = !settings.profileCustom.IsNullOrEmptyOrEqual(Settings.CustomExampleStr);
                        if (addLine) settings.profileCustom += $"\n{r.MethodName}";
                        else settings.profileCustom = $"{r.MethodName}";
                    }
                }

                bool logActive = PatchHandler.logMethod != null && r.Method == PatchHandler.logMethod;
                if (ButtonText(buttonRect2, logActive ? "X" : "Log", tooltip + "\nPress for copy this method name", out bool button2IsMouseOver))
                {
                    // disable log this method
                    if (logActive) PatchHandler.logMethod = null;
                    // enable log this method
                    else PatchHandler.logMethod = r.Method;
                }

                bool isDisabled = PatchDisabler.IsDisabled(r.Method);
                if (ButtonText(buttonRect3, isDisabled ? "On" : "Off", tooltip + (isDisabled ? "\nPress for ENABLE this method" : "\nPress for DISABLE this method"), out bool button3IsMouseOver))
                {
                    if (isDisabled)
                        PatchDisabler.EnableMethod(r.Method);
                    else
                        PatchDisabler.DisableMethod(r.Method);
                }

                if (ButtonText(buttonRect4, "Undo", tooltip + ("\nPress for REMOVE PROFILER for this method"), out bool button4IsMouseOver))
                {
                    Patcher.UnpatchMethod(r.Method);
                    cached = null;
                    break; // and redraw
                }

                if (ButtonText(buttonRect5, r.MethodName, tooltip + "\nPress for hide this line", out bool button5IsMouseOver))
                {
                    hided.Add(r.MethodName);
                    cached = null;
                    break; // and redraw
                }
                
                if (button1IsMouseOver || button2IsMouseOver || button3IsMouseOver || button4IsMouseOver || button5IsMouseOver)
                {
                    stopUpdate = true;
                }

                lister.Label($"  TimeSpent:{r.TimeSpent}ms AvgTick:{r.AvgTime:0.00000}ms Ticks:{r.TicksNum}");
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

        private Stopwatch _methodResolverInputTimer;
        private string _methodResolver;
        private List<(string name, string nameLower)> _methodResolverCache;

        private bool stopUpdate = false;
        private List<StopwatchRecord> cached;
        private Stopwatch cacheUpdateTimer = Stopwatch.StartNew();
        private static HashSet<string> hided = new HashSet<string>();
    }
}