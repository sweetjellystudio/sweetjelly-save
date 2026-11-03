using System.IO;
using UnityEditor;
using UnityEngine;
using SWeetJelly.Save.Framework.Bootstrapper;

namespace SWeetJelly.Save.Framework.Editor
{
    public static class SaveActionsMenu
    {
        [MenuItem("SWeetJelly/Actions/Remove Save", priority = 1)]
        private static void RemoveSave()
        {
            var target = Selection.activeObject as SavePackageBootstrapperSo;
            if (target == null) return;
            var path = SaveEditorUtility.GetSavePath(target);
            if (EditorUtility.DisplayDialog("Remove selected save", "Delete only this file?\n" + path, "Delete", "Cancel"))
                File.Delete(path);
        }
        [MenuItem("SWeetJelly/Actions/Remove Save", true)]
        private static bool RemoveSaveValidation() => !EditorApplication.isPlayingOrWillChangePlaymode
            && Selection.activeObject is SavePackageBootstrapperSo;

        [MenuItem("SWeetJelly/Actions/Reset All PlayerPrefs")]
        private static void ResetPlayerPrefs()
        {
            if (!EditorUtility.DisplayDialog("Reset all PlayerPrefs", "Delete every PlayerPrefs value for this project? Save files are kept.", "Reset all", "Cancel")) return;
            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();
        }
        [MenuItem("SWeetJelly/Actions/Reset All PlayerPrefs", true)]
        private static bool ResetValidation() => !EditorApplication.isPlayingOrWillChangePlaymode;
    }
}
