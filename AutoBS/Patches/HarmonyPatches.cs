
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
                 TransitionPatcher.SelectedSerializedName == "Generated90Degree" ||
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

    //v1.40
    [HarmonyPatch(typeof(BeatmapObjectSpawnMovementData), nameof(BeatmapObjectSpawnMovementData.Init))]
    [HarmonyPatch(new Type[] { typeof(int), typeof(IJumpOffsetYProvider), typeof(Vector3) })]
    internal class SpawnMovementDataUpdatePatch
    {
        //private static bool OriginalValuesSet = false; // Flag to ensure original values are only stored once
        //public static float OriginalNJS; // Store the original startNoteJumpMovementSpeed
        //public static float OriginalNJO;
        internal static void Prefix(int noteLinesCount, IJumpOffsetYProvider jumpOffsetYProvider, Vector3 rightVec)
        {
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

    #region UNUSED Prefix - Custom Colors - Gets  author's custom color scheme to work NOT WORKING but MAYBE NOT NEEDED. TEST!!!!!!!!!!!!!!!!!!!!!!!!!!
    // THIS DOENS'T Work in the same way anymore for v1.40 since IDifficultyBeatmap and overrideColorScheme are gone. Can get this info from another prefix if need to use this
    //This patch was originally intended to override the ColorScheme used for a given beatmap, typically based on custom data in the beatmap’s JSON (e.g. _colorLeft, _envColorRight, _obstacleColor, etc.), but only for certain gamemodes like Generated360 and non-gen 360
    //This will set an author's custom color scheme to work. But built-in and non-customized colors on custom maps will get the standard default color scheme. Colors will revert to the player's color override if that is set.
    //v1.34
    //[HarmonyPatch(typeof(StandardLevelScenesTransitionSetupDataSO), "Init")]
    //v1.40
    /*
    [HarmonyPatch]
    internal class ColorSchemeUpdatePatch
    {
        //v1.34
        //internal static void Prefix(IDifficultyBeatmap difficultyBeatmap, ref ColorScheme overrideColorScheme, ColorScheme beatmapOverrideColorScheme)
        //v1.40
        internal static void Prefix(ref ColorScheme colorScheme)
        {
            if (!Utils.IsEnabledForGeneralFeatures()) return;

            //if (!Config.Instance.EnablePlugin) return;

            // If EnableFeaturesForNonGen360Maps is false and the map is 360Degree or 90Degree, exit
            //if (!Config.Instance.EnableFeaturesForNonGen360Maps &&
            //    (TransitionPatcher.characteristicSerializedName == "360Degree" ||
            //    TransitionPatcher.characteristicSerializedName == "90Degree")) return;

            //only needed for non-standard environments

            //v1.40
            if (TransitionPatcher.characteristicSerializedName == "Generated360Degree" || TransitionPatcher.characteristicSerializedName == "360Degree" || TransitionPatcher.characteristicSerializedName == "90Degree")//only do this for gen 360 or else it will do this for all maps
            {

                string beatmapString = beatmapLevelData.GetBeatmapString(in beatmapKey);

                if (!string.IsNullOrEmpty(beatmapString) && beatmapString.TrimStart().StartsWith("{"))
                {
                    JObject fullJson = JObject.Parse(beatmapString);
                    JObject beatmapCustomData = (JObject)fullJson["customData"];

                    if (beatmapCustomData != null &&
                        (beatmapCustomData["_colorLeft"] != null ||
                         beatmapCustomData["_envColorLeft"] != null ||
                         beatmapCustomData["_obstacleColor"] != null))
                    {
                        // Parse custom colors
                        Color saberAColor = GetColorOrFallback(beatmapCustomData, "_colorLeft", HarmonyPatches.OriginalColorScheme.saberAColor);
                        Color saberBColor = GetColorOrFallback(beatmapCustomData, "_colorRight", HarmonyPatches.OriginalColorScheme.saberBColor);
                        Color environmentColor0 = GetColorOrFallback(beatmapCustomData, "_envColorLeft", HarmonyPatches.OriginalColorScheme.environmentColor0);
                        Color environmentColor1 = GetColorOrFallback(beatmapCustomData, "_envColorRight", HarmonyPatches.OriginalColorScheme.environmentColor1);
                        Color environmentColorW = GetColorOrFallback(beatmapCustomData, "_envColorWhite", HarmonyPatches.OriginalColorScheme.environmentColorW);
                        Color environmentColor0Boost = GetColorOrFallback(beatmapCustomData, "_envColorLeftBoost", HarmonyPatches.OriginalColorScheme.environmentColor0Boost);
                        Color environmentColor1Boost = GetColorOrFallback(beatmapCustomData, "_envColorRightBoost", HarmonyPatches.OriginalColorScheme.environmentColor1Boost);
                        Color environmentColorWBoost = GetColorOrFallback(beatmapCustomData, "_envColorWhiteBoost", HarmonyPatches.OriginalColorScheme.environmentColorWBoost);
                        Color obstaclesColor = GetColorOrFallback(beatmapCustomData, "_obstacleColor", HarmonyPatches.OriginalColorScheme.obstaclesColor);

                        bool supportsEnvironmentColorBoost =
                            beatmapCustomData["_envColorLeftBoost"] != null &&
                            beatmapCustomData["_envColorRightBoost"] != null;

                        //v1.40 added bool usesEnvironmentColor and bool usesObstacleColor 
                        overrideColorScheme = new ColorScheme(
                            "theAuthorsColorScheme",          // colorSchemeId
                            "theAuthorsLocalizationKey",      // localization key
                            false,                            // useOverride
                            "Author's Color Scheme",          // name
                            true,                             // isDefault
                            supportsEnvironmentColorBoost,    // supportsEnvironmentColorBoost
                            saberAColor,                      // saberAColor
                            saberBColor,                      // saberBColor
                            true,                             // usesEnvironmentColor
                            environmentColor0,                // environmentColor0
                            environmentColor1,                // environmentColor1
                            environmentColorW,                // environmentColorW
                            true,                             // usesObstacleColor
                            environmentColor0Boost,           // environmentColor0Boost
                            environmentColor1Boost,           // environmentColor1Boost
                            environmentColorWBoost,           // environmentColorWBoost
                            obstaclesColor                    // obstaclesColor
                        );


                        Plugin.Log.Info("[ColorPatch] Applied author's custom color scheme");
                        return;
                    }
                }

                // If no author-defined scheme, fallback to user or default
                PlayerDataModel playerDataModel = UnityEngine.Object.FindObjectOfType<PlayerDataModel>();
                PlayerData playerData = playerDataModel.playerData;
                bool overrideDefaultColors = playerData.colorSchemesSettings.overrideDefaultColors;

                if (!overrideDefaultColors)
                {
                    Plugin.Log.Info("[ColorPatch] Using default level color scheme");
                    overrideColorScheme = HarmonyPatches.OriginalColorScheme;
                }
                else
                {
                    Plugin.Log.Info("[ColorPatch] User override color scheme is active, doing nothing");
                }

                Color GetColorOrFallback(JObject data, string key, Color fallback)
                {
                    if (data[key] == null)
                        return fallback;

                    return new Color(
                        data[key]["r"]?.Value<float>() ?? fallback.r,
                        data[key]["g"]?.Value<float>() ?? fallback.g,
                        data[key]["b"]?.Value<float>() ?? fallback.b
                    );
                }
                

            }

        }
    }
    */
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

            //TEST for BUILT -IN BEATMAPS ----------------------------------------------
            //BeatmapData originalData;
            /*
            // not called
            string beatmapJson = beatmapLevelData.GetBeatmapString(in beatmapKey);
            string lightShowJson = beatmapLevelData.GetLightshowString(in beatmapKey);

            var environmentLightGroups = targetEnvironmentInfo.environmentLightGroups;

            var lightEventConverter = new BeatmapLightEventConverterNoConvert(); // this works as a dummy but passes no content

            if (beatmapJson != string.Empty)
            {
                Plugin.Log.Info("[Patch_BeatmapDataLoader_LoadAsync] beatmapJson loaded.");
            }
            */

            //if (beatmapLevelData.version < BeatmapSaveDataHelpers.version3.Major)
            //{
            //according to chatGPT CustomJSONData.HarmonyPatches.BeatmapDataLoaderV2_6_0AndEarlierCustomify  CustomJSONData.HarmonyPatches.BeatmapDataLoaderV3Customify have patched the BeatmapDataLoader to keep the customData
            //    originalData = BeatmapDataLoaderVersion2_6_0AndEarlier.BeatmapDataLoader
            //      .GetBeatmapDataFromSaveDataJson(
            //            beatmapJson,
            //            lightShowJson,
            //            beatmapKey.difficulty,
            //            TransitionPatcher.bpm,
            //            false,
            //            targetEnvironmentInfo,
            //            BeatmapLevelDataVersion.Original,
            //            playerSpecificSettings,
            //            lightEventConverter);
            //}
            //else
            //{
            //    originalData = BeatmapDataLoaderVersion3.BeatmapDataLoader
            //      .GetBeatmapDataFromSaveDataJson(
            //            beatmapJson,
            //            lightShowJson,
            //            beatmapKey.difficulty,
            //            TransitionPatcher.bpm,
            //            false,
            //            null,
            //            BeatmapLevelDataVersion.Original,
            //            playerSpecificSettings,
            //            null);
            //}
            //map error since 0 content in logs!!!!!!!!!!!!!!!!!!!!!
            //Plugin.Log.Info($"[Patch_BeatmapDataLoader_LoadAsync] Retrieved Built-in BeatmapData - notes: {originalData.allBeatmapDataItems.OfType<NoteData>().Count()} obstacles: {originalData.allBeatmapDataItems.OfType<ObstacleData>().Count()} events: {originalData.allBeatmapDataItems.OfType<EventData>().Count()}.");

            //__result = Task.FromResult<IReadonlyBeatmapData>(originalData);
            //return false;

            //---------------------------------------

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
            var obj = ____clearedBannerGo.activeInHierarchy ? ____clearedBannerGo : ____failedBannerGo;
            var tmp = obj.GetComponentInChildren<CurvedTextMeshPro>();
            if (tmp != null) _baseText[__instance.GetHashCode()] = tmp.text ?? "";
        }

        // Postfix runs AFTER BS_Utils; normalize text to 1 line (or show your own)
        
        const string LabelName = "AutoBS_NoSubmitLabel";

        static void Postfix(ref GameObject ____clearedBannerGo, ref GameObject ____failedBannerGo, ref TextMeshProUGUI ____rankText)
        {
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






    #region UNUSED Postfix HandleBeatmapDifficultySegmentedControlControllerDidSelectDifficulty - MAY NOT NEED THIS!!!!!!!!
    /*
    // Gets User selected difficulty when a user changes the difficulty in the menu. Otherwise, SetContent will have the default difficulty when a new song is selected.
    [HarmonyPatch(typeof(StandardLevelDetailView), "HandleBeatmapDifficultySegmentedControlControllerDidSelectDifficulty")]
    class Patch_DifficultyChange
    {
        public static BeatmapDifficulty UserSelectedDifficulty = BeatmapDifficulty.Hard; // Default value, can be set by the caller
        static void Postfix(StandardLevelDetailView __instance, BeatmapDifficultySegmentedControlController controller, BeatmapDifficulty difficulty)
        {
            var charController = Traverse.Create(__instance)
                .Field("_beatmapCharacteristicSegmentedControlController")
                .GetValue<BeatmapCharacteristicSegmentedControlController>();

            var selectedCharacteristic = charController.selectedBeatmapCharacteristic;

            var level = Traverse.Create(__instance).Field("_beatmapLevel").GetValue<BeatmapLevel>();


            //SetContent.UserSelectedDifficulty = controller.selectedDifficulty;

            Plugin.Log.Info($"[Patch_DifficultyChange] User selected difficulty: {controller.selectedDifficulty}, characteristic: {selectedCharacteristic.serializedName}");

            //Utils.CreateGen360DifficultySet(level);
        }
    }
    */
    #endregion


    // Beatmap Lighting Logger
    /*
    public static class BeatmapLightingLogger
    {
        public static void LogGLSLightingEvents(BeatmapSaveData beatmapSaveData)
        {
            if (beatmapSaveData == null)
            {
                Plugin.Log.Error("LogLightingEvents: BeatmapSaveData is null!");
                return;
            }
            StringBuilder logOutput = new StringBuilder();

            logOutput.AppendLine($"===== SONG NAME: {HarmonyPatches.SongName} -- LIGHTING EVENT LOG =====");

            // Log Basic Beatmap Events
            logOutput.AppendLine("\n--- Basic Beatmap Events ---");

            foreach (var eventData in beatmapSaveData.basicBeatmapEvents)
            {
                logOutput.AppendLine($"[Beat: {eventData.beat}] | Type: {eventData.eventType} | Value: {eventData.value} | Float Value: {eventData.floatValue}");

                if (beatmapSaveData is CustomBeatmapSaveData customData && eventData is CustomBeatmapSaveData.BasicEventData customEvent)
                {
                    if (customEvent.customData != null)
                    {
                        logOutput.AppendLine($"  ├ Custom Data: {customEvent.customData}");
                    }
                }
            }



            Plugin.Log.Info($"---- SONG NAME: {HarmonyPatches.SongName} LIGHT COLOR EVENT BOX GROUPS ----");
            foreach (var group in beatmapSaveData.lightColorEventBoxGroups)
            {
                Plugin.Log.Info($"Group {group.groupId} at Beat {group.beat}, Events: {group.eventBoxes.Count}");
                foreach (var eventBox in group.eventBoxes)
                {
                    Plugin.Log.Info($"  ├─ LightColorEventBox:");
                    LogIndexFilter(eventBox.indexFilter);

                    Plugin.Log.Info($"  │   ├─ BeatDistParam: {eventBox.beatDistributionParam}, BeatDistType: {eventBox.beatDistributionParamType}");
                    Plugin.Log.Info($"  │   ├─ BrightnessDistParam: {eventBox.brightnessDistributionParam}, BrightnessDistType: {eventBox.brightnessDistributionParamType}");
                    Plugin.Log.Info($"  │   ├─ BrightnessEase: {eventBox.brightnessDistributionEaseType}");

                    foreach (var lightEvent in eventBox.lightColorBaseDataList)
                    {
                        Plugin.Log.Info($"  │   │   ├─ Light Event: Beat {lightEvent.beat}, Transition {lightEvent.transitionType}, Color {lightEvent.colorType}, Brightness {lightEvent.brightness}");
                        Plugin.Log.Info($"  │   │   ├─ StrobeFreq {lightEvent.strobeBeatFrequency}, StrobeBrightness {lightEvent.strobeBrightness}, StrobeFade {lightEvent.strobeFade}");
                    }
                }

            }

            Plugin.Log.Info($"---- SONG NAME: {HarmonyPatches.SongName} LIGHT ROTATION EVENT BOX GROUPS ----");
            foreach (var group in beatmapSaveData.lightRotationEventBoxGroups)
            {
                Plugin.Log.Info($"Group {group.groupId} at Beat {group.beat}, Events: {group.eventBoxes.Count}");
                foreach (var eventBox in group.eventBoxes)
                {
                    Plugin.Log.Info($"  ├─ LightRotationEventBox:");
                    LogIndexFilter(eventBox.indexFilter);

                    Plugin.Log.Info($"  │   ├─ BeatDistParam: {eventBox.beatDistributionParam}, BeatDistType: {eventBox.beatDistributionParamType}");
                    Plugin.Log.Info($"  │   ├─ RotationDistParam: {eventBox.rotationDistributionParam}, RotationDistType: {eventBox.rotationDistributionParamType}, Axis: {eventBox.axis}");
                    Plugin.Log.Info($"  │   ├─ FlipRotation: {eventBox.flipRotation}, RotationEase: {eventBox.rotationDistributionEaseType}");

                    foreach (var rotationEvent in eventBox.lightRotationBaseDataList)
                    {
                        Plugin.Log.Info($"  │   │   ├─ Rotation Event: Beat {rotationEvent.beat}, UsePrev {rotationEvent.usePreviousEventRotationValue}, Ease {rotationEvent.easeType}");
                        Plugin.Log.Info($"  │   │   ├─ Loops {rotationEvent.loopsCount}, Rotation {rotationEvent.rotation}, Direction {rotationEvent.rotationDirection}");
                    }
                }
            }

            Plugin.Log.Info("---- LIGHT TRANSLATION EVENT BOX GROUPS ----");
            foreach (var group in beatmapSaveData.lightTranslationEventBoxGroups)
            {
                Plugin.Log.Info($"Group {group.groupId} at Beat {group.beat}, Events: {group.eventBoxes.Count}");
                foreach (var eventBox in group.eventBoxes)
                {
                    Plugin.Log.Info($"  ├─ LightTranslationEventBox:");
                    LogIndexFilter(eventBox.indexFilter);

                    Plugin.Log.Info($"  │   ├─ BeatDistParam: {eventBox.beatDistributionParam}, BeatDistType: {eventBox.beatDistributionParamType}");
                    Plugin.Log.Info($"  │   ├─ GapDistParam: {eventBox.gapDistributionParam}, GapDistType: {eventBox.gapDistributionParamType}, Axis: {eventBox.axis}");
                    Plugin.Log.Info($"  │   ├─ FlipTranslation: {eventBox.flipTranslation}, TranslationEase: {eventBox.gapDistributionEaseType}");

                    foreach (var translationEvent in eventBox.lightTranslationBaseDataList)
                    {
                        Plugin.Log.Info($"  │   │   ├─ Translation Event: Beat {translationEvent.beat}, UsePrev {translationEvent.usePreviousEventTranslationValue}, Ease {translationEvent.easeType}");
                        Plugin.Log.Info($"  │   │   ├─ Translation: {translationEvent.translation}");
                    }
                }
            }



            // Print log output
            Plugin.Log.Info(logOutput.ToString());
        }
        private static void LogIndexFilter(BeatmapSaveDataVersion3.BeatmapSaveData.IndexFilter indexFilter)
        {
            if (indexFilter == null)
            {
                Plugin.Log.Info($"  │   ├─ IndexFilter: NULL");
                return;
            }

            Plugin.Log.Info($"  │   ├─ IndexFilter:");
            Plugin.Log.Info($"  │   │   ├─ Type: {indexFilter.type}");
            Plugin.Log.Info($"  │   │   ├─ Param0: {indexFilter.param0}, Param1: {indexFilter.param1}");
            Plugin.Log.Info($"  │   │   ├─ Reversed: {indexFilter.reversed}, Random: {indexFilter.random}");
            Plugin.Log.Info($"  │   │   ├─ Seed: {indexFilter.seed}, Chunks: {indexFilter.chunks}");
            Plugin.Log.Info($"  │   │   ├─ Limit: {indexFilter.limit}, LimitAffects: {indexFilter.limitAlsoAffectsType}");
        }

    }
    */

}
