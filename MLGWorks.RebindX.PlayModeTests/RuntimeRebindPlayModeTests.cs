using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Utilities;
using UnityEngine.TestTools;
using MLGWorks.RebindX.Runtime;

namespace MLGWorks.RebindX.PlayModeTests
{
    public sealed class RuntimeRebindPlayModeTests : InputTestFixture
    {
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

            var gameObject = new GameObject("PlayMode RebindActionUI");
            m_UI = gameObject.AddComponent<RebindActionUI>();
            m_UI.actionReference = InputActionReference.Create(m_Action);
            m_UI.bindingId = m_Action.bindings[0].id.ToString();
        }

        [TearDown]
        public override void TearDown()
        {
            if (m_UI != null)
                Object.Destroy(m_UI.gameObject);
            if (m_Asset != null)
                Object.Destroy(m_Asset);

            base.TearDown();
        }

        [UnityTest]
        public IEnumerator CancelRestoresActionStateInRuntime()
        {
            yield return null;

            m_UI.StartInteractiveRebind();
            Assert.That(m_Action.enabled, Is.False);

            m_UI.CancelInteractiveRebind();
            yield return null;

            Assert.That(m_Action.enabled, Is.True);
            Assert.That(m_UI.ongoingRebind, Is.Null);
        }

        [UnityTest]
        public IEnumerator KeyboardDeviceCanBeUsedDuringRuntimeRebind()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            yield return null;

            m_UI.StartInteractiveRebind();
            Assert.That(keyboard, Is.Not.Null);
            Assert.That(m_UI.ongoingRebind, Is.Not.Null);

            m_Action.ApplyBindingOverride(0, "<Keyboard>/enter");
            m_UI.ongoingRebind.Complete();

            Assert.That(m_Action.bindings[0].overridePath, Is.EqualTo("<Keyboard>/enter"));
        }

        [UnityTest]
        public IEnumerator DeviceRemovalCancelsInteractiveRebindInRuntime()
        {
            var gamepad = InputSystem.AddDevice<Gamepad>();
            yield return null;

            m_UI.StartInteractiveRebind();
            InputSystem.RemoveDevice(gamepad);
            yield return null;

            Assert.That(InputSystem.devices.Any(device => device == gamepad), Is.False);
            Assert.That(m_UI.ongoingRebind, Is.Null);
            Assert.That(m_Action.enabled, Is.True);
        }
    }
}
