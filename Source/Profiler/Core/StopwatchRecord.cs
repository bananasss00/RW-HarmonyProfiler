using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using Harmony;
using HarmonyProfiler.Profiler.Extensions;

namespace HarmonyProfiler.Profiler.Core
{
    public class StopwatchRecord
    {
        public StopwatchRecord(MethodBase method, bool collectMemoryUsage)
        {
            this.method = method;
            this.collectMemoryUsage = collectMemoryUsage;
        }

        public StopwatchRecord(StopwatchRecord record)
        {
            this.method = record.method;
            this.avg = record.avg;
            this.num = record.num;
            this.min = record.min;
            this.max = record.max;
            this.allocBytes = record.allocBytes;
            this.allocAvgBytes = record.allocAvgBytes;
            this.patchesCached = record.patchesCached;
            this.patchOwnersCached = record.patchOwnersCached;
        }

        public StopwatchRecord Clone() => new StopwatchRecord(this);

        private bool collectMemoryUsage = false;

        /// <summary>
        /// method can be null, if not have patchinfo
        /// </summary>
        private MethodBase method;
        private Stopwatch watch = new Stopwatch();
        private long bytesStart = 0;

        private Patches patchesCached;
        private string patchOwnersCached;

        private long avg = 0;
        private long num = 0;
        private long min = long.MaxValue;
        private long max = 0;
        private long allocBytes = 0;
        private long allocAvgBytes = 0;

        public void Start()
        {
            if (collectMemoryUsage)
            {
                bytesStart = GC.GetTotalMemory(false);
            }

            watch.Reset();
            watch.Start();
        }

        public void Stop()
        {
            if (watch.IsRunning)
            {
                watch.Stop();

                // calc timings
                long ticks = watch.ElapsedTicks;
                avg = (avg * num + ticks) / (num + 1);
                
                if (ticks < min) min = ticks;
                if (ticks > max) max = ticks;

                if (collectMemoryUsage)
                {
                    // calc mem alloc
                    long bytesStop = GC.GetTotalMemory(false);
                    long bytesDelta = bytesStop - this.bytesStart;
                    if (bytesDelta > 0)
                    {
                        allocBytes += bytesDelta;
                        allocAvgBytes = (allocAvgBytes * num + bytesDelta) / (num + 1);
                    }
                }

                num++;
            }
        }

        public string MethodName => $"{method?.ReflectedType?.FullName}:{method?.Name}";

        public string AssemblyName => $"{method?.ReflectedType?.Assembly.GetName().Name}";

        public double AvgTime => avg.TicksToMs(5);

        public double MinTime => min.TicksToMs(5);

        public double MaxTime => max.TicksToMs(5);

        public long TicksNum => num;

        public long TimeSpent => (avg * num).TicksToMs();

        public long AllocKB => (long)Math.Round((double)allocBytes / 1024L);

        public long AvgKB => (long)Math.Round((double)allocAvgBytes / 1024L);

        // vvv [Problem __originalMethod] - not patched methods can be pushed in to dictionary
        public bool IsValid
        {
            get
            {
                if (method == null) return false;

                if (patchesCached == null)
                {
                    patchesCached = HarmonyMain.Instance.GetPatchInfo(method);
                    if (patchesCached == null) method = null; // broke this method
                }

                return patchesCached != null;
            }
        }

        public string PatchOwners
        {
            get
            {
                if (patchOwnersCached != null)
                {
                    return patchOwnersCached;
                }

                if (!IsValid)
                {
                    patchOwnersCached = String.Empty;
                    return String.Empty;
                }

                string patchOwners = GetPatchOwners("prefix", patchesCached.Prefixes) + "   ";
                patchOwners += GetPatchOwners("transpiler", patchesCached.Transpilers) + "   ";
                patchOwners += GetPatchOwners("postfix", patchesCached.Postfixes);
                return patchOwnersCached = patchOwners;
            }
        }

        /// <summary>
        /// MethodName
        /// <para />Assembly: ...
        /// <para />Min-MaxTime: ...ms
        /// <para />AvgAlloc: ...kb
        /// <para />TotalAlloc: ...kb
        /// <para />PatchedBy: ...kb
        /// </summary>
        public string Tooltip
        {
            get
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine(MethodName);
                sb.AppendLine($"Assembly: {AssemblyName}");
                sb.AppendLine($"Min-MaxTime: {MinTime}-{MaxTime}ms");
                sb.AppendLine($"AvgAlloc: {AvgKB}kb");
                sb.AppendLine($"TotalAlloc: {AllocKB}kb");
                sb.AppendLine($"PatchedBy:{PatchOwners}");
                return sb.ToString();
            }
        }

        public string ToCsvRow(int digits)
        {
            return $"{AssemblyName};{MethodName};{AvgTime.ToString($"F{digits}", CultureInfo.InvariantCulture).Replace(".", ",")};{TicksNum};{MinTime.ToString($"F{digits}", CultureInfo.InvariantCulture).Replace(".", ",")};{MaxTime.ToString($"F{digits}", CultureInfo.InvariantCulture).Replace(".", ",")};{TimeSpent};{AvgKB};{AllocKB};{PatchOwners}";
        }

        public string[] ToStringArray(int digits)
        {
            return new[]
            {
                AssemblyName,
                MethodName,
                AvgTime.ToString($"F{digits}", CultureInfo.InvariantCulture) /*.Replace(".", ",")*/, //slk . ; in csv , !
                TicksNum.ToString(),
                MinTime.ToString($"F{digits}", CultureInfo.InvariantCulture) /*.Replace(".", ",")*/, //slk . ; in csv , !
                MaxTime.ToString($"F{digits}", CultureInfo.InvariantCulture) /*.Replace(".", ",")*/, //slk . ; in csv , !
                TimeSpent.ToString(),
                AvgKB.ToString(),
                AllocKB.ToString(),
                PatchOwners
            };
        }

        private static string GetPatchOwners(string patchName, ReadOnlyCollection<Patch> patches)
        {
            string owners = string.Empty;
            var sorted = patches.OrderByDescending(x => x.priority).ThenBy(x => x.index).Select(x => x.owner).Where(x => !x.Equals(HarmonyMain.Id));
            if (sorted.Any()) owners += $"{patchName}: {String.Join(",", sorted.ToArray())}";
            return owners;
        }
    }
}

/* [Problem __originalMethod] for overriden methods by new 
public class Base {
    public void Internal() {
        Log.Warning($"called Base::Internal()");
    }
}
public class NewClass : Base {
    public new void Internal() {
        Log.Warning($"called NewClass::Internal()");
    }
}
[HarmonyPatch(typeof(NewClass), "Internal")]
public class PathNewClass
{
    //NewClass newClass = new NewClass();
    //newClass.Internal() => called NewClass::Internal()
    //(newClass as Base).Internal() => called Base::Internal()
    static void Prefix(MethodBase __originalMethod)
    {
        Log.Warning($"Prefix: {__originalMethod.DeclaringType.FullName}:{__originalMethod.Name}");
    }
}
*/