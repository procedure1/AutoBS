using TMPro;
using UnityEngine;

namespace AutoBS
{
    internal static class LiveAdjustHudRuntime
    {
        internal static LiveAdjustHud Instance;
    }


    public class LiveAdjustHud : MonoBehaviour
    {
        private TextMeshPro _text;
        private float _hideAt;
        private bool _initialized;

        private Transform _headTransform;

        // false = original world-space placement
        // true  = lock in front of headset
        public bool UseViewLockedMode { get; set; }

        // World-space placement for Standard
        private static readonly Vector3 WorldLocalPosition = new Vector3(0f, 2.2f, 1.8f);

        // View-locked placement for 360 / 90
        private const float DistanceForward = 1.8f;
        private const float VerticalOffset = -0.12f;

        private const float HudScale = 0.24f;

        public void Initialize()
        {
            if (_initialized)
                return;

            var textObj = new GameObject("LiveAdjustHudText");
            textObj.transform.SetParent(transform, false);
            textObj.transform.localPosition = Vector3.zero;
            textObj.transform.localRotation = Quaternion.identity;
            textObj.transform.localScale = Vector3.one * HudScale;

            _text = textObj.AddComponent<TextMeshPro>();
            _text.text = "";
            _text.fontSize = 4f;
            _text.alignment = TextAlignmentOptions.Center;
            _text.textWrappingMode = TextWrappingModes.NoWrap;
            _text.richText = false;

            textObj.SetActive(false);

            TryFindHeadTransform();

            _initialized = true;
        }

        private void Update()
        {
            if (!_initialized || _text == null)
                return;

            if (_text.gameObject.activeSelf)
            {
                if (UseViewLockedMode)
                    UpdateHudTransformViewLocked();

                if (Time.time >= _hideAt)
                    _text.gameObject.SetActive(false);
            }
        }

        public void ShowVolumeOffset(float offsetDb)
        {
            ShowMessage($"Volume {FormatSigned(offsetDb)} dB");
        }

        public void ShowJdValue(float jd)
        {
            ShowMessage($"JD {FormatSmart(jd)}");
        }

        public void ShowNjsValue(float njs)
        {
            ShowMessage($"NJS {FormatSmart(njs)}");
        }

        public void HideNow()
        {
            if (_text != null)
                _text.gameObject.SetActive(false);
        }

        private void ShowMessage(string message)
        {
            if (!_initialized)
                Initialize();

            if (UseViewLockedMode && _headTransform == null)
                TryFindHeadTransform();

            _text.text = message;
            _text.gameObject.SetActive(true);

            if (UseViewLockedMode)
                UpdateHudTransformViewLocked();
            else
                UpdateHudTransformWorldSpace();

            _hideAt = Time.time + 2f;
        }

        private void UpdateHudTransformWorldSpace()
        {
            transform.localPosition = WorldLocalPosition;
            transform.localRotation = Quaternion.identity;
        }

        private void UpdateHudTransformViewLocked()
        {
            if (_headTransform == null)
                return;

            Vector3 headPos = _headTransform.position;
            Vector3 forward = _headTransform.forward;
            Vector3 up = _headTransform.up;

            transform.position = headPos + forward * DistanceForward + up * VerticalOffset;
            transform.rotation = Quaternion.LookRotation(forward, up);
        }

        private void TryFindHeadTransform()
        {
            if (Camera.main != null)
            {
                Plugin.LogDebug($"[LiveAdjustHud] Camera.main = {Camera.main.name} scene={Camera.main.gameObject.scene.name}");
                _headTransform = Camera.main.transform;
                return;
            }

            Camera[] cams = Camera.allCameras;
            Plugin.Log.Info($"[LiveAdjustHud] Camera count = {cams.Length}");

            for (int i = 0; i < cams.Length; i++)
            {
                //if (cams[i] != null)
                //{
                //    Plugin.LogDebug($"[LiveAdjustHud] Cam[{i}] name={cams[i].name} active={cams[i].isActiveAndEnabled} scene={cams[i].gameObject.scene.name}");
                //}

                if (cams[i] != null && cams[i].isActiveAndEnabled)
                {
                    _headTransform = cams[i].transform;
                    //Plugin.LogDebug($"[LiveAdjustHud] Selected head transform = {_headTransform.name}");
                    return;
                }
            }

            Plugin.Log.Info("[LiveAdjustHud] No head transform found");
        }

        private static string FormatSigned(float value)
        {
            float rounded = Mathf.Round(value * 10f) / 10f;

            if (Mathf.Abs(rounded - Mathf.Round(rounded)) < 0.001f)
                return $"{rounded:+0;-0;0}";

            return $"{rounded:+0.0;-0.0;0.0}";
        }

        private static string FormatSmart(float value)
        {
            float rounded = Mathf.Round(value * 10f) / 10f;

            if (Mathf.Abs(rounded - Mathf.Round(rounded)) < 0.001f)
                return Mathf.RoundToInt(rounded).ToString();

            return rounded.ToString("0.0");
        }
    }




















    /// <summary>
    /// Provides a heads-up display (HUD) for live adjustments, displaying volume, NJS and JD
    /// </summary>
    /// <remarks>This class initializes a text object to display messages and automatically hides them after a
    /// specified duration. It requires initialization before use, and the displayed messages are updated based on the
    /// provided values.</remarks>
    /// 

    // great for non-360 environments, but in 360 it can be hard to see. maybe add a setting for it to be in front of the player instead of above?
    /*
    public class LiveAdjustHud : MonoBehaviour
    {
        private TextMeshPro _text;
        private float _hideAt;
        private bool _initialized;

        public void Initialize()
        {
            if (_initialized)
                return;

            var textObj = new GameObject("LiveAdjustHudText");
            textObj.transform.SetParent(transform, false);
            textObj.transform.localPosition = new Vector3(.05f, 2.2f, 1.8f); // y is height in meters z is depth away. tried .1 to far right
            textObj.transform.localRotation = Quaternion.identity;
            textObj.transform.localScale = Vector3.one * 0.12f;

            _text = textObj.AddComponent<TextMeshPro>();
            _text.text = "";
            _text.fontSize = 8f;
            _text.alignment = TextAlignmentOptions.Center;
            _text.textWrappingMode = TextWrappingModes.NoWrap;
            _text.richText = false;

            textObj.SetActive(false);

            _initialized = true;
        }

        private void Update()
        {
            if (!_initialized || _text == null)
                return;

            if (_text.gameObject.activeSelf && Time.time >= _hideAt)
            {
                _text.gameObject.SetActive(false);
            }
        }

        public void ShowVolumeOffset(float offsetDb)
        {
            ShowMessage($"Volume {FormatSigned(offsetDb)} dB");
        }

        public void ShowJdValue(float jd)
        {
            ShowMessage($"JD {FormatSmart(jd)}");
        }

        public void ShowNjsValue(float njs)
        {
            ShowMessage($"NJS {FormatSmart(njs)}");
        }

        public void HideNow()
        {
            if (_text != null)
                _text.gameObject.SetActive(false);
        }

        private void ShowMessage(string message)
        {
            if (!_initialized)
                Initialize();

            _text.text = message;
            _text.gameObject.SetActive(true);
            _hideAt = Time.time + 2f;
        }

        private static string FormatSigned(float value)
        {
            float rounded = Mathf.Round(value * 10f) / 10f;

            if (Mathf.Abs(rounded - Mathf.Round(rounded)) < 0.001f)
                return $"{rounded:+0;-0;0}";

            return $"{rounded:+0.0;-0.0;0.0}";
        }

        private static string FormatSmart(float value)
        {
            float rounded = Mathf.Round(value * 10f) / 10f;

            if (Mathf.Abs(rounded - Mathf.Round(rounded)) < 0.001f)
                return Mathf.RoundToInt(rounded).ToString();

            return rounded.ToString("0.0");
        }
    }
    */
}
