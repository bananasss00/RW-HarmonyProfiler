using System.Collections.Generic;
using Verse;

namespace HarmonyProfiler
{
    public class CfgDef : Def
    {
        public List<string> workerFields;
        public List<string> workerGetters;
    }

    public class Settings
    {
        private static Settings _instance;

        public static Settings Get() => _instance ?? (_instance = new Settings());

        public Settings()
        {
            cfgDef = DefDatabase<CfgDef>.GetNamed("harmonyProfilerConfig");
        }

        public const string CustomExampleStr = "Namespace.Class1:Method1\nNamespace.Class1\nNamespace\nNamespace.*\ndllName.dll";

        public string profileInstances = "";
        public string profileMods = "";
        public string profileCustom = CustomExampleStr;
        public bool allowTranspiledMethods = false;
        public bool allowCoreAsm = false;
        public bool allowInheritedMethods = true;
        public bool collectMemAlloc = true;
        public bool sortByMemAlloc = false;
        public bool debug = false;
        public CfgDef cfgDef;

        public bool perfomanceMode = false;
        public float ruleTiming = 0.01f;
        public int ruleTicks = 100;
        public string ruleTimingBuf, ruleTicksBuf;
    }
}