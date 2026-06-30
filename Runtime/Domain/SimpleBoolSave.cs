using SWeetJelly.Save.Contract;

namespace SWeetJelly.Save.Domain
{
    public sealed class SimpleBoolSave : PrimitiveSaveObject<bool>
    {
        public SimpleBoolSave(SaveKey key, bool defaultValue = false)
            : base(key, defaultValue)
        {
        }
    }
}