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
        public void SavePackageBootstrapperSo_RegistersSaveServiceAndAddsSaveBehaviour()
        {
            var holder = new GameObject("Save Test Holder");
            var module = ScriptableObject.CreateInstance<SavePackageBootstrapperSo>();

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
                UnityEngine.Object.DestroyImmediate(module);
                UnityEngine.Object.DestroyImmediate(holder);
            }
        }
    }
}
