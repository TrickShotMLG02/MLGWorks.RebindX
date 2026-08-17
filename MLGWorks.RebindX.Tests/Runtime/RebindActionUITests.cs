using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
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

            public void SaveRebinds() => SaveCount++;
            public void LoadRebinds() => LoadCount++;
        }

        private InputActionAsset m_Asset;
        private InputAction m_Action;
        private RebindActionUI m_UI;

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

            var method = typeof(RebindActionUI).GetMethod("CheckDuplicateBinding", BindingFlags.Instance | BindingFlags.NonPublic);
            LogAssert.Expect(LogType.Log, "Duplicate composite binding found at <Gamepad>/buttonSouth");
            var duplicate = (bool)method.Invoke(m_UI, new object[] { move, compositeIndex + 2, true });

            Assert.That(duplicate, Is.True);
        }
    }
}
