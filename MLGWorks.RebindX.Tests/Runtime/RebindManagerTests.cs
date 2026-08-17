using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using MLGWorks.RebindX.Runtime;

namespace MLGWorks.RebindX.Tests
{
    public sealed class RebindManagerTests
    {
        private GameObject m_ManagerObject;
        private RebindManager m_Manager;
        private InputActionAsset m_Asset;
        private string m_TestDirectory;

        [SetUp]
        public void Setup()
        {
            LogAssert.ignoreFailingMessages = true;
            m_TestDirectory = Path.Combine(Application.temporaryCachePath, "RebindXTests");
            Directory.CreateDirectory(m_TestDirectory);

            m_ManagerObject = new GameObject("RebindManager Test");
            m_Manager = m_ManagerObject.AddComponent<RebindManager>();
            SetPrivateField("pathType", FileLocationType.Custom);
            SetPrivateField("customPath", m_TestDirectory);
            SetPrivateField("fileName", "bindings.json");

            m_Asset = ScriptableObject.CreateInstance<InputActionAsset>();
            var map = m_Asset.AddActionMap("Gameplay");
            map.AddAction("Jump", InputActionType.Button, binding: "<Keyboard>/space");
            m_Manager.SetActionAsset(m_Asset);
        }

        [TearDown]
        public void TearDown()
        {
            LogAssert.ignoreFailingMessages = false;
            if (m_ManagerObject != null)
                Object.DestroyImmediate(m_ManagerObject);
            if (m_Asset != null)
                Object.DestroyImmediate(m_Asset);
            if (Directory.Exists(m_TestDirectory))
                Directory.Delete(m_TestDirectory, true);
        }

        [Test]
        public void SetActionAsset_UsesAndEnablesConfiguredAsset()
        {
            Assert.That(m_Manager.ActionAsset, Is.SameAs(m_Asset));
            Assert.That(m_Asset.enabled, Is.True);
        }

        [Test]
        public void SetActionAsset_RejectsNullAsset()
        {
            Assert.Throws<System.ArgumentNullException>(() => m_Manager.SetActionAsset(null));
        }

        [Test]
        public void SetControls_RejectsNullControls()
        {
            Assert.Throws<System.ArgumentNullException>(() => m_Manager.SetControls(null));
        }

        [Test]
        public void OverrideStore_RejectsNullImplementation()
        {
            Assert.Throws<System.ArgumentNullException>(() => m_Manager.OverrideStore = null);
        }

        [Test]
        public void PathProvider_RejectsNullImplementation()
        {
            Assert.Throws<System.ArgumentNullException>(() => m_Manager.PathProvider = null);
        }

        [Test]
        public void SaveRebinds_WithoutAssetLogsErrorAndDoesNotThrow()
        {
            SetPrivateField("_actionAsset", null);

            LogAssert.Expect(LogType.Error, "Cannot save rebinds before the input controls have been initialized.");
            Assert.DoesNotThrow(() => m_Manager.SaveRebinds());
        }

        [Test]
        public void LoadRebinds_WithoutAssetLogsErrorAndDoesNotThrow()
        {
            SetPrivateField("_actionAsset", null);

            LogAssert.Expect(LogType.Error, "Cannot load rebinds before the input controls have been initialized.");
            Assert.DoesNotThrow(() => m_Manager.LoadRebinds());
        }

        [Test]
        public void SetActionAsset_SameAssetIsIdempotent()
        {
            var asset = m_Manager.ActionAsset;
            m_Manager.SetActionAsset(asset);
            Assert.That(m_Manager.ActionAsset, Is.SameAs(asset));
            Assert.That(asset.enabled, Is.True);
        }

        [Test]
        public void SaveAndLoadRebinds_RestoresBindingOverrides()
        {
            var action = m_Asset.FindAction("Gameplay/Jump");
            action.ApplyBindingOverride(0, "<Keyboard>/enter");

            m_Manager.SaveRebinds();
            action.RemoveBindingOverride(0);
            m_Manager.LoadRebinds();

            Assert.That(action.bindings[0].overridePath, Is.EqualTo("<Keyboard>/enter"));
            Assert.That(File.Exists(m_Manager.FilePath), Is.True);
        }

        [Test]
        public void OverrideStore_CanBeReplacedWithInMemoryImplementation()
        {
            var store = new InMemoryBindingOverrideStore();
            var action = m_Asset.FindAction("Gameplay/Jump");
            action.ApplyBindingOverride(0, "<Keyboard>/enter");

            store.Save(m_Asset);
            action.RemoveBindingOverride(0);
            store.Load(m_Asset);

            Assert.That(action.bindings[0].overridePath, Is.EqualTo("<Keyboard>/enter"));
        }

        [Test]
        public void LoadRebinds_InvalidJsonDoesNotThrow()
        {
            Directory.CreateDirectory(m_Manager.DirectoryPath);
            File.WriteAllText(m_Manager.FilePath, "not valid json");

            LogAssert.Expect(LogType.Error, new Regex("Failed to load Input Config File:.*"));
            var result = m_Manager.LoadRebinds();

            Assert.That(result.Code, Is.EqualTo(BindingOverrideResultCode.CorruptData));
            Assert.That(File.Exists(m_Manager.FilePath), Is.False);
        }

        [Test]
        public void SetActionAsset_ReplacesPreviouslyManagedAsset()
        {
            var replacement = ScriptableObject.CreateInstance<InputActionAsset>();
            replacement.AddActionMap("Replacement").AddAction("Action", InputActionType.Button);

            m_Manager.SetActionAsset(replacement);

            Assert.That(m_Manager.ActionAsset, Is.SameAs(replacement));
            Assert.That(replacement.enabled, Is.True);
            Assert.That(m_Asset.enabled, Is.False);

            Object.DestroyImmediate(replacement);
        }

        [Test]
        public void SaveRebinds_ReturnsSuccessResult()
        {
            var result = m_Manager.SaveRebinds();

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Code, Is.EqualTo(BindingOverrideResultCode.Success));
        }

        [Test]
        public void LoadRebinds_ReturnsNoDataWhenFileIsMissing()
        {
            var result = m_Manager.LoadRebinds();

            Assert.That(result.Code, Is.EqualTo(BindingOverrideResultCode.NoData));
            Assert.That(result.Succeeded, Is.True);
        }

        [Test]
        public void ResetRebinds_RemovesOverridesAndPersistedFile()
        {
            var action = m_Asset.FindAction("Gameplay/Jump");
            action.ApplyBindingOverride(0, "<Keyboard>/enter");
            m_Manager.SaveRebinds();

            var result = m_Manager.ResetRebinds();

            Assert.That(result.Succeeded, Is.True);
            Assert.That(action.bindings[0].overridePath, Is.Null.Or.Empty);
            Assert.That(File.Exists(m_Manager.FilePath), Is.False);
        }

        [Test]
        public void ProfileId_IsolatesTwoManagersUsingTheSameFile()
        {
            SetPrivateField("profileId", "profile-a");
            var action = m_Asset.FindAction("Gameplay/Jump");
            action.ApplyBindingOverride(0, "<Keyboard>/enter");
            m_Manager.SaveRebinds();

            SetPrivateField("profileId", "profile-b");
            m_Manager.OverrideStore = new JsonBindingOverrideStore(m_Manager.PathProvider, "profile-b");
            action.RemoveBindingOverride(0);
            LogAssert.Expect(LogType.Error, new Regex("Failed to load Input Config File: Binding overrides belong.*"));
            var result = m_Manager.LoadRebinds();

            Assert.That(result.Code, Is.EqualTo(BindingOverrideResultCode.AssetMismatch));
            Assert.That(action.bindings[0].overridePath, Is.Null.Or.Empty);
        }

        [Test]
        public void CustomPath_RejectsBlankConfiguration()
        {
            SetPrivateField("customPath", "");

            Assert.Throws<System.InvalidOperationException>(() => _ = m_Manager.DirectoryPath);
        }

        private void SetPrivateField(string name, object value)
        {
            typeof(RebindManager)
                .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(m_Manager, value);
        }
    }
}
