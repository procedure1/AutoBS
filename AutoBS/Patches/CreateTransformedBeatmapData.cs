using BeatmapSaveDataVersion2_6_0AndEarlier;
using BeatmapSaveDataVersion3;
using BeatmapSaveDataVersion4;
using BS_Utils.Gameplay;
using CustomJSONData.CustomBeatmap;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.Linq;
using UnityEngine;

namespace AutoBS.Patches
{
    // Postfix MAIN CODE!!!! generates map changes and alters the beat map data such as rotation events and arcs and chains...
    // runs after SetContent & TransitionPatcher & BeatmapDataLoader.LoadBeatmapDataAsync
    // This runs automatically after user hits Play
    [HarmonyPatch(typeof(BeatmapDataTransformHelper), "CreateTransformedBeatmapData")]
    public class BeatmapDataTransformHelperPatcher
    {
        public static List<CustomSliderData> arcsAndChains = new List<CustomSliderData>();

        public static bool NoodleProblemNotes = false;
        public static bool NoodleProblemObstacles = false;

        public struct Settings { };

        [HarmonyPostfix]
        [HarmonyPriority(Priority.Last)]//need this since want this to run after mods like DiffReducer since you don't want rotation events created on notes that end up deleted

        static void Postfix(
            IReadonlyBeatmapData beatmapData,
            ref IReadonlyBeatmapData __result, // this is set by LoadBeatmapDataAsync
            BeatmapLevel beatmapLevel,
            GameplayModifiers gameplayModifiers,
            bool leftHanded,
            EnvironmentEffectsFilterPreset environmentEffectsFilterPreset,
            EnvironmentIntensityReductionOptions environmentIntensityReductionOptions,
            in Settings settings)
        {
            if (!Config.Instance.EnablePlugin) return;
            if (!Utils.IsEnabledForGeneralFeatures()) return;

            WallGenerator.ResetAlteredState();

            IReadonlyBeatmapData scoreBaseline = __result;

            EditableCBD eData = null;

            if (beatmapData is CustomBeatmapData cbd) // custom map data
            {
                RotationV3Registry.RotationEventsByKey.TryGetValue(TransitionPatcher.CurrentPlayKey, out var v3RotList);

                Plugin.Log.Info($"[CreateTransformedBeatmapData] Retrieved CustomBeatmapData from JSON v{cbd.version.Major} (major version): " +
                        $"{cbd.cuttableNotesCount} notes, " +
                        $"{cbd.bombsCount} bombs, " +
                        $"{cbd.obstaclesCount} obstacles, " +
                        $"{cbd.allBeatmapDataItems.OfType<CustomSliderData>().Where((e) => e.sliderType == CustomSliderData.Type.Normal).Count()} Arcs, " +
                        $"{cbd.allBeatmapDataItems.OfType<CustomSliderData>().Where((e) => e.sliderType == CustomSliderData.Type.Burst).Count()} Chains, " +
                        $"{v3RotList?.Count()} Rotation Events (V3 SaveData), " +
                        $"{cbd.allBeatmapDataItems.OfType<CustomBasicBeatmapEventData>().Count()} Basic Events, " +
                        $"{cbd.allBeatmapDataItems.OfType<CustomBasicBeatmapEventData>().Where((e) => e.basicBeatmapEventType == BasicBeatmapEventType.Event14 || e.basicBeatmapEventType == BasicBeatmapEventType.Event15).Count()} Basic Rotation Events, " +
                        $"{cbd.allBeatmapDataItems.OfType<CustomEventData>().Count()} Events, " +
                        $"{cbd.allBeatmapDataItems.OfType<CustomColorBoostBeatmapEventData>().Count()} Color Boosts, " + //v2 basic events end up here somehow automatically
                        $"{cbd.allBeatmapDataItems.OfType<CustomBPMChangeBeatmapEventData>().Count()} Bpm Change Events");

                eData = new EditableCBD(cbd);
            }
            else if (beatmapData is BeatmapData bm) // built-in map data
            {
                Plugin.Log.Info($"[CreateTransformedBeatmapData] Retrieved Vanilla BeatmapData from JSON v{TransitionPatcher.CurrentBeatmapVersion}: " +
                         $"{bm.cuttableNotesCount} notes, " +
                         $"{bm.bombsCount} bombs, " +
                         $"{bm.obstaclesCount} obstacles, " +
                         $"{bm.allBeatmapDataItems.OfType<SliderData>().Where((e) => e.sliderType == SliderData.Type.Normal).Count()} Arcs, " +
                         $"{bm.allBeatmapDataItems.OfType<SliderData>().Where((e) => e.sliderType == SliderData.Type.Burst).Count()} Chains, " +
                         //$" No 'Rotation Events' are available for v4, " +
                         $"{bm.allBeatmapDataItems.OfType<BasicBeatmapEventData>().Count()} Basic Events, " +
                         $"{bm.allBeatmapDataItems.OfType<BasicBeatmapEventData>().Where((e) => e.basicBeatmapEventType == BasicBeatmapEventType.Event14 || e.basicBeatmapEventType == BasicBeatmapEventType.Event15).Count()} Basic Rotation Events, " +
                         $"{bm.allBeatmapDataItems.OfType<EventData>().Count()} Events, " +
                         $"{bm.allBeatmapDataItems.OfType<ColorBoostBeatmapEventData>().Count()} Color Boosts, " +
                         $"{bm.allBeatmapDataItems.OfType<BpmChangeEventData>().Count()} Bpm Change Events, " +
                         $"{bm.allBeatmapDataItems.OfType<BpmChangeEventData>().Count()} Bpm Change Events, " +
                         $"{bm.allBeatmapDataItems.OfType<NoteJumpSpeedEventData>().Count()} NJS Events");

                Version version = BeatmapDataRegistry.versionByKey.TryGetValue(TransitionPatcher.CurrentPlayKey, out Version foundVersion) ? foundVersion : new Version(4, 0, 0); // 1.40.8 firestarter song was 4.0.0
                eData = new EditableCBD(bm, version);
            }
            /*
#if DEBUG
            foreach (var rot in eData.RotationEvents)
            {
                if (rot.time < 30)
                    Plugin.Log.Info($"1 Rotation - Time: {rot.time} - Rotation: {rot.rotation} - Total Rotation: {rot.accumRotation}");
            }
#endif
            */
            Plugin.LogDebug($"[CreateTransformedBeatmapData] Converted (Custom)BeatmapData to EditableCBD map version: {eData.Version.Major} - notes: {eData.ColorNotes.Count}, bombs: {eData.BombNotes.Count}, obstacles: {eData.Obstacles.Count}, arcs: {eData.Arcs.Count}, chains: {eData.Chains.Count}, rotations: {eData.RotationEvents.Count}, basic events: {eData.BasicEvents.Count}, customEvents: {eData.CustomEvents.Count}, color boosts: {eData.ColorBoostEvents.Count}.");

            Plugin.LogDebug($"[CreateTransformedBeatmapData] Song Name: {SetContent.SongName} - v{TransitionPatcher.CurrentBeatmapVersion} - {TransitionPatcher.SelectedSerializedName} {TransitionPatcher.SelectedDifficulty}  ----------------------------------------------------------------------------");

            (NoodleProblemNotes, NoodleProblemObstacles) = EditableCBD.TestForNoodleCustomData(eData); //Should remove notes and walls first before figuring out rotations etc which are based on notes


            if (TransitionPatcher.IsBeatSageMap && Config.Instance.EnableCleanBeatSage)
            {
                BeatSageCleanUp.Clean(eData); // reference sent so no need to return eData
            }

            bool arcsAdded = false; bool chainsAdded = false;

            if (Utils.IsEnabledArcs() || Utils.IsEnabledChains())
            {
                if ((eData.Arcs.Count == 0 || eData.Chains.Count == 0) && !NoodleProblemNotes)
                {
                    int originalArcCount = 0; int finalArcCount = 0; int originalChainCount = 0; int finalChainCount = 0;

                    (originalArcCount, finalArcCount, originalChainCount, finalChainCount) = Arcitect.CreateSliders(eData); //update data and update sliders list for arcfix later

                    arcsAdded = Utils.IsEnabledArcs() && originalArcCount == 0 && finalArcCount > 0;
                    chainsAdded = Utils.IsEnabledChains() && originalChainCount == 0 && finalChainCount > 0;
                }
                else
                {
                    Plugin.LogDebug("[CreateTransformedBeatmapData] Arcitect OFF. Native Arcs and Chains already exists.");
                }

                if (Utils.IsEnabledArcs() && // go ahead and arc fix nonGen360 maps if arcs are enabled.
                    !TransitionPatcher.MapAlreadyUsesArcs && // unless they already use arcs. if they exist in 360 then they are probably placed correctly.
                    Config.Instance.ArcFixFull &&
                   (TransitionPatcher.SelectedSerializedName == "360Degree" || TransitionPatcher.SelectedSerializedName == "90Degree"))
                {
                    List<ERotationEventData> allRotations = eData.RotationEvents;

                    Plugin.LogDebug($"[CreateTransformedBeatmapData] Arcitect ArcFix for NonGen360 Maps Activated. Original Rotations Count: {allRotations.Count}");
                    eData.RotationEvents = Arcitect.ArcFix(allRotations, eData);
                }
            }

            #region LightAutoMapper

            if (Utils.IsEnabledLighting() && Config.Instance.EnableLightAutoMapper)
            {
                LightAutoMapper.Start(eData);
            }

            #endregion

            string scoreDisableReason = "";
            string myDisabledReason = "";

            if ((Utils.IsEnabledArcs() ||
                Utils.IsEnabledChains() ||
                Utils.IsEnabledWalls() ||
                (Utils.IsEnabledLighting() &&
                Config.Instance.BoostLighting) ||
                (TransitionPatcher.IsBeatSageMap && Config.Instance.EnableCleanBeatSage) ||
                TransitionPatcher.SelectedSerializedName == GameModeHelper.GENERATED_360DEGREE_MODE))
            {
                Plugin.LogDebug($"[CreateTransformedBeatmapData] Generator Called. Generating map changes for {TransitionPatcher.SelectedSerializedName}...");

                Generator gen = new Generator
                {
                    RotationSpeedMultiplier = (float)Math.Round(Config.Instance.RotationSpeedMultiplier, 1),
                    AllowCrouchWalls = Config.Instance.AllowCrouchWalls,
                    AllowLeanWalls = Config.Instance.AllowLeanWalls,
                };

                if (TransitionPatcher.SelectedSerializedName == GameModeHelper.GENERATED_360DEGREE_MODE)
                {
                    Plugin.LogDebug($"[CreateTransformedBeatmapData] Generating rotation events for {TransitionPatcher.SelectedSerializedName}...");

                    if (Config.Instance.Wireless360)
                    {
                        gen.LimitRotations = 99999;
                        gen.BottleneckRotations = 99999;
                    }
                    else
                    {
                        gen.LimitRotations =
                            (int)((Config.Instance.LimitRotations360 / 360f / 2f) * (24f)); // / Config.Instance.RotationAngleMultiplier));//BW this convert the angle into LimitRotation units of 15 degree slices. Need to divide the Multiplier since it causes the angle to change from 15 degrees. this will keep the desired limit to work if a multiplier is added.
                        gen.BottleneckRotations = gen.LimitRotations / 2;
                    }
                }

                // Noodle events still exist in eData up to this point.
                var outp = gen.Generate(eData, beatmapLevel.beatsPerMinute);

                if (!gen.OriginalMapAltered)
                {
                    Plugin.LogDebug("[CreateTransformedBeatmapData] Generator found no map changes.");
                }
                else
                {
                    if (outp.IsCustom)
                    {
                        __result = outp.Custom!;

                        Plugin.Log.Info($"[CreateTransformedBeatmapData] Final CustomBeatmapData: " +
                             $"{__result.cuttableNotesCount} notes, " +
                             $"{__result.bombsCount} bombs, " +
                             $"{__result.obstaclesCount} obstacles, " +
                             $"{__result.allBeatmapDataItems.OfType<CustomSliderData>().Where((e) => e.sliderType == CustomSliderData.Type.Normal).Count()} Arcs, " +
                             $"{__result.allBeatmapDataItems.OfType<CustomSliderData>().Where((e) => e.sliderType == CustomSliderData.Type.Burst).Count()} Chains, " +
                             $"{__result.allBeatmapDataItems.OfType<CustomBasicBeatmapEventData>().Count()} Basic Events, " +
                             $"{__result.allBeatmapDataItems.OfType<CustomEventData>().Count()} Events, " +
                             $"{__result.allBeatmapDataItems.OfType<CustomColorBoostBeatmapEventData>().Count()} Color Boosts, " +
                             $"{__result.allBeatmapDataItems.OfType<CustomBPMChangeBeatmapEventData>().Count()} Bpm Change Events, " +
                             $"{eData.RotationEvents.Count} Rotation Events (in-line per object)");
                        // v4 unsupported by customJsonData - $"{__result.allBeatmapDataItems.OfType<NoteJumpSpeedEventData>().Count()} NJS Events");

                        JsonOutputConverter.ToJsonFile(__result as CustomBeatmapData, eData);

                    }
                    else
                    {
                        __result = outp.Vanilla!;

                        Plugin.Log.Info($"[CreateTransformedBeatmapData] Final Vanilla BeatmapData v{TransitionPatcher.CurrentBeatmapVersion}: " +
                             $"{__result.cuttableNotesCount} notes, " +
                             $"{__result.bombsCount} bombs, " +
                             $"{__result.obstaclesCount} obstacles, " +
                             $"{__result.allBeatmapDataItems.OfType<SliderData>().Where((e) => e.sliderType == SliderData.Type.Normal).Count()} Arcs, " +
                             $"{__result.allBeatmapDataItems.OfType<SliderData>().Where((e) => e.sliderType == SliderData.Type.Burst).Count()} Chains, " +
                             $"{__result.allBeatmapDataItems.OfType<BasicBeatmapEventData>().Count()} Basic Events, " +
                             $"{__result.allBeatmapDataItems.OfType<EventData>().Count()} Events, " +
                             $"{__result.allBeatmapDataItems.OfType<ColorBoostBeatmapEventData>().Count()} Color Boosts, " +
                             $"{__result.allBeatmapDataItems.OfType<BpmChangeEventData>().Count()} Bpm Change Events, " +
                             $"{__result.allBeatmapDataItems.OfType<NoteJumpSpeedEventData>().Count()} NJS Events, " +
                             $"{eData.RotationEvents.Count} Rotation Events (in-line per object)");
                    }

                    myDisabledReason = TransitionPatcher.DetermineScoreSubmissionReason(arcsAdded, chainsAdded);


                    if (!string.IsNullOrEmpty(myDisabledReason))
                    {
                        Plugin.Log.Info($"[CreateTransformedBeatmapData] Final Score Disable Reason (1st pass): {myDisabledReason}");
                        ScoreGate.Disable(myDisabledReason);
                    }
                    else if (ScoreSensitiveBeatmapComparer.TryGetScoreDisableReason(
                        scoreBaseline,
                        __result,
                        out scoreDisableReason))
                    {
                        Plugin.Log.Info($"[CreateTransformedBeatmapData] Final Score Disable Reason (2nd stringent pass): {scoreDisableReason}");
                        ScoreGate.Disable("AutoBS");
                    }
                    else
                    {
                        Plugin.LogDebug("[CreateTransformedBeatmapData] No beatmap changes affecting score found. Score submission should be allowed unless AutoNJSFixer is enabled.");
                    }
                }

                //Plugin.LogDebug($"[CreateTransformedBeatmapData] Final Lane Rotations in Notes from Data (represents the first note found with a new rotation value - Wireless360: {Config.Instance.Wireless360} - LimitRotations360: {Config.Instance.LimitRotations360}):");

            }
            if (string.IsNullOrEmpty(myDisabledReason) && string.IsNullOrEmpty(scoreDisableReason))
            { 
                bool autoNjsChangedWholeMap =
                    Utils.IsEnabledAutoNjsFixer() &&
                    !TransitionPatcher.AutoNJSDisabledByConflictingMod &&
                    Mathf.Abs(
                        TransitionPatcher.OriginalNoteJumpMovementSpeed -
                        TransitionPatcher.FinalNoteJumpMovementSpeed
                    ) > 0.0001f;

                if (autoNjsChangedWholeMap)
                {
                    Plugin.Log.Info("[CreateTransformedBeatmapData] Final Score Disable Reason (3rd pass): 'AutoBS—Auto NJS Fixer' only.");
                    ScoreGate.Disable("AutoBS—Auto NJS Fixer");
                }
            }
            //BeatmapLightingLogger.LogGLSLightingEvents(HarmonyPatches.CurrentBeatmapSaveData);
        }
    }

    internal static class ScoreSensitiveBeatmapComparer
    {
        public static bool TryGetScoreDisableReason(
            IReadonlyBeatmapData original,
            IReadonlyBeatmapData final,
            out string reason)
        {
            if (!SameNotes(original, final))
            {
                reason = "NoteData changed";
                return true;
            }

            if (!SameObstacles(original, final))
            {
                reason = "ObstacleData changed";
                return true;
            }

            if (!SameSliders(original, final, SliderData.Type.Normal))
            {
                reason = "SliderData Arcs changed";
                return true;
            }

            if (!SameSliders(original, final, SliderData.Type.Burst))
            {
                reason = "SliderData Chains changed";
                return true;
            }

            if (!SameWaypoints(original, final))
            {
                reason = "WaypointData changed";
                return true;
            }

            if (!SameRotations(original, final))
            {
                reason = "Rotation events changed";
                return true;
            }

            if (!SameBpmEvents(original, final))
            {
                reason = "BPM events changed";
                return true;
            }

            if (!SameNjsEvents(original, final))
            {
                reason = "NJS events changed";
                return true;
            }
            if (!SameOtherBeatmapObjects(original, final))
            {
                reason = "Other beatmap objects changed";
                return true;
            }



            reason = "";
            return false;
        }

        private static bool SameNotes(IReadonlyBeatmapData a, IReadonlyBeatmapData b)
        {
            var x = a.allBeatmapDataItems.OfType<NoteData>()
                .OrderBy(n => n.time)
                .ThenBy(n => n.gameplayType)
                .ThenBy(n => n.colorType)
                .ThenBy(n => n.lineIndex)
                .ThenBy(n => n.noteLineLayer)
                .ThenBy(n => n.cutDirection)
                .ToList();

            var y = b.allBeatmapDataItems.OfType<NoteData>()
                .OrderBy(n => n.time)
                .ThenBy(n => n.gameplayType)
                .ThenBy(n => n.colorType)
                .ThenBy(n => n.lineIndex)
                .ThenBy(n => n.noteLineLayer)
                .ThenBy(n => n.cutDirection)
                .ToList();

            if (x.Count != y.Count) return false;

            for (int i = 0; i < x.Count; i++)
            {
                if (!Same(x[i].time, y[i].time)) return false;
                if (x[i].gameplayType != y[i].gameplayType) return false;
                if (x[i].scoringType != y[i].scoringType) return false;
                if (x[i].colorType != y[i].colorType) return false;
                if (x[i].lineIndex != y[i].lineIndex) return false;
                if (x[i].noteLineLayer != y[i].noteLineLayer) return false;
                if (x[i].cutDirection != y[i].cutDirection) return false;
                if (x[i].rotation != y[i].rotation) return false;
            }

            return true;
        }

        private static bool SameObstacles(IReadonlyBeatmapData a, IReadonlyBeatmapData b)
        {
            var x = a.allBeatmapDataItems.OfType<ObstacleData>()
                .OrderBy(o => o.time)
                .ThenBy(o => o.lineIndex)
                .ThenBy(o => o.lineLayer)
                .ThenBy(o => o.duration)
                .ThenBy(o => o.width)
                .ThenBy(o => o.height)
                .ThenBy(o => o.rotation)
                .ToList();

            var y = b.allBeatmapDataItems.OfType<ObstacleData>()
                .OrderBy(o => o.time)
                .ThenBy(o => o.lineIndex)
                .ThenBy(o => o.lineLayer)
                .ThenBy(o => o.duration)
                .ThenBy(o => o.width)
                .ThenBy(o => o.height)
                .ThenBy(o => o.rotation)
                .ToList();

            if (x.Count != y.Count)
            {
                Plugin.LogDebug($"[ScoreCompare][Obstacle] Count mismatch: original {x.Count} obstacles vs final {y.Count} obstacles");
                return false;
            }

            for (int i = 0; i < x.Count; i++)
            {
                if (!Same(x[i].time, y[i].time))
                {
                    LogObstacleMismatch(i, x[i], y[i], "time");
                    return false;
                }

                if (x[i].lineIndex != y[i].lineIndex)
                {
                    LogObstacleMismatch(i, x[i], y[i], "lineIndex");
                    return false;
                }

                if (x[i].lineLayer != y[i].lineLayer)
                {
                    LogObstacleMismatch(i, x[i], y[i], "lineLayer");
                    return false;
                }

                if (!Same(x[i].duration, y[i].duration))
                {
                    LogObstacleMismatch(i, x[i], y[i], "duration");
                    return false;
                }

                if (x[i].width != y[i].width)
                {
                    LogObstacleMismatch(i, x[i], y[i], "width");
                    return false;
                }

                if (x[i].height != y[i].height)
                {
                    LogObstacleMismatch(i, x[i], y[i], "height");
                    return false;
                }

                if (x[i].rotation != y[i].rotation)
                {
                    LogObstacleMismatch(i, x[i], y[i], "rotation");
                    return false;
                }
            }

            return true;
        }
        private static void LogObstacleMismatch(int index, ObstacleData original, ObstacleData final, string field)
        {
            Plugin.LogDebug(
                $"[ScoreCompare][Obstacle] {field} mismatch at index {index}: " +
                $"original t={original.time:F4} line={original.lineIndex} layer={original.lineLayer} dur={original.duration:F4} w={original.width} h={original.height} rot={original.rotation}; " +
                $"final t={final.time:F4} line={final.lineIndex} layer={final.lineLayer} dur={final.duration:F4} w={final.width} h={final.height} rot={final.rotation}");
        }
        private static bool SameSliders(IReadonlyBeatmapData a, IReadonlyBeatmapData b, SliderData.Type sliderType)
        {
            var x = a.allBeatmapDataItems.OfType<SliderData>()
                .Where(s => s.sliderType == sliderType)
                .OrderBy(s => s.time)
                .ThenBy(s => s.colorType)
                .ThenBy(s => s.headLineIndex)
                .ThenBy(s => s.headLineLayer)
                .ThenBy(s => s.tailTime)
                .ThenBy(s => s.tailLineIndex)
                .ThenBy(s => s.tailLineLayer)
                .ToList();

            var y = b.allBeatmapDataItems.OfType<SliderData>()
                .Where(s => s.sliderType == sliderType)
                .OrderBy(s => s.time)
                .ThenBy(s => s.colorType)
                .ThenBy(s => s.headLineIndex)
                .ThenBy(s => s.headLineLayer)
                .ThenBy(s => s.tailTime)
                .ThenBy(s => s.tailLineIndex)
                .ThenBy(s => s.tailLineLayer)
                .ToList();

            if (x.Count != y.Count) return false;

            for (int i = 0; i < x.Count; i++)
            {
                if (!Same(x[i].time, y[i].time)) return false;
                if (x[i].hasHeadNote != y[i].hasHeadNote) return false;
                if (x[i].colorType != y[i].colorType) return false;
                if (x[i].headLineIndex != y[i].headLineIndex) return false;
                if (x[i].headLineLayer != y[i].headLineLayer) return false;
                if (x[i].headCutDirection != y[i].headCutDirection) return false;
                if (!Same(x[i].tailTime, y[i].tailTime)) return false;
                if (x[i].hasTailNote != y[i].hasTailNote) return false;
                if (x[i].tailLineIndex != y[i].tailLineIndex) return false;
                if (x[i].tailLineLayer != y[i].tailLineLayer) return false;
                if (x[i].tailCutDirection != y[i].tailCutDirection) return false;
                if (x[i].midAnchorMode != y[i].midAnchorMode) return false;
                if (x[i].sliceCount != y[i].sliceCount) return false;
                if (!Same(x[i].squishAmount, y[i].squishAmount)) return false;
            }

            return true;
        }

        private static bool SameWaypoints(IReadonlyBeatmapData a, IReadonlyBeatmapData b)
        {
            var x = a.allBeatmapDataItems.OfType<WaypointData>()
                .OrderBy(w => w.time)
                .ThenBy(w => w.lineIndex)
                .ThenBy(w => w.lineLayer)
                .ThenBy(w => w.offsetDirection)
                .ToList();

            var y = b.allBeatmapDataItems.OfType<WaypointData>()
                .OrderBy(w => w.time)
                .ThenBy(w => w.lineIndex)
                .ThenBy(w => w.lineLayer)
                .ThenBy(w => w.offsetDirection)
                .ToList();

            if (x.Count != y.Count) return false;

            for (int i = 0; i < x.Count; i++)
            {
                if (!Same(x[i].time, y[i].time)) return false;
                if (x[i].lineIndex != y[i].lineIndex) return false;
                if (x[i].lineLayer != y[i].lineLayer) return false;
                if (x[i].offsetDirection != y[i].offsetDirection) return false;
            }

            return true;
        }

        private static bool SameRotations(IReadonlyBeatmapData a, IReadonlyBeatmapData b)
        {
            var x = a.allBeatmapDataItems.OfType<BasicBeatmapEventData>()
                .Where(e => e.basicBeatmapEventType == BasicBeatmapEventType.Event14 ||
                            e.basicBeatmapEventType == BasicBeatmapEventType.Event15)
                .OrderBy(e => e.time)
                .ThenBy(e => e.basicBeatmapEventType)
                .ThenBy(e => e.value)
                .ToList();

            var y = b.allBeatmapDataItems.OfType<BasicBeatmapEventData>()
                .Where(e => e.basicBeatmapEventType == BasicBeatmapEventType.Event14 ||
                            e.basicBeatmapEventType == BasicBeatmapEventType.Event15)
                .OrderBy(e => e.time)
                .ThenBy(e => e.basicBeatmapEventType)
                .ThenBy(e => e.value)
                .ToList();

            if (x.Count != y.Count) return false;

            for (int i = 0; i < x.Count; i++)
            {
                if (!Same(x[i].time, y[i].time)) return false;
                if (x[i].basicBeatmapEventType != y[i].basicBeatmapEventType) return false;
                if (x[i].value != y[i].value) return false;
            }

            return true;
        }

        private static bool SameBpmEvents(IReadonlyBeatmapData a, IReadonlyBeatmapData b)
        {
            var x = a.allBeatmapDataItems.OfType<BpmChangeEventData>()
                .OrderBy(e => e.beat)
                .ThenBy(e => e.bpm)
                .ToList();

            var y = b.allBeatmapDataItems.OfType<BpmChangeEventData>()
                .OrderBy(e => e.beat)
                .ThenBy(e => e.bpm)
                .ToList();

            if (x.Count != y.Count) return false;

            for (int i = 0; i < x.Count; i++)
            {
                if (!Same(x[i].beat, y[i].beat)) return false;
                if (!Same(x[i].bpm, y[i].bpm)) return false;
            }

            return true;
        }

        private static bool SameNjsEvents(IReadonlyBeatmapData a, IReadonlyBeatmapData b)
        {
            var x = a.allBeatmapDataItems.OfType<NoteJumpSpeedEventData>()
                .OrderBy(e => e.time)
                .ThenBy(e => e.relativeNoteJumpSpeed)
                .ToList();

            var y = b.allBeatmapDataItems.OfType<NoteJumpSpeedEventData>()
                .OrderBy(e => e.time)
                .ThenBy(e => e.relativeNoteJumpSpeed)
                .ToList();

            if (x.Count != y.Count) return false;

            for (int i = 0; i < x.Count; i++)
            {
                if (!Same(x[i].time, y[i].time)) return false;
                if (!Same(x[i].relativeNoteJumpSpeed, y[i].relativeNoteJumpSpeed)) return false;
            }

            return true;
        }
        private static bool SameOtherBeatmapObjects(IReadonlyBeatmapData a, IReadonlyBeatmapData b)
        {
            var x = a.allBeatmapDataItems.OfType<BeatmapObjectData>()
                .Where(o => !(o is NoteData)
                         && !(o is ObstacleData)
                         && !(o is SliderData))
                .OrderBy(o => o.GetType().FullName)
                .ThenBy(o => o.time)
                .ToList();

            var y = b.allBeatmapDataItems.OfType<BeatmapObjectData>()
                .Where(o => !(o is NoteData)
                         && !(o is ObstacleData)
                         && !(o is SliderData))
                .OrderBy(o => o.GetType().FullName)
                .ThenBy(o => o.time)
                .ToList();

            if (x.Count != y.Count) return false;

            for (int i = 0; i < x.Count; i++)
            {
                if (x[i].GetType() != y[i].GetType()) return false;
                if (!Same(x[i].time, y[i].time)) return false;
            }

            return true;
        }

        private static bool Same(float a, float b)
        {
            return Mathf.Abs(a - b) <= 0.0001f;
        }
    }
}
