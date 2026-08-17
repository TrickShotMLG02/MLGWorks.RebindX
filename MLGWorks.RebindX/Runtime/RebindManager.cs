using MLGWorks.Utils.Patterns;
using Newtonsoft.Json;
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
    public class RebindManager : Singleton<RebindManager>
    {
        [Header("Rebinds File Location")]
        [SerializeField] private FileLocationType pathType = FileLocationType.PersistentDataPath;
        [SerializeField] private string relativePath = "Configs";
        [SerializeField] private string customPath = "";
        [SerializeField] private string fileName = "rebinds.json";
        [SerializeField] private InputActionAsset actionAsset;

        private PlayerInputControls _controls;
        private InputActionAsset _actionAsset;
        public PlayerInputControls Controls => _controls;
        public InputActionAsset ActionAsset => _actionAsset;

        public string DirectoryPath
        {
            get
            {
                switch (pathType)
                {
                    case FileLocationType.PersistentDataPath:
                        return Path.Combine(Application.persistentDataPath, relativePath);

                    case FileLocationType.DataPath:
                        return Path.Combine(Application.dataPath, relativePath);

                    case FileLocationType.Custom:
                        if (string.IsNullOrWhiteSpace(customPath))
                        {
                            throw new InvalidOperationException("A custom rebind path must be configured.");
                        }

                        return customPath;

                    default:
                        throw new ArgumentException("Invalid Path");
                }
            }
        }

        public string FilePath
        {
            get
            {
                return Path.Combine(DirectoryPath, fileName);
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

            if (actionAsset != null)
            {
                _actionAsset = actionAsset;
                _actionAsset.Enable();
            }
            else
            {
                _controls = new PlayerInputControls();
                _actionAsset = _controls.asset;
                _controls.Enable();
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

            if (_controls != null)
            {
                _controls.Disable();
                _controls.Dispose();
            }

            _controls = controls;
            _actionAsset = controls.asset;
            _controls.Enable();
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

            if (_controls != null)
            {
                _controls.Disable();
                _controls.Dispose();
                _controls = null;
            }
            else
            {
                _actionAsset?.Disable();
            }

            _actionAsset = asset;
            _actionAsset.Enable();
            LoadRebinds();
        }

        public void SaveRebinds()
        {
            if (_actionAsset == null)
            {
                Debug.LogError("Cannot save rebinds before the input controls have been initialized.", this);
                return;
            }

            string rebinds = _actionAsset.SaveBindingOverridesAsJson();
            try
            {
                // Get directory from file path
                string directoryPath = Path.GetDirectoryName(FilePath);

                // Create directory if it doesn't exist
                if (string.IsNullOrEmpty(directoryPath))
                {
                    throw new InvalidOperationException("The rebind file path does not contain a directory.");
                }

                if (!Directory.Exists(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                }

                // Deserialize the JSON string to a dynamic object
                var jsonObject = JsonConvert.DeserializeObject(rebinds);

                string formattedJson = "";

                if (jsonObject != null)
                {
                    // Serialize it back to a formatted (indented) JSON string
                    formattedJson = JsonConvert.SerializeObject(jsonObject, Formatting.Indented);
                }

                // Write the input string to the file, overwriting if it exists
                File.WriteAllText(FilePath, formattedJson);
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
                // Check if file exists
                if (File.Exists(FilePath))
                {
                    // Read the file content and return it
                    string fileContent = File.ReadAllText(FilePath);
                    _actionAsset.LoadBindingOverridesFromJson(fileContent);
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
