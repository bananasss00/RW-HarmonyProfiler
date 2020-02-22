using HarmonyProfiler.UI;
using Verse;

namespace HarmonyProfiler
{
    internal class Initializer : Mod
    {
        public Initializer(ModContentPack content) : base(content)
        {
            HarmonyMain.Initialize();
            //Settings.Instance = GetSettings<Settings>();
            HarmonyProfilerController.Initialize();
            Log.Message($"HarmonyProfiler :: Initialized");
        }

        //public override void DoSettingsWindowContents(Rect inRect) => Settings.DoSettingsWindowContents(inRect);

        //public override string SettingsCategory() => "HarmonyProfiler";
    }
}
