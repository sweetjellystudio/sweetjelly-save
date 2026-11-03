namespace SWeetJelly.Save.Tests.EditMode
{
    using System;
    using NUnit.Framework;
    using SWeetJelly.Save.Contract;
    using SWeetJelly.Save.Domain;

    public sealed class SaveDomainTest
    {
        [Test]
        public void SaveKey_WithSameValue_IsEqual()
        {
            SaveKey left = "player";
            SaveKey right = new SaveKey("player");

            Assert.That(left.Equals(right), Is.True);
            Assert.That(left.GetHashCode(), Is.EqualTo(right.GetHashCode()));
            Assert.That(left.ToString(), Is.EqualTo("player"));
        }

        [Test]
        public void PrimitiveSaveObject_CapturesAndRestoresValue()
        {
            var save = new SimpleStringSave("name", "default");

            save.Set("sweet");
            Assert.That(save.Capture(), Is.EqualTo("sweet"));

            save.RestoreUntyped("jelly");
            Assert.That(save.Value, Is.EqualTo("jelly"));
        }

        [Test]
        public void PrimitiveSaveObject_WithWrongRestoreType_ThrowsInvalidCastException()
        {
            var save = new SimpleIntSave("coins", 1);

            var exception = AssertThrows<InvalidCastException>(() =>
                save.RestoreUntyped("not an int"));

            Assert.That(exception.Message, Does.Contain("Cannot restore value"));
        }

        [Test]
        public void GlobalSaveData_Default_PreservesEntryDocumentVersion()
        {
            var save = new GlobalSaveData();
            Assert.That(save.SavedData.Version, Is.EqualTo(1));
            Assert.That(save.SavedData.Entries, Is.Empty);
        }

        private static TException AssertThrows<TException>(Action action)
            where TException : Exception
        {
            try
            {
                action();
            }
            catch (TException exception)
            {
                return exception;
            }

            Assert.Fail($"Expected exception of type {typeof(TException).Name}.");
            return null;
        }
    }
}
