using System;
using System.IO;
using UnityEngine;

namespace MLGWorks.RebindX.Runtime
{
    public interface IRebindPathProvider
    {
        string DirectoryPath { get; }
        string FilePath { get; }
    }

    public sealed class FileSystemRebindPathProvider : IRebindPathProvider
    {
        private readonly FileLocationType m_PathType;
        private readonly string m_RelativePath;
        private readonly string m_CustomPath;
        private readonly string m_FileName;

        public FileSystemRebindPathProvider(
            FileLocationType pathType,
            string relativePath,
            string customPath,
            string fileName)
        {
            m_PathType = pathType;
            m_RelativePath = relativePath ?? string.Empty;
            m_CustomPath = customPath ?? string.Empty;
            m_FileName = fileName ?? string.Empty;
        }

        public string DirectoryPath
        {
            get
            {
                switch (m_PathType)
                {
                    case FileLocationType.PersistentDataPath:
                        return Path.Combine(Application.persistentDataPath, m_RelativePath);
                    case FileLocationType.DataPath:
                        return Path.Combine(Application.dataPath, m_RelativePath);
                    case FileLocationType.Custom:
                        if (string.IsNullOrWhiteSpace(m_CustomPath))
                            throw new InvalidOperationException("A custom rebind path must be configured.");
                        return m_CustomPath;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(m_PathType), m_PathType, "Invalid rebind path type.");
                }
            }
        }

        public string FilePath
        {
            get
            {
                if (string.IsNullOrWhiteSpace(m_FileName))
                    throw new InvalidOperationException("A rebind file name must be configured.");
                return Path.Combine(DirectoryPath, m_FileName);
            }
        }
    }
}
