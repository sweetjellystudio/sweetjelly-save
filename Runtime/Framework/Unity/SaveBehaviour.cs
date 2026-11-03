using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using SWeetJelly.Save.App;
using SWeetJelly.Shared.Result;
using UnityEngine;

namespace SWeetJelly.Save.Framework.Unity
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
        private Task loadTask;
        private Task quitTask;
        private bool quitAllowed;

        public string SaveFileName => saveService?.SaveFileName;

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
            if (saveService != null) throw new InvalidOperationException("Driver already constructed.");
            saveService = service ?? throw new ArgumentNullException(nameof(service));
            quitAllowed = false;
            Application.wantsToQuit += WantsToQuit;
        }

        public void BeginLoad()
        {
            EnsureConstructed();
            if (lifetimeCts == null)
                lifetimeCts = new CancellationTokenSource();

            if (loadTask == null) loadTask = LoadSafeAsync(lifetimeCts.Token);
        }

        public async Task<Result> ForceSaveFromDriverAsync(CancellationToken ct = default)
        {
            EnsureConstructed();

            var source = lifetimeCts;
            var service = saveService;
            var loading = loadTask;
            using var linked = source == null ? null : CancellationTokenSource.CreateLinkedTokenSource(ct, source.Token);
            var token = linked?.Token ?? ct;
            token.ThrowIfCancellationRequested();
            if (loading != null)
            {
                if (token.CanBeCanceled)
                {
                    using var waiting = CancellationTokenSource.CreateLinkedTokenSource(token);
                    var cancelled = Task.Delay(Timeout.Infinite, waiting.Token);
                    await Task.WhenAny(loading, cancelled);
                    waiting.Cancel();
                    token.ThrowIfCancellationRequested();
                }
                await loading;
            }
            token.ThrowIfCancellationRequested();
            if (!ReferenceEquals(service, saveService))
                return Result.Failure(SWeetJelly.Save.Contract.SaveErrors.NotLoaded);
            return await SaveSafeAsync(true, token);
        }

        private async Task LoadSafeAsync(CancellationToken ct)
        {
            try
            {
                EnsureConstructed();



                Result loadResult = await saveService.LoadAsync(ct);

                if (loadResult.IsFailure)
                {
                    Debug.LogError(loadResult.Error.ToString(), this);
                    return;
                }

                if (useAutoSave && !ct.IsCancellationRequested && this != null)
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

                _ = ObserveSaveAsync(false, lifetimeCts.Token);
            }
        }

        private async Task<Result> SaveSafeAsync(bool forceSave, CancellationToken ct)
        {
            if (saveService == null || !saveService.IsLoaded)
                return Result.Failure(SWeetJelly.Save.Contract.SaveErrors.NotLoaded);
            try
            {
                var result = await saveService.SaveAsync(forceSave, ct);
                if (result.IsFailure) Debug.LogError(result.Error.ToString(), this);
                return result;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception e)
            {
                Debug.LogException(e, this);
                return Result.Failure(new SWeetJelly.Shared.Result.Error("Save.DriverFailed", e.Message));
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

            _ = ObserveSaveAsync(true, lifetimeCts?.Token ?? CancellationToken.None);
        }

        private async Task ObserveSaveAsync(bool force, CancellationToken ct)
        {
            try { await SaveSafeAsync(force, ct); }
            catch (OperationCanceledException) { }
        }

        private bool WantsToQuit()
        {
            if (quitAllowed || Application.isEditor || !saveOnApplicationQuit || saveService == null) return true;
            if (!saveService.IsLoaded && loadTask == null) return true;
            if (quitTask == null) quitTask = FlushAndQuitAsync();
            return false;
        }

        private async Task FlushAndQuitAsync()
        {
            await Task.Yield();
            try
            {
                var result = await ForceSaveFromDriverAsync();
                if (result.IsFailure) return;
                quitAllowed = true;
                Application.Quit();
            }
            catch (OperationCanceledException) { }
            catch (Exception e) { Debug.LogException(e, this); }
            finally { quitTask = null; }
        }

        public void Shutdown()
        {
            Application.wantsToQuit -= WantsToQuit;
            StopAutoSave();
            lifetimeCts?.Cancel();
            lifetimeCts?.Dispose();
            lifetimeCts = null;
            saveService = null;
            loadTask = null;
        }

        private void OnDestroy() => Shutdown();

        private void EnsureConstructed()
        {
            if (saveService == null)
                throw new InvalidOperationException(
                    $"{nameof(SaveBehaviour)} has not been constructed.");
        }
    }
}
