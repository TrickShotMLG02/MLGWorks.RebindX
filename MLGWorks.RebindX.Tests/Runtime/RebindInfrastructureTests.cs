using NUnit.Framework;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using MLGWorks.RebindX.Runtime;

namespace MLGWorks.RebindX.Tests
{
    public sealed class RebindInfrastructureTests
    {
        private readonly System.Collections.Generic.List<InputActionAsset> m_Assets =
            new System.Collections.Generic.List<InputActionAsset>();

        [TearDown]
        public void TearDown()
        {
            foreach (var asset in m_Assets)
                Object.DestroyImmediate(asset);
            m_Assets.Clear();
        }

        private InputActionAsset CreateAsset()
        {
            var asset = InputActionAsset.FromJson("{\"maps\":[{\"name\":\"Gameplay\",\"actions\":[{\"name\":\"Jump\",\"type\":\"Button\"}],\"bindings\":[{\"path\":\"<Keyboard>/space\",\"action\":\"Jump\"}]}]}");
            m_Assets.Add(asset);
            return asset;
        }

        [Test]
        public void RebindSession_RestoresInitiallyEnabledAction()
        {
            var asset = CreateAsset();
            var action = asset.FindAction("Gameplay/Jump");
            action.Enable();

            using (var session = new RebindSession())
            {
                session.Begin(action);
                Assert.That(action.enabled, Is.False);
                session.Cancel();
            }

            Assert.That(action.enabled, Is.True);
        }

        [Test]
        public void RebindSession_PreservesInitiallyDisabledAction()
        {
            var asset = CreateAsset();
            var action = asset.FindAction("Gameplay/Jump");

            using (var session = new RebindSession())
            {
                session.Begin(action);
                session.Complete();
            }

            Assert.That(action.enabled, Is.False);
        }

        [Test]
        public void RebindSession_RejectsNestedSessions()
        {
            var asset = CreateAsset();
            var action = asset.FindAction("Gameplay/Jump");
            using var session = new RebindSession();

            session.Begin(action);
            Assert.Throws<System.InvalidOperationException>(() => session.Begin(action));
            session.Cancel();
        }

        [Test]
        public void InMemoryStore_RestoresBindingOverridesWithoutFileSystem()
        {
            var source = CreateAsset();
            var target = CreateAsset();
            var store = new InMemoryBindingOverrideStore();

            source.FindAction("Gameplay/Jump").ApplyBindingOverride(0, "<Keyboard>/enter");
            store.Save(source);
            store.Load(target);

            Assert.That(target.FindAction("Gameplay/Jump").bindings[0].overridePath, Is.EqualTo("<Keyboard>/enter"));
        }

        [Test]
        public void FileSystemPathProvider_ResolvesCustomFilePath()
        {
            var provider = new FileSystemRebindPathProvider(
                FileLocationType.Custom,
                "ignored",
                "C:/RebindX/Profiles",
                "player.json");

            Assert.That(provider.DirectoryPath, Is.EqualTo("C:/RebindX/Profiles"));
            Assert.That(provider.FilePath, Is.EqualTo(Path.Combine("C:/RebindX/Profiles", "player.json")));
        }

        [Test]
        public void FileSystemPathProvider_ResolvesPersistentPath()
        {
            var provider = new FileSystemRebindPathProvider(FileLocationType.PersistentDataPath, "Configs", "", "player.json");
            Assert.That(provider.DirectoryPath, Is.EqualTo(Path.Combine(Application.persistentDataPath, "Configs")));
        }

        [Test]
        public void FileSystemPathProvider_ResolvesDataPath()
        {
            var provider = new FileSystemRebindPathProvider(FileLocationType.DataPath, "Configs", "", "player.json");
            Assert.That(provider.DirectoryPath, Is.EqualTo(Path.Combine(Application.dataPath, "Configs")));
        }

        [Test]
        public void FileSystemPathProvider_RejectsBlankFileName()
        {
            var provider = new FileSystemRebindPathProvider(FileLocationType.Custom, "", "C:/RebindX", "");
            Assert.Throws<System.InvalidOperationException>(() => _ = provider.FilePath);
        }

        [Test]
        public void RebindSession_RejectsNullAction()
        {
            using var session = new RebindSession();
            Assert.Throws<System.ArgumentNullException>(() => session.Begin(null));
        }

        [Test]
        public void RebindSession_CompletingWhenIdleIsSafe()
        {
            using var session = new RebindSession();
            Assert.DoesNotThrow(() => session.Complete());
            Assert.That(session.IsActive, Is.False);
        }

        [Test]
        public void RebindSession_ExposesActiveAction()
        {
            var asset = CreateAsset();
            var action = asset.FindAction("Gameplay/Jump");
            using var session = new RebindSession();

            session.Begin(action);
            Assert.That(session.Action, Is.SameAs(action));
            session.Cancel();
            Assert.That(session.Action, Is.Null);
        }

        [Test]
        public void RebindSession_RestoresMapStateWithoutEnablingOtherActions()
        {
            var asset = CreateAsset();
            var map = asset.FindActionMap("Gameplay");
            map.Disable();
            var otherMap = asset.AddActionMap("Other");
            var otherAction = otherMap.AddAction("Pause", InputActionType.Button, binding: "<Keyboard>/escape");
            otherMap.Disable();
            var action = asset.FindAction("Gameplay/Jump");
            action.Enable();

            using (var session = new RebindSession())
            {
                session.Begin(action);
                session.Complete();
            }

            Assert.That(action.enabled, Is.True);
            Assert.That(otherAction.enabled, Is.False);
            Assert.That(map.enabled, Is.True);
            Assert.That(otherMap.enabled, Is.False);
            Assert.That(asset.enabled, Is.True);
        }

        [Test]
        public void RebindSession_RestoresInitiallyDisabledAsset()
        {
            var asset = CreateAsset();
            var action = asset.FindAction("Gameplay/Jump");
            asset.Disable();

            using (var session = new RebindSession())
            {
                session.Begin(action);
                session.Complete();
            }

            Assert.That(asset.enabled, Is.False);
            Assert.That(action.enabled, Is.False);
        }

        [Test]
        public void InMemoryStore_RejectsNullAssetOnSave()
        {
            Assert.Throws<System.ArgumentNullException>(() => new InMemoryBindingOverrideStore().Save(null));
        }

        [Test]
        public void InMemoryStore_RejectsNullAssetOnLoad()
        {
            Assert.Throws<System.ArgumentNullException>(() => new InMemoryBindingOverrideStore().Load(null));
        }

        [Test]
        public void InMemoryStore_DeleteClearsOverrides()
        {
            var asset = CreateAsset();
            var store = new InMemoryBindingOverrideStore();
            asset.FindAction("Gameplay/Jump").ApplyBindingOverride(0, "<Keyboard>/enter");
            store.Save(asset);
            store.Delete();
            asset.FindAction("Gameplay/Jump").RemoveBindingOverride(0);

            var result = store.Load(asset);

            Assert.That(result.Code, Is.EqualTo(BindingOverrideResultCode.NoData));
            Assert.That(asset.FindAction("Gameplay/Jump").bindings[0].overridePath, Is.Null.Or.Empty);
        }

        [Test]
        public void JsonStore_WritesVersionAndAssetIdentityEnvelope()
        {
            var asset = CreateAsset();
            var directory = Path.Combine(Application.temporaryCachePath, "RebindXInfrastructure");
            var provider = new FileSystemRebindPathProvider(FileLocationType.Custom, "", directory, "versioned.json");
            var store = new JsonBindingOverrideStore(provider, "keyboard-profile");

            var result = store.Save(asset);
            var json = File.ReadAllText(provider.FilePath);

            Assert.That(result.Code, Is.EqualTo(BindingOverrideResultCode.Success));
            Assert.That(json, Does.Contain("\"version\": " + JsonBindingOverrideStore.CurrentVersion));
            Assert.That(json, Does.Contain("\"assetId\": \"profile:keyboard-profile\""));
            Assert.That(json, Does.Contain("\"overrides\": "));
            File.Delete(provider.FilePath);
            Directory.Delete(directory, true);
        }

        [Test]
        public void JsonStore_RejectsOverridesFromDifferentProfile()
        {
            var asset = CreateAsset();
            var directory = Path.Combine(Application.temporaryCachePath, "RebindXInfrastructure");
            var provider = new FileSystemRebindPathProvider(FileLocationType.Custom, "", directory, "profile.json");
            new JsonBindingOverrideStore(provider, "profile-a").Save(asset);

            var result = new JsonBindingOverrideStore(provider, "profile-b").Load(asset);

            Assert.That(result.Code, Is.EqualTo(BindingOverrideResultCode.AssetMismatch), result.Message);
            File.Delete(provider.FilePath);
            Directory.Delete(directory, true);
        }

        [Test]
        public void JsonStore_RejectsOverridesFromDifferentAssetStructure()
        {
            var source = CreateAsset();
            var target = CreateAsset();
            target.FindAction("Gameplay/Jump").ApplyBindingOverride(0, "<Gamepad>/buttonSouth");
            var directory = Path.Combine(Application.temporaryCachePath, "RebindXInfrastructure");
            var provider = new FileSystemRebindPathProvider(FileLocationType.Custom, "", directory, "asset.json");
            var store = new JsonBindingOverrideStore(provider);
            store.Save(source);

            var result = store.Load(target);

            Assert.That(result.Code, Is.EqualTo(BindingOverrideResultCode.AssetMismatch), result.Message);
            Assert.That(target.FindAction("Gameplay/Jump").bindings[0].overridePath, Is.EqualTo("<Gamepad>/buttonSouth"));
            File.Delete(provider.FilePath);
            Directory.Delete(directory, true);
        }

        [Test]
        public void JsonStore_QuarantinesMalformedJson()
        {
            var asset = CreateAsset();
            var directory = Path.Combine(Application.temporaryCachePath, "RebindXInfrastructure");
            var provider = new FileSystemRebindPathProvider(FileLocationType.Custom, "", directory, "corrupt.json");
            Directory.CreateDirectory(directory);
            File.WriteAllText(provider.FilePath, "not valid json");

            var result = new JsonBindingOverrideStore(provider).Load(asset);

            Assert.That(result.Code, Is.EqualTo(BindingOverrideResultCode.CorruptData));
            Assert.That(File.Exists(provider.FilePath), Is.False);
            Assert.That(Directory.GetFiles(directory, "corrupt.json.corrupt-*.json").Length, Is.EqualTo(1));
            Directory.Delete(directory, true);
        }

        [Test]
        public void JsonStore_QuarantinesUnversionedLegacyJson()
        {
            var asset = CreateAsset();
            var directory = Path.Combine(Application.temporaryCachePath, "RebindXInfrastructure");
            var provider = new FileSystemRebindPathProvider(FileLocationType.Custom, "", directory, "legacy.json");
            Directory.CreateDirectory(directory);
            File.WriteAllText(provider.FilePath, "[]");

            var result = new JsonBindingOverrideStore(provider).Load(asset);

            Assert.That(result.Code, Is.EqualTo(BindingOverrideResultCode.CorruptData));
            Assert.That(File.Exists(provider.FilePath), Is.False);
            Directory.Delete(directory, true);
        }

        [Test]
        public void JsonStore_ReportsUnsupportedVersionWithoutQuarantiningFile()
        {
            var asset = CreateAsset();
            var directory = Path.Combine(Application.temporaryCachePath, "RebindXInfrastructure");
            var provider = new FileSystemRebindPathProvider(FileLocationType.Custom, "", directory, "future.json");
            Directory.CreateDirectory(directory);
            File.WriteAllText(provider.FilePath,
                "{\"version\":" + (JsonBindingOverrideStore.CurrentVersion + 1) +
                ",\"assetId\":\"" + JsonBindingOverrideStore.GetAssetId(asset) +
                "\",\"overrides\":\"[]\"}");

            var result = new JsonBindingOverrideStore(provider).Load(asset);

            Assert.That(result.Code, Is.EqualTo(BindingOverrideResultCode.UnsupportedVersion));
            Assert.That(File.Exists(provider.FilePath), Is.True);
            File.Delete(provider.FilePath);
            Directory.Delete(directory, true);
        }

        [Test]
        public void JsonStore_DeleteRemovesPersistedFile()
        {
            var asset = CreateAsset();
            var directory = Path.Combine(Application.temporaryCachePath, "RebindXInfrastructure");
            var provider = new FileSystemRebindPathProvider(FileLocationType.Custom, "", directory, "delete.json");
            var store = new JsonBindingOverrideStore(provider);
            store.Save(asset);

            var result = store.Delete();

            Assert.That(result.Succeeded, Is.True);
            Assert.That(File.Exists(provider.FilePath), Is.False);
            Directory.Delete(directory, true);
        }

        [Test]
        public void InputActionAssetProvider_RejectsNullAsset()
        {
            Assert.Throws<System.ArgumentNullException>(() => new InputActionAssetProvider(null));
        }

        [Test]
        public void GeneratedControlsProvider_RejectsNullControls()
        {
            Assert.Throws<System.ArgumentNullException>(() => new GeneratedControlsProvider(null));
        }
    }
}
