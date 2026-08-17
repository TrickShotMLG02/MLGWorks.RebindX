using System;
using System.Collections.Generic;
using UnityEngine.InputSystem;

namespace MLGWorks.RebindX.Runtime
{
    /// <summary>
    /// Owns the enabled-state transition for one interactive rebind operation.
    /// </summary>
    public sealed class RebindSession : IDisposable
    {
        private InputAction m_Action;
        private InputActionMap m_ActionMap;
        private InputActionAsset m_ActionAsset;
        private bool m_WasEnabled;
        private bool m_ActionMapWasEnabled;
        private bool m_ActionAssetWasEnabled;
        private Dictionary<InputAction, bool> m_ActionStates;

        public InputAction Action => m_Action;
        public bool IsActive => m_Action != null;

        public void Begin(InputAction action)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));
            if (IsActive)
                throw new InvalidOperationException("A rebind session is already active.");

            m_Action = action;
            m_ActionMap = action.actionMap;
            m_ActionAsset = m_ActionMap?.asset;
            m_WasEnabled = action.enabled;
            m_ActionMapWasEnabled = m_ActionMap?.enabled ?? false;
            m_ActionAssetWasEnabled = m_ActionAsset?.enabled ?? false;
            m_ActionStates = new Dictionary<InputAction, bool>();
            if (m_ActionMap != null)
            {
                foreach (var mapAction in m_ActionMap.actions)
                    m_ActionStates[mapAction] = mapAction.enabled;
            }
            action.Disable();
        }

        public void Complete()
        {
            if (!IsActive)
                return;

            if (m_ActionMap != null)
            {
                if (!m_ActionMapWasEnabled)
                    m_ActionMap.Disable();
            }

            if (m_ActionStates != null)
            {
                foreach (var actionState in m_ActionStates)
                {
                    if (actionState.Value)
                        actionState.Key.Enable();
                    else
                        actionState.Key.Disable();
                }
            }
            else if (m_WasEnabled)
                m_Action.Enable();
            else
                m_Action.Disable();

            if (m_ActionAsset != null && !m_ActionAssetWasEnabled)
            {
                m_ActionAsset.Disable();
            }

            m_Action = null;
            m_ActionMap = null;
            m_ActionAsset = null;
            m_WasEnabled = false;
            m_ActionMapWasEnabled = false;
            m_ActionAssetWasEnabled = false;
            m_ActionStates = null;
        }

        public void Cancel() => Complete();
        public void Dispose() => Complete();
    }
}
