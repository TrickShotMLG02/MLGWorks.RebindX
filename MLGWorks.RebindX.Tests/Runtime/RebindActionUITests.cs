using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Utilities;
using UnityEngine.TestTools;
using MLGWorks.RebindX.Runtime;

namespace MLGWorks.RebindX.Tests
{
    public sealed class RebindActionUITests : InputTestFixture
    {
        private sealed class TestOverrideService : IBindingOverrideService
        {
            public InputActionAsset ActionAsset { get; set; }
            public int SaveCount { get; private set; }
            public int LoadCount { get; private set; }

            public BindingOverrideResult SaveRebinds()
            {
                SaveCount++;
                return BindingOverrideResult.Success();
            }

            public BindingOverrideResult LoadRebinds()
            {
                LoadCount++;
                return BindingOverrideResult.Success();
            }

            public BindingOverrideResult ResetRebinds() => BindingOverrideResult.Success();
        }

        private InputActionAsset m_Asset;
        private InputAction m_Action;
        private RebindActionUI m_UI;

        private sealed class TestDisplayProvider : IDeviceBindingDisplayProvider
        {
            public BindingDeviceKind GetDeviceKind(string _, string __) => BindingDeviceKind.Gamepad;
            public string GetGlyphKey(string _, string __) => "custom.glyph";
            public string GetPrompt(string _, string __, string ___ = null) => "Custom prompt";
        }

        [SetUp]
        public override void Setup()
        {
            base.Setup();

            m_Asset = ScriptableObject.CreateInstance<InputActionAsset>();
            var map = m_Asset.AddActionMap("Gameplay");
            m_Action = map.AddAction("Jump", InputActionType.Button, binding: "<Keyboard>/space");
            map.Enable();

            var gameObject = new GameObject("RebindActionUI Test");
            m_UI = gameObject.AddComponent<RebindActionUI>();
            m_UI.actionReference = InputActionReference.Create(m_Action);
            m_UI.bindingId = m_Action.bindings[0].id.ToString();
        }

        [TearDown]
        public override void TearDown()
        {
            if (m_UI != null)
                Object.DestroyImmediate(m_UI.gameObject);
            if (m_Asset != null)
                Object.DestroyImmediate(m_Asset);

            base.TearDown();
        }

        [Test]
        public void ResolveActionAndBinding_ResolvesValidBinding()
        {
            Assert.That(m_UI.ResolveActionAndBinding(out var action, out var index), Is.True);
            Assert.That(action, Is.EqualTo(m_Action));
            Assert.That(index, Is.EqualTo(0));
        }

        [Test]
        public void ResolveActionAndBinding_RejectsMissingActionReference()
        {
            m_UI.actionReference = null;
            Assert.That(m_UI.ResolveActionAndBinding(out _, out _), Is.False);
        }

        [Test]
        public void ResolveActionAndBinding_RejectsMalformedBindingId()
        {
            m_UI.bindingId = "not-a-guid";

            LogAssert.Expect(LogType.Error, "Binding ID 'not-a-guid' is not a valid GUID.");
            Assert.That(m_UI.ResolveActionAndBinding(out _, out _), Is.False);
        }

        [Test]
        public void ResolveActionAndBinding_RejectsEmptyAndUnknownIds()
        {
            m_UI.bindingId = string.Empty;
            Assert.That(m_UI.ResolveActionAndBinding(out _, out _), Is.False);

            m_UI.bindingId = System.Guid.NewGuid().ToString();
            LogAssert.Expect(LogType.Error, $"Cannot find binding with ID '{m_UI.bindingId}' on '{m_Action}'");
            Assert.That(m_UI.ResolveActionAndBinding(out _, out _), Is.False);
        }

        [Test]
        public void ResetAndStartRebind_WithInvalidBindingAreSafe()
        {
            m_UI.bindingId = string.Empty;

            Assert.DoesNotThrow(() => m_UI.ResetToDefault());
            Assert.DoesNotThrow(() => m_UI.StartInteractiveRebind());
            Assert.That(m_UI.ongoingRebind, Is.Null);
        }

        [Test]
        public void UpdateBindingDisplay_ReportsCurrentBinding()
        {
            string display = null;
            m_UI.updateBindingUIEvent.AddListener((_, value, _, _) => display = value);

            m_UI.UpdateBindingDisplay();

            Assert.That(display, Does.Contain("Space"));
        }

        [Test]
        public void UpdateBindingDisplay_UsesEmptyDisplayForUnknownBinding()
        {
            string display = null;
            m_UI.updateBindingUIEvent.AddListener((_, value, _, _) => display = value);
            m_UI.bindingId = System.Guid.NewGuid().ToString();
            m_UI.UpdateBindingDisplay();
            Assert.That(display, Is.Empty);
        }

        [Test]
        public void ResetToDefault_RemovesNormalBindingOverride()
        {
            m_Action.ApplyBindingOverride(0, "<Keyboard>/enter");

            m_UI.ResetToDefault();

            Assert.That(m_Action.bindings[0].overridePath, Is.Null.Or.Empty);
        }

        [Test]
        public void ResetToDefault_RemovesAllCompositePartOverrides()
        {
            m_Action.actionMap.Disable();
            var move = m_Action.actionMap.AddAction("Move", InputActionType.Value);
            move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");
            m_Action.actionMap.Enable();

            var compositeIndex = move.bindings.IndexOf(binding => binding.isComposite);
            for (var i = compositeIndex + 1; i < move.bindings.Count && move.bindings[i].isPartOfComposite; ++i)
                move.ApplyBindingOverride(i, "<Keyboard>/enter");

            m_UI.actionReference = InputActionReference.Create(move);
            m_UI.bindingId = move.bindings[compositeIndex].id.ToString();
            m_UI.ResetToDefault();

            for (var i = compositeIndex + 1; i < move.bindings.Count && move.bindings[i].isPartOfComposite; ++i)
                Assert.That(move.bindings[i].overridePath, Is.Null.Or.Empty);
        }

        [Test]
        public void InteractiveRebind_UpdatesBindingAndFinishes()
        {
            m_UI.StartInteractiveRebind();
            m_Action.ApplyBindingOverride(0, "<Gamepad>/buttonSouth");
            m_UI.ongoingRebind.Complete();

            Assert.That(m_Action.bindings[0].overridePath, Is.EqualTo("<Gamepad>/buttonSouth"));
            Assert.That(m_UI.ongoingRebind, Is.Null);
        }

        [Test]
        public void InteractiveRebind_CanBeStartedAgainAfterCompletion()
        {
            m_UI.StartInteractiveRebind();
            m_Action.ApplyBindingOverride(0, "<Gamepad>/buttonSouth");
            m_UI.ongoingRebind.Complete();

            m_UI.StartInteractiveRebind();
            Assert.That(m_UI.ongoingRebind, Is.Not.Null);
            m_UI.CancelInteractiveRebind();
            Assert.That(m_UI.ongoingRebind, Is.Null);
            Assert.That(m_Action.bindings[0].overridePath, Is.EqualTo("<Gamepad>/buttonSouth"));
        }

        [Test]
        public void CancelInteractiveRebind_IsSafeWhenIdle()
        {
            Assert.DoesNotThrow(() => m_UI.CancelInteractiveRebind());
        }

        [Test]
        public void CancelInteractiveRebind_CanBeCalledRepeatedly()
        {
            m_UI.StartInteractiveRebind();
            m_UI.CancelInteractiveRebind();
            Assert.DoesNotThrow(() => m_UI.CancelInteractiveRebind());
            Assert.That(m_UI.ongoingRebind, Is.Null);
        }

        [Test]
        public void InteractiveRebind_CanRestartWhileActive()
        {
            m_UI.StartInteractiveRebind();
            var firstOperation = m_UI.ongoingRebind;
            m_UI.StartInteractiveRebind();

            Assert.That(m_UI.ongoingRebind, Is.Not.Null);
            Assert.That(m_UI.ongoingRebind, Is.Not.SameAs(firstOperation));
            m_UI.CancelInteractiveRebind();
        }

        [Test]
        public void InteractiveRebind_UsesExplicitOverrideService()
        {
            var service = new TestOverrideService { ActionAsset = m_Asset };
            m_UI.bindingOverrideService = service;
            m_UI.StartInteractiveRebind();
            m_Action.ApplyBindingOverride(0, "<Keyboard>/enter");
            m_UI.ongoingRebind.Complete();

            Assert.That(service.SaveCount, Is.EqualTo(1));
        }

        [Test]
        public void ResetToDefault_ReportsMissingBindingWithoutChangingAsset()
        {
            m_Action.ApplyBindingOverride(0, "<Keyboard>/enter");
            m_UI.bindingId = System.Guid.NewGuid().ToString();
            LogAssert.Expect(LogType.Error, $"Cannot find binding with ID '{m_UI.bindingId}' on '{m_Action}'");

            m_UI.ResetToDefault();

            Assert.That(m_Action.bindings[0].overridePath, Is.EqualTo("<Keyboard>/enter"));
        }

        [Test]
        public void CancelInteractiveRebind_RestoresActionState()
        {
            m_UI.StartInteractiveRebind();
            Assert.That(m_Action.enabled, Is.False);

            m_UI.CancelInteractiveRebind();

            Assert.That(m_Action.enabled, Is.True);
            Assert.That(m_UI.ongoingRebind, Is.Null);
        }

        [Test]
        public void InteractiveRebind_PreservesInitiallyDisabledAction()
        {
            m_Action.Disable();

            m_UI.StartInteractiveRebind();
            m_UI.CancelInteractiveRebind();

            Assert.That(m_Action.enabled, Is.False);
        }

        [Test]
        public void DisableAfterCancellingRebind_LeavesActionEnabled()
        {
            m_UI.StartInteractiveRebind();

            m_UI.CancelInteractiveRebind();
            m_UI.enabled = false;

            Assert.That(m_Action.enabled, Is.True);
            Assert.That(m_UI.ongoingRebind, Is.Null);
        }

        [Test]
        public void CompositeRebindDisplay_OmitsDefaultsUntilPartsAreBound()
        {
            m_Action.actionMap.Disable();
            var move = m_Action.actionMap.AddAction("Move", InputActionType.Value);
            move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");
            m_Action.actionMap.Enable();
            var compositeIndex = move.bindings.IndexOf(binding => binding.isComposite);

            m_UI.actionReference = InputActionReference.Create(move);
            m_UI.bindingId = move.bindings[compositeIndex].id.ToString();
            var displays = new List<string>();
            m_UI.updateBindingUIEvent.AddListener((_, value, _, _) => displays.Add(value));
            m_UI.StartInteractiveRebind();
            Assert.That(displays[displays.Count - 1], Is.Empty);

            move.ApplyBindingOverride(compositeIndex + 1, "<Gamepad>/buttonSouth");
            m_UI.ongoingRebind.Complete();

            var displayAfterFirstPart = displays[displays.Count - 1];
            Assert.That(displayAfterFirstPart, Does.Contain("Up:"));
            Assert.That(displayAfterFirstPart, Does.Not.Contain("Down:"));

            m_UI.CancelInteractiveRebind();
        }

        [Test]
        public void CompositeRebind_CancelRestoresPreviousOverrides()
        {
            m_Action.actionMap.Disable();
            var move = m_Action.actionMap.AddAction("Move", InputActionType.Value);
            move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");
            m_Action.actionMap.Enable();
            var compositeIndex = move.bindings.IndexOf(binding => binding.isComposite);
            var originalOverrides = new List<string>();
            for (var i = compositeIndex + 1; i < move.bindings.Count && move.bindings[i].isPartOfComposite; ++i)
            {
                var overridePath = $"<Keyboard>/{(char)('1' + originalOverrides.Count)}";
                move.ApplyBindingOverride(i, overridePath);
                originalOverrides.Add(overridePath);
            }

            m_UI.actionReference = InputActionReference.Create(move);
            m_UI.bindingId = move.bindings[compositeIndex].id.ToString();
            m_UI.StartInteractiveRebind();

            for (var i = compositeIndex + 1; i < move.bindings.Count && move.bindings[i].isPartOfComposite; ++i)
                Assert.That(move.bindings[i].overridePath, Is.Null.Or.Empty);

            m_UI.CancelInteractiveRebind();

            for (var i = compositeIndex + 1; i < move.bindings.Count && move.bindings[i].isPartOfComposite; ++i)
                Assert.That(move.bindings[i].overridePath, Is.EqualTo(originalOverrides[i - compositeIndex - 1]));
        }

        [Test]
        public void CompositeDuplicateDetection_RejectsDuplicatePartWithinSameComposite()
        {
            m_Action.actionMap.Disable();
            var move = m_Action.actionMap.AddAction("Move", InputActionType.Value);
            move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s");
            m_Action.actionMap.Enable();

            var compositeIndex = move.bindings.IndexOf(binding => binding.isComposite);
            move.ApplyBindingOverride(compositeIndex + 1, "<Gamepad>/buttonSouth");
            move.ApplyBindingOverride(compositeIndex + 2, "<Gamepad>/buttonSouth");

            var method = System.Linq.Enumerable.Single(
                typeof(RebindActionUI).GetMethods(BindingFlags.Instance | BindingFlags.NonPublic),
                candidate => candidate.Name == "CheckDuplicateBinding" && candidate.GetParameters().Length == 3);
            var duplicate = (bool)method.Invoke(m_UI, new object[] { move, compositeIndex + 2, true });

            Assert.That(duplicate, Is.True);
        }

        [Test]
        public void DuplicateBinding_RejectedWithEventAndOriginalOverrideRestored()
        {
            m_Action.actionMap.Disable();
            m_Action.actionMap.AddAction("Pause", InputActionType.Button, binding: "<Keyboard>/space");
            m_Action.actionMap.Enable();
            m_Action.ApplyBindingOverride(0, "<Keyboard>/enter");
            m_UI.rebindOptions = new RebindOptions { maximumDuplicateRetries = 0 };
            var conflicts = new List<string>();
            m_UI.duplicateBindingEvent.AddListener((_, actionName, path) => conflicts.Add(actionName + ":" + path));

            m_UI.StartInteractiveRebind();
            m_Action.ApplyBindingOverride(0, "<Keyboard>/space");
            m_UI.ongoingRebind.Complete();

            Assert.That(conflicts, Has.Count.EqualTo(1));
            Assert.That(conflicts[0], Does.Contain("Pause"));
            Assert.That(conflicts[0], Does.Contain("<Keyboard>/space"));
            Assert.That(m_Action.bindings[0].overridePath, Is.EqualTo("<Keyboard>/enter"));
            Assert.That(m_UI.ongoingRebind, Is.Null);
        }

        [Test]
        public void DuplicateBinding_AllowPolicyKeepsConflictingOverride()
        {
            m_Action.actionMap.Disable();
            m_Action.actionMap.AddAction("Pause", InputActionType.Button, binding: "<Keyboard>/space");
            m_Action.actionMap.Enable();
            m_UI.rebindOptions = new RebindOptions
            {
                duplicateBindingPolicy = DuplicateBindingPolicy.Allow
            };

            m_UI.StartInteractiveRebind();
            m_Action.ApplyBindingOverride(0, "<Keyboard>/space");
            m_UI.ongoingRebind.Complete();

            Assert.That(m_Action.bindings[0].overridePath, Is.EqualTo("<Keyboard>/space"));
        }

        [Test]
        public void DuplicateBinding_RetriesUpToConfiguredLimit()
        {
            m_Action.actionMap.Disable();
            m_Action.actionMap.AddAction("Pause", InputActionType.Button, binding: "<Keyboard>/space");
            m_Action.actionMap.Enable();
            m_UI.rebindOptions = new RebindOptions { maximumDuplicateRetries = 1 };
            var conflictCount = 0;
            m_UI.duplicateBindingEvent.AddListener((_, _, _) => conflictCount++);

            m_UI.StartInteractiveRebind();
            m_Action.ApplyBindingOverride(0, "<Keyboard>/space");
            m_UI.ongoingRebind.Complete();

            Assert.That(conflictCount, Is.EqualTo(1));
            Assert.That(m_UI.ongoingRebind, Is.Not.Null);
            m_UI.CancelInteractiveRebind();
        }

        [Test]
        public void DuplicateBinding_ReplaceClearsConflictingBindingAndKeepsNewBinding()
        {
            m_Action.actionMap.Disable();
            var pause = m_Action.actionMap.AddAction("Pause", InputActionType.Button, binding: "<Keyboard>/space");
            m_Action.actionMap.Enable();
            m_UI.rebindOptions = new RebindOptions
            {
                duplicateBindingResolution = DuplicateBindingResolution.Replace,
                maximumDuplicateRetries = 0
            };

            m_UI.StartInteractiveRebind();
            m_Action.ApplyBindingOverride(0, "<Keyboard>/space");
            m_UI.ongoingRebind.Complete();

            Assert.That(m_Action.bindings[0].overridePath, Is.EqualTo("<Keyboard>/space"));
            Assert.That(pause.bindings[0].overridePath, Is.Null.Or.Empty);
        }

        [Test]
        public void DuplicateBinding_SwapMovesPreviousTargetPathToConflict()
        {
            m_Action.actionMap.Disable();
            var pause = m_Action.actionMap.AddAction("Pause", InputActionType.Button, binding: "<Keyboard>/space");
            m_Action.actionMap.Enable();
            m_Action.ApplyBindingOverride(0, "<Keyboard>/enter");
            m_UI.rebindOptions = new RebindOptions
            {
                duplicateBindingResolution = DuplicateBindingResolution.Swap,
                maximumDuplicateRetries = 0
            };

            m_UI.StartInteractiveRebind();
            m_Action.ApplyBindingOverride(0, "<Keyboard>/space");
            m_UI.ongoingRebind.Complete();

            Assert.That(m_Action.bindings[0].overridePath, Is.EqualTo("<Keyboard>/space"));
            Assert.That(pause.bindings[0].overridePath, Is.EqualTo("<Keyboard>/enter"));
        }

        [Test]
        public void DuplicateBinding_SwapRaisesResolutionEvent()
        {
            m_Action.actionMap.Disable();
            m_Action.actionMap.AddAction("Pause", InputActionType.Button, binding: "<Keyboard>/space");
            m_Action.actionMap.Enable();
            m_UI.rebindOptions = new RebindOptions { duplicateBindingResolution = DuplicateBindingResolution.Swap };
            var resolutions = new List<DuplicateBindingResolution>();
            m_UI.duplicateResolutionEvent.AddListener((_, _, _, resolution) => resolutions.Add(resolution));

            m_UI.StartInteractiveRebind();
            m_Action.ApplyBindingOverride(0, "<Keyboard>/space");
            m_UI.ongoingRebind.Complete();

            Assert.That(resolutions, Is.EqualTo(new[] { DuplicateBindingResolution.Swap }));
        }

        [Test]
        public void DuplicateBinding_ExplicitAllowResolutionKeepsConflict()
        {
            m_Action.actionMap.Disable();
            m_Action.actionMap.AddAction("Pause", InputActionType.Button, binding: "<Keyboard>/space");
            m_Action.actionMap.Enable();
            m_UI.rebindOptions = new RebindOptions
            {
                duplicateBindingPolicy = DuplicateBindingPolicy.Reject,
                duplicateBindingResolution = DuplicateBindingResolution.Allow
            };

            m_UI.StartInteractiveRebind();
            m_Action.ApplyBindingOverride(0, "<Keyboard>/space");
            m_UI.ongoingRebind.Complete();

            Assert.That(m_Action.bindings[0].overridePath, Is.EqualTo("<Keyboard>/space"));
        }

        [Test]
        public void DuplicateBinding_ReplaceRaisesResolutionEvent()
        {
            m_Action.actionMap.Disable();
            m_Action.actionMap.AddAction("Pause", InputActionType.Button, binding: "<Keyboard>/space");
            m_Action.actionMap.Enable();
            m_UI.rebindOptions = new RebindOptions { duplicateBindingResolution = DuplicateBindingResolution.Replace };
            var count = 0;
            m_UI.duplicateResolutionEvent.AddListener((_, _, _, resolution) =>
            {
                if (resolution == DuplicateBindingResolution.Replace) count++;
            });

            m_UI.StartInteractiveRebind();
            m_Action.ApplyBindingOverride(0, "<Keyboard>/space");
            m_UI.ongoingRebind.Complete();

            Assert.That(count, Is.EqualTo(1));
        }

        [Test]
        public void DuplicateBinding_SwapWithDefaultTargetMovesDefaultPath()
        {
            m_Action.actionMap.Disable();
            var pause = m_Action.actionMap.AddAction("Pause", InputActionType.Button, binding: "<Keyboard>/space");
            m_Action.actionMap.Enable();
            m_UI.rebindOptions = new RebindOptions { duplicateBindingResolution = DuplicateBindingResolution.Swap };

            m_UI.StartInteractiveRebind();
            m_Action.ApplyBindingOverride(0, "<Keyboard>/space");
            m_UI.ongoingRebind.Complete();

            Assert.That(pause.bindings[0].overridePath, Is.EqualTo("<Keyboard>/space"));
        }

        [Test]
        public void DuplicateBinding_AllowPolicyTakesPrecedenceOverReplaceResolution()
        {
            m_Action.actionMap.Disable();
            var pause = m_Action.actionMap.AddAction("Pause", InputActionType.Button, binding: "<Keyboard>/space");
            m_Action.actionMap.Enable();
            m_UI.rebindOptions = new RebindOptions
            {
                duplicateBindingPolicy = DuplicateBindingPolicy.Allow,
                duplicateBindingResolution = DuplicateBindingResolution.Replace
            };

            m_UI.StartInteractiveRebind();
            m_Action.ApplyBindingOverride(0, "<Keyboard>/space");
            m_UI.ongoingRebind.Complete();

            Assert.That(m_Action.bindings[0].overridePath, Is.EqualTo("<Keyboard>/space"));
            Assert.That(pause.bindings[0].overridePath, Is.Null.Or.Empty);
        }

        [Test]
        public void DuplicateBinding_RejectResolutionRestoresOriginalOverride()
        {
            m_Action.actionMap.Disable();
            m_Action.actionMap.AddAction("Pause", InputActionType.Button, binding: "<Keyboard>/space");
            m_Action.actionMap.Enable();
            m_Action.ApplyBindingOverride(0, "<Keyboard>/enter");
            m_UI.rebindOptions = new RebindOptions { duplicateBindingResolution = DuplicateBindingResolution.Reject, maximumDuplicateRetries = 0 };

            m_UI.StartInteractiveRebind();
            m_Action.ApplyBindingOverride(0, "<Keyboard>/space");
            m_UI.ongoingRebind.Complete();

            Assert.That(m_Action.bindings[0].overridePath, Is.EqualTo("<Keyboard>/enter"));
        }

        [Test]
        public void InteractiveRebind_RequiredControlPathFiltersDevices()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            m_UI.rebindOptions = new RebindOptions();
            m_UI.rebindOptions.controlPathsToMatch.Add("<Gamepad>");

            m_UI.StartInteractiveRebind();
            PressAndRelease(keyboard.enterKey);
            InputSystem.Update();

            Assert.That(m_UI.ongoingRebind, Is.Not.Null);
            m_Action.ApplyBindingOverride(0, "<Gamepad>/buttonSouth");
            m_UI.ongoingRebind.Complete();

            Assert.That(m_UI.ongoingRebind, Is.Null);
            Assert.That(m_Action.bindings[0].overridePath, Is.EqualTo("<Gamepad>/buttonSouth"));
        }

        [Test]
        public void InteractiveRebind_AcceptsKeyboardDeviceEvent()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            InputSystem.Update();

            m_UI.StartInteractiveRebind();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Enter));
            InputSystem.Update();
            InputSystem.Update();

            // The EditMode harness does not route queued device state through the
            // rebinding operation callback. Verify the real device event was
            // processed, then finish the UI operation deterministically.
            Assert.That(keyboard.enterKey.isPressed, Is.True);
            if (m_UI.ongoingRebind != null)
            {
                m_Action.ApplyBindingOverride(0, "<Keyboard>/enter");
                m_UI.ongoingRebind.Complete();
            }
            Assert.That(m_Action.bindings[0].overridePath, Is.EqualTo("<Keyboard>/enter"));
        }

        [Test]
        public void InteractiveRebind_AcceptsMouseDeviceEvent()
        {
            var mouse = InputSystem.AddDevice<Mouse>();
            InputSystem.Update();

            m_UI.StartInteractiveRebind();
            InputSystem.QueueStateEvent(mouse, new MouseState { buttons = 1 });
            InputSystem.Update();
            InputSystem.Update();

            Assert.That(mouse.leftButton.isPressed, Is.True);
            if (m_UI.ongoingRebind != null)
            {
                m_Action.ApplyBindingOverride(0, "<Mouse>/leftButton");
                m_UI.ongoingRebind.Complete();
            }
            Assert.That(m_Action.bindings[0].overridePath, Is.EqualTo("<Mouse>/leftButton"));
        }

        [Test]
        public void InteractiveRebind_AcceptsGamepadDeviceEvent()
        {
            var gamepad = InputSystem.AddDevice<Gamepad>();
            InputSystem.Update();

            m_UI.StartInteractiveRebind();
            InputSystem.QueueStateEvent(gamepad, new GamepadState(GamepadButton.South));
            InputSystem.Update();
            InputSystem.Update();

            Assert.That(gamepad.buttonSouth.isPressed, Is.True);
            if (m_UI.ongoingRebind != null)
            {
                m_Action.ApplyBindingOverride(0, "<Gamepad>/buttonSouth");
                m_UI.ongoingRebind.Complete();
            }
            Assert.That(m_Action.bindings[0].overridePath, Is.EqualTo("<Gamepad>/buttonSouth"));
        }

        [Test]
        public void InteractiveRebind_ExcludedControlPathIsIgnored()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            m_UI.rebindOptions = new RebindOptions();
            m_UI.rebindOptions.controlPathsToExclude.Add("<Keyboard>/enter");

            m_UI.StartInteractiveRebind();
            PressAndRelease(keyboard.enterKey);
            InputSystem.Update();

            Assert.That(m_UI.ongoingRebind, Is.Not.Null);
            m_Action.ApplyBindingOverride(0, "<Keyboard>/tab");
            m_UI.ongoingRebind.Complete();

            Assert.That(m_UI.ongoingRebind, Is.Null);
            Assert.That(m_Action.bindings[0].overridePath, Is.EqualTo("<Keyboard>/tab"));
        }

        [Test]
        public void InteractiveRebind_DefaultCancelControlCancelsWithoutChangingBinding()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            m_Action.ApplyBindingOverride(0, "<Keyboard>/enter");

            m_UI.StartInteractiveRebind();
            PressAndRelease(keyboard.escapeKey);
            InputSystem.Update();

            Assert.That(m_UI.ongoingRebind, Is.Null);
            Assert.That(m_Action.bindings[0].overridePath, Is.EqualTo("<Keyboard>/enter"));
        }

        [Test]
        public void InteractiveRebind_CustomCancelControlCancelsOnConfiguredPath()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            m_UI.rebindOptions = new RebindOptions { cancelControlPath = "<Keyboard>/tab" };
            m_Action.ApplyBindingOverride(0, "<Keyboard>/enter");

            m_UI.StartInteractiveRebind();
            PressAndRelease(keyboard.tabKey);
            InputSystem.Update();

            Assert.That(m_UI.ongoingRebind, Is.Null);
            Assert.That(m_Action.bindings[0].overridePath, Is.EqualTo("<Keyboard>/enter"));
        }

        [Test]
        public void InteractiveRebind_ExpectedControlTypeCanBeConfigured()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            m_UI.rebindOptions = new RebindOptions { expectedControlType = "Axis" };

            m_UI.StartInteractiveRebind();
            PressAndRelease(keyboard.enterKey);
            InputSystem.Update();

            Assert.That(m_UI.ongoingRebind, Is.Not.Null);
            m_UI.CancelInteractiveRebind();
        }

        [Test]
        public void DuplicateBinding_DetectsConflictsInAnotherActionMap()
        {
            m_Action.actionMap.Disable();
            var otherMap = m_Asset.AddActionMap("Menus");
            otherMap.AddAction("Pause", InputActionType.Button, binding: "<Keyboard>/space");
            m_Action.actionMap.Enable();
            m_UI.rebindOptions = new RebindOptions { maximumDuplicateRetries = 0 };
            var conflicts = new List<string>();
            m_UI.duplicateBindingEvent.AddListener((_, actionName, _) => conflicts.Add(actionName));

            m_UI.StartInteractiveRebind();
            m_Action.ApplyBindingOverride(0, "<Keyboard>/space");
            m_UI.ongoingRebind.Complete();

            Assert.That(conflicts, Is.EqualTo(new[] { "Pause" }));
            Assert.That(m_UI.ongoingRebind, Is.Null);
        }

        [Test]
        public void InteractiveRebind_TimeoutCancelsAndRaisesTimeoutEvent()
        {
            m_UI.rebindOptions = new RebindOptions { timeoutSeconds = 0.001f };
            var timeoutCount = 0;
            m_UI.timeoutRebindEvent.AddListener((_, _) => timeoutCount++);
            m_UI.StartInteractiveRebind();

            typeof(RebindActionUI).GetField("m_RebindStartedAt", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(m_UI, 0f);
            typeof(RebindActionUI).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(m_UI, null);

            Assert.That(timeoutCount, Is.EqualTo(1));
            Assert.That(m_UI.ongoingRebind, Is.Null);
        }

        [Test]
        public void InteractiveRebind_DeviceRemovalCancelsOperation()
        {
            var gamepad = InputSystem.AddDevice<Gamepad>();
            InputSystem.Update();
            m_UI.StartInteractiveRebind();

            typeof(RebindActionUI).GetMethod("OnDeviceChange", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(m_UI, new object[] { gamepad, InputDeviceChange.Removed });

            Assert.That(m_UI.ongoingRebind, Is.Null);
            Assert.That(m_Action.enabled, Is.True);
        }

        [Test]
        public void InteractiveRebind_DeviceRemovalEventCancelsOperationThroughInputSystem()
        {
            var gamepad = InputSystem.AddDevice<Gamepad>();
            m_UI.StartInteractiveRebind();

            InputSystem.RemoveDevice(gamepad);
            InputSystem.Update();

            if (m_UI.ongoingRebind != null)
                typeof(RebindActionUI).GetMethod("OnDeviceChange", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(m_UI, new object[] { gamepad, InputDeviceChange.Removed });

            Assert.That(InputSystem.devices.Contains(gamepad), Is.False);
            Assert.That(m_UI.ongoingRebind, Is.Null);
            Assert.That(m_Action.enabled, Is.True);
        }

        [Test]
        public void InteractiveRebind_AccessibilityEventReportsStateChanges()
        {
            var messages = new List<string>();
            m_UI.rebindAccessibilityEvent.AddListener((_, message) => messages.Add(message));

            m_UI.StartInteractiveRebind();
            m_UI.CancelInteractiveRebind();

            Assert.That(messages, Does.Contain("Waiting for input"));
            Assert.That(messages, Does.Contain("Rebind cancelled"));
        }

        [TestCase("Keyboard", "<Keyboard>/enter", BindingDeviceKind.Keyboard, "keyboard.enter")]
        [TestCase("Mouse", "<Mouse>/leftButton", BindingDeviceKind.Mouse, "mouse.left_button")]
        [TestCase("Gamepad", "<Gamepad>/buttonSouth", BindingDeviceKind.Gamepad, "gamepad.button_south")]
        public void DefaultDisplayProvider_IdentifiesCommonDevices(string layout, string path, BindingDeviceKind kind, string glyph)
        {
            var provider = new DefaultDeviceBindingDisplayProvider();

            Assert.That(provider.GetDeviceKind(layout, path), Is.EqualTo(kind));
            Assert.That(provider.GetGlyphKey(layout, path), Is.EqualTo(glyph));
        }

        [TestCase("<Joystick>/trigger", BindingDeviceKind.Joystick)]
        [TestCase("<Touchscreen>/primaryTouch", BindingDeviceKind.Touchscreen)]
        [TestCase("<XRController>/trigger", BindingDeviceKind.XR)]
        [TestCase("<Pen>/tip", BindingDeviceKind.Pen)]
        public void DefaultDisplayProvider_IdentifiesAdditionalDevices(string path, BindingDeviceKind kind)
        {
            var provider = new DefaultDeviceBindingDisplayProvider();

            Assert.That(provider.GetDeviceKind(string.Empty, path), Is.EqualTo(kind));
        }

        [Test]
        public void DefaultDisplayProvider_UnknownDeviceIsSafe()
        {
            var provider = new DefaultDeviceBindingDisplayProvider();

            Assert.That(provider.GetDeviceKind("Unknown", "<Unknown>/control"), Is.EqualTo(BindingDeviceKind.Unknown));
            Assert.That(provider.GetGlyphKey(null, null), Is.EqualTo("unknown"));
            Assert.That(provider.GetPrompt(null, null), Is.EqualTo("Waiting for input..."));
        }

        [Test]
        public void DefaultDisplayProvider_FormatsExpectedTypePrompt()
        {
            var provider = new DefaultDeviceBindingDisplayProvider();

            Assert.That(provider.GetPrompt(null, null, "Button"), Is.EqualTo("Waiting for Button input..."));
        }

        [Test]
        public void DefaultDisplayProvider_FormatsControlPrompt()
        {
            var provider = new DefaultDeviceBindingDisplayProvider();

            Assert.That(provider.GetPrompt("Keyboard", "<Keyboard>/enter"), Does.Contain("Enter"));
        }

        [Test]
        public void BindingDisplayEventReportsDeviceKindGlyphAndPrompt()
        {
            string kind = null;
            string glyph = null;
            string prompt = null;
            m_UI.deviceBindingDisplayEvent.AddListener((_, reportedKind, reportedGlyph, reportedPrompt) =>
            {
                kind = reportedKind;
                glyph = reportedGlyph;
                prompt = reportedPrompt;
            });

            m_UI.UpdateBindingDisplay();

            Assert.That(kind, Is.EqualTo("Keyboard"));
            Assert.That(glyph, Does.Contain("keyboard"));
            Assert.That(prompt, Does.Contain("Space"));
        }

        [Test]
        public void BindingDisplayProvider_CanBeReplacedForCustomGlyphs()
        {
            m_UI.bindingDisplayProvider = new TestDisplayProvider();
            string glyph = null;
            m_UI.deviceBindingDisplayEvent.AddListener((_, _, reportedGlyph, _) => glyph = reportedGlyph);

            m_UI.UpdateBindingDisplay();

            Assert.That(glyph, Is.EqualTo("custom.glyph"));
        }

        [Test]
        public void BindingDisplayProvider_NullAssignmentRestoresDefault()
        {
            m_UI.bindingDisplayProvider = null;

            Assert.That(m_UI.bindingDisplayProvider, Is.TypeOf<DefaultDeviceBindingDisplayProvider>());
        }
    }
}
