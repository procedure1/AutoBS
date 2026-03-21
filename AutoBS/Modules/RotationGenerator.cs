using AutoBS.Patches;
using CustomJSONData.CustomBeatmap;
using SiraUtil.Zenject;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Policy;
using static AutoBS.MenuDataRegistry;
using static NoteData;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TrackBar;
using static UnityEngine.EventSystems.EventTrigger;

namespace AutoBS
{
    internal static class RotationGenerator
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

        internal static void Generate(EditableCBD eData)
        {
            //Config cfg = Config.Instance;

            /// <summary>
            /// The preferred bar duration in seconds. The generator will loop the song in bars. 
            /// This is called 'preferred' because this value will change depending on a song's bpm (will be aligned around this value).
            /// Affects the speed at which the rotation occurs. It will not affect the total number of rotations or the range of rotation.
            /// BW CREATED CONFIG ROTATION  SPEED to allow user to set this.
            /// </summary>
            float PreferredBarDuration = 2.75f;//BW I like 1.5f instead of 1.84f but very similar to changing LimitRotations, 1.0f is too much and 0.2f freezes beat saber  // Calculated from 130 bpm, which is a pretty standard bpm (60 / 130 bpm * 4 whole notes per bar ~= 1.84)

            ///<summary>
            ///The RotationSpeedMultiplier affects the rotation events primarily by modifying the bar length, which in turn influences the frequency of rotation events. If RotationSpeedMultiplier is Increased (e.g., 1.0 → 2.0), The adjusted PreferredBarDuration becomes shorter, leading to shorter bars. Shorter bars mean more frequent rotation events.
            ///</summary>   
            float RotationSpeedMultiplier = 1.0f;//BW This is a multiplier for PreferredBarDuration
            /// <summary>
            /// The amount of 15 degree rotations before stopping rotation events (rip cable otherwise) (24 is one full 360 rotation)
            /// </summary>
            int LimitRotations = 28;//BW 28 is equivalent to 360 (24*15) so this is 420 degrees. this is set by Config.Instance.LimitRotations360
            /// <summary>
            /// The amount of rotations before preferring the other direction (24 is one full rotation)
            /// </summary>
            int BottleneckRotations = 14; //BW 14 default. This is set by LevelUpdatePatcher which sets this to LimitRotations/2
            /// <summary>
            /// Enable the spin effect when no notes are coming.
            /// </summary>
            bool EnableSpin = false;
            /// <summary>
            /// The total time 1 spin takes in seconds.
            /// </summary>
            float TotalSpinTime = 0.6f;
            /// <summary>
            /// Minimum amount of seconds between each spin effect.
            /// </summary>
            float SpinCooldown = 10f;

            List<ENoteData> notesAndBombs = new List<ENoteData>(eData.ColorNotes);

            Plugin.LogDebug($"[RotationGenerator] Notes Count: {notesAndBombs.Count}"); // BW added to see how many notes are in the map

            notesAndBombs.AddRange(eData.BombNotes);//List<ENoteData> notes = data.GetBeatmapDataItems<ENoteData>(0).ToList(); // NOTES CONTAINS NOTES AND BOMBS

            notesAndBombs.Sort((a, b) => a.time.CompareTo(b.time));

            Plugin.LogDebug($"[RotationGenerator] Notes Count after adding bombs: {notesAndBombs.Count}");

            if (notesAndBombs.Count == 0) return;

            /*
            foreach (var n in notesAndBombs)
            {
                Plugin.Log.Info($"Note: Time: {n.time:F} Line: {n.line} Layer: {n.layer} Type: {n.gameplayType} Color: {n.colorType} CutDir: {n.cutDirection}");
            }
            */

            ESliderData currentActiveChain = null;

            RotationSpeedMultiplier = Config.Instance.RotationSpeedMultiplier;
            //Plugin.LogDebug($"[RotationGenerator] Using RotationSpeedMultiplier={RotationSpeedMultiplier} for this run.");

            int d0 = 0; int d15 = 0; int d30 = 0; int d45 = 0; int d60 = 0; int d75 = 0; int d90 = 0; int d120 = 0;

            if (TransitionPatcher.SelectedSerializedName == GameModeHelper.GENERATED_360DEGREE_MODE)
            {
                Plugin.LogDebug($"[RotationGenerator] Generating rotation events for {TransitionPatcher.SelectedSerializedName}...");

                if (Config.Instance.Wireless360)
                {
                    LimitRotations = 99999;
                    BottleneckRotations = 99999;
                }
                else
                {
                    LimitRotations =
                        (int)((Config.Instance.LimitRotations360 / 360f / 2f) * (24f)); // / Config.Instance.RotationAngleMultiplier));//BW this convert the angle into LimitRotation units of 15 degree slices. Need to divide the Multiplier since it causes the angle to change from 15 degrees. this will keep the desired limit to work if a multiplier is added.
                    BottleneckRotations = LimitRotations / 2;
                }
            }


            List<TimeGap> gaps = new List<TimeGap>();

            Version version = new Version(2, 6, 0);

            int Floor(float f)
            {
                int i = (int)f;
                return f - i >= 0.999f ? i + 1 : i;
            }



            var originalRotations = eData.RotationEvents
                .OrderBy(r => r.time)
                .Select(r => (t: MathF.Round(r.time, 4), rot: r.rotation))
                .ToList();

            bool needsRotationLimitAdjustment = false;

            bool isEnabledWalls = Utils.IsEnabledWalls();

            bool isEnabledRotations = Utils.IsEnabledRotations(); // only Gen 360!

            int originalWallCount = eData.Obstacles.Count;

            Plugin.LogDebug($"[RotationGenerator] Original Wall Count: {originalWallCount}");

            #region Rotate

            int eventCount = 0; // Amount of rotation events emitted
            int totalRotation = 0; // Current rotation

            List<ERotationEventData> allRotations = eData.RotationEvents.Count == 0 ? new List<ERotationEventData>() : eData.RotationEvents; // added for nonGen360 maps

            Plugin.LogDebug($"[RotationGenerator] 0 Rotation List (original) Count: {allRotations.Count}");
            /*
            foreach (var rot in allRotations)
            {
                if (rot.time < 20)
                    Plugin.Log.Info($"0 Rotation - Time: {rot.time} - Rotation: {rot.rotation} - Total Rotation: {rot.accumRotation}");
            }
            */

            bool previousDirectionPositive = true; // Previous spin direction, false is left, true is right

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

            List<(ENoteData arcHeadNote, int accumRotation)> arcHeadNoteRotation = new List<(ENoteData, int)>();
            var arcsAlreadyProcessed = new List<ESliderData>();

            int accumRotation = 0;


            // --- Massive Streak Detection Setup - based on emitted rotation events ---
            int pairStreakRemaining = 0; // remaining mirrored-pair ties in current streak
            int pairStreakSign = +1; // +1 right, -1 left
            var flexibleRotations = new List<bool>();             // parallel to allRotations - list of moments that can go either direction
            var massiveStreaks = new List<(int start, int end)>();// inclusive indices
            int DetectThreshold = (int)Config.Instance.MassiveStreakNumberOfRotationsThreshold; // how many same direction rotations to consider a "massive streak"

            int curRunStart = -1, curRunLen = 0, curRunSign = 0;
            int lastProcessedEvtIdx = -1;

            // Massive Streak Detection - Notes that can have their rotation direction changed without impacting gameplay
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
                    Plugin.LogDebug($"[RotationGenerator][MassiveStreak] Added streak: {start}–{end} (len={end - start + 1})");
                }
                curRunStart = -1; curRunLen = 0; curRunSign = 0;
            }

            // ------------------------------------------

            int minRotationStep = (int)Config.Instance.MinRotationSize / 15;
            int maxRotationStep = (int)Config.Instance.MaxRotationSize / 15;

            if (minRotationStep > maxRotationStep)
                minRotationStep = maxRotationStep;

            // changes the step size based on the minimum allowed. if 2 (30 deg) then step of 1 will be offset to 2 and step of 2 will be offset to 3 etc.
            int rotationStepOffset = minRotationStep - 1;

            float notespersecond = TransitionPatcher.NotesPerSecond;
            float njs = TransitionPatcher.FinalNoteJumpMovementSpeed > 0 ? TransitionPatcher.FinalNoteJumpMovementSpeed : 10; // at least have a fallback

            // high speed high density maps can have too many 30 degree rotations which seems excessive. 
            if (Config.Instance.ReduceRotationForHighSpeedHighDensityMaps && notespersecond > Config.Instance.HighNPSThresholdForRotationReduction && njs > Config.Instance.HighNJSThresholdForRotationReduction)
            {
                maxRotationStep = minRotationStep = 1;
                rotationStepOffset = 0;
                Plugin.LogDebug($"[RotationGenerator] High Speed NJS: {njs} / High Density NPS: {notespersecond} map detected. Setting maxRotationStep and minRotationStep to 1.");
            }

            bool wireless360 = Config.Instance.Wireless360;
            bool addExtraRotation = Config.Instance.AddExtraRotation;

            //Each rotation is 15 degree increments so 24 positive rotations is 360. Negative numbers rotate to the left, positive to the right
            // ---------------------------------------------------------------------------------------------------------------------------------------------------------
            void Rotate(ENoteData note, int rotationStep) //amount is a rotation step (-3 to 3)
            {
                //Plugin.Log.Info($"Rotate() Called - time: {time:F} rotation: {rotationStep * 15}");
                if (rotationStep == 0)//Allows 4*15=60 degree turn max and -60 degree min -- however amounts are never passed in higher than 3 or lower than -3. I in testing I only see 2 to -2
                    return;

                //Plugin.LogDebug($"[Rotate] ENTER t={note.time:F2} rawStep={rotationStep} " +$"minStep={minRotationStep} maxStep={maxRotationStep} totalRotation={totalRotation}");
                /*
                int sign =    Math.Sign(rotationStep);
                int absStep = Math.Abs(rotationStep);

                absStep += rotationStepOffset;

                // Normalize to the generator’s raw 1..4 range
                absStep = Math.Clamp(absStep, minRotationStep, maxRotationStep);

                rotationStep = sign * absStep;
                */
                if (!wireless360)//always true unless you enableSpin in settings
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

                            Plugin.LogDebug($"[Rotate] --- Found arcTailNote so setting it's rotation to match arcHeadNote at time: {match.arcHeadNote.time:F}.");
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

                allRotations.Add(ERotationEventData.CreateInOrder(note.time, rotationStep * 15));

                if (matchArcHeadAndTailRotation)
                {
                    arcsAlreadyProcessed.Add(note.tailNoteArc); // will remove these from arc list later so they don't waste processing time

                    if (note.headNoteArc != null)
                    {
                        arcHeadNoteRotation.Add((note, accumRotation));
                        Plugin.LogDebug($"[Rotate] --- Adding arcHeadNote to list for later processing with tail note time: {note.headNoteArc.tailNote.time:F}.");
                    }
                }
            }
            // ---------------------------------------------------------------------------------------------------------------------------------------------------------
            #endregion

            float beatDuration = 60f / TransitionPatcher.bpm;

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

            //Plugin.LogDebug($"beatDuration: {beatDuration} barLength: {barLength}");
            //Plugin.LogDebug($"PreferredBarDuration: {PreferredBarDuration} * RotationSpeedMultiplier: {RotationSpeedMultiplier} = {PreferredBarDuration/RotationSpeedMultiplier}");
            //Plugin.LogDebug($"RotationAngleMultiplier: {RotationAngleMultiplier}");


            List<ENoteData> notesInBar = new List<ENoteData>(); // CONTAINS NOTES AND BOMBS
            List<ENoteData> notesInBarBeat = new List<ENoteData>(); // CONTAINS NOTES AND BOMBS

            // Align bars to first note, the first note (almost always) identifies the start of the first bar
            float firstBeatmapNoteTime = notesAndBombs[0].time;

            //Plugin.LogDebug($"Setup bpm={TransitionPatcher.bpm} beatDuration={beatDuration} barLength={barLength} firstNoteTime={firstBeatmapNoteTime} firstnoteGameplayType={eData.ColorNotes[0].gameplayType} firstnoteColorType={eData.ColorNotes[0].colorType}");
            


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
            // Any chain with duration >= this is considered “long” for rotation clamping.
            const float MIN_LONG_CHAIN_DURATION = .2f;
            bool clampLongChainsRotations = Config.Instance.EnableLongChains && Config.Instance.LongChainMaxDuration >= MIN_LONG_CHAIN_DURATION;

            const int CLAMPED_STEP_SIZE = 1; // clamp to this rotation step size. could do 2 for 30 degrees or even 0 to remove all rotations.

            // --- Long-chain rotation guard setup ---
            var longChains = eData.Chains
                .Where(ch => (ch.tailTime - ch.time) > MIN_LONG_CHAIN_DURATION)
                .OrderBy(ch => ch.time)
                .ToList();

            // Cursor so we can scan protectedLongChains in O(totalChains + totalRotations)
            int longChainCursor = 0; // pointer that always moves forward in time, never backward, reduces iterations

            //find rotations that cause the tail to be at a different rotation than the head making it hard to hit the tail slice if the rotation is in the wrong direction.
            ESliderData GetActiveLongChain(float time)
            {
                // Skip chains that end before this time
                while (longChainCursor < longChains.Count &&
                       longChains[longChainCursor].tailTime < time)
                {
                    longChainCursor++;
                }

                if (longChainCursor < longChains.Count)
                {
                    var ch = longChains[longChainCursor];
                    if (ch.time <= time && time <= ch.tailTime) // trying instead of this ch.time <= time && time <= ch.tailTime
                        return ch;
                }

                return null;
            }



            // Deterministic RNG based on song identity (or map seed)
            int seed = TransitionPatcher.SelectedPlayKey.GetHashCode();
            Random randStepSize = new Random(seed);

            //Stopwatch stopwatch = new Stopwatch();
            int count1 = 0;  int count2 = 0; int count3 = 0; int count4 = 0;
            #region Main Loop

            //stopwatch.Restart();

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

                // Iterate all the notes in the current bar in barDivider pieces (bar is split in barDivider pieces)
                float dividedBarLength = barLength / barDivider;
                for (int j = 0, k = 0; j < barDivider && k < notesInBar.Count; j++)
                {
                    //if (notes[i].time > 148f && notes[i].time < 155f)
                    //    Plugin.Log.Info($"notesInBarBeat Loop ------------------------------------------");
                    notesInBarBeat.Clear();
                    for (; k < notesInBar.Count && Floor((notesInBar[k].time - firstBeatmapNoteTime - currentBarStart) /dividedBarLength) == j; k++)
                    {
                        notesInBarBeat.Add(notesInBar[k]);
                    }


                    if (notesInBarBeat.Count == 0)
                        continue;

                    float currentBarBeatStart = firstBeatmapNoteTime + currentBarStart + j * dividedBarLength; //. BW Testing this since creates walls touching notes sometimes *************

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
                    double timeDiff = 0;
                    
                    if (afterLastNote != null)
                    {
                        timeDiff = afterLastNote.time - lastNote.time;
                        double ratio = timeDiff / barLength;

                        const double threshold2 = 0.125; // 1/8
                        const double threshold3 = 0.5;   // existing 3-step threshold

                        if (ratio >= threshold3)
                            rotationCount = 3;
                        else if (ratio >= threshold2)
                            rotationCount = 2;
                        else
                            rotationCount = 1;

                        if (rotationCount == 3 && maxRotationStep >= 4)
                        {
                            double extremeRatioFor4 =
                                barDivider <= 2 ? 0.7 :   // sparse bar: slightly easier to get 4 (captures few 4's unless lower it)
                                1f;                       // dense bar: need a very large gap (captures fewer 4's if raise it)

                            if (ratio >= extremeRatioFor4)
                                rotationCount = 4;
                        }
                    }

                    // apply min/max mapping here
                    rotationCount += rotationStepOffset;
                    rotationCount = Math.Clamp(rotationCount, minRotationStep, maxRotationStep);

                    if (rotationCount == 1) count1++; if (rotationCount == 2) count2++; if (rotationCount == 3) count3++; if (rotationCount == 4) count4++;

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
                        int desiredSign = 0;   // initialize to something safe

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
                            if (totalRotation >= BottleneckRotations)
                                desiredSign = -1;
                            else if (totalRotation <= -BottleneckRotations)
                                desiredSign = +1;
                            else
                                desiredSign = previousDirectionPositive ? +1 : -1;
                        }

                        rotationStep = desiredSign * rotationCount;

                        if (rotationStep != 0)
                            previousDirectionPositive = rotationStep > 0;
                    }
                        
                    //In the middle of the range: rotations behave as your generator designs (gap-based sizes, 1–4).
                    //Once you drift too far to one side(bottleneck): it shrinks same - direction steps to the minimum size, preventing big kicks at high angles.
                    //near the configured hard limit: it turns same - direction steps into small opposite steps, gently bouncing the player back toward center instead of ever letting accumulated rotation run away.
                    if (!wireless360) // would be disabled anyone for Wireless360 since limitRotations and BottleneckRotations are set to 9999
                    { 
                        if (totalRotation >= BottleneckRotations && rotationStep > 0)
                        {
                            // too far right → clamp to smallest allowed positive step
                            if (rotationCount > minRotationStep)
                                rotationCount = minRotationStep;
                        }
                        else if (totalRotation <= -BottleneckRotations && rotationStep < 0)
                        {
                            // too far left → clamp to smallest allowed negative step (magnitude)
                            if (rotationCount > minRotationStep)
                                rotationCount = minRotationStep;
                        }

                        // Recompute rotationStep from (sign, magnitude) after bottleneck clamp
                        int dirSign = Math.Sign(rotationStep);
                        rotationStep = dirSign * rotationCount;

                        // --- 2. Hard limit: when we are near the absolute limit, flip direction instead of pushing further ---

                        if (totalRotation >= LimitRotations - minRotationStep && rotationStep > 0)
                        {
                            // too close to +Limit → force a turn in the opposite direction
                            rotationStep = -Math.Abs(rotationStep);
                        }
                        else if (totalRotation <= -LimitRotations + minRotationStep && rotationStep < 0)
                        {
                            // too close to -Limit → force a turn in the opposite direction
                            rotationStep = Math.Abs(rotationStep);
                        }

                        // Ensure rotationCount stays in sync with rotationStep magnitude
                        rotationCount = Math.Abs(rotationStep);
                    }
                    #region AddExtraRotations

                    //############################################################################
                    //had to add more rotations directly in the main loop. tried it outside this main loop. the problem with being outside the loop is you cannot decide if a map is really low on rotations until after the map is finished.
                    //add more rotation to maps without much rotation. If there are few rotations, look for directionless notes up/down/dot/bomb and make their rotation direction the same as the previous direction so that there will be increased totalRotation.
                    //keeps same magnitude, but flips sign to keep same direction streak
                    //Once rotation steps pass the RotationGroupLimit, make this inactive. Stay inactive for RotationGroupSize number of rotations and if there are few rotations while off, activate this again.
                    if (addExtraRotation)// && !Config.Instance.AddExtraRotationV2)
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
                                    //    Plugin.LogDebug($"[AddExtraRotation] lastNote time: {lastNote.time} r: {r} Old Rotation: {rotationStep} New Rotation: {newRotation}");// totalRotationsGroup: {totalRotationsGroup}");

                                    rotationStep = newRotation;

                                    totalRotationsGroup += rotationStep;
                                }

                            }
                            else //has now passed the rotation limit now
                            {
                                addMoreRotations = false;

                                totalRotationsGroup = 0;

                                //Plugin.Log.Info($"[AddExtraRotation] Change to NOT ACTIVE since passed the limit!!! RotationGroupLimit: {RotationGroupLimit}\t totalRotationsGroup: {totalRotationsGroup}");

                                offSetR = r; //need this since when passes the limit, r may be close or equal to being a multiple of RotationGroupSize. that means it could be active soon again. so need to offset r so it will stay off for RotationGroupSize rotations.(r - offSetR) will be 0 on first rotation...
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
                                        RotationGroupLimit += 4; //change the limit size for variety //could not alter RotationGroupSize since causing looping problem
                                    }
                                    else
                                    {
                                        RotationGroupLimit -= 4; //change the limit size for variety //could not alter RotationGroupSize since causing looping problem
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

                    #endregion

                    #region Chain Rotation Clamp

                    if (clampLongChainsRotations)
                    {
                        // --- Long-chain rotation clamp ---
                        // While we are inside a long chain, suppress >15° steps unless (and only allow 1 time)
                        // they “help” the chain: positive for rightish, negative for leftish.
                        

                        ESliderData activeChain = GetActiveLongChain(lastNote.time);
                        if (activeChain != null)
                        {
                            int chainPol = DirPolarity(activeChain.cutDirection);   // +1, -1 or 0
                            int stepSign = Math.Sign(rotationStep);
                            int stepMag = Math.Abs(rotationStep);

                            bool wrongDirection = (chainPol == 0 || chainPol != stepSign) ? true : false;

                            if (currentActiveChain != activeChain)
                            {
                                if (stepMag > 1) // more than 15°
                                {
                                    // Only allow big steps if they align with the chain's horizontal polarity.
                                    // Otherwise, clamp to ±1 so we never exceed 15° “against” the chain.
                                    if (wrongDirection)
                                    {
                                        rotationStep = stepSign * CLAMPED_STEP_SIZE;
                                        Plugin.LogDebug($"[RotationGenerator] Long Chain: {activeChain.time:F} dur: {(activeChain.tailTime - activeChain.time):F} {activeChain.cutDirection} reduced rotation at: {lastNote.time:F} {stepMag * stepSign * 15} --> {rotationStep * 15}.");
                                    }
                                    // else: chainPol == stepSign → allowed, keep 30/45/60° as-is
                                }
                            }
                            else if (wrongDirection)
                            {
                                Plugin.LogDebug($"[RotationGenerator] -- Same Chain reduced rotation at: {lastNote.time:F} {stepMag * stepSign * 15} --> 0.");
                                rotationStep = 0;
                            }
                            else
                                Plugin.LogDebug($"[RotationGenerator] -- Same Chain allowed full rotation at: {lastNote.time:F} {stepMag * stepSign * 15}.");

                            currentActiveChain = activeChain;
                        }
                        else
                        {
                            // No long chain active at this time: reset tracker
                            currentActiveChain = null;
                        }
                    }
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

                    //Plugin.LogDebug($"[PreRotate] t={lastNote.time:F2} step={rotationStep} count={rotationCount} " +$"timeDiff={timeDiff:F3} barLength={barLength:F3}");

                    Rotate(lastNote, rotationStep); //lastNote.time, rotationStep);

                    r++;

                    // --- MassiveStreak detector driven by emitted events ---
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

                        lastProcessedEvtIdx = newIdx;
                    }

                    

                    //Plugin.LogDebug($"Total Rotations: {totalRotation*15} Time: {lastNote.time:F} Rotation: {rotation*15}");
                    //Plugin.LogDebug($"[{currentBarBeatStart}] rotationStep: {rotationStep} (notesInBarBeat={notesInBarBeat.Count},leftCount={leftCount},rightCount={rightCount},lastNotes={lastNotes.Count()},rotationTime={lastNote.time},afterLastNote={afterLastNote?.time:F},rotationCount={rotationCount})");
                }

                //Plugin.LogDebug($"[{currentBarStart + firstBeatmapNoteTime}({(currentBarStart + firstBeatmapNoteTime) / beatDuration}) -> {currentBarEnd + firstBeatmapNoteTime}({(currentBarEnd + firstBeatmapNoteTime) / beatDuration})] count={notesInBar.Count} segments={builder} barDiviver={barDivider}");
            }
            //End main for loop over all notes

            Plugin.LogDebug($"[RotationGenerator] Rotation Events (after main loop) Count: {allRotations.Count} MinRotationStep: {minRotationStep} MaxRotationStep: {maxRotationStep} -- 15deg: {count1}, 30deg: {count2}, 45deg: {count3}, 60deg: {count4}");

            // -----------------------------------------------------------------------------------------------
            #endregion


            CloseSameDirectionStreak(allRotations.Count); // close trailing run safely

            Plugin.LogDebug($"[RotationGenerator][MassiveStreak] Total detected streaks: {massiveStreaks.Count} (using: MassiveStreakNumberOfRotationsThreshold: {Config.Instance.MassiveStreakNumberOfRotationsThreshold})");

            if (massiveStreaks.Count > 0)
            {
                AddRotationsIntoMassiveStreak(allRotations, flexibleRotations, massiveStreaks);
            }

            if (allRotations.Count > 0)
            {
                allRotations = ERotationEventData.RecalculateAccumulatedRotations(allRotations);

                #if DEBUG
                (float accumRot, float time) high = (0, 0);
                (float accumRot, float time) low = (0, 0);
                int endRot = 0;

                foreach (var rot in allRotations)
                {
                    endRot = rot.accumRotation;
                    if (rot.accumRotation < low.accumRot)
                        low = (rot.accumRotation, rot.time);
                    else if (rot.accumRotation > high.accumRot)
                        high = (rot.accumRotation, rot.time);

                    int ro = Math.Abs(rot.rotation);
                    //if (ro == 0) d0++; if (ro == 15) d15++; if (ro == 30) d30++; if (ro == 45) d45++; if (ro == 60) d60++; if (ro == 75) d75++; if (ro == 90) d90++; if (ro == 120) d120++;
                    //if (rot.time > 200)
                    //    Plugin.Log.Info($"2 Rotation - Time: {rot.time} - Rotation: {rot.rotation} - Total Rotation: {rot.accumRotation}");
                }
                Plugin.Log.Info($"[RotationGenerator] Rotation Count: {allRotations.Count}, Largest '-' Rot: {low.accumRot} (time: {low.time:F}), Largest '+' Rot: {high.accumRot} (time: {high.time:F}), Final Rotation: {endRot} --- Wireless360: {Config.Instance.Wireless360}, LimitRot: {Config.Instance.LimitRotations360}, AddExtraRot: {Config.Instance.AddExtraRotation}, RotSpeedMult: {RotationSpeedMultiplier}, MinRotSize: {Config.Instance.MinRotationSize}, MaxRotSize: {Config.Instance.MaxRotationSize}, FOV: {Config.Instance.FOV}, TimeWin: {Config.Instance.TimeWindow}");
                //Plugin.LogDebug($"[RotationGenerator] Rotation Count: {allRotations.Count} -- 0deg: {d0} 15deg: {d15}, 30deg: {d30}, 45deg: {d45}, 60deg: {d60}, 75deg: {d75}, 90deg: {d90}, 120deg: {d120}");
                #endif

                #region Remove Bombs when map turns

                // Remove bombs (just problematic ones) iterate backwards
                // Build list that ties rotation events directly
                var bombCutMoments = new List<(float time, int rotation, ERotationEventData evt)>();

                for (int b = 0; b < allRotations.Count; b++)
                {
                    var rot = allRotations[b];
                    bombCutMoments.Add((rot.time, rot.rotation, rot));
                }


                // Track which rotation events to remove
                var rotsToRemove = new HashSet<ERotationEventData>();

                int originalBombCount = eData.BombNotes.Count;
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
                eData.BombNotesChanged = false;
                int bombsRemoved = originalBombCount - eData.BombNotes.Count;

                if (bombsRemoved > 0)
                {
                    eData.BombNotesChanged = true;
                    eData.BombNotes = eData.BombNotes.OrderBy(n => n.time).ToList();
                }

                // Remove the marked rotation events
                if (rotsToRemove.Count > 0)
                {
                    allRotations = allRotations
                        .Where(p => !rotsToRemove.Contains(p))
                        .OrderBy(p => p.time)
                        .ToList();
                }

                if (bombsRemoved > 0 || rotsToRemove.Count > 0)
                {
                    Plugin.LogDebug($"[RotationGenerator] Removed {bombsRemoved} bombs due to conflicting rotations. Removed {rotsToRemove.Count} rotations since bomb is gone now. Final Rotation Count: {allRotations.Count}");
                    eData.BombNotesChanged = true;
                }


                allRotations = ERotationEventData.RecalculateAccumulatedRotations(allRotations);

                if (rotsToRemove.Count > 0)
                {
                    d0 = 0; d15 = 0; d30 = 0; d45 = 0; d60 = 0; d75 = 0; d90 = 0; d120 = 0;
                    foreach (var rot in allRotations)
                    {
                        int ro = Math.Abs(rot.rotation);
                        if (ro == 0) d0++; if (ro == 15) d15++; if (ro == 30) d30++; if (ro == 45) d45++; if (ro == 60) d60++; if (ro == 75) d75++; if (ro == 90) d90++; if (ro == 120) d120++;
                    }
                    Plugin.LogDebug($"[RotationGenerator] Rotation Count: {allRotations.Count} -- 0deg: {d0}, 15deg: {d15}, 30deg: {d30}, 45deg: {d45}, 60deg: {d60}, 75deg: {d75}, 90deg: {d90}, 120deg: {d120}");
                }

                eData.RotationEventsChanged = false;

                if (originalRotations.Count != allRotations.Count)
                    eData.RotationEventsChanged = true;


                #endregion


                #region Optimize FOV

                //bool wallsAdded = originalWallCount <= 5000 && (WallGenerator._generatedStandardWalls.Count > 0 || WallGenerator._generatedExtensionWalls.Count > 0);
                //Plugin.LogDebug($"WallGenerator._generatedStandardWalls: {WallGenerator._generatedStandardWalls.Count} WallGenerator._generatedExtensionWalls: {WallGenerator._generatedExtensionWalls.Count}");

                if (Utils.IsEnabledFOV(Utils.IsEnabledWalls()) && allRotations.Count > 0 && Config.Instance.TimeWindow > 0) // use this for nonGen360 maps with wall gen since old 360fyer generated maps have wild rotations that cause walls to reverse through the frame. this will not help some walls blocking player vision that are built into 360fyer old generated output
                {
                    int prevRots = allRotations.Count;

                    allRotations.Sort((a, b) => a.time.CompareTo(b.time)); // Sort the rotations by time

                    OptimizeRotationsToFOV optimize = new OptimizeRotationsToFOV(
                        allRotations,
                        Config.Instance.TimeWindow,
                        Config.Instance.FOV
                    );


                    allRotations = optimize.FOVFix(); // Call the ModifyRotations method to adjust the rotations and get the modified list

                    allRotations = ERotationEventData.RecalculateAccumulatedRotations(allRotations);

                    if (!Config.Instance.Wireless360 && optimize.RotationsWereAdjusted)
                        needsRotationLimitAdjustment = true;

                    if (prevRots != allRotations.Count)
                        eData.RotationEventsChanged = true;
                }

                Plugin.LogDebug($"[RotationGenerator] 3 Rotation List (after FOV)  Count: {allRotations.Count}");

                d0 = 0; d15 = 0; d30 = 0; d45 = 0; d60 = 0; d75 = 0; d90 = 0; d120 = 0;
                foreach (var rot in allRotations)
                {
                    int ro = Math.Abs(rot.rotation);
                    if (ro == 0) d0++; if (ro == 15) d15++; if (ro == 30) d30++; if (ro == 45) d45++; if (ro == 60) d60++; if (ro == 75) d75++; if (ro == 90) d90++; if (ro == 120) d120++;
                }
                Plugin.LogDebug($"[RotationGenerator] Rotation Count: {allRotations.Count} -- 0deg: {d0}, 15deg: {d15}, 30deg: {d30}, 45deg: {d45}, 60deg: {d60}, 75deg: {d75}, 90deg: {d90}, 120deg: {d120}");

                /*
                foreach (var rot in allRotations)
                {
                    if (rot.time < 20)
                        Plugin.Log.Info($"3 Rotation - Time: {rot.time} - Rotation: {rot.rotation} - Total Rotation: {rot.accumRotation}");
                }
                */
                #endregion

                if (Config.Instance.ArcFixFull)
                {
                    int prevRots = allRotations.Count;

                    allRotations = Arcitect.ArcFix(allRotations, eData); // this is for Gen 360 only -- Clearing the list is not needed according to AI. nonGen maps use arcFix() from HarmonyPatches.cs

                    if (!Config.Instance.Wireless360)
                        needsRotationLimitAdjustment = true;

                    if (prevRots != allRotations.Count)
                        eData.RotationEventsChanged = true;
                }
                else
                    Plugin.LogDebug($"[RotationGenerator] ArcFix not enabled or not applicable. Starting Game Mode: {TransitionPatcher.SelectedSerializedName} - Characteristic: {TransitionPatcher.SelectedSerializedName}");


                if (!Config.Instance.Wireless360 && Config.Instance.MinRotationSize > 15)
                    needsRotationLimitAdjustment = true;

                if (needsRotationLimitAdjustment)
                {
                    Plugin.LogDebug($"[RotationGenerator] Rotation limits were adjusted.");
                    allRotations = AdjustRotationsToLimit(allRotations);

                    eData.RotationEventsChanged = true;
                }


                allRotations = ERotationEventData.RecalculateAccumulatedRotations(allRotations);

                Plugin.LogDebug($"[RotationGenerator] 4 Rotation List (after ArcFix)  Count: {allRotations.Count} (has accurate accum)");
                /*
                foreach (var rot in allRotations)
                {
                    if (rot.time < 20)
                    {
                        Plugin.Log.Info($"3 Rotation - Time: {rot.time:F} - Rotation: {rot.rotation} - Total Rotation: {rot.accumRotation}"); // accum is accurate here
                    }
                }
                */
                eData.RotationEvents = allRotations; // Update the eData with the final rotations

                eData.WallCutMoments.Clear(); // arcfix will cause changes to the rotation so need to recalculate wallcutmoments

                foreach (var rotation in eData.RotationEvents)
                {
                    eData.WallCutMoments.Add((rotation.time, SpawnRotationDegreesToSteps(rotation.rotation)));
                    //Plugin.Log.Info($"wallCutMoments - time: {rotation.time} rotation: {(int)(rotation.rotation )}");
                }
                Plugin.LogDebug($"[RotationGenerator] WallCutMoments generated with count: {eData.WallCutMoments.Count}");

                Plugin.LogDebug($"[RotationGenerator] 5 Rotation Events Count: {eData.RotationEvents.Count()}");

                //Plugin.LogDebug($"[RotationGenerator] Starting Per Object Rotations after this moment:");
                /*
                foreach (var rot in allRotations)
                {
                    if (rot.time < 20)
                        Plugin.Log.Info($"4 Rotation - Time: {rot.time} - Rotation: {rot.rotation} - Total Rotation: {rot.accumRotation}");
                }
                */
            }

            /// <summary> ArcFix(), MinRotationSize larger than 15, and FOVFix() cause rotations to move beyond the limits of LimitRotations360</summary> 
            List<ERotationEventData> AdjustRotationsToLimit(List<ERotationEventData> rotations)
            {
                int rotationLimit = (int)Config.Instance.LimitRotations360;
                int halfLimit = rotationLimit / 2;

                int currentRotation = 0;
                var adjustedRotations = new List<ERotationEventData>();

                int flippedRotationCount = 0;

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
                            flippedRotationCount++;
                        }
                        // If flipping still goes out of bounds, skip the rotation (optional)

                    }
                }
                adjustedRotations.Sort((a, b) => a.time.CompareTo(b.time));

                Plugin.LogDebug($"[AdjustRotationsToLimit] Flipped Rotation Count: {flippedRotationCount}");

                return adjustedRotations;
            }
        }

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
            int minRun = 6, // change these to control segment lengths
            int maxRun = 16,
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


        /// <summary>
        /// Converts a spawn rotation angle (in degrees) to the legacy Beat Saber rotation event value for v2 maps.
        /// Returns null if the angle is not one of the legacy values.
        /// </summary>
        public static int SpawnRotationDegreesToValueV2(int degrees)
        {
            switch ((int)degrees)
            {
                case -60: return 0;
                case -45: return 1;
                case -30: return 2;
                case -15: return 3;
                case 15: return 4;
                case 30: return 5;
                case 45: return 6;
                case 60: return 7;
                default: return 4;//null; // Or throw, or clamp, as you see fit
            }
        }
        public static int SpawnRotationDegreesToSteps(int degrees)
        {
            switch ((int)degrees)
            {
                case -60: return -4;
                case -45: return -3;
                case -30: return -2;
                case -15: return -1;
                case 15: return 1;
                case 30: return 2;
                case 45: return 3;
                case 60: return 4;
                default: return 1;//null; // Or throw, or clamp, as you see fit
            }
        }
        public static int SpawnRotationValueToDegrees(int value)
        {
            switch (value)
            {
                case 0: return -60;
                case 1: return -45;
                case 2: return -30;
                case 3: return -15;
                case 4: return 15;
                case 5: return 30;
                case 6: return 45;
                case 7: return 60;
                default: return 0;//null; // Or throw, or clamp, as you see fit
            }
        }
        public static int SpawnRotationDegreesToValue(int value)
        {
            switch (value)
            {
                case -60: return 0;
                case -45: return 1;
                case -30: return 2;
                case -15: return 3;
                case 15: return 4;
                case 30: return 5;
                case 45: return 6;
                case 60: return 7;
                default: return 4;//not 0 which is 60 degrees
            }
        }
        public static int SpawnRotationValueToStepsV2(int value)
        {
            switch (value)
            {
                case 0: return -4;
                case 1: return -3;
                case 2: return -2;
                case 3: return -1;
                case 4: return 1;
                case 5: return 2;
                case 6: return 3;
                case 7: return 4;
                default: return 1;//null; // Or throw, or clamp, as you see fit
            }
        }
    }
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
}
