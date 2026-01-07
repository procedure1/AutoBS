using BeatmapSaveDataVersion2_6_0AndEarlier;
using BeatmapSaveDataVersion3;
using BeatmapSaveDataVersion4;
using BS_Utils.Gameplay;
using CustomJSONData.CustomBeatmap;
using HarmonyLib;
using SiraUtil.Zenject;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.Linq;
using static System.Windows.Forms.LinkLabel;

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
                        $"notes: {cbd.cuttableNotesCount}, " +
                        $"bombs: {cbd.bombsCount} bombs, " +
                        $"obstacles: {cbd.obstaclesCount} , " +
                        $"arcs: {cbd.allBeatmapDataItems.OfType<CustomSliderData>().Where((e) => e.sliderType == CustomSliderData.Type.Normal).Count()}, " +
                        $"chains {cbd.allBeatmapDataItems.OfType<CustomSliderData>().Where((e) => e.sliderType == CustomSliderData.Type.Burst).Count()}, " +
                        $"rotation events (v3 saveData): {v3RotList?.Count()}, " +
                        $"basic events: {cbd.allBeatmapDataItems.OfType<CustomBasicBeatmapEventData>().Count()}, " +
                        $"basic rotation events: {cbd.allBeatmapDataItems.OfType<CustomBasicBeatmapEventData>().Where((e) => e.basicBeatmapEventType == BasicBeatmapEventType.Event14 || e.basicBeatmapEventType == BasicBeatmapEventType.Event15).Count()}, " +
                        $"events: {cbd.allBeatmapDataItems.OfType<CustomEventData>().Count()}, " +
                        $"color boosts: {cbd.allBeatmapDataItems.OfType<CustomColorBoostBeatmapEventData>().Count()}, " + //v2 basic events end up here somehow automatically
                        $"bpm events: {cbd.allBeatmapDataItems.OfType<CustomBPMChangeBeatmapEventData>().Count()}");

                var cbdCopy = (CustomBeatmapData)cbd.GetCopy(); // need this so that original data is immutable. otherwise changes to eData will affect original data.
                eData = new EditableCBD(cbdCopy);
            }
            else if (beatmapData is BeatmapData bm) // built-in map data
            {
                Plugin.Log.Info($"[CreateTransformedBeatmapData] Retrieved Vanilla BeatmapData from JSON v{TransitionPatcher.SelectedBeatmapVersion}: " +
                         $"notes: {bm.cuttableNotesCount}, " +
                         $"bombs: {bm.bombsCount}, " +
                         $"obstacles: {bm.obstaclesCount}, " +
                         $"arcs: {bm.allBeatmapDataItems.OfType<SliderData>().Where((e) => e.sliderType == SliderData.Type.Normal).Count()}, " +
                         $"chains: {bm.allBeatmapDataItems.OfType<SliderData>().Where((e) => e.sliderType == SliderData.Type.Burst).Count()}, " +
                         //$" No 'Rotation Events' are available for v4, " +
                         $"basic events: {bm.allBeatmapDataItems.OfType<BasicBeatmapEventData>().Count()}, " +
                         $"basic rotation events: {bm.allBeatmapDataItems.OfType<BasicBeatmapEventData>().Where((e) => e.basicBeatmapEventType == BasicBeatmapEventType.Event14 || e.basicBeatmapEventType == BasicBeatmapEventType.Event15).Count()}, " +
                         $"events: {bm.allBeatmapDataItems.OfType<EventData>().Count()}, " +
                         $"color boosts: {bm.allBeatmapDataItems.OfType<ColorBoostBeatmapEventData>().Count()}, " +
                         $"bpm events: {bm.allBeatmapDataItems.OfType<BpmChangeEventData>().Count()}, " +
                         $"njs events: {bm.allBeatmapDataItems.OfType<NoteJumpSpeedEventData>().Count()}");

                Version version = BeatmapVersionRegistry.versionByKey.TryGetValue(TransitionPatcher.SelectedPlayKey, out Version foundVersion) ? foundVersion : new Version(4, 0, 0); // 1.40.8 firestarter song was 4.0.0
                var bmCopy = bm.GetCopy(); // need this so that original data is immutable. otherwise changes to eData will affect original data.
                eData = new EditableCBD(bmCopy, version);
            }
#if DEBUG
            foreach (var rot in eData.RotationEvents)
            {
                if (rot.time < 30)
                    Plugin.LogDebug($"1 Rotation - Time: {rot.time} - Rotation: {rot.rotation} - Total Rotation: {rot.accumRotation}");
            }
#endif

            Plugin.LogDebug($"[CreateTransformedBeatmapData] Converted (Custom)BeatmapData to EditableCBD map version: {eData.Version.Major} - notes: {eData.ColorNotes.Count}, bombs: {eData.BombNotes.Count}, obstacles: {eData.Obstacles.Count}, arcs: {eData.Arcs.Count}, chains: {eData.Chains.Count}, rotations: {eData.RotationEvents.Count}, basic events: {eData.BasicEvents.Count}, customEvents: {eData.CustomEvents.Count}, color boosts: {eData.ColorBoostEvents.Count}.");

            Plugin.LogDebug($"[CreateTransformedBeatmapData] Song Name: {SetContent.SongName} - v{TransitionPatcher.SelectedBeatmapVersion} - {TransitionPatcher.SelectedSerializedName} {TransitionPatcher.SelectedDifficulty}  ----------------------------------------------------------------------------");

            (NoodleProblemNotes, NoodleProblemObstacles) = EditableCBD.TestForNoodleCustomData(eData); //Should remove notes and walls first before figuring out rotations etc which are based on notes

            /*
            if (TransitionPatcher.IsBeatSageMap && Config.Instance.EnableCleanBeatSage)
            {
                BeatSageCleanUp.Clean(eData); // reference sent so no need to return eData
            }
            */
            /*
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
            */
            /*
            #region LightAutoMapper

            if (Utils.IsEnabledLighting() && Config.Instance.EnableLightAutoMapper)
            {
                LightAutoMapper.Start(eData);
            }
            
            #endregion
            */
            //if ((Utils.IsEnabledArcs() ||
            //    Utils.IsEnabledChains() ||
            //    Utils.IsEnabledWalls() ||
            //    (Utils.IsEnabledLighting() &&
            //    Config.Instance.BoostLighting) || // Config.Instance.EnableLightAutoMapper)) || //Config.Instance.OnlyOneSaber ||
            //    TransitionPatcher.SelectedSerializedName == GameModeHelper.GENERATED_360DEGREE_MODE))
            {
                Plugin.LogDebug($"[CreateTransformedBeatmapData] Pipeline Called. Generating map changes for {TransitionPatcher.SelectedSerializedName}...");
                /*
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
                */

                PipelineResult outp = GenerationPipeline.Run(eData);

                if (!outp.OriginalMapAltered)
                    return;

                __result = outp.IsCustom ? (IReadonlyBeatmapData)outp.Custom : outp.Vanilla;



                if (outp.IsCustom)
                {
                    //__result = outp.Custom!;

                    Plugin.Log.Info($"[CreateTransformedBeatmapData] Final CustomBeatmapData: " +
                         $"notes: {__result.cuttableNotesCount}, " +
                         $"bombs: {__result.bombsCount}, " +
                         $"obstacles: {__result.obstaclesCount}, " +
                         $"arcs: {__result.allBeatmapDataItems.OfType<CustomSliderData>().Where((e) => e.sliderType == CustomSliderData.Type.Normal).Count()}, " +
                         $"chains: {__result.allBeatmapDataItems.OfType<CustomSliderData>().Where((e) => e.sliderType == CustomSliderData.Type.Burst).Count()}, " +
                         $"basic events: {__result.allBeatmapDataItems.OfType<CustomBasicBeatmapEventData>().Count()}, " +
                         $"events: {__result.allBeatmapDataItems.OfType<CustomEventData>().Count()}, " +
                         $"color boosts: {__result.allBeatmapDataItems.OfType<CustomColorBoostBeatmapEventData>().Count()}, " +
                         $"bpm events: {__result.allBeatmapDataItems.OfType<CustomBPMChangeBeatmapEventData>().Count()}, " +
                         $"rotation events (in-line per object) {eData.RotationEvents.Count}" );
                    // v4 unsupported by customJsonData - $"{__result.allBeatmapDataItems.OfType<NoteJumpSpeedEventData>().Count()} NJS Events");

                    //ConvertEditableCBD.PerObjectRotationLog(__result as CustomBeatmapData, eData);

                    JsonOutputConverter.ToJsonFile(__result as CustomBeatmapData, eData);

                }
                else
                {
                    //__result = outp.Vanilla!;
                    
                    Plugin.Log.Info($"[CreateTransformedBeatmapData] Final Vanilla BeatmapData v{TransitionPatcher.SelectedBeatmapVersion}: " +
                        $"notes: {__result.cuttableNotesCount}, " +
                        $"bombs: {__result.bombsCount}, " +
                        $"obstacles: {__result.obstaclesCount}, " +
                        $"arcs: {__result.allBeatmapDataItems.OfType<SliderData>().Where((e) => e.sliderType == SliderData.Type.Normal).Count()}, " +
                        $"chains: {__result.allBeatmapDataItems.OfType<SliderData>().Where((e) => e.sliderType == SliderData.Type.Burst).Count()}, " +
                        $"basic events: {__result.allBeatmapDataItems.OfType<BasicBeatmapEventData>().Count()}, " +
                        $"events: {__result.allBeatmapDataItems.OfType<EventData>().Count()}, " +
                        $"color boosts: {__result.allBeatmapDataItems.OfType<ColorBoostBeatmapEventData>().Count()}, " +
                        $"bpm events: {__result.allBeatmapDataItems.OfType<BpmChangeEventData>().Count()}, " +
                        $"njs events: {__result.allBeatmapDataItems.OfType<NoteJumpSpeedEventData>().Count()}, " +
                        $"rotation events (in -line per object): {eData.RotationEvents.Count}");
                }
                
                //Plugin.LogDebug($"4 Final Lane Rotations in Notes from Data (represents the first note found with a new rotation value - Wireless360: {Config.Instance.Wireless360} - LimitRotations360: {Config.Instance.LimitRotations360}):");
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

            if (Config.Instance.EnableCleanBeatSage && (TransitionPatcher.IsBeatSageMap) && beatSageDisableScoreSubmission) 
            {
                str += (str != "" ? ", " : "") + "Beat Sage Cleaner";
            }

            if (str != "")
                str = "AutoBS—" + str; // prefix once

            return str;
        }
    }
}
