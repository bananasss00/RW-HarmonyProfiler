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
            $"{method.DeclaringType./*Full*/Name}:{method.Name}";

        public static string GetMethodFullString(this MethodBase method) =>
            $"{method.GetMethodNameString()}({method.GetParametersString()})";

        public static bool IsNullOrEmpty(this string s) => String.IsNullOrEmpty(s);

        public static bool IsNullOrEmptyOrEqual(this string s, string equal) => String.IsNullOrEmpty(s) || s.Equals(equal);

        /// <summary>
        /// Check if class is generic or base classes generic
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public static bool IsGenericBase(this Type type)
        {
            while (type != null && type != typeof(object))
            {
                if (type.IsGenericType)
                    return true;
                type = type.BaseType;
            }

            return false;
        }

        public static bool IsIndexerPropertyMethod(this MethodBase method)
        {
            if (!method.IsSpecialName) return false;
            PropertyInfo prop = method.DeclaringType?.GetProperty(method.Name.Substring(4), BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic);
            if (prop == null) return false;
            return prop.GetIndexParameters().Length > 0;
        }

        public static IEnumerable<Type> GetClassesFromNamespace(string @namespace)
        {
            bool isMask = @namespace.Contains("*");
            if (isMask)
            {
                @namespace = @namespace.Replace("*", "");
            }
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (var type in asm.GetTypes())
                {
                    if (type.IsClass && !type.IsGenericType/* && !type.IsGenericBase()*/ && type.Namespace != null && (isMask ? type.Namespace.StartsWith(@namespace) : type.Namespace.Equals(@namespace)))
                    {
                        yield return type;
                    }
                }
            }
        }

        public static IEnumerable<string> GetAllModsDll()
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies()
                .Where(x => (x.GetName().GetPublicKeyToken() == null || x.GetName().GetPublicKeyToken().Length == 0)
                            && !x.ManifestModule.FullyQualifiedName.Contains("RimWorldWin64_Data\\Managed\\")
                            && !x.GetName().Name.Equals("HarmonyProfiler")
                            && !x.GetName().Name.Equals("0Harmony")
                            && !x.GetName().Name.Equals("HarmonySharedState"))
            )
            {
                
                yield return asm.GetName().Name;
            }
        }

        public static IEnumerable<Type> GetClassesFromDll(string dllName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies().Where(x => x.GetName().Name.Equals(dllName)))
            {
                foreach (var type in asm.GetTypes())
                {
                    if (type.IsClass && !type.IsGenericType/* && !type.IsGenericBase()*/ && type.Namespace != null)
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