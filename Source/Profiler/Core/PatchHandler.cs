using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Verse;

namespace HarmonyProfiler.Profiler.Core
{
    public static class PatchHandler
    {
        private static Dictionary<int, StopwatchRecord> _profiledMethods = new Dictionary<int, StopwatchRecord>();

        private static Settings _settings;

        public static void Initialize() => _settings = Settings.Get();

        public static bool IsProfilerMethod(this MethodBase method) =>
            method.MethodHandle == PatchStartMethod.MethodHandle ||
            method.MethodHandle == PatchStopMethod.MethodHandle;

        public static bool Active { get; private set; } = false;

        public static void StartCollectData() => Active = true;

        public static void StopCollectData() => Active = false;

        public static int RemoveProfiledRecords(MethodBase method) => _profiledMethods.RemoveAll(x => x.Value.Method == method);
        
        public static int ProfiledRecordsCount() => _profiledMethods.Count;

        public static MethodBase logMethod = null;

        public static void Reset()
        {
            bool oldState = Active;
            Active = false;
            _profiledMethods.Clear();
            Active = oldState;
        }

        public static readonly MethodInfo PatchStartMethod =
            AccessTools.Method(typeof(PatchHandler), nameof(PatchHandler.Patch_Start));

        public static readonly MethodInfo PatchStopMethod =
            AccessTools.Method(typeof(PatchHandler), nameof(PatchHandler.Patch_Stop));

        public static readonly MethodInfo PatchTranspilerMethod =
            AccessTools.Method(typeof(PatchHandler), nameof(PatchHandler.Patch_Transpiler));

        // Start timer
        public static void Patch_Start(MethodBase __originalMethod, ref StopwatchRecord __state)
        {
            if (!Active) return;
            if (_settings.checkMainThread && !UnityData.IsInMainThread || !_settings.checkMainThread && UnityData.IsInMainThread)
            {
                __state = null;
                return;
            }

            int ptr = __originalMethod.MethodHandle.Value.ToInt32(); // faster then gethashcode?
            if (!_profiledMethods.TryGetValue(ptr, out __state))
            {
                __state = new StopwatchRecord(__originalMethod, _settings.collectMemAlloc);
                _profiledMethods.Add(ptr, __state);
            }
            __state.Start();
        }

        // Stop timer and calc avg time
        public static void Patch_Stop(ref StopwatchRecord __state)
        {
            if (!Active || __state == null) return;
            __state.Stop();

            // log stacktrace
            if (logMethod != null && logMethod == __state.Method)
            {
                Log.Warning(__state.MethodName);
            }
        }

        /*   TRANSPILER MODE   */
        public static void Patch_Transpiler_Template(MethodBase __originalMethod)
        {
            StopwatchRecord profiler = null;
            if (Active && (_settings.checkMainThread && UnityData.IsInMainThread || !_settings.checkMainThread && !UnityData.IsInMainThread))
            {
                int ptrKey = 3333;//__originalMethod.MethodHandle.Value.ToInt32();
                if (!_profiledMethods.TryGetValue(ptrKey, out profiler))
                {
                    MethodBase thisMethod = _getMethodByKey[ptrKey];
                    profiler = new StopwatchRecord(thisMethod, _settings.collectMemAlloc);
                    _profiledMethods.Add(ptrKey, profiler);
                }
                // if (logMethod != null && logMethod == __state.Method) // TODO: Release this to ptrKey
                // {
                //     Log.Warning(__state.MethodName);
                // }
                profiler.Start();
            }
            // function random code
            Log.Warning("RANDOM CODE");
            // end profiling
            profiler?.Stop();
        }

        public static void ClearGetMethodByKey() => _getMethodByKey.Clear();
        private static readonly Dictionary<int, MethodBase> _getMethodByKey = new Dictionary<int, MethodBase>();
        private static readonly MethodInfo m_GetMethodFromHandle1 = typeof(MethodBase).GetMethod("GetMethodFromHandle", new[] { typeof(RuntimeMethodHandle) });
        private static readonly MethodInfo m_GetMethodFromHandle2 = typeof(MethodBase).GetMethod("GetMethodFromHandle", new[] { typeof(RuntimeMethodHandle), typeof(RuntimeTypeHandle) });

        private static readonly MethodInfo tActive = AccessTools.PropertyGetter(typeof(PatchHandler), nameof(Active)).GetBaseDefinition();
        private static readonly MethodInfo tStopwatchRecordStart = AccessTools.Method(typeof(StopwatchRecord), nameof(StopwatchRecord.Start));
        private static readonly MethodInfo tStopwatchRecordStop = AccessTools.Method(typeof(StopwatchRecord), nameof(StopwatchRecord.Stop));
        private static readonly FieldInfo tPatchHandler_settings = AccessTools.Field(typeof(PatchHandler), nameof(PatchHandler._settings));
        private static readonly FieldInfo tSettings_checkMainThread = AccessTools.Field(typeof(Settings), nameof(Settings.checkMainThread));
        private static readonly FieldInfo tSettings_collectMemAlloc = AccessTools.Field(typeof(Settings), nameof(Settings.collectMemAlloc));
        private static readonly MethodInfo tUnityData_IsInMainThread = AccessTools.PropertyGetter(typeof(UnityData), nameof(UnityData.IsInMainThread)).GetBaseDefinition();
        private static readonly FieldInfo tPatchHandler_profiledMethods = AccessTools.Field(typeof(PatchHandler), nameof(PatchHandler._profiledMethods));
        private static readonly MethodInfo t_profiledMethods_TryGetValue = AccessTools.Method(typeof(Dictionary<int, StopwatchRecord>), "TryGetValue");
        private static readonly MethodInfo t_profiledMethods_Add = AccessTools.Method(typeof(Dictionary<int, StopwatchRecord>), "Add");
        private static readonly FieldInfo tPatchHandler_getMethodByKey = AccessTools.Field(typeof(PatchHandler), nameof(PatchHandler._getMethodByKey));
        private static readonly MethodInfo t_getMethodByKey_TryGetValue = AccessTools.Method(typeof(Dictionary<int, MethodBase>), "TryGetValue");
        private static readonly MethodInfo t_getMethodByKey_Value = AccessTools.Method(typeof(Dictionary<int, MethodBase>), "get_Item");
        private static readonly ConstructorInfo tStopwatchRecordCtor = AccessTools.Constructor(typeof(StopwatchRecord), new Type[] {typeof(MethodBase), typeof(bool)});

        private static bool TryEmitOriginalBaseMethod(MethodBase original, out List<CodeInstruction> instructions)
        {
            instructions = new List<CodeInstruction>();
            if (original is MethodInfo method)
                instructions.Add(new CodeInstruction(OpCodes.Ldtoken, method));
            else if (original is ConstructorInfo constructor)
                instructions.Add(new CodeInstruction(OpCodes.Ldtoken, constructor));
            else return false;

            var type = original.ReflectedType;
            if (type.IsGenericType) instructions.Add(new CodeInstruction(OpCodes.Ldtoken, type));
            instructions.Add(new CodeInstruction(OpCodes.Call, type.IsGenericType ? m_GetMethodFromHandle2 : m_GetMethodFromHandle1));
            return true;
        }

        public static IEnumerable<CodeInstruction> Patch_Transpiler(MethodBase __originalMethod, IEnumerable<CodeInstruction> instructions, ILGenerator ilGen)
        {
            int METHOD_KEY = __originalMethod.MethodHandle.Value.ToInt32();

            var profiler = ilGen.DeclareLocal(typeof(StopwatchRecord)); // profiler = new StopwatchRecord(__originalMethod, _settings.collectMemAlloc);
            var ptrKey = ilGen.DeclareLocal(typeof(int)); // int ptrKey = __originalMethod.MethodHandle.Value.ToInt32();
            var originalCodeLabel = ilGen.DefineLabel();
            var checkMainThreadFalseLabel = ilGen.DefineLabel();
            var findProfilerLabel = ilGen.DefineLabel();
            var startProfilerLabel = ilGen.DefineLabel();

            // StopwatchRecord profiler = null;
            yield return new CodeInstruction(OpCodes.Ldnull);
            yield return new CodeInstruction(OpCodes.Stloc, profiler);

            // if (Active && (_settings.checkMainThread && UnityData.IsInMainThread || !_settings.checkMainThread && !UnityData.IsInMainThread))
            // Active
                yield return new CodeInstruction(OpCodes.Call, tActive);
                yield return new CodeInstruction(OpCodes.Brfalse_S, originalCodeLabel);
            // _settings.checkMainThread && UnityData.IsInMainThread
                yield return new CodeInstruction(OpCodes.Ldsfld, tPatchHandler_settings);
                yield return new CodeInstruction(OpCodes.Ldfld, tSettings_checkMainThread);
                yield return new CodeInstruction(OpCodes.Brfalse_S, checkMainThreadFalseLabel);
                yield return new CodeInstruction(OpCodes.Call, tUnityData_IsInMainThread);
                yield return new CodeInstruction(OpCodes.Brtrue_S, findProfilerLabel);
            // !_settings.checkMainThread && !UnityData.IsInMainThread
                yield return new CodeInstruction(OpCodes.Ldsfld, tPatchHandler_settings).WithLabels(checkMainThreadFalseLabel);
                yield return new CodeInstruction(OpCodes.Ldfld, tSettings_checkMainThread);
                yield return new CodeInstruction(OpCodes.Brtrue_S, originalCodeLabel);
                yield return new CodeInstruction(OpCodes.Call, tUnityData_IsInMainThread);
                yield return new CodeInstruction(OpCodes.Brtrue_S, originalCodeLabel);
            {
                // int ptrKey = THIS_METHOD_PTR_KEY;
                yield return new CodeInstruction(OpCodes.Ldc_I4, METHOD_KEY).WithLabels(findProfilerLabel);
                yield return new CodeInstruction(OpCodes.Stloc, ptrKey);
                // if (!_profiledMethods.TryGetValue(ptrKey, out profiler))
                yield return new CodeInstruction(OpCodes.Ldsfld, tPatchHandler_profiledMethods);
                yield return new CodeInstruction(OpCodes.Ldloc, ptrKey);
                yield return new CodeInstruction(OpCodes.Ldloca_S, profiler);
                yield return new CodeInstruction(OpCodes.Callvirt, t_profiledMethods_TryGetValue);
                yield return new CodeInstruction(OpCodes.Brtrue_S, startProfilerLabel);
                {
                    // MethodBase thisMethod = _getMethodByKey[ptrKey];
                    if (_settings.getOriginalFromDict) /* GetOriginalFromDictionary? */
                    {
                        if (!_getMethodByKey.ContainsKey(METHOD_KEY))
                            _getMethodByKey.Add(METHOD_KEY, __originalMethod);
                        yield return new CodeInstruction(OpCodes.Ldsfld, tPatchHandler_getMethodByKey);
                        yield return new CodeInstruction(OpCodes.Ldloc, ptrKey);
                        yield return new CodeInstruction(OpCodes.Callvirt, t_getMethodByKey_Value);
                    }
                    else
                    {
                        if (TryEmitOriginalBaseMethod(__originalMethod, out var thisMethodInstructions))
                            foreach (var thisMethodInstruction in thisMethodInstructions)
                                yield return thisMethodInstruction;
                        else
                            yield return new CodeInstruction(OpCodes.Ldnull);
                    }
                    // profiler = new StopwatchRecord(thisMethod, _settings.collectMemAlloc);
                    yield return new CodeInstruction(OpCodes.Ldsfld, tPatchHandler_settings);
                    yield return new CodeInstruction(OpCodes.Ldfld, tSettings_collectMemAlloc);
                    yield return new CodeInstruction(OpCodes.Newobj, tStopwatchRecordCtor);
                    yield return new CodeInstruction(OpCodes.Stloc, profiler);
                    // _profiledMethods.Add(ptrKey, profiler);
                    yield return new CodeInstruction(OpCodes.Ldsfld, tPatchHandler_profiledMethods);
                    yield return new CodeInstruction(OpCodes.Ldloc, ptrKey);
                    yield return new CodeInstruction(OpCodes.Ldloc, profiler);
                    yield return new CodeInstruction(OpCodes.Callvirt, t_profiledMethods_Add);
                }
                // profiler.Start();
                yield return new CodeInstruction(OpCodes.Ldloc, profiler).WithLabels(startProfilerLabel);
                yield return new CodeInstruction(OpCodes.Call, tStopwatchRecordStart);
            }

            // define label on start original code
            instructions.ElementAt(0).WithLabels(originalCodeLabel);

            // For each instruction which exits this function, append our finishing touches (I.e.)
            // if(profiler != null)
            // {
            //      profiler.Stop();
            // }
            // return; // any labels here are moved to the start of the `if`
            foreach (var inst in instructions)
            {
                if (inst.opcode == OpCodes.Ret)
                {
                    Label endLabel = ilGen.DefineLabel();

                    // profiler?.Stop();
                    yield return new CodeInstruction(OpCodes.Ldloc, profiler).MoveLabelsFrom(inst);
                    yield return new CodeInstruction(OpCodes.Brfalse_S, endLabel);

                    yield return new CodeInstruction(OpCodes.Ldloc, profiler);
                    yield return new CodeInstruction(OpCodes.Call, tStopwatchRecordStop);

                    yield return inst.WithLabels(endLabel);
                }
                else
                {
                    yield return inst;
                }
            }
        }

        /*---TRANSPILER MODE---*/

        /// <summary>
        /// Get sorted profile records by TimeSpent
        /// </summary>
        /// <returns></returns>
        public static List<StopwatchRecord> GetProfileRecordsSorted()
        {
            bool bkpState = Active;
            Active = false;

            bool sortByMemAlloc = Settings.Get().sortByMemAlloc;
            List<StopwatchRecord> records = _profiledMethods.Values
                .Where(x => x.IsValid)
                .Select(x => x.Clone())
                .OrderByDescending(x => !sortByMemAlloc ? x.TimeSpent : x.AllocKB)
                .ToList();

            Active = bkpState;
            return records;
        }
    }
}