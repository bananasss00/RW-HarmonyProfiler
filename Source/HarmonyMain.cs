using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Text;
using HarmonyLib;

namespace HarmonyProfiler
{
    /// <summary>
    /// index ordered by execution priority
    /// </summary>
    public enum PatchType
    {
        Prefix,
        Transpiler,
        Postfix
    }

    public class PatchInfo
    {
        public MethodBase originalMethod;
        public Patch harmonyPatch;
        public PatchType patchType;
    }

    public class HarmonyMain
    {
        public const string Id = "harmony.pirateby.profiler";
        public static readonly Harmony Instance = new Harmony(Id);

        public static void Initialize()
        {
            Instance.PatchAll(Assembly.GetExecutingAssembly());
        }

        public static HashSet<string> GetAllHarmonyInstances()
        {
            HashSet<string> owners = new HashSet<string>();
            var patched = Harmony.GetAllPatchedMethods();
            foreach (var method in patched)
            {
                var patchInfo = Harmony.GetPatchInfo(method);
                foreach (var owner in patchInfo.Owners)
                {
                    if (!owner.Equals(Id))
                    {
                        owners.Add(owner);
                    }
                }
            }
            return owners;
        }

        /// <summary>
        /// Get collection [method, patches]
        /// </summary>
        /// <param name="owners">filter patches by owners or null for get all patches</param>
        /// <param name="skipGenericMethods">include generic methods</param>
        /// <returns></returns>
        public static Dictionary<MethodBase, Patches> GetPatches(string[] owners, bool skipGenericMethods)
        {
            var patches = Harmony.GetAllPatchedMethods()
                .Where(method => !skipGenericMethods || !method.DeclaringType.IsGenericType)
                .Select(method => new {method, patches = Harmony.GetPatchInfo(method)});

            return owners == null
                ? patches.ToDictionary(x => x.method, y => y.patches)
                : patches.Where(patch => patch.patches.Owners.Any(x => owners.Any(x.Equals)))
                    .ToDictionary(x => x.method, y => y.patches);
        }

        /// <summary>
        /// Get collection [method, patch, patchType]
        /// </summary>
        /// <param name="owners">filter patches by owners or null for get all patches</param>
        /// <param name="skipGenericMethods">include generic methods</param>
        /// <returns></returns>
        public static IEnumerable<PatchInfo> GetPatchedMethods(string[] owners, bool skipGenericMethods)
        {
            var patches = GetPatches(owners, skipGenericMethods);
            foreach (var p in patches)
            {
                foreach (var valuePrefix in p.Value.Prefixes)
                {
                    if (owners?.Any(x => x == valuePrefix.owner) ?? true)
                        yield return new PatchInfo { originalMethod = p.Key, harmonyPatch = valuePrefix, patchType = PatchType.Prefix };
                }
                foreach (var valuePostfix in p.Value.Postfixes)
                {
                    if (owners?.Any(x => x == valuePostfix.owner) ?? true)
                        yield return new PatchInfo { originalMethod = p.Key, harmonyPatch = valuePostfix, patchType = PatchType.Postfix };
                }
                foreach (var valueTranspiler in p.Value.Transpilers)
                {
                    if (owners?.Any(x => x == valueTranspiler.owner) ?? true)
                        yield return new PatchInfo { originalMethod = p.Key, harmonyPatch = valueTranspiler, patchType = PatchType.Transpiler };
                }
            }
        }

        public static string AllHarmonyPatchesDump()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("===[Harmony Patches]===");

            void dumpPatchesInfo(string type, ReadOnlyCollection<Patch> patches)
            {
                // Sort patches by harmony execute priority
                var patchesSorted = patches.ToList().OrderByDescending(x => x.priority).ThenBy(x => x.index);

                foreach (var p in patchesSorted)
                {
                    var m = p.PatchMethod;
                    sb.AppendLine($" {type}:{m.ReturnType.Name} {m.GetMethodFullString()} [mod:{p.owner}, prior:{p.priority}, idx:{p.index}]");
                    foreach (var b in p.before)
                        sb.AppendLine($"  before:{b}");
                    foreach (var a in p.after)
                        sb.AppendLine($"  after:{a}");
                }
                //if (patches.Count > 0)
                //    sb.AppendLine();
            }

            var patchesDic = HarmonyMain.GetPatches(null, false);
            foreach (var kv in patchesDic)
            {
                var patch = kv.Value;
                if (patch.Owners.Count >= 1)
                {
                    sb.AppendLine($"{kv.Key.GetMethodFullString()}:(Owners:{patch.Owners.Count}, Prefixes:{patch.Prefixes.Count}, Postfixes:{patch.Postfixes.Count}, Transpilers:{patch.Transpilers.Count})");
                    dumpPatchesInfo("prefix", patch.Prefixes);
                    dumpPatchesInfo("transpiler", patch.Transpilers);
                    dumpPatchesInfo("postfix", patch.Postfixes);
                    sb.AppendLine();
                }
            }

            return sb.ToString();
        }

        public static string CanConflictHarmonyPatchesDump()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("===[Harmony Patches Can Conflict]===");

            var allPatches = HarmonyMain.GetPatchedMethods(null, false)
                .GroupBy(x => x.originalMethod)
                .Select(x => new
                {
                    method = x.Key,
                    patches = x // sort patches in execution priority. prefixes->transpilers->postfixes
                        .Where(y => y.patchType != PatchType.Postfix) // can be blocked only transpilers or low priority prefixes
                        .OrderBy(y => y.patchType)
                        .ThenByDescending(y => y.harmonyPatch.priority)
                        .ThenBy(y => y.harmonyPatch.index)
                        .ToList()
                });

            bool HasConflictPatches(List<PatchInfo> sortedByExecuteOrder)
            {
                for (int i = 0; i < sortedByExecuteOrder.Count; i++)
                {
                    var p = sortedByExecuteOrder[i];
                    if (p.patchType == PatchType.Prefix
                        && p.harmonyPatch.PatchMethod.ReturnType == typeof(System.Boolean)
                        && i != sortedByExecuteOrder.Count - 1)
                    {
                        // can block another prefixes, transpilers or postfixes
                        return true;
                    }

                    if (p.patchType > PatchType.Prefix)
                    {
                        // transpilers and postfixes can't be bool patch
                        return false;
                    }
                }

                return false;
            }

            foreach (var p in allPatches)
            {
                if (!HasConflictPatches(p.patches))
                    continue;

                int owners = p.patches.Select(x => x.harmonyPatch.owner).Distinct().Count();
                if (owners <= 1)
                    continue;

                int prefixes = p.patches.Count(x => x.patchType == PatchType.Prefix);
                int transpilers = p.patches.Count(x => x.patchType == PatchType.Transpiler);
                int postfixes = p.patches.Count(x => x.patchType == PatchType.Postfix);
                sb.AppendLine($"{p.method.GetMethodFullString()}:(Owners: {owners} Prefixes:{prefixes}, Postfixes:{postfixes}, Transpilers:{transpilers})");
                foreach (var patchInfo in p.patches)
                {
                    var harmonyPatch = patchInfo.harmonyPatch;
                    var patchMethod = harmonyPatch.PatchMethod;
                    sb.AppendLine($" {patchInfo.patchType} => {patchMethod.ReturnType.Name} {patchMethod.GetMethodFullString()} [mod:{harmonyPatch.owner}, prior:{harmonyPatch.priority}, idx:{harmonyPatch.index}]");
                    foreach (var b in harmonyPatch.before)
                        sb.AppendLine($"  before:{b}");
                    foreach (var a in harmonyPatch.after)
                        sb.AppendLine($"  after:{a}");
                }
                sb.AppendLine();
            }
            
            return sb.ToString();
        }
    }
}