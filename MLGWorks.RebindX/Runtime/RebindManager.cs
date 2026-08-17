using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MLGWorks.RebindX.Runtime
{
    public enum FileLocationType
    {
        PersistentDataPath,
        DataPath,
        Custom
    }

    [DefaultExecutionOrder(-1000)]
    public class RebindManager : MonoBehaviour, IBindingOverrideService
    {
        [Header("Rebinds File Location")]
        [SerializeField] private FileLocationType pathType = FileLocationType.PersistentDataPath;
        [SerializeField] private string relativePath = "Configs";
        [SerializeField] private string customPath = "";
        [SerializeField] private string fileName = "rebinds.json";
        [Tooltip("Optional stable identifier for this input profile. If empty, an identifier is generated from the asset structure.")]
        [SerializeField] private string profileId = "";
        [SerializeField] private List<RebindProfile> profiles = new List<RebindProfile>();
        [SerializeField] private InputActionAsset actionAsset;

        private PlayerInputControls _controls;
        private InputActionAsset _actionAsset;
        private IInputActionAssetProvider m_AssetProvider;
        private IRebindPathProvider m_PathProvider;
        private IBindingOverrideStore m_OverrideStore;
        private bool m_UseProfileFiles;
        public PlayerInputControls Controls => _controls;
        public InputActionAsset ActionAsset => _actionAsset;

        /// <summary>
        /// Gets the stable persistence profile identifier used by this manager.
        /// Empty values use the generated asset identity.
        /// </summary>
        public string ProfileId => profileId;
        public IReadOnlyList<RebindProfile> Profiles => profiles;

        public IRebindPathProvider PathProvider
        {
            get => m_PathProvider ?? new FileSystemRebindPathProvider(
                pathType, relativePath, customPath, fileName);
            set
            {
                m_PathProvider = value ?? throw new ArgumentNullException(nameof(value));
                if (m_OverrideStore is JsonBindingOverrideStore)
                    m_OverrideStore = null;
            }
        }

        public IBindingOverrideStore OverrideStore
        {
            get => m_OverrideStore ??= new JsonBindingOverrideStore(
                m_UseProfileFiles && !string.IsNullOrWhiteSpace(profileId)
                    ? new ProfileRebindPathProvider(PathProvider, profileId)
                    : PathProvider,
                profileId);
            set => m_OverrideStore = value ?? throw new ArgumentNullException(nameof(value));
        }

        public string DirectoryPath
        {
            get
            {
                return ActivePathProvider.DirectoryPath;
            }
        }

        public string FilePath
        {
            get
            {
                return ActivePathProvider.FilePath;
            }
        }

        private IRebindPathProvider ActivePathProvider =>
            m_UseProfileFiles && !string.IsNullOrWhiteSpace(profileId)
                ? new ProfileRebindPathProvider(PathProvider, profileId)
                : PathProvider;

        protected virtual void Awake()
        {
            m_AssetProvider = null;
            m_PathProvider = null;
            m_OverrideStore = null;
            RebindProfileMetadataStore.Load(PathProvider, profiles);

            if (actionAsset != null)
            {
                _actionAsset = actionAsset;
                m_AssetProvider = new InputActionAssetProvider(_actionAsset);
                m_AssetProvider.Enable();
            }
            else
            {
                _controls = new PlayerInputControls();
                m_AssetProvider = new GeneratedControlsProvider(_controls);
                _actionAsset = m_AssetProvider.Asset;
                m_AssetProvider.Enable();
            }

            LoadRebinds();
        }

        /// <summary>
        /// Changes the persistence profile used by this manager and reloads its asset.
        /// This enables profile switching without relying on a global manager.
        /// </summary>
        public void SetProfileId(string value)
        {
            profileId = value ?? string.Empty;
            m_OverrideStore = null;
            if (_actionAsset != null)
                LoadRebinds();
        }

        public BindingOverrideResult CreateProfile(string id, string displayName = null, bool activate = true)
        {
            if (!IsValidProfileId(id))
                return BindingOverrideResult.Failure(BindingOverrideResultCode.InvalidPath, "Profile IDs must contain at least one valid file-name character.");
            id = id.Trim();
            if (profiles.Exists(profile => string.Equals(profile.Id, id, StringComparison.OrdinalIgnoreCase)))
                return BindingOverrideResult.Failure(BindingOverrideResultCode.IoFailure, "A profile with that ID already exists.");

            profiles.Add(new RebindProfile(id, displayName));
            var metadataResult = RebindProfileMetadataStore.Save(PathProvider, profiles);
            if (!metadataResult.Succeeded)
            {
                profiles.RemoveAt(profiles.Count - 1);
                return metadataResult;
            }
            return activate ? SwitchProfile(id) : BindingOverrideResult.Success("Profile created.");
        }

        public BindingOverrideResult SwitchProfile(string id)
        {
            if (!IsValidProfileId(id))
                return BindingOverrideResult.Failure(BindingOverrideResultCode.InvalidPath, "A valid profile ID is required.");
            id = id.Trim();
            var profile = profiles.Find(candidate => string.Equals(candidate.Id, id, StringComparison.OrdinalIgnoreCase));
            if (profile == null)
                return BindingOverrideResult.Failure(BindingOverrideResultCode.NoData, "The requested profile does not exist.");
            if (string.Equals(profileId, id, StringComparison.Ordinal))
                return BindingOverrideResult.Success("Profile already active.");

            if (_actionAsset != null && !string.IsNullOrWhiteSpace(profileId))
                SaveRebinds();
            _actionAsset?.RemoveAllBindingOverrides();
            profileId = id;
            m_UseProfileFiles = true;
            m_OverrideStore = null;
            return LoadRebinds();
        }

        public BindingOverrideResult RenameProfile(string id, string displayName)
        {
            var profile = profiles.Find(candidate => string.Equals(candidate.Id, id?.Trim(), StringComparison.OrdinalIgnoreCase));
            if (profile == null)
                return BindingOverrideResult.Failure(BindingOverrideResultCode.NoData, "The requested profile does not exist.");
            var previousName = profile.DisplayName;
            profile.Rename(displayName);
            var result = RebindProfileMetadataStore.Save(PathProvider, profiles);
            if (!result.Succeeded)
                profile.Rename(previousName);
            return result.Succeeded ? BindingOverrideResult.Success("Profile renamed.") : result;
        }

        public BindingOverrideResult DeleteProfile(string id)
        {
            var profile = profiles.Find(candidate => string.Equals(candidate.Id, id?.Trim(), StringComparison.OrdinalIgnoreCase));
            if (profile == null)
                return BindingOverrideResult.Failure(BindingOverrideResultCode.NoData, "The requested profile does not exist.");
            if (string.Equals(profileId, profile.Id, StringComparison.Ordinal))
                return BindingOverrideResult.Failure(BindingOverrideResultCode.IoFailure, "The active profile cannot be deleted.");

            var store = new JsonBindingOverrideStore(new ProfileRebindPathProvider(PathProvider, profile.Id), profile.Id);
            var result = store.Delete();
            if (!result.Succeeded)
                return result;
            profiles.Remove(profile);
            var metadataResult = RebindProfileMetadataStore.Save(PathProvider, profiles);
            if (!metadataResult.Succeeded)
                profiles.Add(profile);
            return metadataResult.Succeeded ? BindingOverrideResult.Success("Profile deleted.") : metadataResult;
        }

        private static bool IsValidProfileId(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return false;
            foreach (var character in id.Trim())
                if (Array.IndexOf(Path.GetInvalidFileNameChars(), character) >= 0 || char.IsWhiteSpace(character))
                    return false;
            return true;
        }

        protected virtual void OnDestroy()
        {
            m_AssetProvider?.Dispose();
            m_AssetProvider = null;
            _controls = null;
            _actionAsset = null;
        }

        public void SetControls(PlayerInputControls controls)
        {
            if (controls == null)
            {
                throw new ArgumentNullException(nameof(controls));
            }

            if (_controls == controls)
            {
                return;
            }

            m_AssetProvider?.Dispose();
            m_AssetProvider = new GeneratedControlsProvider(controls);

            _controls = controls;
            _actionAsset = m_AssetProvider.Asset;
            m_AssetProvider.Enable();
            LoadRebinds();
        }

        public void SetActionAsset(InputActionAsset asset)
        {
            if (asset == null)
            {
                throw new ArgumentNullException(nameof(asset));
            }

            if (_actionAsset == asset)
            {
                return;
            }

            m_AssetProvider?.Dispose();
            _controls = null;

            _actionAsset = asset;
            m_AssetProvider = new InputActionAssetProvider(asset);
            m_AssetProvider.Enable();
            LoadRebinds();
        }

        public BindingOverrideResult SaveRebinds()
        {
            if (_actionAsset == null)
            {
                Debug.LogError("Cannot save rebinds before the input controls have been initialized.", this);
                return BindingOverrideResult.Failure(BindingOverrideResultCode.InvalidAsset, "The input action asset has not been initialized.");
            }

            try
            {
                var result = OverrideStore.Save(_actionAsset);
                if (result.Succeeded)
                    Debug.Log("Input Config File saved to " + FilePath);
                else
                    Debug.LogError("Failed to save Input Config File: " + result.Message);
                return result;
            }
            catch (Exception e)
            {
                Debug.LogError("Failed to save Input Config File: " + e.Message);
                return BindingOverrideResult.Failure(BindingOverrideResultCode.IoFailure, e.Message, e);
            }
        }

        public BindingOverrideResult LoadRebinds()
        {
            if (_actionAsset == null)
            {
                Debug.LogError("Cannot load rebinds before the input controls have been initialized.", this);
                return BindingOverrideResult.Failure(BindingOverrideResultCode.InvalidAsset, "The input action asset has not been initialized.");
            }

            try
            {
                var result = OverrideStore.Load(_actionAsset);
                if (result.Code == BindingOverrideResultCode.NoData)
                    Debug.LogWarning("Input Config File not found. " + FilePath);
                else if (result.Succeeded)
                    Debug.Log("Input Config File loaded successfully.", this);
                else
                    Debug.LogError("Failed to load Input Config File: " + result.Message);
                return result;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to load Input Config File: {ex.Message}");
                return BindingOverrideResult.Failure(BindingOverrideResultCode.IoFailure, ex.Message, ex);
            }
        }

        public BindingOverrideResult ResetRebinds()
        {
            if (_actionAsset == null)
            {
                Debug.LogError("Cannot reset rebinds before the input controls have been initialized.", this);
                return BindingOverrideResult.Failure(BindingOverrideResultCode.InvalidAsset, "The input action asset has not been initialized.");
            }

            _actionAsset.RemoveAllBindingOverrides();
            var result = OverrideStore.Delete();
            if (!result.Succeeded)
                Debug.LogError("Failed to reset Input Config File: " + result.Message);
            return result;
        }
    }
}
