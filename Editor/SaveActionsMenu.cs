using UnityEditor;
using UnityEngine;

namespace SWeetJelly.Save.Framework.Editor
{
    public static class SaveActionsMenu
    {
        [MenuItem("SWeetJelly/Actions/Remove Save", priority = 1)]
        private static void RemoveSave()
        {
            bool confirmed = EditorUtility.DisplayDialog(
                "Remove Save",
                "Delete PlayerPrefs and local save files?",
                "Delete",
                "Cancel");

            if (!confirmed)
                return;

            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();

            bool deleted = SaveEditorUtility.DeleteCurrentSaveFile();

            Debug.Log(
                deleted
                    ? "[Save Editor] Save data removed."
                    : "[Save Editor] PlayerPrefs removed. Save file was not found.");
        }

        [MenuItem("SWeetJelly/Actions/Remove Save", true)]
        private static bool RemoveSaveValidation()
        {
            return !EditorApplication.isPlayingOrWillChangePlaymode;
        }
    }
}