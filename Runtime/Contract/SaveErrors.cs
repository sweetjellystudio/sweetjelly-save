using SWeetJelly.Shared.Result;

namespace SWeetJelly.Save.Contract
{
    public static class SaveErrors
    {
        public static readonly Error NotLoaded =
            new Error("Save.NotLoaded", "Save service has not been loaded.");

        public static readonly Error InvalidSaveObject =
            new Error("Save.InvalidSaveObject", "Save object cannot be null.");

        public static readonly Error InvalidKey =
            new Error("Save.InvalidKey", "Save object key cannot be empty.");

        public static Error DuplicateKey(SaveKey key)
        {
            return new Error("Save.DuplicateKey", $"Save object already registered: {key}.");
        }

        public static Error LoadFailed(Error error)
        {
            return new Error("Save.LoadFailed", error.ToString());
        }

        public static Error SaveFailed(Error error)
        {
            return new Error("Save.SaveFailed", error.ToString());
        }

        public static Error CaptureFailed(SaveKey key, Error error)
        {
            return new Error("Save.CaptureFailed", $"Failed to capture save object '{key}'. {error}");
        }

        public static Error RestoreFailed(SaveKey key, Error error)
        {
            return new Error("Save.RestoreFailed", $"Failed to restore save object '{key}'. {error}");
        }
    }
}
