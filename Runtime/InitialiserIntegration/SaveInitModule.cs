using SWeetJelly.Initialiser.Contract;
using SWeetJelly.Initialiser.Framework;
using SWeetJelly.Save.App;
using SWeetJelly.Save.Contract;
using SWeetJelly.Serialization.Contract;
using SWeetJelly.Save.Framework;
using UnityEngine;

namespace SWeetJelly.Save.InitialiserIntegration
{
    [RegisterModule("Save")]
    [CreateAssetMenu(
        fileName = "Save Init Module",
        menuName = "SWeetJelly/Initialiser/Modules/Save")]
    public sealed class SaveInitModule : InitModuleSo
    {
        private const SerializationFormat SaveFormat = SerializationFormat.Json;
        private const SerializationFormat EntryFormat = SerializationFormat.Json;

        [SerializeField] private string saveFileName = "save.json";

#if UNITY_EDITOR
        [SerializeField] private string editorStartScenePath = "Assets/Project/Content/Scenes/Init.unity";
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

        public override string ModuleName => "Save";

        public override void Install(InitModuleInstallContext context)
        {
            if (context == null)
                throw new System.ArgumentNullException(nameof(context));

            ISerializationService serializer = context.Services.Resolve<ISerializationService>();

            var saveService = new SaveService(
                serializer,
                saveFileName,
                SaveFormat,
                EntryFormat);

            var saveBehaviour = context.HolderObject.GetComponent<SaveBehaviour>();

            if (saveBehaviour == null)
                saveBehaviour = context.HolderObject.AddComponent<SaveBehaviour>();

            saveBehaviour.Construct(saveService);

            saveBehaviour.Configure(
                useAutoSave,
                autoSaveInterval,
                saveOnApplicationPause,
                saveOnApplicationQuit);

            context.Services.Register<ISaveService>(saveService);

            saveBehaviour.BeginLoad();
        }
    }
}