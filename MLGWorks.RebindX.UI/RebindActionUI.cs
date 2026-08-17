//using Michsky.UI.Shift;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.SmartFormat.PersistentVariables;
using UnityEngine.Localization.Tables;

namespace MLGWorks.RebindX.Runtime
{
    /// <summary>
    /// A reusable component with a self-contained UI for rebinding a single action.
    /// </summary>

    public class RebindActionUI : MonoBehaviour
    {
        /// <summary>
        /// Reference to the action that is to be rebound.
        /// </summary>
        public InputActionReference actionReference
        {
            get => m_Action;
            set
            {
                m_Action = value;
                UpdateActionLabel();
                UpdateBindingDisplay();
            }
        }

        /// <summary>
        /// ID (in string form) of the binding that is to be rebound on the action.
        /// </summary>
        /// <seealso cref="InputBinding.id"/>
        public string bindingId
        {
            get => m_BindingId;
            set
            {
                m_BindingId = value;
                UpdateBindingDisplay();
            }
        }

        public InputBinding.DisplayStringOptions displayStringOptions
        {
            get => m_DisplayStringOptions;
            set
            {
                m_DisplayStringOptions = value;
                UpdateBindingDisplay();
            }
        }

        /// <summary>
        /// Text component that receives the name of the action. Optional.
        /// </summary>
        public TMPro.TextMeshProUGUI actionLabel
        {
            get => m_ActionLabel;
            set
            {
                m_ActionLabel = value;
                UpdateActionLabel();
            }
        }

        /// <summary>
        /// Text component that receives the display string of the binding. Can be <c>null</c> in which
        /// case the component entirely relies on <see cref="updateBindingUIEvent"/>.
        /// </summary>
        public TMPro.TextMeshProUGUI bindingText
        {
            get => m_BindingText;
            set
            {
                m_BindingText = value;
                UpdateBindingDisplay();
            }
        }

        /// <summary>
        /// Optional text component that receives a text prompt when waiting for a control to be actuated.
        /// </summary>
        /// <seealso cref="startRebindEvent"/>
        /// <seealso cref="rebindOverlay"/>
        public TMPro.TextMeshProUGUI rebindPrompt
        {
            get => m_RebindText;
            set => m_RebindText = value;
        }

        /// <summary>
        /// Optional UI that is activated when an interactive rebind is started and deactivated when the rebind
        /// is finished. This is normally used to display an overlay over the current UI while the system is
        /// waiting for a control to be actuated.
        /// </summary>
        /// <remarks>
        /// If neither <see cref="rebindPrompt"/> nor <c>rebindOverlay</c> is set, the component will temporarily
        /// replaced the <see cref="bindingText"/> (if not <c>null</c>) with <c>"Waiting..."</c>.
        /// </remarks>
        /// <seealso cref="startRebindEvent"/>
        /// <seealso cref="rebindPrompt"/>
        public LocalizedString titleLocalization
        {
            get => m_titleLocalization;
            set => m_titleLocalization = value;
        }

        public LocalizedString descriptionLocalization
        {
            get => m_descriptionLocalization;
            set => m_descriptionLocalization = value;
        }

        /// <summary>
        /// Event that is triggered every time the UI updates to reflect the current binding.
        /// This can be used to tie custom visualizations to bindings.
        /// </summary>
        public UpdateBindingUIEvent updateBindingUIEvent
        {
            get
            {
                if (m_UpdateBindingUIEvent == null)
                {
                    m_UpdateBindingUIEvent = new UpdateBindingUIEvent();
                }

                return m_UpdateBindingUIEvent;
            }
        }

        /// <summary>
        /// Event that is triggered when an interactive rebind is started on the action.
        /// </summary>
        public InteractiveRebindEvent startRebindEvent
        {
            get
            {
                if (m_RebindStartEvent == null)
                {
                    m_RebindStartEvent = new InteractiveRebindEvent();
                }

                return m_RebindStartEvent;
            }
        }

        /// <summary>
        /// Event that is triggered when an interactive rebind has been completed or canceled.
        /// </summary>
        public InteractiveRebindEvent stopRebindEvent
        {
            get
            {
                if (m_RebindStopEvent == null)
                {
                    m_RebindStopEvent = new InteractiveRebindEvent();
                }

                return m_RebindStopEvent;
            }
        }

        /// <summary>
        /// When an interactive rebind is in progress, this is the rebind operation controller.
        /// Otherwise, it is <c>null</c>.
        /// </summary>
        public InputActionRebindingExtensions.RebindingOperation ongoingRebind => m_RebindOperation;

        public RebindOptions rebindOptions
        {
            get => m_RebindOptions;
            set => m_RebindOptions = value ?? new RebindOptions();
        }

        public IDeviceBindingDisplayProvider bindingDisplayProvider
        {
            get => m_BindingDisplayProvider ??= new DefaultDeviceBindingDisplayProvider();
            set => m_BindingDisplayProvider = value ?? new DefaultDeviceBindingDisplayProvider();
        }

        public DuplicateBindingEvent duplicateBindingEvent
        {
            get
            {
                if (m_DuplicateBindingEvent == null)
                    m_DuplicateBindingEvent = new DuplicateBindingEvent();
                return m_DuplicateBindingEvent;
            }
        }

        public DuplicateBindingResolutionEvent duplicateResolutionEvent
        {
            get
            {
                if (m_DuplicateResolutionEvent == null)
                    m_DuplicateResolutionEvent = new DuplicateBindingResolutionEvent();
                return m_DuplicateResolutionEvent;
            }
        }

        public DeviceBindingDisplayEvent deviceBindingDisplayEvent
        {
            get
            {
                if (m_DeviceBindingDisplayEvent == null)
                    m_DeviceBindingDisplayEvent = new DeviceBindingDisplayEvent();
                return m_DeviceBindingDisplayEvent;
            }
        }

        public InteractiveRebindEvent timeoutRebindEvent
        {
            get
            {
                if (m_TimeoutRebindEvent == null)
                    m_TimeoutRebindEvent = new InteractiveRebindEvent();
                return m_TimeoutRebindEvent;
            }
        }

        public RebindAccessibilityEvent rebindAccessibilityEvent
        {
            get
            {
                if (m_RebindAccessibilityEvent == null)
                    m_RebindAccessibilityEvent = new RebindAccessibilityEvent();
                return m_RebindAccessibilityEvent;
            }
        }

        public bool isRebinding => m_RebindOperation != null;

        /// <summary>
        /// Return the action and binding index for the binding that is targeted by the component
        /// according to
        /// </summary>
        /// <param name="action"></param>
        /// <param name="bindingIndex"></param>
        /// <returns></returns>
        public bool ResolveActionAndBinding(out InputAction action, out int bindingIndex)
        {
            bindingIndex = -1;

            action = m_Action?.action;
            if (action == null)
            {
                return false;
            }

            if (string.IsNullOrEmpty(m_BindingId))
            {
                return false;
            }

            // Look up binding index.
            if (!Guid.TryParse(m_BindingId, out var bindingId))
            {
                Debug.LogError($"Binding ID '{m_BindingId}' is not a valid GUID.", this);
                return false;
            }
            bindingIndex = action.bindings.IndexOf(x => x.id == bindingId);
            if (bindingIndex == -1)
            {
                Debug.LogError($"Cannot find binding with ID '{bindingId}' on '{action}'", this);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Trigger a refresh of the currently displayed binding.
        /// </summary>
        public void UpdateBindingDisplay()
        {
            var displayString = string.Empty;
            var deviceLayoutName = default(string);
            var controlPath = default(string);

            // Get display string from action.
            var action = m_Action?.action;
            if (action != null)
            {
                var bindingIndex = action.bindings.IndexOf(x => x.id.ToString() == m_BindingId);
                if (bindingIndex != -1)
                {
                    var binding = action.bindings[bindingIndex];
                    if (binding.isComposite && m_CompositeOverrideBackup != null)
                    {
                        // Input System falls back to the default path when a
                        // composite part has no override. During rebinding we
                        // intentionally omit those defaults from the display.
                        var boundParts = new List<string>();
                        for (var i = bindingIndex + 1;
                             i < action.bindings.Count && action.bindings[i].isPartOfComposite;
                             ++i)
                        {
                            var part = action.bindings[i];
                            if (!string.IsNullOrEmpty(part.overridePath))
                            {
                                var partDisplay = action.GetBindingDisplayString(i, displayStringOptions);
                                boundParts.Add($"{part.name}: {partDisplay}");
                            }
                        }

                        displayString = string.Join(", ", boundParts);
                    }
                    else
                    {
                        displayString = action.GetBindingDisplayString(bindingIndex, out deviceLayoutName, out controlPath, displayStringOptions);
                    }
                }
            }

            // Set on label (if any).
            if (m_BindingText != null)
            {
                m_BindingText.text = displayString;
            }

            // Give listeners a chance to configure UI in response.
            m_UpdateBindingUIEvent?.Invoke(this, displayString, deviceLayoutName, controlPath);
            var displayProvider = bindingDisplayProvider;
            deviceBindingDisplayEvent.Invoke(this,
                displayProvider.GetDeviceKind(deviceLayoutName, controlPath).ToString(),
                displayProvider.GetGlyphKey(deviceLayoutName, controlPath),
                displayProvider.GetPrompt(deviceLayoutName, controlPath));
        }

        /// <summary>
        /// Remove currently applied binding overrides.
        /// </summary>
        public void ResetToDefault()
        {
            if (!ResolveActionAndBinding(out var action, out var bindingIndex))
            {
                return;
            }

            ResetBinding(action, bindingIndex);
            //if (action.bindings[bindingIndex].isComposite)
            //{
            //    // It's a composite. Remove overrides from part bindings.
            //    for (var i = bindingIndex + 1; i < action.bindings.Count && action.bindings[i].isPartOfComposite; ++i)
            //        action.RemoveBindingOverride(i);
            //}
            //else
            //{
            //    action.RemoveBindingOverride(bindingIndex);
            //}
            UpdateBindingDisplay();
        }

        private void ResetBinding(InputAction action, int bindingIndex)
        {
            action.RemoveBindingOverride(bindingIndex);

            // Composite overrides are stored on their parts, not on the
            // composite header.
            if (action.bindings[bindingIndex].isComposite)
            {
                for (var i = bindingIndex + 1;
                     i < action.bindings.Count && action.bindings[i].isPartOfComposite;
                     ++i)
                {
                    action.RemoveBindingOverride(i);
                }
            }
        }

        /// <summary>
        /// Initiate an interactive rebind that lets the player actuate a control to choose a new binding
        /// for the action.
        /// </summary>
        public void StartInteractiveRebind()
        {
            if (!ResolveActionAndBinding(out var action, out var bindingIndex))
            {
                return;
            }

            // If the binding is a composite, we need to rebind each part in turn.
            if (action.bindings[bindingIndex].isComposite)
            {
                var firstPartIndex = bindingIndex + 1;
                if (firstPartIndex < action.bindings.Count && action.bindings[firstPartIndex].isPartOfComposite)
                {
                    BeginCompositeRebind(action, bindingIndex);
                    PerformInteractiveRebind(action, firstPartIndex, allCompositeParts: true);
                }
            }
            else
            {
                m_OriginalBindingOverride = action.bindings[bindingIndex].overridePath;
                m_OriginalBindingPath = action.bindings[bindingIndex].effectivePath;
                PerformInteractiveRebind(action, bindingIndex);
            }
        }

        public void CancelInteractiveRebind()
        {
            m_RebindOperation?.Cancel();
        }

        private void PerformInteractiveRebind(InputAction action, int bindingIndex, bool allCompositeParts = false, int duplicateRetryCount = 0)
        {
            m_RebindOperation?.Cancel(); // Will null out m_RebindOperation.

            m_RebindSession = new RebindSession();
            m_RebindSession.Begin(action);

            void CleanUp()
            {
                m_RebindOperation?.Dispose();
                m_RebindOperation = null;
                m_RebindSession?.Complete();
                m_RebindSession = null;
            }

            // Configure the rebind.
            var options = m_RebindOptions ?? new RebindOptions();
            m_RebindStartedAt = Time.unscaledTime;
            m_RebindOperation = action.PerformInteractiveRebinding(bindingIndex);
            if (!string.IsNullOrWhiteSpace(options.bindingGroup))
                m_RebindOperation.WithBindingGroup(options.bindingGroup);
            if (!string.IsNullOrWhiteSpace(options.cancelControlPath))
                m_RebindOperation.WithCancelingThrough(options.cancelControlPath);
            if (!string.IsNullOrWhiteSpace(options.expectedControlType))
                m_RebindOperation.WithExpectedControlType(options.expectedControlType);
            if (options.minimumMagnitude > 0)
                m_RebindOperation.WithMagnitudeHavingToBeGreaterThan(options.minimumMagnitude);
            if (options.controlPathsToMatch != null)
            {
                foreach (var path in options.controlPathsToMatch)
                {
                    if (!string.IsNullOrWhiteSpace(path))
                        m_RebindOperation.WithControlsHavingToMatchPath(path);
                }
            }
            if (options.controlPathsToExclude != null)
            {
                foreach (var path in options.controlPathsToExclude)
                {
                    if (!string.IsNullOrWhiteSpace(path))
                        m_RebindOperation.WithControlsExcluding(path);
                }
            }

            m_RebindOperation
                .OnCancel(
                    operation =>
                    {
                        m_RebindStopEvent?.Invoke(this, operation);
                        RestoreCompositeOverrides(action);
                        //// TODO: Implement rebind overlay and blur manager
                        /*
                        if (m_RebindOverlay != null)
                        {
                            m_RebindOverlay.ModalWindowOut();
                            m_BlurManager.BlurOutAnim();
                        }
                        */
                        UpdateBindingDisplay();
                        SetRebindOverlayVisible(false);
                        RaiseAccessibility("Rebind cancelled");
                        m_RebindStartedAt = -1f;
                        m_OriginalBindingOverride = null;
                        m_OriginalBindingPath = null;
                        CleanUp();
                    })
                .OnComplete(
                    operation =>
                    {
                        m_RebindStopEvent?.Invoke(this, operation);
                        m_RebindStartedAt = -1f;

                        // Check for duplicates before accepting the new binding.
                        var duplicateResolution = options.duplicateBindingPolicy == DuplicateBindingPolicy.Allow
                            ? DuplicateBindingResolution.Allow
                            : options.duplicateBindingResolution;
                        if (duplicateResolution != DuplicateBindingResolution.Allow &&
                            CheckDuplicateBinding(action, bindingIndex, allCompositeParts, out var conflictingAction, out var conflictingBindingIndex))
                        {
                            var conflictingPath = action.bindings[bindingIndex].effectivePath;
                            duplicateBindingEvent.Invoke(this, conflictingAction?.name ?? "Unknown", conflictingPath);
                            duplicateResolutionEvent.Invoke(this, conflictingAction?.name ?? "Unknown", conflictingPath, duplicateResolution);

                            if (duplicateResolution == DuplicateBindingResolution.Replace && conflictingAction != null)
                            {
                                conflictingAction.RemoveBindingOverride(conflictingBindingIndex);
                                CleanUp();
                                m_OriginalBindingOverride = null;
                                m_OriginalBindingPath = null;
                                SaveRebinds();
                                UpdateBindingDisplay();
                                SetRebindOverlayVisible(false);
                                return;
                            }

                            if (duplicateResolution == DuplicateBindingResolution.Swap && conflictingAction != null)
                            {
                                conflictingAction.ApplyBindingOverride(conflictingBindingIndex, m_OriginalBindingPath);
                                CleanUp();
                                m_OriginalBindingOverride = null;
                                m_OriginalBindingPath = null;
                                SaveRebinds();
                                UpdateBindingDisplay();
                                SetRebindOverlayVisible(false);
                                return;
                            }

                            action.RemoveBindingOverride(bindingIndex);
                            if (allCompositeParts)
                            {
                                RestoreCompositeOverrides(action);
                                CleanUp();
                                m_CompositeOverrideBackup = null;
                                m_RebindOperation = null;
                                if (duplicateRetryCount < Math.Max(0, options.maximumDuplicateRetries))
                                {
                                    var compositeHeaderIndex = FindCompositeHeaderIndex(action, bindingIndex);
                                    BeginCompositeRebind(action, compositeHeaderIndex);
                                    PerformInteractiveRebind(action, compositeHeaderIndex + 1, true, duplicateRetryCount + 1);
                                }
                                else
                                {
                                    UpdateBindingDisplay();
                                    SetRebindOverlayVisible(false);
                                }
                                return;
                            }

                            if (!string.IsNullOrEmpty(m_OriginalBindingOverride))
                                action.ApplyBindingOverride(bindingIndex, m_OriginalBindingOverride);
                            CleanUp();
                            if (duplicateRetryCount < Math.Max(0, options.maximumDuplicateRetries))
                                PerformInteractiveRebind(action, bindingIndex, false, duplicateRetryCount + 1);
                            else
                            {
                                m_OriginalBindingOverride = null;
                                UpdateBindingDisplay();
                                SetRebindOverlayVisible(false);
                            }
                            return;
                        }

                        UpdateBindingDisplay();
                        CleanUp();
                        m_RebindStartedAt = -1f;
                        m_OriginalBindingOverride = null;
                        m_OriginalBindingPath = null;

                        // If there's more composite parts we should bind, initiate a rebind
                        // for the next part.
                        if (allCompositeParts)
                        {
                            var nextBindingIndex = bindingIndex + 1;
                            if (nextBindingIndex < action.bindings.Count && action.bindings[nextBindingIndex].isPartOfComposite)
                            {
                                PerformInteractiveRebind(action, nextBindingIndex, true);
                            }
                            //// TODO: Implement rebind overlay and blur manager
                            /*
                            else if (m_RebindOverlay != null)
                            {
                                // only hide the overlay if we're done with all parts
                                m_RebindOverlay.ModalWindowOut();
                                m_BlurManager.BlurOutAnim();
                                // save rebinds to file only if all parts are done
                                SaveRebinds();
                                if (m_RebindText != null)
                                    m_RebindText.text = string.Empty; // Clear the rebind text after completion
                            }
                            */
                            else
                            {
                                // save rebinds to file only if all parts are done
                                SaveRebinds();
                                if (m_RebindText != null)
                                    m_RebindText.text = string.Empty; // Clear the rebind text after completion
                                SetRebindOverlayVisible(false);
                                m_CompositeOverrideBackup = null;
                                UpdateBindingDisplay();
                            }
                        }
                        //// TODO: Implement rebind overlay and blur manager
                        /*
                        else if (m_RebindOverlay != null)
                        {
                            // only hide the overlay if we're done with all parts
                            m_RebindOverlay.ModalWindowOut();
                            m_BlurManager.BlurOutAnim();
                            // save rebinds to file only if all parts are done
                            SaveRebinds();
                            if (m_RebindText != null)
                                m_RebindText.text = string.Empty; // Clear the rebind text after completion
                        }
                        */
                        else
                        {
                            // save rebinds to file only if all parts are done
                            SaveRebinds();
                            if (m_RebindText != null)
                                m_RebindText.text = string.Empty; // Clear the rebind text after completion
                            SetRebindOverlayVisible(false);
                            m_CompositeOverrideBackup = null;
                            UpdateBindingDisplay();
                        }
                    }
                );

            // If it's a part binding, show the name of the part in the UI.
            var partName = default(string);
            if (action.bindings[bindingIndex].isPartOfComposite)
            {
                partName = $"Binding '{action.bindings[bindingIndex].name}'. ";
            }

            SetRebindOverlayVisible(true);

            if (m_RebindText != null)
            {
                var text = partName + bindingDisplayProvider.GetPrompt(null, null, m_RebindOperation.expectedControlType);

                // Resolve the optional localized prompt. An unconfigured
                // LocalizedString must not produce lookup errors.
                try
                {
                    var table = m_descriptionLocalization.TableReference.TableCollectionName;
                    var key = m_descriptionLocalization.TableEntryReference.Key;
                    if (!string.IsNullOrEmpty(table) && !string.IsNullOrEmpty(key))
                    {
                        var localizedPrompt = m_descriptionLocalization.GetLocalizedString();
                        if (!string.IsNullOrEmpty(localizedPrompt))
                            text = localizedPrompt;
                    }
                }
                catch (Exception exception)
                {
                    Debug.LogWarning($"Could not resolve the rebind prompt localization: {exception.Message}", this);
                }

                m_RebindText.text = text;
            }

            // Give listeners a chance to act on the rebind starting.
            m_RebindStartEvent?.Invoke(this, m_RebindOperation);
            RaiseAccessibility("Waiting for input");

            m_RebindOperation.Start();
        }

        public RebindManager rebindManager
        {
            get => m_RebindManager;
            set
            {
                m_RebindManager = value;
                m_BindingOverrideService = value;
                UpdateBindingDisplay();
            }
        }

        public IBindingOverrideService bindingOverrideService
        {
            get => m_BindingOverrideService ?? m_RebindManager;
            set => m_BindingOverrideService = value;
        }

        private void SetRebindOverlayVisible(bool visible)
        {
            if (m_RebindOverlay != null)
                m_RebindOverlay.SetActive(visible);
        }

        private void RaiseAccessibility(string text)
        {
            m_RebindAccessibilityEvent?.Invoke(this, text);
        }

        private void BeginCompositeRebind(InputAction action, int compositeIndex)
        {
            m_CompositeOverrideBackup = new Dictionary<int, string>();

            for (var i = compositeIndex + 1;
                 i < action.bindings.Count && action.bindings[i].isPartOfComposite;
                 ++i)
            {
                m_CompositeOverrideBackup[i] = action.bindings[i].overridePath;
                action.RemoveBindingOverride(i);
            }

            // The display now contains no stale composite overrides. As each
            // part is rebound, only the freshly captured parts will appear.
            UpdateBindingDisplay();
        }

        private void RestoreCompositeOverrides(InputAction action)
        {
            if (m_CompositeOverrideBackup == null)
                return;

            foreach (var backup in m_CompositeOverrideBackup)
            {
                if (string.IsNullOrEmpty(backup.Value))
                    action.RemoveBindingOverride(backup.Key);
                else
                    action.ApplyBindingOverride(backup.Key, backup.Value);
            }

            m_CompositeOverrideBackup = null;
        }

        private bool CheckDuplicateBinding(InputAction action, int bindingIndex, bool allCompositeParts = false)
        {
            return CheckDuplicateBinding(action, bindingIndex, allCompositeParts, out _);
        }

        private bool CheckDuplicateBinding(InputAction action, int bindingIndex, bool allCompositeParts, out InputAction conflictingAction)
        {
            return CheckDuplicateBinding(action, bindingIndex, allCompositeParts, out conflictingAction, out _);
        }

        private bool CheckDuplicateBinding(InputAction action, int bindingIndex, bool allCompositeParts, out InputAction conflictingAction, out int conflictingBindingIndex)
        {
            conflictingAction = null;
            conflictingBindingIndex = -1;
            InputBinding newBinding = action.bindings[bindingIndex];
            var options = m_RebindOptions ?? new RebindOptions();
            var maps = action.actionMap?.asset != null && options.duplicateBindingScope == DuplicateBindingScope.EntireAsset
                ? action.actionMap.asset.actionMaps
                : new[] { action.actionMap };
            foreach (var map in maps)
            {
                if (map == null)
                    continue;
                for (var mapBindingIndex = 0; mapBindingIndex < map.bindings.Count; ++mapBindingIndex)
                {
                    var binding = map.bindings[mapBindingIndex];
                    if (binding.action == newBinding.action ||
                        binding.effectivePath != newBinding.effectivePath ||
                        !ControlSchemeGroupsOverlap(binding.groups, newBinding.groups))
                        continue;

                    conflictingAction = map.FindAction(binding.action, throwIfNotFound: false);
                    conflictingBindingIndex = conflictingAction == null
                        ? -1
                        : conflictingAction.bindings.IndexOf(candidate => candidate.id == binding.id);
                    return true;
                }
            }

            if (allCompositeParts)
            {
                // Only compare against parts of this composite. The previous
                // implementation started at index 1, which could compare
                // unrelated bindings and miss the actual composite boundary.
                var compositeStart = bindingIndex - 1;
                while (compositeStart >= 0 && !action.bindings[compositeStart].isComposite)
                    compositeStart--;

                for (var i = compositeStart + 1; i < bindingIndex; i++)
                {
                    var previousPart = action.bindings[i];
                    if (previousPart.isPartOfComposite &&
                        !string.IsNullOrEmpty(previousPart.effectivePath) &&
                        previousPart.effectivePath == newBinding.effectivePath)
                    {
                        conflictingAction = action;
                        conflictingBindingIndex = i;
                        return true;
                    }
                }
            }

            return false;
        }

        private bool ControlSchemeGroupsOverlap(string first, string second)
        {
            if (string.IsNullOrEmpty(first) || string.IsNullOrEmpty(second))
                return true;

            foreach (var left in first.Split(InputBinding.Separator))
            foreach (var right in second.Split(InputBinding.Separator))
                if (left == right)
                    return true;
            return false;
        }

        private int FindCompositeHeaderIndex(InputAction action, int bindingIndex)
        {
            for (var i = bindingIndex; i >= 0; --i)
            {
                if (action.bindings[i].isComposite)
                    return i;
            }

            return bindingIndex;
        }

        private void SaveRebinds()
        {
            bindingOverrideService?.SaveRebinds();
        }

        private string ConvertActionToIdentifier(InputActionReference action)
        {
            return "action_" + action.name.Replace(" ", "_").Replace("/", "_").ToLower();
        }

        private string ConvertBindingToIdentifier(InputActionReference action, InputBinding binding)
        {
            string actionName = action.name.Replace(" ", "_").Replace("/", "_").ToLower();
            if (binding.isPartOfComposite)
            {
                return "binding_" + actionName + "_" + binding.name.Replace(" ", "_").Replace("/", "_").ToLower();
            }
            return "binding_" + actionName;
        }

        /// <summary>
        /// Returns the localized string from the localization table for the given identifier.
        /// </summary>
        /// <param name="table"></param>
        /// <param name="identifier"></param>
        /// <returns>localized string</returns>
        private string GetLocalizationString(string table, string identifier)
        {
            try
            {
                StringTable strTable = LocalizationSettings.StringDatabase.GetTable(table);
                return strTable.GetEntry(identifier).GetLocalizedString();
            }
            catch (Exception)
            {
                Debug.LogError($"String table {table} not found/initialized yet or identifier {identifier} not found.");
                return "";
            }
        }

        protected void OnEnable()
        {
            InputSystem.onDeviceChange += OnDeviceChange;
            SetRebindOverlayVisible(false);

            // Replace the serialized reference with the manager's live action
            // while preserving its action map.
            var manager = m_RebindManager;
            if (manager != null && m_Action != null && m_Action.action != null)
            {
                var actionName = m_Action.action.actionMap != null
                    ? $"{m_Action.action.actionMap.name}/{m_Action.action.name}"
                    : m_Action.action.name;
                var managedAction = manager.ActionAsset?.FindAction(actionName, throwIfNotFound: false);
                if (managedAction != null)
                    m_Action = InputActionReference.Create(managedAction);
            }

            if (m_Action != null && m_Action.action != null && m_ActionLabel != null)
            {
                try
                {
                    var table = m_titleLocalization.TableReference.TableCollectionName;
                    if (!string.IsNullOrEmpty(table))
                    {
                        var localizedAction = GetLocalizationString(table, ConvertActionToIdentifier(m_Action));
                        if (!string.IsNullOrEmpty(localizedAction))
                            m_ActionLabel.text = localizedAction;
                    }
                }
                catch (Exception exception)
                {
                    Debug.LogWarning($"Could not resolve the action localization: {exception.Message}", this);
                }
            }

            if (s_RebindActionUIs == null)
            {
                s_RebindActionUIs = new List<RebindActionUI>();
            }

            s_RebindActionUIs.Add(this);
            if (s_RebindActionUIs.Count == 1)
            {
                InputSystem.onActionChange += OnActionChange;
            }

            UpdateBindingDisplay();
        }

        protected void OnDisable()
        {
            InputSystem.onDeviceChange -= OnDeviceChange;
            m_RebindOperation?.Cancel();

            // Cancellation normally invokes CleanUp synchronously. Keep this fallback so disabling
            // the UI cannot leave an action disabled if the Input System defers the callback.
            m_RebindOperation?.Dispose();
            m_RebindOperation = null;
            m_RebindSession?.Cancel();
            m_RebindSession = null;
            m_RebindStartedAt = -1f;
            SetRebindOverlayVisible(false);

            if (s_RebindActionUIs == null)
                return;

            s_RebindActionUIs.Remove(this);
            if (s_RebindActionUIs.Count == 0)
            {
                s_RebindActionUIs = null;
                InputSystem.onActionChange -= OnActionChange;
            }
        }

        // When the action system re-resolves bindings, we want to update our UI in response. While this will
        // also trigger from changes we made ourselves, it ensures that we react to changes made elsewhere. If
        // the user changes keyboard layout, for example, we will get a BoundControlsChanged notification and
        // will update our UI to reflect the current keyboard layout.
        private static void OnActionChange(object obj, InputActionChange change)
        {
            if (change != InputActionChange.BoundControlsChanged || s_RebindActionUIs == null)
            {
                return;
            }

            var action = obj as InputAction;
            var actionMap = action?.actionMap ?? obj as InputActionMap;
            var actionAsset = actionMap?.asset ?? obj as InputActionAsset;

            for (var i = 0; i < s_RebindActionUIs.Count; ++i)
            {
                var component = s_RebindActionUIs[i];
                var referencedAction = component.actionReference?.action;
                if (referencedAction == null)
                {
                    continue;
                }

                if (referencedAction == action ||
                        referencedAction.actionMap == actionMap ||
                        referencedAction.actionMap?.asset == actionAsset)
                {
                    component.UpdateBindingDisplay();
                }
            }
        }

        [Tooltip("Reference to action that is to be rebound from the UI.")]
        [SerializeField]
        private InputActionReference m_Action;

        [Tooltip("Optional manager whose live action asset and persistence service are used by this row.")]
        [SerializeField]
        private RebindManager m_RebindManager;

        private IBindingOverrideService m_BindingOverrideService;

        [SerializeField]
        private string m_BindingId;

        [SerializeField]
        private InputBinding.DisplayStringOptions m_DisplayStringOptions;

        [Tooltip("Text label that will receive the name of the action. Optional. Set to None to have the "
            + "rebind UI not show a label for the action.")]
        [SerializeField]
        private TMPro.TextMeshProUGUI m_ActionLabel;

        [Tooltip("Text label that will receive the current, formatted binding string.")]
        [SerializeField]
        private TMPro.TextMeshProUGUI m_BindingText;

        [Tooltip("Optional overlay GameObject that is shown while a rebind is in progress.")]
        [SerializeField]
        private GameObject m_RebindOverlay;

        [Tooltip("Optional text label that will be updated with prompt for user input.")]
        [SerializeField]
        private TMPro.TextMeshProUGUI m_RebindText;

        [Tooltip("Event that is triggered when the way the binding is display should be updated. This allows displaying "
            + "bindings in custom ways, e.g. using images instead of text.")]
        [SerializeField]
        private UpdateBindingUIEvent m_UpdateBindingUIEvent;

        [Tooltip("Event that is triggered when an interactive rebind is being initiated. This can be used, for example, "
            + "to implement custom UI behavior while a rebind is in progress. It can also be used to further "
            + "customize the rebind.")]
        [SerializeField]
        private InteractiveRebindEvent m_RebindStartEvent;

        [Tooltip("Event that is triggered when an interactive rebind is complete or has been aborted.")]
        [SerializeField]
        private InteractiveRebindEvent m_RebindStopEvent;

        [SerializeField]
        private InteractiveRebindEvent m_TimeoutRebindEvent;

        [SerializeField]
        private RebindAccessibilityEvent m_RebindAccessibilityEvent;

        [Tooltip("Controls accepted, excluded, and conflict behavior for interactive rebinding.")]
        [SerializeField]
        private RebindOptions m_RebindOptions = new RebindOptions();
        private IDeviceBindingDisplayProvider m_BindingDisplayProvider;

        [Tooltip("Event raised when a proposed binding conflicts with another binding.")]
        [SerializeField]
        private DuplicateBindingEvent m_DuplicateBindingEvent;
        [SerializeField] private DuplicateBindingResolutionEvent m_DuplicateResolutionEvent;
        [SerializeField] private DeviceBindingDisplayEvent m_DeviceBindingDisplayEvent;

        private InputActionRebindingExtensions.RebindingOperation m_RebindOperation;
        private RebindSession m_RebindSession;
        private Dictionary<int, string> m_CompositeOverrideBackup;
        private string m_OriginalBindingOverride;
        private string m_OriginalBindingPath;
        private float m_RebindStartedAt = -1f;

        private static List<RebindActionUI> s_RebindActionUIs;

        [SerializeField]
        private LocalizedString m_titleLocalization;

        [SerializeField]
        private LocalizedString m_descriptionLocalization;

        // We want the label for the action name to update in edit mode, too, so
        // we kick that off from here.
#if UNITY_EDITOR

        protected void OnValidate()
        {
            UpdateActionLabel();
            UpdateBindingDisplay();
        }

#endif

        private void UpdateActionLabel()
        {
            if (m_ActionLabel != null)
            {
                var action = m_Action?.action;
                m_ActionLabel.text = action != null ? action.name : string.Empty;
            }
        }

        [Serializable]
        public class UpdateBindingUIEvent : UnityEvent<RebindActionUI, string, string, string>
        {
        }

        [Serializable]
        public class InteractiveRebindEvent : UnityEvent<RebindActionUI, InputActionRebindingExtensions.RebindingOperation>
        {
        }

        [Serializable]
        public class RebindAccessibilityEvent : UnityEvent<RebindActionUI, string>
        {
        }

        private void Update()
        {
            if (m_RebindOperation == null || m_RebindOptions == null || m_RebindOptions.timeoutSeconds <= 0 ||
                m_RebindStartedAt < 0 || Time.unscaledTime - m_RebindStartedAt < m_RebindOptions.timeoutSeconds)
                return;

            timeoutRebindEvent.Invoke(this, m_RebindOperation);
            RaiseAccessibility("Rebind timed out");
            m_RebindOperation.Cancel();
        }

        private void OnDeviceChange(InputDevice device, InputDeviceChange change)
        {
            if (m_RebindOperation == null || m_RebindOptions == null || !m_RebindOptions.cancelWhenDeviceIsRemoved ||
                change != InputDeviceChange.Removed)
                return;

            RaiseAccessibility("Rebind cancelled because a device was removed");
            m_RebindOperation.Cancel();
        }

        [Serializable]
        public class DuplicateBindingEvent : UnityEvent<RebindActionUI, string, string>
        {
        }

        [Serializable]
        public class DuplicateBindingResolutionEvent : UnityEvent<RebindActionUI, string, string, DuplicateBindingResolution>
        {
        }

        [Serializable]
        public class DeviceBindingDisplayEvent : UnityEvent<RebindActionUI, string, string, string>
        {
        }
    }
}
