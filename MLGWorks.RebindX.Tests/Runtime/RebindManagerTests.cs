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
            SetPrivateField("m_PathProvider", null);
            SetPrivateField("m_OverrideStore", null);

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
        public void MultipleManagers_CanOwnDifferentAssetsAndProfiles()
        {
            var secondObject = new GameObject("Second RebindManager Test");
            var secondManager = secondObject.AddComponent<RebindManager>();
            var secondAsset = ScriptableObject.CreateInstance<InputActionAsset>();
            secondAsset.AddActionMap("Menus").AddAction("Pause", InputActionType.Button,
                binding: "<Keyboard>/escape");

            ConfigureManager(secondManager, "profile-b", "bindings-b.json");
            secondManager.SetActionAsset(secondAsset);
            m_Manager.SetProfileId("profile-a");
            secondManager.SetProfileId("profile-b");

            var firstAction = m_Asset.FindAction("Gameplay/Jump");
            var secondAction = secondAsset.FindAction("Menus/Pause");
            firstAction.ApplyBindingOverride(0, "<Keyboard>/enter");
            secondAction.ApplyBindingOverride(0, "<Keyboard>/tab");

            Assert.That(m_Manager.SaveRebinds().Succeeded, Is.True);
            Assert.That(secondManager.SaveRebinds().Succeeded, Is.True);

            firstAction.RemoveBindingOverride(0);
            secondAction.RemoveBindingOverride(0);
            Assert.That(m_Manager.LoadRebinds().Succeeded, Is.True);
            Assert.That(secondManager.LoadRebinds().Succeeded, Is.True);

            Assert.That(firstAction.bindings[0].overridePath, Is.EqualTo("<Keyboard>/enter"));
            Assert.That(secondAction.bindings[0].overridePath, Is.EqualTo("<Keyboard>/tab"));

            Object.DestroyImmediate(secondObject);
            Object.DestroyImmediate(secondAsset);
        }

        [Test]
        public void SetProfileId_UsesNewProfileWithoutChangingManagedAsset()
        {
            var asset = m_Manager.ActionAsset;
            m_Manager.SetProfileId("alternate");

            Assert.That(m_Manager.ProfileId, Is.EqualTo("alternate"));
            Assert.That(m_Manager.ActionAsset, Is.SameAs(asset));
        }

        [Test]
        public void CustomPath_RejectsBlankConfiguration()
        {
            SetPrivateField("customPath", "");

            Assert.Throws<System.InvalidOperationException>(() => _ = m_Manager.DirectoryPath);
        }

        [Test]
        public void CreateProfile_AddsInactiveProfileWithDisplayName()
        {
            var result = m_Manager.CreateProfile("keyboard", "Keyboard Layout", false);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(m_Manager.Profiles, Has.Count.EqualTo(1));
            Assert.That(m_Manager.Profiles[0].Id, Is.EqualTo("keyboard"));
            Assert.That(m_Manager.Profiles[0].DisplayName, Is.EqualTo("Keyboard Layout"));
            Assert.That(m_Manager.ProfileId, Is.Empty);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        [TestCase("profile name")]
        [TestCase("profile/name")]
        public void CreateProfile_RejectsInvalidIds(string id)
        {
            var result = m_Manager.CreateProfile(id, activate: false);

            Assert.That(result.Code, Is.EqualTo(BindingOverrideResultCode.InvalidPath));
            Assert.That(m_Manager.Profiles, Is.Empty);
        }

        [Test]
        public void CreateProfile_RejectsDuplicateIdsCaseInsensitively()
        {
            m_Manager.CreateProfile("Keyboard", activate: false);

            var result = m_Manager.CreateProfile("keyboard", activate: false);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(m_Manager.Profiles, Has.Count.EqualTo(1));
        }

        [Test]
        public void CreateProfile_ActivatesProfileAndUsesProfileSpecificFile()
        {
            var result = m_Manager.CreateProfile("keyboard");

            Assert.That(result.Succeeded, Is.True);
            Assert.That(m_Manager.ProfileId, Is.EqualTo("keyboard"));
            Assert.That(m_Manager.FilePath, Does.Contain(".keyboard."));
        }

        [Test]
        public void SwitchProfile_RejectsUnknownProfileWithoutChangingActiveProfile()
        {
            m_Manager.CreateProfile("keyboard");

            var result = m_Manager.SwitchProfile("gamepad");

            Assert.That(result.Code, Is.EqualTo(BindingOverrideResultCode.NoData));
            Assert.That(m_Manager.ProfileId, Is.EqualTo("keyboard"));
        }

        [Test]
        public void SwitchProfile_PersistsAndRestoresIndependentOverrides()
        {
            m_Manager.CreateProfile("keyboard");
            var action = m_Asset.FindAction("Gameplay/Jump");
            action.ApplyBindingOverride(0, "<Keyboard>/enter");
            m_Manager.SaveRebinds();
            m_Manager.CreateProfile("gamepad");
            action.ApplyBindingOverride(0, "<Gamepad>/buttonSouth");
            m_Manager.SaveRebinds();

            var switchResult = m_Manager.SwitchProfile("keyboard");

            Assert.That(switchResult.Succeeded, Is.True);
            Assert.That(action.bindings[0].overridePath, Is.EqualTo("<Keyboard>/enter"));
            Assert.That(m_Manager.SwitchProfile("gamepad").Succeeded, Is.True);
            Assert.That(action.bindings[0].overridePath, Is.EqualTo("<Gamepad>/buttonSouth"));
        }

        [Test]
        public void SwitchProfile_ToCurrentProfileIsIdempotent()
        {
            m_Manager.CreateProfile("keyboard");

            var result = m_Manager.SwitchProfile("keyboard");

            Assert.That(result.Succeeded, Is.True);
            Assert.That(m_Manager.Profiles, Has.Count.EqualTo(1));
        }

        [Test]
        public void RenameProfile_ChangesDisplayNameOnly()
        {
            m_Manager.CreateProfile("keyboard", "Old", false);

            var result = m_Manager.RenameProfile("keyboard", "New");

            Assert.That(result.Succeeded, Is.True);
            Assert.That(m_Manager.Profiles[0].Id, Is.EqualTo("keyboard"));
            Assert.That(m_Manager.Profiles[0].DisplayName, Is.EqualTo("New"));
        }

        [Test]
        public void RenameProfile_BlankNameFallsBackToId()
        {
            m_Manager.CreateProfile("keyboard", "Old", false);

            m_Manager.RenameProfile("keyboard", " ");

            Assert.That(m_Manager.Profiles[0].DisplayName, Is.EqualTo("keyboard"));
        }

        [Test]
        public void RenameProfile_RejectsUnknownProfile()
        {
            var result = m_Manager.RenameProfile("missing", "Name");

            Assert.That(result.Code, Is.EqualTo(BindingOverrideResultCode.NoData));
        }

        [Test]
        public void DeleteProfile_RejectsActiveProfile()
        {
            m_Manager.CreateProfile("keyboard");

            var result = m_Manager.DeleteProfile("keyboard");

            Assert.That(result.Succeeded, Is.False);
            Assert.That(m_Manager.Profiles, Has.Count.EqualTo(1));
        }

        [Test]
        public void DeleteProfile_RemovesInactiveProfileAndItsFile()
        {
            m_Manager.CreateProfile("keyboard");
            var action = m_Asset.FindAction("Gameplay/Jump");
            action.ApplyBindingOverride(0, "<Keyboard>/enter");
            m_Manager.SaveRebinds();
            m_Manager.CreateProfile("gamepad", activate: false);
            m_Manager.SwitchProfile("keyboard");
            m_Manager.SwitchProfile("gamepad");
            var profilePath = m_Manager.FilePath;

            var result = m_Manager.DeleteProfile("keyboard");

            Assert.That(result.Succeeded, Is.True);
            Assert.That(m_Manager.Profiles, Has.Count.EqualTo(1));
            Assert.That(File.Exists(profilePath), Is.False);
        }

        [Test]
        public void ProfilePathProvider_SanitizesFileNameCharacters()
        {
            var provider = new ProfileRebindPathProvider(m_Manager.PathProvider, "profile:one");

            Assert.That(provider.FilePath, Does.Not.Contain("profile:one"));
            Assert.That(provider.FilePath, Does.Contain("profile_one"));
        }

        [Test]
        public void CreateProfile_PersistsProfileMetadata()
        {
            m_Manager.CreateProfile("keyboard", "Keyboard", false);

            Assert.That(File.Exists(m_Manager.FilePath + ".profiles"), Is.True);
            Assert.That(File.ReadAllText(m_Manager.FilePath + ".profiles"), Does.Contain("Keyboard"));
        }

        [Test]
        public void RenameProfile_UpdatesPersistedMetadata()
        {
            m_Manager.CreateProfile("keyboard", "Old", false);
            m_Manager.RenameProfile("keyboard", "New");

            var metadata = File.ReadAllText(m_Manager.FilePath + ".profiles");
            Assert.That(metadata, Does.Contain("New"));
            Assert.That(metadata, Does.Not.Contain("Old"));
        }

        [Test]
        public void DeleteProfile_UpdatesPersistedMetadata()
        {
            m_Manager.CreateProfile("keyboard", activate: false);
            m_Manager.CreateProfile("gamepad", activate: false);
            m_Manager.DeleteProfile("keyboard");

            var metadata = File.ReadAllText(m_Manager.FilePath + ".profiles");
            Assert.That(metadata, Does.Not.Contain("keyboard"));
            Assert.That(metadata, Does.Contain("gamepad"));
        }

        private void SetPrivateField(string name, object value)
        {
            SetPrivateField(m_Manager, name, value);
        }

        private void ConfigureManager(RebindManager manager, string profile, string file = "bindings.json")
        {
            SetPrivateField(manager, "pathType", FileLocationType.Custom);
            SetPrivateField(manager, "customPath", m_TestDirectory);
            SetPrivateField(manager, "fileName", file);
            SetPrivateField(manager, "profileId", profile);
            SetPrivateField(manager, "m_PathProvider", null);
            SetPrivateField(manager, "m_OverrideStore", null);
        }

        private static void SetPrivateField(RebindManager manager, string name, object value)
        {
            typeof(RebindManager)
                .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(manager, value);
        }
    }
}
