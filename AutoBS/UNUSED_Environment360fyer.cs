/*
using HarmonyLib;
using UnityEngine;
using System.Collections;
using UnityEngine.SceneManagement;

namespace AutoBS
{

    [HarmonyPatch(typeof(StandardLevelScenesTransitionSetupDataSO), "Init")]
    internal class Environment360fyer
    {
        internal static void Prefix(IDifficultyBeatmap difficultyBeatmap, ref ColorScheme overrideColorScheme,
            ColorScheme beatmapOverrideColorScheme)
        {
            if (Config.Instance.EnableEnvironment360fyer)
            {
                // Start a coroutine to wait for the environment to be fully loaded
                new GameObject("EnvironmentFinder2").AddComponent<EnvironmentFinder2>();
            }
        }
    }


    public class EnvironmentFinder2 : MonoBehaviour
    {
        private void Start()
        {
            Plugin.Log.Info("Environment 360fyer Start method called.");
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Plugin.Log.Info($"Scene loaded: {scene.name}");

            if (scene.name.Contains("GameCore"))
            {
                Plugin.Log.Info("GameCore scene loaded. Starting InitialWaitAndCheck coroutine.");
                StartCoroutine(InitialWaitAndCheck());
            }
        }

        private IEnumerator InitialWaitAndCheck()
        {
            Plugin.Log.Info("Initial wait started.");

            float totalWaitTime = 0f;
            float waitInterval = 1f;
            while (totalWaitTime < 10f) // Wait for up to 10 seconds
            {
                Plugin.Log.Info("Checking for environment...");
                GameObject environment = FindEnvironment();

                if (environment != null)
                {
                    Plugin.Log.Info("Environment found!");
                    ConvertStandardEnvironmentTo360(environment);
                    Destroy(gameObject);
                    yield break;
                }

                Plugin.Log.Info("Environment not found. Waiting...");
                totalWaitTime += waitInterval;
                yield return new WaitForSeconds(waitInterval);
            }

            Plugin.Log.Info("Environment was not found within the wait time.");
            Destroy(gameObject);
        }

        private GameObject FindEnvironment()
        {
            int sceneCount = SceneManager.sceneCount;

            for (int i = 0; i < sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (scene.isLoaded && scene.name.EndsWith("Environment"))
                {
                    foreach (GameObject obj in scene.GetRootGameObjects())
                    {
                        if (obj.name == "Environment")
                        {
                            Plugin.Log.Info($"{obj.name} gameObject found in scene: {scene.name}");
                            //LogAllGameObjects(obj);
                            return obj;
                        }
                    }
                }
            }
            return null;
        }
        private static void LogAllGameObjects(GameObject parent)
        {
            Plugin.Log.Info("Logging all root and child GameObjects:");

            foreach (GameObject root in parent.scene.GetRootGameObjects())
            {
                Plugin.Log.Info($"Root GameObject: {root.name}");
                LogChildGameObjects(root.transform, "--");
            }
        }

        private static void LogChildGameObjects(Transform parentTransform, string indent)
        {
            foreach (Transform child in parentTransform)
            {
                Plugin.Log.Info($"{indent} Child GameObject: {child.gameObject.name}");
                LogChildGameObjects(child, indent + "--");
            }
        }

        private void ConvertStandardEnvironmentTo360(GameObject originalEnvironment)
        {
            Plugin.Log.Info($"Converting {originalEnvironment.name} to a 360 environment...");

            const int instanceCount = 11; // 7
            const float angleIncrement = 360f / instanceCount; // 360
            const float radius = 0; // Adjust as necessary

            for (int i = 0; i < instanceCount; i++)
            {
                float angle = i * angleIncrement;
                Vector3 position = CalculatePosition(angle, radius);
                Quaternion rotation = Quaternion.Euler(0, angle, 0);

                // Instantiate a copy of the original environment's visual elements
                GameObject newEnvironment = new GameObject($"EnvironmentInstance_{i}");
                newEnvironment.transform.position = position;
                newEnvironment.transform.rotation = rotation;
                newEnvironment.transform.parent = originalEnvironment.transform;

                var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                //sphere.transform.parent = originalEnvironment.transform;
                sphere.transform.position = position;
                sphere.transform.rotation = rotation;
                sphere.transform.localScale = new Vector3(1, 1, 1);

                // Ensure the sphere is in a visible layer and has proper rendering settings
                sphere.layer = LayerMask.NameToLayer("Default");
                Renderer renderer = sphere.GetComponent<Renderer>();
                Material material = new Material(Shader.Find("Standard"))
                {
                    color = new Color(0.015f, 0.616f, 0.737f) // Color: #039DBC;
                };
                renderer.material = material;

                Plugin.Log.Info($"Sphere created at position {position} with rotation {rotation}");

                CopyVisualElements(originalEnvironment, newEnvironment);
                newEnvironment.SetActive(true);
            }

            Plugin.Log.Info("360 Environment created.");
        }


        private void CopyVisualElements(GameObject original, GameObject copy)
        {
            foreach (Transform originalChild in original.transform)
            {
                if (originalChild.name.Contains("FrontLights") || originalChild.name.Contains("Laser") || originalChild.name.Contains("LightGroup") || originalChild.name.Contains("TrackMirror")) // lightgroup is for gls
                {
                    GameObject newChild = Instantiate(originalChild.gameObject, copy.transform);
                    newChild.name = originalChild.name;
                    newChild.transform.parent = copy.transform;
                    Plugin.Log.Info($"Copied GameObject: {newChild.name} with all its children and components.");
                }
            }
        }

        private Vector3 CalculatePosition(float angle, float radius)
        {
            float radian = angle * Mathf.Deg2Rad;
            float x = Mathf.Sin(radian) * radius;
            float z = Mathf.Cos(radian) * radius;
            return new Vector3(x, 0, z);
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }
    }
}
*/