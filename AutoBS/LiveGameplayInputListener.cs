using AutoBS.Patches;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.XR;
using static AutoBS.Config;

namespace AutoBS
{
    // listens for live buttons and thumbstick input during gameplay to adjust NJS and volume on the fly.
    public class LiveGameplayInputListener : MonoBehaviour
    {
        private InputDevice _leftHand;
        private InputDevice _rightHand;

        private bool _prevLeftX;
        private bool _prevLeftY;
        private bool _prevRightA;
        private bool _prevRightB;

        private bool _leftStickForwardLatched;
        private bool _leftStickBackwardLatched;
        private bool _leftStickRightLatched;
        private bool _leftStickLeftLatched;

        private bool _rightStickForwardLatched;
        private bool _rightStickBackwardLatched;
        private bool _rightStickRightLatched;
        private bool _rightStickLeftLatched;

        private const float StickTriggerThreshold = 0.8f;
        private const float StickReleaseThreshold = 0.5f;
        private const float StickDeadzone = 0.2f;

        private void OnEnable()
        {
            RefreshDevices();
        }

        private void Update()
        {
            if (!LiveGameplayRuntimeState.SongRunning)
                return;

            if (!Config.Instance.EnablePlugin)
                return;

            bool volumeEnabled = Config.Instance.EnableLiveVolumeControl &&
                         Config.Instance.LiveVolumeControl != Config.LiveControlModeType.Off;

            bool njsEnabled = Config.Instance.EnableLiveNjsJdControl &&
                      Config.Instance.LiveNjsControl != Config.LiveControlModeType.Off;

            bool jdEnabled = Config.Instance.EnableLiveNjsJdControl &&
                                 Config.Instance.LiveJdControl != Config.LiveControlModeType.Off;

            if (!volumeEnabled && !njsEnabled && !jdEnabled)
                return;

            EnsureDevicesValid();
            HandleButtons();
            HandleThumbsticks();
        }

        private static bool CanUseLiveVolumeControls()
        {
            return Config.Instance.EnablePlugin &&
                   Config.Instance.LiveVolumeControl != LiveControlModeType.Off;
        }

        private static bool CanUseLiveNjsControls()
        {
            return Config.Instance.EnablePlugin &&
                   Config.Instance.LiveNjsControl != LiveControlModeType.Off;
        }

        private static bool CanUseLiveJdControls()
        {
            return Config.Instance.EnablePlugin &&
                   Config.Instance.LiveJdControl != LiveControlModeType.Off;
        }

        private void HandleButtons()
        {
            bool leftX = GetPrimaryButton(_leftHand);     // X on left
            bool leftY = GetSecondaryButton(_leftHand);   // Y on left
            bool rightA = GetPrimaryButton(_rightHand);   // A on right
            bool rightB = GetSecondaryButton(_rightHand); // B on right

            if (leftX && !_prevLeftX)
            {
                ApplyButtonMode(LiveControlModeType.ButtonsXY, -1, "X");
                ApplyButtonMode(LiveControlModeType.ButtonsXA, -1, "X");
            }

            if (leftY && !_prevLeftY)
            {
                ApplyButtonMode(LiveControlModeType.ButtonsXY, +1, "Y");
                ApplyButtonMode(LiveControlModeType.ButtonsYB, -1, "Y");
            }

            if (rightA && !_prevRightA)
            {
                ApplyButtonMode(LiveControlModeType.ButtonsAB, -1, "A");
                ApplyButtonMode(LiveControlModeType.ButtonsXA, +1, "A");
            }

            if (rightB && !_prevRightB)
            {
                ApplyButtonMode(LiveControlModeType.ButtonsAB, +1, "B");
                ApplyButtonMode(LiveControlModeType.ButtonsYB, +1, "B");
            }

            _prevLeftX = leftX;
            _prevLeftY = leftY;
            _prevRightA = rightA;
            _prevRightB = rightB;
        }
        private void ApplyButtonMode(LiveControlModeType buttonMode, int dir, string label)
        {
            if (CanUseLiveVolumeControls() && Config.Instance.LiveVolumeControl == buttonMode)
            {
                LiveAudioRuntimeState.PendingVolumeSteps += dir;
                Plugin.Log.Info($"[LiveInput] {label} => queued volume {(dir > 0 ? "+2 dB" : "-2 dB")}");
            }

            if (CanUseLiveNjsControls() && Config.Instance.LiveNjsControl == buttonMode)
            {
                AutoNjsRuntimeState.PendingNjsSteps += dir;
                Plugin.Log.Info($"[LiveInput] {label} => queued NJS {(dir > 0 ? "+1" : "-1")}");
            }

            if (CanUseLiveJdControls() && Config.Instance.LiveJdControl == buttonMode)
            {
                AutoNjsRuntimeState.PendingJdSteps += dir;
                Plugin.Log.Info($"[LiveInput] {label} => queued JD {(dir > 0 ? "+2" : "-2")}");
            }
        }

        private void HandleThumbsticks()
        {
            Vector2 leftAxis = GetThumbstick(_leftHand);
            Vector2 rightAxis = GetThumbstick(_rightHand);

            HandleThumbstickWithDominance(
                axis: leftAxis,
                verticalMode: LiveControlModeType.ThumbstickLUpDown,
                horizontalMode: LiveControlModeType.ThumbstickLLeftRight,
                posVerticalLatched: ref _leftStickForwardLatched,
                negVerticalLatched: ref _leftStickBackwardLatched,
                posHorizontalLatched: ref _leftStickRightLatched,
                negHorizontalLatched: ref _leftStickLeftLatched,
                stickName: "Left");

            HandleThumbstickWithDominance(
                axis: rightAxis,
                verticalMode: LiveControlModeType.ThumbstickRUpDown,
                horizontalMode: LiveControlModeType.ThumbstickRLeftRight,
                posVerticalLatched: ref _rightStickForwardLatched,
                negVerticalLatched: ref _rightStickBackwardLatched,
                posHorizontalLatched: ref _rightStickRightLatched,
                negHorizontalLatched: ref _rightStickLeftLatched,
                stickName: "Right");
        }
        private void HandleThumbstickWithDominance(
    Vector2 axis,
    LiveControlModeType verticalMode,
    LiveControlModeType horizontalMode,
    ref bool posVerticalLatched,
    ref bool negVerticalLatched,
    ref bool posHorizontalLatched,
    ref bool negHorizontalLatched,
    string stickName)
        {
            float absX = Mathf.Abs(axis.x);
            float absY = Mathf.Abs(axis.y);

            // Release latches when stick comes back toward center
            if (axis.y <= StickReleaseThreshold)
                posVerticalLatched = false;
            if (axis.y >= -StickReleaseThreshold)
                negVerticalLatched = false;
            if (axis.x <= StickReleaseThreshold)
                posHorizontalLatched = false;
            if (axis.x >= -StickReleaseThreshold)
                negHorizontalLatched = false;

            // If neither axis is strongly engaged, do nothing
            bool yActive = absY >= StickTriggerThreshold;
            bool xActive = absX >= StickTriggerThreshold;

            if (!xActive && !yActive)
                return;

            // Dominance logic: only allow the stronger axis to trigger
            if (absY > absX)
            {
                HandleDominantAxis(
                    axisValue: axis.y,
                    mode: verticalMode,
                    positiveLatched: ref posVerticalLatched,
                    negativeLatched: ref negVerticalLatched,
                    positiveLabel: $"{stickName} stick up",
                    negativeLabel: $"{stickName} stick down");
            }
            else if (absX > absY)
            {
                HandleDominantAxis(
                    axisValue: axis.x,
                    mode: horizontalMode,
                    positiveLatched: ref posHorizontalLatched,
                    negativeLatched: ref negHorizontalLatched,
                    positiveLabel: $"{stickName} stick right",
                    negativeLabel: $"{stickName} stick left");
            }
            // If absX == absY exactly, do nothing to avoid ambiguous diagonal triggers
        }
        private void HandleDominantAxis(
            float axisValue,
            LiveControlModeType mode,
            ref bool positiveLatched,
            ref bool negativeLatched,
            string positiveLabel,
            string negativeLabel)
        {
            if (!positiveLatched && axisValue >= StickTriggerThreshold)
            {
                positiveLatched = true;
                ApplyStickMode(mode, +1, positiveLabel);
            }

            if (!negativeLatched && axisValue <= -StickTriggerThreshold)
            {
                negativeLatched = true;
                ApplyStickMode(mode, -1, negativeLabel);
            }
        }

        private void ApplyStickMode(LiveControlModeType stickMode, int dir, string label)
        {
            if (CanUseLiveVolumeControls() && Config.Instance.LiveVolumeControl == stickMode)
            {
                LiveAudioRuntimeState.PendingVolumeSteps += dir;
                Plugin.Log.Info($"[LiveInput] {label} => queued volume {(dir > 0 ? "+2 dB" : "-2 dB")}");
            }

            if (CanUseLiveNjsControls() && Config.Instance.LiveNjsControl == stickMode)
            {
                AutoNjsRuntimeState.PendingNjsSteps += dir;
                Plugin.Log.Info($"[LiveInput] {label} => queued NJS {(dir > 0 ? "+1" : "-1")}");
            }

            if (CanUseLiveJdControls() && Config.Instance.LiveJdControl == stickMode)
            {
                AutoNjsRuntimeState.PendingJdSteps += dir;
                Plugin.Log.Info($"[LiveInput] {label} => queued JD {(dir > 0 ? "+2" : "-2")}");
            }
        }
        
        private void RefreshDevices()
        {
            _leftHand = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
            _rightHand = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
        }

        private void EnsureDevicesValid()
        {
            if (!_leftHand.isValid || !_rightHand.isValid)
                RefreshDevices();
        }

        private bool GetPrimaryButton(InputDevice device)
        {
            if (!device.isValid)
                return false;

            return device.TryGetFeatureValue(CommonUsages.primaryButton, out bool pressed) && pressed;
        }

        private bool GetSecondaryButton(InputDevice device)
        {
            if (!device.isValid)
                return false;

            return device.TryGetFeatureValue(CommonUsages.secondaryButton, out bool pressed) && pressed;
        }

        private Vector2 GetThumbstick(InputDevice device)
        {
            if (!device.isValid)
                return Vector2.zero;

            if (device.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 axis))
            {
                if (Mathf.Abs(axis.x) < StickDeadzone) axis.x = 0f;
                if (Mathf.Abs(axis.y) < StickDeadzone) axis.y = 0f;
                return axis;
            }

            return Vector2.zero;
        }
    }
    // old working version with less thumbstick and button options
    /*
    public class LiveGameplayInputListener : MonoBehaviour
    {
        private InputDevice _leftHand;
        private InputDevice _rightHand;

        private bool _prevLeftY;
        private bool _prevRightB;

        private bool _leftStickForwardLatched;
        private bool _leftStickBackwardLatched;

        private bool _rightStickForwardLatched;
        private bool _rightStickBackwardLatched;

        private bool _rightStickRightLatched;
        private bool _rightStickLeftLatched;

        private const float StickTriggerThreshold = 0.7f;
        private const float StickReleaseThreshold = 0.3f;
        private const float StickDeadzone = 0.2f;

        private void OnEnable()
        {
            RefreshDevices();
        }

        private void Update()
        {
            if (!LiveGameplayRuntimeState.SongRunning)
                return;

            if (!Config.Instance.EnablePlugin)
                return;

            bool volumeEnabled =
                Config.Instance.EnableLiveVolumeControl &&
                Config.Instance.LiveVolumeControl != LiveControlModeType.Off;

            bool njsEnabled =
                Config.Instance.LiveNoteSpeedControl != LiveControlModeType.Off;

            bool spawnDistanceEnabled =
                Config.Instance.LiveNoteSpawnDistanceControl != LiveControlModeType.Off;

            bool anyLiveControlEnabled =
                volumeEnabled || njsEnabled || spawnDistanceEnabled;

            if (!anyLiveControlEnabled)
                return;

            EnsureDevicesValid();
            HandleButtons();
            HandleThumbsticks();
        }

        private static bool CanUseLiveNjsControls()
        {
            return Config.Instance.EnablePlugin && 
                Config.Instance.LiveNoteSpeedControl != LiveControlModeType.Off;
        }
        private static bool CanUseLiveJDControls()
        {
            return Config.Instance.EnablePlugin &&
                Config.Instance.LiveNoteSpawnDistanceControl != LiveControlModeType.Off;
        }

        private static bool CanUseLiveVolumeControls()
        {
            return Config.Instance.EnablePlugin && 
                   Config.Instance.EnableLiveVolumeControl &&
                   Config.Instance.LiveVolumeControl != Config.LiveControlModeType.Off;
        }

        private static bool UseVolumeButtons()
        {
            return CanUseLiveVolumeControls() &&
                   Config.Instance.LiveVolumeControl == LiveControlModeType.Buttons;
        }

        private static bool UseVolumeThumbstick()
        {
            return CanUseLiveVolumeControls() &&
                   Config.Instance.LiveVolumeControl == LiveControlModeType.Thumbstick;
        }

        private static bool UseNjsButtons()
        {
            return CanUseLiveNjsControls() &&
                   Config.Instance.LiveNoteSpeedControl == LiveControlModeType.Buttons;
        }

        private static bool UseNjsThumbstick()
        {
            return CanUseLiveNjsControls() &&
                   Config.Instance.LiveNoteSpeedControl == LiveControlModeType.Thumbstick;
        }

        private static bool UseJdButtons()
        {
            return CanUseLiveJDControls() &&
                   Config.Instance.LiveNoteSpawnDistanceControl == LiveControlModeType.Buttons;
        }

        private static bool UseJdThumbstick()
        {
            return CanUseLiveJDControls() &&
                   Config.Instance.LiveNoteSpawnDistanceControl == LiveControlModeType.Thumbstick;
        }

        private void HandleButtons()
        {
            bool leftY = GetSecondaryButton(_leftHand);
            bool rightB = GetSecondaryButton(_rightHand);

            if (leftY && !_prevLeftY)
            {
                if (UseVolumeButtons())
                {
                    LiveAudioRuntimeState.PendingVolumeSteps -= 1;
                    Plugin.Log.Info("[LiveInput] Y => queued volume -2 dB");
                }

                if (UseNjsButtons())
                {
                    AutoNjsRuntimeState.PendingNjsSteps -= 1;
                    Plugin.Log.Info("[LiveInput] Y => queued NJS -1");
                }

                if (UseJdButtons())
                {
                    AutoNjsRuntimeState.PendingJdSteps -= 1;
                    Plugin.Log.Info("[LiveInput] Y => queued JD -5");
                }
            }

            if (rightB && !_prevRightB)
            {
                if (UseVolumeButtons())
                {
                    LiveAudioRuntimeState.PendingVolumeSteps += 1;
                    Plugin.Log.Info("[LiveInput] B => queued volume +2 dB");
                }

                if (UseNjsButtons())
                {
                    AutoNjsRuntimeState.PendingNjsSteps += 1;
                    Plugin.Log.Info("[LiveInput] B => queued NJS +1");
                }

                if (UseJdButtons())
                {
                    AutoNjsRuntimeState.PendingJdSteps += 1;
                    Plugin.Log.Info("[LiveInput] B => queued JD +5");
                }
            }

            _prevLeftY = leftY;
            _prevRightB = rightB;
        }

        private void HandleThumbsticks()
        {
            Vector2 leftAxis = GetThumbstick(_leftHand);
            Vector2 rightAxis = GetThumbstick(_rightHand);

            float leftY = leftAxis.y;
            float rightY = rightAxis.y;
            float rightX = rightAxis.x;

            // LEFT STICK Y => VOLUME
            if (!_leftStickForwardLatched && leftY >= StickTriggerThreshold)
            {
                _leftStickForwardLatched = true;

                if (UseVolumeThumbstick())
                {
                    LiveAudioRuntimeState.PendingVolumeSteps += 1;
                    Plugin.Log.Info($"[LiveInput] Left stick forward ({leftY:F2}) => queued volume +2 dB");
                }
            }
            else if (_leftStickForwardLatched && leftY <= StickReleaseThreshold)
            {
                _leftStickForwardLatched = false;
            }

            if (!_leftStickBackwardLatched && leftY <= -StickTriggerThreshold)
            {
                _leftStickBackwardLatched = true;

                if (UseVolumeThumbstick())
                {
                    LiveAudioRuntimeState.PendingVolumeSteps -= 1;
                    Plugin.Log.Info($"[LiveInput] Left stick backward ({leftY:F2}) => queued volume -2 dB");
                }
            }
            else if (_leftStickBackwardLatched && leftY >= -StickReleaseThreshold)
            {
                _leftStickBackwardLatched = false;
            }

            // RIGHT STICK Y => NJS
            if (!_rightStickForwardLatched && rightY >= StickTriggerThreshold)
            {
                _rightStickForwardLatched = true;

                if (UseNjsThumbstick())
                {
                    AutoNjsRuntimeState.PendingNjsSteps += 1;
                    Plugin.Log.Info($"[LiveInput] Right stick forward ({rightY:F2}) => queued NJS +1");
                }
            }
            else if (_rightStickForwardLatched && rightY <= StickReleaseThreshold)
            {
                _rightStickForwardLatched = false;
            }

            if (!_rightStickBackwardLatched && rightY <= -StickTriggerThreshold)
            {
                _rightStickBackwardLatched = true;

                if (UseNjsThumbstick())
                {
                    AutoNjsRuntimeState.PendingNjsSteps -= 1;
                    Plugin.Log.Info($"[LiveInput] Right stick backward ({rightY:F2}) => queued NJS -1");
                }
            }
            else if (_rightStickBackwardLatched && rightY >= -StickReleaseThreshold)
            {
                _rightStickBackwardLatched = false;
            }

            // RIGHT STICK X => JD
            if (!_rightStickRightLatched && rightX >= StickTriggerThreshold)
            {
                _rightStickRightLatched = true;

                if (UseJdThumbstick())
                {
                    AutoNjsRuntimeState.PendingJdSteps += 1;
                    Plugin.Log.Info($"[LiveInput] Right stick right ({rightX:F2}) => queued JD +5");
                }
            }
            else if (_rightStickRightLatched && rightX <= StickReleaseThreshold)
            {
                _rightStickRightLatched = false;
            }

            if (!_rightStickLeftLatched && rightX <= -StickTriggerThreshold)
            {
                _rightStickLeftLatched = true;

                if (UseJdThumbstick())
                {
                    AutoNjsRuntimeState.PendingJdSteps -= 1;
                    Plugin.Log.Info($"[LiveInput] Right stick left ({rightX:F2}) => queued JD -5");
                }
            }
            else if (_rightStickLeftLatched && rightX >= -StickReleaseThreshold)
            {
                _rightStickLeftLatched = false;
            }
        }

        private void RefreshDevices()
        {
            _leftHand = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
            _rightHand = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
        }

        private void EnsureDevicesValid()
        {
            if (!_leftHand.isValid || !_rightHand.isValid)
                RefreshDevices();
        }

        private bool GetSecondaryButton(InputDevice device)
        {
            if (!device.isValid)
                return false;

            return device.TryGetFeatureValue(CommonUsages.secondaryButton, out bool pressed) && pressed;
        }

        private Vector2 GetThumbstick(InputDevice device)
        {
            if (!device.isValid)
                return Vector2.zero;

            if (device.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 axis))
            {
                // Apply deadzone for stick drift
                if (Mathf.Abs(axis.x) < StickDeadzone) axis.x = 0f;
                if (Mathf.Abs(axis.y) < StickDeadzone) axis.y = 0f;
                return axis;
            }

            return Vector2.zero;
        }
    }
    */














    // OLD -----------------------------------------------------------
    // Listens for live button/grip input during gameplay to adjust NJS and volume on the fly.
    // this was grip + buttons. works!
    /*
    public class LiveGameplayInputListener : MonoBehaviour
    {
        private InputDevice _leftHand;
        private InputDevice _rightHand;

        private bool _prevLeftY;
        private bool _prevRightB;

        private void OnEnable()
        {
            RefreshDevices();
        }

        private void Update()
        {
            if (!AutoNjsRuntimeState.SongRunning)
                return;

            EnsureDevicesValid();

            bool leftY = GetSecondaryButton(_leftHand);
            bool rightB = GetSecondaryButton(_rightHand);

            bool leftGripButton = GetGripButtonRaw(_leftHand, out float leftGripValue, out bool leftGripBool);
            bool rightGripButton = GetGripButtonRaw(_rightHand, out float rightGripValue, out bool rightGripBool);

            bool leftGrip = leftGripButton;
            bool rightGrip = rightGripButton;

            if (leftY && !_prevLeftY)
            {
                //Plugin.LogDebug($"[LiveInput] LEFT Y press detected. " + $"leftGrip:{leftGrip} gripButton:{leftGripBool} gripValue:{leftGripValue:F3}");

                if (leftGrip)
                {
                    AutoNjsRuntimeState.PendingNjsSteps -= 1;
                    //Plugin.LogDebug("[LiveInput] Left Grip + Y => queued NJS -1");
                }
                else
                {
                    AutoNjsRuntimeState.PendingVolumeSteps -= 1;
                    //Plugin.LogDebug("[LiveInput] Y => queued volume -2 dB");
                }
            }

            if (rightB && !_prevRightB)
            {
                //Plugin.LogDebug($"[LiveInput] RIGHT B press detected. " + $"rightGrip:{rightGrip} gripButton:{rightGripBool} gripValue:{rightGripValue:F3}");

                if (rightGrip)
                {
                    AutoNjsRuntimeState.PendingNjsSteps += 1;
                    //Plugin.LogDebug("[LiveInput] Right Grip + B => queued NJS +1");
                }
                else
                {
                    AutoNjsRuntimeState.PendingVolumeSteps += 1;
                    //Plugin.LogDebug("[LiveInput] B => queued volume +2 dB");
                }
            }

            _prevLeftY = leftY;
            _prevRightB = rightB;
        }

        private bool GetGripButtonRaw(InputDevice device, out float gripValue, out bool gripButton)
        {
            gripValue = 0f;
            gripButton = false;

            if (!device.isValid)
                return false;

            bool hasGripButton = device.TryGetFeatureValue(CommonUsages.gripButton, out gripButton);
            bool hasGripValue = device.TryGetFeatureValue(CommonUsages.grip, out gripValue);

            return (hasGripButton && gripButton) || (hasGripValue && gripValue > 0.75f);
        }

        private void RefreshDevices()
        {
            _leftHand = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
            _rightHand = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);

            //Plugin.LogDebug($"[LiveInput] RefreshDevices Left:{_leftHand.isValid} Right:{_rightHand.isValid}");
        }

        private void EnsureDevicesValid()
        {
            if (!_leftHand.isValid || !_rightHand.isValid)
                RefreshDevices();
        }

        private bool GetSecondaryButton(InputDevice device)
        {
            if (!device.isValid)
                return false;

            return device.TryGetFeatureValue(CommonUsages.secondaryButton, out bool pressed) && pressed;
        }

        private bool GetGripButton(InputDevice device)
        {
            if (!device.isValid)
                return false;

            if (device.TryGetFeatureValue(CommonUsages.gripButton, out bool pressed))
                return pressed;

            if (device.TryGetFeatureValue(CommonUsages.grip, out float gripValue))
                return gripValue > 0.75f;

            return false;
        }
    }
    */
}
