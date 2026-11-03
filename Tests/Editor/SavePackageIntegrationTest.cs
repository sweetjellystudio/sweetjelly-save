namespace SWeetJelly.Save.Tests.EditMode
{
    using NUnit.Framework;
    using SWeetJelly.Bootstrapper.App;
    using SWeetJelly.Bootstrapper.Framework.Unity;
    using SWeetJelly.Save.Contract;
    using SWeetJelly.Save.Framework.Unity;
    using SWeetJelly.Save.Framework.Bootstrapper;
    using SWeetJelly.Serialization.Contract;
    using UnityEngine;

    public sealed class SavePackageIntegrationTest
    {
        [Test]
        public void BorrowedDriver_IsReleasedButNotDestroyed_AndDuplicateDoesNotReplaceService()
        {
            var holder = new GameObject("borrowed");
            var driver = holder.AddComponent<SaveBehaviour>();
            driver.Configure(false, 17, false, false);
            var module = ScriptableObject.CreateInstance<SavePackageBootstrapperSo>();
            var second = ScriptableObject.CreateInstance<SavePackageBootstrapperSo>();
            var settings = new UnityEditor.SerializedObject(module);
            settings.FindProperty("saveFileName").stringValue = "save-test-" + System.Guid.NewGuid().ToString("N");
            settings.ApplyModifiedPropertiesWithoutUndo();
            var registry = new ServiceRegistry();
            registry.Register<ISerializationService>(new FakeSerializationService());
            var context = new PackageBootstrapContext(holder, null, null, registry);
            try
            {
                module.Bootstrap(context);
                var original = registry.Resolve<ISaveService>();
                Assert.Throws<System.InvalidOperationException>(() => second.Bootstrap(context));
                Assert.That(registry.Resolve<ISaveService>(), Is.SameAs(original));
                module.Shutdown(context);
                Assert.That(holder.GetComponent<SaveBehaviour>(), Is.SameAs(driver));
                Assert.That(driver.IsConstructed, Is.False);
                Assert.That(registry.TryResolve<ISaveService>(out _), Is.False);
                module.Bootstrap(context);
                module.Shutdown(context);
            }
            finally
            {
                module.Shutdown(context);
                Object.DestroyImmediate(second); Object.DestroyImmediate(module); Object.DestroyImmediate(holder);
            }
        }

        [Test]
        public void Shutdown_PreservesAReplacementRegistration()
        {
            var holder = new GameObject("replacement");
            var module = ScriptableObject.CreateInstance<SavePackageBootstrapperSo>();
            var settings = new UnityEditor.SerializedObject(module);
            settings.FindProperty("saveFileName").stringValue = "save-test-" + System.Guid.NewGuid().ToString("N");
            settings.ApplyModifiedPropertiesWithoutUndo();
            var registry = new ServiceRegistry();
            registry.Register<ISerializationService>(new FakeSerializationService());
            var context = new PackageBootstrapContext(holder, null, null, registry);
            try
            {
                module.Bootstrap(context);
                registry.Unregister<ISaveService>();
                var replacement = new SWeetJelly.Save.App.SaveService(new FakeSerializationService(), "unused");
                registry.Register<ISaveService>(replacement);
                module.Shutdown(context);
                Assert.That(registry.Resolve<ISaveService>(), Is.SameAs(replacement));
                Assert.That(holder.GetComponent<SaveBehaviour>(), Is.Null);
            }
            finally { module.Shutdown(context); Object.DestroyImmediate(module); Object.DestroyImmediate(holder); }
        }

        [Test]
        public void SavePackageBootstrapperSo_RegistersSaveServiceAndAddsSaveBehaviour()
        {
            var holder = new GameObject("Save Test Holder");
            var module = ScriptableObject.CreateInstance<SavePackageBootstrapperSo>();

            var settings = new UnityEditor.SerializedObject(module);
            settings.FindProperty("saveFileName").stringValue = "save-test-" + System.Guid.NewGuid().ToString("N") + ".json";
            settings.ApplyModifiedPropertiesWithoutUndo();

            try
            {
                var registry = new ServiceRegistry();
                registry.Register<ISerializationService>(new FakeSerializationService());

                var context = new PackageBootstrapContext(holder, null, null, registry);

                module.Bootstrap(context);

                Assert.That(holder.GetComponent<SaveBehaviour>(), Is.Not.Null);
                Assert.That(holder.GetComponent<SaveBehaviour>().IsConstructed, Is.True);
                Assert.That(registry.TryResolve<ISaveService>(out var service), Is.True);
                Assert.That(service, Is.Not.Null);
            }
            finally
            {
                module.Shutdown(null);
                UnityEngine.Object.DestroyImmediate(module);
                UnityEngine.Object.DestroyImmediate(holder);
            }
        }
    }
}
