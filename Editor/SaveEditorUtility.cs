using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using SWeetJelly.Save.InitialiserIntegration;
using System.Threading;

namespace SWeetJelly.Save.Framework.Editor
{
    internal static class SaveEditorUtility
    {
        public const string DefaultSaveFileName = "save.json";
        public const string LegacySaveFileName = "save";

        public const string PresetsFolderName = "SavePresets";
        public const string PresetPrefix = "savePreset_";
        public const string PresetsOrderFile = "presetsOrderFile";

        private const string DefaultEditorStartScenePath =
            "Assets/Project/Content/Scenes/Init.unity";

        public static string PersistentDataPath => Application.persistentDataPath;

        public static string PresetsDirectoryPath =>
            Path.Combine(PersistentDataPath, PresetsFolderName);

        public static string SaveFileName
        {
            get
            {
                SaveInitModule module = FindSaveInitModule();

                if (module == null || string.IsNullOrWhiteSpace(module.SaveFileName))
                    return DefaultSaveFileName;

                return module.SaveFileName;
            }
        }

        public static string SaveFilePath =>
            Path.Combine(PersistentDataPath, SaveFileName);

        public static string LegacySaveFilePath =>
            Path.Combine(PersistentDataPath, LegacySaveFileName);

        public static string EditorStartScenePath
        {
            get
            {
                SaveInitModule module = FindSaveInitModule();

                if (module == null || string.IsNullOrWhiteSpace(module.EditorStartScenePath))
                    return DefaultEditorStartScenePath;

                return module.EditorStartScenePath;
            }
        }

        public static SaveInitModule FindSaveInitModule()
        {
            string[] guids = AssetDatabase.FindAssets("t:SaveInitModule");

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                SaveInitModule module = AssetDatabase.LoadAssetAtPath<SaveInitModule>(path);

                if (module != null)
                    return module;
            }

            return null;
        }

        public static void EnsurePresetsDirectory()
        {
            if (!Directory.Exists(PresetsDirectoryPath))
                Directory.CreateDirectory(PresetsDirectoryPath);
        }

        public static string GetPresetPath(string presetName)
        {
            return Path.Combine(
                PresetsDirectoryPath,
                PresetPrefix + presetName);
        }

        public static string GetOrderFilePath()
        {
            return Path.Combine(
                PresetsDirectoryPath,
                PresetsOrderFile);
        }

        public static bool TryForceSaveInPlayMode()
        {
            if (!EditorApplication.isPlaying)
                return true;

            SaveBehaviour saveBehaviour = UnityEngine.Object.FindObjectOfType<SaveBehaviour>();

            if (saveBehaviour == null)
            {
                Debug.LogError("[Save Editor] SaveBehaviour was not found in the current scene.");
                return false;
            }

            try
            {
                saveBehaviour
                    .ForceSaveFromDriverAsync(CancellationToken.None)
                    .GetAwaiter()
                    .GetResult();

                return true;
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                return false;
            }
        }

        public static bool DeleteCurrentSaveFile()
        {
            bool deleted = false;

            if (File.Exists(SaveFilePath))
            {
                File.Delete(SaveFilePath);
                deleted = true;
            }

            // 기존 구조의 save 파일도 같이 정리.
            if (File.Exists(LegacySaveFilePath))
            {
                File.Delete(LegacySaveFilePath);
                deleted = true;
            }

            return deleted;
        }
    }
}