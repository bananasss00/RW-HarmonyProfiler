﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using Harmony;
using UnityEngine;
using Verse;

// credits: https://github.com/UnlimitedHugs/RimworldHugsLib
namespace HarmonyProfiler.UI
{
    /// <summary>
	/// Adds an entry point to draw and additional debug button on the toolbar.
	/// The infix is necessary to catch the WidgetRow that the stock buttons are drawn to.
	/// </summary>
	[HarmonyPatch(typeof(DebugWindowsOpener))]
	[HarmonyPatch("DrawButtons")]
	internal class DebugWindowsOpener_Patch {
		private static bool patched;

		[HarmonyPrepare]
		public static void Prepare() {
			LongEventHandler.ExecuteWhenFinished(() => {
				if (!patched) Log.Warning("DebugWindowsOpener_Patch could not be applied.");
			});
		}

		[HarmonyTranspiler]
		public static IEnumerable<CodeInstruction> DrawAdditionalButtons(IEnumerable<CodeInstruction> instructions) {
			patched = false;
			var instructionsArr = instructions.ToArray();
			var widgetRowIndex = TryGetLocalIndexOfConstructedObject(instructionsArr, typeof(WidgetRow));
			foreach (var inst in instructionsArr) {
				if (!patched && widgetRowIndex >= 0 && inst.opcode == OpCodes.Bne_Un) {
					yield return new CodeInstruction(OpCodes.Ldloc, widgetRowIndex);
					yield return new CodeInstruction(OpCodes.Call, ((Action<WidgetRow>)HarmonyProfilerController.DrawDebugToolbarButton).Method);
					patched = true;
				}
				yield return inst;
			}
		}

		private static int TryGetLocalIndexOfConstructedObject(IEnumerable<CodeInstruction> instructions, Type constructedType, Type[] constructorParams = null) {
			var constructor = AccessTools.Constructor(constructedType, constructorParams);
			int localIndex = -1;
			if (constructor == null) {
			    Log.Error($"Could not reflect constructor for type {constructedType}: {Environment.StackTrace}");
				return localIndex;
			}
			CodeInstruction prevInstruction = null;
			foreach (var inst in instructions) {
				if (prevInstruction != null && prevInstruction.opcode == OpCodes.Newobj && constructor.Equals(prevInstruction.operand)) {
					if (inst.opcode == OpCodes.Stloc_0) {
						localIndex = 0;
					} else if (inst.opcode == OpCodes.Stloc_1) {
						localIndex = 1;
					} else if (inst.opcode == OpCodes.Stloc_2) {
						localIndex = 2;
					} else if (inst.opcode == OpCodes.Stloc_3) {
						localIndex = 3;
					} else if (inst.opcode == OpCodes.Stloc && inst.operand is int) {
						localIndex = (int)inst.operand;
					}
					if (localIndex >= 0) break;
				}
				prevInstruction = inst;
			}
			if (localIndex < 0) {
			    Log.Error($"Could not determine local index for constructed type {constructedType}: {Environment.StackTrace}");
			}
			return localIndex;
		}
	}

    /// <summary>
    /// Extends the width of the immediate window the dev toolbar buttons are drawn to to accommodate an additional button
    /// </summary>
    [HarmonyPatch(typeof(DebugWindowsOpener))]
    [HarmonyPatch("DevToolStarterOnGUI")]
    internal class DevToolStarterOnGUI_Patch {
        private static bool patched;

        [HarmonyPrepare]
        public static void Prepare() {
            LongEventHandler.ExecuteWhenFinished(() => {
                if (!patched) Log.Error("DevToolStarterOnGUI_Patch could not be applied.");
            });
        }

        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> ExtendButtonsWindow(IEnumerable<CodeInstruction> instructions) {
            patched = false;
            foreach (var inst in instructions) {
                if (!patched && inst.opcode == OpCodes.Ldc_R4 && 28f.Equals(inst.operand)) {
                    // add one to the number of expected buttons
                    yield return new CodeInstruction(OpCodes.Ldc_R4, 1f);
                    yield return new CodeInstruction(OpCodes.Add);
                    patched = true;
                }
                yield return inst;
            }
        }
    }

	[StaticConstructorOnStartup]
	public static class HarmonyProfilerController {
		
		private static Texture2D profilerIconTex;
        
		internal static void Initialize() {
			LongEventHandler.ExecuteWhenFinished(() => profilerIconTex = ContentFinder<Texture2D>.Get("Buttons/ButtonProfiler"));
		}

		internal static void DrawDebugToolbarButton(WidgetRow widgets) {
			if (widgets.ButtonIcon(profilerIconTex, "Open Harmony Profiler")) {
				var stack = Find.WindowStack;
                if (stack.IsOpen<Dialog_HarmonyProfiler>())
                {
                    stack.TryRemove(typeof(Dialog_HarmonyProfiler));
                }
                else
                {
                    stack.Add(new Dialog_HarmonyProfiler());
                }
			}
		}
	}
}