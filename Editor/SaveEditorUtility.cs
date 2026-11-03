using System;
using System.IO;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using SWeetJelly.Save.Framework.Bootstrapper;
using SWeetJelly.Save.Framework.Unity;

namespace SWeetJelly.Save.Framework.Editor
{
    internal static class SaveEditorUtility
    {
        public const string LegacySaveFileName = "save";
        public const string PresetsFolderName = "SavePresets";
        public const string PresetPrefix = "savePreset_";
        public const string PresetsOrderFile = "presetsOrderFile";
        public static string PersistentDataPath => Application.persistentDataPath;
        public static string PresetsDirectoryPath => Path.Combine(PersistentDataPath, PresetsFolderName);
        public static string GetSavePath(SavePackageBootstrapperSo target)
        {
            if (target == null) throw new InvalidOperationException("Select a Save bootstrap asset.");
            return Path.Combine(PersistentDataPath, ValidateFileName(target.SaveFileName));
        }
        public static string ValidateFileName(string name)
        {
            if (string.IsNullOrWhiteSpace(name) || name != Path.GetFileName(name)
                || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || name == "." || name == ".."
                || name.EndsWith(".") || name.EndsWith(" ") || name.Contains("/") || name.Contains("\\"))
                throw new ArgumentException("Use a plain file name without directory separators or invalid characters.");
            return name;
        }
        public static void EnsurePresetsDirectory() => Directory.CreateDirectory(PresetsDirectoryPath);
        public static string GetPresetPath(string name) => Path.Combine(PresetsDirectoryPath, PresetPrefix + ValidateFileName(name));
        public static string GetOrderFilePath() => Path.Combine(PresetsDirectoryPath, PresetsOrderFile);

        public static async Task<bool> TryForceSaveInPlayModeAsync(SavePackageBootstrapperSo target, SaveBehaviour driver)
        {
            GetSavePath(target);
            if (!EditorApplication.isPlaying) return true;
            if (driver == null || !driver.IsConstructed || driver.SaveFileName != target.SaveFileName)
                throw new InvalidOperationException("Select the running save driver for the selected save file.");
            var result = await driver.ForceSaveFromDriverAsync();
            if (result.IsFailure) Debug.LogError(result.Error.ToString());
            return result.IsSuccess;
        }

        public static void MigrateLegacyPresets()
        {
            EnsurePresetsDirectory();
            foreach (var source in Directory.GetFiles(PersistentDataPath))
            {
                var name = Path.GetFileName(source);
                if (!name.StartsWith(PresetPrefix, StringComparison.Ordinal) && name != PresetsOrderFile) continue;
                var destination = Path.Combine(PresetsDirectoryPath, name);
                if (!File.Exists(destination)) File.Move(source, destination);
            }
        }
    }
}
