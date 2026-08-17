using System;
using System.IO;
using UnityEngine;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace MLGWorks.RebindX.Runtime
{
    [Serializable]
    public sealed class RebindProfile
    {
        [SerializeField] private string id;
        [SerializeField] private string displayName;

        public RebindProfile(string id, string displayName = null)
        {
            this.id = id;
            this.displayName = string.IsNullOrWhiteSpace(displayName) ? id : displayName;
        }

        public string Id => id;
        public string DisplayName => displayName;

        internal void Rename(string value)
        {
            displayName = string.IsNullOrWhiteSpace(value) ? id : value.Trim();
        }
    }

    public sealed class ProfileRebindPathProvider : IRebindPathProvider
    {
        private readonly IRebindPathProvider m_BaseProvider;
        private readonly string m_ProfileId;

        public ProfileRebindPathProvider(IRebindPathProvider baseProvider, string profileId)
        {
            m_BaseProvider = baseProvider ?? throw new ArgumentNullException(nameof(baseProvider));
            if (string.IsNullOrWhiteSpace(profileId))
                throw new ArgumentException("A profile ID is required.", nameof(profileId));
            m_ProfileId = profileId.Trim();
        }

        public string DirectoryPath => m_BaseProvider.DirectoryPath;

        public string FilePath
        {
            get
            {
                var basePath = m_BaseProvider.FilePath;
                var directory = Path.GetDirectoryName(basePath);
                var extension = Path.GetExtension(basePath);
                var stem = Path.GetFileNameWithoutExtension(basePath);
                var safeId = m_ProfileId;
                foreach (var invalid in Path.GetInvalidFileNameChars())
                    safeId = safeId.Replace(invalid, '_');
                return Path.Combine(directory ?? string.Empty, stem + "." + safeId + extension);
            }
        }
    }

    internal static class RebindProfileMetadataStore
    {
        [Serializable]
        private sealed class ProfileRecord
        {
            public string id;
            public string displayName;
        }

        public static BindingOverrideResult Save(IRebindPathProvider provider, IReadOnlyList<RebindProfile> profiles)
        {
            try
            {
                var path = provider.FilePath + ".profiles";
                var records = new List<ProfileRecord>();
                foreach (var profile in profiles)
                    records.Add(new ProfileRecord { id = profile.Id, displayName = profile.DisplayName });
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, JsonConvert.SerializeObject(records, Formatting.Indented));
                return BindingOverrideResult.Success("Profile metadata saved.");
            }
            catch (Exception exception)
            {
                return BindingOverrideResult.Failure(BindingOverrideResultCode.IoFailure, exception.Message, exception);
            }
        }

        public static BindingOverrideResult Load(IRebindPathProvider provider, List<RebindProfile> profiles)
        {
            try
            {
                var path = provider.FilePath + ".profiles";
                if (!File.Exists(path))
                    return BindingOverrideResult.NoData("No profile metadata exists.");
                var records = JsonConvert.DeserializeObject<List<ProfileRecord>>(File.ReadAllText(path));
                profiles.Clear();
                if (records != null)
                    foreach (var record in records)
                        if (record != null && !string.IsNullOrWhiteSpace(record.id))
                            profiles.Add(new RebindProfile(record.id, record.displayName));
                return BindingOverrideResult.Success("Profile metadata loaded.");
            }
            catch (Exception exception)
            {
                return BindingOverrideResult.Failure(BindingOverrideResultCode.CorruptData, exception.Message, exception);
            }
        }
    }
}
