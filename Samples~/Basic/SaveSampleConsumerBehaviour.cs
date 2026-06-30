namespace SWeetJelly.Samples.Save
{
    using System;
    using SWeetJelly.Bootstrapper.Framework.Unity;
    using SWeetJelly.Save.Contract;
    using SWeetJelly.Save.Domain;
    using SWeetJelly.Shared.Result;
    using UnityEngine;

    public sealed class SaveSampleConsumerBehaviour : MonoBehaviour
    {
        private ISaveService saveService;
        private SimpleIntSave coins;
        private SimpleStringSave playerName;
        private string status = "Waiting for Save service...";

        private void OnEnable()
        {
            BootstrapperBehaviour.Bootstrapped += OnBootstrapped;
            ResolveIfReady();
        }

        private void OnDisable()
        {
            BootstrapperBehaviour.Bootstrapped -= OnBootstrapped;
            Unsubscribe();
        }

        private void OnBootstrapped(object sender, EventArgs args)
        {
            ResolveIfReady();
        }

        private void ResolveIfReady()
        {
            if (saveService != null || !BootstrapperBehaviour.IsBootstrapped || BootstrapperBehaviour.Services == null)
                return;

            if (!BootstrapperBehaviour.Services.TryResolve<ISaveService>(out saveService))
            {
                status = "ISaveService is not registered.";
                return;
            }

            saveService.Loaded += OnLoaded;
            saveService.Saved += OnSaved;
            saveService.Deleted += OnDeleted;

            coins = new SimpleIntSave("sample.coins", 0);
            playerName = new SimpleStringSave("sample.playerName", "Sweet Jelly");

            Result coinsRegisterResult = saveService.Register(coins);

            if (coinsRegisterResult.IsFailure)
            {
                status = $"Register coins failed: {coinsRegisterResult.Error}";
                return;
            }

            Result playerNameRegisterResult = saveService.Register(playerName);

            if (playerNameRegisterResult.IsFailure)
            {
                status = $"Register player name failed: {playerNameRegisterResult.Error}";
                return;
            }

            status = saveService.IsLoaded
                ? "Save service is ready."
                : "Save service is registered. Waiting for load.";
        }

        private void Unsubscribe()
        {
            if (saveService == null)
                return;

            saveService.Loaded -= OnLoaded;
            saveService.Saved -= OnSaved;
            saveService.Deleted -= OnDeleted;
        }

        private void OnLoaded(object sender, EventArgs args)
        {
            status = "Loaded and restored registered save objects.";
        }

        private void OnSaved(object sender, EventArgs args)
        {
            status = "Saved registered save objects.";
        }

        private void OnDeleted(object sender, EventArgs args)
        {
            coins?.Set(0);
            playerName?.Set("Sweet Jelly");
            status = "Deleted save data.";
        }

        private void OnGUI()
        {
            const float x = 16f;
            var y = 16f;

            GUI.Label(new Rect(x, y, 620f, 24f), status);
            y += 28f;

            GUI.Label(new Rect(x, y, 420f, 24f), $"Loaded: {saveService?.IsLoaded ?? false}   Dirty: {saveService?.IsDirty ?? false}");
            y += 28f;

            GUI.Label(new Rect(x, y, 420f, 24f), $"Coins: {coins?.Value ?? 0}");
            y += 24f;

            GUI.Label(new Rect(x, y, 420f, 24f), $"Player Name: {playerName?.Value ?? string.Empty}");
            y += 36f;

            GUI.enabled = saveService != null && coins != null && playerName != null;

            if (GUI.Button(new Rect(x, y, 190f, 28f), "Add Coins"))
            {
                coins.Set(coins.Value + 10);
                saveService.MarkDirty();
                status = "Changed coins and marked dirty.";
            }

            y += 34f;

            if (GUI.Button(new Rect(x, y, 190f, 28f), "Change Name"))
            {
                playerName.Set($"Sweet Jelly {coins.Value}");
                saveService.MarkDirty();
                status = "Changed name and marked dirty.";
            }

            y += 42f;

            if (GUI.Button(new Rect(x, y, 190f, 28f), "Load"))
                Load();

            y += 34f;

            if (GUI.Button(new Rect(x, y, 190f, 28f), "Save"))
                Save();

            y += 34f;

            if (GUI.Button(new Rect(x, y, 190f, 28f), "Force Save"))
                ForceSave();

            y += 34f;

            if (GUI.Button(new Rect(x, y, 190f, 28f), "Delete"))
                saveService.Delete();

            GUI.enabled = true;
        }

        private async void Load()
        {
            try
            {
                Result result = await saveService.LoadAsync();

                if (result.IsFailure)
                    status = $"Load failed: {result.Error}";
            }
            catch (Exception ex)
            {
                status = $"Load failed: {ex.Message}";
                Debug.LogException(ex, this);
            }
        }

        private async void Save()
        {
            try
            {
                Result result = await saveService.SaveAsync();

                if (result.IsFailure)
                    status = $"Save failed: {result.Error}";
            }
            catch (Exception ex)
            {
                status = $"Save failed: {ex.Message}";
                Debug.LogException(ex, this);
            }
        }

        private async void ForceSave()
        {
            try
            {
                Result result = await saveService.SaveAsync(forceSave: true);

                if (result.IsFailure)
                    status = $"Force save failed: {result.Error}";
            }
            catch (Exception ex)
            {
                status = $"Force save failed: {ex.Message}";
                Debug.LogException(ex, this);
            }
        }
    }
}
