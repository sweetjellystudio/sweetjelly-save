using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using SWeetJelly.Save.App;
using UnityEngine;

namespace SWeetJelly.Save.Framework
{
    public sealed class SaveBehaviour : MonoBehaviour
    {
        [Header("Auto Save")]
        [SerializeField] private bool useAutoSave = true;
        [SerializeField] private float autoSaveInterval = 30f;

        [Header("Application Lifecycle")]
        [SerializeField] private bool saveOnApplicationPause = true;
        [SerializeField] private bool saveOnApplicationQuit = true;

        private SaveService saveService;
        private CancellationTokenSource lifetimeCts;
        private Coroutine autoSaveCoroutine;
        private bool isSaving;

        public bool IsConstructed => saveService != null;

        public void Configure(
            bool useAutoSave,
            float autoSaveInterval,
            bool saveOnApplicationPause,
            bool saveOnApplicationQuit)
        {
            this.useAutoSave = useAutoSave;
            this.autoSaveInterval = autoSaveInterval;
            this.saveOnApplicationPause = saveOnApplicationPause;
            this.saveOnApplicationQuit = saveOnApplicationQuit;
        }

        public void Construct(SaveService service)
        {
            saveService = service ?? throw new ArgumentNullException(nameof(service));
        }

        public void BeginLoad()
        {
            if (lifetimeCts == null)
                lifetimeCts = new CancellationTokenSource();

            _ = LoadSafeAsync(lifetimeCts.Token);
        }

        public Task ForceSaveFromDriverAsync(CancellationToken ct = default)
        {
            EnsureConstructed();

            return SaveSafeAsync(true, ct);
        }

        private async Task LoadSafeAsync(CancellationToken ct)
        {
            try
            {
                EnsureConstructed();

                saveService.UpdateTime(Time.time);

                await saveService.LoadAsync(ct);

                if (useAutoSave)
                    StartAutoSave();
            }
            catch (OperationCanceledException)
            {
                // Lifetime cancellation. Ignore.
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        private void StartAutoSave()
        {
            if (autoSaveCoroutine != null)
                return;

            if (autoSaveInterval <= 0f)
                return;

            autoSaveCoroutine = StartCoroutine(AutoSaveCoroutine());
        }

        private void StopAutoSave()
        {
            if (autoSaveCoroutine == null)
                return;

            StopCoroutine(autoSaveCoroutine);
            autoSaveCoroutine = null;
        }

        private IEnumerator AutoSaveCoroutine()
        {
            var wait = new WaitForSeconds(autoSaveInterval);

            while (true)
            {
                yield return wait;

                if (saveService == null || !saveService.IsLoaded)
                    continue;

                _ = SaveSafeAsync(false, lifetimeCts.Token);
            }
        }

        private async Task SaveSafeAsync(
            bool forceSave,
            CancellationToken ct)
        {
            if (saveService == null)
                return;

            if (!saveService.IsLoaded)
                return;

            if (isSaving)
                return;

            try
            {
                isSaving = true;

                saveService.UpdateTime(Time.time);

                await saveService.SaveAsync(forceSave, ct);
            }
            catch (OperationCanceledException)
            {
                // Lifetime cancellation. Ignore.
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
            finally
            {
                isSaving = false;
            }
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (!pauseStatus)
                return;

            if (!saveOnApplicationPause)
                return;

            if (saveService == null || !saveService.IsLoaded)
                return;

            _ = SaveSafeAsync(true, CancellationToken.None);
        }

        private void OnApplicationQuit()
        {
            if (!saveOnApplicationQuit)
                return;

            if (saveService == null || !saveService.IsLoaded)
                return;

            try
            {
                saveService.UpdateTime(Time.time);

                saveService
                    .SaveAsync(true, CancellationToken.None)
                    .GetAwaiter()
                    .GetResult();
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        private void OnDestroy()
        {
            StopAutoSave();

            if (lifetimeCts == null)
                return;

            lifetimeCts.Cancel();
            lifetimeCts.Dispose();
            lifetimeCts = null;
        }

        private void EnsureConstructed()
        {
            if (saveService == null)
                throw new InvalidOperationException(
                    $"{nameof(SaveBehaviour)} has not been constructed.");
        }
    }
}