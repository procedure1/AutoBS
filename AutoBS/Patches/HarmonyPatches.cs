
using BeatmapSaveDataVersion2_6_0AndEarlier;
using BS_Utils.Gameplay;//BW added to Disable Score submission https://github.com/Kylemc1413/Beat-Saber-Utils 
using CustomJSONData.CustomBeatmap;
using HarmonyLib;
using HMUI;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SongCore;
using SongCore.UI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using UnityEngine;
using UnityEngine.SceneManagement;
using Zenject;
using static IPA.Logging.Logger;
using static NoteData;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.ToolTip;
using TMPro;





namespace AutoBS.Patches
{

    #region Prefix - Bright Lasers
    //Taken from Technicolor mod - needs no other code except the BSML & Config. without this, rotating lasers are very dull
    [HarmonyPatch]
    [HarmonyPriority(Priority.Low)]
    internal static class BrightLights
    {
        [HarmonyPrefix]
        [HarmonyPatch(typeof(BloomPrePassBackgroundLightWithId), "ColorWasSet")]
        private static void Prefix1(ref Color newColor)
        {
            BrightenLights(ref newColor);
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(BloomPrePassBackgroundColorsGradientTintColorWithLightIds), "ColorWasSet")]
        [HarmonyPatch(typeof(BloomPrePassBackgroundColorsGradientTintColorWithLightId), "ColorWasSet")]
        [HarmonyPatch(typeof(BloomPrePassBackgroundColorsGradientElementWithLightId), "ColorWasSet")]
        [HarmonyPatch(typeof(TubeBloomPrePassLightWithId), "ColorWasSet")]
        private static void Prefix2(ref Color color)
        {
            BrightenLights(ref color);
        }

        private static void BrightenLights(ref Color color)
        {
            if (!Config.Instance.EnablePlugin) return;
            if (!Utils.IsEnabledLighting()) return;
            /*
            if (!Config.Instance.EnablePlugin) return;

            // If EnableFeaturesForNonGen360Maps is false and the map is 360Degree or 90Degree, exit
            if (!Config.Instance.EnableFeaturesForNonGen360Maps &&
                (TransitionPatcher.characteristicSerializedName == "360Degree" ||
                 TransitionPatcher.characteristicSerializedName == "90Degree")) return;

            // If EnableFeaturesForStandardMaps is false and the map is a standard map, exit
            if (!Config.Instance.EnableFeaturesForStandardMaps &&
                (TransitionPatcher.characteristicSerializedName != "Generated360Degree" &&
                 TransitionPatcher.characteristicSerializedName != "Generated90Degree" &&
                 TransitionPatcher.characteristicSerializedName != "360Degree" &&
                 TransitionPatcher.characteristicSerializedName != "90Degree")) return;
            */

            // Doesn't apply to standard maps
            if (Config.Instance.BrightLights &&
                (TransitionPatcher.SelectedSerializedName == "Generated360Degree" ||
                 TransitionPatcher.SelectedSerializedName == "360Degree" ||
                 TransitionPatcher.SelectedSerializedName == "90Degree"))
            {
                color.a *= 2f; // Apply brightness adjustment
            }
        }
    }
    #endregion

    #region Prefix - BeatmapObjectSpawnMovementData -  BigLasers
    //BW 5th item that runs. JDFixer uses this method so that the user can update the MaxJNS over and over. i tried it in LevelUpdatePatcher. it works but can only be updated before play song one time https://github.com/zeph-yr/JDFixer/blob/b51c659def0e9cefb9e0893b19647bb9d97ee9ae/StandardLevelDetailViewPatch.cs
    //note jump offset determines how far away notes spawn from you. A negative modifier means notes will spawn closer to you, and a positive modifier means notes will spawn further away

    [HarmonyPatch(typeof(BeatmapObjectSpawnMovementData), nameof(BeatmapObjectSpawnMovementData.Init))]
    [HarmonyPatch(new Type[] { typeof(int), typeof(IJumpOffsetYProvider), typeof(Vector3) })]
    internal class SpawnMovementDataUpdatePatch
    {
        //private static bool OriginalValuesSet = false; // Flag to ensure original values are only stored once
        //public static float OriginalNJS; // Store the original startNoteJumpMovementSpeed
        //public static float OriginalNJO;
        internal static void Prefix(int noteLinesCount, IJumpOffsetYProvider jumpOffsetYProvider, Vector3 rightVec)
        {
            if (!Config.Instance.EnablePlugin) return;
            if (!Utils.IsEnabledLighting()) return;

            if (Config.Instance.BigLasers &&
                (TransitionPatcher.SelectedSerializedName == "Generated360Degree" ||
                 TransitionPatcher.SelectedSerializedName == "Generated90Degree" ||
                 TransitionPatcher.SelectedSerializedName == "360Degree" ||
                 TransitionPatcher.SelectedSerializedName == "90Degree")) // only do this for gen 360 or else it will do this for all maps
            {
                BigLasers();
                //BigLasers myOtherInstance = new BigLasers();
                // myOtherInstance.Big();
            }

        }

        // Used plugin.cs zenject installer in order to access ParametricBoxController() method. it seems to control lasers.but some unknown method calls it. so instead i scaled the gameObject that uses ParametricBoxController()
        public static void BigLasers()
        {
            // Get all ParametricBoxController objects in the scene
            //Environment>TopLaser>BoxLight, Environment>DownLaser>BoxLight, Environment/RotatingLaser/Pair/BaseR or BaseL/Laser/BoxLight
            ParametricBoxController[] boxControllers = GameObject.FindObjectsOfType<ParametricBoxController>();

            // Modify the ParametricBoxController properties of all BoxLights
            int i = 1;
            foreach (ParametricBoxController boxController in boxControllers)
            {
                Transform parentTransform = boxController.gameObject.transform.parent;//scale gameObject parent
                Vector3 currentScale = parentTransform.localScale;

                //if (i == 1)//so doesn't reappear several times
                //    Plugin.Log.Info($"BoxLights Scaled");

                switch (parentTransform.name)
                {
                    case "Laser":
                        parentTransform.localScale = new Vector3(currentScale.x * 8, currentScale.y * 1, currentScale.z * 8);
                        //Plugin.Log.Info($"Rotating Lasers Scaled");
                        break;
                    case "TopLaser":
                        parentTransform.localScale = new Vector3(currentScale.x * 5, currentScale.y * 5, currentScale.z * 5);//y seems to be the length of the long top laser bars
                        //Plugin.Log.Info($"Top Lasers Scaled");
                        break;
                    default:
                        if (i == 4 || i == 5 || i == 10 || i == 11 || i == 12)
                        {
                            parentTransform.localScale = new Vector3(currentScale.x * 5, currentScale.y * 5, currentScale.z * 5);
                            //Plugin.Log.Info($"Down Lasers Fully Scaled");
                        }
                        else
                        {
                            parentTransform.localScale = new Vector3(currentScale.x * 2, currentScale.y * 1, currentScale.z * 2);//Don't scale these actual DownLasers to be longer since looks messy
                            //Plugin.Log.Info($"Down Lasers Partially Scaled");
                        }
                        break;
                }
                i++;
            }
        }
    }
    #endregion


    #region Prefix - BeatmapDataLoader.LoadBeatmapDataAsync - adds the beatmapData (IReadonlyBeatmapData)

    // This works great, but required mods like Noodle and Chroma will not activate on the gen 360 map. It uses a unique BeatmapKey so scoring works.
    // If want required mods to work, Do not prefix BeatmapDataLoader.LoadBeatmapDataAsync to feed your own BeatmapKey. There is no way to force activate the mods like i do for Mapping extensions which has its own built in method for that.
    // since that shortcut bypasses parts of the normal pipeline that Heck/NE/Chroma expect to hook (esp.their CustomDataManager.DeserializeObjects path). Remove that prefix.Let the loader run naturally so requirements get detected and those mods attach.
    // -----------
    // If not worried about required mods working in gen 360, then this is a great way to inject your custom data and have song core see it as a new different map with a different beatmapKey
    // MUST USE THIS! Otherwise will get a NULL for the beatmapData when CreateTransformedBeatmapData is called on gen360 since there is no beatmapData yet without this.
    // Short-circuit the loader - load custom data for a gen360 map. 
    // Set Content only creates metaData for the map, not the actual notes and obstacles.
    // this adds the beatmapData (IReadonlyBeatmapData) with actual notes etc to the map and hands it off to CreateTransformedBeatmapData

    [HarmonyPatch(typeof(BeatmapDataLoader), nameof(BeatmapDataLoader.LoadBeatmapDataAsync))]
    static class Patch_BeatmapDataLoader_LoadAsync
    {
        static bool Prefix(
            BeatmapDataLoader __instance,
            IBeatmapLevelData beatmapLevelData,
            BeatmapKey beatmapKey,
            float startBpm,
            bool loadingForDesignatedEnvironment,
            IEnvironmentInfo targetEnvironmentInfo,
            IEnvironmentInfo originalEnvironmentInfo,
            object beatmapLevelDataVersion,    // catch the real BeatmapLevelDataVersion
            GameplayModifiers gameplayModifiers,
            PlayerSpecificSettings playerSpecificSettings,
            bool enableBeatmapDataCaching,
            ref Task<IReadonlyBeatmapData> __result)
        {
            if (!Config.Instance.EnablePlugin) return true;
            if (!Utils.IsEnabledForGeneralFeatures()) return true;

            if (!loadingForDesignatedEnvironment)
            {
                Plugin.Log.Info("[LoadBeatmapDataAsync] Skipping non-designated environment load.");
                return true; // run the original for previews etc. this prevents it from running multiple times on the same difficulty
            }

            Plugin.Log.Info($"[LoadBeatmapDataAsync] Called for ID: {beatmapKey.levelId} Difficulty: {beatmapKey.difficulty} Characteristic: {beatmapKey.beatmapCharacteristic.serializedName}.");

            // This is called multiple times so I use this limit to only the difficulty the user selected to play.
            if (!TransitionPatcher.UserSelectedMapToInject)//beatmapKey.beatmapCharacteristic.serializedName != "Generated360Degree")
            {
                Plugin.Log.Info($"[LoadBeatmapDataAsync] Not a Generated360Degree map, skipping custom data loading.");
                return true; // if NOT gen360 then original method runs unmodified (true)
            }

            // Use your custom data (from a static or recently generated variable)

            IReadonlyBeatmapData cbd = BeatmapDataRegistry.beatmapDataByKey[beatmapKey] as IReadonlyBeatmapData;

                //if (HarmonyPatches.cjBeatmapData != null)
                //if (saveData != null)
            if (cbd != null)
            {
                Plugin.Log.Info($"[LoadBeatmapDataAsync] Custom Level - Using cjBeatmapData for {beatmapKey.beatmapCharacteristic.serializedName} requires360Movement: {beatmapKey.beatmapCharacteristic.requires360Movement} containsRotationEvents: {beatmapKey.beatmapCharacteristic.containsRotationEvents}.");
                Plugin.Log.Info($"[LoadBeatmapDataAsync] Custom Level - Retrieved BeatmapData from JSON - notes: {cbd.allBeatmapDataItems.OfType<NoteData>().Count()} obstacles: {cbd.allBeatmapDataItems.OfType<ObstacleData>().Count()} events: {cbd.allBeatmapDataItems.OfType<EventData>().Count()} bpm change events: {cbd.allBeatmapDataItems.OfType<CustomBPMChangeBeatmapEventData>().Count()}.");

                //IReadonlyBeatmapData cjBeatmapData = SetContent.ProduceSimpleCJD(saveData);
                //__result = Task.FromResult(cjBeatmapData);
                __result = Task.FromResult(cbd);
                return false;
            }


            Plugin.Log.Error($"[LoadBeatmapDataAsync] cjBeatmapData is NULL for {beatmapKey.beatmapCharacteristic.serializedName} requires360Movement: {beatmapKey.beatmapCharacteristic.requires360Movement} containsRotationEvents: {beatmapKey.beatmapCharacteristic.containsRotationEvents} {beatmapKey.levelId} {beatmapKey.difficulty}.");

            return true;
        }
    }

    #endregion

    #region Prefix - SetContentForBeatmapData -- used to populate Gen 360 difficulties stats in the menu

    [HarmonyPatch(typeof(StandardLevelDetailView), "SetContentForBeatmapData")]
    public static class Patch_StandardLevelDetailView_SetContentForBeatmapData
    {
        static bool Prefix(StandardLevelDetailView __instance)
        {
            if (!Config.Instance.EnablePlugin) return true;
            if (!Config.Instance.Enable360fyer) return true;

            // Access current level and selected difficulty
            var levelField = typeof(StandardLevelDetailView).GetField("_beatmapLevel", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var level = (BeatmapLevel)levelField.GetValue(__instance);

            Plugin.Log.Info($"[SetContentForBeatmapData] Called for level: {level?.levelID}");

            if (level == null)
                return true; // fallback to vanilla

            // Access characteristic/difficulty controls
            var charCtrl = (BeatmapCharacteristicSegmentedControlController)
                typeof(StandardLevelDetailView).GetField("_beatmapCharacteristicSegmentedControlController", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(__instance);

            var diffCtrl = (BeatmapDifficultySegmentedControlController)
                typeof(StandardLevelDetailView).GetField("_beatmapDifficultySegmentedControlController", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(__instance);

            var selectedCharacteristic = charCtrl.selectedBeatmapCharacteristic;
            var selectedDifficulty = diffCtrl.selectedDifficulty;

            // Build BeatmapKey for lookup
            var key = new BeatmapKey(level.levelID, selectedCharacteristic, selectedDifficulty);

            // Check if this is a Gen360 or custom-injected difficulty
            // (Update this condition to fit your naming for generated gamemodes)
            if (selectedCharacteristic.serializedName == GameModeHelper.GENERATED_360DEGREE_MODE)
            {
                // Try to get stats from your registry
                var stats = MenuDataRegistry.GetStatsForKey(key); // <-- Implement this method

                if (stats != null)
                {
                    // Set UI with your stats
                    // Access _levelParamsPanel and _levelParamsPanelCanvasGroup
                    var panelField = typeof(StandardLevelDetailView).GetField("_levelParamsPanel", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    var canvasGroupField = typeof(StandardLevelDetailView).GetField("_levelParamsPanelCanvasGroup", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                    var panel = (LevelParamsPanel)panelField.GetValue(__instance);
                    var canvasGroup = (CanvasGroup)canvasGroupField.GetValue(__instance);

                    // Un-fade the panel
                    canvasGroup.alpha = 1f;

                    // Set values (assuming your stats object has these fields)
                    panel.notesCount = stats.notesCount;
                    panel.obstaclesCount = stats.obstaclesCount;
                    panel.bombsCount = stats.bombsCount;
                    panel.notesPerSecond = stats.notesPerSecond; // Or compute notes/sec

                    Plugin.Log.Info($"[SetContentForBeatmapData] Updated UI for Gen360 - Notes: {stats.notesCount}, Obstacles: {stats.obstaclesCount}, Bombs: {stats.bombsCount}, NPS: {stats.notesPerSecond}");

                    // Optional: update additional UI text if needed, e.g. set NPS text label
                    // (depends on your LevelParamsPanel fields)

                    // Prevent vanilla from overwriting your stats/UI
                    return false;
                }
                // If not found, let vanilla run and fallback to normal
            }

            Plugin.Log.Info("[SetContentForBeatmapData] Not a Gen360 map or stats not found, using vanilla UI.");

            // Not Gen360: let vanilla code handle everything else
            return true;
        }
    }
    /*
    // this patch is a 2nd SetContentForBeatmapData patch and for this current version of the mod, it ends up blocking the menu stats from appearing.
    #region 3 Prefix - Patch_StandardLevelDetailView_ForceRecalc
    //v1.40
    // Force the menu stats‐panel to recalculate from JSON
    // By default the detail‐view will only use the built‐in precalculated data(and you’ve been returning a blank placeholder for your 360 characteristic). The easiest hack is to short‐circuit always go into the JSON‐recalculate path:
    [HarmonyPatch(typeof(StandardLevelDetailView))]
    [HarmonyPatch("SetContentForBeatmapData", MethodType.Normal)]
    static class Patch_StandardLevelDetailView_ForceRecalc
    {
        // Cache the MethodInfos so we only look them up once
        static readonly MethodInfo ClearContentMI = typeof(StandardLevelDetailView)
            .GetMethod("ClearContent", BindingFlags.Instance | BindingFlags.NonPublic);
        static readonly MethodInfo CalcContentMI = typeof(StandardLevelDetailView)
            .GetMethod("CalculateAndSetContent", BindingFlags.Instance | BindingFlags.NonPublic);

        static bool Prefix(StandardLevelDetailView __instance)
        {
            Plugin.Log.Info("[SetContentForBeatmapData] Force Recalc on menu stats called.");
            // 1) Call ClearContent()
            ClearContentMI.Invoke(__instance, null);
            // 2) Call CalculateAndSetContent()
            CalcContentMI.Invoke(__instance, null);
            // 3) Skip the original SetContentForBeatmapData entirely
            return false;
        }
    }
    #endregion
    */



    #endregion


    // Patch BS_Utils score submission disabled banner to show only one line instead of multiple lines and to fix problem of 1st run not showing disabled mod reason

    [HarmonyPatch(typeof(ResultsViewController), "SetDataToUI")]
    [HarmonyAfter("com.kyle1413.BeatSaber.BS-Utils")] // run after BS_Utils
    static class ResultsViewController_SetDataToUI_Fix
    {
        // Store base text per controller instance (weak map pattern)
        static readonly Dictionary<int, string> _baseText = new Dictionary<int, string>();

        // Prefix runs BEFORE BS_Utils; remember the base TMP text
        static void Prefix(ResultsViewController __instance,
                           ref GameObject ____clearedBannerGo,
                           ref GameObject ____failedBannerGo)
        {
            if (!Config.Instance.EnablePlugin) return;
            if (!Utils.IsEnabledForGeneralFeatures()) return;

            var obj = ____clearedBannerGo.activeInHierarchy ? ____clearedBannerGo : ____failedBannerGo;
            var tmp = obj.GetComponentInChildren<CurvedTextMeshPro>();
            if (tmp != null) _baseText[__instance.GetHashCode()] = tmp.text ?? "";
        }

        // Postfix runs AFTER BS_Utils; normalize text to 1 line (or show your own)
        
        const string LabelName = "AutoBS_NoSubmitLabel";

        static void Postfix(ref GameObject ____clearedBannerGo, ref GameObject ____failedBannerGo, ref TextMeshProUGUI ____rankText)
        {
            if (!Config.Instance.EnablePlugin) return;
            if (!Utils.IsEnabledForGeneralFeatures()) return;

            // Debug: confirm gate state at render time
            Plugin.Log.Info($"[ScoreGate] Results UI: IsDisabled={ScoreGate.IsDisabledThisRun} Reason='{ScoreGate.ReasonThisRun}'");

            // If not disabled this run, hide (or remove) our label if present and bail.
            var host = ____clearedBannerGo.activeInHierarchy ? ____clearedBannerGo : ____failedBannerGo;
            var label = host.transform.Find(LabelName)?.GetComponent<TextMeshProUGUI>();
            if (!ScoreGate.IsDisabledThisRun)
            {
                if (label) label.gameObject.SetActive(false);
                return;
            }

            // Ensure a label exists (create once, reuse forever).
            if (!label)
            {
                var go = new GameObject(LabelName);
                go.transform.SetParent(host.transform, false);

                label = go.AddComponent<TextMeshProUGUI>();
                label.raycastTarget = false;
                label.enableWordWrapping = false;
                label.alignment = TextAlignmentOptions.Center;
                label.fontSize = 3.5f;

                // Place it just under the banner text (or under the rank text as a fallback).
                var rt = (RectTransform)label.transform;
                rt.anchorMin = new Vector2(0.5f, 0f);
                rt.anchorMax = new Vector2(0.5f, 0f);
                rt.pivot = new Vector2(0.5f, 0f);
                rt.anchoredPosition = new Vector2(0f, 35f);

                // If the banner hierarchy is odd on first run, fall back to rankText’s parent
                if (!label.isActiveAndEnabled && ____rankText && ____rankText.transform is RectTransform rankRT)
                {
                    rt.SetParent(rankRT.parent, false);
                    rt.anchoredPosition = new Vector2(0f, -30f);
                }

                Plugin.Log.Info("[ScoreGate] Created results label");
            }

            // Write a single, clean line every time (no stacking).
            var reason = ScoreGate.ReasonThisRun;
            label.text =
                $"\n<size=200%><color=#ff0000ff>Score submission disabled:</color>{(string.IsNullOrEmpty(reason) ? "" : $"<color=#ff0000ff>  {reason}")}</color></size>";
            label.gameObject.SetActive(true);
        }
    }
    //Clear your flag when you return to menu so it doesn’t carry over
    [HarmonyPatch(typeof(MainFlowCoordinator), "DidActivate")]
    static class ScoreGate_ClearOnMenu
    {
        static void Postfix()
        {
            if (!Config.Instance.EnablePlugin) return;
            if (!Utils.IsEnabledForGeneralFeatures()) return;

            Plugin.Log.Info("[ScoreGate] Clearing on MainFlowCoordinator.DidActivate");
            ScoreGate.Clear();
        }

    }


    public static class ScoreGate
    {
        public static bool IsDisabledThisRun { get; private set; }
        public static string ReasonThisRun { get; private set; } = "";

        public static void Set(string reason)
        {
            IsDisabledThisRun = !string.IsNullOrEmpty(reason);
            ReasonThisRun = reason ?? "";
        }
        public static void Clear()
        {
            IsDisabledThisRun = false;
            ReasonThisRun = "";
        }
    }

    // Gets User selected difficulty when a user changes the difficulty in the menu. Otherwise, SetContent will have the default difficulty when a new song is selected.
    //[HarmonyPatch(typeof(StandardLevelDetailView), "HandleBeatmapDifficultySegmentedControlControllerDidSelectDifficulty")]

}
