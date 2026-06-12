using AutoBS.Patches;
using BeatmapSaveDataVersion4;
using CustomJSONData.CustomBeatmap;
using HMUI;
using IPA.Config.Data;
using JetBrains.Annotations;
using System;
using System.Collections.Generic;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Reflection;
using UnityEngine;
using static NoteData;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.Tab;


namespace AutoBS
{
    internal class Arcitect//Lolighter https://github.com/Loloppe/Lolighter/blob/master/Lolighter/Algorithm/Arc.cs
    {
        public static List<ENoteData> notes; // hold original notes for various checks
        public static List<ENoteData> notesAndBombs;
        public static List<ESliderData> arcs; // hold finished arcs for various chains checks
        public static Version version = new Version(3, 3, 0);

        public static int preferredArcCount = 1;
        public static float arcMultiplier = 1f;

        public static Config.ArcSwingModeType ArcSwingMode = Config.Instance.ArcSwingMode;

        public static float lowestPossibleMinDurationAdjustment = 0.4f;

        public static NoteLineLayer headBeforeJumpLineLayer = 0; // set automatically in game from json maps but must set it since i'm creating arcs. 
        public static NoteLineLayer tailBeforeJumpLineLayer = 0; 

        public static float headCutDirectionAngleOffset = 0f;
        public static float tailCutDirectionAngleOffset = 0f; 

        public static SliderMidAnchorMode sliderMidAnchorMode = SliderMidAnchorMode.Straight;

        public static float squishAmount = 1f; // burst slider only - .5 is squeezed together and 1.5 is unsqueezed - cannot be 0. A float which represents squish factor. This is the proportion of how much of the path from (x,y) to (tx, ty) is used by the chain. This does not alter the shape of the path. Values greater than 1 will extend the path beyond the specified end point.

        public static bool addArc;

        //CHAINS-------------------------

        public static float pauseThresholdMultiplier = 1.5f;// makes pause detection adaptive to the varying average speeds of different sections of the song

        public static int preferredChainCount = 1;
        public static bool forceMoreChains = false;
        public static bool pauseDetection = true; // algorithm type

        public static int doubleChainCount = 0;

        public static CustomBeatmapData data;

        /// <summary>
        /// Change a notes position to make chain possible. For example, move a note up to allow a chain below it. 
        /// Didn't like using this since it alters notes and usually makes awkward moments
        /// </summary>
        public static List<(ENoteData, ENoteData)> alteredNotes = new List<(ENoteData, ENoteData)>();

        public static List<ESliderData> chainsTemp = new List<ESliderData>(); // used by MoveWallsBlockingChainTail()

        public static int chainArcToggle = 0; // toggle between arcs (most of the time) and ChairArcs (long chain with tail note)

        public static string ScoreSubmissionDisableText = "";


        /// <summary>
        /// Method to create Arcs and Chains. Arcs between two notes of the same colorType based on their cut direction and the duration between notes. Chains are creaed based on pause detection between notes.
        /// </summary>
        /// <returns>List of SliderData and writes to BeatmapData</returns>
        public static void CreateSliders(EditableCBD eData) // reference list so no need to return it.
        {
            ScoreSubmissionDisableText = "";

            if (eData.ColorNotes.Count == 0) return;
            //If AlterNotes() is used, the original notes of the song would be altered permanently in the context of that data object.

            notes = eData.ColorNotes; // hold original notes for various checks
            notesAndBombs = new List<ENoteData>(eData.ColorNotes);
            notesAndBombs.AddRange(eData.BombNotes);

            doubleChainCount = 0;
            
            float songDuration = (eData.ColorNotes.Last().time - eData.ColorNotes.First().time) / 60; // minutes between 1st and last note

            Plugin.LogDebug($"[Arcitect][CreateSliders] Song Duration: {songDuration} sec. ColorNote Count: {eData.ColorNotes.Count()} 1st Note at: {eData.ColorNotes.First().time} last note at: {eData.ColorNotes.Last().time}");

            preferredArcCount = (int)(songDuration * Config.Instance.PreferredArcCountPerMin);
            preferredChainCount = (int)(songDuration * Config.Instance.PreferredChainCountPerMin);

            forceMoreChains = Config.Instance.ForceMoreChains;

            pauseDetection = Config.Instance.PauseDetection;

            List<ENoteData> colorA = new List<ENoteData>();
            List<ENoteData> colorB = new List<ENoteData>();

            // We need to sort the notes per type
            foreach (ENoteData note in eData.ColorNotes)
            {
                if (note.colorType == ColorType.ColorA)
                {
                    colorA.Add(note);
                }
                else if (note.colorType == ColorType.ColorB)
                {
                    colorB.Add(note);
                }
            }
            if (eData.Arcs.Count == 0 && Utils.IsEnabledArcs())//only add arcs if map doesn't have any
            {
                Plugin.LogDebug($"[Arcitect][CreateSliders] Preferred Arc Count: {preferredArcCount}.");

                ArcSwingMode = Config.Instance.ArcSwingMode;

                eData.Arcs = SetPreferredArcCount(colorA, colorB);

                eData.Arcs = eData.Arcs.OrderBy(o => o.time).ToList(); // must do it this way with the =

                if (eData.Arcs.Count > 0)
                {
                    eData.ArcsChanged = true;
                    eData.ArcsChangedByArcitect = true;
                }

                arcs = eData.Arcs; // save arcs to static variable for chains to use

                //bool moveWallsBlockingArc = false;
                //if (!Utils.IsEnabledWalls() || Utils.IsEnabledWalls() && (!Config.Instance.EnableStandardWalls || !Config.Instance.EnableBigWalls)) // this should be used whenever chains are added. but should be done after standard/big walls are added since those walls can block chain tails too
                //    moveWallsBlockingArc = WallGenerator.MoveWallsBlockingArc(eData);
            }
            else
            {
                Plugin.LogDebug($"[Arcitect][CreateSliders] Map already has {eData.Arcs.Count} arcs so will not add any.");
                arcs = eData.Arcs; // save arcs to static variable for chains to use
            }

            if (eData.Chains.Count == 0 && Utils.IsEnabledChains())//only add chains if map doesn't have any
            {
                Plugin.LogDebug($"[Arcitect][CreateSliders] Preferred Chain Count: {preferredChainCount}.");

                if (pauseDetection) // USING THIS <-------------------------------------------------------
                {
                    Plugin.LogDebug($"[Arcitect][CreateSliders] Chain - Pause Detection Algorithm.");
                    eData.Chains = SetPreferredChainCountPauseDetection(colorA, colorB);
                }
                else
                {
                    Plugin.LogDebug($"[Arcitect][CreateSliders] Chain - Tempo Change Algorithm.");
                    eData.Chains = SetPreferredChainCountTempoChange(colorA, colorB);
                }

                eData.Chains = eData.Chains.OrderBy(o => o.time).ToList(); // sort chains by time

                if (eData.Chains.Count > 0)
                {
                    ScoreSubmissionDisableText = "Arcitect Chains";
                    eData.ChainsChanged = true;
                    eData.ChainsChangedByArcitect = true;
                }

                //if (!Utils.IsEnabledWalls() || Utils.IsEnabledWalls() && (!Config.Instance.EnableStandardWalls || !Config.Instance.EnableBigWalls)) // this should be used whenever chains are added. but should be done after standard/big walls are added since those walls can block chain tails too
                //    WallGenerator.MoveWallsBlockingChainTail(eData);
            }
            else
            {
                Plugin.LogDebug($"[Arcitect][CreateSliders] Map already has {eData.Chains.Count} chains so will not add any.");

                ScoreSubmissionDisableText = "";
            }

            Plugin.LogDebug($"[Arcitect][CreateSliders] after Creation: Arcs Count: {eData.Arcs.Count}. Chains Count: {eData.Chains.Count}.");
        }

        public static List<ESliderData> SetPreferredArcCount(List<ENoteData> colorA, List<ENoteData> colorB) // https://github.com/Loloppe/Lolighter/blob/master/Lolighter/Algorithm/Arc.cs
        {
            if (colorA.Count < 3 && colorB.Count < 3)
                return new List<ESliderData>();

            float arcMult = arcMultiplier;

            float minDur = Config.Instance.MinArcDuration;

            List<ESliderData> arcs = new List<ESliderData>();

            if (colorA.Count > 1)
                arcs.AddRange(CreateArcs(colorA, arcMult, minDur));
            if (colorB.Count > 1)
                arcs.AddRange(CreateArcs(colorB, arcMult, minDur));

            if (arcs.Count < preferredArcCount) // check if produced too few arcs
            {
                Plugin.LogDebug($"[Arcitect] Using arcMultiplier: {arcMult} minDuration: {minDur} - Not enough arcs produced: {arcs.Count} which is {preferredArcCount - arcs.Count} less than desired.");

                //reduce minDur
                minDur = Config.Instance.MinArcDuration - lowestPossibleMinDurationAdjustment / 2;
                if (minDur < 0) minDur = .2f;

                //2nd pass: add arcs to arcs list using default arcMult and shorter minDur
                arcs.Clear();

                if (colorA.Count > 1)
                    arcs.AddRange(CreateArcs(colorA, arcMult, minDur));
                if (colorB.Count > 1)
                    arcs.AddRange(CreateArcs(colorB, arcMult, minDur));

                if (arcs.Count < preferredArcCount) // check if produced too few arcs
                {
                    Plugin.LogDebug($"[Arcitect] Using arcMultiplier: {arcMult} minDuration: {minDur} - Not enough arcs produced: {arcs.Count} which is {preferredArcCount - arcs.Count} less than desired.");

                    //reduce minDur again
                    minDur = Config.Instance.MinArcDuration - lowestPossibleMinDurationAdjustment;
                    if (minDur < 0) minDur = .2f;

                    //3rd pass: add arcs to arcs list using default arcMult and even shorter minDur
                    arcs.Clear();

                    if (colorA.Count > 1)
                        arcs.AddRange(CreateArcs(colorA, arcMult, minDur));
                    if (colorB.Count > 1)
                        arcs.AddRange(CreateArcs(colorB, arcMult, minDur));
                }
            }

            if (arcs.Count > preferredArcCount) // check if there are too many arcs
            {
                Plugin.LogDebug($"[Arcitect] Using arcMultiplier: {arcMult} minDuration: {minDur} - Too many arcs produced: {arcs.Count}  which is {arcs.Count - preferredArcCount} more than desired so will adjust arcMultiplier.");

                // Reduce arcMultiplier to decrease the number of arcs to the perfect amount
                arcMult = (float)Math.Max(0.1, arcMultiplier * (preferredArcCount / (float)arcs.Count));

                Plugin.LogDebug($"[Arcitect] Using arcMultiplier: {arcMult} minDuration: {minDur} - should produce the preferred count of {preferredArcCount}.");

                //4th pass maybe: add arcs to arcs list using reduced arcMult and latest minDur
                arcs.Clear();

                if (colorA.Count > 1)
                    arcs.AddRange(CreateArcs(colorA, arcMult, minDur));
                if (colorB.Count > 1)
                    arcs.AddRange(CreateArcs(colorB, arcMult, minDur));

                Plugin.LogDebug($"[Arcitect] Arc Count is: {arcs.Count} which is {arcs.Count - preferredArcCount} more than desired. Arc Swing Mode: {ArcSwingMode}.");
            }
            else if (arcs.Count == preferredArcCount)
                Plugin.LogDebug($"[Arcitect] Arc Count is: {arcs.Count}. That is the desired goal! Arc Swing Mode: {ArcSwingMode}.");
            else
                Plugin.LogDebug($"[Arcitect] Using arcMultiplier: {arcMult} minDuration: {minDur} - Not enough arcs produced :( But we reached the limit so we are done! Final count: {arcs.Count}. Arc Swing Mode: {ArcSwingMode}.");


            return arcs;
        }

        static List<ESliderData> CreateArcs(List<ENoteData> notes, float arcMult, float minDur) // https://github.com/Loloppe/Lolighter/blob/8815aac4e050bbc0485e5fb1748258710192e3dc/Lolighter/Info/Helper.cs
        {
            List<ESliderData> arcs = new List<ESliderData>();
            int countArcs = 0;

            //This is a way to reduce the number of iterations by percent. Ex: an arcMult of 43 is 43%. so the loop will trigger the 1st 4 times of 10 times etc. With cycleLength = 0, this is imprecise and will round to 10% increments. so arcMult is effectively only 100%, 90%, 80% etc.
            arcMult *= 100;// percent
            int counter = 0;
            int cycleLength = 10; // Fixed cycle length
            int triggerCount = (int)(arcMult * cycleLength) / 100; // Number of times condition should be true per cycle

            ColorType colorType = notes[0].colorType;

            for (int i = 0; i < notes.Count - 1; i++)
            {
                ENoteData note1 = notes[i]; ENoteData note2 = notes[i + 1];

                // Look for note.time pairs that are larger than minDur and smaller than maxDur and that are not dot notes.
                if ((note2.time - note1.time >= minDur) && (note2.time - note1.time < Config.Instance.MaxArcDuration))// && (note1.cutDirection != NoteCutDirection.Any) && (note2.cutDirection != NoteCutDirection.Any))//only works for notes pairs of a certain duration and must not be dots.
                {
                    if (counter < triggerCount)
                    {
                        // Check if the current swing is contrary to the previous swing.
                        if (IsCompatibleForArc(note1, note2))
                        {
                            float headControlPointLengthMultiplier = 1;// Config.Instance.ControlPointLength; // A float which represents how far the arc goes from the head of the arc. If head direction is a dot, this does nothing.        
                            float tailControlPointLengthMultiplier = 1;

                            if (note1.cutDirection == NoteCutDirection.Left || note1.cutDirection == NoteCutDirection.Right)
                                headControlPointLengthMultiplier = 2; // if control point is higher than 1 on up and down notes, arc will be too large and will be visibly squared off.
                            if (note2.cutDirection == NoteCutDirection.Left || note2.cutDirection == NoteCutDirection.Right)
                                tailControlPointLengthMultiplier = 2; // can be larger arc

                            if (note2.cutDirection == NoteCutDirection.Left || note2.cutDirection == NoteCutDirection.UpLeft || note2.cutDirection == NoteCutDirection.DownLeft)
                                sliderMidAnchorMode = SliderMidAnchorMode.Clockwise;
                            else if (note2.cutDirection == NoteCutDirection.Right || note2.cutDirection == NoteCutDirection.UpRight || note2.cutDirection == NoteCutDirection.DownRight)
                                sliderMidAnchorMode = SliderMidAnchorMode.CounterClockwise;
                            else
                                sliderMidAnchorMode = SliderMidAnchorMode.Straight;

                            ESliderData newArc = ESliderData.CreateArc(colorType, note1.time,
                                note1.line, note1.layer, note1.cutDirection,
                                note2.time, note2.line, note2.layer, note2.cutDirection, note1.rotation, //for nonGen360 or 90 maps, need to pass their rotation
                                sliderMidAnchorMode,
                                headControlPointLengthMultiplier, tailControlPointLengthMultiplier,
                                note1, note2);

                            arcs.Add(newArc);
                            countArcs++;
                            note1.headNoteArc = newArc; note1.scoringType = NoteData.ScoringType.ArcHead;
                            note2.tailNoteArc = newArc; note2.scoringType = NoteData.ScoringType.ArcTail;
                        }


                    }
                    counter++;
                    if (counter >= cycleLength)
                    {
                        counter = 0; // Reset counter at the end of the cycle
                    }

                }
                //Plugin.LogDebug($"{colorType} Arc Count: {countArcs}.");
            }
            return arcs;
        }
        public static bool IsCompatibleForArc(ENoteData note1, ENoteData note2)
        {
            //Plugin.LogDebug($"IsCompatibleForArc: Checking notes at t1={note1.time:F3} scoringType={note1.scoringType} t2={note2.time:F3} scoringType={note2.scoringType}");

            if (note1.headNoteChain != null) return false; // Never use chain heads as arc heads. chain heads would be from the original map in this case

            bool allowDotHead = Config.Instance.AllowArcHeadDotNotes; 
            bool allowDotTail = Config.Instance.AllowArcTailDotNotes;
            bool headIsDot = note1.cutDirection == NoteCutDirection.Any;
            bool tailIsDot = note2.cutDirection == NoteCutDirection.Any;

            if (headIsDot || tailIsDot)
            {
                // Head dot not allowed
                if (headIsDot && !allowDotHead) return false;

                // Tail dot not allowed
                if (tailIsDot && !allowDotTail) return false;

                // If we get here, any present dots are allowed by config
                Plugin.LogDebug($"[Arcitect] IsCompatibleForArc: Dot allowed. head={note1.cutDirection} tail={note2.cutDirection} t1={note1.time:F3} t2={note2.time:F3}");
                return true; // if your policy is "dots bypass contrary checks"
            }

            if (ArcSwingMode == Config.ArcSwingModeType.Curated180and135)
                return IsContraryLikeDirectionBetweenSwings180and135(note1, note2);
            else if (ArcSwingMode == Config.ArcSwingModeType.Curated180and135and90)
                return IsContraryLikeDirectionBetweenSwings180and135and90(note1, note2);
            else if (ArcSwingMode == Config.ArcSwingModeType.All180and135)
                return IsContraryByDegrees(note1, note2, 135);
            else
                return IsContraryByDegrees(note1, note2, 90);
            //return Config.Instance.ForceNaturalArcs
            //    ? IsContraryByDegrees(note1, note2, 135) //IsContraryLikeDirectionBetweenSwings(note1, note2)
            //    : IsContraryByDegrees(note1, note2, 90); // less natural less restrictive
        }
        public static bool IsContraryLikeDirectionBetweenSwings180and135and90(ENoteData n1, ENoteData n2)
        {
            if (n1.cutDirection == NoteCutDirection.Any || n2.cutDirection == NoteCutDirection.Any)
                return true; // always good

            Vector2 d1 = n1.cutDirection.Direction(); // returns vector from NoteCutDirectionExtensions decompiled code
            Vector2 d2 = n2.cutDirection.Direction();
            if (d1 == Vector2.zero || d2 == Vector2.zero) // means has NO actual direction
                return false;

            int diff = Mathf.Clamp(Mathf.RoundToInt(Vector2.Angle(d1, d2)), 0, 180);

            if (diff == 180) return true;

            if (diff != 90 && diff != 135) return false;

            if (diff == 135) return IsContraryBy135Degrees(n1, n2);

            if (diff == 90) return IsContraryBy90Degrees(n1, n2);

            return false;
        }
        public static bool IsContraryBy90Degrees(ENoteData n1, ENoteData n2)
        {
            if (n2.cutDirection == NoteCutDirection.Up || n2.cutDirection == NoteCutDirection.UpLeft || n2.cutDirection == NoteCutDirection.UpRight)
                return false;
            else
                return true; // allows 90 degree for all other variations
        }
        public static bool IsContraryBy135Degrees(ENoteData n1, ENoteData n2)
        {
            if (n1.colorType == ColorType.ColorA) // right blue notes - right favors right and down
            {
                switch (n1.cutDirection)
                {
                    case NoteCutDirection.Up:
                        {
                            if (n2.cutDirection == NoteCutDirection.DownRight) // Missing: Up → DownLeft
                                return true;
                            else
                                return false;
                        }
                    case NoteCutDirection.Down:
                        {
                            if (n2.cutDirection == NoteCutDirection.UpRight) // Missing: Down → UpLeft
                                return true;
                            else
                                return false;
                        }
                    case NoteCutDirection.Left:
                        {
                            if (n2.cutDirection == NoteCutDirection.DownRight) // Missing: Left → UpRight
                                return true;
                            else
                                return false;
                        }
                    case NoteCutDirection.Right:
                        {
                            if (n2.cutDirection == NoteCutDirection.DownLeft) // Missing: Right → UpLeft
                                return true;
                            else
                                return false;
                        }
                    case NoteCutDirection.UpLeft: // Missing: UpLeft → Right, UpLeft → Down
                    case NoteCutDirection.UpRight: // Missing: UpRight → Left, UpRight → Down
                    case NoteCutDirection.DownLeft: // Missing: DownLeft → Right, DownLeft → Up
                    case NoteCutDirection.DownRight: // Missing: DownRight → Left, DownRight → Up
                    default: // For any other cases
                        {
                            return false;
                        }
                }
            }
            else // left red notes left favors left and down
            {
                switch (n1.cutDirection)
                {
                    case NoteCutDirection.Up:
                        {
                            if (n2.cutDirection == NoteCutDirection.DownLeft) // all these are missing the same strokes as above
                                return true;
                            else
                                return false;
                        }
                    case NoteCutDirection.Down:
                        {
                            if (n2.cutDirection == NoteCutDirection.UpLeft)
                                return true;
                            else
                                return false;
                        }
                    case NoteCutDirection.Left:
                        {
                            if (n2.cutDirection == NoteCutDirection.DownRight)
                                return true;
                            else
                                return false;
                        }
                    case NoteCutDirection.Right:
                        {
                            if (n2.cutDirection == NoteCutDirection.DownLeft)
                                return true;
                            else
                                return false;
                        }
                    case NoteCutDirection.UpLeft:
                    case NoteCutDirection.UpRight:
                    case NoteCutDirection.DownLeft:
                    case NoteCutDirection.DownRight:
                    default:
                        {
                            return false;
                        }
                }
            }
        }
        public static bool IsContraryLikeDirectionBetweenSwings180and135(ENoteData n1, ENoteData n2) // new BW curated version that takes into account how it feels to reverse the swing of each hand
        {
            if (n1.colorType == ColorType.ColorA) // right blue notes - right favors right and down
            {
                switch (n1.cutDirection)
                {
                    case NoteCutDirection.Up:
                        {
                            if (n2.cutDirection == NoteCutDirection.Down || n2.cutDirection == NoteCutDirection.DownRight) // Missing: Up → DownLeft
                                return true;
                            else
                                return false;
                        }
                    case NoteCutDirection.Down:
                        {
                            if (n2.cutDirection == NoteCutDirection.Up || n2.cutDirection == NoteCutDirection.UpRight) // Missing: Down → UpLeft
                                return true;
                            else
                                return false;
                        }
                    case NoteCutDirection.Left:
                        {
                            if (n2.cutDirection == NoteCutDirection.Right || n2.cutDirection == NoteCutDirection.DownRight) // Missing: Left → UpRight
                                return true;
                            else
                                return false;
                        }
                    case NoteCutDirection.Right:
                        {
                            if (n2.cutDirection == NoteCutDirection.Left || n2.cutDirection == NoteCutDirection.DownLeft) // Missing: Right → UpLeft
                                return true;
                            else
                                return false;
                        }
                    case NoteCutDirection.UpLeft: // Missing: UpLeft → Right, UpLeft → Down
                        {
                            if (n2.cutDirection == NoteCutDirection.DownRight)// || note2.cutDirection == NoteCutDirection.UpRight) // added 2/22
                                return true;
                            else
                                return false;
                        }
                    case NoteCutDirection.UpRight: // Missing: UpRight → Left, UpRight → Down
                        {
                            if (n2.cutDirection == NoteCutDirection.DownLeft)// || note2.cutDirection == NoteCutDirection.DownRight) // added 2/22
                                return true;
                            else
                                return false;
                        }
                    case NoteCutDirection.DownLeft: // Missing: DownLeft → Right, DownLeft → Up
                        {
                            if (n2.cutDirection == NoteCutDirection.UpRight)// || note2.cutDirection == NoteCutDirection.DownRight) // added 2/22
                                return true;
                            else
                                return false;
                        }
                    case NoteCutDirection.DownRight: // Missing: DownRight → Left, DownRight → Up
                        {
                            if (n2.cutDirection == NoteCutDirection.UpLeft)// || note2.cutDirection == NoteCutDirection.DownLeft) // added 2/22
                                return true;
                            else
                                return false;
                        }
                    case NoteCutDirection.Any:
                        {
                            return true;
                        }
                    default: // For any other cases
                        {
                            return false;
                        }
                }
            }
            else // left red notes left favors left and down
            {
                switch (n1.cutDirection)
                {
                    case NoteCutDirection.Up:
                        {
                            if (n2.cutDirection == NoteCutDirection.Down || n2.cutDirection == NoteCutDirection.DownLeft) // all these are missing the same strokes as above
                                return true;
                            else
                                return false;
                        }
                    case NoteCutDirection.Down:
                        {
                            if (n2.cutDirection == NoteCutDirection.Up || n2.cutDirection == NoteCutDirection.UpLeft)
                                return true;
                            else
                                return false;
                        }
                    case NoteCutDirection.Left:
                        {
                            if (n2.cutDirection == NoteCutDirection.Right || n2.cutDirection == NoteCutDirection.DownRight)
                                return true;
                            else
                                return false;
                        }
                    case NoteCutDirection.Right:
                        {
                            if (n2.cutDirection == NoteCutDirection.Left || n2.cutDirection == NoteCutDirection.DownLeft)
                                return true;
                            else
                                return false;
                        }
                    case NoteCutDirection.UpLeft:
                        {
                            if (n2.cutDirection == NoteCutDirection.DownRight)// || note2.cutDirection == NoteCutDirection.UpRight) // added 2/22
                                return true;
                            else
                                return false;
                        }
                    case NoteCutDirection.UpRight:
                        {
                            if (n2.cutDirection == NoteCutDirection.DownLeft)// || note2.cutDirection == NoteCutDirection.DownRight) // added 2/22
                                return true;
                            else
                                return false;
                        }
                    case NoteCutDirection.DownLeft:
                        {
                            if (n2.cutDirection == NoteCutDirection.UpRight)// || note2.cutDirection == NoteCutDirection.DownRight) // added 2/22
                                return true;
                            else
                                return false;
                        }
                    case NoteCutDirection.DownRight:
                        {
                            if (n2.cutDirection == NoteCutDirection.UpLeft)// || note2.cutDirection == NoteCutDirection.DownLeft) // added 2/22
                                return true;
                            else
                                return false;
                        }
                    case NoteCutDirection.Any:
                        {
                            return true;
                        }
                    default: // For any other cases
                        {
                            return false;
                        }
                }
            }
        }
        
        /// <summary>
        /// Caluculates angle of each input direction and checks if the difference in angle between the two directions is greater than or equal to the specified minimum swing separation in degrees. This allows for more flexibility in what counts as "contrary" directions, while still ensuring that they are sufficiently different to create a satisfying arc swing. For example, with a minSeparationDeg of 90, a note with an up cut direction could be paired with a note with a left cut direction, but not with another note with an upLeft cut direction.
        /// Suggested inputs are 180, 135, and 90.
        /// </summary>
        /// <param name="n1"></param>
        /// <param name="n2"></param>
        /// <param name="minSwingDeg"></param>
        /// <returns></returns>
        public static bool IsContraryByDegrees(ENoteData n1, ENoteData n2, int minSwingDeg)
        {
            if (n1.cutDirection == NoteCutDirection.Any || n2.cutDirection == NoteCutDirection.Any)
                return true; // always good

            Vector2 d1 = n1.cutDirection.Direction(); // returns vector from NoteCutDirectionExtensions decompiled code
            Vector2 d2 = n2.cutDirection.Direction();

            // Safety: if Any maps to (0,0) in your extension, avoid bogus angles
            if (d1 == Vector2.zero || d2 == Vector2.zero) // means has NO actual direction
                return false;

            float diff = Vector2.Angle(d1, d2); // 0..180

            return diff >= minSwingDeg; //diff >= minSwingDeg
        }
       
        public static bool IsOppositeBetweenSwings(ENoteData note1, NoteData note2) // may be the best option
        {
            if (note1.cutDirection == NoteCutDirection.Any || note2.cutDirection == NoteCutDirection.Any) return false;
            else if (note1.cutDirection.Opposite() == note2.cutDirection) // left vs right, upleft vs downright etc
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        //Chains Pause Detection--------USING THIS--------------------------------------------------------------------------
        public static List<ESliderData> SetPreferredChainCountPauseDetection(List<ENoteData> colorA, List<ENoteData> colorB)
        {
            if (colorA.Count < 3 && colorB.Count < 3) new List<ESliderData>();

            List<ENoteData> chainNotesA = new List<ENoteData>();
            List<ENoteData> chainNotesB = new List<ENoteData>();

            pauseThresholdMultiplier = 1.5f;// makes pause detection adaptive to the varying average speeds of different sections of the song

            if (colorA.Count > 1)
                chainNotesA = PauseDetection(colorA);
            if (colorB.Count > 1)
                chainNotesB = PauseDetection(colorB);

            int currentChainsCount = chainNotesA.Count + chainNotesB.Count;

            int counter = 1;
            int maxIterations = 10;

            Plugin.LogDebug($"[Arcitect] currentChainsCount: {currentChainsCount}, preferredChainCount: {preferredChainCount}");

            // Iteratively adjust pauseThresholdMultiplier to get closer to preferredChainCount
            while (Math.Abs(currentChainsCount - preferredChainCount) >= 5 && counter < maxIterations) // 5 is a threshold for acceptable difference
            {
                if (currentChainsCount > preferredChainCount)
                {
                    //Plugin.LogDebug($"--- Iteration: {counter} Chains Count (Too Many) : {currentChainsCount}, Threshold: {pauseThresholdMultiplier}");
                    pauseThresholdMultiplier *= 1.1f; // Increase multiplier if too many chains
                }
                else if (forceMoreChains && (currentChainsCount < preferredChainCount))//may decide not to force adding more chains
                {
                    //Plugin.LogDebug($"--- Iteration: {counter} Chains Count (Too Few) : {currentChainsCount}, Threshold: {pauseThresholdMultiplier}");
                    pauseThresholdMultiplier *= 0.9f; // Decrease multiplier if too few chains
                }

                alteredNotes.Clear();
                //doubleChainNotes.Clear();
                chainNotesA.Clear(); chainNotesB.Clear();

                if (colorA.Count > 1)
                    chainNotesA = PauseDetection(colorA);
                if (colorB.Count > 1)
                    chainNotesB = PauseDetection(colorB);

                const float EPS = 0.001f;

                // This new idea helps get more compatible double chains. since pauseDetection works per color, each may present a differnt list of notes based on pauses. thus double notes may not be added to chain notes in all cases.
                // But if one note in double note pair was detected as compatible, its likely the other note of opposite color may accept a chain too despite not meeting pause detection's constraints. 
                // But nevertheless, it may be nice to have a double chain at the moment. and anyway, if the other color is completely incompatible for a chain, it will not be added anyway.
                // A → B
                foreach (var a in chainNotesA.ToList())
                {
                    var partnerB = colorB.FirstOrDefault(b =>
                        Math.Abs(b.time - a.time) < EPS &&
                        IsCompatibleForChain(b));

                    if (partnerB != null && !chainNotesB.Contains(partnerB))
                        chainNotesB.Add(partnerB);
                }
                // Must do this 2x since each list may be completely different than the other.
                // B → A
                foreach (var b in chainNotesB.ToList())
                {
                    var partnerA = colorA.FirstOrDefault(a =>
                        Math.Abs(a.time - b.time) < EPS &&
                        IsCompatibleForChain(a));

                    if (partnerA != null && !chainNotesA.Contains(partnerA))
                        chainNotesA.Add(partnerA);
                }



                currentChainsCount = chainNotesA.Count + chainNotesB.Count;// + doubleChainNotes.Count;

                counter++;
            }

            //AlterNotes(); // didn't like using this since it alters notes and usually makes awkward moments

            //added this. did i need this? ---------------------------------------------
            List<ENoteData> chainNotes = new List<ENoteData>();
            chainNotes.AddRange(chainNotesA); chainNotes.AddRange(chainNotesB); // these can accidentally have chains at same time so appear to be double chains but haven't been vetted as double chains.

            chainNotes.Sort((note1, note2) => note1.time.CompareTo(note2.time));
            /*
            foreach (ENoteData note in chainNotes)
            {
                Plugin.LogDebug($"---- Chain Note: {note.time:F} - cutDirection: {note.cutDirection} line: {note.line} layer: {note.layer} color: {note.colorType} ");
            }
            */
            float firstChainTime = chainNotes.Count > 0 ? chainNotes.First().time : 0f;

            Plugin.LogDebug($"[Arcitect] Chains Count: {currentChainsCount} with pauseMultiplier: {pauseThresholdMultiplier} took {counter} iterations. -- pause detection -- LongChainMaxDuration: {Config.Instance.LongChainMaxDuration} First chain at: {firstChainTime:F}"); //Double Chains Count: { doubleChainNotes.Count}

            return CreateChains(chainNotes);
        }

        //Not Used
        public static List<ESliderData> SetPreferredChainCountTempoChange(List<ENoteData> colorA, List<ENoteData> colorB)
        {
            float threshold = 0.5f;// 2f; // Starting threshold value
            int maxIterations = 10; // Maximum number of iterations to avoid infinite loops
            int iterationCount = 1;

            List<ENoteData> ChainNotes = new List<ENoteData>();
            List<float> intervalsA = CalculateNoteIntervals(colorA);
            List<float> intervalsB = CalculateNoteIntervals(colorB);

            //List<float> intervalsAll = CalculateNoteIntervals(_notes); // Double chain test

            while (iterationCount < maxIterations)
            {
                alteredNotes.Clear();
                //doubleChainNotes.Clear();
                ChainNotes.Clear();
                if (colorA.Count > 1)
                    ChainNotes.AddRange(IdentifyTempoChanges(colorA, intervalsA, threshold, true)); // Double chain test - bool to say 1 color 
                if (colorB.Count > 1)
                    ChainNotes.AddRange(IdentifyTempoChanges(colorB, intervalsB, threshold, true)); // Double chain test - bool to say 1 color 

                //chains.AddRange(IdentifyTempoChanges(_notes, intervalsAll, threshold, false)); // Double chain test - bool to say 2 color 

                int chainsCount = ChainNotes.Count;// + doubleChainNotes.Count;

                if (Math.Abs(chainsCount - preferredChainCount) <= 5)
                {
                    break;// Close enough to preferred count
                }
                else if (chainsCount > preferredChainCount)
                {
                    Plugin.LogDebug($"[Arcitect] Chains Count (Too Many) : {chainsCount}, Threshold: {threshold}, Iteration# {iterationCount}");
                    threshold *= 1.2f;// Too many chains, increase threshold
                }
                else if (forceMoreChains && ChainNotes.Count < preferredChainCount)//user can decide if wnat to force more chains
                {
                    Plugin.LogDebug($"[Arcitect] Chains Count (Too Few): {chainsCount}, Threshold: {threshold}, Iteration# {iterationCount}");
                    threshold *= 0.8f;// Too few chains, decrease threshold
                }

                iterationCount++;
            }

            //AlterNotes();

            //chains.AddRange(doubleChainNotes);
            Plugin.LogDebug($"[Arcitect] Double Chain Notes:");
            //foreach (ENoteData doubleNote in doubleChainNotes)
            //{
            //    Plugin.LogDebug($"---- Double Chain: {doubleNote.time:F} - cutDirection: {doubleNote.cutDirection}");
            //}
            ChainNotes.Sort((note1, note2) => note1.time.CompareTo(note2.time));

            //MoveWallsBlockingChainTail();

            Plugin.LogDebug($"[Arcitect] Final Chains Count: {ChainNotes.Count}, Final Threshold: {threshold}, Iterations: {iterationCount} - Tempo Change Algorithm.");

            return CreateChains(ChainNotes);
        }

        //Using this
        public static List<ENoteData> PauseDetection(List<ENoteData> colorNotes)
        {
            List<ENoteData> groupBoundaries = new List<ENoteData>();

            if (colorNotes == null || colorNotes.Count < 5)
                return groupBoundaries;

            int frequencyWindowSize = 10; //This sets the size of the window for calculating the average frequency (or rate) of notes.
            int beginningNote = 3;
            bool justDetectedPause = false;  // Flag to indicate a pause was just detected. It's used to prevent the detection of consecutive pauses

            Queue<float> recentTimeDifferences = new Queue<float>(); //This queue holds the time differences between consecutive notes, limited by the frequencyWindowSize.
            float prevNoteTime = colorNotes[beginningNote - 1].time; //Stores the time of the note just before the beginningNote.

            // calculates the time difference between notes.
            for (int i = beginningNote; i < colorNotes.Count; i++)
            {
                float currentNoteTime = colorNotes[i].time;
                float timeDifference = currentNoteTime - prevNoteTime;

                if (recentTimeDifferences.Count == frequencyWindowSize)
                {
                    recentTimeDifferences.Dequeue();
                }
                recentTimeDifferences.Enqueue(timeDifference);

                if (timeDifference >= Config.Instance.ChainTimeBumper && (timeDifference > recentTimeDifferences.Average() * pauseThresholdMultiplier) && !justDetectedPause) // don't scale ChainTimeBumper
                {
                    if (recentTimeDifferences.Count > 1)
                    {

                        if (IsCompatibleForChain(colorNotes[i - 1]))
                        {
                            groupBoundaries.Add(colorNotes[i - 1]);
                        }

                        if (IsCompatibleForChain(colorNotes[i]))
                        {
                            groupBoundaries.Add(colorNotes[i]);
                        }


                        //Plugin.LogDebug($"Pause Between Notes: {timeDifference}----------------------------------------");

                        recentTimeDifferences.Clear();
                        recentTimeDifferences.Enqueue(timeDifference);
                        justDetectedPause = true; // Set the flag as pause detected
                    }
                }
                else
                {
                    justDetectedPause = false; // Reset the flag as we're processing normal notes
                    //Plugin.LogDebug($"Group Note: {notes[i].time:F}. Average Frequency of Notes in Group: {averageFrequency}");
                }

                prevNoteTime = currentNoteTime;
            }

            //ensures that you don't miss the potential for a chain at the very end of your note sequence. the last note of the song <list>notes
            if (recentTimeDifferences.Count > 1)
            {
                ENoteData lastNote = colorNotes[colorNotes.Count - 1];

                if (IsCompatibleForChain(lastNote))// if (IsCompatibleForChain(lastNote, colorNotes))
                {
                    groupBoundaries.Add(lastNote);
                }
                else
                {
                    //Plugin.LogDebug($"Incompatible Note for Chain: {lastNote.time:F}");
                }
            }
            //Plugin.LogDebug($"Chains {groupBoundaries[0].colorType} Count: {groupBoundaries.Count} with pauseMultipler: {pauseThresholdMultiplier}");

            return groupBoundaries;
        }

        public static List<ENoteData> IdentifyTempoChanges(List<ENoteData> notes, List<float> intervals, float threshold, bool findSingleChains)
        {
            List<ENoteData> tempoChangePoint = new List<ENoteData>();

            for (int i = 1; i < intervals.Count; i++)
            {
                float changeMagnitude = Math.Abs(intervals[i] - intervals[i - 1]);
                if (changeMagnitude > threshold)
                {
                    //Plugin.LogDebug($"Tempo Change Point: {notes[i].time:F} - Magnitude of change: {changeMagnitude}");

                    if (findSingleChains && IsCompatibleForChain(notes[i])) // if (IsCompatibleForChain(notes[i], notes)
                    {
                        tempoChangePoint.Add((notes[i]));

                        //Plugin.LogDebug($"Compatible tempo change point so CHAIN added.");
                    }
                }
            }
            //Plugin.LogDebug($"Chain Count: {tempoChangePoint.Count} - Tempo Change Algorithm");

            return tempoChangePoint;
        }

        private static int _lastCheckedIndex = 0;


        // Final step for both style options for making chains
        private static List<ESliderData> CreateChains(List<ENoteData> chainNotes)
        {
            var chains = new List<ESliderData>();
            int longChainCount = 1;

            // Reset the lastCheckedIndex at the beginning of the processing
            _lastCheckedIndex = 0;

            // Final movement parameters (seconds-based system in your mod)
            float njs = MathF.Max(1f, TransitionPatcher.FinalNoteJumpMovementSpeed);
            float jd = MathF.Max(1f, TransitionPatcher.FinalJumpDistance);

            // Jump duration approximation (seconds)
            // (Conceptually: jumpDistance ≈ njs * jumpDuration)
            float jumpDuration = jd / njs;

            // -----------------------------
            // SHORT / MEDIUM: fixed seconds
            // -----------------------------
            // Tune these by feel. These should remain stable across NJS/JD.
            const float SHORT_CHAIN_DUR = 0.01f; // 4 slices
            float MED_CHAIN_DUR = 0.05f; // 5–6 slices;

            // -----------------------------
            // LONG: derived but capped
            // -----------------------------
            // Your user-config max (seconds)
            float userMaxLong = MathF.Max(0.0001f, Config.Instance.LongChainMaxDuration);

            // Cap long chains relative to jump duration to prevent extremes:
            // - If someone sets JD high and/or NJS high, jumpDuration can shrink/grow and long chains should not become absurd.
            // Tune these bounds and fraction to taste.
            float maxLongByJump = Math.Clamp(0.70f * jumpDuration, 0.12f, 0.90f);
            float maxLongChainAllowed = Math.Min(userMaxLong, maxLongByJump);

            // Gating values for “is there enough room to place a long chain?”
            // These are best expressed as fractions of jumpDuration (not NJS alone) when JD is user-controlled.
            float minGapAfterLongChain = Math.Clamp(0.35f * jumpDuration, 0.10f, 0.60f);
            float minLongChainAllowed = Math.Clamp(0.12f * jumpDuration, 0.04f, 0.30f);

            // Long chain chance evaluated per note
            int longChainChanceInt = Mathf.Clamp((int)Config.Instance.LongChainChanceMultiplier, 0, 10); 

            // Optional: cap how close any chain tail can get to the next note (same color or all notes).
            // If you want no extra logic, set this to 0.
            const float MIN_TAIL_CLEARANCE = .05f;

            // Helper: clamp tailTime so it doesn't bump into next note if you want.
            float ClampTailToNextNote(ENoteData chainNote, float desiredTailTime)
            {
                if (MIN_TAIL_CLEARANCE <= 0f)
                    return desiredTailTime;

                float timeUntilNext = GetTimeUntilNextNote(notesAndBombs, chainNote);
                if (timeUntilNext <= 0f)
                    return desiredTailTime;

                float latestTail = chainNote.time + Math.Max(0.0001f, timeUntilNext - MIN_TAIL_CLEARANCE);
                return Math.Min(desiredTailTime, latestTail);
            }

            // Helper: epsilon compare for floats
            static bool NearlyEqual(float a, float b, float eps = 1e-5f) => MathF.Abs(a - b) < eps;

            foreach (ENoteData chainNote in chainNotes)
            {
                // -----------------------------
                // Default: SHORT chain
                // -----------------------------
                int sliceCount = 4;
                float tailTime = chainNote.time + SHORT_CHAIN_DUR;

                // Tail geometry from your existing logic
                (int headLineLayer, int headLineIndex, int tailLineLayer, int tailLineIndex) = CalculateTailPosition(chainNote);
                NoteLineLayer tailBeforeJumpLineLayer = (NoteLineLayer)tailLineLayer;

                // -----------------------------
                // MEDIUM chain heuristic
                // -----------------------------
                int dx = Math.Abs(tailLineIndex - chainNote.line);
                int dy = Math.Abs(tailLineLayer - (int)chainNote.layer);

                // Your existing intent: if span is “large”, use 5–6 slices and a bit longer duration.
                if (dx == 2 || dy == 2)
                {
                    // “Knight-ish” diagonal tends to feel bunched; keep at 5.
                    //if ((dx == 2 && dy == 1) || (dx == 1 && dy == 2))
                    //    sliceCount = 5;
                    //else
                        sliceCount = 6;

                    tailTime = chainNote.time + MED_CHAIN_DUR;
                }

                // safety to avoid hitting next note
                tailTime = ClampTailToNextNote(chainNote, tailTime);

                // -----------------------------
                // Accidental double-chain handling (your existing logic)
                // -----------------------------
                bool shouldAddChain = true;

                ESliderData existingChainAtSameTime = chains.FirstOrDefault(a => Math.Abs(a.time - chainNote.time) < 0.01f);

                if (existingChainAtSameTime != null)
                {
                    //Plugin.LogDebug($"[Arcitect][CreateChains] ChainNote: {chainNote.time:F} {chainNote.cutDirection} x: {chainNote.line} y: {chainNote.layer} -- Found existingChainAtSameTime {existingChainAtSameTime.cutDirection} x: {existingChainAtSameTime.line} y: {existingChainAtSameTime.layer} dur: {(existingChainAtSameTime.tailTime - existingChainAtSameTime.time):F}");

                    if (TryCreateDoubleChain(chainNote, existingChainAtSameTime, MED_CHAIN_DUR, out var matchingChain))
                    {
                        // For long double chains, prevent “X” crossings
                        UncrossLongDoubleChainsIfNeeded(existingChainAtSameTime, matchingChain, MED_CHAIN_DUR);

                        if (!chains.Contains(matchingChain))
                        {
                            chains.Add(matchingChain);
                            chainsTemp.Add(matchingChain);
                        }

                        shouldAddChain = false; // double chain handled, skip creating new one
                    }
                    else
                    {
                        shouldAddChain = false;
                    }
                }

                if (!shouldAddChain)
                    continue;

                // -----------------------------
                // LONG chain attempt (optional)
                // -----------------------------
                bool longChainChance = TransitionPatcher.RepeatableRandom.Next(10) < longChainChanceInt; // is set to 10 so will always happen, 5 is 50%, 1 is 10%

                if (Config.Instance.EnableLongChains &&
                    longChainChance &&
                    chainNotes.IndexOf(chainNote) < chainNotes.Count - 1)
                {
                    // Your edge/direction eligibility gate (kept)
                    bool eligibleForLong =
                        (chainNote.layer < 2 && // test was (chainNote.layer == 0) so add extra layer
                         (chainNote.cutDirection == NoteCutDirection.Up ||
                          chainNote.cutDirection == NoteCutDirection.UpLeft ||
                          chainNote.cutDirection == NoteCutDirection.UpRight))
                        ||
                        (chainNote.layer == 2 && chainNote.cutDirection == NoteCutDirection.Down)
                        ||
                        ((chainNote.layer > 0) && // was (chainNote.layer == 1 || chainNote.layer == 2) should be the same exactly
                         (chainNote.cutDirection == NoteCutDirection.DownLeft ||
                          chainNote.cutDirection == NoteCutDirection.DownRight))
                        ||
                        (chainNote.line > 1 && // was (chainNote.line == 3) so add extra line
                         (chainNote.cutDirection == NoteCutDirection.Left ||
                          chainNote.cutDirection == NoteCutDirection.DownLeft ||
                          chainNote.cutDirection == NoteCutDirection.UpLeft))
                        ||
                        (chainNote.line < 2 && // was (chainNote.line == 0) so add extra line
                         (chainNote.cutDirection == NoteCutDirection.Right ||
                          chainNote.cutDirection == NoteCutDirection.DownRight ||
                          chainNote.cutDirection == NoteCutDirection.UpRight));

                    if (eligibleForLong)
                    {
                        float timeUntilNextNote = GetTimeUntilNextNote(notesAndBombs, chainNote);

                        if (timeUntilNextNote > (minGapAfterLongChain + minLongChainAllowed))
                        {
                            float longChainDuration = Math.Min(timeUntilNextNote - minGapAfterLongChain, maxLongChainAllowed);

                            bool awkwardLongChain = false;
                            bool limitMaxDuration = false;

                            // --- Tail positioning rules ---
                            // Your current code forces tailLineLayer = 0 for any Down/DownLeft/DownRight regardless of head layer.
                            // If you want original behaviour, gate it on head layer == 2:
                            if ((chainNote.cutDirection == NoteCutDirection.Up ||
                                 chainNote.cutDirection == NoteCutDirection.UpLeft ||
                                 chainNote.cutDirection == NoteCutDirection.UpRight))// && removed these
                                ///chainNote.layer == 0)
                            {
                                tailLineLayer = 2;
                            }
                            else if ((chainNote.cutDirection == NoteCutDirection.Down ||
                                      chainNote.cutDirection == NoteCutDirection.DownLeft ||
                                      chainNote.cutDirection == NoteCutDirection.DownRight))// && removed these
                                                                                            //chainNote.layer == 2)
                            {
                                tailLineLayer = 0;
                            }

                            if ((chainNote.cutDirection == NoteCutDirection.Right ||
                                 chainNote.cutDirection == NoteCutDirection.DownRight ||
                                 chainNote.cutDirection == NoteCutDirection.UpRight)) //&& removed these
                                                                                      //chainNote.line == 0)
                            {
                                tailLineIndex = 3;
                            }
                            else if ((chainNote.cutDirection == NoteCutDirection.Left ||
                                      chainNote.cutDirection == NoteCutDirection.DownLeft ||
                                      chainNote.cutDirection == NoteCutDirection.UpLeft))// && removed these
                                                                                         //chainNote.line == 3)
                            {
                                tailLineIndex = 0;
                            }

                            // --- Awkwardness + special cases (kept with minimal edits) ---
                            if (chainNote.cutDirection == NoteCutDirection.Down)
                            {
                                limitMaxDuration = true;
                                tailLineIndex = chainNote.line;

                                if ((chainNote.colorType == ColorType.ColorA && chainNote.line > 1) ||
                                    (chainNote.colorType == ColorType.ColorB && chainNote.line < 2))
                                {
                                    awkwardLongChain = true;
                                }
                            }
                            else if (chainNote.cutDirection == NoteCutDirection.Up && chainNote.layer == 0) // tested layer 1 is awkward for long up chain
                            {
                                tailLineIndex = chainNote.line;

                                if ((chainNote.colorType == ColorType.ColorA && chainNote.line > 1) ||
                                    (chainNote.colorType == ColorType.ColorB && chainNote.line < 2))
                                {
                                    awkwardLongChain = true;
                                }
                            }
                            else if (chainNote.cutDirection == NoteCutDirection.Left || chainNote.cutDirection == NoteCutDirection.Right)
                            {
                                tailLineLayer = (int)chainNote.layer;
                                //limitMaxDuration = true;
                            }
                            else if (chainNote.cutDirection == NoteCutDirection.UpRight &&
                                     chainNote.line < 2 && chainNote.layer < 2) // was chainNote.line == 0 && chainNote.layer == 0)
                            {
                                tailLineIndex = 3;
                                tailLineLayer = 2;
                                sliceCount = 8;
                                //limitMaxDuration = true;
                            }
                            else if (chainNote.cutDirection == NoteCutDirection.UpLeft &&
                                     chainNote.line > 1 && chainNote.layer < 2) // was wchainNote.line == 3 && chainNote.layer == 0
                            {
                                tailLineIndex = 0;
                                tailLineLayer = 2;
                                sliceCount = 8;
                                //limitMaxDuration = true;
                            }
                            else if (chainNote.cutDirection == NoteCutDirection.DownRight &&
                                     chainNote.line < 2 && chainNote.layer > 0) // was chainNote.line == 0 && chainNote.layer == 2
                            {
                                tailLineIndex = 3;
                                tailLineLayer = 0;
                                sliceCount = 8;
                                limitMaxDuration = true;
                            }
                            else if (chainNote.cutDirection == NoteCutDirection.DownLeft &&
                                     chainNote.line > 1 && chainNote.layer > 0) // was chainNote.line == 3 && chainNote.layer == 2
                            {
                                tailLineIndex = 0;
                                tailLineLayer = 0;
                                sliceCount = 8;
                                limitMaxDuration = true;
                            }
                            else
                            {
                                awkwardLongChain = true;
                            }

                            if (!awkwardLongChain)
                            {
                                // If we are hitting the max cap, trim a little (in seconds).
                                // Use epsilon comparison instead of ==.
                                if (limitMaxDuration && NearlyEqual(longChainDuration, maxLongChainAllowed))
                                {
                                    longChainDuration = Math.Max(0.0001f, longChainDuration - 0.030f);
                                }

                                // Special handling for Down: cap it so it stays hittable.
                                if (chainNote.cutDirection == NoteCutDirection.Down)
                                {
                                    // Cap down long chains relative to jumpDuration (or a fixed ceiling).
                                    float downCap = Math.Clamp(0.20f * jumpDuration, 0.05f, 0.10f);
                                    longChainDuration = Math.Min(longChainDuration, downCap);
                                }

                                longChainDuration = Math.Max(longChainDuration, 0.0001f);
                                tailTime = chainNote.time + longChainDuration;

                                // LONG CHAIN sliceCount:

                                //int tx = Math.Abs(tailLineIndex - chainNote.line);
                                //int ty = Math.Abs(tailLineLayer - (int)chainNote.layer);
                                float dist = MathF.Sqrt(dx * dx + dy * dy);

                                // Base min for any long chain
                                int baselineMin = 8;

                                // Distance-based base density
                                const float SLICES_PER_GRID_UNIT = 4.5f; // tune this to taste higher adds more slices. 4.5 is 12-15% more. 5.0 is 25% more.
                                int baseSlicesByDist = (int)MathF.Ceiling(dist * SLICES_PER_GRID_UNIT);

                                // NJS-based factor: linear, stronger than before
                                const float REF_NJS = 16f;
                                //float njs = MathF.Max(1f, TransitionPatcher.FinalNoteJumpMovementSpeed);

                                // Raw ratio: 5/16 ≈ 0.31, 18/16 ≈ 1.125, 31/16 ≈ 1.94
                                float rawFactor = njs / REF_NJS;

                                // Clamp so low NJS doesn’t kill slices, high NJS doesn’t explode them
                                float speedFactor = Math.Clamp(rawFactor, 0.5f, 2.5f);

                                // Apply speed factor
                                int desiredByDistAndSpeed = (int)MathF.Ceiling(baseSlicesByDist * speedFactor);

                                // Time-based ceiling so slices aren't impossibly fast
                                const float MIN_SECONDS_PER_SLICE = 0.018f;
                                int maxSlicesByTime = (int)MathF.Floor(longChainDuration / MIN_SECONDS_PER_SLICE);

                                // Combine
                                int desired = Math.Max(baselineMin, desiredByDistAndSpeed);
                                int ceiling = Math.Max(baselineMin, maxSlicesByTime);

                                sliceCount = Math.Clamp(desired, baselineMin, ceiling);


                                // Optional: don’t let long chains bump next note
                                tailTime = ClampTailToNextNote(chainNote, tailTime);
                                
                                Plugin.LogDebug(
                                    $"[Arcitect][CreateChains] Long Chain {longChainCount}: {chainNote.time:F} {chainNote.colorType} {chainNote.cutDirection} " +
                                    $"x: {chainNote.line} xTail: {tailLineIndex} - y: {(int)chainNote.layer} yTail: {tailLineLayer} " +
                                    $"Dur: {longChainDuration:F3} slices: {sliceCount} (jumpDur={jumpDuration:F3}, maxLong={maxLongChainAllowed:F3})"
                                );
                                
                                longChainCount++;
                            }
                        }
                    }
                }

                // Log final choice for this chain
                /*Plugin.LogDebug(
                    $"Chain: {chainNote.time:F} {chainNote.colorType} dir: {chainNote.cutDirection} " +
                    $"H index: {chainNote.line} T index: {tailLineIndex} - H layer: {(int)chainNote.layer} T layer: {tailLineLayer} " +
                    $"Dur: {(tailTime - chainNote.time):F3} slices: {sliceCount}"
                );*/

                var newChain = ESliderData.CreateChain(
                    chainNote.colorType,
                    chainNote.time, headLineIndex, headLineLayer, chainNote.cutDirection,
                    tailTime, tailLineIndex, tailLineLayer,
                    sliceCount, squishAmount,
                    chainNote);

                if (chains.Contains(newChain))
                    continue;

                chains.Add(newChain);

                chainNote.headNoteChain = newChain;
                chainNote.scoringType = NoteData.ScoringType.ChainHead;
                chainNote.gameplayType = NoteData.GameplayType.BurstSliderHead;

                chainsTemp.Add(newChain);
            }

            return chains;
        }

        private static void UncrossLongDoubleChainsIfNeeded(ESliderData chainA, ESliderData chainB, float MED_CHAIN_DUR)
        {
            //Plugin.LogDebug("[UncrossLongDoubleChainsIfNeeded] Called...");
            // 1) Only touch long chains – leave short/medium double chains alone
            float durA = chainA.tailTime - chainA.time;
            float durB = chainB.tailTime - chainB.time;

            MED_CHAIN_DUR += .01f; // therefore a long chain

            if (durA < MED_CHAIN_DUR || durB < MED_CHAIN_DUR)
                return;
            //Plugin.LogDebug("[UncrossLongDoubleChainsIfNeeded] 2");
            // 2) Only consider diagonals (avoid messing with pure vertical/horizontal double chains)
            if (!IsDiagonal(chainA) || !IsDiagonal(chainB))
                return;
            //Plugin.LogDebug("[UncrossLongDoubleChainsIfNeeded] 3");
            // 3) Extract endpoints (grid coordinates)
            int h1x = chainA.line;
            int h1y = chainA.layer;
            int t1x = chainA.tailLine;
            int t1y = chainA.tailLayer;

            int h2x = chainB.line;
            int h2y = chainB.layer;
            int t2x = chainB.tailLine;
            int t2y = chainB.tailLayer;

            // 4) Do these two segments actually cross (an “X”)?
            if (!SegmentsProperlyIntersect(h1x, h1y, t1x, t1y,
                                           h2x, h2y, t2x, t2y))
                return;
            //Plugin.LogDebug("[UncrossLongDoubleChainsIfNeeded] 4");
            // 5) Decide whether to “uncross horizontally” or “uncross vertically”
            int minX = Math.Min(Math.Min(h1x, t1x), Math.Min(h2x, t2x));
            int maxX = Math.Max(Math.Max(h1x, t1x), Math.Max(h2x, t2x));
            int minY = Math.Min(Math.Min(h1y, t1y), Math.Min(h2y, t2y));
            int maxY = Math.Max(Math.Max(h1y, t1y), Math.Max(h2y, t2y));

            int spanX = maxX - minX; // 0..3
            int spanY = maxY - minY; // 0..2

            // 6) For the axis we adjust, we keep heads as-is and move tails inward.
            //    Pick which chain is “first” along that axis to assign the lower/inner slot.

            if (spanX >= spanY && spanX >= 2)
            {
                // X spread dominates → keep layers, adjust lineIndex to 1 & 2
                ESliderData leftChain = (h1x <= h2x) ? chainA : chainB;
                ESliderData rightChain = (leftChain == chainA) ? chainB : chainA;

                // Both tails get the same layer they already use (no vertical change)
                int sharedTailLayer = leftChain.tailLayer; // they are diagonals so y-diff != 0, but we do not care which

                leftChain.tailLine = 1;
                rightChain.tailLine = 2;

                leftChain.tailLayer = sharedTailLayer;
                rightChain.tailLayer = sharedTailLayer;

                Plugin.LogDebug(
                    $"[Arcitect][UncrossLongDoubleChainsIfNeeded] Uncrossed by lineIndex at t={chainA.time:F3}: " +
                    $"L tail=({leftChain.tailLine},{leftChain.tailLayer}) " +
                    $"R tail=({rightChain.tailLine},{rightChain.tailLayer})");
            }
            else if (spanY > spanX && spanY >= 1)
            {
                // Y spread dominates → keep lineIndex, adjust layer to (0,1) or (1,2)
                ESliderData lowChain = (h1y <= h2y) ? chainA : chainB;
                ESliderData highChain = (lowChain == chainA) ? chainB : chainA;

                int newLowLayer, newHighLayer;

                // If they are mostly in lower half, use 0&1; otherwise 1&2
                if (minY == 0)
                {
                    newLowLayer = 0;
                    newHighLayer = 1;
                }
                else
                {
                    newLowLayer = 1;
                    newHighLayer = 2;
                }

                lowChain.tailLayer = newLowLayer;
                highChain.tailLayer = newHighLayer;

                // Keep their lineIndices so one stays “left”, one “right”
                // (if you want to pull both inward horizontally too, you can adjust lineIndex here as well)

                Plugin.LogDebug(
                    $"[Arcitect][UncrossLongDoubleChainsIfNeeded] Uncrossed by layer at t={chainA.time:F3}: " +
                    $"Low tail=({lowChain.tailLine},{lowChain.tailLayer}) " +
                    $"High tail=({highChain.tailLine},{highChain.tailLayer})");
            }
        }

        private static bool IsDiagonal(ESliderData chain)
        {
            int dx = chain.tailLine - chain.line;
            int dy = chain.tailLayer - chain.layer;
            return dx != 0 && dy != 0;
        }
        private static int Orient(int ax, int ay, int bx, int by, int cx, int cy)
        {
            int v = (bx - ax) * (cy - ay) - (by - ay) * (cx - ax);
            if (v == 0) return 0;
            return (v > 0) ? 1 : -1;
        }

        private static bool OnSegment(int ax, int ay, int bx, int by, int px, int py)
        {
            return Math.Min(ax, bx) <= px && px <= Math.Max(ax, bx) &&
                   Math.Min(ay, by) <= py && py <= Math.Max(ay, by) &&
                   Orient(ax, ay, bx, by, px, py) == 0;
        }

        private static bool SegmentsProperlyIntersect(
            int x1, int y1, int x2, int y2,
            int x3, int y3, int x4, int y4)
        {
            int o1 = Orient(x1, y1, x2, y2, x3, y3);
            int o2 = Orient(x1, y1, x2, y2, x4, y4);
            int o3 = Orient(x3, y3, x4, y4, x1, y1);
            int o4 = Orient(x3, y3, x4, y4, x2, y2);

            // Proper intersection: each segment straddles the other.
            if (o1 != o2 && o3 != o4)
                return true;

            // Optional: treat collinear overlapping as non-“X” (we don’t need to modify those)
            // If you *do* want to treat collinear overlaps as "bad", add OnSegment checks here.

            return false;
        }
        private static float GetTimeUntilNextNote(List<ENoteData> notesAndBombs, ENoteData chainNote)
        {
            float currentTime = chainNote.time;

            for (int i = _lastCheckedIndex; i < notesAndBombs.Count; i++)
            {
                ENoteData nb = notesAndBombs[i];

                // Skip anything at or before currentTime
                if (nb.time <= currentTime)
                    continue;

                bool isBomb = nb.cutDirection == NoteCutDirection.None || nb.colorType == ColorType.None;

                if (!isBomb)
                {
                    // This is a normal note: always blocks the chain.
                    _lastCheckedIndex = i;
                    return nb.time - currentTime;
                }

                // Bomb: only count it if it can actually block this chain
                if (BombBlocksChain(nb, chainNote))
                {
                    _lastCheckedIndex = i;
                    return nb.time - currentTime;
                }

                // Otherwise, ignore this bomb and keep scanning
            }

            return -1f; // no future note or blocking bomb
        }

        private static bool BombBlocksChain(ENoteData bomb, ENoteData chainNote)
        {
            Plugin.LogDebug($"[Arcitect][BombBlocksChain] Checking chainNote {chainNote.time:F} x:{chainNote.line} y:{chainNote.layer} -- bomb {bomb.time:F} x:{bomb.line} y:{bomb.layer}");

            int dx = bomb.line - chainNote.line;
            int dy = (int)bomb.layer - (int)chainNote.layer;

            // Bomb at or behind the head in both axes is never blocking.
            if (dx == 0 && dy == 0)
                return false;

            switch (chainNote.cutDirection)
            {
                case NoteCutDirection.Left:
                    // Must be strictly to the left, and roughly in the same row
                    if (dx >= 0) return false; else return true;

                case NoteCutDirection.Right:
                    if (dx <= 0) return false; else return true;

                case NoteCutDirection.Up:
                    if (dy <= 0) return false; else return true;

                case NoteCutDirection.Down:
                    if (dy >= 0) return false; else return true;

                case NoteCutDirection.UpLeft:
                    // Must be up AND left of the head
                    if (dx >= 0 || dy <= 0) return false; else return true;

                case NoteCutDirection.UpRight:
                    if (dx <= 0 || dy <= 0) return false; else return true;

                case NoteCutDirection.DownLeft:
                    if (dx >= 0 || dy >= 0) return false; else return true;

                case NoteCutDirection.DownRight:
                    if (dx <= 0 || dy >= 0) return false; else return true;

                default:
                    return false;
            }
        }



        // look for double chains or 2 chains that can be hit at the same time. i limited it to both notes up or both down or both left or both right
        private static bool IsCompatibleForChain(ENoteData note)
        {
            if (note.cutDirection == NoteCutDirection.None || note.cutDirection == NoteCutDirection.Any || note.gameplayType == GameplayType.Bomb)
            {
                return false;
            }

            int index = notes.IndexOf(note);
            float timeThreshold = Config.Instance.ChainTimeBumper; // Assuming this is defined elsewhere

            ENoteData matchingNoteInOtherColor = null;
            int matchingNotesCount = 0; // Counter for matching notes

            ENoteData potentialChainCollisonNote = null;

            // Start from the possible first note within the time threshold before the current note
            int startIndex = Math.Max(index - 1, 0); // Ensure we don't go out of bounds
            while (startIndex > 0 && note.time - notes[startIndex].time <= timeThreshold) startIndex--;

            // End at the possible last note within the time threshold after the current note
            int endIndex = Math.Min(index + 1, notes.Count - 1);
            while (endIndex < notes.Count - 1 && notes[endIndex].time - note.time <= timeThreshold) endIndex++;


            //Plugin.LogDebug($"Start Index: {startIndex} Index: {index} End Index: {endIndex}");
            // Loop over nearby notes to detect potential conflicts
            for (int i = startIndex; i <= endIndex; i++)
            {
                if (i == index) // Skip the current note
                {
                    continue;
                }

                float timeDifference = Math.Abs(notes[i].time - note.time);

                // Check for notes too close before or after, excluding those at the exact same time for opposite color matching
                if (timeDifference <= timeThreshold && timeDifference > 0)
                {
                    return false;
                }
                // check if there are multiple notes of the same color at the same time.
                if (timeDifference == 0 && notes[i].colorType == note.colorType)
                {
                    return false;
                }

                // Check for a note of the opposite color at the same time
                if (notes[i].time == note.time && notes[i].colorType != note.colorType)
                {
                    if (IsCompatibleForDoubleChain(note, notes[i])) // not sure why i have this here. doesn't seem to matter if there is a double chain possibilty here. but i does need to check if the note will cause a problem with the chain
                    {
                        matchingNoteInOtherColor = notes[i];
                        //Plugin.LogDebug($"Potential Double chain note found: Note {note.time} {note.colorType} index: {note.lineIndex} layer: {(int)note.layer} dir: {note.cutDirection} - other color: {_notes[i].time} index: {_notes[i].lineIndex} layer: {(int)_notes[i].layer} dir: {_notes[i].cutDirection}");

                    }
                    else
                    {
                        potentialChainCollisonNote = notes[i];
                        //Plugin.LogDebug($"Potential Collision note in opposite color note found: Note {note.time} {note.colorType} index: {note.lineIndex} layer: {(int)note.layer} dir: {note.cutDirection} - other color: {_notes[i].time} index: {_notes[i].lineIndex} layer: {(int)_notes[i].layer} dir: {_notes[i].cutDirection}");
                    }

                    matchingNotesCount++;
                }

            }

            // Ensure only one matching note is found
            if (matchingNotesCount > 1) //(foundMatchingNoteInOtherColor == null || matchingNotesCount != 1)
            {
                //Plugin.LogDebug($"TOO MANY matching notes here -- count: {matchingNotesCount} - {note.time:F}");// color: {note.colorType} other color: {matchingNoteInOtherColor.colorType} note index: {note.lineIndex} other index: {matchingNoteInOtherColor.lineIndex} note layer: {note.layer} other layer: {matchingNoteInOtherColor.layer} cut dir: {note.cutDirection} other cut dir: {matchingNoteInOtherColor.cutDirection}");

                return false; // Exit if no matching note found or more than one matching note exists
            }
            //else
            //Plugin.LogDebug($"matching Notes count: {matchingNotesCount}");


            if (Config.Instance.DisallowChainsOnCrossedPairs && matchingNoteInOtherColor != null)
            {
                if (!naturalPairNotes(note, matchingNoteInOtherColor) && !sideBySideVerticalPair(note, matchingNoteInOtherColor))
                    return false; // ColorA is effectively to the right of ColorB → do not make a chain on this note with the exception of side-by-side vertical pairs
            }

            foreach (var arc in arcs)
            {
                if (arc.time == note.time || (matchingNoteInOtherColor != null && arc.time == matchingNoteInOtherColor.time))
                {
                    //Plugin.LogDebug($"Arc Head blocking chain at: {note.time:F} color: {note.colorType} ");
                    return false; // Chain at the head of an arc is not compatible
                }
            }


            // Calculate the tail position and check grid boundaries
            (int headLineLayer, int headLineIndex, int tailLineLayer, int tailLineIndex) = CalculateTailPosition(note);
            bool isTailLineIndexValid = tailLineIndex >= 0 && tailLineIndex <= 3;
            bool isTailLineLayerValid = tailLineLayer >= 0 && tailLineLayer <= 2;

            // COLLISION CHECK: Verify that no interfering note is on the path from head to tail.
            // directly check if its position interferes with the chain’s path.
            if (potentialChainCollisonNote != null)
            {
                if (IsPointOnLineSegment(
                        potentialChainCollisonNote.line, (int)potentialChainCollisonNote.layer,
                        headLineIndex, headLineLayer,
                        tailLineIndex, tailLineLayer))
                {
                    //Plugin.LogDebug($"Chain collision: interfering note found at time {potentialChainCollisonNote.time} at position ({potentialChainCollisonNote.line}, {(int)potentialChainCollisonNote.layer}) blocking chain from {note.time}");
                    return false;
                }
            }
            bool naturalPairNotes(ENoteData note1, ENoteData note2)
            {
                if (note1.line == note2.line) // Same column → vertical stack; this is always “natural” regardless of height.
                    return true;
                if (note1.colorType == note2.colorType) // Same color: the setting is only about A/B pairs; do not block chains for AA/BB
                    return true;

                // Order them so leftNote is the one with the smaller line index
                ENoteData leftNote = note1.line <= note2.line ? note1 : note2;

                // Natural pairing = red on the left, blue on the right.
                // Any other ordering means ColorA is effectively “to the right of” ColorB.
                return leftNote.colorType == ColorType.ColorA;
            }

            bool sideBySideVerticalPair(ENoteData note1, ENoteData note2) //exception for side-by-side vertical pairs
            {
                if (Math.Abs(note1.line - note2.line) == 1 &&
                    (note1.cutDirection == NoteCutDirection.Up && note2.cutDirection == NoteCutDirection.Up ||
                    note1.cutDirection == NoteCutDirection.Down && note2.cutDirection == NoteCutDirection.Down))
                    return true;

                return false;
            }

            return isTailLineIndexValid && isTailLineLayerValid; // Return true if all conditions are met
        }
        // Helper method to determine if note is intersecting a chain head and tail. if a point (x, y) is on the line segment between (x1, y1) and (x2, y2)
        private static bool IsPointOnLineSegment(int x, int y, int x1, int y1, int x2, int y2)
        {
            // Check collinearity using cross product (avoid division for integer math)
            int dx = x2 - x1;
            int dy = y2 - y1;
            int dxp = x - x1;
            int dyp = y - y1;

            // The point is collinear if the cross product is zero
            if (dx * dyp != dy * dxp)
                return false;

            // Check if the point (x, y) is within the bounding box defined by (x1, y1) and (x2, y2)
            if (x < Math.Min(x1, x2) || x > Math.Max(x1, x2))
                return false;
            if (y < Math.Min(y1, y2) || y > Math.Max(y1, y2))
                return false;

            return true;
        }

        // attempt diagonal double chains
        private static bool TryCreateDoubleChain(ENoteData note, ESliderData existingChain, float MED_CHAIN_DUR, out ESliderData matchingChain)
        {
            //Plugin.LogDebug($"[Arcitect][TryCreateDoubleChain] Called...");

            int matchingTailIndex = -1;
            int matchingTailLayer = -1;

            float dur = existingChain.tailTime - existingChain.time;
            bool isLongChain = dur > MED_CHAIN_DUR + .01f;

            bool bothUp = (note.cutDirection == NoteCutDirection.Up && existingChain.cutDirection == NoteCutDirection.Up);
            bool bothDown = (note.cutDirection == NoteCutDirection.Down && existingChain.cutDirection == NoteCutDirection.Down);
            bool bothVertical = bothUp || bothDown;

            if (isLongChain)
            {
                bool pairOnNaturalSides = naturalPair(note, existingChain); // left note (type 0) is left of right note (type 1)

                bool safeDoubleLongChain = true;

                //Plugin.LogDebug($"[Arcitect][TryCreateDoubleChain] -- Long chain: pairOnNaturalSides: {pairOnNaturalSides}");

                if (!pairOnNaturalSides)
                {
                    bool adjacentColumns = Math.Abs(note.line - existingChain.line) == 1;
                    bool sameLayer = note.layer == existingChain.layer;
                    if (bothVertical && adjacentColumns && sameLayer) safeDoubleLongChain = true; else safeDoubleLongChain = false;

                    //Plugin.LogDebug($"[Arcitect][TryCreateDoubleChain] ---- Long chain: bothVertical && adjacentColumns && sameLayer: {safeDoubleLongChain}");
                }

                if (!safeDoubleLongChain) // wrong side notes must be up or down only.
                {
                    //Plugin.LogDebug($"[Arcitect][TryCreateDoubleChain] ---- Long Chain failed to produce double chain.");
                    matchingChain = null;
                    return false;
                }
            }

            // Check if note and existingChain head are compatible.
            // For vertical and horizontal, we already have rules; now we add diagonal.
            if (
                   // Vertical: Up/Down must match; different column but same layer.
                   (
                      bothVertical &&
                      note.line != existingChain.line &&
                      note.layer == existingChain.layer
                   )
                ||
                   // Horizontal: Left/Right must match; different layer but same column.
                   (
                      (note.cutDirection == NoteCutDirection.Left || note.cutDirection == NoteCutDirection.Right) &&
                      (existingChain.cutDirection == NoteCutDirection.Left || existingChain.cutDirection == NoteCutDirection.Right) &&
                      note.layer != existingChain.layer &&
                      note.line == existingChain.line
                   )
                ||
                   // Diagonals: Both must be diagonal and in a valid pairing (only parallel chains are allowed)
                   (
                        IsDiagonal(note.cutDirection) &&
                        IsDiagonal(existingChain.cutDirection) &&
                        IsValidDiagonalPair(note.cutDirection, existingChain.cutDirection) &&
                        IsValidPositionalPair(note) &&
                        IsValidDiagonalSpacing(note, existingChain)
                   )
               )
            {
                // --- Unified tail placement block --- fixes diagonal chain aiming wrong direction in love me harder nostalgia 1:47
                (int sx, int sy) StepFromDir(NoteCutDirection d) => d switch
                {
                    NoteCutDirection.Left => (-1, 0),
                    NoteCutDirection.Right => (1, 0),
                    NoteCutDirection.Up => (0, 1),
                    NoteCutDirection.Down => (0, -1),
                    NoteCutDirection.UpLeft => (-1, 1),
                    NoteCutDirection.UpRight => (1, 1),
                    NoteCutDirection.DownLeft => (-1, -1),
                    NoteCutDirection.DownRight => (1, -1),
                    _ => (0, 0)
                };

                int absDx = Math.Abs(existingChain.tailLine - existingChain.line);
                int absDy = Math.Abs((int)existingChain.tailLayer - (int)existingChain.layer);

                // Zero out the axis not used by the head’s direction
                switch (note.cutDirection)
                {
                    case NoteCutDirection.Left:
                    case NoteCutDirection.Right:
                        absDy = 0;
                        break;
                    case NoteCutDirection.Up:
                    case NoteCutDirection.Down:
                        absDx = 0;
                        break;
                }

                (int sx, int sy) = StepFromDir(note.cutDirection);
                int maxPosX = 3, maxPosY = 2;
                int headLy = (int)note.layer;

                int capDx = sx > 0 ? Math.Min(absDx, maxPosX - note.line)
                         : sx < 0 ? Math.Min(absDx, note.line - 0)
                         : 0;
                int capDy = sy > 0 ? Math.Min(absDy, maxPosY - headLy)
                         : sy < 0 ? Math.Min(absDy, headLy - 0)
                         : 0;

                if (capDx == 0 && capDy == 0)
                {
                    matchingChain = null;
                    return false;
                }

                int desiredTailIndex = note.line + sx * capDx;
                int desiredTailLayer = headLy + sy * capDy;

                desiredTailIndex = Math.Clamp(desiredTailIndex, 0, 3);
                desiredTailLayer = Math.Clamp(desiredTailLayer, 0, 2);

                matchingTailIndex = desiredTailIndex;
                matchingTailLayer = desiredTailLayer;


                float headBeat = note.time * TransitionPatcher.bpm / 60f;

                var mirroredChain = ESliderData.CreateChain(
                    note.colorType, note.time, note.line, note.layer, note.cutDirection,
                    existingChain.tailTime, matchingTailIndex, matchingTailLayer,
                    existingChain.sliceCount, existingChain.squishAmount, note
                );

                note.headNoteChain = mirroredChain; note.scoringType = NoteData.ScoringType.ChainHead; note.gameplayType = NoteData.GameplayType.BurstSliderHead;

                doubleChainCount++;

                //Plugin.LogDebug($"[Arcitect][TryCreateDoubleChain] {doubleChainCount} Double chain at {mirroredChain.time:F}");
                //Plugin.LogDebug($"[Arcitect][TryCreateDoubleChain] --- {existingChain.colorType} {existingChain.cutDirection} x: {existingChain.line} y: {(int)existingChain.layer} -- tail -- x: {existingChain.tailLine} y: {existingChain.layer} - dur: {(existingChain.tailTime - existingChain.time):F}");
                //Plugin.LogDebug($"[Arcitect][TryCreateDoubleChain] --- {mirroredChain.colorType} {mirroredChain.cutDirection} x: {mirroredChain.line} y: {(int)mirroredChain.layer} -- tail -- x: {mirroredChain.tailLine} y: {mirroredChain.layer} - dur: {(mirroredChain.tailTime - mirroredChain.time):F}");

                matchingChain = mirroredChain;
                return true;
            }
            else
                //Plugin.LogDebug("[Arcitect][TryCreateDoubleChain] failed to produce double chain.");

            matchingChain = null;
            return false;

            bool naturalPair(ENoteData note, ESliderData existingChain) // left notes (0) on the left of right notes (1). if on the same line, then that is natural no matter which is on top or bottom.
            {
                if (note.line == existingChain.line) // Same column → vertical stack; don't treat this as “crossed hands”.
                    return true;
                if (note.colorType == existingChain.colorType)
                    return false;

                if ((note.line < existingChain.line && note.colorType == ColorType.ColorA) ||
                    (note.line > existingChain.line && note.colorType == ColorType.ColorB))
                    return true;

                return false;
            }



            // Helper: Determines if a direction is diagonal.
            bool IsDiagonal(NoteCutDirection dir) =>
                dir == NoteCutDirection.UpLeft ||
                dir == NoteCutDirection.UpRight ||
                dir == NoteCutDirection.DownLeft ||
                dir == NoteCutDirection.DownRight;

            // Helper: Determines if two diagonal directions are valid for parallel chains.
            // Allowed cases: either the same direction OR one is UpRight with DownLeft (or vice versa)
            // OR one is DownRight with UpLeft (or vice versa).
            bool IsValidDiagonalPair(NoteCutDirection d1, NoteCutDirection d2)
            {
                if (d1 == d2) return true;

                // Up-left + up-right, down-left + down-right, in any order.
                if ((d1 == NoteCutDirection.UpRight && d2 == NoteCutDirection.UpLeft) ||
                    (d1 == NoteCutDirection.UpLeft && d2 == NoteCutDirection.UpRight) ||
                    (d1 == NoteCutDirection.DownRight && d2 == NoteCutDirection.DownLeft) ||
                    (d1 == NoteCutDirection.DownLeft && d2 == NoteCutDirection.DownRight))
                {
                    return true;
                }
                //opposite diagonal” pairs -- too difficult to hit so removed!!!!
                /*
                if ((d1 == NoteCutDirection.UpRight && d2 == NoteCutDirection.DownLeft) ||
                    (d1 == NoteCutDirection.DownLeft && d2 == NoteCutDirection.UpRight) ||
                    (d1 == NoteCutDirection.DownRight && d2 == NoteCutDirection.UpLeft) ||
                    (d1 == NoteCutDirection.UpLeft && d2 == NoteCutDirection.DownRight))
                {
                    return true;
                }
                */
                // WITH THIS ADDED there are NO BAD DIAGONAL PAIRS!!! so better to just rely of position for all diagonal 
                // Same horizontal sense (both right-going or both left-going)
                // For your vertical pairs: UpRight + DownRight, UpLeft + DownLeft.
                if ((d1 == NoteCutDirection.UpRight && d2 == NoteCutDirection.DownRight) ||
                    (d1 == NoteCutDirection.DownRight && d2 == NoteCutDirection.UpRight) ||
                    (d1 == NoteCutDirection.UpLeft && d2 == NoteCutDirection.DownLeft) ||
                    (d1 == NoteCutDirection.DownLeft && d2 == NoteCutDirection.UpLeft))
                {
                    return true;
                }
                Plugin.LogDebug("[Arcitect][TryCreateDoubleChain] IsValidDiagonalPair FALSE");
                return false;
            }
            bool IsValidPositionalPair(ENoteData candidate)
            {
                // For diagonal cases, validate based on candidate note's direction.
                switch (candidate.cutDirection)
                {
                    case NoteCutDirection.UpLeft:
                        if ((int)candidate.layer >= 2 || candidate.line <= 0)
                            return false;
                        break;
                    case NoteCutDirection.UpRight:
                        if ((int)candidate.layer >= 2 || candidate.line >= 3)
                            return false;
                        break;
                    case NoteCutDirection.DownLeft:
                        if ((int)candidate.layer <= 0 || candidate.line <= 0)
                            return false;
                        break;
                    case NoteCutDirection.DownRight:
                        if ((int)candidate.layer <= 0 || candidate.line >= 3)
                            return false;
                        break;
                    default:
                        break;
                }
                return true;
            }
            bool IsValidDiagonalSpacing(ENoteData candidate, ESliderData existingChain)
            {
                int x1 = candidate.line;
                int y1 = (int)candidate.layer;

                int x2 = existingChain.line;
                int y2 = existingChain.layer;

                int dx = Math.Abs(x1 - x2);
                int dy = Math.Abs(y1 - y2);

                Plugin.LogDebug(
                    $"[Arcitect][IsValidDiagonalSpacing] {candidate.time:F} Note: x{x1} y{y1} " +
                    $"ExistingChain: x{x2} y{y2} -- dx: {dx} dy: {dy}");

                // Same exact head cell → nonsense for a double
                if (dx == 0 && dy == 0)
                    return false;

                // Helper: horizontal direction of a diagonal (returns +1 for right, -1 left, 0 otherwise)
                int HorizDiag(NoteCutDirection d) => d switch 
                {
                    NoteCutDirection.UpRight => 1,
                    NoteCutDirection.DownRight => 1,
                    NoteCutDirection.UpLeft => -1,
                    NoteCutDirection.DownLeft => -1,
                    _ => 0
                };

                int h1 = HorizDiag(candidate.cutDirection);
                int h2 = HorizDiag(existingChain.cutDirection);

                // -----------------------------
                // Case 1: same row (dy == 0)
                // -----------------------------
                if (dy == 0)
                {
                    // Identify which head is left and which is right
                    bool candidateIsLeft = x1 <= x2;
                    int hLeft = candidateIsLeft ? h1 : h2;
                    int hRight = candidateIsLeft ? h2 : h1;

                    // Converging if left goes right and right goes left
                    bool converging = (hLeft > 0 && hRight < 0);

                    if (converging)
                    {
                        // Heads are aiming toward each other → require a gap so long chains
                        // don't form a tight X without room to uncross.
                        return dx >= 2;
                    }

                    // Parallel (same direction) or diverging (left goes left, right goes right)
                    // → adjacency is fine.
                    return dx >= 1;
                }

                // -----------------------------
                // Case 2: same column (dx == 0)
                // -----------------------------
                if (dx == 0)
                {
                    // Vertical stacking of diagonals (same column) is generally fine.
                    // dy >= 1 already holds here.
                    return true;
                }

                // -----------------------------
                // Case 3: both x and y differ
                // -----------------------------
                // For now, allow them: they’re already filtered by IsValidDiagonalPair,
                // and UncrossLongDoubleChainsIfNeeded will clean up true X crossings for long chains.
                return true;
            }




        }

        // checks if 2 notes of placed in the grid so that tails could exist
        private static bool IsCompatibleForDoubleChain(ENoteData note, ENoteData note2)
        {
            int headLineLayer; int headLineIndex; int tailLineLayer; int tailLineIndex;
            //bool IsCompatible = false;

            if ((((note.cutDirection == NoteCutDirection.Up || note.cutDirection == NoteCutDirection.Down) && (note2.cutDirection == NoteCutDirection.Up || note2.cutDirection == NoteCutDirection.Down)) && (note.line != note2.line)) ||
                (((note.cutDirection == NoteCutDirection.Left || note.cutDirection == NoteCutDirection.Right) && (note2.cutDirection == NoteCutDirection.Left || note2.cutDirection == NoteCutDirection.Right)) && (note.layer != note2.layer)) ||
                (note.cutDirection == NoteCutDirection.UpLeft && note2.cutDirection == NoteCutDirection.UpLeft) ||
                (note.cutDirection == NoteCutDirection.UpRight && note2.cutDirection == NoteCutDirection.UpRight) ||
                (note.cutDirection == NoteCutDirection.DownLeft && note2.cutDirection == NoteCutDirection.DownLeft) ||
                (note.cutDirection == NoteCutDirection.DownRight && note2.cutDirection == NoteCutDirection.DownRight))
            {
                (headLineLayer, headLineIndex, tailLineLayer, tailLineIndex) = CalculateTailPosition(note2);
                //Plugin.LogDebug($"Double Chain at: {note.time:F} note1 cutDir: {note.cutDirection} note2 cutDir: {matchingNote.cutDirection} note1 lineLayer: {(int)note.layer} note2 lineLayer: {(int)matchingNote.layer}  note1 lineIndex: {note.lineIndex} note2 lineIndex: {matchingNote.lineIndex}");
            }
            else
                return false;

            bool isTailLineIndexValid = tailLineIndex >= 0 && tailLineIndex <= 3;
            bool isTailLineLayerValid = tailLineLayer >= 0 && tailLineLayer <= 2;

            return isTailLineIndexValid && isTailLineLayerValid;
        }

        private static (int headLineLayer, int headLineIndex, int tailLineLayer, int tailLineIndex) CalculateTailPosition(ENoteData note)
        {
            int tailLineIndex, tailLineLayer;

            switch (note.cutDirection)
            {
                case NoteCutDirection.Up:
                    {
                        tailLineIndex = note.line;
                        tailLineLayer = (int)note.layer + 2;
                        break;
                    }
                case NoteCutDirection.Down:
                    {
                        tailLineIndex = note.line;
                        tailLineLayer = (int)note.layer - 2;
                        break;
                    }
                case NoteCutDirection.Left:
                    {
                        tailLineIndex = note.line - 2;
                        tailLineLayer = (int)note.layer;
                        break;
                    }
                case NoteCutDirection.Right:
                    {
                        tailLineIndex = note.line + 2;
                        tailLineLayer = (int)note.layer;
                        break;
                    }
                case NoteCutDirection.UpLeft:
                    {
                        tailLineIndex = note.line - 2;
                        tailLineLayer = (int)note.layer + 2;
                        break;
                    }
                case NoteCutDirection.UpRight:
                    {
                        tailLineIndex = note.line + 2;
                        tailLineLayer = (int)note.layer + 2;
                        break;
                    }
                case NoteCutDirection.DownLeft:
                    {
                        tailLineIndex = note.line - 2;
                        tailLineLayer = (int)note.layer - 2;
                        break;
                    }
                case NoteCutDirection.DownRight:
                    {
                        tailLineIndex = note.line + 2;
                        tailLineLayer = (int)note.layer - 2;
                        break;
                    }
                default: // For Dot Note or any other cases
                    {
                        tailLineIndex = note.line; // No change in position
                        tailLineLayer = (int)note.layer;
                        break;
                    }
            }

            // Ensure tail position is within the valid grid boundaries if changing by 2 is too much change by 1.
            if (tailLineLayer == -1)
            {
                tailLineLayer = 0;
            }
            else if (tailLineLayer == 3)
            {
                tailLineLayer = 2;
            }
            if (tailLineIndex == -1)
            {
                tailLineIndex = 0;
            }
            else if (tailLineIndex == 4)
            {
                tailLineIndex = 3;
            }

            int headLineLayer = (int)note.layer;
            int headLineIndex = note.line;

            // Adjust tail position to keep it within grid boundaries
            if (Config.Instance.AlterNotes)
            {
                bool alteredHeadLineLayer = false;
                bool alteredHeadLineIndex = false;

                if (tailLineLayer < 0) { tailLineLayer = 0; headLineLayer = (int)note.layer + 1; alteredHeadLineLayer = true; }
                if (tailLineLayer > 2) { tailLineLayer = 2; headLineLayer = (int)note.layer - 1; alteredHeadLineLayer = true; }
                if (tailLineIndex < 0) { tailLineIndex = 0; headLineIndex = note.line + 1; alteredHeadLineIndex = true; }
                if (tailLineIndex > 3) { tailLineIndex = 3; headLineIndex = note.line - 1; alteredHeadLineIndex = true; }

                if (alteredHeadLineLayer || alteredHeadLineIndex)
                {
                    ENoteData customNote = ENoteData.Create(
                        note.time,
                        note.colorType,
                        headLineIndex,
                        headLineLayer,
                        note.cutDirection
                    );

                    alteredNotes.Add((note, customNote));
                }

            }

            //so if even changing by 1 is giving results outside the range will still return invalid range if note is incompatible
            return (headLineLayer, headLineIndex, tailLineLayer, tailLineIndex);
        }


        //Chains Tempo Change
        public static List<float> CalculateNoteIntervals(List<ENoteData> notes)
        {
            List<float> intervals = new List<float>();
            for (int i = 0; i < notes.Count - 1; i++)
            {
                float interval = notes[i + 1].time - notes[i].time;
                intervals.Add(interval);
            }
            return intervals;
        }
        /*
        /// <summary>
        /// Replace note with altered note Used for chains. For example, move a note up to allow a chain below it. 
        /// Didn't like using this since it alters notes and usually makes awkward moments
        /// </summary>
        public static void AlterNotes()
        {
            if (alteredNotes.Count > 0)
            {
                foreach (var (note, customNote) in alteredNotes)
                {
                    //FieldHelper.SetProperty(note, "layer", (layer)lineLayer); // set private variable not working

                    int oldLineLayer = (int)note.layer; int oldLineIndex = note.line;

                    eData.ColorNotes.Remove(note);
                    Plugin.LogDebug($"Chain head moved! time: {customNote.time:F} Old lineIndex: {oldLineIndex} Old lineLayer: {oldLineLayer} -- New lineIndex: {customNote.line} New lineLayer: {(int)customNote.layer}");

                }
            }
        }
        */

        public static List<ERotationEventData> ArcFix(
            List<ERotationEventData> rotations,
            EditableCBD eData)
        {
            // ---------------- CONFIG & SETUP ----------------
            const float TOL = 0.0005f;

            bool rotationModeLate = eData.RotationModeLate; //false;

            int allowedCumulativeRots = Config.Instance.ArcRotationMode == Config.ArcRotationModeType.ForceZero ? 0 : 15;

            if (rotations == null || rotations.Count < 2) return rotations;
            if (eData?.Arcs == null || eData.Arcs.Count == 0) return rotations;

            rotations = rotations.OrderBy(r => r.time).ToList();
            var arcs = eData.Arcs.OrderBy(a => a.time).ToList();

            // ---------------- ADD MISSING HEAD/TAIL MARKERS ----------------
            int addedHeads = 0, addedTails = 0;
            bool HasEventAt(float t) => rotations.Any(r => Math.Abs(r.time - t) <= TOL);

            void AddZeroEventAt(float t, string why)
            {
                //Plugin.LogDebug($"[ArcFix] Inserting 0-delta marker at {t:F3}s ({why}).");
                rotations.Add(ERotationEventData.Create(t, 0));
            }

            foreach (var a in arcs)
            {
                if (!HasEventAt(a.time)) { AddZeroEventAt(a.time, "arc head"); addedHeads++; }
                if (!HasEventAt(a.tailTime)) { AddZeroEventAt(a.tailTime, "arc tail"); addedTails++; }
            }
            if (addedHeads + addedTails > 0)
                rotations = rotations.OrderBy(r => r.time).ToList();

            // ---------------- GROUP OVERLAPPING ARCS ----------------
            var arcGroups = new List<List<ESliderData>>();
            {
                var cur = new List<ESliderData>();
                float curMaxTail = float.NegativeInfinity;

                foreach (var a in arcs)
                {
                    if (cur.Count == 0)
                    {
                        cur.Add(a);
                        curMaxTail = a.tailTime;
                    }
                    else if (a.time <= curMaxTail + TOL)
                    {
                        cur.Add(a);
                        curMaxTail = Math.Max(curMaxTail, a.tailTime);
                    }
                    else
                    {
                        arcGroups.Add(new List<ESliderData>(cur));
                        cur.Clear();
                        cur.Add(a);
                        curMaxTail = a.tailTime;
                    }
                }
                if (cur.Count > 0) arcGroups.Add(cur);
            }

            //Plugin.LogDebug($"[Arcitect][ArcFix] Start: arcs={arcs.Count}, groups={arcGroups.Count}, rotBefore={rotations.Count}, " +
            //                $"addedHeads={addedHeads}, addedTails={addedTails}, allowedCumulativeRots={allowedCumulativeRots}, " +
            //                $"mode={(rotationModeLate ? "Late" : "Early")}");

            // ---------------- ADJUSTMENTS ----------------
            var adjusted = new Dictionary<int, int>(); // idx -> reduced delta (never increase)

            int DeltaAt(int idx) => adjusted.TryGetValue(idx, out var v) ? v : rotations[idx].rotation;

            int SumUpTo(float tInclusive)
            {
                int s = 0;
                for (int i = 0; i < rotations.Count; i++)
                {
                    var r = rotations[i];
                    if (r.time <= tInclusive + TOL) s += DeltaAt(i);
                    else break;
                }
                return s;
            }

            // Segment event selection:
            // Early  => (start, end]  (head excluded)
            // Late   => (start, end]  (head excluded)  <-- corrected: never include head in Late segments
            List<int> EventIdxsIn(float start, float end, bool includeStart)
            {
                var idxs = new List<int>();
                for (int i = 0; i < rotations.Count; i++)
                {
                    float tt = rotations[i].time;
                    bool afterStart = includeStart ? (tt >= start - TOL) : (tt > start + TOL);
                    if (afterStart && tt <= end + TOL) idxs.Add(i);
                    else if (tt > end + TOL) break;
                }
                return idxs;
            }

            bool IsAt(float a, float b) => Math.Abs(a - b) <= TOL;

            foreach (var group in arcGroups)
            {
                float gStart = group.Min(a => a.time);
                float gEnd = group.Max(a => a.tailTime);

                // Baseline (engine semantics):
                // Early: include events at head (objects at head see the post-head state).
                // Late:  exclude events at head (objects at head see the pre-head state).
                int baseCum = rotationModeLate
                    ? SumUpTo(gStart - 1e-6f)
                    : SumUpTo(gStart);

                // Boundaries = union of heads/tails in group
                var boundarySet = new SortedSet<float>();
                foreach (var a in group) { boundarySet.Add(a.time); boundarySet.Add(a.tailTime); }
                var B = boundarySet.OrderBy(x => x).ToList();
                if (B.Count < 2) continue;

                int D = 0; // running deviation from baseline within the group

                // ---- Late mode: process head-time events in a dedicated pass, then anchor to AFTER-head state
                if (rotationModeLate)
                {
                    // Collect exact head-time indices
                    var headIdxs = new List<int>();
                    for (int i = 0; i < rotations.Count; i++)
                    {
                        if (IsAt(rotations[i].time, gStart))
                            headIdxs.Add(i);
                        else if (rotations[i].time > gStart + TOL) break;
                    }

                    // Clamp head deltas to stay within ±allowedCumulativeRots relative to D (starts 0)
                    foreach (int idx in headIdxs)
                    {
                        int orig = DeltaAt(idx);
                        if (orig == 0) continue;

                        int lower = -allowedCumulativeRots - D;
                        int upper = allowedCumulativeRots - D;

                        int newVal = orig;
                        if (orig > 0)
                        {
                            int clamped = Math.Min(orig, Math.Max(0, upper));
                            //if (clamped != orig)
                            //    Plugin.LogDebug($"[ArcFix][HeadClamp] t={rotations[idx].time:F3}s +{orig} → +{clamped} (D={D}, upper={upper})");
                            newVal = clamped;
                        }
                        else // orig < 0
                        {
                            int clamped = Math.Max(orig, Math.Min(0, lower));
                            //if (clamped != orig)
                            //    Plugin.LogDebug($"[ArcFix][HeadClamp] t={rotations[idx].time:F3}s {orig} → {clamped} (D={D}, lower={lower})");
                            newVal = clamped;
                        }

                        adjusted[idx] = newVal;
                        D += newVal; // now D reflects the AFTER-head state delta
                    }

                    // Anchor baseline to AFTER-head state for math inside the covered window
                    baseCum += D;
                }

                //Plugin.LogDebug($"[ArcFix][Group] Window {gStart:F3}s → {gEnd:F3}s, baseCum={baseCum}, boundaries={B.Count}, mode={(rotationModeLate ? "Late" : "Early")}");

                float segStart = B[0];
                for (int k = 1; k < B.Count; k++)
                {
                    float segEnd = B[k];

                    // Coverage test (same as before; Early excludes head, Late also excludes head)
                    bool covered = false;
                    foreach (var a in group)
                    {
                        bool headOK = a.time < segEnd - TOL; // head must be strictly before segEnd
                        if (headOK && a.tailTime >= segEnd - TOL) { covered = true; break; }
                    }

                    // In Early, includeStart=false ( (start,end] ).
                    // In Late, also includeStart=false ( (start,end] )  <-- corrected
                    var segIdxs = EventIdxsIn(segStart, segEnd, includeStart: false);

                    if (segIdxs.Count > 0)
                    {
                        //Plugin.LogDebug($"[ArcFix][Seg] {segStart:F3}s → {segEnd:F3}s, covered={covered}, events={segIdxs.Count}, includeStart=False");

                        // ---- 1) Clamp pass: keep |baseCum + D + delta| <= allowed ----
                        foreach (int idx in segIdxs)
                        {
                            int orig = DeltaAt(idx);
                            if (orig == 0) continue;

                            int lower = -allowedCumulativeRots - D;
                            int upper = allowedCumulativeRots - D;

                            int newVal = orig;
                            if (orig > 0)
                            {
                                int clamped = Math.Min(orig, Math.Max(0, upper));
                                //if (clamped != orig)
                                //    Plugin.LogDebug($"[ArcFix][Clamp] t={rotations[idx].time:F3}s +{orig} → +{clamped} (D={D}, upper={upper})");
                                newVal = clamped;
                            }
                            else // orig < 0
                            {
                                int clamped = Math.Max(orig, Math.Min(0, lower));
                                //if (clamped != orig)
                                //    Plugin.LogDebug($"[ArcFix][Clamp] t={rotations[idx].time:F3}s {orig} → {clamped} (D={D}, lower={lower})");
                                newVal = clamped;
                            }

                            adjusted[idx] = newVal;
                            D += newVal;
                        }

                        // ---- 2) Net-zero covered segments so head==tail accum inside overlap ----
                        if (covered)
                        {
                            int segSum = 0;
                            foreach (int idx in segIdxs) segSum += DeltaAt(idx);

                            if (segSum != 0)
                            {
                                //Plugin.LogDebug($"[ArcFix][Zero] Neutralizing segSum={segSum} by shaving from end.");

                                if (segSum > 0)
                                {
                                    int remain = segSum;
                                    for (int j = segIdxs.Count - 1; j >= 0 && remain > 0; j--)
                                    {
                                        int idx = segIdxs[j];
                                        int val = DeltaAt(idx);
                                        if (val > 0)
                                        {
                                            int dec = Math.Min(val, remain);
                                            int newVal = val - dec;
                                            adjusted[idx] = newVal;
                                            remain -= dec;
                                            //Plugin.LogDebug($"[ArcFix][Zero]  t={rotations[idx].time:F3}s +{val} → +{newVal} (took {dec})");
                                        }
                                    }
                                    D -= segSum; // restore deviation to seg start
                                }
                                else // segSum < 0
                                {
                                    int remain = -segSum;
                                    for (int j = segIdxs.Count - 1; j >= 0 && remain > 0; j--)
                                    {
                                        int idx = segIdxs[j];
                                        int val = DeltaAt(idx);
                                        if (val < 0)
                                        {
                                            int inc = Math.Min(-val, remain); // toward 0
                                            int newVal = val + inc;
                                            adjusted[idx] = newVal;
                                            remain -= inc;
                                            //Plugin.LogDebug($"[ArcFix][Zero]  t={rotations[idx].time:F3}s {val} → {newVal} (gave {inc})");
                                        }
                                    }
                                    D -= segSum; // segSum is negative; restores D to seg start
                                }
                            }
                        }
                    }
                    else
                    {
                        //Plugin.LogDebug($"[Arcitect][ArcFix][Seg] {segStart:F3}s → {segEnd:F3}s, covered={covered}, events=0 (nothing to adjust).");
                    }

                    segStart = segEnd;
                }
            }

            // try to fix NoRestrictions when 2 notes at the same time on same layer on different rotations will cross together. need to enforce same rotation on each note in that case.
            // Identify arcs that are involved in "problem" same-time/same-layer note clusters.
            // Used ONLY when ArcRotationMode == NoRestriction.
            var arcsNeedingNetZero = new HashSet<ESliderData>();

            if (Config.Instance.ArcRotationMode == Config.ArcRotationModeType.NoRestriction
                && eData?.ColorNotes != null
                && eData.ColorNotes.Count > 1)
            {
                // Key: (time, layer) where >=2 notes share that (within TOL)
                var problemKeys = new HashSet<(float time, int layer)>();

                var notesByTime = eData.ColorNotes
                    .OrderBy(n => n.time)
                    .ToList();

                int i = 0;
                while (i < notesByTime.Count)
                {
                    float baseTime = notesByTime[i].time;
                    int j = i + 1;

                    while (j < notesByTime.Count &&
                           Math.Abs(notesByTime[j].time - baseTime) <= TOL)
                    {
                        j++;
                    }

                    var cluster = notesByTime.GetRange(i, j - i);

                    // Group by layer and only mark (time,layer) with >=2 notes
                    foreach (var g in cluster.GroupBy(n => n.layer))
                    {
                        if (g.Count() >= 2)
                        {
                            // everything in this small time window is effectively same time
                            float clusterTime = baseTime;
                            problemKeys.Add((clusterTime, g.Key));
                            //Plugin.LogDebug($"[ArcFix][ProblemCluster] t≈{clusterTime:F3}s layer={g.Key} count={g.Count()}");
                        }
                    }

                    i = j;
                }

                if (problemKeys.Count > 0)
                {
                    // Helper to see if a (time, layer) is near a problemKey
                    bool IsProblemTimeLayer(float t, int layer)
                    {
                        foreach (var pk in problemKeys)
                        {
                            if (pk.layer != layer) continue;
                            if (Math.Abs(pk.time - t) <= TOL)
                                return true;
                        }
                        return false;
                    }

                    // Map problem (time,layer) back to arcs via their head/tail notes
                    foreach (var a in arcs)
                    {
                        bool mark = false;

                        if (a.headNote != null &&
                            IsProblemTimeLayer(a.headNote.time, a.headNote.layer))
                        {
                            mark = true;
                        }

                        if (!mark && a.tailNote != null &&
                            IsProblemTimeLayer(a.tailNote.time, a.tailNote.layer))
                        {
                            mark = true;
                        }

                        if (mark)
                            arcsNeedingNetZero.Add(a);
                    }

                    //Plugin.LogDebug($"[ArcFix] arcsNeedingNetZero={arcsNeedingNetZero.Count} (NoRestriction, same-time+layer)");
                }
            }



            // ---------------- PER-ARC NET-ZERO ENFORCEMENT ----------------
            // Ensures selected arcs have head==tail accum per engine semantics.
            //
            // - In NetZero mode: all arcs.
            // - In NoRestriction: only arcs in arcsNeedingNetZero.
            bool doPerArcNetZero =
                Config.Instance.ArcRotationMode == Config.ArcRotationModeType.NetZero
                || (Config.Instance.ArcRotationMode == Config.ArcRotationModeType.NoRestriction
                    && arcsNeedingNetZero.Count > 0);

            if (doPerArcNetZero)
            {
                // Precompute times for binary search
                var times = rotations.Select(r => r.time).ToArray();

                // Binary search helpers
                int LowerBound(float x)
                { // first idx with times[idx] >= x
                    int lo = 0, hi = times.Length;
                    while (lo < hi)
                    {
                        int mid = (lo + hi) >> 1;
                        if (times[mid] < x) lo = mid + 1; else hi = mid;
                    }
                    return lo;
                }
                int UpperBound(float x)
                { // first idx with times[idx] > x
                    int lo = 0, hi = times.Length;
                    while (lo < hi)
                    {
                        int mid = (lo + hi) >> 1;
                        if (times[mid] <= x) lo = mid + 1; else hi = mid;
                    }
                    return lo;
                }

                // Get current (possibly adjusted) delta
                int GetVal(int i) => adjusted.TryGetValue(i, out var v) ? v : rotations[i].rotation;

                // Sum over [i0, i1] inclusive (caller ensures i0<=i1)
                int SumRange(int i0, int i1)
                {
                    int s = 0;
                    for (int i = i0; i <= i1; i++) s += GetVal(i);
                    return s;
                }

                // For diagnostics: show what sanity check compares for Late/Early
                int AccumInclusive(float t)
                {
                    int s = 0;
                    for (int i = 0; i < rotations.Count; i++)
                    {
                        if (rotations[i].time <= t + TOL) s += GetVal(i); else break;
                    }
                    return s;
                }
                int AccumBefore(float t)
                {
                    int s = 0;
                    for (int i = 0; i < rotations.Count; i++)
                    {
                        if (rotations[i].time < t - TOL) s += GetVal(i); else break;
                    }
                    return s;
                }

                foreach (var a in arcs)
                {
                    float head = a.time;
                    float tail = a.tailTime;

                    // Interval indices per engine semantics:
                    // Early:  [head, tail]  -> include events at both ends (unchanged)
                    // Late:   [head-TOL, tail-TOL)  -> EXACT match for AccumBefore(tail)-AccumBefore(head)
                    int startIdx, endIdx;

                    if (!rotationModeLate)
                    {
                        // Early: include head and tail as before
                        startIdx = LowerBound(head - TOL);      // first idx with time >= head - TOL
                        endIdx = UpperBound(tail + TOL) - 1;  // last  idx with time <= tail + TOL
                    }
                    else
                    {
                        // Late: sum events e with head - TOL <= e.time < tail - TOL
                        startIdx = LowerBound(head - TOL);      // first idx with time >= head - TOL
                        int endExclusive = LowerBound(tail - TOL); // first idx with time >= tail - TOL
                        endIdx = endExclusive - 1;              // last idx with time < tail - TOL
                    }

                    if (startIdx >= rotations.Count || endIdx < 0 || startIdx > endIdx)
                    {
                        // No events in this arc interval — log once for visibility
                        //Plugin.LogDebug($"[ArcFix][ArcZero][SKIP] no events in {(rotationModeLate ? "(head,tail]" : "[head,tail]")} @ {head:F3}->{tail:F3} (idx {startIdx}..{endIdx})");
                        continue;
                    }

                    int arcSum = SumRange(startIdx, endIdx);

                    // Cross-check: what does sanity compare?
                    int sanHead = rotationModeLate ? AccumBefore(head) : AccumInclusive(head);
                    int sanTail = rotationModeLate ? AccumBefore(tail) : AccumInclusive(tail);
                    int sanDiff = sanTail - sanHead; // should equal arcSum if indices are correct

                    //string intervalLabel = rotationModeLate ? "[head-TOL,tail-TOL)" : "[head,tail]";
                    //Plugin.LogDebug(
                    //    $"[ArcFix][ArcZero][CHK] {intervalLabel} @ {head:F3}->{tail:F3}  "
                    //  + $"idx {startIdx}..{endIdx}  arcSum={arcSum}  sanityDiff={sanDiff}  "
                    //  + $"events={Math.Max(0, endIdx - startIdx + 1)}"
                    //);

                    if (arcSum == 0) continue; // already neutral for this arc

                    // Safety: if our interval sum doesn't match sanity, tighten bounds
                    if (arcSum != sanDiff)
                    {
                        // Try one more time with stricter bounds matching exact semantics:
                        if (!rotationModeLate)
                        {
                            // exact [head, tail]
                            startIdx = LowerBound(head - TOL);
                            while (startIdx > 0 && Math.Abs(times[startIdx - 1] - head) <= TOL) startIdx--;
                            endIdx = UpperBound(tail + TOL) - 1;
                        }
                        else
                        {
                            // exact (head, tail]
                            startIdx = UpperBound(head + TOL);
                            endIdx = UpperBound(tail + TOL) - 1;
                        }

                        if (!(startIdx < rotations.Count && endIdx >= 0 && startIdx <= endIdx))
                        {
                            //Plugin.LogDebug($"[ArcFix][ArcZero][WARN] could not align indices for {head:F3}->{tail:F3}");
                            continue;
                        }

                        arcSum = SumRange(startIdx, endIdx);
                        sanDiff = (rotationModeLate ? AccumBefore(tail) - AccumBefore(head)
                                                    : AccumInclusive(tail) - AccumInclusive(head));
                        //Plugin.LogDebug($"[ArcFix][ArcZero][CHK2] idx {startIdx}..{endIdx}  arcSum={arcSum}  sanityDiff={sanDiff} (post-align)");
                        if (arcSum == 0) continue;
                    }

                    // Neutralize by shaving from the end toward zero
                    //Plugin.LogDebug($"[ArcFix][ArcZero] Neutralizing arcSum={arcSum} over {(rotationModeLate ? "(head,tail]" : "[head,tail]")} @ {head:F3}->{tail:F3}");

                    if (arcSum > 0)
                    {
                        int remain = arcSum;
                        for (int j = endIdx; j >= startIdx && remain > 0; j--)
                        {
                            int val = GetVal(j);
                            if (val > 0)
                            {
                                int take = Math.Min(val, remain);
                                int newVal = val - take;           // move toward 0
                                adjusted[j] = newVal;
                                remain -= take;
                                //Plugin.LogDebug($"[ArcFix][ArcZero]  t={times[j]:F3}s +{val} → +{newVal} (took {take})");
                            }
                        }
                    }
                    else
                    { // arcSum < 0
                        int remain = -arcSum;
                        for (int j = endIdx; j >= startIdx && remain > 0; j--)
                        {
                            int val = GetVal(j);
                            if (val < 0)
                            {
                                int give = Math.Min(-val, remain); // toward 0
                                int newVal = val + give;
                                adjusted[j] = newVal;
                                remain -= give;
                                //Plugin.LogDebug($"[ArcFix][ArcZero]  t={times[j]:F3}s {val} → {newVal} (gave {give})");
                            }
                        }
                    }
                }
            }

            // ---------------- BUILD RESULT ----------------
            // Keep boundary markers (even if 0), drop other zeros to keep things tidy.
            var boundaryTimes = new HashSet<float>(arcs.Select(a => a.time));
            foreach (var a in arcs) boundaryTimes.Add(a.tailTime);

            var result = new List<ERotationEventData>(rotations.Count);
            int reduced = 0, removed = 0, unchanged = 0;
            
            for (int i = 0; i < rotations.Count; i++)
            {
                var r = rotations[i];
                int val = adjusted.TryGetValue(i, out var v) ? v : r.rotation;

                bool isBoundary = boundaryTimes.Any(bt => Math.Abs(bt - r.time) <= TOL);

                if (val == 0)
                {
                    if (isBoundary)
                    {
                        // keep explicit boundary zero
                        if (val == r.rotation) unchanged++;
                        else reduced++;

                        result.Add(ERotationEventData.Create(r.time, 0, 0, r.customData));
                    }
                    else
                    {
                        removed++; // drop interior zero
                    }

                    continue;
                }

                if (val == r.rotation)
                {
                    unchanged++;
                    result.Add(r);
                }
                else
                {
                    reduced++;
                    result.Add(ERotationEventData.Create(r.time, val, 0, r.customData));
                }
            }

            // ---------------- FINAL: RECOMPUTE accumRotation ----------------
            result = ERotationEventData.RecalculateAccumulatedRotations(result);

            Plugin.LogDebug($"[Arcitect][ArcFix] Done: Rotation Final Count={result.Count} (reduced={reduced}, removed={removed}, unchanged={unchanged}). " +
                            $"ArcRotationMode={Config.Instance.ArcRotationMode}, allowedCumul={allowedCumulativeRots}, mode={(rotationModeLate ? "Late" : "Early")}");

            // Engine-accurate sanity check: show only assumed accumRotation at arc HEAD and TAIL.
            //SanityCheckHeadsTails_EngineView(result, arcs, TOL, rotationModeLate);

            return result;
        }
    }
}
