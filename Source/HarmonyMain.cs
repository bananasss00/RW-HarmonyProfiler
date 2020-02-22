using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Text;
using Harmony;

namespace HarmonyProfiler
{
    public enum PatchType
    {
        Prefix,
        Postfix,
        Transpiler
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
        public static readonly HarmonyInstance Instance = HarmonyInstance.Create(Id);

        public static void Initialize()
        {
            Instance.PatchAll(Assembly.GetExecutingAssembly());
        }

        public static HashSet<string> GetAllHarmonyInstances()
        {
            HashSet<string> owners = new HashSet<string>();
            var patched = Instance.GetPatchedMethods();
            foreach (var method in patched)
            {
                var patchInfo = Instance.GetPatchInfo(method);
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

        public static Dictionary<MethodBase, Patches> GetPatches(string[] owners, bool skipGenericMethods)
        {
            var patches = Instance.GetPatchedMethods()
                .Where(method => !skipGenericMethods || !method.ReflectedType.IsGenericType)
                .Select(method => new {method, patches = Instance.GetPatchInfo(method)});

            return owners == null
                ? patches.ToDictionary(x => x.method, y => y.patches)
                : patches.Where(patch => patch.patches.Owners.Any(x => owners.Any(x.Equals)))
                    .ToDictionary(x => x.method, y => y.patches);
        }

        public static IEnumerable<PatchInfo> GetPatchedMethods(string[] owners, bool skipGenericMethods)
        {
            var patches = GetPatches(owners, skipGenericMethods);
            foreach (var p in patches)
            {
                foreach (var valuePrefix in p.Value.Prefixes)
                {
                    if (owners.Any(x => x == valuePrefix.owner))
                        yield return new PatchInfo { originalMethod = p.Key, harmonyPatch = valuePrefix, patchType = PatchType.Prefix };
                }
                foreach (var valuePostfix in p.Value.Postfixes)
                {
                    if (owners.Any(x => x == valuePostfix.owner))
                        yield return new PatchInfo { originalMethod = p.Key, harmonyPatch = valuePostfix, patchType = PatchType.Postfix };
                }
                foreach (var valueTranspiler in p.Value.Transpilers)
                {
                    if (owners.Any(x => x == valueTranspiler.owner))
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
                    var m = p.patch;
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
    }
}