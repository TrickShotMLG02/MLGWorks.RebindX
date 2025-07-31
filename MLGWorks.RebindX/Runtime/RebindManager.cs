using MLGWorks.Utils.Patterns;
using Newtonsoft.Json;
using System;
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
    public class RebindManager : Singleton<RebindManager>
    {
        [Header("Rebinds File Location")]
        [SerializeField] private FileLocationType pathType = FileLocationType.PersistentDataPath;
        [SerializeField] private string relativePath = "Configs";
        [SerializeField] private string customPath = "";
        [SerializeField] private string fileName = "rebinds.json";

        private PlayerInputControls _controls;
        public PlayerInputControls Controls => _controls;

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
                        return Path.Combine(customPath);

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

        private void Awake()
        {
            _controls = new PlayerInputControls();
            _controls.Enable();

            LoadRebinds();
        }

        public void SetControls(PlayerInputControls controls)
        {
            _controls = controls;
        }

        public void SaveRebinds()
        {
            string rebinds = _controls.asset.SaveBindingOverridesAsJson();
            try
            {
                // Get directory from file path
                string directoryPath = Path.GetDirectoryName(FilePath);

                // Create directory if it doesn't exist
                if (!System.IO.Directory.Exists(directoryPath))
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
            string directoryPath = Path.GetDirectoryName(FilePath);

            try
            {
                // Check if file exists
                if (File.Exists(FilePath))
                {
                    // Read the file content and return it
                    string fileContent = File.ReadAllText(FilePath);
                    _controls.LoadBindingOverridesFromJson(fileContent);
                    Console.WriteLine("Input Config File loaded successfully.");
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
