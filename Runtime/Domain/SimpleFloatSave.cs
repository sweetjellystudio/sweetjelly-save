using SWeetJelly.Save.Contract;

namespace SWeetJelly.Save.Domain
{
    public sealed class SimpleFloatSave : PrimitiveSaveObject<float>
    {
        public SimpleFloatSave(SaveKey key, float defaultValue = 0f)
            : base(key, defaultValue)
        {
        }
    }
}