using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
using UnityEngine;

namespace SWeetJelly.Save.Framework.Editor
{
    public sealed class SavePresetsWindow : EditorWindow
    {
        private const int RemoveButtonWidth = 32;
        private const int UpdateButtonWidth = 64;
        private const int ActivateButtonWidth = 64;
        private const int DateLabelWidth = 64;
        private const int DefaultSpace = 8;

        private static readonly Vector2 WindowSize = new Vector2(800, 600);

        private readonly List<SavePreset> savePresets = new();

        private Vector2 scrollView;
        private string tempPresetName = string.Empty;
        private ReorderableList savePresetsList;
        private Rect workRect;

        [MenuItem("SWeetJelly/Editor/Save Presets")]
        private static void ShowWindow()
        {
            SavePresetsWindow window = GetWindow<SavePresetsWindow>();
            window.minSize = WindowSize;
            window.titleContent = new GUIContent("Save Presets");
        }

        private void OnEnable()
        {
            ReloadPresets();

            savePresetsList = new ReorderableList(
                savePresets,
                typeof(SavePreset),
                draggable: true,
                displayHeader: false,
                displayAddButton: false,
                displayRemoveButton: true);

            savePresetsList.elementHeight = 32f;
            savePresetsList.drawElementCallback = DrawElement;
            savePresetsList.onRemoveCallback = RemoveCallback;
            savePresetsList.onChangedCallback = _ => SaveListOrder();

            workRect = new Rect();
        }

        private void ReloadPresets()
        {
            savePresets.Clear();

            SaveEditorUtility.EnsurePresetsDirectory();
            MigrateLegacyPresetFiles();

            List<SavePreset> unsortedPresets = new();

            string[] files = Directory.GetFiles(SaveEditorUtility.PresetsDirectoryPath);
            string[] order = Array.Empty<string>();

            for (int i = 0; i < files.Length; i++)
            {
                string file = files[i];
                string fileName = Path.GetFileName(file);

                if (fileName.StartsWith(SaveEditorUtility.PresetPrefix, StringComparison.Ordinal))
                {
                    unsortedPresets.Add(new SavePreset(
                        File.GetCreationTimeUtc(file),
                        file));
                }
                else if (fileName == SaveEditorUtility.PresetsOrderFile)
                {
                    order = File.ReadAllLines(file);
                }
            }

            ApplySavedOrder(unsortedPresets, order);
        }

        private void MigrateLegacyPresetFiles()
        {
            string[] rootFiles = Directory.GetFiles(SaveEditorUtility.PersistentDataPath);

            for (int i = 0; i < rootFiles.Length; i++)
            {
                string file = rootFiles[i];
                string fileName = Path.GetFileName(file);

                if (fileName.StartsWith(SaveEditorUtility.PresetPrefix, StringComparison.Ordinal))
                {
                    string destination = Path.Combine(
                        SaveEditorUtility.PresetsDirectoryPath,
                        fileName);

                    if (!File.Exists(destination))
                        File.Move(file, destination);
                }

                if (fileName == SaveEditorUtility.PresetsOrderFile)
                {
                    string destination = SaveEditorUtility.GetOrderFilePath();

                    if (!File.Exists(destination))
                        File.Move(file, destination);
                }
            }
        }

        private void ApplySavedOrder(
            List<SavePreset> unsortedPresets,
            string[] order)
        {
            for (int i = 0; i < order.Length; i++)
            {
                string orderedName = order[i];

                int index = unsortedPresets.FindIndex(
                    preset => preset.Name == orderedName);

                if (index < 0)
                    continue;

                savePresets.Add(unsortedPresets[index]);
                unsortedPresets.RemoveAt(index);
            }

            savePresets.AddRange(unsortedPresets);
        }

        private void DrawElement(
            Rect rect,
            int index,
            bool isActive,
            bool isFocused)
        {
            if (index < 0 || index >= savePresets.Count)
                return;

            SavePreset preset = savePresets[index];

            workRect.Set(rect.x + rect.width, rect.y + 4f, 0f, 18f);

            workRect.x -= UpdateButtonWidth + DefaultSpace;
            workRect.width = UpdateButtonWidth;

            if (GUI.Button(workRect, "Update"))
            {
                bool confirmed = EditorUtility.DisplayDialog(
                    "Update Preset",
                    $"Update preset '{preset.Name}' with the current save?",
                    "Update",
                    "Cancel");

                if (confirmed)
                    UpdatePreset(preset);
            }

            workRect.x -= ActivateButtonWidth + DefaultSpace;
            workRect.width = ActivateButtonWidth;

            if (GUI.Button(workRect, "Activate"))
            {
                ActivatePreset(preset.Name);
            }

            workRect.x -= DateLabelWidth + DefaultSpace;
            workRect.width = DateLabelWidth;

            GUI.Label(workRect, preset.CreationDate.ToString("MM.dd"));

            workRect.x -= DefaultSpace;
            workRect.width = workRect.x - rect.x;
            workRect.x = rect.x;

            GUI.Label(workRect, preset.Name);
        }

        private void RemoveCallback(ReorderableList list)
        {
            if (list.index < 0 || list.index >= savePresets.Count)
                return;

            bool confirmed = EditorUtility.DisplayDialog(
                "Remove Preset",
                $"Remove preset '{savePresets[list.index].Name}'?",
                "Remove",
                "Cancel");

            if (!confirmed)
                return;

            RemovePreset(list.index);
            list.ClearSelection();
            SaveListOrder();
        }

        private void RemovePreset(int index)
        {
            if (index < 0 || index >= savePresets.Count)
                return;

            string path = savePresets[index].Path;

            if (File.Exists(path))
                File.Delete(path);

            savePresets.RemoveAt(index);
        }

        private void UpdatePreset(SavePreset preset)
        {
            if (preset == null)
                return;

            if (!SaveEditorUtility.TryForceSaveInPlayMode())
                return;

            string savePath = SaveEditorUtility.SaveFilePath;

            if (!File.Exists(savePath))
            {
                Debug.LogError($"[Save Presets] Save file does not exist: {savePath}");
                return;
            }

            File.Copy(savePath, preset.Path, true);
            File.SetCreationTimeUtc(preset.Path, DateTime.UtcNow);

            preset.CreationDate = DateTime.UtcNow;

            Repaint();
        }

        private void ActivatePreset(string presetName)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[Save Presets] Cannot activate preset while playing.");
                return;
            }

            if (EditorApplication.isCompiling)
            {
                Debug.LogError("[Save Presets] Cannot activate preset while compiling.");
                return;
            }

            string presetPath = SaveEditorUtility.GetPresetPath(presetName);

            if (!File.Exists(presetPath))
            {
                Debug.LogError($"[Save Presets] Preset file does not exist: {presetPath}");
                return;
            }

            string savePath = SaveEditorUtility.SaveFilePath;
            Directory.CreateDirectory(Path.GetDirectoryName(savePath));

            File.Copy(presetPath, savePath, true);

            string scenePath = SaveEditorUtility.EditorStartScenePath;

            if (!string.IsNullOrWhiteSpace(scenePath) && File.Exists(scenePath))
            {
                EditorSceneManager.OpenScene(scenePath);
            }
            else
            {
                Debug.LogWarning($"[Save Presets] Start scene path is invalid: {scenePath}");
            }

            EditorApplication.isPlaying = true;
        }

        private void SaveListOrder()
        {
            SaveEditorUtility.EnsurePresetsDirectory();

            string[] order = new string[savePresets.Count];

            for (int i = 0; i < order.Length; i++)
                order[i] = savePresets[i].Name;

            File.WriteAllLines(SaveEditorUtility.GetOrderFilePath(), order);
        }

        private void OnGUI()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            DrawToolbar();

            scrollView = EditorGUILayout.BeginScrollView(scrollView);
            savePresetsList?.DoLayoutList();
            EditorGUILayout.EndScrollView();

            GUILayout.FlexibleSpace();

            DrawAddPresetArea();

            EditorGUILayout.EndVertical();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            GUILayout.Label("Save Presets", EditorStyles.boldLabel);

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Reload", EditorStyles.toolbarButton, GUILayout.Width(64f)))
            {
                ReloadPresets();
                savePresetsList.list = savePresets;
                Repaint();
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.HelpBox(
                $"Save file: {SaveEditorUtility.SaveFilePath}",
                MessageType.Info);
        }

        private void DrawAddPresetArea()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

            tempPresetName = EditorGUILayout.TextField(tempPresetName);

            if (GUILayout.Button("Add", GUILayout.Width(80f)))
            {
                AddPreset(tempPresetName);
                tempPresetName = string.Empty;
                GUI.FocusControl(null);
            }

            EditorGUILayout.EndHorizontal();
        }

        private void AddPreset(string presetName)
        {
            if (string.IsNullOrWhiteSpace(presetName))
            {
                Debug.LogError("[Save Presets] Preset name cannot be empty.");
                return;
            }

            if (savePresets.Exists(preset => preset.Name == presetName))
            {
                Debug.LogError($"[Save Presets] Preset already exists: {presetName}");
                return;
            }

            if (!SaveEditorUtility.TryForceSaveInPlayMode())
                return;

            string savePath = SaveEditorUtility.SaveFilePath;

            if (!File.Exists(savePath))
            {
                Debug.LogError($"[Save Presets] Save file does not exist: {savePath}");
                return;
            }

            SaveEditorUtility.EnsurePresetsDirectory();

            string presetPath = SaveEditorUtility.GetPresetPath(presetName);
            File.Copy(savePath, presetPath, true);

            var preset = new SavePreset(
                File.GetCreationTimeUtc(presetPath),
                presetPath);

            savePresets.Add(preset);
            SaveListOrder();

            Repaint();
        }

        private void OnDestroy()
        {
            SaveListOrder();
        }

        private sealed class SavePreset
        {
            public string Name { get; }
            public string Path { get; }
            public DateTime CreationDate { get; set; }

            public SavePreset(DateTime creationDate, string path)
            {
                CreationDate = creationDate;
                Path = path;
                Name = System.IO.Path
                    .GetFileName(path)
                    .Replace(SaveEditorUtility.PresetPrefix, string.Empty);
            }
        }
    }
}