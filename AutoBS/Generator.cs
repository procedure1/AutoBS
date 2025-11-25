using AutoBS.Patches; // BW FINAL 4/26/2024
using BeatmapSaveDataVersion2_6_0AndEarlier;
using CustomJSONData.CustomBeatmap;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.UI;
using static NoteData;
using static SliderController.Pool;
//using Newtonsoft.Json;//BW added to work with JSON files - rt click solution explorer and manage NuGet packages
//using System.IO;//BW for file writing

namespace AutoBS
{
    //1. How a Rotation is Created
    //A rotation event is triggered in the following way:

    //Notes are grouped into bars

    //The song is divided into bars based on PreferredBarDuration and RotationSpeedMultiplier.
    //The code collects all notes within a bar into notesInBar.
    //Each bar is further divided into smaller beats

    //The number of subdivisions depends on the note density (barDivider logic).
    //The more notes in a bar, the fewer rotation events are added.
    //Direction is determined based on note positions and cut directions
    //If a large gap before the next note, allow a bigger rotation(rotationCount = 2 or 3).
    //If notes are close together, use a smaller rotation(rotationCount = 1).

    //The final note(s) in a bar are analyzed.
    //If most notes are on the left or are cut leftward → rotate left. If most notes are on the right or are cut rightward → rotate right.
    //If notes are balanced, it follows:
    //If total rotation is too far to one side, prefer rotating in the opposite direction. If total rotations exceed BottleneckRotations, prefer the opposite direction
    //Otherwise, follow the previous rotation direction.

    /// <summary>
    /// Must convert built-in OST maps to standard beatmapData or for some reason heck will crash beat saber since it finds cast exception for customBeatmapData
    /// </summary>
    public sealed class GeneratorOutput
    {
        public BeatmapData Vanilla { get; set; }
        public CustomBeatmapData Custom { get; set; }
        public bool IsCustom => Custom != null;
    }



    public class Generator//actually generates 90 maps too
    {
        /// <summary>
        /// The preferred bar duration in seconds. The generator will loop the song in bars. 
        /// This is called 'preferred' because this value will change depending on a song's bpm (will be aligned around this value).
        /// Affects the speed at which the rotation occurs. It will not affect the total number of rotations or the range of rotation.
        /// BW CREATED CONFIG ROTATION  SPEED to allow user to set this.
        /// </summary>
        public float PreferredBarDuration { get; set; } = 2.75f;//BW I like 1.5f instead of 1.84f but very similar to changing LimitRotations, 1.0f is too much and 0.2f freezes beat saber  // Calculated from 130 bpm, which is a pretty standard bpm (60 / 130 bpm * 4 whole notes per bar ~= 1.84)

        ///<summary>
        ///The RotationSpeedMultiplier affects the rotation events primarily by modifying the bar length, which in turn influences the frequency of rotation events. If RotationSpeedMultiplier is Increased (e.g., 1.0 → 2.0), The adjusted PreferredBarDuration becomes shorter, leading to shorter bars. Shorter bars mean more frequent rotation events.
        ///</summary>   
        public float RotationSpeedMultiplier { get; set; } = 1.0f;//BW This is a multiplier for PreferredBarDuration
        /// <summary>
        /// The amount of 15 degree rotations before stopping rotation events (rip cable otherwise) (24 is one full 360 rotation)
        /// </summary>
        public int LimitRotations { get; set; } = 28;//BW 28 is equivalent to 360 (24*15) so this is 420 degrees. this is set by Config.Instance.LimitRotations360
        /// <summary>
        /// The amount of rotations before preferring the other direction (24 is one full rotation)
        /// </summary>
        public int BottleneckRotations { get; set; } = 14; //BW 14 default. This is set by LevelUpdatePatcher which sets this to LimitRotations/2
        /// <summary>
        /// Enable the spin effect when no notes are coming.
        /// </summary>
        public bool EnableSpin { get; set; } = false;
        /// <summary>
        /// The total time 1 spin takes in seconds.
        /// </summary>
        public float TotalSpinTime { get; set; } = 0.6f;
        /// <summary>
        /// Minimum amount of seconds between each spin effect.
        /// </summary>
        public float SpinCooldown { get; set; } = 10f;
        /// <summary>
        /// True if you want to generate walls, walls are cool in 360 mode
        /// </summary>
        public bool WallGenerator1 { get; set; } = false;
        /// <summary>
        /// Use to increase or decrease general rotation amount. This doesn't alter the number of rotations - .5 will reduce rotations size by 50% and 2 will double the rotation size.
        /// Set to default for rotations in increments of 15 degrees. 2 would make increments of 30 degrees etc.
        /// </summary>
        //public float RotationAngleMultiplier { get; set; } = 1f;//BW added this to lessen/increase rotation angle amount. 1 means 15 decre
        /// <summary>
        /// Allow crouch obstacles
        /// </summary>
        public bool AllowCrouchWalls { get; set; } = false;//BW added
        /// <summary>
        /// Allow lean obstacles (step to the left, step to the right)
        /// </summary>
        public bool AllowLeanWalls { get; set; } = false;//BW added
        /// <summary>
        /// Removes some notes on a rotation that are hard to reach. Otherwise mirrors (color, index, cutDirection) left hand notes to right hand notes.
        /// </summary>
        //public bool OnlyOneSaber { get; set; } = false;
        /// <summary>
        /// Removes some notes on a rotation that are hard to reach. Otherwise mirrors (color, index, cutDirection) right hand notes to left hand notes.
        /// </summary>
        //public bool LeftHandedOneSaber { get; set; } = false;//BW added

        public static float lastTunnelWallTime; // Initialize this before your loop starts, used to prevent tunnel walls from overlapping
        public static float lastWindowPaneWallTime; // Initialize this before your loop starts, used to prevent tunnel walls from overlapping
        public static bool tunnelWallsHappening; // used to avoid overlapping tunnels with other walls. important since tunnels occur over periods of time whereas most wall groups are at a single moment
        public static bool paneWallsHappening;  // used to avoid overlapping window pans with other walls.

        public static int distantCount; public static int columnCount; public static int rowCount; public static int tunnelCount; public static int gridCount; public static int paneCount;

        public static bool gridWallWide = true;

        public static List<TimeGap> gaps = new List<TimeGap>();

        //v1.39
        public static Version version = new Version(2, 6, 0);

        private static int Floor(float f)
        {
            int i = (int)f;
            return f - i >= 0.999f ? i + 1 : i;
        }

        public GeneratorOutput Generate(EditableCBD eData, float bpm)
        {
            //version = BeatmapDataTransformHelperPatcher.version;

            bool needsRotationLimitAdjustment = false;

            bool isEnabledWalls = Utils.IsEnabledWalls();
            bool isEnabledRotations = Utils.IsEnabledRotations();
            /*
            var booster = new UNUSED_RotationBooster
            {
                Enabled = Config.Instance.AddExtraRotation,
                // optional: tune from Config
                WindowSize = 12,
                TargetAbsPerWindow = 60,
                BoostLimitMin = (int)Config.Instance.RotationGroupLimit - 4,
                BoostLimitMax = (int)Config.Instance.RotationGroupLimit + 4,
                RestLenMin = (int)Config.Instance.RotationGroupSize / 2,
                RestLenMax = (int)Config.Instance.RotationGroupSize
            };
            booster.ResetForNewMap();
            */

            //EnableSpin = Config.Instance.EnableSpin;

            //Plugin.Log.Info($"SongName: {LevelUpdatePatcher.SongName} - Difficulty: {LevelUpdatePatcher.Difficulty}");
            //Plugin.Log.Info($"PBD:  {PreferredBarDuration}");
            //Plugin.Log.Info($"RotationGroupLimit:  {Config.Instance.RotationGroupLimit} RotationGroupSize: {Config.Instance.RotationGroupSize}"); 
            //Plugin.Log.Info(" ");

            // Find the MultiplierSet based on the RotationAngleMultiplier
            //MultiplierSet selectedMultiplierSet = multiplierSets.FirstOrDefault(ms => ms.Multiplier == RotationAngleMultiplier);

            //Plugin.Log.Info($" -- 1 First Note: {bmData.allBeatmapDataItems.OfType<ENoteData>().ToList()[0].time:F}");

            //CustomBeatmapData data = (CustomBeatmapData)bmData.GetCopy(); //v1.40 this will not work now since i changed bmData to CustomBeatmapData.
            //CustomBeatmapData data = DeepCopyCustomBeatmapData(eData); //v1.40 need to test if copying enough custom data!!!!

            //Plugin.Log.Info($" -- 2 First Note: {data.allBeatmapDataItems.OfType<ENoteData>().ToList()[0].time:F}");

            //LinkedList<BeatmapDataItem> dataItems = data.allBeatmapDataItems;

            int originalWallCount = eData.Obstacles.Count;
            Plugin.Log.Info($"Original Wall Count: {originalWallCount}");

            /*
            foreach (ObstacleData obs in dataItems.OfType<ObstacleData>())
            {
                Plugin.Log.Info($"Test for Walls: {obs.time:F} i: {obs.lineIndex} l: {(int)obs.lineLayer} d: {obs.duration} w: {obs.width} h: {obs.height}");
            }
            */

            //List<CustomSliderData> sliders = dataItems.OfType<CustomSliderData>().ToList();//Where((e) => e.sliderType == SliderData.Type.Normal).ToList();//get arcs and chains (original or added by arcitect)

            if (isEnabledWalls && (!Config.Instance.AllowCrouchWalls || !Config.Instance.AllowLeanWalls) || isEnabledWalls || Utils.IsEnabledArcs() || Utils.IsEnabledChains() || Config.Instance.Enable360fyer || Config.Instance.ShowGenerated90)
                WallGenerator.ResetWalls(eData); // reset for each song so variables clear out - do this even if wall generator is off since need to change walls for chains and rotations etc

            //List<SliderData> burstSliders = dataItems.OfType<SliderData>().Where((e) => e.sliderType == SliderData.Type.Burst).ToList();//get original arcs

            //decides if there are at least 12 custom obstacles with _position
            //bool containsCustomWalls = dataItems.Count((e) => e is CustomObstacleData d && (d.customData?.ContainsKey("_position") ?? false)) > 12;
            //Plugin.Log.Info($"Contains custom walls: {containsCustomWalls}");
            int wallGenCount = 0;
            // Amount of rotation events emitted
            int eventCount = 0;
            // Current rotation
            int totalRotation = 0;
            // Moments where a wall should be cut

            /// <summary>
            /// Rotation events by time and rotation step -2, -1, 0, 1, 2
            /// </summary>
            List<(float time, int rotationSteps)> wallCutMoments = new List<(float, int)>();
            List<ERotationEventData> allRotations = eData.RotationEvents.Count == 0 ? new List<ERotationEventData>() : eData.RotationEvents; // added for nonGen360 maps

            // Previous spin direction, false is left, true is right
            bool previousDirectionPositive = true;

            //BOOST Lighting Events
            int boostIteration = 0; // Counter for tracking iterations
            bool boostOn = true; // Initial boolean value

            //Add Extra Rotations
            int r = 1;
            int totalRotationsGroup = 0;
            bool prevRotationPositive = true;
            int newRotation = 0;
            bool addMoreRotations = false;
            int RotationGroupLimit = (int)Config.Instance.RotationGroupLimit;
            int RotationGroupSize = (int)Config.Instance.RotationGroupSize;
            bool alternateParams = false;
            int offSetR = 0;

            lastTunnelWallTime = 0; // must reset this here or will keep last setting from previous song play through
            lastWindowPaneWallTime = 0;
            tunnelWallsHappening = false;
            paneWallsHappening = false;

            distantCount = 0; columnCount = 0; rowCount = 0; tunnelCount = 0; gridCount = 0; paneCount = 0;

            #region Rotate

            List<(ENoteData arcHeadNote, int accumRotation)> arcHeadNoteRotation = new List<(ENoteData, int)>();
            var arcsAlreadyProcessed = new List<ESliderData>();

            int accumRotation = 0;

            int pairStreakRemaining = 0; // remaining mirrored-pair ties in current streak
            int pairStreakSign = +1; // +1 right, -1 left
            //bool pairStreakInit = false;






            // --- Massive Streak Detection Setup ---
            var flexibleRotations = new List<bool>();                     // parallel to allRotations
            var massiveStreaks = new List<(int start, int end)>();// inclusive indices
            int DetectThreshold = 30; // how many same direction rotations to consider a "massive streak"

            int curRunStart = -1, curRunLen = 0, curRunSign = 0;
            int lastProcessedEvtIdx = -1;

            // Notes that can have their rotation direction changed without impacting gameplay
            bool IsFlexible(ENoteData n)
            {
                if (n?.tailNoteArc != null || n?.headNoteArc != null) return false;
                return n.cutDirection == NoteCutDirection.Up
                    || n.cutDirection == NoteCutDirection.Down
                    || n.cutDirection == NoteCutDirection.Any
                    || n.cutDirection == NoteCutDirection.None;
            }
            /// <summary>
            /// Sets start and close of a entire streak of same direction rotations
            /// </summary>  
            void CloseSameDirectionStreak(int endExclusive)
            {
                if (curRunLen >= DetectThreshold && curRunStart >= 0)
                {
                    int start = curRunStart;
                    int end = Math.Max(start, endExclusive - 1);
                    massiveStreaks.Add((start, end));
                    Plugin.Log.Info($"[MassiveStreak] Added streak: {start}–{end} (len={end - start + 1})");
                }
                curRunStart = -1; curRunLen = 0; curRunSign = 0;
            }


            // ------------------------------------------











            int maxRotationStep = (int)Config.Instance.MaxRotationSize / 15;
            int minRotationStep = (int)Config.Instance.MinRotationSize / 15;

            float notespersecond = TransitionPatcher.NotesPerSecond;
            float njs = TransitionPatcher.FinalNoteJumpMovementSpeed;

            // high speed high density maps can have too many 30 degree rotations which seems excessive. 
            if (Config.Instance.ReduceRotationForHighSpeedHighDensityMaps && notespersecond > Config.Instance.HighDensityThreshold && njs > Config.Instance.HighSpeedThreshold)
            {
                maxRotationStep = minRotationStep = 1;
                Plugin.Log.Info($"[Generator] High Speed NJS: {njs} / High Density NPS: {notespersecond} map detected. Setting maxRotationStep and minRotationStep to 1.");
            }

            //Each rotation is 15 degree increments so 24 positive rotations is 360. Negative numbers rotate to the left, positive to the right
            void Rotate(ENoteData note, int rotationStep, bool enableLimit = true) //amount is a rotation step (-3 to 3)
            {
                //Plugin.Log.Info($"Rotate() Called - time: {time:F} rotation: {rotationStep * 15}");
                if (rotationStep == 0)//Allows 4*15=60 degree turn max and -60 degree min -- however amounts are never passed in higher than 3 or lower than -3. I in testing I only see 2 to -2
                    return;

                if (minRotationStep > maxRotationStep)
                    maxRotationStep = minRotationStep;

                if (rotationStep < -maxRotationStep)
                    rotationStep = -maxRotationStep;
                if (rotationStep > maxRotationStep)
                    rotationStep = maxRotationStep;

                if (enableLimit)//always true unless you enableSpin in settings
                {
                    if (totalRotation + rotationStep > LimitRotations)
                        rotationStep = Math.Min(rotationStep, Math.Max(0, LimitRotations - totalRotation));
                    else if (totalRotation + rotationStep < -LimitRotations)
                        rotationStep = Math.Max(rotationStep, Math.Min(0, -(LimitRotations + totalRotation)));
                    if (rotationStep == 0)
                        return;

                    totalRotation += rotationStep;
                    //Plugin.Log.Info($"totalRotation: {totalRotation} at time: {time}.");
                }
                /*
                //decided to add some rotations if they accumulate to 0 during an arc. so call Arcitect.ArcFixForNonGen360(sliders, data) instead of using this section;
                //works by breaking out of the rotate() method to avoid creating a rotation event if there is a slider present)
                if (Config.Instance.ArcFixFull)
                {

                    foreach (SliderData slider in sliders)
                    {
                        //if (slider.sliderType == SliderData.Type.Normal)
                        //    Plugin.Log.Info($"Found a normal slider!"); //no sliders are found on maps that had them in the first place

                        if (time < slider.time - .001f)//if rotation time is at least .001s less than slider.time (so definitely less)
                        {
                            break;//if the rotation is before a slider, it will be before all the remaining sliders too so can break from the foreach // lastRotationBeforeSlider = amount;//get the last rotation before or at the head of the slider
                        }
                        else if (time <= slider.tailTime + .001f)//can be a tiny bit .001s past the tailTime
                        {
                            //Plugin.Log.Info($"----- ARC FIX: Cancelled Rotation time: {time} amount: {amount * 15f}. During Slider time: {slider.time:F} type: {slider.sliderType} tailTime: {slider.tailTime}.");

                            return;//exit rotate() and thus cancel the rotation

                            //Plugin.Log.Info($"--- lastRotationBeforeSlider: {lastRotationBeforeSlider}. During Slider found rotation at: {ro.time:F} amount: {ro.rotation} -- # of rotations inside so far: {sliderRotations.Count}");

                        }

                    }
                }
                */
                if (minRotationStep == 2)
                {
                    if (rotationStep == 1)
                        rotationStep = 2;
                    else if (rotationStep == -1)
                        rotationStep = -2;
                }

                bool matchArcHeadAndTailRotation = false;

                if (matchArcHeadAndTailRotation)
                {
                    if (note.tailNoteArc != null && note.tailNoteArc.headNote != null)
                    {
                        var match = arcHeadNoteRotation.FirstOrDefault(x => x.arcHeadNote == note.tailNoteArc.headNote); // find if there is an arcHeadNote from the list that matches the headNote of the tailNoteArc of the current note
                        if (match.arcHeadNote != null)
                        {
                            rotationStep = (match.accumRotation - accumRotation) / 15;
                            accumRotation = match.accumRotation;

                            Plugin.Log.Info($"[Rotate] --- Found arcTailNote so setting it's rotation to match arcHeadNote at time: {match.arcHeadNote.time:F}.");
                        }

                    }
                    else
                        accumRotation += rotationStep * 15;
                }
                else
                {
                    accumRotation += rotationStep * 15;
                }

                previousDirectionPositive = rotationStep > 0;
                
                eventCount++;
                //wallCutMoments.Add((time, rotationStep));

                allRotations.Add(ERotationEventData.CreateInOrder(note.time, rotationStep * 15));

                if (matchArcHeadAndTailRotation)
                {
                    arcsAlreadyProcessed.Add(note.tailNoteArc); // will remove these from arc list later so they don't waste processing time

                    if (note.headNoteArc != null)
                    {
                        arcHeadNoteRotation.Add((note, accumRotation));
                        //Plugin.Log.Info($"[Rotate] --- Adding arcHeadNote to list for later processing with tail note time: {note.headNoteArc.tailNote.time:F}.");
                    }
                }
                //Plugin.Log.Info($"[Rotate] Rotation added: time: {allRotations.Last().time:F} rotation: {allRotations.Last().rotation} accumRotation: {allRotations.Last().accumRotation}");

                //BW Discord help said to change InsertBeatmapEventData to InsertBeatmapEventDataInOrder which allowed content to be stored to data.
                //data.InsertBeatmapEventDataInOrder(new SpawnRotationBeatmapEventData(time, moment, rotationStep * 15.0f));// * RotationAngleMultiplier));//discord suggestion

                //Plugin.Log.Info($"Rotate Event --- Time: {time:F}, Rotation: {rotationStep * 15} -- Total Rotation: {totalRotation * 15}");


                //Plugin.Log.Info("{");
                //Plugin.Log.Info($"\t_time: {time}");
                //Plugin.Log.Info($"\t_type: {(int)moment+13}");
                //Log.Info($"\t_value: {amount * 15.0f}");
                //Plugin.Log.Info("},");
            }
            #endregion




            float beatDuration = 60f / bpm;

            // Align PreferredBarDuration to beatDuration
            float barLength = beatDuration;

            while (barLength >= PreferredBarDuration * 1.25f / RotationSpeedMultiplier) // RotationSpeedMultiplier causes to emit more (smaller, closer-together) delta rotation events
            {
                barLength /= 2f;
            }
            while (barLength < PreferredBarDuration * 0.75f / RotationSpeedMultiplier)
            {
                barLength *= 2f;
            }

            //Plugin.Log.Info($"beatDuration: {beatDuration} barLength: {barLength}");
            //Plugin.Log.Info($"PreferredBarDuration: {PreferredBarDuration} * RotationSpeedMultiplier: {RotationSpeedMultiplier} = {PreferredBarDuration/RotationSpeedMultiplier}");
            //Plugin.Log.Info($"RotationAngleMultiplier: {RotationAngleMultiplier}");

            //All in seconds
            List<ENoteData> notesAndBombs = eData.ColorNotes;
            Plugin.Log.Info($"Notes Count: {notesAndBombs.Count}"); // BW added to see how many notes are in the map
            notesAndBombs.AddRange(eData.BombNotes);//List<ENoteData> notes = data.GetBeatmapDataItems<ENoteData>(0).ToList(); // NOTES CONTAINS NOTES AND BOMBS

            notesAndBombs.Sort((a, b) => a.time.CompareTo(b.time));

            Plugin.Log.Info($"Notes Count after adding bombs: {notesAndBombs.Count}"); 
            /*
            foreach (var n in notesAndBombs)
            {
                Plugin.Log.Info($"Note: Time: {n.time:F} Line: {n.line} Layer: {n.layer} Type: {n.gameplayType} Color: {n.colorType} CutDir: {n.cutDirection}");
            }
            */

            List<ENoteData> notesInBar = new List<ENoteData>(); // CONTAINS NOTES AND BOMBS
            List<ENoteData> notesInBarBeat = new List<ENoteData>(); // CONTAINS NOTES AND BOMBS

            // Moved to Patcher to avoid chains having improperly placed segments
            /*
            //Should remove notes first before figuring out rotations etc which are based on notes
            if (StandardLevelDetailViewPatch.BeatSage && Config.Instance.CleanBeatSage)
                BeatSageCleanUp.Clean(data);//(notes, obstacles) = BeatSageCleanUp.Clean(data);

            // Moved to Patcher
            if ((sliders.Count == 0 && Config.Instance.EnableArcs) || (burstSliders.Count == 0 && Config.Instance.EnableChains))
                sliders = Arcitect.CreateSliders(data, notes, sliders, burstSliders);//update data and update sliders list for arcfix later
            */

            // Align bars to first note, the first note (almost always) identifies the start of the first bar
            float firstBeatmapNoteTime = notesAndBombs[0].time;

#if DEBUG
				Plugin.Log.Info($"Setup bpm={bpm} beatDuration={beatDuration} barLength={barLength} firstNoteTime={firstBeatmapNoteTime} firstnoteGameplayType={notes[0].gameplayType} firstnoteColorType={notes[0].colorType}");
#endif
            // get all left and all right notes (color indenpendent)
            var notesBySideLeft = new List<ENoteData>();
            var notesBySideRight = new List<ENoteData>();
            foreach (var n in notesAndBombs)
            {
                if (n.line <= 1) notesBySideLeft.Add(n);   // columns 0 or 1
                else if (n.line >= 2) notesBySideRight.Add(n); // columns 2 or 3
            }


            // Moving cursors (indexes) into those lists; start at 0.
            int leftCur = 0, rightCur = 0;


            static bool IsRightish(NoteCutDirection d) =>
                d == NoteCutDirection.Right || d == NoteCutDirection.UpRight || d == NoteCutDirection.DownRight;

            static bool IsLeftish(NoteCutDirection d) =>
                d == NoteCutDirection.Left || d == NoteCutDirection.UpLeft || d == NoteCutDirection.DownLeft;

            static int DirPolarity(NoteCutDirection d)
            {
                if (IsRightish(d)) return +1;
                if (IsLeftish(d)) return -1;
                return 0; // Any/Up/Down -> neutral
            }


            Stopwatch stopwatch = new Stopwatch();


            #region Main Loop

            stopwatch.Restart();

            for (int i = 0; i < notesAndBombs.Count;)
            {
                float currentBarStart = Floor((notesAndBombs[i].time - firstBeatmapNoteTime) / barLength) * barLength;
                float currentBarEnd = currentBarStart + barLength - 0.001f;

                //Plugin.Log.Info($"Setup currentBarStart={currentBarStart} currentBarEnd={currentBarEnd}");
                //if (notes[i].time > 148f && notes[i].time < 155f)
                //Plugin.Log.Info(
                //             $"Main Loop Note {i}: {notes[i].time} {notes[i].lineIndex} {notes[i].cutDirection} -----------------------------------------------------------");

                notesInBar.Clear();
                for (; i < notesAndBombs.Count && notesAndBombs[i].time - firstBeatmapNoteTime < currentBarEnd; i++)
                {
                    //if (notes[i].time > 148f && notes[i].time < 155f)
                    //    Plugin.Log.Info($"notesInBar {i} --- Time: {notes[i].time:F} Index: {notes[i].lineIndex} CutDirection: {notes[i].cutDirection}");
                    notesInBar.Add(notesAndBombs[i]);
                }
                //if (notes[i].time > 148f && notes[i].time < 155f)
                //Plugin.Log.Info($"notesInBar count: {notesInBar.Count}");

                if (notesInBar.Count == 0)
                    continue;

                // Divide the current bar in x pieces (or notes), for each piece, a rotation event CAN be emitted
                // Is calculated from the amount of notes in the current bar
                // barDivider | rotations
                // 0          | . . . . (no rotations)
                // 1          | r . . . (only on first beat)
                // 2          | r . r . (on first and third beat)
                // 4          | r r r r 
                // 8          |brrrrrrrr
                // ...        | ...
                // TODO: Create formula out of these if statements
                int barDivider;
                if (notesInBar.Count >= 58)
                    barDivider = 0; // Too mush notes, do not rotate
                else if (notesInBar.Count >= 38)
                    barDivider = 1;
                else if (notesInBar.Count >= 26)
                    barDivider = 2;
                else if (notesInBar.Count >= 8)
                    barDivider = 4;
                else
                    barDivider = 8;

                //Plugin.Log.Info($"notesInBar.Count: {notesInBar.Count} barDivider: {barDivider}");

                if (barDivider <= 0)
                    continue;

#if DEBUG
					//StringBuilder builder = new StringBuilder();
#endif

                // Iterate all the notes in the current bar in barDivider pieces (bar is split in barDivider pieces)
                float dividedBarLength = barLength / barDivider;
                for (int j = 0, k = 0; j < barDivider && k < notesInBar.Count; j++)
                {
                    //if (notes[i].time > 148f && notes[i].time < 155f)
                    //    Plugin.Log.Info($"notesInBarBeat Loop ------------------------------------------");
                    notesInBarBeat.Clear();
                    for (;
                         k < notesInBar.Count && Floor((notesInBar[k].time - firstBeatmapNoteTime - currentBarStart) /
                                                       dividedBarLength) == j;
                         k++)
                    {
                        notesInBarBeat.Add(notesInBar[k]);
                        //if (notes[i].time > 148f && notes[i].time < 155f)
                        //    Plugin.Log.Info($"notesInBarBeat {k} --- Time: {notesInBar[k].time:F} Index: {notesInBar[k].lineIndex} CutDirection: {notesInBar[k].cutDirection}");
                    }

                    //if (notes[i].time > 148f && notes[i].time < 155f)
                    //    Plugin.Log.Info($"notesInBarBeat count: {notesInBarBeat.Count}");
#if DEBUG
						// Debug purpose
						//if (j != 0)
						//	builder.Append(',');
						//builder.Append(notesInBarBeat.Count);
#endif

                    if (notesInBarBeat.Count == 0)
                        continue;

                    float currentBarBeatStart = firstBeatmapNoteTime + currentBarStart + j * dividedBarLength; //. BW Testing this since creates walls touching notes sometimes *************

                    if (currentBarBeatStart > lastTunnelWallTime)
                        tunnelWallsHappening = false;
                    if (currentBarBeatStart > lastWindowPaneWallTime)
                        paneWallsHappening = false;

                    ENoteData lastNote = notesInBarBeat[notesInBarBeat.Count - 1];

                    // Determine the rotation direction based on the last notes in the bar
                    IEnumerable<ENoteData> lastNotes =
                        notesInBarBeat.Where((e) => Math.Abs(e.time - lastNote.time) < 0.005f);

                    // Amount of notes pointing to the left/right
                    int leftCount = lastNotes.Count((e) =>
                        e.line <= 1 || e.cutDirection == NoteCutDirection.Left ||
                        e.cutDirection == NoteCutDirection.UpLeft || e.cutDirection == NoteCutDirection.DownLeft);
                    int rightCount = lastNotes.Count((e) =>
                        e.line >= 2 || e.cutDirection == NoteCutDirection.Right ||
                        e.cutDirection == NoteCutDirection.UpRight || e.cutDirection == NoteCutDirection.DownRight);


                    // added this to look ahead for wall generator to see if there is a note after the last note in this bar segment that may block a wall generation
                    ENoteData afterLastNote = (k < notesInBar.Count ? notesInBar[k] : i < notesAndBombs.Count ? notesAndBombs[i] : null);

                    // Determine amount to rotate at once
                    int rotationCount = 1;
                    if (afterLastNote != null)
                    {
                        double barLength8thRound = Math.Round(barLength / 8, 4);
                        double timeDiff = Math.Round(afterLastNote.time - lastNote.time, 4); //BW without any rounding or rounding to 5 or more digits still produces a different rotation between exe and plugin.

                        //double epsilon = 0.00000001;
                        if (notesInBarBeat.Count >= 1)
                        {
                            if (timeDiff >= barLength)
                                rotationCount = 3;
                            else if
                                (timeDiff >=
                                 barLength8thRound) //barLength / 8)BW ---- This is the place where exe vs plugin maps will differ due to rounding between the 2 applications. i added rounding to 4 digits in order to match the output between the 2
                                rotationCount = 2;

                        }
                    }

                    int rotationStep = 0;
                    if (leftCount > rightCount)
                    {
                        // Most of the notes are pointing to the left, rotate to the left
                        rotationStep = -rotationCount;
                    }
                    else if (rightCount > leftCount)
                    {
                        // Most of the notes are pointing to the right, rotate to the right
                        rotationStep = rotationCount;
                    }
                    else
                    {
                        int desiredSign = 0;   // ✅ initialize to something safe

                        bool handledByPairLogic = false;

                        // Only analyze pairs if there are ≥2 notes at the same time
                        if (lastNotes.Count() >= 2)
                        {
                            var lastA = lastNotes.Where(n => n.colorType == ColorType.ColorA).ToList();
                            var lastB = lastNotes.Where(n => n.colorType == ColorType.ColorB).ToList();
                            bool hasColorPair = lastA.Count > 0 && lastB.Count > 0;

                            if (hasColorPair)
                            {
                                // prefer non-neutral reps
                                ENoteData pickA = lastA.FirstOrDefault(n => DirPolarity(n.cutDirection) != 0) ?? lastA[0];
                                ENoteData pickB = lastB.FirstOrDefault(n => DirPolarity(n.cutDirection) != 0) ?? lastB[0];
                                int polA = DirPolarity(pickA.cutDirection);
                                int polB = DirPolarity(pickB.cutDirection);

                                // mirrored if +1/-1 or both 0 (Any/Up/Down)
                                bool isOppositeByColor = (polA + polB) == 0;

                                if (isOppositeByColor)
                                {
                                    // --- NEW: mirrored-pair streak controller (deterministic 2..12) ---
                                    int barIdx = (int)Math.Floor((notesInBarBeat[0].time - firstBeatmapNoteTime) / barLength);

                                    if (pairStreakRemaining <= 0)
                                    {
                                        // flip direction between streaks to avoid chaining long runs one way
                                        pairStreakSign = -pairStreakSign;
                                        int ran = (barIdx * 7 + j * 11) & 0x7fffffff; // simple deterministic mix
                                        pairStreakRemaining = 2 + (ran % 11);         // [2..12]
                                    }

                                    desiredSign = pairStreakSign;
                                    pairStreakRemaining--;
                                    handledByPairLogic = true;
                                }
                            }
                        }

                        if (!handledByPairLogic)
                        {
                            // --- ORIGINAL tie behavior unchanged ---
                            if (totalRotation >= BottleneckRotations)
                                desiredSign = -1;
                            else if (totalRotation <= -BottleneckRotations)
                                desiredSign = +1;
                            else
                                desiredSign = previousDirectionPositive ? +1 : -1;
                        }

                        rotationStep = desiredSign * rotationCount;

                        // keep your existing limit clamps here (unchanged)

                        if (rotationStep != 0)
                            previousDirectionPositive = rotationStep > 0;
                    }






                    if (isEnabledRotations)
                    {
                        //Plugin.Log.Info($"Rotation will be--------------: {rotation *15}");

                        if (totalRotation >= BottleneckRotations && rotationCount > 1)
                        {
                            rotationCount = 1;
                        }
                        else if (totalRotation <= -BottleneckRotations && rotationCount < -1)
                        {
                            rotationCount = -1;
                        }

                        if (totalRotation >= LimitRotations - 1 && rotationCount > 0)
                        {
                            rotationCount = -rotationCount;
                        }
                        else if (totalRotation <= -LimitRotations + 1 && rotationCount < 0)
                        {
                            rotationCount = -rotationCount;
                        }

                        #region AddExtraRotations

                        //############################################################################
                        //BW had to add more rotations directly in the main loop. tried it outside this main loop. the problem with being outside the loop is you cannot decide if a map is really low on rotations until after the map is finished.
                        //add more rotation to maps without much rotation. If there are few rotations, look for directionless notes up/down/dot/bomb and make their rotation direction the same as the previous direction so that there will be increased totalRotation.
                        //Once rotation steps pass the RotationGroupLimit, make this inactive. Stay inactive for RotationGroupSize number of rotations and if there are few rotations while off, activate this again.


                        if (Config.Instance.AddExtraRotation)// && !Config.Instance.AddExtraRotationV2)
                        {
                            if (addMoreRotations) //this stays on until passes the rotation limit
                            {
                                if (Math.Abs(totalRotationsGroup) < Math.Abs(RotationGroupLimit))
                                {
                                    if (lastNote.cutDirection == NoteCutDirection.Up ||
                                        lastNote.cutDirection == NoteCutDirection.Down ||
                                        lastNote.cutDirection == NoteCutDirection.Any ||
                                        lastNote.cutDirection == NoteCutDirection.None) //only change rotation if using a non-directional note. if remove this will allow a lot more rotations
                                    {
                                        if (prevRotationPositive) //keep direction the same as the previous note
                                            newRotation = Math.Abs(rotationStep);
                                        else
                                            newRotation = -Math.Abs(rotationStep);

                                        //if (newRotation != rotationStep)
                                        //    Plugin.Log.Info($"[AddExtraRotation] lastNote time: {lastNote.time} r: {r} Old Rotation: {rotationStep} New Rotation: {newRotation}");// totalRotationsGroup: {totalRotationsGroup}");

                                        rotationStep = newRotation;

                                        totalRotationsGroup += rotationStep;
                                    }

                                }
                                else //has now passed the rotation limit now
                                {
                                    addMoreRotations = false;

                                    totalRotationsGroup = 0;

                                    //Plugin.Log.Info($"[AddExtraRotation] Change to NOT ACTIVE since passed the limit!!! RotationGroupLimit: {RotationGroupLimit}\t totalRotationsGroup: {totalRotationsGroup}");

                                    offSetR =
                                        r; //need this since when passes the limit, r may be close or equal to being a multiple of RotationGroupSize. that means it could be active soon again. so need to offset r so it will stay off for RotationGroupSize rotations.(r - offSetR) will be 0 on first rotation...
                                }

                            }
                            else //inactive
                            {
                                totalRotationsGroup += rotationStep;

                                if ((r - offSetR) % RotationGroupSize ==
                                    0) // after RotationGroupSize - offset number of iterations, this will check if rotations are over the limit
                                {
                                    if (Math.Abs(totalRotationsGroup) >=
                                        Math.Abs(
                                            RotationGroupLimit)) //if the total rotations was over the limit, stay inactive
                                    {
                                        addMoreRotations = false;

                                        //Plugin.Log.Info($"[AddExtraRotation] Continue to be NOT ACTIVE: Inactive rotations are over the limit so stay inactive for {RotationGroupSize} rotations. RotationGroupLimit: {RotationGroupLimit}\t RotationGroupSize set to: 0 ++++++++++++++++++++++++++++++++++++++++++++++++");
                                    }
                                    else //if the total rotations was under the limit, activate more rotations
                                    {
                                        addMoreRotations = true;

                                        if (alternateParams)
                                        {
                                            RotationGroupLimit +=
                                                4; //change the limit size for variety //could not alter RotationGroupSize since causing looping problem
                                        }
                                        else
                                        {
                                            RotationGroupLimit -=
                                                4; //change the limit size for variety //could not alter RotationGroupSize since causing looping problem
                                        }

                                        alternateParams =
                                            !alternateParams; // Toggles every other time addMoreRotations is true

                                        //Plugin.Log.Info($"[AddExtraRotation] ACTIVE:     RotationGroupLimit: {RotationGroupLimit}\t RotationGroupSize: {RotationGroupSize}------------------------------------------------");
                                    }

                                    totalRotationsGroup = 0;

                                }
                            }

                            if (rotationStep > 0)
                                prevRotationPositive = true;
                            else
                                prevRotationPositive = false;

                        }
                        /*
                        else if (Config.Instance.AddExtraRotation && Config.Instance.AddExtraRotationV2)
                        {
                            // 2) If you want to pause boosting near arcs/per-object rotations, gate it:
                            //bool inArcWindow = ArcCovers(note.time) || PerObjectRotationNearby(note.time);
                            //bool eligibleWindow = true;// !inArcWindow;

                            // 3) Let the booster optionally nudge the *sign* / magnitude for directionless notes:
                            booster.MaybeBoost(lastNote, ref rotationStep);
                        }
                        */
                        //############################################################################

                        #endregion

                        //***********************************
                        //Finally rotate - possible values here are -3,-2,-1,0,1,2,3 but in testing I only see -2 to 2
                        //The condition for setting rotationCount to 3 is that timeDiff (the time difference between afterLastNote and lastNote) is greater than or equal to barLength. If your test data rarely or never satisfies this condition, you won't see rotation values of -3 or 3.
                        //Similarly, the condition for setting rotationCount to 2 is that timeDiff is greater than or equal to barLength / 8. If this condition is rarely met in your test cases, it would explain why you mostly see rotation values of - 2, -1, 0, 1, or 2.

                        //Plugin.Log.Info($"Rotate() r: {r}\t Time: {Math.Round(lastNote.time, 2).ToString("0.00")}\t Rotation Step:\t {rotation}\t lastNoteDir:\t {lastNote.cutDirection}\t totalRotation:\t {totalRotation}\t totalRotationsGroup:\t {totalRotationsGroup}");// Type: {(int)SpawnRotationBeatmapEventData.SpawnRotationEventType.Late}"); \t Beat: {lastNote.time * bpm / 60f}


                        //RotationStep can get a value of 1 when notes are close together in a bar.
                        //Or a value of 2 when there is A noticeable gap between notes but not a full bar.
                        //Or a value of 3 but only if afterLastNote.time - lastNote.time >= barLength. This means a full bar of time with no notes in between.
                        //In most maps, rotationStep = 3 is unlikely unless there are big gaps between note groups, like in slower maps or maps with intentional large gaps.


                        /*
                        if (rotationStep > 0 && sameDirectionStreak >= 0)
                            sameDirectionStreak++;
                        else if (rotationStep < 0 && sameDirectionStreak <= 0)
                            sameDirectionStreak--;
                        else if (rotationStep > 0)
                            sameDirectionStreak = 1;
                        else if (rotationStep < 0)
                            sameDirectionStreak = -1;
                        else sameDirectionStreak = 0;

                        if (Math.Abs(sameDirectionStreak) >= maxSameDirectionStreak && //too many in same direction
                           (lastNote.cutDirection == NoteCutDirection.Up ||
                            lastNote.cutDirection == NoteCutDirection.Down ||
                            lastNote.cutDirection == NoteCutDirection.Any ||
                            lastNote.cutDirection == NoteCutDirection.None)) //only change rotation if using a non-directional note. if remove this will allow a lot more rotations
                        {
                            Plugin.Log.Info($"[Generator] Same Direction Streak: {sameDirectionStreak} (Max: {maxSameDirectionStreak})");

                            rotationStep *= -1; //reverse direction
                            if (rotationStep >= 0)
                                sameDirectionStreak = 1;
                            else if (rotationStep <= 0)
                                sameDirectionStreak = -1;
                            if (r % 3 == 0 && maxSameDirectionStreak <= Config.Instance.MaxSameDirectionStreak + 5) maxSameDirectionStreak += 5;
                            else if (r % 5 == 0 && maxSameDirectionStreak > Config.Instance.MaxSameDirectionStreak - 5) maxSameDirectionStreak -= 5;
                            else maxSameDirectionStreak = Config.Instance.MaxSameDirectionStreak;
                        }
                        */



                        






                        Rotate(lastNote, rotationStep); //lastNote.time, rotationStep);

                        /*
                        if (Config.Instance.AddExtraRotation && Config.Instance.AddExtraRotationV2)
                        {
                            // 5) Tell the booster what actually happened so it can update its window/net drift:
                            booster.TrackApplied(rotationStep);
                        }
                        */
                        r++;

                        // --- Streak detector driven by emitted events ---
                        int newIdx = allRotations.Count - 1;
                        if (newIdx > lastProcessedEvtIdx) // means Rotate() actually emitted an event (rotationStep != 0 *and* not clamped to 0)
                        {
                            // record flex flag aligned to each lastNote
                            flexibleRotations.Add(IsFlexible(lastNote));

                            // use the sign of the emitted EVENT (not rotationStep)
                            int emittedDeg = allRotations[newIdx].rotation; // +/- 15, 30, ...
                            int sgn = Math.Sign(emittedDeg);

                            if (curRunLen == 0)
                            {
                                curRunStart = newIdx;
                                curRunSign = sgn;
                                curRunLen = 1;
                            }
                            else if (sgn == curRunSign)
                            {
                                curRunLen++;
                            }
                            else
                            {
                                // sign flipped → close previous run at newIdx (exclusive) and start a new one at newIdx
                                CloseSameDirectionStreak(newIdx);
                                curRunStart = newIdx;
                                curRunSign = sgn;
                                curRunLen = 1;
                            }

                            // optional debug every N events
                            //if ((newIdx < 5) || (newIdx % 50 == 0))
                            //    Plugin.Log.Info($"[MassiveStreakDbg]  note Idx={newIdx}, time={allRotations[newIdx].time:F}, deg={emittedDeg}, flex={eventFlex[newIdx]}");

                            lastProcessedEvtIdx = newIdx;
                        }

                    }


                    //Plugin.Log.Info($"Total Rotations: {totalRotation*15} Time: {lastNote.time:F} Rotation: {rotation*15}");


                    #region Boost Lighting
                    // Works on standard and 360 gen and non-gen maps
                    //Creates a boost lighting event. if ON, will set color left to boost color left new color etc. Will only boost a color scheme that has boost colors set so works primarily with COLORS > OVERRIDE DEFAULT COLORS. Or an authors color scheme must have boost colors set (that will probably never happen since they will have boost colors set if they use boost events).

                    if (Config.Instance.BoostLighting && !TransitionPatcher.AlreadyUsingEnvColorBoost)
                    {
                        boostIteration++;
                        if (boostIteration == 24 || boostIteration == 29)//33)//5 & 13 is good but frequent
                        {
                            var newBoostEvent = EColorBoostEvent.Create(lastNote.time, boostOn);
                            eData.ColorBoostEvents.Add(newBoostEvent);
                            //data.InsertBeatmapEventDataInOrder(new CustomColorBoostBeatmapEventData(lastNote.time, boostOn, new CustomData(), version));
                            //Plugin.Log.Info($"Boost Light! --- Time: {time} On: {boostOn}");
                            boostOn = !boostOn; // Toggle the boolean
                        }

                        // Reset the iteration counter if it reaches 13
                        if (boostIteration == 33) { boostIteration = 0; }
                    }
                    #endregion

                    //This is not good. It creates a combination of left and right notes makeing them often swing in the same direction consecutively.
                    #region OneSaber UNUSED
                    /*
                    if (OnlyOneSaber)
                    {
                        foreach (NoteData nd in notesInBarBeat)
                        {
                            if (LeftHandedOneSaber)
                            {
                                if (nd.colorType == (rotation > 0 ? ColorType.ColorA : ColorType.ColorB)) // If rotation is positive (> 0), it returns ColorType.ColorA otherwise ColorType.ColorB. remove the note if it matches that color since may be hard to hit
                                {
                                    // Remove note
                                    dataItems.Remove(nd);
                                }
                                else
                                {
                                    // mirror right hand note to ColorA
                                    if (nd.colorType == ColorType.ColorB)
                                    {
                                        nd.Mirror(data.numberOfLines);
                                    }
                                }
                            }
                            else
                            {
                                if (nd.colorType == (rotation < 0 ? ColorType.ColorB : ColorType.ColorA)) // If rotation is negative (< 0), it returns ColorType.ColorB otherwise ColorType.ColorA. remove the note if it matches that color since may be hard to hit
                                {
                                    // Remove note
                                    dataItems.Remove(nd);
                                }
                                else
                                {
                                    // mirror left hand note to ColorA
                                    if (nd.colorType == ColorType.ColorA)
                                    {
                                        nd.Mirror(data.numberOfLines);
                                    }
                                }
                            }
                        }
                    }
                    */
                    #endregion

                    /*
                    if (notes[i].time > 148f && notes[i].time < 155f)
                    {
                        foreach (NoteData note in notesInBar)
                        {
                            Plugin.Log.Info(
                                $"Final notesInBar Note: {note.time} {note.lineIndex} {note.cutDirection} ");
                        }

                        foreach (NoteData note in notesInBarBeat)
                        {
                            Plugin.Log.Info(
                                $"Final notesInBarBeat Note: {note.time} {note.lineIndex} {note.cutDirection} ");
                        }

                        Plugin.Log.Info(
                            $"Walltime: {currentBarBeatStart} ");
                    }

                    */

                    if (isEnabledWalls && originalWallCount < 5000)// && !BeatmapDataTransformHelperPatcher.NoodleProblemNotes && !BeatmapDataTransformHelperPatcher.NoodleProblemObstacles)
                    {
                        float barEnd = firstBeatmapNoteTime + currentBarEnd;

                        // advance left cursor until time >= barEndAbs
                        while (leftCur < notesBySideLeft.Count && notesBySideLeft[leftCur].time < barEnd)
                            leftCur++;

                        // advance right cursor until time >= barEndAbs
                        while (rightCur < notesBySideRight.Count && notesBySideRight[rightCur].time < barEnd)
                            rightCur++;

                        // These are your guard note times to help make sure a wall does dip into this note
                        float nextNoteLeftTime  = (leftCur < notesBySideLeft.Count) ? notesBySideLeft[leftCur].time : -1f;
                        float nextNoteRightTime = (rightCur < notesBySideRight.Count) ? notesBySideRight[rightCur].time : -1f;

                        WallGenerator.WallGen(i, currentBarBeatStart, dividedBarLength, afterLastNote, notesInBarBeat, notesInBar, nextNoteLeftTime, nextNoteRightTime);
                        wallGenCount++;
                    }

#if DEBUG
						//Plugin.Log.Info($"[{currentBarBeatStart}] Rotate {rotation} (c={notesInBarBeat.Count},lc={leftCount},rc={rightCount},lastNotes={lastNotes.Count()},rotationTime={lastNote.time + 0.01f},afterLastNote={afterLastNote?.time:F},rotationCount={rotationCount})");
#endif
                }


#if DEBUG
					//Plugin.Log.Info($"[{currentBarStart + firstBeatmapNoteTime}({(currentBarStart + firstBeatmapNoteTime) / beatDuration}) -> {currentBarEnd + firstBeatmapNoteTime}({(currentBarEnd + firstBeatmapNoteTime) / beatDuration})] count={notesInBar.Count} segments={builder} barDiviver={barDivider}");
#endif
            }//End main for loop over all notes---------------------------------------------------------------

            CloseSameDirectionStreak(allRotations.Count); // close trailing run safely
            Plugin.Log.Info($"[MassiveStreak] Total detected streaks: {massiveStreaks.Count}");



            //AddRotationsIntoMassiveStreak(allRotations, flexibleRotations, massiveStreaks);



            // CreateArcTailNoteRotations(); // Testing doing all arc work with in ArcFix() instead of here


            // REMOVE!!!!????? not needed?
            /*
            void CreateArcTailNoteRotations() //If an arc head note has a rotation, make the arc tail note have the same rotation as the head note. not all tail notes get a rotation event typically since rotation events are only created for the last note in a group of notes
            {   
                
                //foreach (var rot in allRotations)
                //{
                //    Plugin.Log.Info($"allRotations - Time: {rot.time:F} - Rotation: {rot.rotation} - Accum Rotation: {rot.accumRotation}");
                //}
                

                Plugin.Log.Info($"[RotTail] Start ----------------------");

                // if arcsAlreadyProcessed are the same references that are in eData.Arcs
                //var processed = new HashSet<ESliderData>(arcsAlreadyProcessed); // reference-based
                //var potentialArcsToProcess = eData.Arcs
                //    .Where(a => !processed.Contains(a))
                //    .OrderBy(a => a.time)
                //    .ToList();
                // Plugin.Log.Info($"[RotTail] Found {potentialArcsToProcess.Count} arcs with tailNotes that may need rotation events added (removed {processed.Count} already processed arcs).");

                // Pre-group rotation events by rounded time
                var sameTimeLookup = allRotations
                     .GroupBy(ev => MathF.Round(ev.time, 3))
                     .ToDictionary(g => g.Key, g => g.First()); // ensure only one event per key

                // Extract a time array for fast binary search on "previous event"
                var rotTimes = allRotations.Select(ev => ev.time).ToArray();

                int FindPrevEventIndex(float t)
                {
                    // returns index of the last event with time < t (with EPS)
                    int lo = 0, hi = rotTimes.Length - 1, ans = -1;
                    while (lo <= hi)
                    {
                        int mid = (lo + hi) >> 1;
                        if (rotTimes[mid] <= t - 0.001f) { ans = mid; lo = mid + 1; }
                        else hi = mid - 1;
                    }
                    return ans; // -1 means "none before t"
                }

                int GetPrevAccum(float t)
                {
                    int idx = FindPrevEventIndex(t);
                    return idx >= 0 ? allRotations[idx].accumRotation : 0;
                }

                int count = 0;

                // Now check arcs
                foreach (var arc in eData.Arcs)
                {
                    float tailTime = arc.tailTime;
                    float tailKey = MathF.Round(tailTime, 3);

                    if (sameTimeLookup.TryGetValue(tailKey, out var existingTail))
                    {
                        Plugin.Log.Info($"[RotTail] --- Found rotation on arc tailNote at {tailTime:F3}s → delta:{existingTail.rotation}, accum:{existingTail.accumRotation} so don't need to add a new rotation.");
                        continue; // do not modify if one already exists
                    }

                    float headTime = arc.time;
                    float headKey = MathF.Round(headTime, 3);

                    int? desiredAccum = null;

                    if (sameTimeLookup.TryGetValue(headKey, out var existingHead))
                    {
                        Plugin.Log.Info($"[RotTail] - Found rotation on arc headNote at {headTime:F3}s → delta:{existingHead.rotation}, accum:{existingHead.accumRotation} so use it fix tailNote rotation at {arc.tailTime:F}.");
                        desiredAccum = existingHead.accumRotation;
                    }
                    else
                    {
                        // Accumulation just before the head time
                        desiredAccum = GetPrevAccum(headTime);
                    }

                    // prev accum just BEFORE the TAIL time
                    int? prevAccumBeforeTail = GetPrevAccum(tailTime);

                    if (desiredAccum == null || prevAccumBeforeTail == null)
                    {
                        Plugin.Log.Warn($"[RotTail] Cannot determine desired or previous accumulation for arc tailNote at {tailTime:F3}s, skipping.");
                        continue;
                    }

                    // delta needed at tail to reach desired accum
                    int newDelta = desiredAccum.Value - prevAccumBeforeTail.Value;

                    // Construct explicitly (don’t use CreateInOrder here)
                    var newEvt = new ERotationEventData
                    {
                        time = tailTime,
                        rotation = newDelta,
                        accumRotation = prevAccumBeforeTail.Value + newDelta
                    };
                    allRotations.Add(newEvt);
                    // Keep the single-key invariant up-to-date for subsequent iterations
                    sameTimeLookup[tailKey] = newEvt;
                    count++;

                    Plugin.Log.Info($"[RotTail] {count} Inserted new rotation event for arc tailNote at: {newEvt.time:F}s → delta:{newEvt.rotation} accum: {newEvt.accumRotation}, prevAccum:{prevAccumBeforeTail}, desiredAccum:{desiredAccum} (from arc head at: {arc.time:F}");
                }

                allRotations = allRotations.OrderBy(ev => ev.time).ToList();

                // Recompute accumRotation for all events since we may have inserted new ones in the middle (this is needed)
                int prevRot = 0;
                foreach (var rot in allRotations)
                {
                    rot.accumRotation = prevRot + rot.rotation;
                    prevRot = rot.accumRotation;
                }

                Plugin.Log.Info($"[RotTail] Finished processing arcs tails, total new rotation events added: {count} for a full total of {allRotations.Count} --------------- ");
            }
            */
            Plugin.Log.Info($"1 Rotation List (after main loop) Count: {allRotations.Count}");

            (float accumRot, float time) high = (0,0);
            (float accumRot, float time) low = (0, 0);
            int endRot = 0;

            foreach (var rot in allRotations)
            {
                endRot = rot.accumRotation;
                if (rot.accumRotation < low.accumRot)
                    low = (rot.accumRotation, rot.time);
                else if (rot.accumRotation > high.accumRot)
                    high = (rot.accumRotation, rot.time);
                //Plugin.Log.Info($"1 Rotation - Time: {rot.time:F} - Rotation: {rot.rotation} - Total Rotation: {rot.accumRotation}");
            }
            Plugin.Log.Info($"1 Rotation Events - Wireless360: {Config.Instance.Wireless360} - LimitRotations360: {Config.Instance.LimitRotations360} - Largest Neg Rot: {low.accumRot} Time: {low.time:F} - Largest Pos Rot: {high.accumRot} Time: {high.time:F} - Final Rotation: {endRot}");

            Plugin.Log.Info(
                $" ------- Main Loop time elapsed: {stopwatch.ElapsedMilliseconds / 1000.0:F} Called WallGenerator {wallGenCount} times.");
            stopwatch.Stop();

            #endregion

            
            /*
            // Remove bombs from walls
            // Do this before add tons of extension walls. Test if can add this somewhere else where a loop is already being done such as when add the wall in the first place --- also doesn't check for bombs on the other side of walls -
            foreach (ObstacleData obs in data.allBeatmapDataItems.OfType<ObstacleData>())
            {
                if (bomb.time >= obs.time + WallGenerator.WallFrontCut &&
                    bomb.time <= obs.time + obs.duration + WallGenerator.WallBackCut)
                {
                    if ((bomb.lineIndex >= obs.lineIndex && bomb.lineIndex <= obs.lineIndex + obs.width)
                        && (bomb.noteLineLayer >= obs.lineLayer)) // may not work for mapping extension lineIndex and width test!
                    {
                        dataItems.Remove(bomb);
                        //Plugin.Log.Info($"Remove Bomb: {nd.time:F}");
                    }
                }
            }
            */
            //List<(float time, int rotation)> finalRotations = allRotations.ToList(); // is a reference if don't use ToList()
            
            //FIX!!!!!!!!!!!!!!!!!!!!!!!!!
            //if (allRotations.Count > 0)//FIX!!!!! not using the in-memory 360fyer map. its using the standard map. && TransitionPatcher.characteristicSerializedName == "Generated360Degree")
            //{
            //    eData.RotationEvents = allRotations;
            //    Plugin.Log.Info($"Rotation Count after add allRotations (with count: {allRotations.Count()}): {eData.RotationEvents.Count}");
            //}
            // otherwise the map was a non-gen 360 or 90 map with its own rotations so don't replace them.



            #region Optimize FOV

            if (Utils.IsEnabledFOV() && allRotations.Count > 0) // use this for nonGen360 maps with wall gen since old 360fyer generated maps have wild rotations that cause walls to reverse through the frame. this will not help some walls blocking player vision that are built into 360fyer old generated output
            {
                //if (allRotations.Count == 0 &&
                //    (TransitionPatcher.characteristicSerializedName == "360Degree" || TransitionPatcher.characteristicSerializedName == "90Degree")) // if no rotations, then must be nonGen360 map so need to populate finalRotations
               // {


                    //v1.34
                    /*
                    float prevRotate = 0;
                    foreach (var rotation in data.allBeatmapDataItems.OfType<SpawnRotationBeatmapEventData>().ToList())
                    {
                        int rot = (int)((rotation.rotation - prevRotate));
                        finalRotations.Add((rotation.time, rot));
                        prevRotate = rotation.rotation;
                        //Plugin.Log.Info($"finalRotations - time: {rotation.time} rotation: {rot}");
                    }*/
                    //v1.39
                    /*
                    foreach (var rotation in data.allBeatmapDataItems.OfType<CustomBasicBeatmapEventData>().Where(e => e.basicBeatmapEventType == BasicBeatmapEventType.Event14 || e.basicBeatmapEventType == BasicBeatmapEventType.Event15).ToList())
                    {
                        int rot = SpawnRotationValueToDegrees(rotation.value);
                        finalRotations.Add((rotation.time, rot));
                        //Plugin.Log.Info($"finalRotations - time: {rotation.time} rotation: {rot}");
                    }
                    */
                    //finalRotations = eData.RotationEvents.ToList(); 
                //}
                
                allRotations.Sort((a, b) => a.time.CompareTo(b.time)); // Sort the rotations by time

                OptimizeRotationsToFOV optimize = new OptimizeRotationsToFOV(
                    allRotations,
                    Config.Instance.TimeWindow,
                    Config.Instance.FOV
                ); // Create an instance of BeatMapModifier - wallCutMoments has all the rotation events
                

                allRotations = optimize.FOVFix(); // Call the ModifyRotations method to adjust the rotations and get the modified list
                allRotations.Sort((a, b) => a.time.CompareTo(b.time));
                int prevRot = 0;
                foreach (var rot in allRotations)
                {
                    rot.accumRotation = prevRot + rot.rotation;
                    prevRot = rot.accumRotation;
                }

                if (!Config.Instance.Wireless360 && optimize.RotationsWereAdjusted)
                    needsRotationLimitAdjustment = true;

                //if (TransitionPatcher.characteristicSerializedName == "360Degree" || TransitionPatcher.characteristicSerializedName == "90Degree")
                //    optimize.UpdateBeatmapDataWithRotations(data);
            }
            
            Plugin.Log.Info($"2 Rotation List (after FOV)  Count: {allRotations.Count}");
            /*
            float totalRotation2 = 0;
            foreach (var rot in allRotations)
            {
                if (rot.time < 100)
                {
                    totalRotation2 += rot.rotation;
                    Plugin.Log.Info($"2 Rotation - Time: {rot.time:F} - Rotation: {rot.rotation} - Total Rotation: {(int)totalRotation2} or {rot.accumRotation}");
                }
            }
            */
            /*foreach (var rotation in finalRotations)
            {
                Plugin.Log.Info($"Final Rotations before ArcFix() - time: {rotation.time} rotation: {rotation.rotation}");
            }
            */
            #endregion

            if (Config.Instance.ArcFixFull &&
                //(!BeatmapDataTransformHelperPatcher.NoodleProblemObstacles || !BeatmapDataTransformHelperPatcher.NoodleProblemNotes) &&
                allRotations.Count() > 0 &&
                TransitionPatcher.SelectedSerializedName == GameModeHelper.GENERATED_360DEGREE_MODE)
            {
                //1.40
                //CustomBeatmapData temp = new CustomBeatmapData(4, new CustomData(), new CustomData(), new CustomData(), new Version(2, 6, 0)); // i added this without really thinking if i needed to use data instead of temp
                allRotations = Arcitect.ArcFix(allRotations, eData); // this is for Gen 360 only -- Clearing the list is not needed according to AI. nonGen maps use arcFix() from HarmonyPatches.cs

                if (!Config.Instance.Wireless360)
                    needsRotationLimitAdjustment = true;
            }
            else
                Plugin.Log.Info($"ArcFix not enabled or not applicable. Starting Game Mode: {TransitionPatcher.SelectedSerializedName} - Characteristic: {TransitionPatcher.SelectedSerializedName}");




            if (isEnabledRotations)
            {
                if (!Config.Instance.Wireless360 && Config.Instance.MinRotationSize > 15)
                    needsRotationLimitAdjustment = true;

                if (needsRotationLimitAdjustment)
                {
                    Plugin.Log.Info($"Rotation limits were adjusted.");
                    allRotations = AdjustRotationsToLimit(allRotations);
                }
            }

            allRotations.Sort((a, b) => a.time.CompareTo(b.time));
            allRotations = ERotationEventData.RecalculateAccumulatedRotations(allRotations);

            Plugin.Log.Info($"3 Rotation List (after ArcFix)  Count: {allRotations.Count} (has accurate accum)");
            /*
            foreach (var rot in allRotations)
            {
                if (rot.time < 100)
                {
                    Plugin.Log.Info($"3 Rotation - Time: {rot.time:F} - Rotation: {rot.rotation} - Total Rotation: {rot.accumRotation}"); // accum is accurate here
                }
            }
            */
            eData.RotationEvents = allRotations; // Update the eData with the final rotations

            wallCutMoments.Clear(); // arcfix will cause changes to the rotation so need to recalculate wallcutmoments

            foreach (var rotation in eData.RotationEvents)
            {
                wallCutMoments.Add((rotation.time, SpawnRotationDegreesToSteps(rotation.rotation)));
                //Plugin.Log.Info($"wallCutMoments - time: {rotation.time} rotation: {(int)(rotation.rotation )}");
            }

            #region Wall Generator


            //outside the loop. so not using wallTime and not on the beat
            if (Utils.IsEnabledExtensionWalls() && Config.Instance.EnableMappingExtensionsWallsGenerator && originalWallCount < 5000)// && !BeatmapDataTransformHelperPatcher.NoodleProblemNotes && !BeatmapDataTransformHelperPatcher.NoodleProblemObstacles) // turn off automated extended walls for maps already using mapping extensions
            {
                stopwatch.Restart();

                if (wallCutMoments.Count > 0)
                    gaps = FindGapsUsingRotations(wallCutMoments, 2.0f);
                else
                    gaps = FindGapsUsingNotes(eData.ColorNotes, 2.0f);

                    //Plugin.Log.Info("Gaps: " + gaps.Count + " wallcutmoments: " + wallCutMoments.Count);

                WallGenerator.ParticleWalls();
                WallGenerator.FloorWalls(gaps);
                WallGenerator.MegaWalls(gaps);


                Plugin.Log.Info(
                    $" ------- Create Particle & Floor Walls Time Elapsed: {stopwatch.ElapsedMilliseconds / 1000.0:F1}");
                stopwatch.Stop();
            }

            if (originalWallCount < 5000)
            {
                WallGenerator.LogWallCount(eData);

                //Plugin.Log.Info($" TransitionPatcher.startingGameMode: {TransitionPatcher.startingGameMode}, GameModeHelper.GENERATED_360DEGREE_MODE: {GameModeHelper.GENERATED_360DEGREE_MODE} AllowLeanWalls: {Config.Instance.AllowLeanWalls} AllowCrouchWalls: {Config.Instance.AllowCrouchWalls}");
                
                if (TransitionPatcher.SelectedSerializedName == GameModeHelper.GENERATED_360DEGREE_MODE || // will remove 1 width lean walls from all gen maps even if user allows lean walls!!!!!!!!!!!!!!!
                   !Config.Instance.AllowLeanWalls || !Config.Instance.AllowCrouchWalls)
                {
                    WallGenerator.LeanCrouchWallRemoval();
                }
                else
                {
                    Plugin.Log.Info($"LeanCrouchWallRemoval() NOT CALLED!!");
                }

                eData.RotationEvents = WallGenerator.RemoveCrouchWallRotations(eData.RotationEvents);


                stopwatch.Restart();

                if (Utils.IsEnabledChains())// && Config.Instance.EnableWallGenerator && (Config.Instance.EnableStandardWalls || Config.Instance.EnableBigWalls))
                    WallGenerator.MoveWallsBlockingChainTail(eData);
                else
                {
                    Plugin.Log.Info(
                        $"MoveWallsBlockingChainTail() NOT CALLED!!");
                }

                if (Utils.IsEnabledArcs())// && !BeatmapDataTransformHelperPatcher.NoodleProblemObstacles)// && Config.Instance.EnableWallGenerator && (Config.Instance.EnableStandardWalls || Config.Instance.EnableBigWalls))
                    WallGenerator.MoveWallsBlockingArc(eData);
                else
                {
                    Plugin.Log.Info(
                        $"MoveWallsBlockingArc() NOT CALLED!!");
                }

                Plugin.Log.Info(
                    $" ------- MoveWallsBlockingChainTail() & MoveWallsBlockingArc() time elapsed: {stopwatch.ElapsedMilliseconds / 1000.0:F1}");


                if (isEnabledWalls && !TransitionPatcher.MapAlreadyUsesMappingExtensions && Config.Instance.EnableMappingExtensionsWallsGenerator)
                    WallGenerator.RemoveIntersectingWalls();

                if (TransitionPatcher.SelectedSerializedName == "360Degree" || TransitionPatcher.SelectedSerializedName == "90Degree")
                {
                    List<(float time, int rotationSteps)> wallCutMoments2 = new List<(float, int)>();
                    //v1.34
                    /*
                    float previousRotation = 0;
                    foreach (var rotation in data.allBeatmapDataItems.OfType<SpawnRotationBeatmapEventData>().ToList())
                    {
                        int rot = (int)((rotation.rotation - previousRotation) / 15);
                        wallCutMoments2.Add((rotation.time, rot));
                        previousRotation = rotation.rotation;
                        //Plugin.Log.Info($"wallCutMoments2 - time: {rotation.time} rotation: {rot}");
                    }*/
                    foreach (var rotation in eData.RotationEvents)
                    {
                        wallCutMoments2.Add((rotation.time, SpawnRotationDegreesToSteps(rotation.rotation)));
                        //Plugin.Log.Info($"wallCutMoments2 - time: {rotation.time} rotation: {(int)(rotation.floatValue)}");
                    }

                    //Plugin.Log.Info($" ------- WallRemovalForRotations for nonGen360/90 maps Rotation Count: {wallCutMoments2.Count} 1st rotation: {wallCutMoments2[0].Item1} - {wallCutMoments2[0].Item2} ");

                    //WallGenerator.WallRemovalForRotations(wallCutMoments2);
                }
                else if (wallCutMoments.Count > 0)
                {
                    //foreach ((float time, int rotation) in wallCutMoments)
                    //{
                    //    Plugin.Log.Info($"wallCutMoments - time: {time} rotation: {rotation}");
                    //}

                    //WallGenerator.WallRemovalForRotations(wallCutMoments);
                }

                //if (Config.Instance.EnableWallGenerator)
                WallGenerator.FinalizeWallsToMap(eData);
            }
            else
            {
                WallGenerator.FinalizeOriginalOnlyWallsToMap(eData);
            }

            List<ENoteData> bombsToRemove = new List<ENoteData>();
            foreach (var obj in eData.ColorNotes) // bombs get added to colorNotes for loop so need to remove them. this is still needed
            {
                if (obj.cutDirection == NoteCutDirection.None)
                    bombsToRemove.Add(obj);
            }
            //Plugin.Log.Info($" --- Removing {bombsToRemove.Count()} Bombs --- ");
            eData.ColorNotes.RemoveAll(b => bombsToRemove.Contains(b)); // remove bombs
            eData.ColorNotes = eData.ColorNotes.OrderBy(n => n.time).ToList();

            Plugin.Log.Info($" --- Remaining Walls --- {eData.Obstacles.Count()}");

            #endregion

            #region Remove Bombs when map turns (360/90)

            if (isEnabledRotations)
            {
                // Remove bombs (just problematic ones) iterate backwards
                // Build list that ties rotation events directly
                var bombCutMoments = new List<(float time, int rotation, ERotationEventData evt)>();

                for (int b = 0; b < eData.RotationEvents.Count; b++)
                {
                    var rot = eData.RotationEvents[b];
                    bombCutMoments.Add((rot.time, rot.rotation, rot));
                }


                // Track which rotation events to remove
                var rotsToRemove = new HashSet<ERotationEventData>();


                // remove bombs and their rotation events since leaving the rotation event that occurs after a long will will mean other wall will rotate and the player expects a note to come in that direction perhaps
                for (int i = eData.BombNotes.Count - 1; i >= 0; i--)
                {
                    var bomb = eData.BombNotes[i];

                    foreach (var (cutTime, rotAmount, rotEvt) in bombCutMoments)
                    {
                        if (bomb.time >= cutTime - WallGenerator.WallFrontCut &&
                            bomb.time < cutTime + WallGenerator.WallBackCut)
                        {
                            if ((bomb.line < 2 && rotAmount < 0) || (bomb.line > 1 && rotAmount > 0))
                            {
                                eData.BombNotes.RemoveAt(i);
                                rotsToRemove.Add(rotEvt);

                                //Plugin.Log.Info($"Removed bomb {bomb.time:F2}, and rotation at {rotEvt.time:F2} (rot={rotAmount})");

                                break;
                            }
                        }
                    }
                }

                // Remove the marked rotation events
                if (rotsToRemove.Count > 0)
                {
                    eData.RotationEvents = eData.RotationEvents
                        .Where(p => !rotsToRemove.Contains(p))
                        .OrderBy(p => p.time)
                        .ToList();

                }

                eData.RotationEvents = ERotationEventData.RecalculateAccumulatedRotations(eData.RotationEvents);

                //old working but doesn't remove rotations as well
                /*
                for (int i = eData.BombNotes.Count - 1; i >= 0; i--)
                {
                    var bomb = eData.BombNotes[i];
                    foreach ((float cutTime, int cutAmount) in wallCutMoments)
                    {
                        if (bomb.time >= cutTime - WallGenerator.WallFrontCut &&
                            bomb.time < cutTime + WallGenerator.WallBackCut)
                        {
                            if ((bomb.line <= 2 && cutAmount < 0) || (bomb.line >= 1 && cutAmount > 0))
                            {
                                eData.BombNotes.RemoveAt(i);
                                //Plugin.Log.Info($"Remove Bomb: {bomb.time:F}");
                                break; // break inner foreach to avoid using removed bomb
                            }
                        }
                    }
                }
                */
            }

            #endregion

            //Plugin.Log.Info($"Emitted {eventCount} rotation events");
            //Plugin.Log.Info($"LimitRotations: {LimitRotations}");
            //Plugin.Log.Info($"BottleneckRotations: {BottleneckRotations}");

            //v1.34
            //int rotationEventsCount = data.allBeatmapDataItems.OfType<SpawnRotationBeatmapEventData>().Count();
            //v1.39
            //int rotationEventsCount = eData.RotationEvents.Count();

            //int obstaclesCount = data.allBeatmapDataItems.OfType<ObstacleData>().Count();

            Plugin.Log.Info($"Rotation Events Count: {eData.RotationEvents.Count()}");
            //Plugin.Log.Info($"obstaclesCount: {obstaclesCount}");

            //if (LevelUpdatePatcher.BeatSage)
            //    BeatSageCleanUp(notes, new List<ObstacleData>(dataItems.OfType<ObstacleData>()));
            /*
            #region LightAutoMapper

            if (Utils.IsEnabledLighting() && Config.Instance.EnableLightAutoMapper)
            {
                stopwatch.Restart();

                LightAutoMapper.Start(data);

                Plugin.Log.Info(
                $" ------- LightAutoMapper time elapsed: {stopwatch.ElapsedMilliseconds / 1000.0:F1}");
                stopwatch.Stop();
            }

            #endregion
            */

            /*
            foreach (var light in data.allBeatmapDataItems.OfType<BasicBeatmapEventData>())
            {
                Plugin.Log.Info($"Final Light Events: _time: {light.time:F}, _type: {light.basicBeatmapEventType}, _value: {light.value}");
            }

            float prevRot = 0;
            foreach (var rotation in data.allBeatmapDataItems.OfType<SpawnRotationBeatmapEventData>())
            {
                Plugin.Log.Info($"Final Rotations: time: {rotation.time:F} rotation: {rotation.rotation - prevRot} -- Total Rotation: {rotation.rotation}");
                prevRot = rotation.rotation;
            }
           
            foreach (var not in notesAndBombs)
            {
                if (not.time > 0 && not.time < 33)
                    Plugin.Log.Info($"Final Notes: time: {not.time:F} dir: {not.cutDirection} index: {not.line} layer: {not.layer} color: {not.colorType}");
            }
             */
           
            ConvertEditableCBD.ApplyPerObjectRotations(eData);

            Plugin.Log.Info($"[Generator] EditableCBD map version: {eData.Version} - notes: {eData.ColorNotes.Count()} bombs: {eData.BombNotes.Count()} arcs: {eData.Arcs.Count()} chains: {eData.Chains.Count()} obstacles: {eData.Obstacles.Count()} events: {eData.BasicEvents.Count()} customEvents: {eData.CustomEvents.Count()}.");

            if (eData.OriginalCBData != null)
            {
                // This is a real CustomJSON map → build CustomBeatmapData
                var cbd = ConvertEditableCBD.Convert(eData);

                // (Optional but wise) sanitize numbers if you ever mix doubles/longs
                // NormalizeCustomJson(cbd.customData); etc.
                Plugin.Log.Info($"[Generator] Final Result CustomBeatmapData after Generator - notes: {cbd.allBeatmapDataItems.OfType<NoteData>().Count()} obstacles: {cbd.allBeatmapDataItems.OfType<ObstacleData>().Count()} events: {cbd.allBeatmapDataItems.OfType<BasicBeatmapEventData>().Count()} customEvents: {cbd.allBeatmapDataItems.OfType<CustomEventData>().Count()}.");


                return new GeneratorOutput { Custom = cbd };
            }

            // Built-in / OST path → emit vanilla BeatmapData
            var bd = ConvertEditableCBD.ConvertVanilla(eData);

            Plugin.Log.Info($"[Generator] Final Result BeatmapData after Generator - notes: {bd.allBeatmapDataItems.OfType<NoteData>().Count()} obstacles: {bd.allBeatmapDataItems.OfType<ObstacleData>().Count()} events: {bd.allBeatmapDataItems.OfType<BasicBeatmapEventData>().Count()} customEvents: {bd.allBeatmapDataItems.OfType<CustomEventData>().Count()}.");

            return new GeneratorOutput { Vanilla = bd };
            


            

            //return cbd;//ApplyPerObjectRotations(data, finalRotations);//data;
        }

        /// <summary>
        ///  Detect gaps between walls (times when there are no walls) for sky floor walls to be added
        ///  Looks for time gaps between rotation moments that are at least a certain length (minGapDuration), and returns those gaps as TimeGap objects.
        /// </summary>
        public List<TimeGap> FindGapsUsingRotations(List<(float time, int rotation)> wallCutMoments, float minGapDuration)
        {
            List<TimeGap> gaps = new List<TimeGap>();

            wallCutMoments = wallCutMoments.OrderBy(w => w.time).ToList();

            for (int i = 0; i < wallCutMoments.Count - 1; i++)
            {
                float currentRotation = wallCutMoments[i].time;
                float nextRotation = wallCutMoments[i + 1].time;

                float gapDuration = nextRotation - currentRotation;
                if (gapDuration >= minGapDuration)
                {
                    gaps.Add(new TimeGap(currentRotation, nextRotation));
                }
            }

            return gaps;
        }

        /// <summary>
        /// Use this to find gaps between notes when there are no rotations
        /// </summary>
        /// <param name="notes"></param>
        /// <param name="minGapDuration"></param>
        /// <param name="edgeBuffer"></param>
        /// <returns></returns>
        public static List<TimeGap> FindGapsUsingNotes(
            List<ENoteData> notes,
            float minGapDuration,
            float edgeBuffer = 0f)
        {
            float sameTimeEpsilon = 0.0005f; // collapse nearly-identical timestamps

            var gaps = new List<TimeGap>();

            if (notes == null || notes.Count == 0) return gaps;

            // Collapse near-duplicates in-place
            var collapsed = new List<float>(notes.Count);
            float last = notes[0].time;
            collapsed.Add(last);
            for (int i = 1; i < notes.Count; i++)
            {
                float t = notes[i].time;
                if (t - last >= sameTimeEpsilon)
                {
                    collapsed.Add(t);
                    last = t;
                }
                // else: skip near-duplicate
            }

            // Internal gaps only (first → last), no leading/trailing beyond notes
            for (int i = 0; i < collapsed.Count - 1; i++)
            {
                float rawStart = collapsed[i];
                float rawEnd = collapsed[i + 1];

                // Edge-buffer so walls don’t overlap the notes at the gap edges
                float s = rawStart + edgeBuffer;
                float e = rawEnd - edgeBuffer;

                if (e - s >= minGapDuration)
                {
                    gaps.Add(new TimeGap(s, e));
                }
            }

            return gaps;
        }


        /// <summary> ArcFix(), MinRotationSize larger than 15, and FOVFix() cause rotations to move beyond the limits of LimitRotations360</summary> 
        public List<ERotationEventData> AdjustRotationsToLimit(List<ERotationEventData> rotations)
        {
            int rotationLimit = (int)Config.Instance.LimitRotations360;
            int halfLimit = rotationLimit / 2;

            int currentRotation = 0;
            var adjustedRotations = new List<ERotationEventData>();

            foreach (var rotation in rotations)
            {
                int proposedRotation = currentRotation + rotation.rotation;

                if (Math.Abs(proposedRotation) <= halfLimit)
                {
                    // Within bounds, use the original rotation
                    currentRotation = proposedRotation;
                    var newRot = ERotationEventData.Create(rotation.time, rotation.rotation);
                    adjustedRotations.Add(newRot);
                }
                else
                {
                    // Try flipping the rotation
                    int flippedRotation = -rotation.rotation;
                    proposedRotation = currentRotation + flippedRotation;

                    if (Math.Abs(proposedRotation) <= halfLimit)
                    {
                        currentRotation = proposedRotation;
                        var flipRot = ERotationEventData.Create(rotation.time, flippedRotation);
                        adjustedRotations.Add(flipRot);
                    }
                    // If flipping still goes out of bounds, skip the rotation (optional)

                }
            }
            adjustedRotations.Sort((a, b) => a.time.CompareTo(b.time));

            return adjustedRotations;
        }

        /// <summary>
        /// Converts a spawn rotation angle (in degrees) to the legacy Beat Saber rotation event value for v2 maps.
        /// Returns null if the angle is not one of the legacy values.
        /// </summary>
        public int SpawnRotationDegreesToValueV2(int degrees)
        {
            switch ((int)degrees)
            {
                //case -60: return 0;
                case -45: return 1;
                case -30: return 2;
                case -15: return 3;
                case 15: return 4;
                case 30: return 5;
                case 45: return 6;
                //case 60: return 7;
                default: return 4;//null; // Or throw, or clamp, as you see fit
            }
        }
        public int SpawnRotationDegreesToSteps(int degrees)
        {
            switch ((int)degrees)
            {
                //case -60: return -4;
                case -45: return -3;
                case -30: return -2;
                case -15: return -1;
                case 15: return   1;
                case 30: return   2;
                case 45: return   3;
                //case 60: return 4;
                default: return 1;//null; // Or throw, or clamp, as you see fit
            }
        }
        public static int SpawnRotationValueToDegrees(int value)
        {
            switch (value)
            {
                //case -60: return 0;
                case 1: return -45;
                case 2: return -30;
                case 3: return -15;
                case 4: return 15;
                case 5: return 30;
                case 6: return 45;
                //case 7: return 60f;
                default: return 0;//null; // Or throw, or clamp, as you see fit
            }
        }
        public static int SpawnRotationValueToStepsV2(int value)
        {
            switch (value)
            {
                //case -60: return -4;
                case 1: return -3;
                case 2: return -2;
                case 3: return -1;
                case 4: return 1;
                case 5: return 2;
                case 6: return 3;
                //case 7: return 4f;
                default: return 1;//null; // Or throw, or clamp, as you see fit
            }
        }

        public CustomBeatmapData DeepCopyCustomBeatmapData(CustomBeatmapData original) // not really tested to see if catches all custom data. probably not!!!
        {
            var copy = new CustomBeatmapData(original.numberOfLines, original.beatmapCustomData, original.levelCustomData, original.customData, original.version);


            // Copy objects
            foreach (var item in original.allBeatmapDataItems)
            {
                // Only copy if it's a BeatmapObjectData (note, obstacle, slider, etc)
                if (item is BeatmapObjectData obj)
                {
                    var copiedObj = obj.GetCopy() as BeatmapObjectData;
                    if (copiedObj != null)
                        copy.AddBeatmapObjectDataInOrder(copiedObj);

                }
            }

            // Copy events
            foreach (var evt in original.allBeatmapDataItems)
            {
                if (evt is BeatmapEventData eventData)
                {
                    var copiedEvt = evt.GetCopy() as BeatmapEventData;
                    if (copiedEvt != null)
                        copy.InsertBeatmapEventDataInOrder(copiedEvt);
                }
            }

            // Copy special event keywords, if needed
            foreach (string keyword in original.specialBasicBeatmapEventKeywords)
            {
                copy.AddSpecialBasicBeatmapEventKeyword(keyword);
            }

            // Copy any additional collections if CustomBeatmapData has them (waypoints, custom fields, etc.)

            return copy;
        }
        public static CustomBeatmapData DeepCopyBeatmapData(BeatmapData original)// = new Version(2,6,0) // not really tested to see if catches all custom data. probably not!!!
        {
            var copy = new CustomBeatmapData(original.numberOfLines, new CustomData(), new CustomData(), new CustomData(), TransitionPatcher.LevelVersion); // can get from JSON if need to

            // Copy objects
            foreach (var item in original.allBeatmapDataItems)
            {
                var dup = item.GetCopy();
                switch (dup)
                {
                    case BeatmapObjectData obj:
                        copy.AddBeatmapObjectDataInOrder(obj);
                        break;
                    case BeatmapEventData evt:
                        copy.InsertBeatmapEventDataInOrder(evt);
                        break;
                    case CustomEventData cevt:
                        copy.InsertCustomEventDataInOrder(cevt);
                        break;
                }
            }

            // Copy special event keywords, if needed
            foreach (string keyword in original.specialBasicBeatmapEventKeywords)
            {
                copy.AddSpecialBasicBeatmapEventKeyword(keyword);
            }

            // Copy any additional collections if CustomBeatmapData has them (waypoints, custom fields, etc.)

            return copy;
        }
        /*
        public static Version VersionGetter (int majorVersion)
        {
            Version version = new Version(2, 6, 0);

            if (majorVersion == 3)
                version = new Version(majorVersion, 3, 0);
            else if (majorVersion == 4)
                version = new Version(majorVersion, 0, 0);
            return version;
        }
        */

        /// <summary>
        /// Adds rotation events into massive streaks of single direction rotations to break them up. Will alter the input list of rotations (allRotations)
        /// </summary>
        /// <param name="rots"></param>
        /// <param name="flexibleRotations"></param>
        /// <param name="massiveStreaks"></param>
        /// <param name="minRun"></param>
        /// <param name="maxRun"></param>
        static void AddRotationsIntoMassiveStreak(
            List<ERotationEventData> rots,
            List<bool> flexibleRotations,
            List<(int start, int end)> massiveStreaks,
            int minRun = 2,
            int maxRun = 12,
            int minFlexiblePerSegment = 2   // NEW: ensure each segment has at least this many flexible events when possible
)
        {
            if (rots == null || flexibleRotations == null || massiveStreaks == null) return;
            if (rots.Count == 0 || flexibleRotations.Count != rots.Count) return;
            if (minRun < 1) minRun = 1;
            if (maxRun < minRun) maxRun = minRun;
            if (minFlexiblePerSegment < 0) minFlexiblePerSegment = 0;

            // deterministic "randomlike" generator
            static uint LcgNext(ref uint s) { unchecked { s = 1664525u * s + 1013904223u; } return s; }
            static int NextRunLen(ref uint s, int minR, int maxR)
            {
                uint span = (uint)(maxR - minR + 1);
                uint n = LcgNext(ref s) % span;
                return minR + (int)n;
            }

            foreach (var (start, end) in massiveStreaks)
            {
                if (start < 0 || end >= rots.Count || start > end) continue;

                int origSign = Math.Sign(rots[start].rotation);
                if (origSign == 0)
                {
                    for (int k = start; k <= end && origSign == 0; k++)
                        origSign = Math.Sign(rots[k].rotation);
                    if (origSign == 0) continue; // nothing to do
                }

                // Seed from stable streak properties (deterministic)
                uint seed;
                unchecked
                {
                    seed = 0x9E3779B9u;
                    seed ^= (uint)start * 0x85EBCA6Bu;
                    seed ^= (uint)end * 0xC2B2AE35u;
                    seed ^= (uint)(Math.Abs(rots[start].rotation) + 1) * 0x27D4EB2Fu;
                    seed ^= (uint)(Math.Abs(rots[end].rotation) + 3) * 0x165667B1u;
                }

                // 1) Build initial segments from deterministic run lengths (index space)
                var segs = new List<(int a, int b)>();
                {
                    int segStart = start;
                    while (segStart <= end)
                    {
                        int runLen = NextRunLen(ref seed, minRun, maxRun);
                        int segEnd = Math.Min(end, segStart + runLen - 1);
                        segs.Add((segStart, segEnd));
                        segStart = segEnd + 1;
                    }
                }

                // 2) Merge segments that have too few flexible events
                if (minFlexiblePerSegment > 0 && segs.Count > 0)
                {
                    var merged = new List<(int a, int b)>();
                    int idx = 0;
                    while (idx < segs.Count)
                    {
                        int a = segs[idx].a;
                        int b = segs[idx].b;

                        // count flexible in [a..b]
                        int flexHere = 0;
                        for (int i = a; i <= b; i++)
                            if (flexibleRotations[i]) flexHere++;

                        // greedily merge forward until we hit the threshold (or run out)
                        int j = idx + 1;
                        while (flexHere < minFlexiblePerSegment && j < segs.Count)
                        {
                            int na = segs[j].a;
                            int nb = segs[j].b;
                            // extend current segment
                            for (int i = na; i <= nb; i++)
                                if (flexibleRotations[i]) flexHere++;
                            b = nb;
                            j++;
                        }

                        merged.Add((a, b));
                        idx = j;
                    }
                    segs = merged;
                }

                // 3) Apply alternating signs per (possibly merged) segment
                int totalChanged = 0;
                Plugin.Log.Info($"[MassiveStreakDbg] --- Streak {start}-{end} len={(end - start + 1)}, origSign={(origSign > 0 ? "R" : "L")} --- start time: {rots[start].time} end time: {rots[end].time}");
                for (int s = 0; s < segs.Count; s++)
                {
                    var (a, b) = segs[s];
                    int desiredSign = (s % 2 == 0) ? origSign : -origSign;
                    string dirLabel = desiredSign > 0 ? "RIGHT" : "LEFT";

                    int flexCount = 0;
                    int changedThisSeg = 0;

                    for (int i = a; i <= b; i++)
                    {
                        if (!flexibleRotations[i]) continue;
                        flexCount++;
                        int mag = Math.Abs(rots[i].rotation);
                        if (mag == 0) continue;
                        rots[i].rotation = desiredSign * mag;
                        changedThisSeg++;
                    }

                    totalChanged += changedThisSeg;
                    int actualLen = b - a + 1;

                    Plugin.Log.Info(
                        $"[MassiveStreakDbg]   Segment {s:D2} {a}-{b} len={actualLen} dir={dirLabel} flex={flexCount} changed={changedThisSeg}");
                }

                int flips = Math.Max(0, segs.Count - 1);
                Plugin.Log.Info($"[MassiveStreakDbg] >>> Finished streak {start}-{end}: segments={segs.Count}, flips={flips}, changed={totalChanged}");
            }
        }









    }//Public Class Generator



    /// <summary>
    ///  Gaps between walls (times when there are no walls) for sky floor walls to be added.
    ///  Added to detect gaps between walls (times when there are no walls) for sky floor walls to be added
    /// </summary>
    public class TimeGap // 
    {
        public float StartTime { get; }
        public float EndTime { get; }

        public TimeGap(float startTime, float endTime)
        {
            StartTime = startTime;
            EndTime = endTime;
        }

        public bool WithinGap(float time)
        {
            return time >= StartTime && time <= EndTime;
        }
    }






    //these don't work
    /*
    public static class BeatmapDataItemExtensions
    {
        public static void UpdateTime(this BeatmapDataItem item, float newTime) // bw not working can't time on this according to AI
        {
            FieldHelper.Set(item, "<time>k__BackingField", newTime);
        }
        public static void UpdateLineIndex(this ObstacleData item, int newLineIndex) //bw not working can't set a private setter
        {
            //FieldHelper.SetPropertyWithPrivateSetter(item, nameof(ObstacleData.lineIndex), newLineIndex);
            FieldHelper.SetBackingField(item, "<lineIndex>k__BackingField", newLineIndex);
        }
    }
    */
}

