using HarmonyLib;
using HMUI;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.Windows;
using UnityEngine.XR;

namespace AutoBS
{
    /// <summary>
    /// Sets 360 Directional Markers to Menu Environment so player can easily face the menus and
    /// Add Green Screens to Menus and Gameplay for Mixed Reality Portals using Virtual Desktop
    /// </summary>
    public class EnvironmentMarkersAndGreenScreen : MonoBehaviour
    {
        // === Add 360 Directional Markers to Menu Environment
        private bool _menuMarkersAdded;
        private readonly List<GameObject> _markers = new List<GameObject>();

        // Green Screen
        private Transform _headTransform;
        private GameObject _gameplayShell;
        private bool _lockShellToHead;
        private bool _tryingToFindHead;
        private bool _loggedMainMenuWrapper;

        //private bool? _lastMenuCircular;
        //private float _lastMenuYOffset;
        //private float _lastMenuZOffset;

        private static Material _greenTemplateMat;
        private static string _greenTemplateShaderName;

        private GameObject _menuShell;

        private Coroutine _menuRoutine;
        private Coroutine _gameRoutine;

        //private bool _loggedHubScreenState;

        private bool _isMainMenuLoaded;
        private bool _isGameplayLoaded;

        const float menuFloorHeight = 0.001f; // the menu floor is very close to 0, but we add a small offset to ensure the floor quad renders above it and doesn't z-fight
        const float gameplayFloorHeight = 0.02f;//-0.05f; // this lowers the floor just below the feet graphic so its still visible. gamplay has no floor.

        //private Transform _menuCameraTransform;

        private void Start()
        {
            Plugin.Log.Info($"[EnvGreen] Start go={gameObject.name} scene={gameObject.scene.name} activeInHierarchy={gameObject.activeInHierarchy}");

            _isMainMenuLoaded = false;
            _isGameplayLoaded = false;

            if (!Config.Instance.EnableGreenScreen)
            {
                Plugin.Log.Info("[EnvGreen] Green screen disabled at Start, clearing any existing shells");
                ClearAllGreenScreens();
                return;
            }

            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded)
                    continue;

                if (scene.name == "MainMenu")
                    _isMainMenuLoaded = true;

                if (scene.name.Contains("GameCore") || scene.name.Contains("StandardGameplay"))
                    _isGameplayLoaded = true;
            }

            if (_isMainMenuLoaded)
            {
                Plugin.Log.Info("[EnvGreen] MainMenu already loaded, ensuring menu greenscreen immediately");
                EnsureMenuGreenScreen();
            }
            else if (_isGameplayLoaded)
            {
                Plugin.Log.Info("[EnvGreen] GameCore already loaded, starting scan immediately");
                _gameRoutine = StartCoroutine(InitialWaitAndCheck());
            }
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Plugin.Log.Info($"[EnvGreen] Scene loaded: {scene.name}");

            if (!Config.Instance.EnableGreenScreen)
            {
                Plugin.Log.Info("[EnvGreen] Scene loaded while green screen disabled, clearing shells");
                ClearAllGreenScreens();
                return;
            }

            if (scene.name == "MainMenu")
                _isMainMenuLoaded = true;

            if (scene.name.Contains("GameCore") || scene.name.Contains("StandardGameplay"))
                _isGameplayLoaded = true;

            if (scene.name.Contains("GlassDesertEnvironment"))
            {
                AddDirectionalMarkersIfNeeded();
            }
            else if (scene.name.Contains("Environment"))
            {
                RemoveDirectionalMarkers();
            }

            if (scene.name.Contains("GameCore"))
            {
                ResetRunState();
                ResetMenuState();

                if (!Config.Instance.EnableGreenScreen)
                    return;

                Plugin.Log.Info("[EnvGreen] GameCore detected, starting scan");

                if (_gameRoutine != null)
                    StopCoroutine(_gameRoutine);

                _gameRoutine = StartCoroutine(InitialWaitAndCheck());
                return;
            }

            if (!_isGameplayLoaded && scene.name == "MainMenu")
            {
                Plugin.Log.Info("[EnvGreen] MainMenu detected, ensuring menu greenscreen");
                EnsureMenuGreenScreen();
            }
        }

        private void OnSceneUnloaded(Scene scene)
        {
            Plugin.Log.Info($"[EnvGreen] Scene unloaded: {scene.name}");

            if (scene.name == "MainMenu")
            {
                _isMainMenuLoaded = false;
                ResetMenuState();
                return;
            }

            if (scene.name.Contains("GameCore") || scene.name.Contains("StandardGameplay"))
            {
                _isGameplayLoaded = false;

                ResetRunState();

                if (_isMainMenuLoaded && Config.Instance.EnableGreenScreen)
                    EnsureMenuGreenScreen();

                return;
            }

            if (scene.name.Contains("GlassDesertEnvironment"))
            {
                ResetRunState();
            }
        }
        private void ResetRunState()
        {
            if (_gameRoutine != null)
            {
                StopCoroutine(_gameRoutine);
                _gameRoutine = null;
            }

            _loggedMainMenuWrapper = false;
            _headTransform = null;
            _tryingToFindHead = false;
            _lockShellToHead = false;

            if (_gameplayShell != null)
            {
                Destroy(_gameplayShell);
                _gameplayShell = null;
            }
        }

        private void ResetMenuState()
        {
            if (_menuRoutine != null)
            {
                StopCoroutine(_menuRoutine);
                _menuRoutine = null;
            }

            if (_menuShell != null)
            {
                Destroy(_menuShell);
                _menuShell = null;
            }
        }

        private bool _subscribed;

        private void OnEnable()
        {
            Plugin.Log.Info($"[EnvGreen] OnEnable go={gameObject.name} scene={gameObject.scene.name} activeInHierarchy={gameObject.activeInHierarchy}");

            if (!_subscribed)
            {
                SceneManager.sceneLoaded += OnSceneLoaded;
                SceneManager.sceneUnloaded += OnSceneUnloaded;
                _subscribed = true;
            }
            if (!Config.Instance.EnableGreenScreen)
            {
                Plugin.Log.Info("[EnvGreen] OnEnable while disabled, clearing shells");
                ClearAllGreenScreens();
            }
        }

        private void OnDestroy()
        {
            if (_subscribed)
            {
                SceneManager.sceneLoaded -= OnSceneLoaded;
                SceneManager.sceneUnloaded -= OnSceneUnloaded;
                _subscribed = false;

                if (_greenTemplateMat != null)
                {
                    Destroy(_greenTemplateMat);
                    _greenTemplateMat = null;
                }
            }

            RemoveDirectionalMarkers();
        }
        private void ClearAllGreenScreens()
        {
            if (_menuRoutine != null)
            {
                StopCoroutine(_menuRoutine);
                _menuRoutine = null;
            }

            if (_gameRoutine != null)
            {
                StopCoroutine(_gameRoutine);
                _gameRoutine = null;
            }

            ResetMenuState();
            ResetRunState();
        }
        #region 360 Markers
        private void AddDirectionalMarkersIfNeeded()
        {
            if (_menuMarkersAdded)
                return;

            GameObject wrapper = FindMainMenuWrapper();
            if (wrapper == null)
            {
                Plugin.LogDebug("[EnvGreen] Wrapper not found. Cannot add directional markers.");
                return;
            }

            Transform menuEnvironmentManager = wrapper.transform.Find("MenuEnvironmentManager");
            if (menuEnvironmentManager == null)
                return;

            Transform defaultMenuEnvironment = menuEnvironmentManager.Find("DefaultMenuEnvironment");
            if (defaultMenuEnvironment == null)
                return;

            Transform levitatingNote = defaultMenuEnvironment.Find("Notes/LevitatingNote/Note (16)");
            if (levitatingNote == null)
                return;

            Transform noteArrow = levitatingNote.Find("NoteArrow");
            Transform noteArrowGlow = levitatingNote.Find("NoteArrowGlow");
            if (noteArrow == null || noteArrowGlow == null)
                return;

            float radius = 4.2f;
            float height = 1.3f;

            float[] angles = { 90f, 100f, 110f, 160f, 170f, 179f, 181f, 190f, 200f, 250f, 260f, 270f };
            float[] scales = { 0.4f, 0.7f, 1f, 0.4f, 0.7f, 1f, 1f, 0.7f, 0.4f, 0.4f, 0.7f, 1f };

            Quaternion pointTo0 = Quaternion.Euler(0, 5, -90);
            Quaternion pointTo360 = Quaternion.Euler(0, -5, 90);

            for (int i = 0; i < angles.Length; i++)
            {
                float angle = angles[i];
                float scale = scales[i];
                Vector3 position = CalculateMarkerPosition(angle, radius, height);

                Vector3 direction = (position - new Vector3(0, height, 0)).normalized;
                Quaternion rotation = Quaternion.LookRotation(direction) * ((angle < 180f) ? pointTo0 : pointTo360);

                GameObject arrowInstance = Instantiate(noteArrow.gameObject, position, rotation, wrapper.transform);
                arrowInstance.transform.localScale = new Vector3(scale, scale, scale);
                arrowInstance.name = $"DirectionArrow_{i}";
                arrowInstance.SetActive(true);
                _markers.Add(arrowInstance);

                GameObject glowInstance = Instantiate(noteArrowGlow.gameObject, position, rotation, wrapper.transform);
                glowInstance.transform.localScale = new Vector3(scale, scale, scale);
                glowInstance.name = $"DirectionArrowGlow_{i}";
                glowInstance.SetActive(true);
                _markers.Add(glowInstance);

                arrowInstance.layer = wrapper.layer;
                glowInstance.layer = wrapper.layer;
            }

            _menuMarkersAdded = true;
            Plugin.LogDebug("[EnvGreen] Direction markers added.");
        }

        private void RemoveDirectionalMarkers()
        {
            if (!_menuMarkersAdded)
                return;

            foreach (GameObject marker in _markers)
            {
                if (marker != null)
                    Destroy(marker);
            }

            _markers.Clear();
            _menuMarkersAdded = false;
        }

        private GameObject FindMainMenuWrapper()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded || scene.name != "MainMenu")
                    continue;

                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    if (root.name == "Wrapper")
                        return root;
                }
            }

            return null;
        }

        private static Vector3 CalculateMarkerPosition(float angle, float radius, float height)
        {
            float radian = angle * Mathf.Deg2Rad;
            float x = Mathf.Sin(radian) * radius;
            float z = Mathf.Cos(radian) * radius;
            return new Vector3(x, height, z);
        }

        #endregion

        private void EnsureMenuGreenScreen()
        {
            if (!Config.Instance.EnableGreenScreen)
            {
                ResetMenuState();
                return;
            }

            if (_isGameplayLoaded)
                return;

            if (!_isMainMenuLoaded)
                return;

            if (_menuShell != null)
                return;

            if (_menuRoutine != null)
            {
                StopCoroutine(_menuRoutine);
                _menuRoutine = null;
            }

            Plugin.Log.Info("[EnvGreen] Ensuring menu greenscreen");
            _menuRoutine = StartCoroutine(SetupMenuGreenScreen());
        }

        private IEnumerator InitialWaitAndCheck()
        {
            if (!Config.Instance.EnableGreenScreen)
            {
                _gameRoutine = null;
                yield break;
            }

            float totalWaitTime = 0f;
            const float waitInterval = 0.5f;

            while (totalWaitTime < 10f)
            {
                GameObject environment = FindEnvironment();
                if (environment != null)
                {
                    Plugin.Log.Info($"[EnvGreen] Environment found: {environment.name}");
                    //LogRenderers(environment);
                    //LogPossibleNoteRenderers();
                    SetupGamePlayGreenScreen(environment);

                    _gameRoutine = null;

                    // DO NOT destroy the finder — we need it alive to follow the headset
                    //if (environment.scene.name != "GlassDesertEnvironment")
                    //    Destroy(gameObject);

                    yield break;
                }

                totalWaitTime += waitInterval;
                yield return new WaitForSecondsRealtime(waitInterval);
            }

            Plugin.Log.Info("[EnvGreen] Environment not found within wait time");
            _gameRoutine = null;
            yield break;

        }
        private IEnumerator SetupMenuGreenScreen()
        {
            if (!Config.Instance.EnableGreenScreen)
            {
                ResetMenuState();
                _menuRoutine = null;
                yield break;
            }

            Plugin.Log.Info("[EnvGreen] SetupMenuGreenScreen started");

            float elapsed = 0f;

            while (elapsed < 10f)
            {
                GameObject environment = FindMenuEnvironment();

                if (environment != null)
                {
                    Plugin.Log.Info($"[EnvGreen] Menu environment found: {environment.name} scene={environment.scene.name} path={GetPath(environment.transform, environment.transform.root)}");

                    if (_menuShell != null)
                    {
                        Destroy(_menuShell);
                        _menuShell = null;
                    }

                    Material greenMat = GetGreenMaterial();
                    if (greenMat == null)
                    {
                        Plugin.Log.Info("[EnvGreen] Could not build green material");
                        _menuRoutine = null;
                        yield break;
                    }

                    _menuShell = new GameObject("EnvGreen_MenuShell");
                    _menuShell.transform.SetParent(environment.transform, false);
                    _menuShell.transform.localPosition = Vector3.zero;
                    _menuShell.transform.localRotation = Quaternion.identity;

                    CreateQuadBox(_menuShell.transform, greenMat); // A box to hide the group of notes on the floor in the menu environment behind the player

                    if (Config.Instance.GreenScreenRound)
                        CreateRoundAperature(_menuShell.transform, greenMat, depth: 5, zOffset: Config.Instance.GreenScreenMenuZOffset, menuOverrideHeight: true);
                    else
                        CreateRectangularAperture(_menuShell.transform, greenMat, width: 7, height: 5, zOffset: Config.Instance.GreenScreenMenuZOffset, menuOverrideHeight: true);

                    //_lastMenuCircular = Config.Instance.GreenScreenCircular;
                    //_lastMenuYOffset  = Config.Instance.GreenScreenMenuYOffset;
                    //_lastMenuZOffset  = Config.Instance.GreenScreenMenuZOffset;

                    Plugin.Log.Info(
                        $"[EnvGreen] Menu shell worldPos={_menuShell.transform.position} " +
                        $"localPos={_menuShell.transform.localPosition} " +
                        $"parent={_menuShell.transform.parent?.name}");

                    _menuRoutine = null;

                    yield break;
                }

                if (!_loggedMainMenuWrapper)
                {
                    _loggedMainMenuWrapper = true;
                    //LogMainMenuWrapper();
                }

                yield return new WaitForSecondsRealtime(0.25f);
                elapsed += 0.25f;
            }

            Plugin.Log.Info("[EnvGreen] SetupMenuGreenScreen timed out");
            _menuRoutine = null;
        }

        
        /*
        private static void LogUiCandidatesInMainMenu()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded || scene.name != "MainMenu")
                    continue;

                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    if (root.name != "Wrapper")
                        continue;

                    Plugin.Log.Info("[EnvGreen] ===== UI CANDIDATES START =====");

                    var transforms = root.GetComponentsInChildren<Transform>(true);
                    foreach (var t in transforms)
                    {
                        Component[] comps = t.GetComponents<Component>();
                        foreach (var c in comps)
                        {
                            if (c == null)
                                continue;

                            string typeName = c.GetType().FullName;
                            string lower = typeName.ToLowerInvariant();

                            if (lower.Contains("image") ||
                                lower.Contains("canvas") ||
                                lower.Contains("textmesh") ||
                                lower.Contains("curved") ||
                                lower.Contains("hmui") ||
                                lower.Contains("sprite") ||
                                lower.Contains("renderer"))
                            {
                                Plugin.Log.Info($"[EnvGreen] GO: {t.gameObject.name} | Path: {GetPathFromRoot(t, root.transform)} | Component: {typeName}");
                            }
                        }
                    }

                    Plugin.Log.Info("[EnvGreen] ===== UI CANDIDATES END =====");
                    return;
                }
            }
        }
        */
        
        
        private GameObject FindMenuEnvironment()
        {
            if (_isGameplayLoaded)
                return null;

            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded)
                    continue;

                // First: startup/main menu environment under Wrapper
                if (scene.name == "MainMenu")
                {
                    foreach (GameObject root in scene.GetRootGameObjects())
                    {
                        if (root.name != "Wrapper")
                            continue;

                        Transform menuEnvManager = root.transform.Find("MenuEnvironmentManager");
                        if (menuEnvManager != null)
                        {
                            // Prefer the specific default menu environment
                            Transform defaultMenuEnv = menuEnvManager.Find("DefaultMenuEnvironment");
                            if (defaultMenuEnv != null)
                                return defaultMenuEnv.gameObject;

                            // Fallback: first child under MenuEnvironmentManager
                            if (menuEnvManager.childCount > 0)
                                return menuEnvManager.GetChild(0).gameObject;
                        }
                    }
                }

                // Fallback for other non-gameplay scenes that may expose a plain Environment root
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    if (root.name == "Environment")
                        return root;

                    Transform childEnv = root.transform.Find("Environment");
                    if (childEnv != null)
                        return childEnv.gameObject;
                }
            }

            return null;
        }

        private GameObject FindEnvironment()
        {
            int sceneCount = SceneManager.sceneCount;

            for (int i = 0; i < sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded)
                    continue;

                //Plugin.Log.Info($"[EnvGreen] Checking scene: {scene.name}");

                foreach (GameObject obj in scene.GetRootGameObjects())
                {
                    //Plugin.Log.Info($"[EnvGreen] Root: {obj.name}");

                    if (obj.name == "Environment")
                    {
                        Plugin.Log.Info($"[EnvGreen] Exact Environment root found in scene {scene.name}");
                        return obj;
                    }
                }
            }

            return null;
        }
        
        private void SetupGamePlayGreenScreen(GameObject root)
        {
            if (_gameplayShell != null)
            {
                Destroy(_gameplayShell);
                _gameplayShell = null;
            }

            Plugin.Log.Info("[EnvGreen] Applying selective green test");

            OffsetPlayerFeetSymbol(root.transform, 1.2f);

            var renderers = root.GetComponentsInChildren<Renderer>(true);

            Material greenMat = GetGreenMaterial();
            if (greenMat == null)
            {
                Plugin.Log.Info("[EnvGreen] Could not build green material");
                return;
            }

            Renderer playersPlaceConstruction = null;
            Renderer playersPlaceMirror = null;

            foreach (var r in renderers)
            {
                string path = GetPath(r.transform, root.transform);

                if (path == "Environment/PlayersPlace/Construction")
                    playersPlaceConstruction = r;

                if (path == "Environment/PlayersPlace/Mirror")
                    playersPlaceMirror = r;
            }
            if (playersPlaceConstruction != null)
            {
                playersPlaceConstruction.enabled = false;
                Plugin.Log.Info("[EnvGreen] Disabled PlayersPlace/Construction");
            }

            if (playersPlaceMirror != null)
            {
                playersPlaceMirror.enabled = false;
                Plugin.Log.Info("[EnvGreen] Disabled PlayersPlace/Mirror");
            }

            if (root.scene.name == "GlassDesertEnvironment")
            {
                if (Config.Instance.GreenScreen360Diameter <= 0) return;

                Plugin.Log.Info("[EnvGreen] GlassDesertEnvironment detected.");

                _gameplayShell = CreateRadialFloorFan(
                    root.transform,
                    greenMat,
                    diameter: Config.Instance.GreenScreen360Diameter,
                    createCeiling: Config.Instance.GreenScreen360HasCeiling,
                    blades: 36,
                    floorHeight: 0.001f);

                Plugin.Log.Info("[EnvGreen] Spawned 360 floor green screen.");
            }
            else
            {
                if (Config.Instance.GreenScreenRound)
                {
                    //greenMat = BuildChromaMaterial(noteArrowRenderer);
                    //_spawnedShell = CreateInwardQuadBox(root.transform, greenMat, 130f);
                    _gameplayShell = CreateRoundAperature(root.transform, greenMat, diameter: Config.Instance.GreenScreenRoundDiameter, zOffset: Config.Instance.GreenScreenGamePlayZOffset);
                }
                else
                {
                    _gameplayShell = CreateRectangularAperture(root.transform, greenMat, width: Config.Instance.GreenScreenRectWidth, height: Config.Instance.GreenScreenRectHeight, zOffset: Config.Instance.GreenScreenGamePlayZOffset);
                }
            }
        }
        
        private static Renderer FindRendererByNameAcrossScenes(string objectName)
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded)
                    continue;

                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    var renderers = root.GetComponentsInChildren<Renderer>(true);
                    foreach (var r in renderers)
                    {
                        if (r.gameObject.name == objectName)
                            return r;
                    }
                }
            }

            return null;
        }
        private static GameObject CreateRadialFloorFan(
            Transform parent,
            Material mat,
            float diameter = 10f,
            int blades = 36,
            float floorHeight = 0.001f,              // floor height
            bool createCeiling = true,
            float ceilingHeight = 2.8f
        )
        {
            GameObject root = new GameObject("EnvGreen_RadialFloor");

            if (parent != null)
                root.transform.SetParent(parent, false);

            root.transform.localPosition = new Vector3(0f, floorHeight, 0f);
            root.transform.localRotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;

            float radius = diameter * 0.5f;
            float angleStep = 360f / blades;

            for (int i = 0; i < blades; i++)
            {
                float angleDeg0 = i * angleStep;
                float angleDeg1 = (i + 1) * angleStep;

                float angleRad0 = angleDeg0 * Mathf.Deg2Rad;
                float angleRad1 = angleDeg1 * Mathf.Deg2Rad;

                Vector3 outer0 = new Vector3(Mathf.Cos(angleRad0) * radius, 0f, Mathf.Sin(angleRad0) * radius);
                Vector3 outer1 = new Vector3(Mathf.Cos(angleRad1) * radius, 0f, Mathf.Sin(angleRad1) * radius);

                Vector3 outerMid = (outer0 + outer1) * 0.5f;
                float bladeLength = outerMid.magnitude;
                float bladeWidth = Vector3.Distance(outer0, outer1) * 1.05f;

                // Floor: visible from above / inside playspace
                CreateQuad(
                    root.transform,
                    $"FloorBlade_{i}",
                    outerMid * 0.5f,
                    Quaternion.LookRotation(Vector3.down, outerMid.normalized),
                    new Vector3(bladeWidth, bladeLength, 1f),
                    mat);

                if (createCeiling)
                {
                    // Ceiling: same blade, moved up, normals flipped toward playspace
                    CreateQuad(
                        root.transform,
                        $"CeilingBlade_{i}",
                        outerMid * 0.5f + new Vector3(0f, ceilingHeight, 0f),
                        Quaternion.LookRotation(Vector3.up, outerMid.normalized),
                        new Vector3(bladeWidth, bladeLength, 1f),
                        mat);
                }
            }

            return root;
        }

        private static GameObject CreateRoundAperature(
            Transform parent,
            Material mat,
            float diameter = 7f,         // circular opening diameter
            float depth = 7f,
            float zOffset = 0f,          // front opening ends up at zOffset
            bool createFloor = true,     // unused; outer box already has a floor
            int segments = 36,
            float outerDiameter = 13f,   // used as outer box width
            bool createBackCap = true,
            bool createFrontIris = true,
            bool menuOverrideHeight = false
        )
        {
            GameObject root = new GameObject("EnvGreen_RoundApertureBox");

            if (parent != null)
                root.transform.SetParent(parent, false);

            const float minOuterHeight = 2.5f;

            if (Config.Instance.MixedRealityModeTest)
            {
                if (menuOverrideHeight)
                {
                    diameter = 0.1f;
                    outerDiameter = 50f;
                    depth = 45f;
                }
                else
                {
                    diameter = 60f;
                    outerDiameter = 100f;
                    depth = 150f;
                }

                zOffset = depth - 10f;
                menuOverrideHeight = false;
            }

            if (diameter > outerDiameter)
                outerDiameter = diameter;

            float apertureRadius = diameter * 0.5f;
            float outerWidth = Mathf.Max(outerDiameter, diameter);
            float outerHalfW = outerWidth * 0.5f;

            float halfDepth = depth * 0.5f;
            float frontZ = +halfDepth;
            float backZ = -halfDepth;

            bool useCustomHeight =
                Config.Instance.GreenScreenRoundHeightMode == Config.GreenScreenHeightMode.CenterAtCustomHeight;

            float customCenterHeight = Config.Instance.GreenScreenCustomCenterHeight;

            if (menuOverrideHeight)
            {
                useCustomHeight = true;
                customCenterHeight = 2f; // aperture bottom is below floor. i like this look for menus
            }

            // Real floor level of the box
            float floorWorld = menuOverrideHeight
                ? menuFloorHeight
                : gameplayFloorHeight;

            // Aperture center can now be anywhere, including below floor
            float apertureCenterYWorld;
            if (useCustomHeight)
            {
                apertureCenterYWorld = customCenterHeight;
            }
            else
            {
                // default: circle sits on floor
                apertureCenterYWorld = floorWorld + apertureRadius;
            }

            float apertureTopWorld = apertureCenterYWorld + apertureRadius;

            // Box bottom remains on floor; top expands only if needed
            float effectiveOuterHeight = Mathf.Max(minOuterHeight, apertureTopWorld - floorWorld);
            float outerHalfH = effectiveOuterHeight * 0.5f;
            float centerHeight = floorWorld + outerHalfH;

            // Aperture center relative to root
            float apertureCenterY = apertureCenterYWorld - centerHeight;
            Vector2 apertureCenter2 = new Vector2(0f, apertureCenterY);

            // In local box space, floor is always at bottom of rectangle
            float localFloorY = -outerHalfH;

            // Outer front rectangle
            Vector3 fTL = new Vector3(-outerHalfW, +outerHalfH, frontZ);
            Vector3 fTR = new Vector3(+outerHalfW, +outerHalfH, frontZ);
            Vector3 fBR = new Vector3(+outerHalfW, -outerHalfH, frontZ);
            Vector3 fBL = new Vector3(-outerHalfW, -outerHalfH, frontZ);

            // Outer back rectangle
            Vector3 bTL = new Vector3(-outerHalfW, +outerHalfH, backZ);
            Vector3 bTR = new Vector3(+outerHalfW, +outerHalfH, backZ);
            Vector3 bBR = new Vector3(+outerHalfW, -outerHalfH, backZ);
            Vector3 bBL = new Vector3(-outerHalfW, -outerHalfH, backZ);

            void CreateWall(string name, Vector3 a0, Vector3 a1, Vector3 b0, Vector3 b1, Vector3 forwardHint)
            {
                Vector3 edgeMidA = (a0 + a1) * 0.5f;
                Vector3 edgeMidB = (b0 + b1) * 0.5f;

                Vector3 up = (edgeMidA - edgeMidB).normalized;
                float wallHeight = Vector3.Distance(edgeMidA, edgeMidB);
                float wallWidth = (Vector3.Distance(a0, a1) + Vector3.Distance(b0, b1)) * 0.5f;
                Vector3 wallCenter = (edgeMidA + edgeMidB) * 0.5f;

                Vector3 forward = forwardHint.normalized;
                Vector3 right = Vector3.Cross(up, forward).normalized;
                forward = Vector3.Cross(right, up).normalized;

                CreateQuad(
                    root.transform,
                    name,
                    wallCenter,
                    Quaternion.LookRotation(forward, up),
                    new Vector3(wallWidth, wallHeight, 1f),
                    mat);
            }

            CreateWall("TopWall", fTL, fTR, bTL, bTR, Vector3.up);
            CreateWall("RightWall", fTR, fBR, bTR, bBR, Vector3.right);
            CreateWall("BottomWall", fBR, fBL, bBR, bBL, Vector3.down);
            CreateWall("LeftWall", fBL, fTL, bBL, bTL, Vector3.left);

            if (createFrontIris)
            {
                float angleStep = 360f / segments;

                // Distance from aperture center to the farthest visible front-rectangle corner.
                // This lets every blade extend well past the rectangular opening.
                Vector2[] frontCorners = new Vector2[]
                {
                    new Vector2(-outerHalfW,  outerHalfH),
                    new Vector2( outerHalfW,  outerHalfH),
                    new Vector2( outerHalfW,  localFloorY),
                    new Vector2(-outerHalfW,  localFloorY),
                };

                float farthestCornerDist = 0f;
                for (int c = 0; c < frontCorners.Length; c++)
                {
                    float d = Vector2.Distance(apertureCenter2, frontCorners[c]);
                    if (d > farthestCornerDist)
                        farthestCornerDist = d;
                }

                // Make blades extend noticeably beyond the opening so no gaps appear.
                float bladeLength = Mathf.Max(0.01f, farthestCornerDist - apertureRadius) * 1.15f;

                for (int i = 0; i < segments; i++)
                {
                    float angleDeg0 = i * angleStep;
                    float angleDeg1 = (i + 1) * angleStep;
                    float angleDegMid = (angleDeg0 + angleDeg1) * 0.5f;

                    float angleRad0 = angleDeg0 * Mathf.Deg2Rad;
                    float angleRad1 = angleDeg1 * Mathf.Deg2Rad;
                    float angleRadMid = angleDegMid * Mathf.Deg2Rad;

                    Vector2 dir0 = new Vector2(Mathf.Cos(angleRad0), Mathf.Sin(angleRad0)).normalized;
                    Vector2 dir1 = new Vector2(Mathf.Cos(angleRad1), Mathf.Sin(angleRad1)).normalized;
                    Vector2 dirMid = new Vector2(Mathf.Cos(angleRadMid), Mathf.Sin(angleRadMid)).normalized;

                    // True radial line from the aperture center.
                    Vector3 radial3 = new Vector3(dirMid.x, dirMid.y, 0f);

                    // Start at the circular opening edge, extend well beyond the front rectangle.
                    Vector3 innerMid = new Vector3(
                        apertureCenter2.x + dirMid.x * apertureRadius,
                        apertureCenter2.y + dirMid.y * apertureRadius,
                        frontZ);

                    Vector3 outerMid = new Vector3(
                        apertureCenter2.x + dirMid.x * (apertureRadius + bladeLength),
                        apertureCenter2.y + dirMid.y * (apertureRadius + bladeLength),
                        frontZ);

                    // Skip blades that are completely below the floor.
                    if (innerMid.y <= localFloorY && outerMid.y <= localFloorY)
                        continue;

                    Vector3 bladeCenter = (innerMid + outerMid) * 0.5f;
                    Vector3 up = radial3.normalized;
                    float bladeHeight = Vector3.Distance(innerMid, outerMid);

                    // Width must be based on the far end, not just near the aperture,
                    // otherwise neighboring blades separate near the box edges.
                    float farRadius = apertureRadius + bladeLength;
                    float bladeWidth = 2f * farRadius * Mathf.Tan((angleStep * Mathf.Deg2Rad) * 0.5f) * 1.15f;

                    if (bladeHeight > 0.0001f && bladeWidth > 0.0001f)
                    {
                        Vector3 forward = Vector3.forward;
                        Vector3 right = Vector3.Cross(up, forward).normalized;
                        forward = Vector3.Cross(right, up).normalized;

                        CreateQuad(
                            root.transform,
                            $"IrisSegment_{i}",
                            bladeCenter,
                            Quaternion.LookRotation(forward, up),
                            new Vector3(bladeWidth, bladeHeight, 1f),
                            mat);
                    }
                }
            }

            if (createBackCap)
            {
                CreateQuad(
                    root.transform,
                    "BackCap",
                    new Vector3(0f, 0f, backZ),
                    Quaternion.Euler(0f, 180f, 0f),
                    new Vector3(outerWidth, effectiveOuterHeight, 1f),
                    mat);
            }

            root.transform.localPosition = new Vector3(0f, centerHeight, zOffset - halfDepth);
            root.transform.localRotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;

            Plugin.Log.Info($"[EnvGreen] Spawned round aperture with rectangular outer box ({segments} iris segments)");
            return root;
        }

        private static GameObject CreateRectangularAperture(
            Transform parent,
            Material mat,
            float zOffset = 0f,      // front opening ends up at zOffset, 0 is center of playspace
            float width = 3f,        // inner aperture width
            float height = 2f,       // inner aperture height
            float depth = 4f,
            float outerWidth = 9f,   // minimum outer box width; expands if aperture needs more
            float outerHeight = 2.5f,  // minimum outer box height; expands if aperture needs more
            bool createBackCap = true,
            bool createFrontIris = true,
            bool menuOverrideHeight = false
        )
        {
            GameObject root = new GameObject("EnvGreen_RectTunnel");

            if (parent != null)
                root.transform.SetParent(parent, false);

            const float floorOffset = 0.003f;

            float halfDepth = depth * 0.5f;
            float frontZ = +halfDepth;
            float backZ = -halfDepth;

            float innerHalfW = width * 0.5f;
            float innerHalfH = height * 0.5f;

            // Outer box must at least contain the aperture width
            outerWidth = Mathf.Max(outerWidth, width);

            bool useCustomHeight =
                Config.Instance.GreenScreenRectHeightMode == Config.GreenScreenHeightMode.CenterAtCustomHeight;

            if (menuOverrideHeight)
                useCustomHeight = false;

            // Aperture world-space placement
            float apertureCenterYWorld;
            float apertureBottomWorld;

            if (useCustomHeight)
            {
                apertureCenterYWorld = Config.Instance.GreenScreenCustomCenterHeight;
                apertureBottomWorld = apertureCenterYWorld - innerHalfH;
            }
            else
            {
                apertureBottomWorld = floorOffset;
                apertureCenterYWorld = apertureBottomWorld + innerHalfH;
            }

            float apertureTopWorld = apertureBottomWorld + height;

            // Outer box must be tall enough to contain the aperture
            float effectiveOuterHeight = Mathf.Max(outerHeight, apertureTopWorld - floorOffset);
            float outerHalfW = outerWidth * 0.5f;
            float outerHalfH = effectiveOuterHeight * 0.5f;

            // Outer box bottom anchored to floor
            float centerHeight = floorOffset + outerHalfH;

            // Aperture center relative to the box root
            float apertureCenterY = apertureCenterYWorld - centerHeight;

            // Inner front rectangle (aperture)
            Vector3 iTL = new Vector3(-innerHalfW, apertureCenterY + innerHalfH, frontZ);
            Vector3 iTR = new Vector3(+innerHalfW, apertureCenterY + innerHalfH, frontZ);
            Vector3 iBR = new Vector3(+innerHalfW, apertureCenterY - innerHalfH, frontZ);
            Vector3 iBL = new Vector3(-innerHalfW, apertureCenterY - innerHalfH, frontZ);

            // Outer front rectangle
            Vector3 fTL = new Vector3(-outerHalfW, +outerHalfH, frontZ);
            Vector3 fTR = new Vector3(+outerHalfW, +outerHalfH, frontZ);
            Vector3 fBR = new Vector3(+outerHalfW, -outerHalfH, frontZ);
            Vector3 fBL = new Vector3(-outerHalfW, -outerHalfH, frontZ);

            // Outer back rectangle
            Vector3 bTL = new Vector3(-outerHalfW, +outerHalfH, backZ);
            Vector3 bTR = new Vector3(+outerHalfW, +outerHalfH, backZ);
            Vector3 bBR = new Vector3(+outerHalfW, -outerHalfH, backZ);
            Vector3 bBL = new Vector3(-outerHalfW, -outerHalfH, backZ);

            void CreateWall(string name, Vector3 a0, Vector3 a1, Vector3 b0, Vector3 b1, Vector3 forwardHint)
            {
                Vector3 edgeMidA = (a0 + a1) * 0.5f;
                Vector3 edgeMidB = (b0 + b1) * 0.5f;

                Vector3 up = (edgeMidA - edgeMidB).normalized;

                float wallHeight = Vector3.Distance(edgeMidA, edgeMidB);
                float wallWidth = (Vector3.Distance(a0, a1) + Vector3.Distance(b0, b1)) * 0.5f;

                Vector3 wallCenter = (edgeMidA + edgeMidB) * 0.5f;

                Vector3 forward = forwardHint.normalized;
                Vector3 right = Vector3.Cross(up, forward).normalized;
                forward = Vector3.Cross(right, up).normalized;

                CreateQuad(
                    root.transform,
                    name,
                    wallCenter,
                    Quaternion.LookRotation(forward, up),
                    new Vector3(wallWidth, wallHeight, 1f),
                    mat);
            }

            // Outer box walls
            CreateWall("TopWall", fTL, fTR, bTL, bTR, Vector3.up);
            CreateWall("RightWall", fTR, fBR, bTR, bBR, Vector3.right);
            CreateWall("BottomWall", fBR, fBL, bBR, bBL, Vector3.down);
            CreateWall("LeftWall", fBL, fTL, bBL, bTL, Vector3.left);

            if (createFrontIris)
            {
                float apertureTopLocal = apertureCenterY + innerHalfH;
                float apertureBottomLocal = apertureCenterY - innerHalfH;

                float topBandHeight = outerHalfH - apertureTopLocal;
                float bottomBandHeight = apertureBottomLocal + outerHalfH;
                float leftRightWidth = (outerWidth - width) * 0.5f;

                float topY = (outerHalfH + apertureTopLocal) * 0.5f;
                float bottomY = (-outerHalfH + apertureBottomLocal) * 0.5f;
                float leftX = -(outerHalfW + innerHalfW) * 0.5f;
                float rightX = (outerHalfW + innerHalfW) * 0.5f;

                if (topBandHeight > 0.0001f)
                {
                    CreateQuad(
                        root.transform,
                        "TopIris",
                        new Vector3(0f, topY, frontZ),
                        Quaternion.identity,
                        new Vector3(outerWidth, topBandHeight, 1f),
                        mat);
                }

                // Only create bottom iris if aperture is lifted above floor
                if (bottomBandHeight > 0.0001f)
                {
                    CreateQuad(
                        root.transform,
                        "BottomIris",
                        new Vector3(0f, bottomY, frontZ),
                        Quaternion.identity,
                        new Vector3(outerWidth, bottomBandHeight, 1f),
                        mat);
                }

                if (leftRightWidth > 0.0001f)
                {
                    CreateQuad(
                        root.transform,
                        "LeftIris",
                        new Vector3(leftX, apertureCenterY, frontZ),
                        Quaternion.identity,
                        new Vector3(leftRightWidth, height, 1f),
                        mat);

                    CreateQuad(
                        root.transform,
                        "RightIris",
                        new Vector3(rightX, apertureCenterY, frontZ),
                        Quaternion.identity,
                        new Vector3(leftRightWidth, height, 1f),
                        mat);
                }
            }

            if (createBackCap)
            {
                CreateQuad(
                    root.transform,
                    "BackCap",
                    new Vector3(0f, 0f, backZ),
                    Quaternion.Euler(0f, 180f, 0f),
                    new Vector3(outerWidth, effectiveOuterHeight, 1f),
                    mat);
            }

            // Front opening sits at zOffset
            root.transform.localPosition = new Vector3(0f, centerHeight, zOffset - halfDepth);
            root.transform.localRotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;

            Plugin.Log.Info("[EnvGreen] Spawned inward rectangular aperture tunnel");
            return root;
        }
        /// <summary>
        /// A box to hide the group of notes on the floor in the menu environment behind the player
        /// </summary>
        /// <param name="parent"></param>
        /// <param name="mat"></param>
        /// <param name="width"></param>
        /// <param name="height"></param>
        /// <param name="depth"></param>
        /// <param name="zOffset"></param>
        /// <returns></returns>
        private static GameObject CreateQuadBox(Transform parent, Material mat, float width = 4f, float height = .75f, float depth = 2.2f, float zOffset = -2.4f)
        {
            GameObject boxRoot = new GameObject("EnvGreen_QuadBox");

            if (parent != null)
                boxRoot.transform.SetParent(parent, false);

            float halfW = width * 0.5f;
            float halfH = height * 0.5f;
            float halfD = depth * 0.5f;



            boxRoot.transform.localPosition = new Vector3(0, halfH, zOffset); //Vector3.zero;
            boxRoot.transform.localRotation = Quaternion.identity;
            boxRoot.transform.localScale = Vector3.one;


            // Back wall
            CreateQuad(boxRoot.transform, "Back",
                new Vector3(0f, 0f, -halfD),
                Quaternion.Euler(0f, 0f, 180f),
                new Vector3(width, height, 1f),
                mat);

            // Front wall
            CreateQuad(boxRoot.transform, "Front",
                new Vector3(0f, 0f, halfD),
                Quaternion.Euler(0f, 180f, 0f),
                new Vector3(width, height, 1f),
                mat);

            // Left wall
            CreateQuad(boxRoot.transform, "Left",
                new Vector3(-halfW, 0f, 0f),
                Quaternion.Euler(0f, 90f, 0f),
                new Vector3(depth, height, 1f),
                mat);

            // Right wall
            CreateQuad(boxRoot.transform, "Right",
                new Vector3(halfW, 0f, 0f),
                Quaternion.Euler(0f, -90f, 0f),
                new Vector3(depth, height, 1f),
                mat);

            // Ceiling
            CreateQuad(boxRoot.transform, "Ceiling",
                new Vector3(0f, halfH, 0f),
                Quaternion.Euler(90f, 0f, 0f),
                new Vector3(width, depth, 1f),
                mat);

            // Floor
            //CreateQuad(boxRoot.transform, "Floor",
            //    new Vector3(0f, -halfH, 0f),
            //    Quaternion.Euler(90f, 0f, 0f),
            //    new Vector3(width, depth, 1f),
            //    mat);

            Plugin.Log.Info("[EnvGreen] Menu box to hide back decorative notes.");
            return boxRoot;
        }

        private static GameObject CreateQuad(
            Transform parent,
            string name,
            Vector3 localPosition,
            Quaternion localRotation,
            Vector3 localScale,
            Material mat)
        {
            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = name;
            quad.transform.SetParent(parent, false);
            quad.transform.localPosition = localPosition;
            quad.transform.localRotation = localRotation;
            quad.transform.localScale = localScale;

            var renderer = quad.GetComponent<Renderer>();
            if (renderer != null)
                renderer.material = mat;

            var collider = quad.GetComponent<Collider>();
            if (collider != null)
                Destroy(collider);

            return quad;
        }

        private static string GetPath(Transform current, Transform root)
        {
            if (current == root)
                return root.name;

            var sb = new StringBuilder(current.name);
            var t = current.parent;

            while (t != null)
            {
                sb.Insert(0, t.name + "/");
                if (t == root)
                    break;
                t = t.parent;
            }

            return sb.ToString();
        }

        private static Material GetGreenMaterial()
        {
            if (_greenTemplateMat == null)
            {
                Renderer noteArrowRenderer = FindRendererByNameAcrossScenes("NoteArrow");
                if (noteArrowRenderer == null || noteArrowRenderer.sharedMaterial == null)
                    return null;

                Material src = noteArrowRenderer.sharedMaterial;
                Material m = new Material(src);

                UnityEngine.Color chroma = Config.Instance.GreenScreenColor; // new UnityEngine.Color(0f, 5f, 0f, 1f); // very bright green

                if (m.HasProperty("_Color"))
                    m.SetColor("_Color", chroma);

                if (m.HasProperty("_BaseColor"))
                    m.SetColor("_BaseColor", chroma);

                if (m.HasProperty("_EmissionColor"))
                    m.SetColor("_EmissionColor", chroma * 5f);

                if (m.HasProperty("_MainTex"))
                    m.SetTexture("_MainTex", null);

                if (m.HasProperty("_BaseMap"))
                    m.SetTexture("_BaseMap", null);

                if (m.HasProperty("_Glossiness"))
                    m.SetFloat("_Glossiness", 0f);

                if (m.HasProperty("_Metallic"))
                    m.SetFloat("_Metallic", 0f);

                _greenTemplateMat = m;
                _greenTemplateShaderName = src.shader != null ? src.shader.name : "null";
            }

            return new Material(_greenTemplateMat);
        }
        private static void OffsetPlayerFeetSymbol(Transform environmentRoot, float inches = 1f)
        {
            if (environmentRoot == null)
                return;

            float yOffset = inches * 0.0254f;

            Transform feet = environmentRoot.Find("PlayersPlace/Feet");
            if (feet != null)
            {
                Vector3 p = feet.localPosition;
                feet.localPosition = new Vector3(p.x, p.y + yOffset, p.z);
                Plugin.Log.Info($"[EnvGreen] Offset Feet by {yOffset:F4}m");
            }
            
            Transform construction = environmentRoot.Find("PlayersPlace/RectangleFakeGlow");
            if (construction != null)
            {
                Vector3 p = construction.localPosition;
                construction.localPosition = new Vector3(p.x, p.y + yOffset, p.z);
                Plugin.Log.Info($"[EnvGreen] Offset Construction by {yOffset:F4}m");
            }
            
        }

        /*
        private void LateUpdate()
        {
            if (!Config.Instance.EnableGreenScreen)
            {
                DisableAllGreenScreens();
                return;
            }
            
            //if (!_loggedHubScreenState && !_isGameplayLoaded && _isMainMenuLoaded)
            //{
            //    _loggedHubScreenState = true;
            //    LogHubScreenState();
            //}
            
            if (!_isGameplayLoaded && _isMainMenuLoaded && _menuShell != null && MenuShapeSettingsChanged())
            {
                Destroy(_menuShell);
                _menuShell = null;
            }

            if (!_isGameplayLoaded && _isMainMenuLoaded && _menuShell == null && _menuRoutine == null)
            {
                Plugin.Log.Info("[EnvGreen] LateUpdate detected missing menu shell; starting scan");
                _menuRoutine = StartCoroutine(SetupMenuGreenScreen());
            }

            if (_isGameplayLoaded && _spawnedShell == null && _gameRoutine == null)
            {
                Plugin.Log.Info("[EnvGreen] LateUpdate detected missing gameplay shell; starting scan");
                _gameRoutine = StartCoroutine(InitialWaitAndCheck());
            }
        }
        private void DisableAllGreenScreens()
        {
            if (_gameRoutine != null)
            {
                StopCoroutine(_gameRoutine);
                _gameRoutine = null;
            }

            if (_menuRoutine != null)
            {
                StopCoroutine(_menuRoutine);
                _menuRoutine = null;
            }

            if (_spawnedShell != null)
            {
                Destroy(_spawnedShell);
                _spawnedShell = null;
            }

            if (_menuShell != null)
            {
                Destroy(_menuShell);
                _menuShell = null;
            }

            //_playerTransforms = null;
            _headTransform = null;
            _tryingToFindHead = false;
            _lockShellToHead = false;
            _startedScan = false;
        }
        */
        /*
        // This locks floor to head
        private void LateUpdate()
        {
            if (_spawnedShell == null || !_lockShellToHead)
                return;

            if (_playerTransforms == null)
                TryFindHeadTransform();

            if (_playerTransforms == null)
                return;

            _spawnedShell.transform.position = _playerTransforms.headWorldPos;
            _spawnedShell.transform.rotation = _playerTransforms.headWorldRot;
        }
        */

        /*
        private IEnumerator FindHeadAndEnableLock()
        {
            _tryingToFindHead = true;

            float elapsed = 0f;
            while (elapsed < 15f)
            {
                TryFindHeadTransform();

                Plugin.Log.Info(
                    $"[EnvGreen] Poll t={elapsed:F2} " +
                    $"PlayerTransforms={(_playerTransforms != null)} " +
                    $"headTransform={(_headTransform != null ? _headTransform.name : "null")} " +
                    $"Camera.main={(Camera.main != null ? Camera.main.name + " scene=" + Camera.main.gameObject.scene.name : "null")}");

                if (_playerTransforms != null)
                {
                    Plugin.Log.Info("[EnvGreen] Head-follow source found; enabling shell follow");
                    _lockShellToHead = true;
                    _tryingToFindHead = false;
                    yield break;
                }

                yield return new WaitForSecondsRealtime(0.25f);
                elapsed += 0.25f;
            }

            Plugin.Log.Info("[EnvGreen] Failed to find head-follow source in time");
            _tryingToFindHead = false;
        }
        
        private void TryFindMenuCamera()
        {
            if (_menuCameraTransform != null)
                return;

            Camera[] cams = Camera.allCameras;
            for (int i = 0; i < cams.Length; i++)
            {
                Camera cam = cams[i];
                if (cam == null || !cam.isActiveAndEnabled)
                    continue;

                string camName = cam.name ?? "";
                string sceneName = cam.gameObject.scene.name ?? "";

                if (camName == "MenuMainCamera" || sceneName == "MainMenu")
                {
                    _menuCameraTransform = cam.transform;
                    Plugin.Log.Info($"[EnvGreen] Menu camera acquired: {cam.name} scene={sceneName}");
                    return;
                }
            }
        }
        
        private PlayerTransforms _playerTransforms;

        private void TryFindHeadTransform()
        {
            if (_playerTransforms != null)
                return;

            _playerTransforms = FindObjectOfType<PlayerTransforms>();
            Plugin.Log.Info($"[EnvGreen] FindObjectOfType<PlayerTransforms>() => {(_playerTransforms != null ? "FOUND" : "null")}");

            if (_playerTransforms != null)
            {
                Plugin.Log.Info("[EnvGreen] PlayerTransforms acquired");
                _lockShellToHead = true;
            }
        }
       */
        private static void LogPossibleNoteRenderers()
        {
            Plugin.Log.Info("[EnvGreen] ===== NOTE RENDERER SEARCH START =====");

            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded)
                    continue;

                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    var renderers = root.GetComponentsInChildren<Renderer>(true);
                    foreach (var r in renderers)
                    {
                        string n = r.gameObject.name.ToLowerInvariant();

                        if (!n.Contains("note") && !n.Contains("cube"))
                            continue;

                        string matName = r.sharedMaterial != null ? r.sharedMaterial.name : "null";
                        string shaderName = r.sharedMaterial?.shader != null ? r.sharedMaterial.shader.name : "null";

                        Plugin.Log.Info(
                            $"[EnvGreen] Note-like renderer scene={scene.name} go={r.gameObject.name} " +
                            $"type={r.GetType().Name} mat={matName} shader={shaderName}");
                    }
                }
            }

            Plugin.Log.Info("[EnvGreen] ===== NOTE RENDERER SEARCH END =====");
        }

        private static void LogHubScreenState()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded || scene.name != "MainMenu")
                    continue;

                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    if (root.name != "Wrapper")
                        continue;

                    Transform mainScreen = root.transform.Find("MenuCore/UI/ScreenSystem/ScreenContainer/MainScreen");
                    Transform mainMenuVC = root.transform.Find("MenuCore/UI/ScreenSystem/ScreenContainer/MainScreen/MainMenuViewController");
                    Transform leftScreen = root.transform.Find("MenuCore/UI/ScreenSystem/ScreenContainer/LeftScreen");
                    Transform menuButtonsVC = root.transform.Find("MenuCore/UI/ScreenSystem/ScreenContainer/LeftScreen/MenuButtonsViewController");

                    Plugin.Log.Info("[EnvGreen] ===== Hub Screen State START =====");

                    if (mainScreen != null)
                        Plugin.Log.Info($"[EnvGreen] MainScreen activeSelf={mainScreen.gameObject.activeSelf} activeInHierarchy={mainScreen.gameObject.activeInHierarchy}");
                    else
                        Plugin.Log.Info("[EnvGreen] MainScreen not found");

                    if (mainMenuVC != null)
                        Plugin.Log.Info($"[EnvGreen] MainMenuViewController activeSelf={mainMenuVC.gameObject.activeSelf} activeInHierarchy={mainMenuVC.gameObject.activeInHierarchy}");
                    else
                        Plugin.Log.Info("[EnvGreen] MainMenuViewController not found");

                    if (leftScreen != null)
                        Plugin.Log.Info($"[EnvGreen] LeftScreen activeSelf={leftScreen.gameObject.activeSelf} activeInHierarchy={leftScreen.gameObject.activeInHierarchy}");
                    else
                        Plugin.Log.Info("[EnvGreen] LeftScreen not found");

                    if (menuButtonsVC != null)
                        Plugin.Log.Info($"[EnvGreen] MenuButtonsViewController activeSelf={menuButtonsVC.gameObject.activeSelf} activeInHierarchy={menuButtonsVC.gameObject.activeInHierarchy}");
                    else
                        Plugin.Log.Info("[EnvGreen] MenuButtonsViewController not found");

                    Plugin.Log.Info("[EnvGreen] ===== Hub Screen State END =====");
                    return;
                }
            }

            Plugin.Log.Info("[EnvGreen] Wrapper not found for hub screen state");
        }

        private static void LogLoadedScenesAndRoots(string tag = "")
        {
            Plugin.Log.Info($"[EnvGreen] ===== Scene/Root Dump START {tag} =====");

            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded)
                    continue;

                Plugin.Log.Info($"[EnvGreen] Scene[{i}] name={scene.name} isLoaded={scene.isLoaded}");

                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    Plugin.Log.Info($"[EnvGreen]   Root: {root.name}");
                }
            }

            Plugin.Log.Info($"[EnvGreen] ===== Scene/Root Dump END {tag} =====");
        }
        private static void LogHierarchy(Transform root, int maxDepth = 4, string indent = "", int depth = 0)
        {
            if (root == null || depth > maxDepth)
                return;

            Plugin.Log.Info($"[EnvGreen] {indent}{root.name}");

            for (int i = 0; i < root.childCount; i++)
                LogHierarchy(root.GetChild(i), maxDepth, indent + "  ", depth + 1);
        }

        private static void LogMainMenuWrapper()
        {
            Scene mainMenu = default;
            bool foundScene = false;

            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene s = SceneManager.GetSceneAt(i);
                if (!s.isLoaded)
                    continue;

                if (s.name == "MainMenu")
                {
                    mainMenu = s;
                    foundScene = true;
                    break;
                }
            }

            if (!foundScene)
            {
                Plugin.Log.Info("[EnvGreen] MainMenu scene not found");
                return;
            }

            foreach (GameObject root in mainMenu.GetRootGameObjects())
            {
                if (root.name == "Wrapper")
                {
                    Plugin.Log.Info("[EnvGreen] ===== MainMenu Wrapper Hierarchy START =====");
                    LogHierarchy(root.transform, 6);
                    Plugin.Log.Info("[EnvGreen] ===== MainMenu Wrapper Hierarchy END =====");
                    return;
                }
            }

            Plugin.Log.Info("[EnvGreen] Wrapper root not found in MainMenu");
        }

        private static void LogRenderers(GameObject root)
        {
            Plugin.Log.Info("[EnvGreen] ===== RENDERER LOG START =====");

            var renderers = root.GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers)
            {
                string path = GetPath(r.transform, root.transform);

                string shaderName = "null";
                string materialName = "null";

                if (r.sharedMaterial != null)
                {
                    materialName = r.sharedMaterial.name;
                    if (r.sharedMaterial.shader != null)
                        shaderName = r.sharedMaterial.shader.name;
                }

                Plugin.Log.Info(
                    $"[EnvGreen] Renderer Path: {path} | " +
                    $"GO: {r.gameObject.name} | " +
                    $"Type: {r.GetType().Name} | " +
                    $"Mat: {materialName} | " +
                    $"Shader: {shaderName} | " +
                    $"Enabled: {r.enabled}");
            }

            Plugin.Log.Info("[EnvGreen] ===== RENDERER LOG END =====");
        }
    }

}