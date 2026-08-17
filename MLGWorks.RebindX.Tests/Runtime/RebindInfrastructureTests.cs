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
