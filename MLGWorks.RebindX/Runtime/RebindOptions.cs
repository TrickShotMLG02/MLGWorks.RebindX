using System;
using System.Collections.Generic;

namespace MLGWorks.RebindX.Runtime
{
    public enum DuplicateBindingPolicy
    {
        Reject,
        Allow
    }

    public enum DuplicateBindingResolution
    {
        Reject,
        Allow,
        Replace,
        Swap
    }

    public enum DuplicateBindingScope
    {
        SameActionMap,
        EntireAsset
    }

    /// <summary>
    /// Controls how an interactive rebind selects controls and handles conflicts.
    /// Empty collections and optional values retain the Input System defaults.
    /// </summary>
    [Serializable]
    public sealed class RebindOptions
    {
        public string bindingGroup;
        public List<string> controlPathsToMatch = new List<string>();
        public List<string> controlPathsToExclude = new List<string>();
        public string cancelControlPath = "<Keyboard>/escape";
        public string expectedControlType;
        public float minimumMagnitude;
        public float timeoutSeconds;
        public bool cancelWhenDeviceIsRemoved = true;
        public DuplicateBindingPolicy duplicateBindingPolicy = DuplicateBindingPolicy.Reject;
        public DuplicateBindingResolution duplicateBindingResolution = DuplicateBindingResolution.Reject;
        public DuplicateBindingScope duplicateBindingScope = DuplicateBindingScope.EntireAsset;
        public int maximumDuplicateRetries = 3;

        public RebindOptions Clone()
        {
            return new RebindOptions
            {
                bindingGroup = bindingGroup,
                controlPathsToMatch = new List<string>(controlPathsToMatch ?? new List<string>()),
                controlPathsToExclude = new List<string>(controlPathsToExclude ?? new List<string>()),
                cancelControlPath = cancelControlPath,
                expectedControlType = expectedControlType,
                minimumMagnitude = minimumMagnitude,
                timeoutSeconds = timeoutSeconds,
                cancelWhenDeviceIsRemoved = cancelWhenDeviceIsRemoved,
                duplicateBindingPolicy = duplicateBindingPolicy,
                duplicateBindingResolution = duplicateBindingResolution,
                duplicateBindingScope = duplicateBindingScope,
                maximumDuplicateRetries = maximumDuplicateRetries
            };
        }
    }
}
