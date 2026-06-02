using SWeetJelly.Save.Contract;

namespace SWeetJelly.Save.Domain
{
    public sealed class SimpleIntSave : PrimitiveSaveObject<int>
    {
        public SimpleIntSave(SaveKey key, int defaultValue = 0)
            : base(key, defaultValue)
        {
        }
    }
}