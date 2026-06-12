
using BeatmapSaveDataVersion2_6_0AndEarlier;
using BS_Utils.Gameplay;//BW added to Disable Score submission https://github.com/Kylemc1413/Beat-Saber-Utils 
using CustomJSONData.CustomBeatmap;
using HarmonyLib;
using HMUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;

namespace AutoBS.Patches
{
    // !!!!!!!!!!! This should be in Mapping Extentions not AutoBS!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
    //
    //
    //
    /// <summary>
    /// !May get fixed by Real Mapping Extensions but they may exclude v4 so would still need this. Method to preserve raw obstacle layer values outside the standard range. Starting v1.42, v4 maps clamp obstacle layers from 0 to 4 which breaks v4 mapping extensions.
    /// </summary>
    /// <remarks>This patch allows out-of-range obstacle layer values to be retained. For standard layer values (0 to 4), the original method behavior is preserved.</remarks>
    [HarmonyPatch(typeof(BeatmapTypeConverters), nameof(BeatmapTypeConverters.ConvertObstacleLineLayer))]
    static class Patch_ConvertObstacleLineLayer_PreserveRaw
    {
        static bool Prefix(int layer, ref NoteLineLayer __result)
        {
            if (!Config.Instance.EnablePlugin) return true;
            if (!Config.Instance.PatchV4ObstacleExtensionLayers) return true;

            if (layer >= 0 && layer <= 4)
                return true;

            Plugin.Log.Debug($"[ConvertObstacleLineLayer] preserving raw obstacle layer {layer}");
            __result = (NoteLineLayer)layer;
            return false;
        }
    }
    //
    //
    //
    // !!!!!!!!!!! This should be in Mapping Extentions not AutoBS!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!


    #region Prefix - BeatmapDataLoader.LoadBeatmapDataAsync - adds the beatmapData (IReadonlyBeatmapData)

    // This works great, but required mods like Noodle and Chroma will not activate on the gen 360 map. It uses a unique BeatmapKey so scoring works.
    // Set Content only creates metaData for the map, not the actual notes and obstacles.
    // this adds the beatmapData (IReadonlyBeatmapData) with actual notes etc to the map and hands it off to CreateTransformedBeatmapData

    // v1.42 distinct BeatmapKey for scoring, but uses the basedOnKey to pass the original beatmapData to CreateTransformedBeatmapData. No longer need to send it stored beatmapData from a registry. great for built-in since i can't get those maps anymore with my previous technique
    [HarmonyPatch(typeof(BeatmapDataLoader), nameof(BeatmapDataLoader.LoadBeatmapDataAsync))]
    static class Patch_BeatmapDataLoader_LoadAsync
    {
        [ThreadStatic] private static bool _reentry;

        static bool Prefix(
            BeatmapDataLoader __instance,
            IBeatmapLevelData beatmapLevelData,
            BeatmapKey beatmapKey,
            float startBpm,
            bool loadingForDesignatedEnvironment,
            IEnvironmentInfo targetEnvironmentInfo,
            IEnvironmentInfo originalEnvironmentInfo,
            BeatmapLevelDataVersion beatmapLevelDataVersion,
            GameplayModifiers gameplayModifiers,
            PlayerSpecificSettings playerSpecificSettings,
            bool enableBeatmapDataCaching,
            ref Task<IReadonlyBeatmapData> __result)
        {
            if (!Config.Instance.EnablePlugin) return true;
            if (!Utils.IsEnabledForGeneralFeatures()) return true;

            // avoid previews etc
            if (!loadingForDesignatedEnvironment)
                return true;

            // IMPORTANT: allow original method to run when we re-enter
            if (_reentry)
                return true;

            // Only intercept generated modes
            bool isGen360 = beatmapKey.beatmapCharacteristic.serializedName == GameModeHelper.GENERATED_360DEGREE_MODE;

            if (!isGen360)
                return true;

            // Map generated -> basedOn
            if (!SetContent.GeneratedToStandardKey.TryGetValue(beatmapKey.SerializedName(), out var basedOnKey))
            {
                Plugin.Log.Error($"[LoadBeatmapDataAsync] Missing generated→standard mapping for {beatmapKey.beatmapCharacteristic.serializedName} - {beatmapKey.difficulty}");
                return true;
            }

            Plugin.LogDebug($"[LoadBeatmapDataAsync] Generated requested. Loading based-on: {basedOnKey.beatmapCharacteristic.serializedName} - {basedOnKey.difficulty}");

            _reentry = true;
            try
            {
                // call the real method (we'll hit Prefix again, but _reentry makes it return true)
                var baseTask = __instance.LoadBeatmapDataAsync(
                    beatmapLevelData,
                    basedOnKey,
                    startBpm,
                    loadingForDesignatedEnvironment,
                    targetEnvironmentInfo,
                    originalEnvironmentInfo,
                    beatmapLevelDataVersion,
                    gameplayModifiers,
                    playerSpecificSettings,
                    enableBeatmapDataCaching
                );

                __result = baseTask.ContinueWith<IReadonlyBeatmapData>(t =>
                {
                    if (t.IsFaulted)
                    {
                        Plugin.Log.Error($"[LoadBeatmapDataAsync] Base load faulted for {basedOnKey.beatmapCharacteristic.serializedName} - {basedOnKey.difficulty}: {t.Exception}");
                        // rethrow the underlying exception so Beat Saber behaves normally
                        throw t.Exception ?? new Exception("Base load faulted");
                    }

                    if (t.IsCanceled)
                        throw new TaskCanceledException($"Base load canceled for {basedOnKey.beatmapCharacteristic.serializedName} - {basedOnKey.difficulty}");

                    var baseData = t.Result;
                    if (baseData == null)
                        return null;

                    // Your transformation: take baseData (standard) and produce generated data
                    var transformed = TransformPipeline(baseData, beatmapKey, basedOnKey, gameplayModifiers, playerSpecificSettings);

                    return transformed ?? baseData;
                }, TaskScheduler.Default);

                return false; // we replaced the async result
            }
            finally
            {
                _reentry = false;
            }
        }

        private static IReadonlyBeatmapData TransformPipeline(
            IReadonlyBeatmapData baseData,
            BeatmapKey generatedKey,
            BeatmapKey basedOnKey,
            GameplayModifiers gameplayModifiers,
            PlayerSpecificSettings pss)
        {
            // implement: CreateTransformedBeatmapData / gen360 rotations / etc.
            // MUST return IReadonlyBeatmapData.
            return baseData;
        }
    }


    #endregion


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
    
    // JDFixer uses this method so that the user can update the MaxJNS over and over. i tried it in LevelUpdatePatcher. it works but can only be updated before play song one time https://github.com/zeph-yr/JDFixer/blob/b51c659def0e9cefb9e0893b19647bb9d97ee9ae/StandardLevelDetailViewPatch.cs
    // note jump offset determines how far away notes spawn from you. A negative modifier means notes will spawn closer to you, and a positive modifier means notes will spawn further away
    [HarmonyPatch(typeof(BeatmapObjectSpawnMovementData), nameof(BeatmapObjectSpawnMovementData.Init))]
    [HarmonyPatch(new Type[] { typeof(int), typeof(IJumpOffsetYProvider), typeof(Vector3) })]
    internal class SpawnMovementDataUpdatePatch
    {
        internal static void Prefix(int noteLinesCount, IJumpOffsetYProvider jumpOffsetYProvider, Vector3 rightVec)
        {
            if (!Config.Instance.EnablePlugin) return;
            if (!Utils.IsEnabledLighting()) return;

            if (Config.Instance.BigLasers &&
                (TransitionPatcher.SelectedSerializedName == "Generated360Degree" ||
                 TransitionPatcher.SelectedSerializedName == "360Degree" ||
                 TransitionPatcher.SelectedSerializedName == "90Degree")) // only do this for 360 or else it will do this for all maps
            {
                BigLasers();
            }

        }

        // Used plugin.cs zenject installer in order to access ParametricBoxController() method. it seems to control lasers.but some unknown method calls it. so instead i scaled the gameObject that uses ParametricBoxController()
        public static void BigLasers()
        {
            // Get all ParametricBoxController objects in the scene
            //Environment>TopLaser>BoxLight, Environment>DownLaser>BoxLight, Environment/RotatingLaser/Pair/BaseR or BaseL/Laser/BoxLight
            ParametricBoxController[] boxControllers =
                GameObject.FindObjectsByType<ParametricBoxController>(FindObjectsSortMode.None);

            int i = 1;
            foreach (ParametricBoxController boxController in boxControllers)
            {
                Transform parentTransform = boxController.gameObject.transform.parent;//scale gameObject parent
                Vector3 currentScale = parentTransform.localScale;

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

            Plugin.LogDebug($"[SetContentForBeatmapData] Called for level: {level?.levelID}");

            if (level == null)
                return true; // fallback to vanilla

            // Access characteristic/difficulty controls
            var charCtrl = (BeatmapCharacteristicSegmentedControlController)
                typeof(StandardLevelDetailView).GetField("_beatmapCharacteristicSegmentedControlController", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(__instance);

            var diffCtrl = (BeatmapDifficultySegmentedControlController)
                typeof(StandardLevelDetailView).GetField("_beatmapDifficultySegmentedControlController", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(__instance);

            var selectedCharacteristic = charCtrl.selectedBeatmapCharacteristic;
            var selectedDifficulty = diffCtrl.selectedDifficulty;

            var key = new BeatmapKey(level.levelID, selectedCharacteristic, selectedDifficulty);

            if (selectedCharacteristic.serializedName == GameModeHelper.GENERATED_360DEGREE_MODE)
            {
                // Try to get stats from your registry
                var stats = MenuDataRegistry.GetStatsForKey(key);

                if (stats != null)
                {
                    // Set UI with your stats
                    var panelField = typeof(StandardLevelDetailView).GetField("_levelParamsPanel", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    var canvasGroupField = typeof(StandardLevelDetailView).GetField("_levelParamsPanelCanvasGroup", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                    var panel = (LevelParamsPanel)panelField.GetValue(__instance);
                    var canvasGroup = (CanvasGroup)canvasGroupField.GetValue(__instance);

                    canvasGroup.alpha = 1f; // Un-fade the panel

                    panel.notesCount = stats.notesCount;
                    panel.obstaclesCount = stats.obstaclesCount;
                    panel.bombsCount = stats.bombsCount;
                    panel.notesPerSecond = stats.notesPerSecond;

                    Plugin.LogDebug($"[SetContentForBeatmapData] Updated UI for Gen360 - Notes: {stats.notesCount}, Obstacles: {stats.obstaclesCount}, Bombs: {stats.bombsCount}, NPS: {stats.notesPerSecond}");

                    return false; // Prevent vanilla from overwriting your stats/UI
                }
                // If not found, let vanilla run and fallback to normal
            }

            Plugin.LogDebug("[SetContentForBeatmapData] Not a Gen360 map or stats not found, using vanilla UI.");

            return true; // Not Gen360: let vanilla code handle everything else
        }
    }
    #endregion

}
