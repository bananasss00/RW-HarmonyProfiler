using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using HarmonyLib;
using HarmonyProfiler.Profiler.Extensions;
using Verse;

namespace HarmonyProfiler.Profiler
{
    public class TypeByName
    {
        private static readonly Dictionary<string, Type> TypeByString = new Dictionary<string, Type>();

        public static void Initialize()
        {
            var sw = Stopwatch.StartNew();
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies().Where(
                x => (x.GetName().GetPublicKeyToken() == null || x.GetName().GetPublicKeyToken().Length == 0)
                && !x.ManifestModule.FullyQualifiedName.Contains("RimWorldWin64_Data\\Managed\\")
                && !x.GetName().Name.Equals("HarmonyProfiler")
                && !x.GetName().Name.Equals("0Harmony")
                && !x.GetName().Name.Equals("HarmonySharedState")))
            {
                foreach (var type in asm.GetTypes())
                {
                    if (type.IsClass && !type.IsGenericType/* && !type.IsGenericBase()*/ && type.Namespace != null)
                    {
                        string typeName = type.FullName;
                        if (!TypeByString.ContainsKey(typeName))
                            TypeByString.Add(typeName, type);
                    }
                }
            }
            Log.Warning($"[TypeByName:Initialize] Elapsed time: {sw.ElapsedTicks.TicksToMs(4)}ms");
        }

        public static Type Get(string typeName)
        {
            if (!TypeByString.TryGetValue(typeName, out Type type))
            {
                // Inner classes
                type = AccessTools.TypeByName(typeName);
                TypeByString.Add(typeName, type);
                Log.Warning($"[TypeByName:Get] {typeName} not was in Dictionart, try find by AccessTools.TypeByName: {type != null}");
            }
            return type;
        }

        public static void Close()
        {
            TypeByString.Clear();
        }
    }
}