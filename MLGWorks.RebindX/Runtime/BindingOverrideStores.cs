using System;
using System.IO;
using Newtonsoft.Json;
using UnityEngine.InputSystem;

namespace MLGWorks.RebindX.Runtime
{
    public interface IBindingOverrideStore
    {
        void Save(InputActionAsset actionAsset);
        void Load(InputActionAsset actionAsset);
    }

    public sealed class InMemoryBindingOverrideStore : IBindingOverrideStore
    {
        private string m_OverridesJson;

        public void Save(InputActionAsset actionAsset)
        {
            if (actionAsset == null)
                throw new ArgumentNullException(nameof(actionAsset));
            m_OverridesJson = actionAsset.SaveBindingOverridesAsJson();
        }

        public void Load(InputActionAsset actionAsset)
        {
            if (actionAsset == null)
                throw new ArgumentNullException(nameof(actionAsset));
            if (!string.IsNullOrWhiteSpace(m_OverridesJson))
                actionAsset.LoadBindingOverridesFromJson(m_OverridesJson);
        }
    }

    public sealed class JsonBindingOverrideStore : IBindingOverrideStore
    {
        private readonly IRebindPathProvider m_PathProvider;

        public JsonBindingOverrideStore(IRebindPathProvider pathProvider)
        {
            m_PathProvider = pathProvider ?? throw new ArgumentNullException(nameof(pathProvider));
        }

        public void Save(InputActionAsset actionAsset)
        {
            if (actionAsset == null)
                throw new ArgumentNullException(nameof(actionAsset));

            var filePath = m_PathProvider.FilePath;
            var directoryPath = Path.GetDirectoryName(filePath);
            if (string.IsNullOrEmpty(directoryPath))
                throw new InvalidOperationException("The rebind file path does not contain a directory.");

            Directory.CreateDirectory(directoryPath);
            var json = actionAsset.SaveBindingOverridesAsJson();
            var jsonObject = JsonConvert.DeserializeObject(json);
            var formattedJson = jsonObject == null
                ? string.Empty
                : JsonConvert.SerializeObject(jsonObject, Formatting.Indented);

            var temporaryPath = filePath + ".tmp";
            File.WriteAllText(temporaryPath, formattedJson);
            if (File.Exists(filePath))
                File.Replace(temporaryPath, filePath, null);
            else
                File.Move(temporaryPath, filePath);
        }

        public void Load(InputActionAsset actionAsset)
        {
            if (actionAsset == null)
                throw new ArgumentNullException(nameof(actionAsset));

            var filePath = m_PathProvider.FilePath;
            if (!File.Exists(filePath))
                return;

            actionAsset.LoadBindingOverridesFromJson(File.ReadAllText(filePath));
        }
    }
}
