using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Verse;

namespace HarmonyProfiler
{
    public static class Utils
    {
        public static string GetParametersString(this MethodBase method) =>
            string.Join(",", method.GetParameters().Select(o => $"{o.ParameterType.Name} {o.Name}").ToArray());

        public static string GetMethodNameString(this MethodBase method) =>
            $"{method.ReflectedType./*Full*/Name}:{method.Name}";

        public static string GetMethodFullString(this MethodBase method) =>
            $"{method.GetMethodNameString()}({method.GetParametersString()})";

        public static bool IsNullOrEmpty(this string s) => String.IsNullOrEmpty(s);

        public static bool IsNullOrEmptyOrEqual(this string s, string equal) => String.IsNullOrEmpty(s) || s.Equals(equal);

        public static IEnumerable<Type> GetClassesFromNamespace(string @namespace)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (var type in asm.GetTypes())
                {
                    if (type.IsClass && !type.IsGenericType && type.Namespace == @namespace)
                    {
                        yield return type;
                    }
                }
            }
        }

        public static string GetAllDefsList(string[] modNames)
        {
            HashSet<string> defs = new HashSet<string>();
            foreach (var m in modNames)
            {
                var mod = LoadedModManager.RunningModsListForReading.FirstOrDefault(x => x.RootDir.Contains(m));
                foreach (var d in mod.AllDefs)
                {
                    defs.Add(d.GetType().FullName);
                }
            }

            StringBuilder sb = new StringBuilder();
            foreach (var def in defs)
            {
                sb.AppendLine(def);
            }

            return sb.ToString();
        }
    }
}