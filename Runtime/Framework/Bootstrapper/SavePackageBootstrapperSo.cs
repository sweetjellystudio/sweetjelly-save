using SWeetJelly.Bootstrapper.Framework.Unity;
using SWeetJelly.Save.App;
using SWeetJelly.Bootstrapper.Contract;
using SWeetJelly.Serialization.Framework.Unity;
using SWeetJelly.Save.Contract;
using SWeetJelly.Serialization.Contract;
using SWeetJelly.Save.Framework.Unity;
using UnityEngine;

namespace SWeetJelly.Save.Framework.Bootstrapper
{
    [CreateAssetMenu(
        fileName = "Save Package Bootstrapper",
        menuName = "SWeetJelly/Bootstrapper/Packages/Save")]
    public sealed class SavePackageBootstrapperSo : PackageBootstrapperSo
    {
        private const SerializationFormat SaveFormat = SerializationFormat.Json;
        private const SerializationFormat EntryFormat = SerializationFormat.Json;

        [SerializeField] private string saveFileName = "save.json";

#if UNITY_EDITOR
        [SerializeField] private string editorStartScenePath = "";
#endif

        public string SaveFileName => saveFileName;

#if UNITY_EDITOR
        public string EditorStartScenePath => editorStartScenePath;
#endif

        [Header("Auto Save")]
        [SerializeField] private bool useAutoSave = false;
        [SerializeField] private float autoSaveInterval = 30f;

        [Header("Application Lifecycle")]
        [SerializeField] private bool saveOnApplicationPause = true;
        [SerializeField] private bool saveOnApplicationQuit = true;

        public override string PackageName => "Save";

        private SaveService ownedService;
        private SaveBehaviour driver;
        private IServiceRegistry registry;
        private bool ownsDriver;

        public override void Bootstrap(PackageBootstrapContext context)
        {
            if (context == null) throw new System.ArgumentNullException(nameof(context));
            if (ownedService != null) throw new System.InvalidOperationException("Save module is already running.");
            if (context.Services.TryResolve<ISaveService>(out _))
                throw new System.InvalidOperationException("A save service is already registered.");
            var existing = context.HolderObject.GetComponent<SaveBehaviour>();
            if (existing != null && existing.IsConstructed)
                throw new System.InvalidOperationException("The existing save driver is already constructed.");
            var codecs = context.Services.Resolve<ISerializationService>();
            var serializer = new AtomicSaveSerializer(codecs, new UnityPersistentDataPathProvider(), new AtomicFileStorage());
            var service = new SaveService(serializer, saveFileName, SaveFormat, EntryFormat);
            bool created = existing == null;
            var newDriver = existing != null ? existing : context.HolderObject.AddComponent<SaveBehaviour>();
            try
            {
                if (created) newDriver.Configure(useAutoSave, autoSaveInterval, saveOnApplicationPause, saveOnApplicationQuit);
                newDriver.Construct(service);
                context.Services.Register<ISaveService>(service);
                newDriver.BeginLoad();
            }
            catch
            {
                newDriver.Shutdown();
                if (context.Services.TryResolve<ISaveService>(out var registered) && ReferenceEquals(service, registered))
                    context.Services.Unregister<ISaveService>();
                if (created) DestroyDriver(newDriver);
                throw;
            }
            ownedService = service;
            driver = newDriver;
            registry = context.Services;
            ownsDriver = created;
        }

        public override void Shutdown(PackageBootstrapContext context)
        {
            if (ownedService == null) return;
            if (driver != null) driver.Shutdown();
            if (registry.TryResolve<ISaveService>(out var registered) && ReferenceEquals(registered, ownedService))
                registry.Unregister<ISaveService>();
            if (ownsDriver && driver != null) DestroyDriver(driver);
            driver = null;
            ownedService = null;
            registry = null;
            ownsDriver = false;
        }

        private static void DestroyDriver(SaveBehaviour value)
        {
            if (Application.isPlaying) Destroy(value);
            else DestroyImmediate(value);
        }
    }
}
