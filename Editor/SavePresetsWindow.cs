using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using SWeetJelly.Save.Framework.Bootstrapper;
using SWeetJelly.Save.Framework.Unity;
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

        [SerializeField] private SavePackageBootstrapperSo target;
        [SerializeField] private SaveBehaviour runtimeDriver;
        private bool busy;
        private bool playAfterApply;
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
                    RunOperation(() => UpdatePresetAsync(preset));
            }

            workRect.x -= ActivateButtonWidth + DefaultSpace;
            workRect.width = ActivateButtonWidth;

            if (GUI.Button(workRect, "Apply"))
            {
                RunOperation(() => { ActivatePreset(preset.Name); return Task.CompletedTask; });
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

        private async Task UpdatePresetAsync(SavePreset preset)
        {
            if (preset == null)
                return;

            if (!await SaveEditorUtility.TryForceSaveInPlayModeAsync(target, runtimeDriver))
                return;

            string savePath = SaveEditorUtility.GetSavePath(target);

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

            string scenePath = target.EditorStartScenePath;
            if (playAfterApply)
            {
                if (string.IsNullOrWhiteSpace(scenePath) || !scenePath.EndsWith(".unity") || AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) == null)
                    throw new InvalidOperationException("Select a valid start scene on the bootstrap asset before Apply and Play.");
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            }

            string presetPath = SaveEditorUtility.GetPresetPath(presetName);

            if (!File.Exists(presetPath))
            {
                Debug.LogError($"[Save Presets] Preset file does not exist: {presetPath}");
                return;
            }

            string savePath = SaveEditorUtility.GetSavePath(target);
            Directory.CreateDirectory(Path.GetDirectoryName(savePath));

            File.Copy(presetPath, savePath, true);

            if (playAfterApply)
            {
                EditorSceneManager.OpenScene(scenePath);
                EditorApplication.isPlaying = true;
            }
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

            EditorGUI.BeginDisabledGroup(busy);
            DrawToolbar();
            EditorGUI.BeginDisabledGroup(target == null);

            scrollView = EditorGUILayout.BeginScrollView(scrollView);
            savePresetsList?.DoLayoutList();
            EditorGUILayout.EndScrollView();

            GUILayout.FlexibleSpace();

            DrawAddPresetArea();

            EditorGUI.EndDisabledGroup();
            EditorGUI.EndDisabledGroup();
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

            target = (SavePackageBootstrapperSo)EditorGUILayout.ObjectField("Save target", target, typeof(SavePackageBootstrapperSo), false);
            if (EditorApplication.isPlaying)
                runtimeDriver = (SaveBehaviour)EditorGUILayout.ObjectField("Running driver", runtimeDriver, typeof(SaveBehaviour), true);
            playAfterApply = EditorGUILayout.Toggle("Apply and Play", playAfterApply);
            EditorGUILayout.HelpBox(target == null ? "Select a Save bootstrap asset. Opening this window does not migrate files."
                : "Save file: " + target.SaveFileName, MessageType.Info);
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            {
                if (GUILayout.Button("Migrate legacy presets"))
                    RunOperation(() => { SaveEditorUtility.MigrateLegacyPresets(); ReloadPresets(); return Task.CompletedTask; });
                if (target != null && GUILayout.Button("Migrate legacy save to selected target"))
                    RunOperation(() =>
                    {
                        string source = Path.Combine(SaveEditorUtility.PersistentDataPath, SaveEditorUtility.LegacySaveFileName);
                        string destination = SaveEditorUtility.GetSavePath(target);
                        if (File.Exists(destination)) throw new IOException("Destination already exists; no files changed.");
                        if (EditorUtility.DisplayDialog("Migrate legacy save", source + "\nCopy to: " + destination + "? Source is kept.", "Copy", "Cancel"))
                            File.Copy(source, destination, false);
                        return Task.CompletedTask;
                    });
            }
        }

        private void DrawAddPresetArea()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

            tempPresetName = EditorGUILayout.TextField(tempPresetName);

            if (GUILayout.Button("Add", GUILayout.Width(80f)))
            {
                string name = tempPresetName;
                RunOperation(() => AddPresetAsync(name));
                tempPresetName = string.Empty;
                GUI.FocusControl(null);
            }

            EditorGUILayout.EndHorizontal();
        }

        private async Task AddPresetAsync(string presetName)
        {
            if (string.IsNullOrWhiteSpace(presetName))
            {
                Debug.LogError("[Save Presets] Preset name cannot be empty.");
                return;
            }

            SaveEditorUtility.ValidateFileName(presetName);
            if (savePresets.Exists(preset => preset.Name == presetName))
            {
                Debug.LogError($"[Save Presets] Preset already exists: {presetName}");
                return;
            }

            if (!await SaveEditorUtility.TryForceSaveInPlayModeAsync(target, runtimeDriver))
                return;

            string savePath = SaveEditorUtility.GetSavePath(target);

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

        private async void RunOperation(Func<Task> action)
        {
            if (busy) return;
            busy = true;
            try { await action(); }
            catch (Exception e) { Debug.LogException(e); }
            finally { busy = false; if (this != null) Repaint(); }
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
                Name = System.IO.Path.GetFileName(path).Substring(SaveEditorUtility.PresetPrefix.Length);
            }
        }
    }
}