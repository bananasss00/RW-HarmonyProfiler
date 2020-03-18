using HarmonyProfiler.Profiler;
using HarmonyProfiler.UI;
using Verse;

namespace HarmonyProfiler
{
    internal class Initializer : Mod
    {
        internal static Initializer Instance;

        public Initializer(ModContentPack content) : base(content)
        {
            Instance = this;
            HarmonyMain.Initialize();
            //Settings.Instance = GetSettings<Settings>();
            HarmonyProfilerController.Initialize();
            Log.Message($"HarmonyProfiler :: Initialized");
        }

        internal static readonly int CheckTickDelay = 60 * 15;

        internal void Tick(int currentTick)
        {
            if (currentTick % CheckTickDelay != 0)
                return;

            var settings = Settings.Get();
            if (settings.perfomanceMode)
            {
                Patcher.UnpatchByRule(settings.ruleTiming, settings.ruleTicks);
            }
        }

        //public override void DoSettingsWindowContents(Rect inRect) => Settings.DoSettingsWindowContents(inRect);

        //public override string SettingsCategory() => "HarmonyProfiler";
    }
}
