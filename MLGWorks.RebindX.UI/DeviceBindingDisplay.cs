using System;
using UnityEngine.InputSystem;

namespace MLGWorks.RebindX.Runtime
{
    public enum BindingDeviceKind { Unknown, Keyboard, Mouse, Gamepad, Joystick, Touchscreen, XR, Pen }

    public interface IDeviceBindingDisplayProvider
    {
        BindingDeviceKind GetDeviceKind(string deviceLayoutName, string controlPath);
        string GetGlyphKey(string deviceLayoutName, string controlPath);
        string GetPrompt(string deviceLayoutName, string controlPath, string expectedControlType = null);
    }

    [Serializable]
    public sealed class DefaultDeviceBindingDisplayProvider : IDeviceBindingDisplayProvider
    {
        public BindingDeviceKind GetDeviceKind(string deviceLayoutName, string controlPath)
        {
            var value = (deviceLayoutName + " " + controlPath).ToLowerInvariant();
            if (value.Contains("gamepad") || value.Contains("xinput") || value.Contains("dualshock") || value.Contains("switch")) return BindingDeviceKind.Gamepad;
            if (value.Contains("mouse")) return BindingDeviceKind.Mouse;
            if (value.Contains("keyboard") || value.Contains("key/")) return BindingDeviceKind.Keyboard;
            if (value.Contains("joystick")) return BindingDeviceKind.Joystick;
            if (value.Contains("touch")) return BindingDeviceKind.Touchscreen;
            if (value.Contains("xrcontroller") || value.Contains("trackeddevice")) return BindingDeviceKind.XR;
            if (value.Contains("pen")) return BindingDeviceKind.Pen;
            return BindingDeviceKind.Unknown;
        }

        public string GetGlyphKey(string deviceLayoutName, string controlPath)
        {
            var kind = GetDeviceKind(deviceLayoutName, controlPath).ToString().ToLowerInvariant();
            var control = InputControlPath.ToHumanReadableString(controlPath ?? string.Empty,
                InputControlPath.HumanReadableStringOptions.OmitDevice);
            control = control.Replace(" ", "_").Replace("/", "_").ToLowerInvariant();
            return string.IsNullOrEmpty(control) ? kind : kind + "." + control;
        }

        public string GetPrompt(string deviceLayoutName, string controlPath, string expectedControlType = null)
        {
            var control = InputControlPath.ToHumanReadableString(controlPath ?? string.Empty,
                InputControlPath.HumanReadableStringOptions.OmitDevice);
            if (!string.IsNullOrEmpty(control)) return "Press " + char.ToUpperInvariant(control[0]) + control.Substring(1);
            return string.IsNullOrWhiteSpace(expectedControlType) ? "Waiting for input..." : "Waiting for " + expectedControlType + " input...";
        }
    }
}
