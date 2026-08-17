using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine.InputSystem;

namespace MLGWorks.RebindX.Runtime
{
    public interface IBindingOverrideService
    {
        InputActionAsset ActionAsset { get; }
        BindingOverrideResult SaveRebinds();
        BindingOverrideResult LoadRebinds();
        BindingOverrideResult ResetRebinds();
    }

    public enum BindingOverrideResultCode
    {
        Success,
        NoData,
        InvalidAsset,
        InvalidPath,
        AssetMismatch,
        UnsupportedVersion,
        CorruptData,
        IoFailure
    }

    public sealed class BindingOverrideResult
    {
        private BindingOverrideResult(BindingOverrideResultCode code, string message, Exception exception = null)
        {
            Code = code;
            Message = message ?? string.Empty;
            Exception = exception;
        }

        public BindingOverrideResultCode Code { get; }
        public string Message { get; }
        public Exception Exception { get; }
        public bool Succeeded => Code == BindingOverrideResultCode.Success || Code == BindingOverrideResultCode.NoData;

        public static BindingOverrideResult Success(string message = "") =>
            new BindingOverrideResult(BindingOverrideResultCode.Success, message);
        public static BindingOverrideResult NoData(string message) =>
            new BindingOverrideResult(BindingOverrideResultCode.NoData, message);
        public static BindingOverrideResult Failure(BindingOverrideResultCode code, string message, Exception exception = null) =>
            new BindingOverrideResult(code, message, exception);
    }

    public interface IBindingOverrideStore
    {
        BindingOverrideResult Save(InputActionAsset actionAsset);
        BindingOverrideResult Load(InputActionAsset actionAsset);
        BindingOverrideResult Delete();
    }

    public sealed class InMemoryBindingOverrideStore : IBindingOverrideStore
    {
        private string m_OverridesJson;

        public BindingOverrideResult Save(InputActionAsset actionAsset)
        {
            if (actionAsset == null)
                throw new ArgumentNullException(nameof(actionAsset));
            m_OverridesJson = actionAsset.SaveBindingOverridesAsJson();
            return BindingOverrideResult.Success("Binding overrides saved in memory.");
        }

        public BindingOverrideResult Load(InputActionAsset actionAsset)
        {
            if (actionAsset == null)
                throw new ArgumentNullException(nameof(actionAsset));
            if (!string.IsNullOrWhiteSpace(m_OverridesJson))
            {
                actionAsset.LoadBindingOverridesFromJson(m_OverridesJson);
                return BindingOverrideResult.Success("Binding overrides loaded from memory.");
            }
            return BindingOverrideResult.NoData("No in-memory binding overrides exist.");
        }

        public BindingOverrideResult Delete()
        {
            m_OverridesJson = null;
            return BindingOverrideResult.Success("In-memory binding overrides reset.");
        }
    }

    public sealed class JsonBindingOverrideStore : IBindingOverrideStore
    {
        public const int CurrentVersion = 1;
        private readonly IRebindPathProvider m_PathProvider;
        private readonly string m_ProfileId;

        public JsonBindingOverrideStore(IRebindPathProvider pathProvider, string profileId = null)
        {
            m_PathProvider = pathProvider ?? throw new ArgumentNullException(nameof(pathProvider));
            m_ProfileId = profileId;
        }

        public BindingOverrideResult Save(InputActionAsset actionAsset)
        {
            if (actionAsset == null)
                throw new ArgumentNullException(nameof(actionAsset));

            try
            {
                var filePath = m_PathProvider.FilePath;
                var directoryPath = Path.GetDirectoryName(filePath);
                if (string.IsNullOrEmpty(directoryPath))
                    return BindingOverrideResult.Failure(BindingOverrideResultCode.InvalidPath, "The rebind file path does not contain a directory.");

                Directory.CreateDirectory(directoryPath);
                var envelope = new BindingOverrideFile
                {
                    version = CurrentVersion,
                    assetId = GetAssetId(actionAsset, m_ProfileId),
                    overrides = actionAsset.SaveBindingOverridesAsJson()
                };
                if (string.IsNullOrWhiteSpace(envelope.overrides))
                    envelope.overrides = "[]";
                var formattedJson = JsonConvert.SerializeObject(envelope, Formatting.Indented);
                var temporaryPath = filePath + ".tmp";
                try
                {
                    File.WriteAllText(temporaryPath, formattedJson);
                    if (File.Exists(filePath))
                        File.Replace(temporaryPath, filePath, null);
                    else
                        File.Move(temporaryPath, filePath);
                }
                finally
                {
                    if (File.Exists(temporaryPath))
                        File.Delete(temporaryPath);
                }
                return BindingOverrideResult.Success("Binding overrides saved.");
            }
            catch (Exception exception)
            {
                return BindingOverrideResult.Failure(BindingOverrideResultCode.IoFailure, exception.Message, exception);
            }
        }

        public BindingOverrideResult Load(InputActionAsset actionAsset)
        {
            if (actionAsset == null)
                throw new ArgumentNullException(nameof(actionAsset));

            try
            {
                var filePath = m_PathProvider.FilePath;
                if (!File.Exists(filePath))
                    return BindingOverrideResult.NoData("No binding override file exists.");

                var root = JToken.Parse(File.ReadAllText(filePath));
                if (root.Type != JTokenType.Object)
                    return QuarantineCorruptFile(filePath, "The binding override file is not a versioned object.");

                var overridesToken = root["overrides"];
                var envelope = new BindingOverrideFile
                {
                    version = root["version"]?.Value<int>() ?? 0,
                    assetId = root["assetId"]?.Value<string>(),
                    overrides = overridesToken == null
                        ? null
                        : overridesToken.Type == JTokenType.String
                            ? overridesToken.Value<string>()
                            : overridesToken.ToString(Formatting.None)
                };
                if (envelope == null || envelope.version <= 0 || string.IsNullOrWhiteSpace(envelope.assetId) || envelope.overrides == null)
                    return QuarantineCorruptFile(filePath, "The binding override file is missing required fields.");
                if (envelope.version != CurrentVersion)
                    return BindingOverrideResult.Failure(BindingOverrideResultCode.UnsupportedVersion, $"Unsupported binding override version {envelope.version}.");

                var expectedAssetId = GetAssetId(actionAsset, m_ProfileId);
                if (!string.Equals(envelope.assetId, expectedAssetId, StringComparison.Ordinal))
                    return BindingOverrideResult.Failure(BindingOverrideResultCode.AssetMismatch, "Binding overrides belong to a different input asset or profile.");

                actionAsset.LoadBindingOverridesFromJson(envelope.overrides);
                return BindingOverrideResult.Success("Binding overrides loaded.");
            }
            catch (JsonException exception)
            {
                return QuarantineCorruptFile(m_PathProvider.FilePath, exception.Message, exception);
            }
            catch (Exception exception)
            {
                return BindingOverrideResult.Failure(BindingOverrideResultCode.IoFailure, exception.Message, exception);
            }
        }

        public BindingOverrideResult Delete()
        {
            try
            {
                var filePath = m_PathProvider.FilePath;
                if (File.Exists(filePath))
                    File.Delete(filePath);
                return BindingOverrideResult.Success("Binding override file deleted.");
            }
            catch (Exception exception)
            {
                return BindingOverrideResult.Failure(BindingOverrideResultCode.IoFailure, exception.Message, exception);
            }
        }

        public static string GetAssetId(InputActionAsset actionAsset, string profileId = null)
        {
            if (actionAsset == null)
                throw new ArgumentNullException(nameof(actionAsset));
            if (!string.IsNullOrWhiteSpace(profileId))
                return "profile:" + profileId.Trim();

            var builder = new StringBuilder();
            builder.Append(actionAsset.name).Append('|');
            foreach (var map in actionAsset.actionMaps)
            {
                builder.Append(map.id).Append(':').Append(map.name).Append('|');
                foreach (var action in map.actions)
                {
                    builder.Append(action.id).Append(':').Append(action.name).Append(':').Append(action.type).Append('|');
                    foreach (var binding in action.bindings)
                    {
                        builder.Append(binding.id).Append(':').Append(binding.name).Append(':')
                            .Append(binding.path).Append(':').Append(binding.action).Append(':')
                            .Append(binding.groups).Append(':').Append(binding.isComposite).Append(':')
                            .Append(binding.isPartOfComposite).Append('|');
                    }
                }
            }

            using (var sha = SHA256.Create())
            {
                var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(builder.ToString()));
                return "asset:" + BitConverter.ToString(bytes).Replace("-", string.Empty).ToLowerInvariant();
            }
        }

        private static BindingOverrideResult QuarantineCorruptFile(string filePath, string message, Exception exception = null)
        {
            try
            {
                var quarantinePath = filePath + ".corrupt-" + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff") + ".json";
                File.Move(filePath, quarantinePath);
                return BindingOverrideResult.Failure(BindingOverrideResultCode.CorruptData, message + " The file was moved to " + quarantinePath + ".", exception);
            }
            catch (Exception quarantineException)
            {
                return BindingOverrideResult.Failure(BindingOverrideResultCode.CorruptData, message + " The corrupt file could not be quarantined: " + quarantineException.Message, exception ?? quarantineException);
            }
        }

        [Serializable]
        private sealed class BindingOverrideFile
        {
            public int version;
            public string assetId;
            public string overrides;
        }
    }
}
