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
        public void GlobalSaveData_Flush_AccumulatesGameTimeAndExitTime()
        {
            var save = new GlobalSaveData();
            var exitTime = new DateTime(2026, 6, 4, 12, 0, 0);

            save.Initialise(10f);
            save.SetTime(12.5f);
            save.Flush(15f, exitTime);

            Assert.That(save.SavedGameTime, Is.EqualTo(5f).Within(0.0001f));
            Assert.That(save.GameTime, Is.EqualTo(5f).Within(0.0001f));
            Assert.That(save.LastExitTime, Is.EqualTo(exitTime));
            Assert.That(save.SavedData, Is.Not.Null);
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
