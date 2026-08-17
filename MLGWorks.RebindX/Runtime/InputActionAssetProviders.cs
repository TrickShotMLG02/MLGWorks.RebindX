using System;
using UnityEngine.InputSystem;

namespace MLGWorks.RebindX.Runtime
{
    public interface IInputActionAssetProvider : IDisposable
    {
        InputActionAsset Asset { get; }
        void Enable();
        void Disable();
    }

    public sealed class InputActionAssetProvider : IInputActionAssetProvider
    {
        public InputActionAssetProvider(InputActionAsset asset)
        {
            Asset = asset ?? throw new ArgumentNullException(nameof(asset));
        }

        public InputActionAsset Asset { get; }

        public void Enable() => Asset.Enable();
        public void Disable() => Asset.Disable();
        public void Dispose() => Disable();
    }

    public sealed class GeneratedControlsProvider : IInputActionAssetProvider
    {
        private readonly PlayerInputControls m_Controls;

        public GeneratedControlsProvider(PlayerInputControls controls)
        {
            m_Controls = controls ?? throw new ArgumentNullException(nameof(controls));
        }

        public PlayerInputControls Controls => m_Controls;
        public InputActionAsset Asset => m_Controls.asset;

        public void Enable() => m_Controls.Enable();
        public void Disable() => m_Controls.Disable();
        public void Dispose() => m_Controls.Dispose();
    }
}
