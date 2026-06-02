using SWeetJelly.Save.Contract;

namespace SWeetJelly.Save.Domain
{
    public sealed class SimpleStringSave : PrimitiveSaveObject<string>
    {
        public SimpleStringSave(SaveKey key, string defaultValue = "")
            : base(key, defaultValue)
        {
        }
    }
}