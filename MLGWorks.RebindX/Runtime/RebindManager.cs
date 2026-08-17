using MLGWorks.Utils.Patterns;
using System;
using System.IO;
using MLGWorks.Utils.Patterns.Singletons;
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
    public class RebindManager : Singleton<RebindManager>, IBindingOverrideService
    {
        [Header("Rebinds File Location")]
        [SerializeField] private FileLocationType pathType = FileLocationType.PersistentDataPath;
        [SerializeField] private string relativePath = "Configs";
        [SerializeField] private string customPath = "";
        [SerializeField] private string fileName = "rebinds.json";
        [SerializeField] private InputActionAsset actionAsset;

        private PlayerInputControls _controls;
        private InputActionAsset _actionAsset;
        private IInputActionAssetProvider m_AssetProvider;
        private IRebindPathProvider m_PathProvider;
        private IBindingOverrideStore m_OverrideStore;
        public PlayerInputControls Controls => _controls;
        public InputActionAsset ActionAsset => _actionAsset;

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
            get => m_OverrideStore ??= new JsonBindingOverrideStore(PathProvider);
            set => m_OverrideStore = value ?? throw new ArgumentNullException(nameof(value));
        }

        public string DirectoryPath
        {
            get
            {
                return PathProvider.DirectoryPath;
            }
        }

        public string FilePath
        {
            get
            {
                return PathProvider.FilePath;
            }
        }

        protected override void Awake()
        {
            base.Awake();

            // The base singleton destroys duplicate objects. Do not initialize a
            // duplicate's input asset before Unity removes it.
            if (Instance != this)
            {
                return;
            }

            m_AssetProvider = null;
            m_PathProvider = null;
            m_OverrideStore = null;

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

        public void SaveRebinds()
        {
            if (_actionAsset == null)
            {
                Debug.LogError("Cannot save rebinds before the input controls have been initialized.", this);
                return;
            }

            try
            {
                OverrideStore.Save(_actionAsset);
                Debug.Log("Input Config File saved to " + FilePath);
            }
            catch (Exception e)
            {
                Debug.LogError("Failed to save Input Config File: " + e.Message);
            }
        }

        public void LoadRebinds()
        {
            if (_actionAsset == null)
            {
                Debug.LogError("Cannot load rebinds before the input controls have been initialized.", this);
                return;
            }

            try
            {
                if (File.Exists(FilePath))
                {
                    OverrideStore.Load(_actionAsset);
                    Debug.Log("Input Config File loaded successfully.", this);
                }
                else
                {
                    Debug.LogWarning("Input Config File not found. " + FilePath);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to load Input Config File: {ex.Message}");
            }
        }
    }
}
