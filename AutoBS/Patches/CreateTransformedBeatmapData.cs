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

                int nativeBasicRotationV2EventsCount = cbd.allBeatmapDataItems.OfType<CustomBasicBeatmapEventData>().Where((e) => e.basicBeatmapEventType == BasicBeatmapEventType.Event14 || e.basicBeatmapEventType == BasicBeatmapEventType.Event15).Count();

                Plugin.Log.Info($"[CreateTransformedBeatmapData] Retrieved CustomBeatmapData from JSON v{cbd.version.Major} (major version): " +
                        $"notes: {cbd.cuttableNotesCount}, " +
                        $"bombs: {cbd.bombsCount} bombs, " +
                        $"obstacles: {cbd.obstaclesCount} , " +
                        $"arcs: {cbd.allBeatmapDataItems.OfType<CustomSliderData>().Where((e) => e.sliderType == CustomSliderData.Type.Normal).Count()}, " +
                        $"chains {cbd.allBeatmapDataItems.OfType<CustomSliderData>().Where((e) => e.sliderType == CustomSliderData.Type.Burst).Count()}, " +
                        $"rotation events (v3 saveData): {v3RotList?.Count()}, " +
                        $"basic events: {cbd.allBeatmapDataItems.OfType<CustomBasicBeatmapEventData>().Count()}, " +
                        $"basic rotation events: {nativeBasicRotationV2EventsCount}, " +
                        $"events: {cbd.allBeatmapDataItems.OfType<CustomEventData>().Count()}, " +
                        $"color boosts: {cbd.allBeatmapDataItems.OfType<CustomColorBoostBeatmapEventData>().Count()}, " + //v2 basic events end up here somehow automatically
                        $"bpm events: {cbd.allBeatmapDataItems.OfType<CustomBPMChangeBeatmapEventData>().Count()}");

                /*
                #if DEBUG
                if (nativeBasicRotationV2EventsCount > 0)
                {
                    int prevAccum = 0;
                    Plugin.LogDebug("Original Native 360/90 Rotations Events:");
                    foreach (var rot in cbd.allBeatmapDataItems.OfType<CustomBasicBeatmapEventData>().Where((e) => e.basicBeatmapEventType == BasicBeatmapEventType.Event14 || e.basicBeatmapEventType == BasicBeatmapEventType.Event15))
                    {
                        int rotation = RotationGenerator.SpawnRotationValueToDegrees(rot.value);
                        int accumRotation = rotation + prevAccum;
                        prevAccum = accumRotation;
                        Plugin.LogDebug($"Native v2 Rotation - Time: {rot.time:F} - Rotation: {rotation} - Total Rotation: {accumRotation}");
                    }
                }
                #endif
                */


                var cbdCopy = (CustomBeatmapData)cbd.GetCopy(); // need this so that original data is immutable. otherwise changes to eData will affect original data.
                eData = new EditableCBD(cbdCopy);

                //ConvertEditableCBD.PerObjectRotationLog(cbd, eData, 30f, 60f);
                /*
                Plugin.LogDebug($"Original Native 360 Note Rotations:");
                foreach (var note in cbd.allBeatmapDataItems
                .OfType<NoteData>()
                .Where(n => n.time >= 0f && n.time <= 60f))
                {
                    Plugin.LogDebug($"Note: {note.time:F} Rot: {note.rotation}");
                }
                */
            }
            else if (beatmapData is BeatmapData bm) // built-in map data
            {
                Plugin.Log.Info($"[CreateTransformedBeatmapData] Retrieved custom v4 or Vanilla BeatmapData from JSON v{TransitionPatcher.SelectedBeatmapVersion}: " +
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
                /*
                foreach (var obs in eData.Obstacles)
                {
                    if (obs.time > 32 && obs.time < 33)
                        Plugin.LogDebug($"Original Obstacle - Time: {obs.time:F} - Line: {obs.line} - Layer: {obs.layer} - Height: {obs.height} - width: {obs.width} - Dur: {obs.duration}");
                }
                
    #if DEBUG
                foreach (var rot in eData.RotationEvents)
                {
                    if (rot.time < 30)
                        Plugin.LogDebug($"Original Rotation - Time: {rot.time} - Rotation: {rot.rotation} - Total Rotation: {rot.accumRotation}");
                }
    #endif
                */
                Plugin.LogDebug($"[CreateTransformedBeatmapData] Converted (Custom)BeatmapData to EditableCBD map version: {eData.Version.Major} - notes: {eData.ColorNotes.Count}, bombs: {eData.BombNotes.Count}, obstacles: {eData.Obstacles.Count}, arcs: {eData.Arcs.Count}, chains: {eData.Chains.Count}, rotations: {eData.RotationEvents.Count}, basic events: {eData.BasicEvents.Count}, customEvents: {eData.CustomEvents.Count}, color boosts: {eData.ColorBoostEvents.Count}.");

                Plugin.LogDebug($"[CreateTransformedBeatmapData] Song Name: {SetContent.SongName} - v{TransitionPatcher.SelectedBeatmapVersion} - {TransitionPatcher.SelectedSerializedName} {TransitionPatcher.SelectedDifficulty}  ----------------------------------------------------------------------------");

                (NoodleProblemNotes, NoodleProblemObstacles) = EditableCBD.TestForNoodleCustomData(eData); //Should remove notes and walls first before figuring out rotations etc which are based on notes

                
                // CALL PIPELINE ---------------------------------------------

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
                            $"rotation events (in-line per object) {eData.RotationEvents.Count}");
                    // v4 unsupported by customJsonData - $"{__result.allBeatmapDataItems.OfType<NoteJumpSpeedEventData>().Count()} NJS Events");

                    //ConvertEditableCBD.PerObjectRotationLog(__result as CustomBeatmapData, eData, 205, 220);
                    /*
                    Plugin.LogDebug($"Final Note Rotations:");
                    foreach (var note in __result.allBeatmapDataItems
                    .OfType<NoteData>()
                    .Where(n => n.time >= 204f && n.time <= 300f))
                    {
                        Plugin.LogDebug($"Note: {note.time:F} {note.cutDirection} Rot: {note.rotation}");
                    }
                    */
                    /*
                    foreach (var evt in __result.allBeatmapDataItems.OfType<CustomBasicBeatmapEventData>())
                    {
                        if (evt.time > 0 && evt.time < 85)
                            Plugin.LogDebug($"Light: {evt.time:F3} {(EventType)evt.basicBeatmapEventType} {(EventValue)evt.value} float: {evt.floatValue}");
                    }
                    */

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

                    if (TransitionPatcher.IsCustomLevel) // can be v4 custom level. i'm trying to avoid outputing json for vanilla maps for copywrite reasons i suppose
                        JsonOutputConverter.ToJsonFile(__result as CustomBeatmapData, eData);
                /*
                if (__result.allBeatmapDataItems.OfType<NoteJumpSpeedEventData>().Count() > 0)
                {

                    Plugin.LogDebug($"Final NJS Events:");
                    foreach (var evt in __result.allBeatmapDataItems.OfType<NoteJumpSpeedEventData>())//.Where(n => n.time >= 0f && n.time <= 60f))
                    {
                        Plugin.LogDebug($"NJS Event: {evt.time:F} relative NJS: {evt.relativeNoteJumpSpeed} EaseType: {evt.easeType} usePreviousValue: {evt.usePreviousValue}");
                    }


                } 
                */
                /*
                foreach (var obs in __result.allBeatmapDataItems.OfType<ObstacleData>())
                {
                    if (obs.time > 32 && obs.time < 33)
                        Plugin.LogDebug($"Final ObstacleData - Time: {obs.time:F} - Line: {obs.lineIndex} - Layer: {(int)obs.lineLayer} - Height: {obs.height} - width: {obs.width} - Dur: {obs.duration}");
                }
                */
            }
                
                //Plugin.LogDebug($"4 Final Lane Rotations in Notes from Data (represents the first note found with a new rotation value - Wireless360: {Config.Instance.Wireless360} - LimitRotations360: {Config.Instance.LimitRotations360}):");

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
