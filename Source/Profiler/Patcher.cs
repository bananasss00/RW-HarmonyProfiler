using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Harmony;
using HarmonyProfiler.Profiler.Core;
using Verse;
using Verse.AI;

namespace HarmonyProfiler.Profiler
{
    public static class Patcher
    {
        // min priority can rip profiler by another prefixes if he return false
        private static readonly HarmonyMethod MethodPrefix =
            new HarmonyMethod(PatchHandler.PatchStartMethod) {prioritiy = int.MaxValue}; 

        private static readonly HarmonyMethod MethodPostfix =
            new HarmonyMethod(PatchHandler.PatchStopMethod) {prioritiy = int.MaxValue};

        // prevent multi patch and unpatching
        private static readonly HashSet<MethodBase> PatchedMethods = new HashSet<MethodBase>();

        #region Main methods
        /// <summary>
        /// Predict game crashes, when try patch method
        /// </summary>
        /// <param name="method"></param>
        /// <returns></returns>
        private static bool TestMethodForPatch(MethodBase method)
        {
            try
            {
                // can cause exception like: Exception System.TypeLoadException: Could not load type 'AlienComp'.
                // when HarmonyLib call GetMethodBody() on that method - ez game crash
                method.MethodHandle.GetFunctionPointer();
                return true;
            }
            catch (Exception ex)
            {
                Logger.Add($"[Exception] {method.GetMethodFullString()} => {ex.Message}");
            }
            return false;
        }

        private static IEnumerable<MethodBase> GetAllMethodsWithOverloads(string typeColonMethodname)
        {
            string[] arr = typeColonMethodname.Split(':');
            if (arr.Length != 2)
                throw new Exception("Not valid typeColonMethodname");

            var type = AccessTools.TypeByName(arr[0]);
            if (type == null)
                yield break;

            var methods = type.GetMethods(AccessTools.all).Where(x => x.Name.Equals(arr[1]));
            foreach (var m in methods)
            {
                yield return m;
            }
        }

        private static bool TryAddProfiler(MethodBase method)
        {
            if (PatchedMethods.Add(method) && method != PatchHandler.PatchStartMethod && method != PatchHandler.PatchStopMethod)
            {
                if (!TestMethodForPatch(method))
                {
                    PatchedMethods.Remove(method);
                    return false; // method cause crash game if we try patch
                }

                try
                {
                    if (Settings.Get().debug)
                    {
                        Log.Error($"[TryAddProfiler] Try patch method => {method.GetMethodFullString()}");
                    }

                    HarmonyMain.Instance.Patch(method, prefix: MethodPrefix, postfix: MethodPostfix);
                    return true;
                }
                catch (Exception e)
                {
                    Log.Error($"[TryAddProfiler] Exception: {e.Message}; method => {method.GetMethodFullString()}");
                }
            }
            
            return false;
        }

        public static void UnpatchAll()
        {
            //Harmony.UnpatchAll(HarmonyBrowserId);
            foreach (var methodBase in PatchedMethods)
            {
                try
                {
                    HarmonyMain.Instance.Unpatch(methodBase, HarmonyPatchType.All, HarmonyMain.Id);
                    Logger.Add($"Unpatched: {methodBase.GetMethodNameString()}");
                }
                catch (Exception e)
                {
                    Logger.Add($"[ERROR][UnpatchAll] Exception: {e.Message}; method => {methodBase.GetMethodFullString()}");
                    Log.Error($"[ERROR][UnpatchAll] Exception: {e.Message}; method => {methodBase.GetMethodFullString()}");
                }
                
            }
            Log.Warning($"Unpatched methods: {PatchedMethods.Count}");
            PatchHandler.Reset();
            PatchHandler.StopCollectData();
            PatchedMethods.Clear();
        }
        #endregion

        #region Custom / Patches profiling
        public static void ProfileMethods(string[] methodNames)
        {
            int patchedCount = 0;
            foreach (var methodName in methodNames)
            {
                //var method = AccessTools.Method(methodName); // TODO: overloaded functions
                foreach (var method in GetAllMethodsWithOverloads(methodName))
                {
                    if (method == null)
                    {
                        Logger.Add($"[ERROR] Can't patch: {methodName}");
                        Log.Error($"[ERROR] Can't patch: {methodName}");
                        continue;
                    }

                    if (TryAddProfiler(method))
                    {
                        Logger.Add($"Patched: {methodName}");
                        patchedCount++;
                    }
                    else
                    {
                        Logger.Add($"Already patched: {methodName}");
                    }
                }
            }
            Log.Warning($"Patched methods: {patchedCount}");
            PatchHandler.StartCollectData();
        }

        public static void ProfileHarmonyPatches(string[] owners, bool skipGenericMethods, bool skipTranspiledMethods)
        {
            int transpilersSkip = 0;
            int patchedCount = 0;
            var patchedMethodsByOwners = HarmonyMain.GetPatchedMethods(owners, skipGenericMethods);
            foreach (var patchInfo in patchedMethodsByOwners)
            {
                bool isTranspiler = patchInfo.patchType == PatchType.Transpiler;
                string patchOwner = patchInfo.harmonyPatch.owner;
                var patchMethod = isTranspiler ? patchInfo.originalMethod : patchInfo.harmonyPatch.patch;

                if (skipTranspiledMethods && isTranspiler)
                {
                    Logger.Add($"Skip transpiled: [{patchOwner}] {patchMethod.GetMethodFullString()}");
                    transpilersSkip++;
                    continue;
                }

                if (TryAddProfiler(patchMethod))
                {
                    Logger.Add($"Patch: [{patchOwner}] {patchMethod.GetMethodFullString()}");
                    patchedCount++;
                }
                else
                    Logger.Add($"Already patched: [{patchOwner}] {patchMethod.GetMethodFullString()}");
            }
            PatchHandler.StartCollectData();
            Log.Warning($"Patched methods: {patchedCount}");
            if (transpilersSkip > 0)
            {
                Log.Warning($"Skip transpiled methods: {transpilersSkip}");
            }
        }
        #endregion
        
        #region Mod defs / HugsLib tick
        private static IEnumerable<string> GetHugsLibTicks()
        {
            Type hugsModBase = AccessTools.TypeByName("HugsLib.ModBase");
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                string n = asm.FullName;
                if (n.Split(new []{','})[0].Length == 36)
                    continue;
                if (n.StartsWith("Mono.") || n.StartsWith("Anonymously") || n.StartsWith("System")
                    || n.StartsWith("NVorbis") || n.StartsWith("NAudio") || n.StartsWith("UnityEngine")
                    || n.StartsWith("mscorlib")
                )
                    continue;

                foreach (var type in asm.GetTypes())
                {
                    if (type.IsEnum || type.IsGenericType || type == hugsModBase || !hugsModBase.IsAssignableFrom(type))
                        continue;

                    MethodInfo tickMethod = type.GetMethod("Tick");
                    if (tickMethod != null && tickMethod.DeclaringType == type)
                    {
                        yield return $"{type.FullName}:Tick";
                    }
                }
            }
        }

        /// <summary>
        /// Find all methods in @defClass and fill @patches list. Methods inherited from external assembly's not included!
        /// </summary>
        /// <param name="patches">Harmony list typeColonMethodnames</param>
        /// <param name="defClass">Target class for find methods</param>
        /// <param name="allowCore">Allow assembly: Assembly-CSharp.dll</param>
        /// <param name="skipInherited">If true then include only Declared in @defClass methods</param>
        public static void AddDefMethodsAdvanced(this HashSet<string> patches, Type defClass, bool allowCore = false, bool skipInherited = false)
        {
            if (allowCore && defClass != null || (!defClass?.AssemblyQualifiedName?.Contains("Assembly-CSharp") ?? false))
            {
                var modAssembly = defClass.Assembly;
                var defClassMethods = defClass.GetMethods(AccessTools.all);
                foreach (var methodInfo in defClassMethods)
                {
                    var declaringType = methodInfo.DeclaringType;
                    if (declaringType == null)
                        continue;
                    if (methodInfo.IsGenericMethod || methodInfo.IsAbstract)
                        continue;
                    if (!declaringType.Assembly.Equals(modAssembly)) // !declaringType.AssemblyQualifiedName.Contains("Assembly-CSharp")
                        continue;

                    if (!skipInherited || methodInfo.DeclaringType == defClass) 
                    {
                        patches.Add($"{declaringType.FullName}:{methodInfo.Name}");
                    }
                    //else
                    //{
                    //    Log.Warning($"Skip: [{defClass.FullName}] {declaringType.FullName}:{methodInfo.Name}");
                    //}
                }
            }
        }

        private static IEnumerable<Type> GetWorkerClasses(Def d, List<string> workerFields, List<string> workerGetters)
        {
            foreach (var classField in workerFields)
            {
                var field = d.GetType().GetField(classField);
                if (field == null)
                    continue;

                if (!(field.GetValue(d) is Type classWorker))
                    continue;

                yield return classWorker;
            }

            foreach (var classGetter in workerGetters)
            {
                var method = d.GetType().GetMethod(classGetter);
                if (method == null)
                    continue;

                var classWorker = method.Invoke(d, null);
                if (classWorker == null)
                    continue;

                yield return classWorker.GetType();
            }
        }

        public static void ProfileMods(List<string> modNames)
        {
            var profilerCfg = Settings.Get().cfgDef;

            HashSet<string> patches = new HashSet<string>();

            // hugs tick
            var hugsTicks = GetHugsLibTicks();
            if (hugsTicks != null)
            {
                patches.UnionWith(hugsTicks);
            }

            
            var modsDir = LoadedModManager.RunningModsListForReading.Select(x => new {modContentPack = x, dirName = Path.GetFileName(x.RootDir)}).ToList();
            foreach (var modName in modNames)
            {
                var mod = modsDir.FirstOrDefault(x => x.dirName.StartsWith(modName))?.modContentPack;
                if (mod == null)
                {
                    Log.Error($"Can't find mod with name: {modName}");
                    continue;
                }

                foreach (var d in mod.AllDefs)
                {
                    // ThinkTree childs
                    if (d is ThinkTreeDef thinkDef)
                    {
                        foreach (var child in thinkDef.thinkRoot.ChildrenRecursive)
                        {
                            patches.AddDefMethodsAdvanced((child as ThinkNode_JobGiver)?.GetType());
                        }
                    }
                    // DesignationCategory childs
                    if (d is DesignationCategoryDef designationCategoryDef)
                    {
                        foreach (var child in designationCategoryDef.specialDesignatorClasses)
                        {
                            patches.AddDefMethodsAdvanced(child);
                        }
                    }
                    // Auto class getter
                    var workers = GetWorkerClasses(d, profilerCfg.workerFields, profilerCfg.workerGetters);
                    foreach (var worker in workers)
                    {
                        patches.AddDefMethodsAdvanced(worker);
                    }
                }
            }
            ProfileMethods(patches.ToArray());
        }
        #endregion
    }
}