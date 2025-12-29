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

            EditableCBD eData = null;

            if (beatmapData is CustomBeatmapData cbd) // custom map data
            {
                RotationV3Registry.RotationEventsByKey.TryGetValue(TransitionPatcher.SelectedPlayKey, out var v3RotList);

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
                Plugin.Log.Info($"[CreateTransformedBeatmapData] Retrieved Vanilla BeatmapData from JSON v{TransitionPatcher.SelectedBeatmapVersion}: " +
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

                Version version = BeatmapVersionRegistry.versionByKey.TryGetValue(TransitionPatcher.SelectedPlayKey, out Version foundVersion) ? foundVersion : new Version(4, 0, 0); // 1.40.8 firestarter song was 4.0.0
                eData = new EditableCBD(bm, version);
            }
#if DEBUG
            foreach (var rot in eData.RotationEvents)
            {
                if (rot.time < 30)
                    Plugin.Log.Info($"1 Rotation - Time: {rot.time} - Rotation: {rot.rotation} - Total Rotation: {rot.accumRotation}");
            }
#endif

            Plugin.LogDebug($"[CreateTransformedBeatmapData] Converted (Custom)BeatmapData to EditableCBD map version: {eData.Version.Major} - notes: {eData.ColorNotes.Count}, bombs: {eData.BombNotes.Count}, obstacles: {eData.Obstacles.Count}, arcs: {eData.Arcs.Count}, chains: {eData.Chains.Count}, rotations: {eData.RotationEvents.Count}, basic events: {eData.BasicEvents.Count}, customEvents: {eData.CustomEvents.Count}, color boosts: {eData.ColorBoostEvents.Count}.");

            Plugin.LogDebug($"[CreateTransformedBeatmapData] Song Name: {SetContent.SongName} - v{TransitionPatcher.SelectedBeatmapVersion} - {TransitionPatcher.SelectedSerializedName} {TransitionPatcher.SelectedDifficulty}  ----------------------------------------------------------------------------");

            (NoodleProblemNotes, NoodleProblemObstacles) = EditableCBD.TestForNoodleCustomData(eData); //Should remove notes and walls first before figuring out rotations etc which are based on notes


            if (SetContent.IsBeatSageMap && Config.Instance.EnableCleanBeatSage)
            {
                BeatSageCleanUp.Clean(eData); // reference sent so no need to return eData
            }

            if (Utils.IsEnabledArcs() || Utils.IsEnabledChains())
            {
                if ((eData.Arcs.Count == 0 || eData.Chains.Count == 0) && !NoodleProblemNotes)
                {
                    Arcitect.CreateSliders(eData); //update data and update sliders list for arcfix later

                    string disabledText = DetermineScoreSubmissionReason(BeatSageCleanUp.DisableScoreSubmission, eData.MapAlreadyUsesChains, eData.Chains.Count);

                    if (!string.IsNullOrEmpty(disabledText))
                    {
                        ScoreGate.Set(disabledText);
                    }
                }
                else
                {
                    Plugin.LogDebug("[CreateTransformedBeatmapData] Arcitect OFF. Native Arcs and Chains already exists.");
                }

                if (Utils.IsEnabledArcs() && // go ahead and arc fix nonGen360 maps if arcs are enabled.
                    !eData.MapAlreadyUsesArcs && // unless they already use arcs. if they exist in 360 then they are probably placed correctly.
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

            if ((Utils.IsEnabledArcs() ||
                Utils.IsEnabledChains() ||
                Utils.IsEnabledWalls() ||
                (Utils.IsEnabledLighting() &&
                Config.Instance.BoostLighting) || // Config.Instance.EnableLightAutoMapper)) || //Config.Instance.OnlyOneSaber ||
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
                    return;

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
                    
                    Plugin.Log.Info($"[CreateTransformedBeatmapData] Final Vanilla BeatmapData v{TransitionPatcher.SelectedBeatmapVersion}: " +
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
                
                Plugin.LogDebug($"4 Final Lane Rotations in Notes from Data (represents the first note found with a new rotation value - Wireless360: {Config.Instance.Wireless360} - LimitRotations360: {Config.Instance.LimitRotations360}):");
            }
            //BeatmapLightingLogger.LogGLSLightingEvents(HarmonyPatches.CurrentBeatmapSaveData);
        }

        public static string DetermineScoreSubmissionReason(bool beatSageDisableScoreSubmission, bool mapAlreadyUsesChains, int chainsCount)
        {
            string str = "";

            if (TransitionPatcher.SelectedSerializedName == GameModeHelper.GENERATED_360DEGREE_MODE)
            {
                if (Config.Instance.BasedOn != Config.Base.Standard)
                {
                    str = "Base Map Not Standard";
                }
                if (Config.Instance.RotationSpeedMultiplier < 0.3f)
                {
                    str += (str != "" ? ", " : "") + "Rotation Mult Low";
                }
                if (!Config.Instance.Wireless360 && Config.Instance.LimitRotations360 < 90)
                {
                    str += (str != "" ? ", " : "") + "Rotations Limited";
                }
            }

            if (BS_Utils.Plugin.LevelData.Mode == BS_Utils.Gameplay.Mode.Standard &&
                Utils.IsEnabledAutoNjsFixer() &&
                !TransitionPatcher.AutoNJSDisabledByConflictingMod &&
                TransitionPatcher.OriginalNoteJumpMovementSpeed > TransitionPatcher.FinalNoteJumpMovementSpeed)
            {
                str += (str != "" ? ", " : "") + "Auto NJS Fixer";
            }

            if (Utils.IsEnabledChains() && !mapAlreadyUsesChains && chainsCount > 0)
            {
                str += (str != "" ? ", " : "") + "Architect Chains";
            }

            if (Config.Instance.EnableCleanBeatSage && (SetContent.IsBeatSageMap || TransitionPatcher.IsBeatSageMap) && beatSageDisableScoreSubmission)
            {
                str += (str != "" ? ", " : "") + "Beat Sage Cleaner";
            }

            if (str != "")
                str = "AutoBS—" + str; // prefix once

            return str;
        }
    }
}
