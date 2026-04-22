using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace AutoBS.Modules
{/*
    internal static class CameraCalibrationRig
    {
        private static GameObject _root;

        // Grid dimensions
        private const float MinX = -1f;
        private const float MidX = 0f;
        private const float MaxX = 1f;

        private const float MinY = 0f;
        private const float MidY = 1f;
        private const float MaxY = 2f;

        // Plane depths
        private const float RearZ = -1f;
        private const float MiddleZ = 0f;
        private const float FrontZ = 1f;

        // Visual sizes
        private const float LineThickness = 0.015f;
        private const float MarkerDiameter = 0.06f;

        private static readonly Color FrontColor = Color.yellow;
        private static readonly Color MiddleColor = Color.white;
        private static readonly Color RearColor = Color.magenta;

        internal static void Refresh(Transform parent = null)
        {
            Clear();

            if (!Config.Instance.EnableCameraCalibrationRig)
                return;

            if (Config.Instance.CameraCalibrationMode == CameraCalibrationDisplayMode.Off)
                return;

            _root = new GameObject("AutoBS_CameraCalibrationRig");

            if (parent != null)
            {
                _root.transform.SetParent(parent, false);
                _root.transform.localPosition = Vector3.zero;
                _root.transform.localRotation = Quaternion.identity;
                _root.transform.localScale = Vector3.one;
            }

            if (Config.Instance.ShowCameraCalibrationFront)
                CreateGridPlane("FrontGrid", FrontZ, FrontColor, Config.Instance.CameraCalibrationMode);

            if (Config.Instance.ShowCameraCalibrationMiddle)
                CreateGridPlane("MiddleGrid", MiddleZ, MiddleColor, Config.Instance.CameraCalibrationMode);

            if (Config.Instance.ShowCameraCalibrationRear)
                CreateGridPlane("RearGrid", RearZ, RearColor, Config.Instance.CameraCalibrationMode);
        }

        internal static void Clear()
        {
            if (_root != null)
            {
                UnityEngine.Object.Destroy(_root);
                _root = null;
            }
        }

        private static void CreateGridPlane(string name, float z, Color color, CameraCalibrationDisplayMode mode)
        {
            GameObject planeRoot = new GameObject(name);
            planeRoot.transform.SetParent(_root.transform, false);

            if (mode == CameraCalibrationDisplayMode.Lines)
            {
                CreateGridLines(planeRoot.transform, z, color);
            }
            else if (mode == CameraCalibrationDisplayMode.Markers)
            {
                CreateGridMarkers(planeRoot.transform, z, color);
            }
        }

        private static void CreateGridLines(Transform parent, float z, Color color)
        {
            // Verticals at x = -1, 0, 1 from y=0 to y=2
            CreateLine(parent, new Vector3(MinX, MinY, z), new Vector3(MinX, MaxY, z), color, "V_-1");
            CreateLine(parent, new Vector3(MidX, MinY, z), new Vector3(MidX, MaxY, z), color, "V_0");
            CreateLine(parent, new Vector3(MaxX, MinY, z), new Vector3(MaxX, MaxY, z), color, "V_1");

            // Horizontals at y = 0, 1, 2 from x=-1 to x=1
            CreateLine(parent, new Vector3(MinX, MinY, z), new Vector3(MaxX, MinY, z), color, "H_0");
            CreateLine(parent, new Vector3(MinX, MidY, z), new Vector3(MaxX, MidY, z), color, "H_1");
            CreateLine(parent, new Vector3(MinX, MaxY, z), new Vector3(MaxX, MaxY, z), color, "H_2");
        }

        private static void CreateGridMarkers(Transform parent, float z, Color color)
        {
            float[] xs = { MinX, MidX, MaxX };
            float[] ys = { MinY, MidY, MaxY };

            foreach (float x in xs)
            {
                foreach (float y in ys)
                {
                    CreateMarker(parent, new Vector3(x, y, z), color, $"Marker_{x}_{y}");
                }
            }
        }

        private static void CreateLine(Transform parent, Vector3 start, Vector3 end, Color color, string name)
        {
            GameObject line = GameObject.CreatePrimitive(PrimitiveType.Cube);
            line.name = name;
            line.transform.SetParent(parent, false);

            Vector3 delta = end - start;
            float length = delta.magnitude;
            Vector3 center = (start + end) * 0.5f;

            line.transform.position = center;
            line.transform.rotation = Quaternion.FromToRotation(Vector3.up, delta.normalized);

            // Cube is oriented so Y is the line length
            line.transform.localScale = new Vector3(LineThickness, length, LineThickness);

            ApplyColor(line, color);
            DisableCollider(line);
        }

        private static void CreateMarker(Transform parent, Vector3 position, Color color, string name)
        {
            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            marker.name = name;
            marker.transform.SetParent(parent, false);
            marker.transform.position = position;
            marker.transform.localScale = Vector3.one * MarkerDiameter;

            ApplyColor(marker, color);
            DisableCollider(marker);
        }

        private static void ApplyColor(GameObject go, Color color)
        {
            Renderer renderer = go.GetComponent<Renderer>();
            if (renderer == null)
                return;

            Material mat = new Material(Shader.Find("Standard"));
            mat.color = color;

            // Slight emission so it stays readable
            if (mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", color * 0.4f);
            }

            renderer.material = mat;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        private static void DisableCollider(GameObject go)
        {
            Collider collider = go.GetComponent<Collider>();
            if (collider != null)
                UnityEngine.Object.Destroy(collider);
        }
    }*/
}