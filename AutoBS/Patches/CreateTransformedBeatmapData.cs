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
using static VRControllersRecorder;

namespace AutoBS.Patches
{
    // 7 Postfix MAIN CODE!!!! generates map changes and alters the beat map data such as rotation events...
    //runs after LevelUpdatePatcher & GameModeHelper & TransitionPatcher https://harmony.pardeike.net/articles/patching-prefix.html
    //This runs automatically after user hits Play
    [HarmonyPatch(typeof(BeatmapDataTransformHelper), "CreateTransformedBeatmapData")]
    public class BeatmapDataTransformHelperPatcher
    {
        //public static CustomBeatmapData data; //only added this for slider rotations. can remove if don't end up using it
        public static List<CustomSliderData> arcsAndChains = new List<CustomSliderData>(); //v1.40 - added this to store arcs and chains for later use in slider rotations

        public static bool NoodleProblemNotes = false;
        public static bool NoodleProblemObstacles = false;

        public struct Settings { }; //v1.40 - added this to get rid of the error in the new version of BS. I don't know what it does but it works.


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

            //if (!(beatmapData is CustomBeatmapData)) return;

            //var cbd = beatmapData as CustomBeatmapData;
            //Plugin.Log.Info($"[CreateTransformedBeatmapData] Retrieved BeatmapData from JSON - notes: {beatmapData.allBeatmapDataItems.OfType<NoteData>().Count()} obstacles: {beatmapData.allBeatmapDataItems.OfType<ObstacleData>().Count()} events: {beatmapData.allBeatmapDataItems.OfType<EventData>().Count()} customEvents: {beatmapData.allBeatmapDataItems.OfType<CustomEventData>().Count()}.");

            //beatmapData = BeatmapDataRegistry.beatmapDataByKey[TransitionPatcher.CurrentPlayKey];
            EditableCBD eData = null;

            if (beatmapData is CustomBeatmapData customData) // custom map data
            {
                Plugin.Log.Info($"[CreateTransformedBeatmapData] Retrieved CustomBeatmapData from JSON v{customData.version} (major version): " +
                         $"{customData.cuttableNotesCount} notes, " +
                         $"{customData.bombsCount} bombs, " +
                         $"{customData.obstaclesCount} obstacles, " +
                         $"{customData.allBeatmapDataItems.OfType<CustomSliderData>().Where((e) => e.sliderType == CustomSliderData.Type.Normal).Count()} Arcs, " +
                         $"{customData.allBeatmapDataItems.OfType<CustomSliderData>().Where((e) => e.sliderType == CustomSliderData.Type.Burst).Count()} Chains, " +
                         $"{customData.allBeatmapDataItems.OfType<RotationEventData>().Count()} Rotation Events, " +
                         $"{customData.allBeatmapDataItems.OfType<CustomBasicBeatmapEventData>().Count()} Basic Events, " +
                         $"{customData.allBeatmapDataItems.OfType<CustomEventData>().Count()} Events, " +
                         $"{customData.allBeatmapDataItems.OfType<CustomColorBoostBeatmapEventData>().Count()} Color Boosts, " +
                         $"{customData.allBeatmapDataItems.OfType<CustomBPMChangeBeatmapEventData>().Count()} Bpm Change Events");

                eData = new EditableCBD(customData);
            }
            else if (beatmapData is BeatmapData bm) // built-in map data
            {
                Plugin.Log.Info($"[CreateTransformedBeatmapData] Retrieved Vanilla BeatmapData from JSON v{TransitionPatcher.CurrentBeatmapVersion}: " +
                         $"{bm.cuttableNotesCount} notes, " +
                         $"{bm.bombsCount} bombs, " +
                         $"{bm.obstaclesCount} obstacles, " +
                         $"{bm.allBeatmapDataItems.OfType<SliderData>().Where((e) => e.sliderType == SliderData.Type.Normal).Count()} Arcs, " +
                         $"{bm.allBeatmapDataItems.OfType<SliderData>().Where((e) => e.sliderType == SliderData.Type.Burst).Count()} Chains, " +
                         $"{bm.allBeatmapDataItems.OfType<RotationEventData>().Count()} Rotation Events, " +
                         $"{bm.allBeatmapDataItems.OfType<BasicBeatmapEventData>().Count()} Basic Events, " +
                         $"{bm.allBeatmapDataItems.OfType<EventData>().Count()} Events, " +
                         $"{bm.allBeatmapDataItems.OfType<ColorBoostBeatmapEventData>().Count()} Color Boosts, " +
                         $"{bm.allBeatmapDataItems.OfType<BpmChangeEventData>().Count()} Bpm Change Events, " +
                         $"{bm.allBeatmapDataItems.OfType<NoteJumpSpeedEventData>().Count()} NJS Events");

                Version version = BeatmapDataRegistry.versionByKey.TryGetValue(TransitionPatcher.CurrentPlayKey, out Version foundVersion) ? foundVersion : new Version(4, 0, 0); // 1.40.8 firestarter song was 4.0.0
                eData = new EditableCBD(bm, version);
            }

            Plugin.Log.Info($"[CreateTransformedBeatmapData] Converted (Custom)BeatmapData to EditableCBD map version: {eData.Version} - notes: {eData.ColorNotes.Count()} bombs: {eData.BombNotes.Count()} arcs: {eData.Arcs.Count()} chains: {eData.Chains.Count()} obstacles: {eData.Obstacles.Count()} rotations: {eData.RotationEvents.Count()} events: {eData.BasicEvents.Count()} customEvents: {eData.CustomEvents.Count()}.");

            //Version2_6_0AndEarlierCustomBeatmapSaveData saveData = SaveDataRegistry.cjdSaveDataByKey[TransitionPatcher.CurrentPlayKey];
            //CustomBeatmapData cbd = SetContent.ProduceCJD(saveData);
            // Create easily editable version of CustomBeatmapData
            //var eData = new EditableCBD(cbd);


            Plugin.Log.Info($"[CreateTransformedBeatmapData] Song Name: {SetContent.SongName} - v{TransitionPatcher.LevelVersion} - {TransitionPatcher.SelectedSerializedName} {TransitionPatcher.SelectedDifficulty}  ----------------------------------------------------------------------------");

            // Moved this here since chains inside of generator have segments improperly placed -----------------------------------------
            //CustomBeatmapData data = Generator.DeepCopyBeatmapData((BeatmapData)__result);
            //data = __result.GetCopy(); //add back the Type BeatmapData if not keeping my global variable verison for slider rotations

            Stopwatch stopwatch = new Stopwatch();

            //Should remove notes and walls first before figuring out rotations etc which are based on notes

            (NoodleProblemNotes, NoodleProblemObstacles) = EditableCBD.TestForNoodleCustomData(eData);


            if (SetContent.IsBeatSageMap && Config.Instance.EnableCleanBeatSage)
            {
                stopwatch.Restart();

                BeatSageCleanUp.Clean(eData); // reference sent so no need to return eData
                Plugin.Log.Info(
                $" ------- BeatSageCleanUp time elapsed: {stopwatch.ElapsedMilliseconds / 1000.0:F1}");

                stopwatch.Stop();
            }

            if (Utils.IsEnabledArcs() || Utils.IsEnabledChains())
            {
                stopwatch.Restart();



                /*
                List<CustomSliderData> arcs = data.allBeatmapDataItems.OfType<CustomSliderData>()
                .Where((e) => e.sliderType == SliderData.Type.Normal).ToList(); //get original arcs
                List<CustomSliderData> chains = data.allBeatmapDataItems.OfType<CustomSliderData>()
                    .Where((e) => e.sliderType == SliderData.Type.Burst).ToList(); //get original chains
                List<CustomNoteData> notes = data.allBeatmapDataItems.OfType<CustomNoteData>().ToList();

                //List<SpawnRotationBeatmapEventData> rotationData = data.allBeatmapDataItems.OfType<SpawnRotationBeatmapEventData>().ToList();// get original rotations
                List<CustomBasicBeatmapEventData> rotationData = data.allBeatmapDataItems
                    .OfType<CustomBasicBeatmapEventData>()
                    .Where(e => e.basicBeatmapEventType == BasicBeatmapEventType.Event14 || e.basicBeatmapEventType == BasicBeatmapEventType.Event15)
                    .ToList();

                List<(float time, int rotate)> originalRotations = new List<(float time, int rotate)>();

                //float prevRotation = 0;
                foreach (var rotation in rotationData)
                {
                    originalRotations.Add((rotation.time, Generator.SpawnRotationValueToDegrees(rotation.value))); //- prevRotation)));
                    //prevRotation = rotation.rotation;
                }
                */




                //foreach (var arc in arcs)
                //{
                //    Plugin.Log.Info($"Original arc: Start: {arc.time} - Duration: {(arc.tailTime - arc.time)} - End: {arc.tailTime}");
                //}

                //IReadOnlyList<SliderData> readOnlySliders = sliders.ToList(); // Made this since sliders the sliders list will have more super short sliders added to it (i have no idea why!!!!) by the time we get to arcFix a few lines down. so made this copy to avoid that problem.



                if ((eData.Arcs.Count == 0 || eData.Chains.Count == 0) && !NoodleProblemNotes)
                {
                    Arcitect.CreateSliders(eData); //update data and update sliders list for arcfix later

                    string disabledText = TransitionPatcher.DetermineScoreSubmissionReason(BeatSageCleanUp.DisableScoreSubmission, eData.Chains.Count);

                    if (!string.IsNullOrEmpty(disabledText))
                    {
                        ScoreGate.Set(disabledText);
                    }
                }
                else
                {
                    Plugin.Log.Info("[CreateTransformedBeatmapData] Arcitect OFF. Native Arcs and Chains already exists.");
                }

                //if (Config.Instance.EnableFeaturesForNonGen360Maps &&
                if (Utils.IsEnabledArcs() && // go ahead and arc fix nonGen360 maps if arcs are enabled.
                    Config.Instance.ArcFixFull &&
                   (TransitionPatcher.SelectedSerializedName == "360Degree" || TransitionPatcher.SelectedSerializedName == "90Degree"))
                {
                    List<ERotationEventData> allRotations = eData.RotationEvents;
                    
                    Plugin.Log.Info($"[CreateTransformedBeatmapData] Arcitect ArcFix for NonGen360 Maps Activated. Original Rotations Count: {allRotations.Count}");
                    //Arcitect.ArcFixForNonGen360(arcs, data);
                    eData.RotationEvents = Arcitect.ArcFix(allRotations, eData);
                }

                //if (Utils.IsEnabledChains())
                //{
                //    ScoreSubmission.DisableSubmission("Architect Chains");
                //    //Plugin.Log.Info("Score disabled by Architect Chains.");
                //}


                Plugin.Log.Info(
                    $" ------- Arcitect time elapsed: {stopwatch.ElapsedMilliseconds / 1000.0:F1}");
                stopwatch.Stop();
            }

            #region LightAutoMapper

            if (Utils.IsEnabledLighting() && Config.Instance.EnableLightAutoMapper)
            {
                stopwatch.Restart();

                LightAutoMapper.Start(eData);

                Plugin.Log.Info(
                $" ------- LightAutoMapper time elapsed: {stopwatch.ElapsedMilliseconds / 1000.0:F1}");
                stopwatch.Stop();
            }

            #endregion


            //__result = data; // must use this (that means __result is used multiple times) to avoid problem chains

            // End --------------------------------------------------------------------------------------------------------------------------

            // Moved this here to give standard non-360 maps all the mod features -- had to comment these out of Generator360
            // Chains -- Moved this here since doing this inside of Generator360 causes the chains to be displayed incorrectly!!!!! the 1st segment will overlap the note!!!!
            //if (Config.Instance.AllowCrouchWalls ||
            //    Config.Instance.AllowLeanWalls ||
            //     Config.Instance.EnableWallGenerator ||
            //     Config.Instance.BoostLighting ||
            //     Config.Instance.EnableLightAutoMapper ||
            //     TransitionPatcher.startingGameMode == GameModeHelper.GENERATED_360DEGREE_MODE ||
            //    TransitionPatcher.startingGameMode == GameModeHelper.GENERATED_90DEGREE_MODE)
            if ((Utils.IsEnabledArcs() ||
                Utils.IsEnabledChains() ||
                Utils.IsEnabledWalls() ||
                (Utils.IsEnabledLighting() &&
                Config.Instance.BoostLighting) || // Config.Instance.EnableLightAutoMapper)) || //Config.Instance.OnlyOneSaber ||
                TransitionPatcher.SelectedSerializedName == GameModeHelper.GENERATED_360DEGREE_MODE))
            {
                Plugin.Log.Info($"[CreateTransformedBeatmapData] Generator Called. Generating map changes for {TransitionPatcher.SelectedSerializedName}...");

                Generator gen = new Generator
                {
                    //WallGenerator1 = Config.Instance.EnableWallGenerator,
                    RotationSpeedMultiplier = (float)Math.Round(Config.Instance.RotationSpeedMultiplier, 1),
                    AllowCrouchWalls = Config.Instance.AllowCrouchWalls,
                    AllowLeanWalls = Config.Instance.AllowLeanWalls,
                    //OnlyOneSaber = Config.Instance.OnlyOneSaber,
                    //LeftHandedOneSaber = Config.Instance.LeftHandedOneSaber
                };

                if (TransitionPatcher.SelectedSerializedName == GameModeHelper.GENERATED_360DEGREE_MODE)
                {
                    Plugin.Log.Info($"[CreateTransformedBeatmapData] Generating rotation events for {TransitionPatcher.SelectedSerializedName}...");

                    /*
                    if (Config.Instance.BasedOn != Config.Base.Standard)
                    {
                        ScoreSubmission.DisableSubmission("360fyer Base Map Not Standard");
                        //Plugin.Log.Info("Score disabled by Standard or Multiplier " + gen.RotationSpeedMultiplier);
                    }
                    if (gen.RotationSpeedMultiplier < 0.3f)
                    {
                        ScoreSubmission.DisableSubmission("360fyer Rotation Mult Set Low");
                        //Plugin.Log.Info("Score disabled by Standard or Multiplier " + gen.RotationSpeedMultiplier);
                    }
                    */

                    if (TransitionPatcher.SelectedSerializedName == GameModeHelper.GENERATED_360DEGREE_MODE)
                    {
                        if (Config.Instance.Wireless360)
                        {
                            gen.LimitRotations = 99999;
                            gen.BottleneckRotations = 99999;
                        }
                        else
                        {
                            /*
                            //BW divided by 2 to make the rotation angle accurate. 90 degrees was 180 degress without this 
                            if (Config.Instance.LimitRotations360 <
                                90) //|| gen.OnlyOneSaber || gen.AllowCrouchWalls || gen.AllowLeanWalls)// || gen.RotationAngleMultiplier != 1.0f)
                            {
                                ScoreSubmission.DisableSubmission("360fyer Rotations Limited");
                                //Plugin.Log.Info("Score disabled by LimitRotations360 set less than 150.");
                            }
                            */
                            gen.LimitRotations =
                                (int)((Config.Instance.LimitRotations360 / 360f / 2f) *
                                      (24f)); // / Config.Instance.RotationAngleMultiplier));//BW this convert the angle into LimitRotation units of 15 degree slices. Need to divide the Multiplier since it causes the angle to change from 15 degrees. this will keep the desired limit to work if a multiplier is added.
                            gen.BottleneckRotations = gen.LimitRotations / 2;
                        }
                    }

                }

                // Noodle events still exist in eData up to this point.
                var outp = gen.Generate(eData, beatmapLevel.beatsPerMinute);
                if (outp.IsCustom)
                {
                    __result = outp.Custom!;

                    Plugin.Log.Info($"[CreateTransformedBeatmapData] eData -> Final CustomBeatmapData v{TransitionPatcher.CurrentBeatmapVersion}: " +
                         $"{__result.cuttableNotesCount} notes, " +
                         $"{__result.bombsCount} bombs, " +
                         $"{__result.obstaclesCount} obstacles, " +
                         $"{__result.allBeatmapDataItems.OfType<CustomSliderData>().Where((e) => e.sliderType == CustomSliderData.Type.Normal).Count()} Arcs, " +
                         $"{__result.allBeatmapDataItems.OfType<CustomSliderData>().Where((e) => e.sliderType == CustomSliderData.Type.Burst).Count()} Chains, " +
                         $"{__result.allBeatmapDataItems.OfType<CustomBasicBeatmapEventData>().Count()} Basic Events, " +
                         $"{__result.allBeatmapDataItems.OfType<CustomEventData>().Count()} Events, " +
                         $"{__result.allBeatmapDataItems.OfType<CustomColorBoostBeatmapEventData>().Count()} Color Boosts, " +
                         $"{__result.allBeatmapDataItems.OfType<CustomBPMChangeBeatmapEventData>().Count()} Bpm Change Events");
                    // v4 unsupported by customJsonData - $"{__result.allBeatmapDataItems.OfType<NoteJumpSpeedEventData>().Count()} NJS Events");

                    JsonOutputConverter.ToJsonFile(__result as CustomBeatmapData, eData);

                }
                else
                {
                    __result = outp.Vanilla!;
                    
                    Plugin.Log.Info($"[CreateTransformedBeatmapData] eData -> Final Vanilla BeatmapData v{TransitionPatcher.CurrentBeatmapVersion}: " +
                         $"{__result.cuttableNotesCount} notes, " +
                         $"{__result.bombsCount} bombs, " +
                         $"{__result.obstaclesCount} obstacles, " +
                         $"{__result.allBeatmapDataItems.OfType<SliderData>().Where((e) => e.sliderType == SliderData.Type.Normal).Count()} Arcs, " +
                         $"{__result.allBeatmapDataItems.OfType<SliderData>().Where((e) => e.sliderType == SliderData.Type.Burst).Count()} Chains, " +
                         $"{__result.allBeatmapDataItems.OfType<BasicBeatmapEventData>().Count()} Basic Events, " +
                         $"{__result.allBeatmapDataItems.OfType<EventData>().Count()} Events, " +
                         $"{__result.allBeatmapDataItems.OfType<ColorBoostBeatmapEventData>().Count()} Color Boosts, " +
                         $"{__result.allBeatmapDataItems.OfType<BpmChangeEventData>().Count()} Bpm Change Events, " +
                         $"{__result.allBeatmapDataItems.OfType<NoteJumpSpeedEventData>().Count()} NJS Events");
                }
                /*
                foreach (var note in __result.allBeatmapDataItems.OfType<NoteData>())
                {
                    if (note.time < 200)
                      Plugin.Log.Info($"Note Time: {note.time}, Type: {note.type} ColorType: {note.colorType} GameplayType: {note.gameplayType} ScoringType: {note.scoringType}");
                }
                foreach (var slider in __result.allBeatmapDataItems.OfType<SliderData>().Where((e) => e.sliderType == SliderData.Type.Normal))
                {
                    if (slider.time < 200)
                        Plugin.Log.Info($"Arc Time: {slider.time}, Type: {slider.sliderType} HasHeadNote: {slider.hasHeadNote}");
                }
                foreach (var slider in __result.allBeatmapDataItems.OfType<SliderData>().Where((e) => e.sliderType == SliderData.Type.Burst))
                {
                    if (slider.time < 200)
                        Plugin.Log.Info($"Chain Time: {slider.time}, Type: {slider.sliderType} HasHeadNote: {slider.hasHeadNote} SliceCount: {slider.sliceCount}");
                }
                */
                // __result = gen.Generate(eData, beatmapLevel.beatsPerMinute); //__result, beatmapLevel.beatsPerMinute);




                //data = Generator.DeepCopyBeatmapData((BeatmapData)__result);



                //ConvertEditableCBD.PerObjectRotationLog(data, eData);


                //data = __result.GetCopy(); // added this for slider rotations only !!!!!!!!!!!!!!!! DO I NEED THIS ANYMORE????
                
                Plugin.Log.Info($"4 Final Lane Rotations in Notes from Data (represents the first note found with a new rotation value - Wireless360: {Config.Instance.Wireless360} - LimitRotations360: {Config.Instance.LimitRotations360}):");
                /*
                float prevRotation = 0; // Use NaN so first comparison always passes
                
                foreach (var note in __result.allBeatmapDataItems.OfType<NoteData>())
                {
                    if (note.time < 10)
                    {
                        float currentRot = note.rotation;

                        float deltaRot = currentRot - prevRotation;
                        Plugin.Log.Info($"4 Rotation - Time: {note.time:F} - Rot: {(int)deltaRot} - Accumulated Rot: {(int)currentRot}");

                        prevRotation = currentRot;

                    }
                }
                */
                /*
                foreach (var obst in data.allBeatmapDataItems.OfType<ObstacleData>())
                {
                    if (obst is CustomObstacleData custObst)
                    {
                        if (obst.time < 50)
                        {
                            Plugin.Log.Info($"Obst Time: {obst.time}, Index:{obst.lineIndex}, Layer: {(int)obst.lineLayer}, Dur: {obst.duration}, Width: {obst.width}, Height: {obst.height}");
                        }
                    }
                }
                */
            }

            //BeatmapLightingLogger.LogGLSLightingEvents(HarmonyPatches.CurrentBeatmapSaveData);

        }
    }
}
