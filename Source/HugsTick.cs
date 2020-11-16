using System;
using HarmonyLib;
using Verse;

// credits: https://github.com/UnlimitedHugs/RimworldHugsLib
namespace HarmonyProfiler
{
    /// <summary>
    /// Adds a hook for the early initialization of a Game.
    /// </summary>
    [HarmonyPatch(typeof(Game))]
    [HarmonyPatch("FillComponents")]
    internal static class Game_FillComponents_Patch {
        [HarmonyPrefix]
        public static void GameInitializationHook(Game __instance) {
            try {
                __instance.tickManager.RegisterAllTickabilityFor(new HugsTickProxy {CreatedByController = true});
            } catch  {}
        }
    }

    /// <summary>
    /// Forwards ticks to the controller. Will not be saved and is never spawned.
    /// </summary>
    public class HugsTickProxy : Thing {
        // a precaution against ending up in a save. Shouldn't happen, as it is never spawned.
        public bool CreatedByController { get; internal set; }

        public HugsTickProxy() {
            def = new ThingDef{ tickerType = TickerType.Normal, isSaveable = false };
        }

        public override void Tick() {
            if (CreatedByController) Initializer.Instance?.Tick(GenTicks.TicksGame);
        }
    }
}