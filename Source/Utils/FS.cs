using System.IO;
using RimWorld;
using Verse;

namespace HarmonyProfiler
{
    public class FS
    {
        private static string FolderUnderSaveData(string folderName)
        {
            string text = Path.Combine(GenFilePaths.SaveDataFolderPath, folderName);
            DirectoryInfo directoryInfo = new DirectoryInfo(text);
            if (!directoryInfo.Exists)
            {
                directoryInfo.Create();
            }
            return text;
        }

        public static void WriteAllText(string fileName, string content)
        {
            string path = $"{FolderUnderSaveData("HarmonyProfiler")}\\{fileName}";
            File.WriteAllText(path, content);
            Messages.Message($"Saved to {path}", MessageTypeDefOf.TaskCompletion, false);
        }

        public static void AppendAllText(string fileName, string content)
        {
            string path = $"{FolderUnderSaveData("HarmonyProfiler")}\\{fileName}";
            File.AppendAllText(path, content);
            Messages.Message($"Saved to {path}", MessageTypeDefOf.TaskCompletion, false);
        }
    }
}