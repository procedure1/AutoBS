using AutoBS.Patches;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AutoBS
{
    // Add Directional Markers to Menu Environment so player can easily face the menus
    public class GlassEnvironmentFinder : MonoBehaviour
    {
        public static GlassEnvironmentFinder Instance { get; private set; }
        public bool menuMarkersAdded = false;
        private List<GameObject> markers = new List<GameObject>();

        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            Plugin.LogDebug("[GlassEnvironmentFinder] Start method called. Will try to add directional markers for players to know what direction to face.");
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Plugin.LogDebug($"[GlassEnvironmentFinder] {scene.name} checking environment to add markers potentially if its a 360 environment...");

            if (scene.name.Contains("GlassDesertEnvironment"))
            {
                Plugin.LogDebug($"{scene.name} scene loaded.");
                GameObject environment = FindEnvironment(scene);
                if (environment != null)
                {
                    GameObject root = SceneManager.GetActiveScene().GetRootGameObjects()[0];
                    AddDirectionalMarkersToMenu.AddMarkers(root);
                }
                else
                {
                    Plugin.LogDebug("[GlassEnvironmentFinder] Environment within GlassDesertEnvironment not found.");
                }
            }
            else if (scene.name.Contains("Environment"))
            {
                if (menuMarkersAdded)
                {
                    Plugin.LogDebug($"[GlassEnvironmentFinder] Removing markers since scene is: {scene.name}.");
                    RemoveMarkers();
                }
            }
        }

        private GameObject FindEnvironment(Scene scene)
        {
            if (scene.isLoaded && scene.name.EndsWith("GlassDesertEnvironment"))
            {
                foreach (GameObject obj in scene.GetRootGameObjects())
                {
                    if (obj.name.EndsWith("Environment"))
                    {
                        Plugin.LogDebug($"[GlassEnvironmentFinder] {obj.name} found in scene: {scene.name}");
                        return obj;
                    }
                }
            }
            return null;
        }

        private void RemoveMarkers()
        {
            foreach (var marker in markers)
            {
                if (marker != null)
                {
                    Destroy(marker);
                }
            }
            markers.Clear();
            menuMarkersAdded = false;
            Plugin.LogDebug("[GlassEnvironmentFinder] Direction markers removed.");
        }

        public void AddMarker(GameObject marker)
        {
            markers.Add(marker);
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (Instance == this)
            {
                Instance = null;
            }
        }
    }


    public class AddDirectionalMarkersToMenu
    {
        // Use NoteArrow & NoteArrowGlow which are found in the menu environment - so can duplicate them and use them as directional arrows
        public static void AddMarkers (GameObject root)
        {
            if (GlassEnvironmentFinder.Instance.menuMarkersAdded)
            {
                Plugin.LogDebug("[AddDirectionalMarkersToMenu] Markers already exist. Skipping creation.");
                return;
            }

            GameObject noteArrow = null;
            GameObject noteArrowGlow = null;

            // Locate MenuEnvironmentManager and DefaultMenu`Environment
            var menuEnvironmentManager = root.transform.Find("MenuEnvironmentManager").gameObject;

            if (menuEnvironmentManager != null)
            {
                var defaultMenuEnvironment = menuEnvironmentManager.transform.Find("DefaultMenuEnvironment").gameObject;

                if (defaultMenuEnvironment != null)
                {
                    // Locate the Note (16) GameObject which contains NoteArrow and NoteArrowGlow
                    var levitatingNote = defaultMenuEnvironment.transform.Find("Notes/LevitatingNote/Note (16)").gameObject;

                    // Assign noteArrow and noteArrowGlow
                    if (levitatingNote != null)
                    {
                        noteArrow = levitatingNote.transform.Find("NoteArrow").gameObject;
                        noteArrowGlow = levitatingNote.transform.Find("NoteArrowGlow").gameObject;

                        if (noteArrow == null || noteArrowGlow == null)
                        {
                            Plugin.LogDebug("[AddDirectionalMarkersToMenu] NoteArrow or NoteArrowGlow not found.No directional markers added!!!");
                            //AddDirectionMarkerSpheresToMenu(root);
                            return;
                        }
                    }
                }
            }

            Plugin.LogDebug("[AddDirectionalMarkersToMenu] Adding direction markers to Menu...");

            float radius = 4.2f; // Radial distance of 4.2m
            float height = 1.3f; // Height off the ground floor

            float[] angles = { 90f, 100f, 110f, 160f, 170f, 179f, 181f, 190f, 200f, 250f, 260f, 270f };// ends are too close to center {  110f, 120f, 130f,       160f, 170f, 179f, 181f, 190f, 200f,      250f, 240f, 230f };
            float[] scales = { 0.4f, 0.7f, 1f, 0.4f, 0.7f, 1f, 1f, 0.7f, 0.4f, 0.4f, 0.7f, 1f };

            Quaternion pointTo0 = Quaternion.Euler(0, 5, -90);   // arrows aim toward menu which is at 0/360
            Quaternion pointTo360 = Quaternion.Euler(0, -5, 90); // arrows aim toward menu which is at 0/360

            for (int i = 0; i < angles.Length; i++)
            {
                float angle = angles[i];
                float scale = scales[i];
                Vector3 position = CalculatePosition(angle, radius, height);

                // Calculate the direction each arrow should face based on its position
                Vector3 direction = (position - new Vector3(0, height, 0)).normalized;
                Quaternion rotation = Quaternion.LookRotation(direction) * ((angle < 180) ? pointTo0 : pointTo360);

                // Instantiate NoteArrow and NoteArrowGlow
                GameObject arrowInstance = UnityEngine.Object.Instantiate(noteArrow, position, rotation, root.transform);
                arrowInstance.transform.localScale = new Vector3(scale, scale, scale);
                arrowInstance.name = $"DirectionArrow_{i}";
                arrowInstance.SetActive(true); // Ensure the object is active

                GameObject glowInstance = UnityEngine.Object.Instantiate(noteArrowGlow, position, rotation, root.transform);
                glowInstance.transform.localScale = new Vector3(scale, scale, scale);
                glowInstance.name = $"DirectionArrowGlow_{i}";
                glowInstance.SetActive(true); // Ensure the object is active

                // Ensure correct rendering layer
                arrowInstance.layer = root.layer;
                glowInstance.layer = root.layer;

                GlassEnvironmentFinder.Instance.AddMarker(arrowInstance);
                GlassEnvironmentFinder.Instance.AddMarker(glowInstance);

                //Plugin.Log.Info($"Direction arrow created at position {position} with rotation {rotation} and scale {scale}");
            }

            GlassEnvironmentFinder.Instance.menuMarkersAdded = true;
            Plugin.LogDebug("[AddDirectionalMarkersToMenu] Direction markers added.");
        }

        private static Vector3 CalculatePosition(float angle, float radius, float height)
        {
            float radian = angle * Mathf.Deg2Rad;
            float x = Mathf.Sin(radian) * radius;
            float z = Mathf.Cos(radian) * radius;
            return new Vector3(x, height, z);
        }

        // spheres
        /*
        public static void AddDirectionMarkerSpheresToMenu(GameObject root)
        {
            if (GlassEnvironmentFinder.Instance.menuMarkersAdded)
            {
                Plugin.LogDebug("[AddDirectionalMarkersToMenu] Sphere Markers already exist. Skipping creation.");
                return;
            }

            Plugin.LogDebug("[AddDirectionalMarkersToMenu] Adding sphere direction markers to Menu...");

            float radius = 4.2f; // Radial distance of 4m
            float height = 1.3f; // Height off the ground floor
            Color sphereColor = new Color(0.015f, 0.616f, 0.737f); // Color: #039DBC

            float[] angles = { 80f, 90f, 100f, 160f, 170f, 180f, 190f, 200f, 280f, 270f, 260f };
            float[] scales = { 0.1f, 0.2f, 0.3f, 0.1f, 0.2f, 0.3f, 0.2f, 0.1f, 0.1f, 0.2f, 0.3f };

            for (int i = 0; i < angles.Length; i++)
            {
                float angle = angles[i];
                float scale = scales[i];
                Vector3 position = CalculatePosition(angle, radius, height);
                Quaternion rotation = Quaternion.Euler(0, angle, 0);

                GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                sphere.name = $"DirectionMarker_{i}";
                sphere.transform.position = position;
                sphere.transform.rotation = rotation;
                sphere.transform.localScale = new Vector3(scale, scale, scale);
                sphere.transform.parent = root.transform;

                Renderer renderer = sphere.GetComponent<Renderer>();
                Material material = new Material(Shader.Find("Standard"))
                {
                    color = sphereColor
                };
                renderer.material = material;


                Plugin.LogDebug($"[AddDirectionalMarkersToMenu] Sphere Marker created at position {position} with rotation {rotation} and scale {scale}");
            }

            GlassEnvironmentFinder.Instance.menuMarkersAdded = true;
            Plugin.LogDebug("[AddDirectionalMarkersToMenu] Sphere Direction markers added.");
        }
        */
    }
}
