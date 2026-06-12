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
using static UnityEngine.EventSystems.EventTrigger;

namespace AutoBS
{
    /// <summary>
    /// Sets 360 Directional Markers to Menu Environment so player can easily face the menus and
    /// Add Green Screens to Menus and Gameplay for Mixed Reality Portals using Virtual Desktop
    /// </summary>
    public class EnvironmentMarkersAndGreenScreen : MonoBehaviour
    {
        private GameObject _cameraAlignmentRoot;

        // === Add 360 Directional Markers to Menu Environment
        private bool _menuMarkersAdded;
        private readonly List<GameObject> _markers = new List<GameObject>();

        // Green Screen
        //private Transform _headTransform;
        private GameObject _gameplayShell;
        //private bool _lockShellToHead;
        //private bool _tryingToFindHead;
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
        const float gameplayFloorHeight = 0.005f; // this higher than the player contruction area. but spooky environment has bricks that are maybe 2 or 3 inches higher than the floor.

        private bool ShouldShowMenuGreenScreen()
        {
            return Config.Instance.EnablePlugin &&
                   Config.Instance.EnableMixedRealityMenus &&
                   _isMainMenuLoaded &&
                   !_isGameplayLoaded;
        }

        private bool ShouldShowGameplayGreenScreen()
        {
            return Config.Instance.EnablePlugin &&
                   Utils.IsEnabledGameplayMixedReality() &&
                   _isGameplayLoaded;
        }

        private bool ShouldShowDirectionalMarkers()
        {
            return Config.Instance.EnablePlugin &&
                   (AutoBS.Patches.TransitionPatcher.SelectedSerializedName == GameModeHelper.GENERATED_360DEGREE_MODE ||
                    AutoBS.Patches.TransitionPatcher.SelectedSerializedName == "360Degree" ||
                    AutoBS.Patches.TransitionPatcher.SelectedSerializedName == "90Degree");
        }

        //private Transform _menuCameraTransform;
        public static EnvironmentMarkersAndGreenScreen Instance { get; private set; }

        private float GetCameraAlignmentGridUnitSizeMeters()
        {
            return Mathf.Max(0.01f, Config.Instance.CameraAlignmentGridUnitSizeFeet * 0.3048f);
        }

        private Vector3 GetCameraAlignmentGrid1OriginMeters()
        {
            return Config.Instance.CameraAlignmentGrid1OriginFeet.ToMetersFromFeet();
        }

        private Vector3 GetCameraAlignmentGrid2OriginMeters()
        {
            return Config.Instance.CameraAlignmentGrid2OriginFeet.ToMetersFromFeet();
        }

        private void ClearCameraAlignmentTool()
        {
            if (_cameraAlignmentRoot != null)
            {
                Destroy(_cameraAlignmentRoot);
                _cameraAlignmentRoot = null;
                Plugin.LogDebug("[CameraAlignmentTool] Cleared calibration tool");
            }
        }


        private void Awake()
        {
            Instance = this;
        }

        private void Start()
        {
            Plugin.LogDebug($"[MixedReality] Start go={gameObject.name} scene={gameObject.scene.name} activeInHierarchy={gameObject.activeInHierarchy}");

            _isMainMenuLoaded = false;
            _isGameplayLoaded = false;

            if (!Utils.IsEnabledGameplayMixedReality() && !Config.Instance.EnableMixedRealityMenus)
            {
                Plugin.LogDebug("[MixedReality] Mixed reality disabled at Start, clearing any existing shells");
                ClearAllGreenScreens();
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

            if (_isMainMenuLoaded && Config.Instance.EnableMixedRealityMenus)
            {
                Plugin.LogDebug("[MixedReality] MainMenu already loaded, ensuring menu greenscreen immediately");
                EnsureMenuGreenScreen();
            }
            else if (_isGameplayLoaded && Utils.IsEnabledGameplayMixedReality())
            {
                Plugin.LogDebug("[MixedReality] GameCore already loaded, starting scan immediately");
                _gameRoutine = StartCoroutine(InitialWaitAndCheck());
            }

            RefreshCameraAlignmentForCurrentScene();
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Plugin.LogDebug($"[MixedReality] Scene loaded: {scene.name}");

            if (scene.name == "MainMenu")
                _isMainMenuLoaded = true;

            if (scene.name.Contains("GameCore") || scene.name.Contains("StandardGameplay"))
                _isGameplayLoaded = true;

            if (scene.name.Contains("GlassDesertEnvironment"))
            {
                if (ShouldShowDirectionalMarkers())
                    AddDirectionalMarkersIfNeeded();
                else
                    RemoveDirectionalMarkers();
            }
            else if (scene.name.Contains("Environment"))
            {
                RemoveDirectionalMarkers();
            }

            if (!Utils.IsMixedRealitySystemEnabled())
            {
                ClearAllGreenScreens();
                RefreshCameraAlignmentForCurrentScene();
                return;
            }

            if (scene.name.Contains("GameCore"))
            {
                ResetGameplayState();
                ResetMenuState();

                if (ShouldShowGameplayGreenScreen())
                {
                    Plugin.LogDebug("[MixedReality] GameCore detected, starting scan");
                    _gameRoutine = StartCoroutine(InitialWaitAndCheck());
                }

                RefreshCameraAlignmentForCurrentScene();
                return;
            }

            if (scene.name == "MainMenu")
            {
                ResetGameplayState();

                if (ShouldShowMenuGreenScreen())
                {
                    Plugin.LogDebug("[MixedReality] MainMenu detected, ensuring menu greenscreen");
                    EnsureMenuGreenScreen();
                }
                else
                {
                    ResetMenuState();
                }

                RefreshCameraAlignmentForCurrentScene();
                return;
            }

            RefreshCameraAlignmentForCurrentScene();
        }

        private void OnSceneUnloaded(Scene scene)
        {
            Plugin.LogDebug($"[MixedReality] Scene unloaded: {scene.name}");

            if (scene.name == "MainMenu")
            {
                _isMainMenuLoaded = false;
                ResetMenuState();
            }
            else if (scene.name.Contains("GameCore") || scene.name.Contains("StandardGameplay"))
            {
                _isGameplayLoaded = false;

                ResetGameplayState();

                if (_isMainMenuLoaded && Config.Instance.EnableMixedRealityMenus)
                    EnsureMenuGreenScreen();
            }
            else if (scene.name.Contains("GlassDesertEnvironment"))
            {
                ResetGameplayState();
            }

            RefreshCameraAlignmentForCurrentScene();
        }

        public void RefreshVisualState()
        {
            RefreshMixedRealityState();
            RefreshCameraAlignmentForCurrentScene();
        }

        private void OnEnable()
        {
            Plugin.LogDebug($"[MixedReality] OnEnable go={gameObject.name} scene={gameObject.scene.name} activeInHierarchy={gameObject.activeInHierarchy}");

            if (!_subscribed)
            {
                SceneManager.sceneLoaded += OnSceneLoaded;
                SceneManager.sceneUnloaded += OnSceneUnloaded;
                _subscribed = true;
            }

            if (!Utils.IsEnabledGameplayMixedReality() && !Config.Instance.EnableMixedRealityMenus)
            {
                Plugin.LogDebug("[MixedReality] OnEnable while disabled, clearing shells");
                ClearAllGreenScreens();
            }
        }
        private void ResetGameplayState()
        {
            if (_gameRoutine != null)
            {
                StopCoroutine(_gameRoutine);
                _gameRoutine = null;
            }

            _loggedMainMenuWrapper = false;
            //_headTransform = null;
            //_tryingToFindHead = false;
            //_lockShellToHead = false;

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
        /*
        private void ResetGameplayState()
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
        */
        private bool _subscribed;

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;

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

                if (_cameraAlignmentTemplateMat != null)
                {
                    Destroy(_cameraAlignmentTemplateMat);
                    _cameraAlignmentTemplateMat = null;
                }
            }

            RemoveDirectionalMarkers();
            ClearCameraAlignmentTool();
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
            ResetGameplayState();
        }

        public void RefreshMixedRealityState()
        {
            if (!Config.Instance.EnablePlugin)
            {
                ClearAllGreenScreens();
                RemoveDirectionalMarkers();
                RefreshCameraAlignmentForCurrentScene();
                return;
            }

            if (ShouldShowMenuGreenScreen())
            {
                ResetMenuState();
                EnsureMenuGreenScreen();
            }
            else
            {
                ResetMenuState();
            }

            if (ShouldShowGameplayGreenScreen())
            {
                ResetGameplayState();
                _gameRoutine = StartCoroutine(InitialWaitAndCheck());
            }
            else
            {
                ResetGameplayState();
            }

            RefreshCameraAlignmentForCurrentScene();
        }

        #region 360 Markers
        private void AddDirectionalMarkersIfNeeded()
        {
            if (_menuMarkersAdded)
                return;

            GameObject wrapper = FindMainMenuWrapper();
            if (wrapper == null)
            {
                Plugin.LogDebug("[AddDirectionalMarkers] Wrapper not found. Cannot add directional markers.");
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
            Plugin.LogDebug("[AddDirectionalMarkers] Direction markers added.");
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
            if (!Config.Instance.EnablePlugin || !Config.Instance.EnableMixedRealityMenus)
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

            Plugin.LogDebug("[MixedReality] Ensuring menu greenscreen");
            _menuRoutine = StartCoroutine(SetupMenuGreenScreen());
        }

        private IEnumerator InitialWaitAndCheck()
        {
            if (!ShouldShowGameplayGreenScreen())
            {
                ResetGameplayState();
                _gameRoutine = null;
                yield break;
            }

            float totalWaitTime = 0f;
            const float waitInterval = 0.5f;

            while (totalWaitTime < 10f)
            {
                if (!ShouldShowGameplayGreenScreen())
                {
                    ResetGameplayState();
                    _gameRoutine = null;
                    yield break;
                }

                GameObject environment = FindEnvironment();

                if (environment != null)
                {
                    if (!ShouldShowGameplayGreenScreen())
                    {
                        ResetGameplayState();
                        _gameRoutine = null;
                        yield break;
                    }

                    SetupGamePlayGreenScreen(environment);
                    _gameRoutine = null;
                    yield break;
                }

                totalWaitTime += waitInterval;
                yield return new WaitForSecondsRealtime(waitInterval);
            }

            _gameRoutine = null;
        }
        private IEnumerator SetupMenuGreenScreen()
        {
            if (!ShouldShowMenuGreenScreen())
            {
                ResetMenuState();
                _menuRoutine = null;
                yield break;
            }

            float elapsed = 0f;

            while (elapsed < 10f)
            {
                if (!ShouldShowMenuGreenScreen())
                {
                    ResetMenuState();
                    _menuRoutine = null;
                    yield break;
                }

                GameObject environment = FindMenuEnvironment();

                if (environment != null)
                {
                    if (!ShouldShowMenuGreenScreen())
                    {
                        ResetMenuState();
                        _menuRoutine = null;
                        yield break;
                    }

                    //Plugin.LogDebug($"[MixedReality] Menu environment found: {environment.name} scene={environment.scene.name} path={GetPath(environment.transform, environment.transform.root)}");

                    if (_menuShell != null)
                    {
                        Destroy(_menuShell);
                        _menuShell = null;
                    }

                    Material greenMat = GetGreenScreenMaterial();
                    if (greenMat == null)
                    {
                        Plugin.LogDebug("[MixedReality] Could not build green material");
                        _menuRoutine = null;
                        yield break;
                    }

                    _menuShell = new GameObject("EnvGreen_MenuShell");
                    _menuShell.transform.SetParent(environment.transform, false);
                    _menuShell.transform.localPosition = Vector3.zero;
                    _menuShell.transform.localRotation = Quaternion.identity;

                    CreateQuadBox(_menuShell.transform, greenMat); // A box to hide the group of notes on the floor in the menu environment behind the player

                    if (Config.Instance.MixedRealityMenuStyle == Config.MixedRealityMenuModeType.Style1)
                    {
                        CreateCylinderAperture(_menuShell.transform, greenMat, ceilingRoundStyle1: true);
                    }
                    else if (Config.Instance.MixedRealityMenuStyle == Config.MixedRealityMenuModeType.Style2)
                    {
                        CreateCylinderAperture(_menuShell.transform, greenMat, ceilingRoundStyle1: false);
                    }
                    else
                    {
                        CreateRoundAperature(_menuShell.transform, greenMat, menuOverrideHeight: true, zOffset: Config.Instance.MixedRealityMenuZOffset);
                    }

                    //Plugin.LogDebug($"[MixedReality] Menu shell worldPos={_menuShell.transform.position} localPos={_menuShell.transform.localPosition} parent={_menuShell.transform.parent?.name}");

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

            Plugin.LogDebug("[MixedReality] SetupMenuGreenScreen timed out");
            _menuRoutine = null;
        }

        
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

                //Plugin.LogDebug($"[MixedReality] Checking scene: {scene.name}");

                foreach (GameObject obj in scene.GetRootGameObjects())
                {
                    //Plugin.LogDebug($"[MixedReality] Root: {obj.name}");

                    if (obj.name == "Environment")
                    {
                        //Plugin.LogDebug($"[MixedReality] Exact Environment root found in scene {scene.name}");
                        return obj;
                    }
                }
            }

            return null;
        }
        
        private void SetupGamePlayGreenScreen(GameObject root)
        {
            if (!ShouldShowGameplayGreenScreen())
            {
                ResetGameplayState();
                return;
            }

            if (root == null)
                return;

            if (_gameplayShell != null)
            {
                Destroy(_gameplayShell);
                _gameplayShell = null;
            }

            //Plugin.LogDebug("[MixedReality] Applying selective green test");

            OffsetPlayerFeetSymbol(root.transform, 1.2f);

            var renderers = root.GetComponentsInChildren<Renderer>(true);

            Material greenMat = GetGreenScreenMaterial();
            if (greenMat == null)
            {
                Plugin.LogDebug("[MixedReality] Could not build green material");
                return;
            }
            if (root.scene.name == "GlassDesertEnvironment")
            {
                if (Config.Instance.MixedReality360Diameter <= 0) return;

                //Plugin.LogDebug("[MixedReality] GlassDesertEnvironment detected.");

                _gameplayShell = CreateRadialFloorFan(
                    root.transform,
                    greenMat,
                    diameter: Config.Instance.MixedReality360Diameter,
                    ceilingHeight: Config.Instance.MixedReality360CeilingHeight,
                    blades: 36,
                    floorHeight: gameplayFloorHeight);

                //Plugin.LogDebug("[MixedReality] Spawned 360 floor green screen.");
            }
            else
            {
                if (Config.Instance.MixedRealityPortalShapeRound)
                {
                    if (Config.Instance.MixedRealityRoundDiameter <= 0) return;

                    _gameplayShell = CreateRoundAperature(root.transform, greenMat, diameter: Config.Instance.MixedRealityRoundDiameter, zOffset: Config.Instance.MixedRealityGamePlayRoundZOffset);
                }
                else
                {
                    if (Config.Instance.MixedRealityRectHeight <= 0 || Config.Instance.MixedRealityRectWidth <= 0) return;
                    _gameplayShell = CreateRectangularAperture(root.transform, greenMat, width: Config.Instance.MixedRealityRectWidth, height: Config.Instance.MixedRealityRectHeight, zOffset: Config.Instance.MixedRealityGamePlayRectZOffset);
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

                if (ceilingHeight > 0)
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
            /*
            if (Config.Instance.MixedRealityFullModeTest)
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
            */
            if (diameter > outerDiameter)
                outerDiameter = diameter;

            float apertureRadius = diameter * 0.5f;
            float outerWidth = Mathf.Max(outerDiameter, diameter);
            float outerHalfW = outerWidth * 0.5f;

            float halfDepth = depth * 0.5f;

            float frontZ;
            float backZ;
            float rootZ;

            if (zOffset >= 0f)
            {
                // Front stays exactly at zOffset in world space.
                // Box grows forward from its old back position.
                frontZ = halfDepth + zOffset;
                backZ = -halfDepth;
                rootZ = -halfDepth;
            }
            else
            {
                // Whole box shifts backward, unchanged size.
                frontZ = +halfDepth;
                backZ = -halfDepth;
                rootZ = zOffset - halfDepth;
            }

            float apertureBottomHeight = Config.Instance.MixedRealityRoundYOffset;

            if (menuOverrideHeight)
            {
                apertureBottomHeight = -2f; // changed to set to bottom of aperture
            }

            // Real floor level of the box
            float floorWorld = menuOverrideHeight
                ? menuFloorHeight
                : gameplayFloorHeight;

            // Aperture center can now be anywhere, including below floor
            float apertureCenterYWorld = apertureBottomHeight + apertureRadius;

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

            root.transform.localPosition = new Vector3(0f, centerHeight, rootZ);
            root.transform.localRotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;

            Plugin.LogDebug($"[MixedReality] Spawned round aperture with rectangular outer box ({segments} iris segments)");
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

            float halfDepth = depth * 0.5f;

            float frontZ;
            float backZ;
            float rootZ;

            if (zOffset >= 0f)
            {
                // Front stays exactly at zOffset in world space.
                // Box grows forward from its old back position.
                frontZ = halfDepth + zOffset;
                backZ = -halfDepth;
                rootZ = -halfDepth;
            }
            else
            {
                // Whole box shifts backward, unchanged size.
                frontZ = +halfDepth;
                backZ = -halfDepth;
                rootZ = zOffset - halfDepth;
            }

            float innerHalfW = width * 0.5f;
            float innerHalfH = height * 0.5f;

            // Outer box must at least contain the aperture width
            outerWidth = Mathf.Max(outerWidth, width);

            // Aperture world-space placement
            float apertureCenterYWorld;
            float apertureBottomWorld;

            float floorOffset;

            if (menuOverrideHeight)
            {
                floorOffset = menuFloorHeight;
                apertureBottomWorld = floorOffset;
                apertureCenterYWorld = apertureBottomWorld + innerHalfH;

            }
            else
            {
                floorOffset = gameplayFloorHeight;
                //apertureCenterYWorld = Config.Instance.MixedRealityRectCenterHeight; // sets center of portal
                //apertureBottomWorld = apertureCenterYWorld - innerHalfH;
                apertureBottomWorld = Config.Instance.MixedRealityRectYOffset; // changed this to set bottom of portal instead of center
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
            root.transform.localPosition = new Vector3(0f, centerHeight, rootZ);
            root.transform.localRotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;

            Plugin.LogDebug("[MixedReality] Spawned inward rectangular aperture tunnel");
            return root;
        }

        private static GameObject CreateCylinderAperture(
            Transform parent,
            Material mat,
            float diameter = 7.5f,
            float height = 2.9f,//3
            float zOffset = 0,                 // front of cylinder opening sits at zOffset
            int segments = 42,
            float frontOpeningAngleDeg = 180f,   // centered on forward; 90 = open from -45 to +45
            bool createFloor = true,
            bool createCeiling = true,
            bool ceilingRound = true, // rect works now with straight line across top
            bool ceilingRoundStyle1 = true
        )
        {
            GameObject root = new GameObject("EnvGreen_VerticalCylinderAperture");

            if (parent != null)
                root.transform.SetParent(parent, false);

            float radius = diameter * 0.5f;
            float halfHeight = height * 0.5f;
            float angleStep = 360f / segments;

            // Put the cylinder so its front-most point is at zOffset, same idea as your other methods.
            // Since the front-most point of the circle is +radius in local Z, shift root back by radius.
            root.transform.localPosition = new Vector3(0f, halfHeight + menuFloorHeight, zOffset);
            root.transform.localRotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;

            float halfOpen = Mathf.Clamp(frontOpeningAngleDeg, 0f, 360f) * 0.5f;

            bool IsAngleInFrontOpening(float angleDeg)
            {
                // Normalize to [-180, 180]
                float a = Mathf.Repeat(angleDeg + 180f, 360f) - 180f;
                return a >= -halfOpen && a <= halfOpen;
            }

            // ----- Vertical cylinder wall blades -----
            for (int i = 0; i < segments; i++)
            {
                float angleDeg0 = i * angleStep;
                float angleDeg1 = (i + 1) * angleStep;
                float angleMidDeg = (angleDeg0 + angleDeg1) * 0.5f;

                // Convert so 0 deg = front (+Z), matching your requested opening
                float frontCenteredDeg = Mathf.Repeat(90f - angleMidDeg + 180f, 360f) - 180f;

                if (frontOpeningAngleDeg > 0.0001f && IsAngleInFrontOpening(frontCenteredDeg))
                    continue;

                float angleRad0 = angleDeg0 * Mathf.Deg2Rad;
                float angleRad1 = angleDeg1 * Mathf.Deg2Rad;
                float angleRadMid = angleMidDeg * Mathf.Deg2Rad;

                Vector3 ring0 = new Vector3(Mathf.Cos(angleRad0) * radius, 0f, Mathf.Sin(angleRad0) * radius);
                Vector3 ring1 = new Vector3(Mathf.Cos(angleRad1) * radius, 0f, Mathf.Sin(angleRad1) * radius);
                Vector3 radialMid = new Vector3(Mathf.Cos(angleRadMid), 0f, Mathf.Sin(angleRadMid)).normalized;

                Vector3 bottomA = ring0 + new Vector3(0f, -halfHeight, 0f);
                Vector3 bottomB = ring1 + new Vector3(0f, -halfHeight, 0f);
                Vector3 topA = ring0 + new Vector3(0f, +halfHeight, 0f);
                Vector3 topB = ring1 + new Vector3(0f, +halfHeight, 0f);

                Vector3 topMid = (topA + topB) * 0.5f;
                Vector3 bottomMid = (bottomA + bottomB) * 0.5f;
                Vector3 bladeCenter = (topMid + bottomMid) * 0.5f;

                float bladeHeight = Vector3.Distance(topMid, bottomMid);
                float bladeWidth = Vector3.Distance(bottomA, bottomB) * 1.05f;

                Vector3 up = (topMid - bottomMid).normalized;

                // inward-facing
                Vector3 forward = radialMid;
                Vector3 right = Vector3.Cross(up, forward).normalized;
                forward = Vector3.Cross(right, up).normalized;

                CreateQuad(
                    root.transform,
                    $"CylinderBlade_{i}",
                    bladeCenter,
                    Quaternion.LookRotation(forward, up),
                    new Vector3(bladeWidth, bladeHeight, 1f),
                    mat);
            }

            // ----- Floor -----
            if (createFloor)
            {
                for (int i = 0; i < segments; i++)
                {
                    float angleDeg0 = i * angleStep;
                    float angleDeg1 = (i + 1) * angleStep;

                    float angleRad0 = angleDeg0 * Mathf.Deg2Rad;
                    float angleRad1 = angleDeg1 * Mathf.Deg2Rad;

                    Vector3 outer0 = new Vector3(Mathf.Cos(angleRad0) * radius, -halfHeight, Mathf.Sin(angleRad0) * radius);
                    Vector3 outer1 = new Vector3(Mathf.Cos(angleRad1) * radius, -halfHeight, Mathf.Sin(angleRad1) * radius);

                    Vector3 outerMid = (outer0 + outer1) * 0.5f;
                    float bladeLength = outerMid.magnitude;
                    float bladeWidth = Vector3.Distance(outer0, outer1) * 1.05f;

                    CreateQuad(
                        root.transform,
                        $"FloorBlade_{i}",
                        new Vector3(outerMid.x * 0.5f, -halfHeight, outerMid.z * 0.5f),
                        Quaternion.LookRotation(Vector3.down, new Vector3(outerMid.x, 0f, outerMid.z).normalized),
                        new Vector3(bladeWidth, bladeLength, 1f),
                        mat);
                }
            }
            // ----- Ceiling -----
            if (createCeiling)
            {
                if (!ceilingRound)
                {
                    float clampedOpen = Mathf.Clamp(frontOpeningAngleDeg, 0f, 360f);

                    float ceilingWidth;
                    float ceilingDepth;
                    float ceilingCenterZ;


                    if (clampedOpen > 0.0001f && clampedOpen < 360f)
                    {
                        float halfOpenRad = halfOpen * Mathf.Deg2Rad;

                        // Front opening chord across the cylinder
                        float openingFrontZ = radius * Mathf.Cos(halfOpenRad);
                        float openingChordWidth = 2f * radius * Mathf.Sin(halfOpenRad);

                        // Back of cylinder is at -radius
                        ceilingDepth = openingFrontZ + radius;

                        // Center the quad between front chord and back edge
                        ceilingCenterZ = openingFrontZ - ceilingDepth * 0.5f;

                        float tinyOffset = -.265f;// with diameter 7.5f // -.04f with prev diameter; // to get ceiling quad to line up to front of opening
                        float frontEdgePadding = Vector3.Distance(
                            new Vector3(Mathf.Cos(0f) * radius, 0f, Mathf.Sin(0f) * radius),
                            new Vector3(Mathf.Cos(angleStep * Mathf.Deg2Rad) * radius, 0f, Mathf.Sin(angleStep * Mathf.Deg2Rad) * radius)
                        ) * 0.5f + tinyOffset;

                        // move the ceiling front edge slightly forward to reach the blade inner edge
                        float targetFrontZ = openingFrontZ + frontEdgePadding;

                        ceilingDepth = targetFrontZ + radius;
                        ceilingCenterZ = targetFrontZ - ceilingDepth * 0.5f;

                        // Make width match or slightly exceed the chord endpoints
                        ceilingWidth = openingChordWidth * 1.02f;
                    }
                    else
                    {
                        // No opening: use full square cap
                        ceilingWidth = diameter;
                        ceilingDepth = diameter;
                        ceilingCenterZ = 0f;
                    }

                    CreateQuad(
                        root.transform,
                        "CeilingCap",
                        new Vector3(0f, +halfHeight, ceilingCenterZ),
                        Quaternion.Euler(-90f, 0f, 0f),
                        new Vector3(ceilingWidth, ceilingDepth, 1f),
                        mat);
                }
                else // radial blades circular
                {
                    // BS logo hidden
                    float startDeg = 0; // full ceiling so hiding BS logo
                    float endDeg = 360;
                    int bladeCount = 36;

                    float ceilingZOffset = 0f;
                    float ceilingRadiusMultiplier = 1.02f;
                    
                    if (ceilingRoundStyle1)
                    {
                        startDeg = 325f; // partial opening
                        endDeg = 35f;
                        bladeCount = 20; // fewer blades since partial ceiling

                        // added a huge offset partial radial ceiling to give a nice curve above the player.
                        ceilingZOffset = -7.2f; // move the ceiling backwards
                        ceilingRadiusMultiplier = 2.2f; // make a huge radius ceiling
                    }

                    // has 'V' shape in front  of BS logo don't like as much
                    /*
                    startDeg = 50; // full ceiling but open in front for bs logo
                    endDeg = 310;
                    bladeCount = 36;

                    ceilingZOffset = 0f;
                    ceilingRadiusMultiplier = 1.02f;
                    */

                    float ceilingRadius = radius * ceilingRadiusMultiplier;

                    float spanDeg = endDeg - startDeg;
                    if (spanDeg <= 0f)
                        spanDeg += 360f;

                    float userAngleStep = spanDeg / bladeCount;

                    for (int i = 0; i < bladeCount; i++)
                    {
                        float userAngle0 = startDeg + i * userAngleStep;
                        float userAngle1 = startDeg + (i + 1) * userAngleStep;

                        float mathAngleDeg0 = 90f - userAngle0;
                        float mathAngleDeg1 = 90f - userAngle1;

                        float angleRad0 = mathAngleDeg0 * Mathf.Deg2Rad;
                        float angleRad1 = mathAngleDeg1 * Mathf.Deg2Rad;

                        Vector3 outer0 = new Vector3(Mathf.Cos(angleRad0) * ceilingRadius, +halfHeight, Mathf.Sin(angleRad0) * ceilingRadius);
                        Vector3 outer1 = new Vector3(Mathf.Cos(angleRad1) * ceilingRadius, +halfHeight, Mathf.Sin(angleRad1) * ceilingRadius);

                        Vector3 outerMid = (outer0 + outer1) * 0.5f;
                        float bladeLength = new Vector2(outerMid.x, outerMid.z).magnitude;
                        float bladeWidth = Vector3.Distance(outer0, outer1) * 1.05f;

                        CreateQuad(
                            root.transform,
                            $"CeilingBlade_{i}",
                            new Vector3(outerMid.x * 0.5f, +halfHeight, outerMid.z * 0.5f + ceilingZOffset),
                            Quaternion.LookRotation(Vector3.up, new Vector3(outerMid.x, 0f, outerMid.z).normalized),
                            new Vector3(bladeWidth, bladeLength, 1f),
                            mat);
                    }
                }
            }
            Plugin.LogDebug($"[MixedReality] Spawned vertical cylinder aperture with opening {frontOpeningAngleDeg:F1} deg");
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
            GameObject boxRoot = new GameObject("MixedReality_QuadBox");

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

            //Plugin.LogDebug("[MixedReality] Menu box to hide back pile of notes.");
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

        private static Material GetGreenScreenMaterial()
        {
            UnityEngine.Color chroma = Config.Instance.MixedRealityGreenScreenColor.ToUnityColor(Config.Instance.MixedRealityColorMultiplier);

            if (_greenTemplateMat == null)
            {
                Renderer noteArrowRenderer = FindRendererByNameAcrossScenes("NoteArrow");
                if (noteArrowRenderer == null || noteArrowRenderer.sharedMaterial == null)
                    return null;

                Material src = noteArrowRenderer.sharedMaterial;
                Material m = new Material(src);

                if (m.HasProperty("_Color"))
                    m.SetColor("_Color", chroma);

                /*
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
                */

                _greenTemplateMat = m;
                _greenTemplateShaderName = src.shader != null ? src.shader.name : "null";
            }
            else
            {
                if (_greenTemplateMat.HasProperty("_Color"))
                    _greenTemplateMat.SetColor("_Color", chroma);
            }

            return _greenTemplateMat;
        }
        private static void OffsetPlayerFeetSymbol(Transform environmentRoot, float inches = 1f)
        {
            if (environmentRoot == null)
                return;
            inches = -5; // test
            float yOffset = inches * 0.0254f;

            Transform feet = environmentRoot.Find("PlayersPlace/Feet");
            if (feet != null)
            {
                Vector3 p = feet.localPosition;
                feet.localPosition = new Vector3(p.x, p.y + yOffset, p.z);
                //Plugin.LogDebug($"[MixedReality] Offset Feet by {yOffset:F4}m");
            }
            
            Transform construction = environmentRoot.Find("PlayersPlace/RectangleFakeGlow");
            if (construction != null)
            {
                Vector3 p = construction.localPosition;
                construction.localPosition = new Vector3(p.x, p.y + yOffset, p.z);
                //Plugin.LogDebug($"[MixedReality] Offset Construction by {yOffset:F4}m");
            }
            
        }

        #region Camera Alignment Rig

        private const float CameraAlignmentLineThickness = 0.0075f;
        private static float CameraAlignmentMarkerDiameter = Config.Instance.CameraAlignmentMarkerDiameterFeet;
        private const float CameraAlignmentAlpha = 0.5f; // not working
        private float CameraAlignmentLineEndInset = CameraAlignmentMarkerDiameter * 2f; // 0.015f

        private void RefreshCameraAlignmentForCurrentScene()
        {
            ClearCameraAlignmentTool();

            if (!Config.Instance.EnablePlugin)
            {
                //Plugin.LogDebug("[CameraAlignmentTool] Skipped: plugin disabled");
                return;
            }

            if (!Config.Instance.EnableCameraAlignmentGrid1 && !Config.Instance.EnableCameraAlignmentGrid2 && !Config.Instance.EnableCameraAlignmentVerticalMarkerSet1 && !Config.Instance.EnableCameraAlignmentVerticalMarkerSet2 && !Config.Instance.EnableCameraAlignmentVerticalMarkerSet3)
            {
                //Plugin.LogDebug("[CameraAlignmentTool] Skipped: all grids disabled");
                return;
            }

            GameObject parentObject = null;

            if (_isGameplayLoaded)
                parentObject = FindEnvironment();

            if (parentObject == null && _isMainMenuLoaded)
                parentObject = FindMenuEnvironment();

            if (parentObject == null)
            {
                Plugin.LogDebug("[CameraAlignmentTool] No valid menu/gameplay environment found");
                return;
            }

            CreateCameraAlignmentMarkers(parentObject.transform);
        }
        private void CreateCameraAlignmentMarkers(Transform parent)
        {
            _cameraAlignmentRoot = new GameObject("AutoBS_CameraAlignmentGrids");
            _cameraAlignmentRoot.transform.SetParent(parent, false);
            _cameraAlignmentRoot.transform.localPosition = Vector3.zero;
            _cameraAlignmentRoot.transform.localRotation = Quaternion.identity;
            _cameraAlignmentRoot.transform.localScale = Vector3.one;

            Plugin.LogDebug($"[CameraAlignmentGrid] Creating under parent={parent.name} scene={parent.gameObject.scene.name}");

            if (Config.Instance.EnableCameraAlignmentGrid1)
            {
                CreateAlignmentGrid(
                    _cameraAlignmentRoot.transform,
                    "Grid1",
                    GetCameraAlignmentGrid1OriginMeters(),
                    UnityEngine.Color.white);
            }

            if (Config.Instance.EnableCameraAlignmentGrid2)
            {
                CreateAlignmentGrid(
                    _cameraAlignmentRoot.transform,
                    "Grid2",
                    GetCameraAlignmentGrid2OriginMeters(),
                    UnityEngine.Color.yellow);
            }

            if (Config.Instance.EnableCameraAlignmentVerticalMarkerSet1)
            {
                CreateVerticalMarkerSet(
                    _cameraAlignmentRoot.transform,
                    "VerticalMarkers1",
                    Config.Instance.CameraAlignmentVerticalMarkerSet1OriginFeet.ToMetersFromFeet(),
                    UnityEngine.Color.white);
            }

            if (Config.Instance.EnableCameraAlignmentVerticalMarkerSet2)
            {
                CreateVerticalMarkerSet(
                    _cameraAlignmentRoot.transform,
                    "VerticalMarkers2",
                    Config.Instance.CameraAlignmentVerticalMarkerSet2OriginFeet.ToMetersFromFeet(),
                    UnityEngine.Color.yellow);
            }

            if (Config.Instance.EnableCameraAlignmentVerticalMarkerSet3)
            {
                CreateVerticalMarkerSet(
                    _cameraAlignmentRoot.transform,
                    "VerticalMarkers3",
                    Config.Instance.CameraAlignmentVerticalMarkerSet3OriginFeet.ToMetersFromFeet(),
                    UnityEngine.Color.magenta);
            }

            if (!HasGridAtOrigin())
            {
                CreateOriginFloorReference(_cameraAlignmentRoot.transform);
            }
        }
        /// <summary>
        /// Creates a 3x3 grid of lines and markers with the specified local origin.
        /// </summary>
        /// <param name="parent"></param>
        /// <param name="gridName"></param>
        /// <param name="localOrigin"></param>
        /// <param name="color"></param>
        private void CreateAlignmentGrid(Transform parent, string gridName, Vector3 localOrigin, UnityEngine.Color color)
        {
            GameObject gridRoot = new GameObject(gridName);
            gridRoot.transform.SetParent(parent, false);
            gridRoot.transform.localPosition = localOrigin;
            gridRoot.transform.localRotation = Quaternion.identity;
            gridRoot.transform.localScale = Vector3.one;

            float step = GetCameraAlignmentGridUnitSizeMeters();

            float xLeft = -step;
            float xMid = 0f;
            float xRight = step;

            float y0 = 0f;
            float y1 = step;
            float y2 = step * 2f;

            Plugin.LogDebug($"[CameraAlignmentGrids] Create {gridName} origin={localOrigin} step={step:F3}m lines={Config.Instance.CameraAlignmentGridDisplayLines}");

            if (Config.Instance.CameraAlignmentGridDisplayLines)
            {
                // Vertical segments at xLeft
                CreateLine(gridRoot.transform,
                    new Vector3(xLeft, y0 + CameraAlignmentLineEndInset, 0f),
                    new Vector3(xLeft, y1 - CameraAlignmentLineEndInset, 0f),
                    color, "V_Left_Bottom");

                CreateLine(gridRoot.transform,
                    new Vector3(xLeft, y1 + CameraAlignmentLineEndInset, 0f),
                    new Vector3(xLeft, y2 - CameraAlignmentLineEndInset, 0f),
                    color, "V_Left_Top");

                // Vertical segments at xMid
                CreateLine(gridRoot.transform,
                    new Vector3(xMid, y0 + CameraAlignmentLineEndInset, 0f),
                    new Vector3(xMid, y1 - CameraAlignmentLineEndInset, 0f),
                    color, "V_Mid_Bottom");

                CreateLine(gridRoot.transform,
                    new Vector3(xMid, y1 + CameraAlignmentLineEndInset, 0f),
                    new Vector3(xMid, y2 - CameraAlignmentLineEndInset, 0f),
                    color, "V_Mid_Top");

                // Vertical segments at xRight
                CreateLine(gridRoot.transform,
                    new Vector3(xRight, y0 + CameraAlignmentLineEndInset, 0f),
                    new Vector3(xRight, y1 - CameraAlignmentLineEndInset, 0f),
                    color, "V_Right_Bottom");

                CreateLine(gridRoot.transform,
                    new Vector3(xRight, y1 + CameraAlignmentLineEndInset, 0f),
                    new Vector3(xRight, y2 - CameraAlignmentLineEndInset, 0f),
                    color, "V_Right_Top");

                // Horizontal segments at y0
                CreateLine(gridRoot.transform,
                    new Vector3(xLeft + CameraAlignmentLineEndInset, y0, 0f),
                    new Vector3(xMid - CameraAlignmentLineEndInset, y0, 0f),
                    color, "H_Bottom_Left");

                CreateLine(gridRoot.transform,
                    new Vector3(xMid + CameraAlignmentLineEndInset, y0, 0f),
                    new Vector3(xRight - CameraAlignmentLineEndInset, y0, 0f),
                    color, "H_Bottom_Right");

                // Horizontal segments at y1
                CreateLine(gridRoot.transform,
                    new Vector3(xLeft + CameraAlignmentLineEndInset, y1, 0f),
                    new Vector3(xMid - CameraAlignmentLineEndInset, y1, 0f),
                    color, "H_Mid_Left");

                CreateLine(gridRoot.transform,
                    new Vector3(xMid + CameraAlignmentLineEndInset, y1, 0f),
                    new Vector3(xRight - CameraAlignmentLineEndInset, y1, 0f),
                    color, "H_Mid_Right");

                // Horizontal segments at y2
                CreateLine(gridRoot.transform,
                    new Vector3(xLeft + CameraAlignmentLineEndInset, y2, 0f),
                    new Vector3(xMid - CameraAlignmentLineEndInset, y2, 0f),
                    color, "H_Top_Left");

                CreateLine(gridRoot.transform,
                    new Vector3(xMid + CameraAlignmentLineEndInset, y2, 0f),
                    new Vector3(xRight - CameraAlignmentLineEndInset, y2, 0f),
                    color, "H_Top_Right");
            }

            // Always show markers if lines mode is on, and also when markers-only mode is selected
            float[] xs = { xLeft, xMid, xRight };
            float[] ys = { y0, y1, y2 };

            foreach (float x in xs)
            {
                foreach (float y in ys)
                {
                    CreateAlignmentMarker(gridRoot.transform, new Vector3(x, y, 0f), color, $"Marker_{x:F2}_{y:F2}");
                }
            }
        }
        /// <summary>
        /// Creates a set of vertical alignment markers as opposed to a full grid with the specified local origin.
        /// </summary>
        /// <remarks>Connecting lines between markers are created only if the camera alignment grid
        /// display lines option is enabled in the configuration.</remarks>
        /// <param name="parent">The parent transform to which the marker set root will be attached.</param>
        /// <param name="setName">The name to assign to the root GameObject of the marker set.</param>
        /// <param name="localOrigin">The local position, relative to the parent, at which to place the marker set root.</param>
        /// <param name="color">The color to use for the markers and lines.</param>
        private void CreateVerticalMarkerSet(Transform parent, string setName, Vector3 localOrigin, UnityEngine.Color color)
        {
            GameObject setRoot = new GameObject(setName);
            setRoot.transform.SetParent(parent, false);
            setRoot.transform.localPosition = localOrigin;
            setRoot.transform.localRotation = Quaternion.identity;
            setRoot.transform.localScale = Vector3.one;

            float step = GetCameraAlignmentGridUnitSizeMeters();

            Vector3 bottom = new Vector3(0f, 0f, 0f);
            Vector3 middle = new Vector3(0f, step, 0f);
            Vector3 top = new Vector3(0f, step * 2f, 0f);

            if (Config.Instance.CameraAlignmentGridDisplayLines)
            {
                CreateLine(
                    setRoot.transform,
                    bottom + Vector3.up * CameraAlignmentLineEndInset,
                    middle - Vector3.up * CameraAlignmentLineEndInset,
                    color,
                    "Line_BottomToMiddle");

                CreateLine(
                    setRoot.transform,
                    middle + Vector3.up * CameraAlignmentLineEndInset,
                    top - Vector3.up * CameraAlignmentLineEndInset,
                    color,
                    "Line_MiddleToTop");
            }

            CreateAlignmentMarker(setRoot.transform, bottom, color, "Marker_Bottom");
            CreateAlignmentMarker(setRoot.transform, middle, color, "Marker_Middle");
            CreateAlignmentMarker(setRoot.transform, top, color, "Marker_Top");
        }

        /// <summary>
        /// This is a center space and orientation marker set on the floor so the user has a reference when resetting their space.
        /// </summary>
        /// <param name="parent"></param>
        private void CreateOriginFloorReference(Transform parent)
        {
            GameObject root = new GameObject("OriginFloorReference");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = Vector3.zero;
            root.transform.localRotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;

            float step = .5f;// half a meter // GetCameraAlignmentGridUnitSizeMeters();

            Vector3 left = new Vector3(-step, 0f, 0f);
            Vector3 center = Vector3.zero;
            Vector3 right = new Vector3(step, 0f, 0f);

            UnityEngine.Color color = UnityEngine.Color.white;

            // Always show lines for this set.
            CreateLine(
                root.transform,
                left + Vector3.right * CameraAlignmentLineEndInset,
                center - Vector3.right * CameraAlignmentLineEndInset,
                color,
                "OriginLine_Left");

            CreateLine(
                root.transform,
                center + Vector3.right * CameraAlignmentLineEndInset,
                right - Vector3.right * CameraAlignmentLineEndInset,
                color,
                "OriginLine_Right");

            CreateAlignmentMarker(root.transform, left, color, "OriginMarker_Left");
            CreateAlignmentMarker(root.transform, center, color, "OriginMarker_Center");
            CreateAlignmentMarker(root.transform, right, color, "OriginMarker_Right");
        }

        private bool IsOriginZeroFeet(SerializableVector3 v)
        {
            const float eps = 0.05f;
            return Mathf.Abs(v.x) < eps && Mathf.Abs(v.y) < eps && Mathf.Abs(v.z) < eps;
        }

        private bool HasGridAtOrigin()
        {
            return
                Config.Instance.EnableCameraAlignmentGrid1 &&
                IsOriginZeroFeet(Config.Instance.CameraAlignmentGrid1OriginFeet)
                ||
                Config.Instance.EnableCameraAlignmentGrid2 &&
                IsOriginZeroFeet(Config.Instance.CameraAlignmentGrid2OriginFeet);
        }

        private void CreateLine(Transform parent, Vector3 start, Vector3 end, UnityEngine.Color color, string name)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);

            Vector3 delta = end - start;
            float length = delta.magnitude;
            Vector3 center = (start + end) * 0.5f;

            go.transform.localPosition = center;
            go.transform.localRotation = Quaternion.FromToRotation(Vector3.up, delta.normalized);
            go.transform.localScale = new Vector3(CameraAlignmentLineThickness, length, CameraAlignmentLineThickness);

            ApplyAlignmentMaterial(go, color);
            RemoveCollider(go);
        }

        private void CreateAlignmentMarker(Transform parent, Vector3 localPos, UnityEngine.Color color, string name)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one * CameraAlignmentMarkerDiameter;

            ApplyAlignmentMaterial(go, color);
            RemoveCollider(go);
        }

        private static Material _cameraAlignmentTemplateMat;

        private Material GetAlignmentMaterial(UnityEngine.Color color)
        {
            UnityEngine.Color transparentColor = GetTransparentAlignmentColor(color);

            if (_cameraAlignmentTemplateMat == null)
            {
                Renderer noteArrowRenderer = FindRendererByNameAcrossScenes("NoteArrow");
                if (noteArrowRenderer != null && noteArrowRenderer.sharedMaterial != null)
                {
                    _cameraAlignmentTemplateMat = new Material(noteArrowRenderer.sharedMaterial);
                    Plugin.LogDebug("[CameraAlignmentGrids] Using NoteArrow material as calibration template");
                }
                else
                {
                    Shader shader =
                        Shader.Find("Custom/SimpleLit") ??
                        Shader.Find("Sprites/Default") ??
                        Shader.Find("UI/Default") ??
                        Shader.Find("Unlit/Color");

                    if (shader == null)
                    {
                        Plugin.LogDebug("[CameraAlignmentGrids] Could not find fallback shader for alignment grids");
                        return null;
                    }

                    _cameraAlignmentTemplateMat = new Material(shader);
                    Plugin.LogDebug($"[CameraAlignmentGrids] Using fallback shader: {shader.name}");
                }
            }

            Material mat = new Material(_cameraAlignmentTemplateMat);

            if (mat.HasProperty("_Color"))
                mat.SetColor("_Color", transparentColor);

            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", transparentColor);

            if (mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", transparentColor * 0.5f);
            }

            return mat;
        }
        private UnityEngine.Color GetTransparentAlignmentColor(UnityEngine.Color color)
        {
            color.a = CameraAlignmentAlpha;
            return color;
        }

        private void ApplyAlignmentMaterial(GameObject go, UnityEngine.Color color)
        {
            var renderer = go.GetComponent<Renderer>();
            if (renderer == null)
                return;

            Material mat = GetAlignmentMaterial(color);
            if (mat == null)
            {
                Plugin.LogDebug($"[CameraAlignmentTool] Failed to create material for {go.name}");
                return;
            }

            renderer.material = mat;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        private void RemoveCollider(GameObject go)
        {
            var collider = go.GetComponent<Collider>();
            if (collider != null)
                Destroy(collider);
        }

        #endregion

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
                Plugin.LogDebug("[MixedReality] LateUpdate detected missing menu shell; starting scan");
                _menuRoutine = StartCoroutine(SetupMenuGreenScreen());
            }

            if (_isGameplayLoaded && _spawnedShell == null && _gameRoutine == null)
            {
                Plugin.LogDebug("[MixedReality] LateUpdate detected missing gameplay shell; starting scan");
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

                Plugin.LogDebug(
                    $"[MixedReality] Poll t={elapsed:F2} " +
                    $"PlayerTransforms={(_playerTransforms != null)} " +
                    $"headTransform={(_headTransform != null ? _headTransform.name : "null")} " +
                    $"Camera.main={(Camera.main != null ? Camera.main.name + " scene=" + Camera.main.gameObject.scene.name : "null")}");

                if (_playerTransforms != null)
                {
                    Plugin.LogDebug("[MixedReality] Head-follow source found; enabling shell follow");
                    _lockShellToHead = true;
                    _tryingToFindHead = false;
                    yield break;
                }

                yield return new WaitForSecondsRealtime(0.25f);
                elapsed += 0.25f;
            }

            Plugin.LogDebug("[MixedReality] Failed to find head-follow source in time");
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
                    Plugin.LogDebug($"[MixedReality] Menu camera acquired: {cam.name} scene={sceneName}");
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
            Plugin.LogDebug($"[MixedReality] FindObjectOfType<PlayerTransforms>() => {(_playerTransforms != null ? "FOUND" : "null")}");

            if (_playerTransforms != null)
            {
                Plugin.LogDebug("[MixedReality] PlayerTransforms acquired");
                _lockShellToHead = true;
            }
        }
       */

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

                    Plugin.LogDebug("[MixedReality] ===== UI CANDIDATES START =====");

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
                                Plugin.LogDebug($"[MixedReality] GO: {t.gameObject.name} | Path: {GetPathFromRoot(t, root.transform)} | Component: {typeName}");
                            }
                        }
                    }

                    Plugin.LogDebug("[MixedReality] ===== UI CANDIDATES END =====");
                    return;
                }
            }
        }
        */
        private static void LogPossibleNoteRenderers()
        {
            Plugin.LogDebug("[MixedReality] ===== NOTE RENDERER SEARCH START =====");

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

                        Plugin.LogDebug(
                            $"[MixedReality] Note-like renderer scene={scene.name} go={r.gameObject.name} " +
                            $"type={r.GetType().Name} mat={matName} shader={shaderName}");
                    }
                }
            }

            Plugin.LogDebug("[MixedReality] ===== NOTE RENDERER SEARCH END =====");
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

                    Plugin.LogDebug("[MixedReality] ===== Hub Screen State START =====");

                    if (mainScreen != null)
                        Plugin.LogDebug($"[MixedReality] MainScreen activeSelf={mainScreen.gameObject.activeSelf} activeInHierarchy={mainScreen.gameObject.activeInHierarchy}");
                    else
                        Plugin.LogDebug("[MixedReality] MainScreen not found");

                    if (mainMenuVC != null)
                        Plugin.LogDebug($"[MixedReality] MainMenuViewController activeSelf={mainMenuVC.gameObject.activeSelf} activeInHierarchy={mainMenuVC.gameObject.activeInHierarchy}");
                    else
                        Plugin.LogDebug("[MixedReality] MainMenuViewController not found");

                    if (leftScreen != null)
                        Plugin.LogDebug($"[MixedReality] LeftScreen activeSelf={leftScreen.gameObject.activeSelf} activeInHierarchy={leftScreen.gameObject.activeInHierarchy}");
                    else
                        Plugin.LogDebug("[MixedReality] LeftScreen not found");

                    if (menuButtonsVC != null)
                        Plugin.LogDebug($"[MixedReality] MenuButtonsViewController activeSelf={menuButtonsVC.gameObject.activeSelf} activeInHierarchy={menuButtonsVC.gameObject.activeInHierarchy}");
                    else
                        Plugin.LogDebug("[MixedReality] MenuButtonsViewController not found");

                    Plugin.LogDebug("[MixedReality] ===== Hub Screen State END =====");
                    return;
                }
            }

            Plugin.LogDebug("[MixedReality] Wrapper not found for hub screen state");
        }

        private static void LogLoadedScenesAndRoots(string tag = "")
        {
            Plugin.LogDebug($"[MixedReality] ===== Scene/Root Dump START {tag} =====");

            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded)
                    continue;

                Plugin.LogDebug($"[MixedReality] Scene[{i}] name={scene.name} isLoaded={scene.isLoaded}");

                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    Plugin.LogDebug($"[MixedReality]   Root: {root.name}");
                }
            }

            Plugin.LogDebug($"[MixedReality] ===== Scene/Root Dump END {tag} =====");
        }
        private static void LogHierarchy(Transform root, int maxDepth = 4, string indent = "", int depth = 0)
        {
            if (root == null || depth > maxDepth)
                return;

            Plugin.LogDebug($"[MixedReality] {indent}{root.name}");

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
                Plugin.LogDebug("[MixedReality] MainMenu scene not found");
                return;
            }

            foreach (GameObject root in mainMenu.GetRootGameObjects())
            {
                if (root.name == "Wrapper")
                {
                    Plugin.LogDebug("[MixedReality] ===== MainMenu Wrapper Hierarchy START =====");
                    LogHierarchy(root.transform, 6);
                    Plugin.LogDebug("[MixedReality] ===== MainMenu Wrapper Hierarchy END =====");
                    return;
                }
            }

            Plugin.LogDebug("[MixedReality] Wrapper root not found in MainMenu");
        }

        private static void LogRenderers(GameObject root)
        {
            Plugin.LogDebug("[MixedReality] ===== RENDERER LOG START =====");

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

                Plugin.LogDebug(
                    $"[MixedReality] Renderer Path: {path} | " +
                    $"GO: {r.gameObject.name} | " +
                    $"Type: {r.GetType().Name} | " +
                    $"Mat: {materialName} | " +
                    $"Shader: {shaderName} | " +
                    $"Enabled: {r.enabled}");
            }

            Plugin.LogDebug("[MixedReality] ===== RENDERER LOG END =====");
        }
    }

}
