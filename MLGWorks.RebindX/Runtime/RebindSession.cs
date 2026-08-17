using System;
using UnityEngine.InputSystem;

namespace MLGWorks.RebindX.Runtime
{
    /// <summary>
    /// Owns the enabled-state transition for one interactive rebind operation.
    /// </summary>
    public sealed class RebindSession : IDisposable
    {
        private InputAction m_Action;
        private bool m_WasEnabled;

        public InputAction Action => m_Action;
        public bool IsActive => m_Action != null;

        public void Begin(InputAction action)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));
            if (IsActive)
                throw new InvalidOperationException("A rebind session is already active.");

            m_Action = action;
            m_WasEnabled = action.enabled;
            action.Disable();
        }

        public void Complete()
        {
            if (!IsActive)
                return;

            if (m_WasEnabled)
                m_Action.Enable();
            else
                m_Action.Disable();

            m_Action = null;
            m_WasEnabled = false;
        }

        public void Cancel() => Complete();
        public void Dispose() => Complete();
    }
}
