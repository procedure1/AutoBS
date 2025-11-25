using AutoBS.Patches;
using BeatmapSaveDataVersion4;
using CustomJSONData.CustomBeatmap;
using HMUI;
using JetBrains.Annotations;
using System;
using System.Collections.Generic;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Reflection;
using UnityEngine;


namespace AutoBS
{
    
    internal class Arcitect//Lolighter https://github.com/Loloppe/Lolighter/blob/master/Lolighter/Algorithm/Arc.cs
    {
        public static List<ENoteData> notes; // hold original notes for various checks
        public static List<ESliderData> arcs; // hold finished arcs for various chains checks
        //v1.39
        public static Version version = new Version(3, 3, 0);


        public static int preferredArcCount = 1;
        public static float arcMultiplier = 1f;
        //public static float controlPointLength = Config.Instance.ControlPointLength;
        //public static float minDuration = Config.Instance.MinArcDuration;
        //public static float maxDuration = Config.Instance.MaxArcDuration;

        //public static List<ESliderData> arcsAndChains = new List<ESliderData>();

        public static float lowestPossibleMinDurationAdjustment = 0.4f;


        public static NoteLineLayer headBeforeJumpLineLayer = 0; //no idea not used by map creators but seems to be base when i log the Magic OST map
        public static NoteLineLayer tailBeforeJumpLineLayer = 0; //no idea not used by map creators but seems to be base when i log the Magic OST map

        public static float headCutDirectionAngleOffset = 0f; // no idea not used by map creators
        public static float tailCutDirectionAngleOffset = 0f; // no idea not used by map creators

        public static SliderMidAnchorMode sliderMidAnchorMode = SliderMidAnchorMode.Straight;

        //public static int sliceCount = 0; // burst sldier only - An integer number, greater than 0 for burst, which represents the number of segments in the burst slider. The head counts as a segment.
        public static float squishAmount = 1f; // burst slider only - .5 is squeezed together and 1.5 is unsqueezed - cannot be 0. A float which represents squish factor. This is the proportion of how much of the path from (x,y) to (tx, ty) is used by the chain. This does not alter the shape of the path. Values greater than 1 will extend the path beyond the specified end point.

        public static bool addArc;

        //CHAINS-------------------------

        public static float pauseThresholdMultiplier = 1.5f;// makes pause detection adaptive to the varying average speeds of different sections of the song

        public static int preferredChainCount = 1;
        public static bool forceMoreChains = false;
        public static bool pauseDetection = true; // algorithm type

        public static int doubleChainCount = 0;

        //public static List<ENoteData> TempLongChainTails = new List<ENoteData>(); // used by ApplyWallVisionBlockingFix() to alter walls for chain tails

        public static CustomBeatmapData data;

        //public static List<ENoteData> doubleChainNotes = new List<ENoteData>(); // when find a chain compatible note and find it is next to another compatible note in the other color, that note will be added here

        /// <summary>
        /// Change a notes position to make chain possible. For example, move a note up to allow a chain below it. 
        /// Didn't like using this since it alters notes and usually makes awkward moments
        /// </summary>
        public static List<(ENoteData, ENoteData)> alteredNotes = new List<(ENoteData, ENoteData)>();

        public static List<ESliderData> chainsTemp = new List<ESliderData>(); // used by MoveWallsBlockingChainTail()

        public static int chainArcToggle = 0; // toggle between arcs (most of the time) and ChairArcs (long chain with tail note)

        public static string ScoreSubmissionDisableText = "";


        // FIXXXXXXXXXXXXX!!!!! if have original arcs in map without chains, chains will use empty arcsAndChains and therefore add chains to arc tails sometimes
        /// <summary>
        /// Method to create Arcs and Chains. Arcs between two notes of the same colorType based on their cut direction and the duration between notes. Chains are creaed based on pause detection between notes.
        /// </summary>
        /// <returns>List of SliderData and writes to BeatmapData</returns>
        public static void CreateSliders(EditableCBD eData) // reference list so no need to return it.
        {
            ScoreSubmissionDisableText = "";

            if (eData.ColorNotes.Count == 0) return;
            //If AlterNotes() is used, the original notes of the song would be altered permanently in the context of that data object.
            //but haven't been able to solve the problem.
            //data = bmData;

            notes = eData.ColorNotes; // hold original notes for various checks

            //version = TransitionPatcher.version;


            //List<ESliderData> prevArc = eData.Sliders;
            //List<ESliderData> prevChains = eData.BurstSliders;


            doubleChainCount = 0;

            //arcsAndChains.Clear();// Clear the existing arcs list to avoid duplication when you play the same song again.
            //doubleChainNotes.Clear();

            

            float songDuration = (eData.ColorNotes.Last().time - eData.ColorNotes.First().time) / 60; // minutes between 1st and last note

            Plugin.Log.Info($"Arcitect: Song Duration: {songDuration} sec. ColorNote Count: {eData.ColorNotes.Count()} 1st Note at: {eData.ColorNotes.First().time} last note at: {eData.ColorNotes.Last().time}");

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
                Plugin.Log.Info($"Preferred Arc Count: {preferredArcCount}.");

                eData.Arcs = SetPreferredArcCount(colorA, colorB);

                eData.Arcs = eData.Arcs.OrderBy(o => o.time).ToList(); // nust do it this way with the =

                arcs = eData.Arcs; // save arcs to static variable for chains to use

                if (!Utils.IsEnabledWalls() || Utils.IsEnabledWalls() && (!Config.Instance.EnableStandardWalls || !Config.Instance.EnableBigWalls)) // this should be used whenever chains are added. but should be done after standard/big walls are added since those walls can block chain tails too
                    WallGenerator.MoveWallsBlockingArc(eData);
            }
            else
            {
                Plugin.Log.Info($"Arcitect: Map already has {eData.Arcs.Count} arcs so will not add any.");
                arcs = eData.Arcs; // save arcs to static variable for chains to use
            }

            if (eData.Chains.Count == 0 && Utils.IsEnabledChains())//only add chains if map doesn't have any
            {
                Plugin.Log.Info($"Preferred Chain Count: {preferredChainCount}.");

                if (pauseDetection) // USING THIS <-------------------------------------------------------
                {
                    Plugin.Log.Info($"Chain - Pause Detection Algorithm.");
                    eData.Chains = SetPreferredChainCountPauseDetection(colorA, colorB);
                }
                else
                {
                    Plugin.Log.Info($"Chain - Tempo Change Algorithm.");
                    eData.Chains = SetPreferredChainCountTempoChange(colorA, colorB);
                }

                eData.Chains = eData.Chains.OrderBy(o => o.time).ToList(); // sort chains by time

                if (eData.Chains.Count > 0) ScoreSubmissionDisableText = "Architect Chains";

                if (!Utils.IsEnabledWalls() || Utils.IsEnabledWalls() && (!Config.Instance.EnableStandardWalls || !Config.Instance.EnableBigWalls)) // this should be used whenever chains are added. but should be done after standard/big walls are added since those walls can block chain tails too
                    WallGenerator.MoveWallsBlockingChainTail(eData);
            }
            else
            {
                Plugin.Log.Info($"Arcitect: Map already has {eData.Chains.Count} chains so will not add any.");

                ScoreSubmissionDisableText = "";
            }

            /*

            if ((eData.Sliders.Count == 0 && Utils.IsEnabledArcs()) || (eData.BurstSliders.Count == 0 && Utils.IsEnabledChains()))
            {
                Plugin.Log.Info($"CreateSliders(): Force Natural Arcs: {Config.Instance.ForceNaturalArcs}");

                //arcsAndChains = arcsAndChains.OrderBy(o => o.time).ToList();
                eData.Sliders.OrderBy(o => o.time).ToList(); 
                eData.BurstSliders.OrderBy(o => o.time).ToList();

                Plugin.Log.Info($"Final Sliders: Arcs {eData.Sliders.Count} and chains {eData.BurstSliders.Count}. ");// +
                   // $"Arcs: {arcsAndChains.Count(s => s.sliderType == SliderData.Type.Normal)}. Chains: {arcsAndChains.Count(s => s.sliderType == SliderData.Type.Burst)}.");
            */
            /*
            foreach (SliderData slider in arcsAndChains) // add arcs to BeatmapData
            {
                string logMessage =
                        $"\nTime: {slider.time:F}, " +
                        $"\nType: {slider.sliderType}, -----------------" +
                        $"\nColor: {slider.colorType}, " +
                        //$"\nHas Head Note: {arc.hasHeadNote}, " +
                        $"\nIndex: {slider.headLineIndex}, " +
                        $"Layer: {(int)slider.headLineLayer}, " +
                        //$"\nBefore Layer: {(int)slider.headBeforeJumpLineLayer}, " +
                        //$"\nControl: {arc.headControlPointLengthMultiplier}, " +
                        $"\nDir: {slider.headCutDirection}, " + //arc cutDirection should match note cutDirection
                                                                //$"\nAngle: {arc.headCutDirectionAngleOffset}, " +
                                                                //$"\nHas tail: {arc.hasTailNote}, " +
                        $"\nt Time: {slider.tailTime}, " +
                        $"\nt Index: {slider.tailLineIndex}, " +
                        $"t Layer: {(int)slider.tailLineLayer}, " +
                        //$"\nt Before Layer: {(int)slider.tailBeforeJumpLineLayer}, " +
                        //$"\nt Control: {arc.tailControlPointLengthMultiplier}, " +
                        $"\nt Dir: {slider.tailCutDirection}, " + //arc cutDirection should match note cutDirection
                                                                  //$"\ntail Angle: {arc.tailCutDirectionAngleOffset}, " +
                                                                  //$"\nMode: {arc.midAnchorMode}, " +
                        $"\nSlice Count: {slider.sliceCount}, " +
                        $"Squish Amount: {slider.squishAmount} " +
                        "\n ";
                Plugin.Log.Info(logMessage);


                //Testing
                //if (arc.colorType == ColorType.ColorB)//colorA is left colorB is right

                //string arcDir = "";
                //if (slider.sliderType == SliderData.Type.Normal)
                //{
                //    arcDir = "$ Dir1: " + slider.headCutDirection + " Dir2: " + slider.tailCutDirection + "Angle Diff: " + (slider.headCutDirection.RotationAngle() - slider.tailCutDirection.RotationAngle());
                //}


                //Plugin.Log.Info($" ---- {slider.time} index: {slider.headLineIndex} layer: {(int)slider.headLineLayer} - End: {slider.tailTime}  index: {slider.tailLineIndex} layer: {(int)slider.tailLineLayer} - {slider.sliderType} {slider.colorType} + {arcDir}");


                data.AddBeatmapObjectDataInOrder(slider); //v1.40
            }
            */
            /*
            foreach (SliderData prevChain in prevChains) // add arcs to BeatmapData
            {
                string logMessage =
                        $"\nType: {prevChain.sliderType}, -----------------" +
                        $"\nColor: {prevChain.colorType}, " +
                        $"\nHas Head Note: {prevChain.hasHeadNote}, " +
                        $"\nTime: {prevChain.time:F}, " +
                        $"\nIndex: {prevChain.headLineIndex}, " +
                        $"Layer: {(int)prevChain.headLineLayer}, " +
                        $"\nBefore Layer: {(int)prevChain.headBeforeJumpLineLayer}, " +
                        $"\nControl: {prevChain.headControlPointLengthMultiplier}, " +
                        $"\nDir: {prevChain.headCutDirection}, " + //arc cutDirection should match note cutDirection
                        $"\nAngle: {prevChain.headCutDirectionAngleOffset}, " +
                        $"\nHas tail: {prevChain.hasTailNote}, " +
                        $"\nt Time: {prevChain.tailTime}, " +
                        $"\nt Index: {prevChain.tailLineIndex}, " +
                        $"t Layer: {(int)prevChain.tailLineLayer}, " +
                        $"\nt Before Layer: {(int)prevChain.tailBeforeJumpLineLayer}, " +
                        $"\nt Control: {prevChain.tailControlPointLengthMultiplier}, " +
                        $"\nt Dir: {prevChain.tailCutDirection}, " + //arc cutDirection should match note cutDirection
                        $"\ntail Angle: {prevChain.tailCutDirectionAngleOffset}, " +
                        $"\nMode: {prevChain.midAnchorMode}, " +
                        $"\nSlice Count: {prevChain.sliceCount}, " +
                        $"Squish Amount: {prevChain.squishAmount} " +
                        "\n ";
                    Plugin.Log.Info(logMessage);
            }
            */
            //}
            //bw added to make sure that prev data is included in arcs. without this, arcFix will not fix arcs for v3 maps that already had arcs
            //if (eData.Sliders.Count > 0)
            //    arcsAndChains.AddRange(eData.Sliders);
            //if (eData.BurstSliders.Count > 0)
            //    arcsAndChains.AddRange(eData.BurstSliders);

            //arcsAndChains = arcsAndChains.OrderBy(o => o.time).ToList();

            Plugin.Log.Info($"Arcitect after Creation: Arcs Count: {eData.Arcs.Count}. Chains Count: {eData.Chains.Count}.");
            /*
            foreach (ESliderData arc in eData.Arcs)
            {
                //if (slider.sliderType == SliderData.Type.Normal)
                {
                    //Plugin.Log.Info($"Arc: time: {arc.time} index: {arc.headLineIndex} layer: {(int)arc.headLineLayer} - Tail time: {arc.tailTime} index: {arc.tailLineIndex} layer: {(int)arc.tailLineLayer}");
                    Plugin.Log.Info($"Arc: time: {arc.time} --- Tail time: {arc.tailTime}");
                }
            }
            */
            //return (arcsAndChains);
        }

        public static List<ESliderData> SetPreferredArcCount(List<ENoteData> colorA, List<ENoteData> colorB) // https://github.com/Loloppe/Lolighter/blob/master/Lolighter/Algorithm/Arc.cs
        {
            if (colorA.Count < 3 && colorB.Count < 3)
                return new List<ESliderData>();

            float arcMult = arcMultiplier;

            float minDur = Config.Instance.MinArcDuration;

            //1st pass: add arcs to arcs list using default arcMult and minDur
            //arcsAndChains.Clear();

            List<ESliderData> arcs = new List<ESliderData>();

            if (colorA.Count > 1)
                arcs.AddRange(CreateArcs(colorA, arcMult, minDur));
            if (colorB.Count > 1)
                arcs.AddRange(CreateArcs(colorB, arcMult, minDur));

            if (arcs.Count < preferredArcCount) // check if produced too few arcs
            {
                Plugin.Log.Info($"Using arcMultiplier: {arcMult} minDuration: {minDur} - Not enough arcs produced: {arcs.Count} which is {preferredArcCount - arcs.Count} less than desired.");

                //reduce minDur
                minDur = Config.Instance.MinArcDuration - lowestPossibleMinDurationAdjustment / 2;
                if (minDur < 0) minDur = .2f;

                //2nd pass: add arcs to arcs list using default arcMult and shorter minDur
                //arcsAndChains.Clear();
                arcs.Clear();

                if (colorA.Count > 1)
                    arcs.AddRange(CreateArcs(colorA, arcMult, minDur));
                if (colorB.Count > 1)
                    arcs.AddRange(CreateArcs(colorB, arcMult, minDur));

                if (arcs.Count < preferredArcCount) // check if produced too few arcs
                {
                    Plugin.Log.Info($"Using arcMultiplier: {arcMult} minDuration: {minDur} - Not enough arcs produced: {arcs.Count} which is {preferredArcCount - arcs.Count} less than desired.");

                    //reduce minDur again
                    minDur = Config.Instance.MinArcDuration - lowestPossibleMinDurationAdjustment;
                    if (minDur < 0) minDur = .2f;

                    //3rd pass: add arcs to arcs list using default arcMult and even shorter minDur
                    //arcsAndChains.Clear();
                    arcs.Clear();

                    if (colorA.Count > 1)
                        arcs.AddRange(CreateArcs(colorA, arcMult, minDur));
                    if (colorB.Count > 1)
                        arcs.AddRange(CreateArcs(colorB, arcMult, minDur));
                }
            }

            if (arcs.Count > preferredArcCount) // check if there are too many arcs
            {
                Plugin.Log.Info($"Using arcMultiplier: {arcMult} minDuration: {minDur} - Too many arcs produced: {arcs.Count}  which is {arcs.Count - preferredArcCount} more than desired so will adjust arcMultiplier.");

                // Reduce arcMultiplier to decrease the number of arcs to the perfect amount
                arcMult = (float)Math.Max(0.1, arcMultiplier * (preferredArcCount / (float)arcs.Count));

                Plugin.Log.Info($"Using arcMultiplier: {arcMult} minDuration: {minDur} - should produce the preferred count of {preferredArcCount}.");

                //4th pass maybe: add arcs to arcs list using reduced arcMult and latest minDur
                //arcsAndChains.Clear();
                arcs.Clear();

                if (colorA.Count > 1)
                    arcs.AddRange(CreateArcs(colorA, arcMult, minDur));
                if (colorB.Count > 1)
                    arcs.AddRange(CreateArcs(colorB, arcMult, minDur));

                Plugin.Log.Info($"Arc Count is: {arcs.Count} which is {arcs.Count - preferredArcCount} more than desired.");
            }
            else if (arcs.Count == preferredArcCount)
                Plugin.Log.Info($"Arc Count is: {arcs.Count}. That is the desired goal!");
            else
                Plugin.Log.Info($"Using arcMultiplier: {arcMult} minDuration: {minDur} - Not enough arcs produced :( But we reached the limit so we are done! Final count: {arcs.Count}");


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
                            float tailControlPointLengthMultiplier = 1;// Config.Instance.ControlPointLength; // A float which represents how far the arc goes from the head of the arc. If head direction is a dot, this does nothing.
                            //var arcType = SliderData.Type.Normal;
                            //int sliceCount = 0;

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

                //Plugin.Log.Info($"{colorType} Arc Count: {countArcs}.");
            }
            return arcs;
        }
        public static bool IsCompatibleForArc(ENoteData note1, ENoteData note2)
        {
            if (note1.cutDirection == NoteCutDirection.Any || note2.cutDirection == NoteCutDirection.Any)
                return false;

            if (Config.Instance.ForceNaturalArcs)
            {
                return IsContraryLikeDirectionBetweenSwings(note1, note2);
            }
            else
            {
                return IsContrarySwingsBy135Degrees(note1, note2);
            }
        }

        public static bool IsContraryLikeDirectionBetweenSwings(ENoteData note1, ENoteData note2) // new BW version that takes into account how it feels to reverse the swing of each hand
        {
            if (note1.colorType == ColorType.ColorA) // right blue notes - right favors right and down
            {
                switch (note1.cutDirection)
                {
                    case NoteCutDirection.Up:
                        {
                            if (note2.cutDirection == NoteCutDirection.Down || note2.cutDirection == NoteCutDirection.DownRight)
                                return true;
                            else
                                return false;
                        }
                    case NoteCutDirection.Down:
                        {
                            if (note2.cutDirection == NoteCutDirection.Up || note2.cutDirection == NoteCutDirection.UpRight)
                                return true;
                            else
                                return false;
                        }
                    case NoteCutDirection.Left:
                        {
                            if (note2.cutDirection == NoteCutDirection.Right || note2.cutDirection == NoteCutDirection.DownRight)
                                return true;
                            else
                                return false;
                        }
                    case NoteCutDirection.Right:
                        {
                            if (note2.cutDirection == NoteCutDirection.Left || note2.cutDirection == NoteCutDirection.DownLeft)
                                return true;
                            else
                                return false;
                        }
                    case NoteCutDirection.UpLeft:
                        {
                            if (note2.cutDirection == NoteCutDirection.DownRight)
                                return true;
                            else
                                return false;
                        }
                    case NoteCutDirection.UpRight:
                        {
                            if (note2.cutDirection == NoteCutDirection.DownLeft)
                                return true;
                            else
                                return false;
                        }
                    case NoteCutDirection.DownLeft:
                        {
                            if (note2.cutDirection == NoteCutDirection.UpRight)
                                return true;
                            else
                                return false;
                        }
                    case NoteCutDirection.DownRight:
                        {
                            if (note2.cutDirection == NoteCutDirection.UpLeft)
                                return true;
                            else
                                return false;
                        }
                    default: // For Dot Note or any other cases
                        {
                            return false;
                        }
                }
            }
            else // left red notes left favors left and down
            {
                switch (note1.cutDirection)
                {
                    case NoteCutDirection.Up:
                        {
                            if (note2.cutDirection == NoteCutDirection.Down || note2.cutDirection == NoteCutDirection.DownLeft)
                                return true;
                            else
                                return false;
                        }
                    case NoteCutDirection.Down:
                        {
                            if (note2.cutDirection == NoteCutDirection.Up || note2.cutDirection == NoteCutDirection.UpLeft)
                                return true;
                            else
                                return false;
                        }
                    case NoteCutDirection.Left:
                        {
                            if (note2.cutDirection == NoteCutDirection.Right || note2.cutDirection == NoteCutDirection.DownRight)
                                return true;
                            else
                                return false;
                        }
                    case NoteCutDirection.Right:
                        {
                            if (note2.cutDirection == NoteCutDirection.Left || note2.cutDirection == NoteCutDirection.DownLeft)
                                return true;
                            else
                                return false;
                        }
                    case NoteCutDirection.UpLeft:
                        {
                            if (note2.cutDirection == NoteCutDirection.DownRight)
                                return true;
                            else
                                return false;
                        }
                    case NoteCutDirection.UpRight:
                        {
                            if (note2.cutDirection == NoteCutDirection.DownLeft)
                                return true;
                            else
                                return false;
                        }
                    case NoteCutDirection.DownLeft:
                        {
                            if (note2.cutDirection == NoteCutDirection.UpRight)
                                return true;
                            else
                                return false;
                        }
                    case NoteCutDirection.DownRight:
                        {
                            if (note2.cutDirection == NoteCutDirection.UpLeft)
                                return true;
                            else
                                return false;
                        }
                    default: // For Dot Note or any other cases
                        {
                            return false;
                        }
                }
            }
        }

        public static bool IsContrarySwingsBy135Degrees(ENoteData note1, ENoteData note2) // i changed it but original version actually returns true is direction is similar not contrary
        {
            if (note1.cutDirection == NoteCutDirection.Any || note2.cutDirection == NoteCutDirection.Any) return false;
            else
            {
                float angleDiff = note1.cutDirection.RotationAngle() - note2.cutDirection.RotationAngle();
                if (Mathf.Abs(angleDiff) < 135f)
                    return false;
                else
                {
                    //Plugin.Log.Info($"IsContraryDirectionBetweenSwings: COMPATIBLE!!! Note1: {note1.cutDirection} Note2: {note2.cutDirection} Angle Diff: {angleDiff}.");
                    return true;
                }
            }
        }
        // allows 180 degree difference and some at 135     

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

        // less restrictive version - allows 180 degree difference and more 135 degree difference options
        public static bool IsContraryLikeDirectionBetweenSwings_NOT_NEEEDED_SAME_AS_ContrarySwings135Degree(ENoteData note1, NoteData note2) // new BW version that takes into account how it feels to reverse the swing of each hand
        {
            if (note1.cutDirection == NoteCutDirection.Any || note2.cutDirection == NoteCutDirection.Any) return false;
            else
            {
                if (note1.colorType == ColorType.ColorA) // right blue notes - right favors right (and down usually)
                {
                    switch (note1.cutDirection)
                    {
                        case NoteCutDirection.Up: // added new last one
                            {
                                if (note2.cutDirection == NoteCutDirection.Down || note2.cutDirection == NoteCutDirection.DownRight || note2.cutDirection == NoteCutDirection.DownLeft)
                                    return true;
                                else
                                    return false;
                            }
                        case NoteCutDirection.Down: // added new last one
                            {
                                if (note2.cutDirection == NoteCutDirection.Up || note2.cutDirection == NoteCutDirection.UpRight || note2.cutDirection == NoteCutDirection.DownLeft)
                                    return true;
                                else
                                    return false;
                            }
                        case NoteCutDirection.Left: // added new last one
                            {
                                if (note2.cutDirection == NoteCutDirection.Right || note2.cutDirection == NoteCutDirection.DownRight || note2.cutDirection == NoteCutDirection.UpRight)
                                    return true;
                                else
                                    return false;
                            }
                        case NoteCutDirection.Right: // added new last one
                            {
                                if (note2.cutDirection == NoteCutDirection.Left || note2.cutDirection == NoteCutDirection.DownLeft || note2.cutDirection == NoteCutDirection.UpLeft)
                                    return true;
                                else
                                    return false;
                            }
                        case NoteCutDirection.UpLeft: // added new last two
                            {
                                if (note2.cutDirection == NoteCutDirection.DownRight || note2.cutDirection == NoteCutDirection.Down || note2.cutDirection == NoteCutDirection.Right)
                                    return true;
                                else
                                    return false;
                            }
                        case NoteCutDirection.UpRight:  // added new last two
                            {
                                if (note2.cutDirection == NoteCutDirection.DownLeft || note2.cutDirection == NoteCutDirection.Down || note2.cutDirection == NoteCutDirection.Left)
                                    return true;
                                else
                                    return false;
                            }
                        case NoteCutDirection.DownLeft:  // added new last two
                            {
                                if (note2.cutDirection == NoteCutDirection.UpRight || note2.cutDirection == NoteCutDirection.Up || note2.cutDirection == NoteCutDirection.Right)
                                    return true;
                                else
                                    return false;
                            }
                        case NoteCutDirection.DownRight:  // added new last two
                            {
                                if (note2.cutDirection == NoteCutDirection.UpLeft || note2.cutDirection == NoteCutDirection.Up || note2.cutDirection == NoteCutDirection.Left)
                                    return true;
                                else
                                    return false;
                            }
                        default: // For Dot Note or any other cases
                            {
                                return false;
                            }
                    }
                }
                else // left red notes - left favors left (and down usually)
                {
                    switch (note1.cutDirection)
                    {
                        case NoteCutDirection.Up: // added new last one
                            {
                                if (note2.cutDirection == NoteCutDirection.Down || note2.cutDirection == NoteCutDirection.DownLeft || note2.cutDirection == NoteCutDirection.DownRight)
                                    return true;
                                else
                                    return false;
                            }
                        case NoteCutDirection.Down:// added new last one
                            {
                                if (note2.cutDirection == NoteCutDirection.Up || note2.cutDirection == NoteCutDirection.UpLeft || note2.cutDirection == NoteCutDirection.UpRight)
                                    return true;
                                else
                                    return false;
                            }
                        case NoteCutDirection.Left:// added new last one
                            {
                                if (note2.cutDirection == NoteCutDirection.Right || note2.cutDirection == NoteCutDirection.DownRight || note2.cutDirection == NoteCutDirection.UpRight)
                                    return true;
                                else
                                    return false;
                            }
                        case NoteCutDirection.Right:// added new last one
                            {
                                if (note2.cutDirection == NoteCutDirection.Left || note2.cutDirection == NoteCutDirection.DownLeft || note2.cutDirection == NoteCutDirection.UpLeft)
                                    return true;
                                else
                                    return false;
                            }
                        case NoteCutDirection.UpLeft:// added new last two
                            {
                                if (note2.cutDirection == NoteCutDirection.DownRight || note2.cutDirection == NoteCutDirection.Down || note2.cutDirection == NoteCutDirection.Right)
                                    return true;
                                else
                                    return false;
                            }
                        case NoteCutDirection.UpRight:// added new last two
                            {
                                if (note2.cutDirection == NoteCutDirection.DownLeft || note2.cutDirection == NoteCutDirection.Down || note2.cutDirection == NoteCutDirection.Left)
                                    return true;
                                else
                                    return false;
                            }
                        case NoteCutDirection.DownLeft:// added new last two
                            {
                                if (note2.cutDirection == NoteCutDirection.UpRight || note2.cutDirection == NoteCutDirection.Up || note2.cutDirection == NoteCutDirection.Right)
                                    return true;
                                else
                                    return false;
                            }
                        case NoteCutDirection.DownRight:// added new last two
                            {
                                if (note2.cutDirection == NoteCutDirection.UpLeft || note2.cutDirection == NoteCutDirection.Up || note2.cutDirection == NoteCutDirection.Left)
                                    return true;
                                else
                                    return false;
                            }
                        default: // For Dot Note or any other cases
                            {
                                return false;
                            }
                    }
                }
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

            Plugin.Log.Info($"currentChainsCount: {currentChainsCount}, preferredChainCount: {preferredChainCount}");

            // Iteratively adjust pauseThresholdMultiplier to get closer to preferredChainCount
            while (Math.Abs(currentChainsCount - preferredChainCount) >= 5 && counter < maxIterations) // 5 is a threshold for acceptable difference
            {
                if (currentChainsCount > preferredChainCount)
                {
                    //Plugin.Log.Info($"--- Iteration: {counter} Chains Count (Too Many) : {currentChainsCount}, Threshold: {pauseThresholdMultiplier}");
                    pauseThresholdMultiplier *= 1.1f; // Increase multiplier if too many chains
                }
                else if (forceMoreChains && (currentChainsCount < preferredChainCount))//may decide not to force adding more chains
                {
                    //Plugin.Log.Info($"--- Iteration: {counter} Chains Count (Too Few) : {currentChainsCount}, Threshold: {pauseThresholdMultiplier}");
                    pauseThresholdMultiplier *= 0.9f; // Decrease multiplier if too few chains
                }

                alteredNotes.Clear();
                //doubleChainNotes.Clear();
                chainNotesA.Clear(); chainNotesB.Clear();

                if (colorA.Count > 1)
                    chainNotesA = PauseDetection(colorA);
                if (colorB.Count > 1)
                    chainNotesB = PauseDetection(colorB);

                currentChainsCount = chainNotesA.Count + chainNotesB.Count;// + doubleChainNotes.Count;

                counter++;
            }

            //AlterNotes(); // didn't like using this since it alters notes and usually makes awkward moments

            //added this. did i need this? ---------------------------------------------
            List<ENoteData> chainNotes = new List<ENoteData>();
            chainNotes.AddRange(chainNotesA); chainNotes.AddRange(chainNotesB); // these can accidentally have chains at same time so appear to be double chains but haven't been vetted as double chains.

            //RemoveSimultaneousChains(chains); // this is before we add double chains.  don't need it since i check now if there is another chain before adding it in CreateChains()

            //chains.AddRange(doubleChainNotes);

            //Plugin.Log.Info($"Double Chain Notes:");
            //foreach (ENoteData doubleNote in doubleChainNotes)
            //{
            //    Plugin.Log.Info($"    Double Chain: {doubleNote.time:F}"); 
            //}

            chainNotes.Sort((note1, note2) => note1.time.CompareTo(note2.time));


            Plugin.Log.Info($"Chains Count: {currentChainsCount} with pauseMultiplier: {pauseThresholdMultiplier} took {counter} iterations. -- pause detection"); //Double Chains Count: { doubleChainNotes.Count}

            //CreateChains(currentChainsA); CreateChains(currentChainsB);
            //---------------------------------------------------------------------------
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
                    Plugin.Log.Info($"Chains Count (Too Many) : {chainsCount}, Threshold: {threshold}, Iteration# {iterationCount}");
                    threshold *= 1.2f;// Too many chains, increase threshold
                }
                else if (forceMoreChains && ChainNotes.Count < preferredChainCount)//user can decide if wnat to force more chains
                {
                    Plugin.Log.Info($"Chains Count (Too Few): {chainsCount}, Threshold: {threshold}, Iteration# {iterationCount}");
                    threshold *= 0.8f;// Too few chains, decrease threshold
                }

                iterationCount++;
            }

            //AlterNotes();

            //chains.AddRange(doubleChainNotes);
            Plugin.Log.Info($"Double Chain Notes:");
            //foreach (ENoteData doubleNote in doubleChainNotes)
            //{
            //    Plugin.Log.Info($"---- Double Chain: {doubleNote.time:F} - cutDirection: {doubleNote.cutDirection}");
            //}
            ChainNotes.Sort((note1, note2) => note1.time.CompareTo(note2.time));

            //MoveWallsBlockingChainTail();

            Plugin.Log.Info($"Final Chains Count: {ChainNotes.Count}, Final Threshold: {threshold}, Iterations: {iterationCount} - Tempo Change Algorithm.");

            return CreateChains(ChainNotes);
        }

        //Using this
        public static List<ENoteData> PauseDetection(List<ENoteData> colorNotes)
        {
            int frequencyWindowSize = 10; //This sets the size of the window for calculating the average frequency (or rate) of notes.
            int beginningNote = 3;
            bool justDetectedPause = false;  // Flag to indicate a pause was just detected. It's used to prevent the detection of consecutive pauses

            List<ENoteData> groupBoundaries = new List<ENoteData>();

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

                if (timeDifference >= Config.Instance.ChainTimeBumper && (timeDifference > recentTimeDifferences.Average() * pauseThresholdMultiplier) && !justDetectedPause)
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


                        //Plugin.Log.Info($"Pause Between Notes: {timeDifference}----------------------------------------");

                        recentTimeDifferences.Clear();
                        recentTimeDifferences.Enqueue(timeDifference);
                        justDetectedPause = true; // Set the flag as pause detected
                    }
                }
                else
                {
                    justDetectedPause = false; // Reset the flag as we're processing normal notes
                    //Plugin.Log.Info($"Group Note: {notes[i].time:F}. Average Frequency of Notes in Group: {averageFrequency}");
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
                    //Plugin.Log.Info($"Incompatible Note for Chain: {lastNote.time:F}");
                }
            }
            //Plugin.Log.Info($"Chains {groupBoundaries[0].colorType} Count: {groupBoundaries.Count} with pauseMultipler: {pauseThresholdMultiplier}");

            return groupBoundaries;
        }

        public static List<ENoteData> IdentifyTempoChanges(List<ENoteData> notes, List<float> intervals, float threshold, bool findSingleChains)
        {
            //List<(ENoteData note, float changeMagnitude)> changePoints = new List<(ENoteData, float)>();
            List<ENoteData> tempoChangePoint = new List<ENoteData>();

            for (int i = 1; i < intervals.Count; i++)
            {
                float changeMagnitude = Math.Abs(intervals[i] - intervals[i - 1]);
                if (changeMagnitude > threshold)
                {
                    //Plugin.Log.Info($"Tempo Change Point: {notes[i].time:F} - Magnitude of change: {changeMagnitude}");

                    if (findSingleChains && IsCompatibleForChain(notes[i])) // if (IsCompatibleForChain(notes[i], notes)
                    {
                        tempoChangePoint.Add((notes[i]));

                        //Plugin.Log.Info($"Compatible tempo change point so CHAIN added.");
                    }
                    /*
                    else if (!findSingleChains && IsCompatibleForChain(notes[i], notes)) // FIX new method to test for double chains
                    {
                        tempoChangePoint.Add((notes[i]));

                        //Plugin.Log.Info($"Compatible tempo change point so CHAIN added.");
                    }
                    */

                }
            }
            //Plugin.Log.Info($"Chain Count: {tempoChangePoint.Count} - Tempo Change Algorithm");

            return tempoChangePoint;
            //CreateChains (tempoChangePoint);
        }

        private static int _lastCheckedIndex = 0;

        // Final step for both style options for making chains
        private static List<ESliderData> CreateChains(List<ENoteData> chainNotes)
        {
            //float headControlPointLengthMultiplier = 0; // was 0 on ilomilo map for burst sliders. A float which represents how far the arc goes from the head of the arc. If head direction is a dot, this does nothing.        
            //float tailControlPointLengthMultiplier = 0; // was 0 on ilomilo map for burst sliders.
            // bool hasTailNote = false; // was false on ilomilo map for burst sliders.
            //NoteCutDirection tailCutDirection = NoteCutDirection.Any; // was any on ilomilo map for burst sliders.
            //SliderMidAnchorMode sliderMidAnchorMode = SliderMidAnchorMode.Straight;// was straight on ilomilo map for burst sliders.

            List<ESliderData> chains = new List<ESliderData>();
            //TempLongChainTails = new List<ENoteData>();

            int longChainCount = 1; // use to avoid adding chains at the same or almost the same time. let Double chains do that.

            // Reset the lastCheckedIndex at the beginning of the processing
            _lastCheckedIndex = 0;

            foreach (ENoteData chainNote in chainNotes)
            {
                int sliceCount = 4;

                float tailTime = chainNote.time + .02f;

                (int headLineLayer, int headLineIndex, int tailLineLayer, int tailLineIndex) = CalculateTailPosition(chainNote);
                NoteLineLayer tailBeforeJumpLineLayer = (NoteLineLayer)tailLineLayer;

                // Set sliceCount to 6 only if the absolute difference between head and tail positions is 2
                if (Math.Abs(tailLineIndex - chainNote.line) == 2 || Math.Abs(tailLineLayer - (int)chainNote.layer) == 2)
                {
                    if (chainNote.cutDirection == NoteCutDirection.Up || chainNote.cutDirection == NoteCutDirection.Down)
                        sliceCount = 6; // up and down is easy to hit all the segments
                    else
                        sliceCount = 6; // diagonal can be hard to hit the 6th segment

                    tailTime = chainNote.time + .05f;
                }
                /*
                //chains are so long that it's hard to hit the last segments
                if (sliceCount > 4 && (note.cutDirection == NoteCutDirection.UpRight || note.cutDirection == NoteCutDirection.UpLeft || note.cutDirection == NoteCutDirection.DownLeft || note.cutDirection == NoteCutDirection.DownRight))//diagonal chains need more spacing or they look bad
                {
                    squishAmount = 1.5f;
                }
                */

                // This is a single chain being added. However, its possible there has been another single chain added to a different note at the same time by chance. This can produce illegitimate double chains that are hard for the player to hit
                // To avoid this problem, if this is an accidental double chain, then check if its compatible. If not, skip this chain.
                bool shouldAddChain = true;

                // check if there is already a chain added 
                ESliderData existingChainAtSameTime = chains.FirstOrDefault(a => Math.Abs(a.time - chainNote.time) < 0.01f);
                // or a.time == note.time but can have rounding errors

                // if there is another chain added already...
                if (existingChainAtSameTime != null)
                {
                    ESliderData matchingChain;

                    if (TryCreateDoubleChain(chainNote, existingChainAtSameTime, out matchingChain)) // Attempt to generate a matching mirrored chain
                    {
                        if (!chains.Contains(matchingChain))
                        {
                            chains.Add(matchingChain);
                            chainsTemp.Add(matchingChain); // optional: for MoveWallsBlockingChainTail()

                            //Plugin.Log.Info($"[Double Chain] Added matching chain at {matchingChain.time}");
                        }

                        // A double chain has been added — skip creating a new one
                        shouldAddChain = false;
                    }
                    else
                    {
                        // Not a good double chain match; proceed with normal single-chain creation
                        shouldAddChain = true;
                    }

                }

                if (shouldAddChain)
                {
                    #region Long Chain

                    // Add LONG CHAIN potentially
                    if (Config.Instance.EnableLongChains && chainNotes.IndexOf(chainNote) < chainNotes.Count - 1) // chainNotes.IndexOf(chainNote) % 3 == 0 && 
                    {
                        ENoteData nextChain = chainNotes[chainNotes.IndexOf(chainNote) + 1];

                        //float gapBetweenChains = nextChain.time - chainNote.time;

                        if (//gapBetweenChains >= .5f && // Check for .5 second or longer gap between chainNotes 
                            ((chainNote.layer == 0 && // make sure the note is far edge note at the very top, bottom, left or right etc
                              (chainNote.cutDirection == NoteCutDirection.Up || chainNote.cutDirection == NoteCutDirection.UpLeft || chainNote.cutDirection == NoteCutDirection.UpRight)) ||
                             (chainNote.layer == 1 &&
                              (chainNote.cutDirection == NoteCutDirection.Down || chainNote.cutDirection == NoteCutDirection.DownLeft || chainNote.cutDirection == NoteCutDirection.DownRight)) ||
                             (chainNote.line == 3 &&
                              (chainNote.cutDirection == NoteCutDirection.Left || chainNote.cutDirection == NoteCutDirection.DownLeft || chainNote.cutDirection == NoteCutDirection.UpLeft)) ||
                             (chainNote.line == 0 &&
                              (chainNote.cutDirection == NoteCutDirection.Right || chainNote.cutDirection == NoteCutDirection.DownRight || chainNote.cutDirection == NoteCutDirection.UpRight))))
                        {
                            //bool chainDuration = (chainNotes.IndexOf(chainNote) / 3) % 2 == 0; // every 3rd note, the duration alternates between 0.5 seconds and 0.75 seconds
                            //float tempTailTime = chainNote.time + (chainDuration ? 0.2f : 0.5f);

                            float minGapAfterLongChain = .2f; // need time after long chain for player to get to the next note test was .4
                            float minLongChainAllowed = .1f;
                            float maxLongChainAllowed = Config.Instance.LongChainMaxDuration; // max length allowed. super long chains don't curve, so they are not ideal

                            // originally wanted to only check for notes during and overlapping a potential chain of the same color. but after testing, any note of any color or type can block the chain so check for all notes
                            //List<ENoteData> relevantNotes = chainNote.colorType == ColorType.ColorA ? colorA : colorB;

                            float timeUntilNextNote = GetTimeUntilNextNote(notes, chainNote.time);// relevantNotes, chainNote.time);

                            if (timeUntilNextNote > minGapAfterLongChain + minLongChainAllowed)
                            {
                                float chainDuration = Math.Min(timeUntilNextNote - minGapAfterLongChain, maxLongChainAllowed);

                                bool awkwardLongChain = false; // 

                                // make sure long chains get max width between head and tail - not good for diagonal chains that are not in a corner
                                if ((chainNote.cutDirection == NoteCutDirection.Up || chainNote.cutDirection == NoteCutDirection.UpLeft || chainNote.cutDirection == NoteCutDirection.UpRight) &&
                                    chainNote.layer == 0) tailLineLayer = 2;
                                else if ((chainNote.cutDirection == NoteCutDirection.Down || chainNote.cutDirection == NoteCutDirection.DownLeft || chainNote.cutDirection == NoteCutDirection.DownRight) &&
                                         chainNote.layer == 2) tailLineLayer = 0;
                                if ((chainNote.cutDirection == NoteCutDirection.Right || chainNote.cutDirection == NoteCutDirection.DownRight || chainNote.cutDirection == NoteCutDirection.UpRight) &&
                                    chainNote.line == 0) tailLineIndex = 3;
                                else if ((chainNote.cutDirection == NoteCutDirection.Left || chainNote.cutDirection == NoteCutDirection.DownLeft || chainNote.cutDirection == NoteCutDirection.UpLeft) &&
                                         chainNote.line == 3) tailLineIndex = 0;

                                if (chainNote.cutDirection == NoteCutDirection.Down)
                                {
                                    awkwardLongChain = true;
                                }
                                else if (chainNote.cutDirection == NoteCutDirection.Up)
                                {
                                    tailLineIndex = chainNote.line;// Keep the same lineIndex

                                    if ((chainNote.colorType == ColorType.ColorA && chainNote.line > 1) || // right note (blue) are on the left which is awkward
                                        (chainNote.colorType == ColorType.ColorB && chainNote.line < 2))   // left note (red) are on the right which is awkward
                                    {
                                        awkwardLongChain = true;
                                    }
                                }
                                else if (chainNote.cutDirection == NoteCutDirection.Left || chainNote.cutDirection == NoteCutDirection.Right)
                                {
                                    tailLineLayer = (int)chainNote.layer; // Keep the same layer
                                }
                                else if (chainNote.cutDirection == NoteCutDirection.UpRight &&
                                         chainNote.line == 0 && chainNote.layer == 0)
                                {
                                    tailLineIndex = 3;
                                    tailLineLayer = (int)2;
                                    sliceCount = 8; // long diagonal
                                }
                                else if (chainNote.cutDirection == NoteCutDirection.UpLeft &&
                                         chainNote.line == 3 && chainNote.layer == 0)
                                {
                                    tailLineIndex = 0;
                                    tailLineLayer = (int)2;
                                    sliceCount = 8; // long diagonal
                                }
                                else if (chainNote.cutDirection == NoteCutDirection.DownRight &&
                                         chainNote.line == 0 && chainNote.layer == 2)
                                {
                                    tailLineIndex = 3;
                                    tailLineLayer = (int)0;
                                    sliceCount = 8; // long diagonal
                                }
                                else if (chainNote.cutDirection == NoteCutDirection.DownLeft &&
                                         chainNote.line == 3 && chainNote.layer == 2)
                                {
                                    tailLineIndex = 0;
                                    tailLineLayer = (int)0;
                                    sliceCount = 8; // long diagonal
                                }
                                else
                                {
                                    awkwardLongChain = true; // notes that arrive on their opposite side and notes that are not on the far edges of the player area (notes in the middle are awkward long chains since the movement distance is short
                                }
                                /*
                                if (((Math.Abs(chainNote.lineIndex) - Math.Abs(tailLineIndex)) +
                                     (Math.Abs((int)chainNote.layer) - Math.Abs(tailLineLayer))) > 3) // semi-long diagonal
                                {
                                    sliceCount = Math.Max(6, sliceCount);
                                }
                                */
                                if (!awkwardLongChain)
                                {
                                    tailTime = chainNote.time + chainDuration;
                                    sliceCount = Math.Max((int)(chainDuration * 17), 6); // more segments depending on the length of time with min of 6
                                    //Plugin.Log.Info($"gapBetweenChains: {gapBetweenChains}");
                                    //Plugin.Log.Info($"Long Chain {longChainCount}: {chainNote.time:F} {chainNote.colorType} dir: {chainNote.cutDirection} H index: {chainNote.line} T index: {tailLineIndex} - H layer: {(int)chainNote.layer} T layer: {tailLineLayer} Dur: {chainDuration} slices: {sliceCount}");
                                    longChainCount++;

                                    //TempLongChainTails.Add(ENoteData.Create(tailTime, ColorType.None, tailLineIndex, tailLineLayer, NoteCutDirection.Any));
                                }

                            }
                        }
                    }
                    #endregion

                    // needs to be ESliderData or will get chroma errors missing customData i think
                    var newChain = ESliderData.CreateChain(
                        chainNote.colorType, 
                        chainNote.time, headLineIndex, headLineLayer, chainNote.cutDirection,
                        tailTime, tailLineIndex,tailLineLayer,
                        sliceCount, squishAmount,
                        chainNote);

                    if (chains.Contains(newChain)) continue;

                    chains.Add(newChain);

                    chainNote.headNoteChain = newChain; chainNote.scoringType = NoteData.ScoringType.ChainHead; chainNote.gameplayType = NoteData.GameplayType.BurstSliderHead;

                    chainsTemp.Add(newChain);// used by MoveWallsBlockingChainTail()
                }

                //Plugin.Log.Info($"Chain: {note.time:F} color: {note.colorType} index: {note.lineIndex} layer: {note.layer} cut dir: {note.cutDirection}");
                //Plugin.Log.Info($"ALREADY HAVE a double chain here: {note.time:F} color: {note.colorType} other color: {matchingNoteInOtherColor.colorType} note index: {note.lineIndex} other index: {matchingNoteInOtherColor.lineIndex} note layer: {note.layer} other layer: {matchingNoteInOtherColor.layer} cut dir: {note.cutDirection} other cut dir: {matchingNoteInOtherColor.cutDirection}");

            }
            return chains;
        }
        private static float GetTimeUntilNextNote(List<ENoteData> notes, float currentTime)
        {
            /*
            foreach (var note in notes)
            {
                if (note.time > currentTime)
                {
                    return note.time - currentTime;
                }
            }
            return -1f; // Return -1 if no next note is found, indicating no further notes of the same color
            */
            for (int i = _lastCheckedIndex; i < notes.Count; i++) // this avoids sorting through all the beginning notes that were already previously checked for earlier long chains
            {
                if (notes[i].time > currentTime)
                {
                    _lastCheckedIndex = i; // Update the last checked index
                    return notes[i].time - currentTime;
                }
            }
            return -1f; // Return -1 if no next note is found, indicating no further notes of the same color
        }


        // NEW version to look for double chains or 2 chains that can be hit at the same time. i limited it to both notes up or both down or both left or both right
        private static bool IsCompatibleForChain(ENoteData note)
        {
            if (note.cutDirection == NoteCutDirection.None || note.cutDirection == NoteCutDirection.Any)
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


            //Plugin.Log.Info($"Start Index: {startIndex} Index: {index} End Index: {endIndex}");
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
                        //Plugin.Log.Info($"Potential Double chain note found: Note {note.time} {note.colorType} index: {note.lineIndex} layer: {(int)note.layer} dir: {note.cutDirection} - other color: {_notes[i].time} index: {_notes[i].lineIndex} layer: {(int)_notes[i].layer} dir: {_notes[i].cutDirection}");

                    }
                    else
                    {
                        potentialChainCollisonNote = notes[i];
                        //Plugin.Log.Info($"Potential Collision note in opposite color note found: Note {note.time} {note.colorType} index: {note.lineIndex} layer: {(int)note.layer} dir: {note.cutDirection} - other color: {_notes[i].time} index: {_notes[i].lineIndex} layer: {(int)_notes[i].layer} dir: {_notes[i].cutDirection}");
                    }

                    matchingNotesCount++;
                }

            }

            // Ensure only one matching note is found
            if (matchingNotesCount > 1) //(foundMatchingNoteInOtherColor == null || matchingNotesCount != 1)
            {
                //Plugin.Log.Info($"TOO MANY matching notes here -- count: {matchingNotesCount} - {note.time:F}");// color: {note.colorType} other color: {matchingNoteInOtherColor.colorType} note index: {note.lineIndex} other index: {matchingNoteInOtherColor.lineIndex} note layer: {note.layer} other layer: {matchingNoteInOtherColor.layer} cut dir: {note.cutDirection} other cut dir: {matchingNoteInOtherColor.cutDirection}");

                return false; // Exit if no matching note found or more than one matching note exists
            }
            //else
            //Plugin.Log.Info($"matching Notes count: {matchingNotesCount}");


            // Remaining checks...

            // FIXXXXXXXXXXXXX!!!!! if have original arcs in map without chains, chains will use empty arcsAndChains and therefore add chains to arc tails sometimes. need to use prevArcs
            // DO SAME FOR maps with only chains and no arcs!!!
            foreach (var arc in arcs)
            {
                if (arc.time == note.time || (matchingNoteInOtherColor != null && arc.time == matchingNoteInOtherColor.time))
                {
                    //Plugin.Log.Info($"Arc Head blocking chain at: {note.time:F} color: {note.colorType} ");
                    return false; // Chain at the head of an arc is not compatible
                }
            }


            // Calculate the tail position and check grid boundaries
            (int headLineLayer, int headLineIndex, int tailLineLayer, int tailLineIndex) = CalculateTailPosition(note);
            bool isTailLineIndexValid = tailLineIndex >= 0 && tailLineIndex <= 3;
            bool isTailLineLayerValid = tailLineLayer >= 0 && tailLineLayer <= 2;

            // NEW COLLISION CHECK: Verify that no interfering note is on the path from head to tail.
            // directly check if its position interferes with the chain’s path.
            if (potentialChainCollisonNote != null)
            {
                if (IsPointOnLineSegment(
                        potentialChainCollisonNote.line, (int)potentialChainCollisonNote.layer,
                        headLineIndex, headLineLayer,
                        tailLineIndex, tailLineLayer))
                {
                    Plugin.Log.Info($"Chain collision: interfering note found at time {potentialChainCollisonNote.time} at position ({potentialChainCollisonNote.line}, {(int)potentialChainCollisonNote.layer}) blocking chain from {note.time}");
                    return false;
                }
            }

            /*
            if (matchingNoteInOtherColor != null && isTailLineIndexValid && isTailLineLayerValid)
            {
                //Plugin.Log.Info($"Can have double chain here: {note.time:F} color: {note.colorType} other color: {matchingNoteInOtherColor.colorType} note index: {note.lineIndex} other index: {matchingNoteInOtherColor.lineIndex} note layer: {note.layer} other layer: {matchingNoteInOtherColor.layer} cut dir: {note.cutDirection} other cut dir: {matchingNoteInOtherColor.cutDirection}");

                bool existsAlready = doubleChainNotes.Any(doubleNote =>
                    doubleNote.colorType == matchingNoteInOtherColor.colorType &&
                    doubleNote.time == matchingNoteInOtherColor.time);

                if (!existsAlready)
                {
                    doubleChainNotes.Add(matchingNoteInOtherColor);
                    Plugin.Log.Info($"ADDED double chain here: {note.time} dir: {note.cutDirection}  i: {note.lineIndex} l: {(int)note.layer} -- other dir: {matchingNoteInOtherColor.cutDirection} i: {matchingNoteInOtherColor.lineIndex} l: {(int)matchingNoteInOtherColor.layer}");
                }
                //else
                    //Plugin.Log.Info($"ALREADY HAVE a double chain here: {note.time:F} color: {note.colorType} other color: {matchingNoteInOtherColor.colorType} note index: {note.lineIndex} other index: {matchingNoteInOtherColor.lineIndex} note layer: {note.layer} other layer: {matchingNoteInOtherColor.layer} cut dir: {note.cutDirection} other cut dir: {matchingNoteInOtherColor.cutDirection}");

            }
            */
            //else if (matchingNoteInOtherColor != null)
            //Plugin.Log.Info($"No Double Note here: {note.time:F} since tail out of bounds -- tailLineIndex: {tailLineIndex} tailLineLayer: {tailLineLayer}");


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
        private static bool TryCreateDoubleChain(ENoteData note, ESliderData existingChain, out ESliderData matchingChain)
        {
            int matchingTailIndex = -1;
            int matchingTailLayer = -1;

            // Check if note and existingChain head are compatible.
            // For vertical and horizontal, we already have rules; now we add diagonal.
            if (
                   // Vertical: Up/Down must match; different column but same layer.
                   (
                      (note.cutDirection == NoteCutDirection.Up || note.cutDirection == NoteCutDirection.Down) &&
                      (existingChain.cutDirection == NoteCutDirection.Up || existingChain.cutDirection == NoteCutDirection.Down) &&
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
                        IsValidPositionalPair(note)
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
                //-----------------------------------------

                // Create the new chain using the note as the head and the computed (mirrored) tail.
                // (Here we use note.layer for headBeforeJumpLineLayer; adjust as needed.)
                //v1.34
                /*
                ESliderData mirroredChain = (ESliderData)ESliderData.CreateCustomBurstSliderData(
                    note.colorType,note.time,note.lineIndex,note.layer,note.layer, // headBeforeJumpLineLayernote.cutDirection,
                    existingChain.tailTime, // Using same tail time for now; you might adjust it if the chain shortens.
                    matchingTailIndex,
                    (layer)matchingTailLayer,(layer)matchingTailLayer, // tailBeforeJumpLineLayer: using same as tail layer.
                    note.cutDirection, // Tail cut direction – mirror if needed.
                    existingChain.sliceCount,existingChain.squishAmount,new CustomData()
                );
                */
                //v1.39

                float headBeat = note.time * TransitionPatcher.bpm / 60f;

                var mirroredChain = ESliderData.CreateChain(
                    note.colorType, note.time, note.line, note.layer, note.cutDirection,
                    existingChain.tailTime, matchingTailIndex, matchingTailLayer,
                    existingChain.sliceCount, existingChain.squishAmount, note
                );

                note.headNoteChain = mirroredChain; note.scoringType = NoteData.ScoringType.ChainHead; note.gameplayType = NoteData.GameplayType.BurstSliderHead;

                doubleChainCount++;

                Plugin.Log.Info($" {doubleChainCount} Double chain at {mirroredChain.time}");
                Plugin.Log.Info($" --- {existingChain.colorType} dir: {existingChain.cutDirection} index: {existingChain.line} layer: {(int)existingChain.layer} -- tail -- index: {existingChain.tailLine} layer: {existingChain.layer} - time: {existingChain.tailTime}");
                Plugin.Log.Info($" --- {mirroredChain.colorType} dir: {mirroredChain.cutDirection} index: {mirroredChain.line} layer: {(int)mirroredChain.layer} -- tail -- index: {mirroredChain.tailLine} layer: {mirroredChain.layer} - time: {mirroredChain.tailTime}");

                matchingChain = mirroredChain;
                return true;
            }

            matchingChain = null;
            return false;

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

                if ((d1 == NoteCutDirection.UpRight && d2 == NoteCutDirection.DownLeft) ||
                    (d1 == NoteCutDirection.DownLeft && d2 == NoteCutDirection.UpRight) ||
                    (d1 == NoteCutDirection.DownRight && d2 == NoteCutDirection.UpLeft) ||
                    (d1 == NoteCutDirection.UpLeft && d2 == NoteCutDirection.DownRight))
                {
                    return true;
                }
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

        }






        // up/down and left/right only
        // checks if 2 notes of placed in the grid so that tails could exist
        /*
        private static bool TryCreateDoubleChain(ENoteData note, SliderData existingChain, out ESliderData matchingChain)
        {
            int matchingTailIndex = -1;
            int matchingTailLayer = -1;

            // Check if note and existingChain head are compatible
            if (
                (
                    // Vertical directions: Up/Down must match and be on different columns, same layer
                    (note.cutDirection == NoteCutDirection.Up || note.cutDirection == NoteCutDirection.Down) &&
                    (existingChain.headCutDirection == NoteCutDirection.Up || existingChain.headCutDirection == NoteCutDirection.Down) &&
                    note.line != existingChain.headLineIndex &&
                    note.layer == existingChain.headLineLayer
                )
                ||
                (
                    // Horizontal directions: Left/Right must match and be on different layers, same column
                    (note.cutDirection == NoteCutDirection.Left || note.cutDirection == NoteCutDirection.Right) &&
                    (existingChain.headCutDirection == NoteCutDirection.Left || existingChain.headCutDirection == NoteCutDirection.Right) &&
                    note.layer != existingChain.headLineLayer &&
                    note.line == existingChain.headLineIndex
                )
            )
            {
                if (note.cutDirection == NoteCutDirection.Up || note.cutDirection == NoteCutDirection.Down)
                {
                    matchingTailIndex = note.lineIndex;
                    matchingTailLayer = (int)existingChain.tailLineLayer;
                }
                if (note.cutDirection == NoteCutDirection.Left || note.cutDirection == NoteCutDirection.Right)
                {
                    matchingTailIndex = existingChain.tailLineIndex;
                    matchingTailLayer = (int)note.layer;
                }



                // Create the new chain using the note as head and mirrored tail
                ESliderData mirroredChain = (ESliderData)ESliderData.CreateCustomBurstSliderData(
                    note.colorType,
                    note.time,
                    note.lineIndex,
                    note.layer,
                    note.layer,//??
                    note.cutDirection,
                    existingChain.tailTime,
                    matchingTailIndex,
                    (layer)matchingTailLayer,
                    (layer)matchingTailLayer,
                    note.cutDirection,
                    existingChain.sliceCount,
                    existingChain.squishAmount,
                    new CustomData()
                );
                doubleChainCount++;

                Plugin.Log.Info($" {doubleChainCount} Double chain at {mirroredChain.time}");
                Plugin.Log.Info($" --- {existingChain.colorType} dir: {existingChain.headCutDirection} index: {existingChain.headLineIndex} layer: {(int)existingChain.headLineLayer} -- tail -- index: {existingChain.tailLineIndex} layer: {(int)existingChain.tailLineLayer} - time: {existingChain.tailTime}");

                Plugin.Log.Info($" --- {mirroredChain.colorType} dir: {mirroredChain.headCutDirection} index: {mirroredChain.headLineIndex} layer: {(int)mirroredChain.headLineLayer} -- tail -- index: {mirroredChain.tailLineIndex} layer: {(int)mirroredChain.tailLineLayer} - time: {mirroredChain.tailTime}");

                matchingChain = mirroredChain;
                return true;
            }

            matchingChain = null;
            return false;
        }
        */



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
                //Plugin.Log.Info($"Double Chain at: {note.time:F} note1 cutDir: {note.cutDirection} note2 cutDir: {matchingNote.cutDirection} note1 lineLayer: {(int)note.layer} note2 lineLayer: {(int)matchingNote.layer}  note1 lineIndex: {note.lineIndex} note2 lineIndex: {matchingNote.lineIndex}");
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
                    Plugin.Log.Info($"Chain head moved! time: {customNote.time:F} Old lineIndex: {oldLineIndex} Old lineLayer: {oldLineLayer} -- New lineIndex: {customNote.line} New lineLayer: {(int)customNote.layer}");

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

            bool rotationModeLate = Config.Instance.RotationModeLate;

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
                //Plugin.Log.Info($"[ArcFix] Inserting 0-delta marker at {t:F3}s ({why}).");
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

            //Plugin.Log.Info($"[ArcFix] Start: arcs={arcs.Count}, groups={arcGroups.Count}, rotBefore={rotations.Count}, " +
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
                            //    Plugin.Log.Info($"[ArcFix][HeadClamp] t={rotations[idx].time:F3}s +{orig} → +{clamped} (D={D}, upper={upper})");
                            newVal = clamped;
                        }
                        else // orig < 0
                        {
                            int clamped = Math.Max(orig, Math.Min(0, lower));
                            //if (clamped != orig)
                            //    Plugin.Log.Info($"[ArcFix][HeadClamp] t={rotations[idx].time:F3}s {orig} → {clamped} (D={D}, lower={lower})");
                            newVal = clamped;
                        }

                        adjusted[idx] = newVal;
                        D += newVal; // now D reflects the AFTER-head state delta
                    }

                    // Anchor baseline to AFTER-head state for math inside the covered window
                    baseCum += D;
                }

                //Plugin.Log.Info($"[ArcFix][Group] Window {gStart:F3}s → {gEnd:F3}s, baseCum={baseCum}, boundaries={B.Count}, mode={(rotationModeLate ? "Late" : "Early")}");

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
                        //Plugin.Log.Info($"[ArcFix][Seg] {segStart:F3}s → {segEnd:F3}s, covered={covered}, events={segIdxs.Count}, includeStart=False");

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
                                //    Plugin.Log.Info($"[ArcFix][Clamp] t={rotations[idx].time:F3}s +{orig} → +{clamped} (D={D}, upper={upper})");
                                newVal = clamped;
                            }
                            else // orig < 0
                            {
                                int clamped = Math.Max(orig, Math.Min(0, lower));
                                //if (clamped != orig)
                                //    Plugin.Log.Info($"[ArcFix][Clamp] t={rotations[idx].time:F3}s {orig} → {clamped} (D={D}, lower={lower})");
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
                                //Plugin.Log.Info($"[ArcFix][Zero] Neutralizing segSum={segSum} by shaving from end.");

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
                                            //Plugin.Log.Info($"[ArcFix][Zero]  t={rotations[idx].time:F3}s +{val} → +{newVal} (took {dec})");
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
                                            //Plugin.Log.Info($"[ArcFix][Zero]  t={rotations[idx].time:F3}s {val} → {newVal} (gave {inc})");
                                        }
                                    }
                                    D -= segSum; // segSum is negative; restores D to seg start
                                }
                            }
                        }
                    }
                    else
                    {
                        //Plugin.Log.Info($"[ArcFix][Seg] {segStart:F3}s → {segEnd:F3}s, covered={covered}, events=0 (nothing to adjust).");
                    }

                    segStart = segEnd;
                }
            }





            // this removes almost all rotations during arcs esp if there is only 1 rotation during an arc.
            // ---------------- PER-ARC NET-ZERO ENFORCEMENT ----------------
            // Ensures every individual arc has head==tail accum per engine semantics.
            if (Config.Instance.ArcRotationMode == Config.ArcRotationModeType.NetZero)
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
                        Plugin.Log.Info($"[ArcFix][ArcZero][SKIP] no events in {(rotationModeLate ? "(head,tail]" : "[head,tail]")} @ {head:F3}->{tail:F3} (idx {startIdx}..{endIdx})");
                        continue;
                    }

                    int arcSum = SumRange(startIdx, endIdx);

                    // Cross-check: what does sanity compare?
                    int sanHead = rotationModeLate ? AccumBefore(head) : AccumInclusive(head);
                    int sanTail = rotationModeLate ? AccumBefore(tail) : AccumInclusive(tail);
                    int sanDiff = sanTail - sanHead; // should equal arcSum if indices are correct

                    string intervalLabel = rotationModeLate ? "[head-TOL,tail-TOL)" : "[head,tail]";
                    Plugin.Log.Info(
                        $"[ArcFix][ArcZero][CHK] {intervalLabel} @ {head:F3}->{tail:F3}  "
                      + $"idx {startIdx}..{endIdx}  arcSum={arcSum}  sanityDiff={sanDiff}  "
                      + $"events={Math.Max(0, endIdx - startIdx + 1)}"
                    );

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
                            Plugin.Log.Info($"[ArcFix][ArcZero][WARN] could not align indices for {head:F3}->{tail:F3}");
                            continue;
                        }

                        arcSum = SumRange(startIdx, endIdx);
                        sanDiff = (rotationModeLate ? AccumBefore(tail) - AccumBefore(head)
                                                    : AccumInclusive(tail) - AccumInclusive(head));
                        Plugin.Log.Info($"[ArcFix][ArcZero][CHK2] idx {startIdx}..{endIdx}  arcSum={arcSum}  sanityDiff={sanDiff} (post-align)");
                        if (arcSum == 0) continue;
                    }

                    // Neutralize by shaving from the end toward zero
                    Plugin.Log.Info($"[ArcFix][ArcZero] Neutralizing arcSum={arcSum} over {(rotationModeLate ? "(head,tail]" : "[head,tail]")} @ {head:F3}->{tail:F3}");

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
                                Plugin.Log.Info($"[ArcFix][ArcZero]  t={times[j]:F3}s +{val} → +{newVal} (took {take})");
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
                                Plugin.Log.Info($"[ArcFix][ArcZero]  t={times[j]:F3}s {val} → {newVal} (gave {give})");
                            }
                        }
                    }
                    // (No D tracking here; this is a local fine-tune.)
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

                if (val == 0 && !isBoundary)
                {
                    removed++;
                    // drop interior zero
                }
                else if (val == r.rotation)
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

            result = result.OrderBy(r => r.time).ToList();

            // ---------------- FINAL: RECOMPUTE accumRotation ----------------
            result = ERotationEventData.RecalculateAccumulatedRotations(result);

            Plugin.Log.Info($"[ArcFix] Done: Rotation Final Count={result.Count} (reduced={reduced}, removed={removed}, unchanged={unchanged}). " +
                            $"ArcRotationMode={Config.Instance.ArcRotationMode}, allowedCumul={allowedCumulativeRots}, mode={(rotationModeLate ? "Late" : "Early")}");

            // Engine-accurate sanity check: show only assumed accumRotation at arc HEAD and TAIL.
            SanityCheckHeadsTails_EngineView(result, arcs, TOL, rotationModeLate);

            return result;
        }

        // ---- Engine-accurate sanity: print ONLY accumRotation seen at arc HEAD and TAIL ----
        // Early: objects at boundary see INCLUSIVE (post-boundary) state
        // Late:  objects at boundary see EXCLUSIVE (pre-boundary) state
        private static void SanityCheckHeadsTails_EngineView(List<ERotationEventData> rotations, List<ESliderData> arcs, float TOL, bool rotationModeLate)
        {
            int AccumInclusive(float t)
            {
                int s = 0;
                foreach (var r in rotations)
                {
                    if (r.time <= t + TOL) s += r.rotation;
                    else break;
                }
                return s;
            }
            int AccumBefore(float t)
            {
                int s = 0;
                foreach (var r in rotations)
                {
                    if (r.time < t - TOL) s += r.rotation;
                    else break;
                }
                return s;
            }

            foreach (var a in arcs)
            {
                int head, tail;
                string mismatch = "";
                if (!rotationModeLate)
                {
                    head = AccumInclusive(a.time);
                    tail = AccumInclusive(a.tailTime);
                    mismatch = (head != tail) ? "<<< MISMATCH!" : "";

                    //Plugin.Log.Info($"[ArcFix][Check] EARLY head={head} tail={tail} @ {a.time:F3}->{a.tailTime:F3} {mismatch}");
                }
                else
                {
                    head = AccumBefore(a.time);
                    tail = AccumBefore(a.tailTime);
                    mismatch = (head != tail) ? "<<< MISMATCH!" : "";

                    //Plugin.Log.Info($"[ArcFix][Check] LATE head={head} tail={tail} @ {a.time:F3}->{a.tailTime:F3} {mismatch}");
                }
            }
        }











        /*
        //works great! latest version 9/9/2025 but forceZeroCumulativeRots = false is sending out rotated tails sometimes.
        public static List<ERotationEventData> ArcFix(
            List<ERotationEventData> rotations,
            EditableCBD eData)
        {
            // ---------------- CONFIG & SETUP ----------------
            const float TOL = 0.0005f;

            bool rotationModeLate = Config.Instance.RotationModeLate;

            bool forceZeroCumulativeRots = false; // strict: no deviation inside covered segments
            int allowedCumulativeRots = forceZeroCumulativeRots ? 0 : 15;

            if (rotations == null || rotations.Count < 2) return rotations;

            if (eData?.Arcs == null || eData.Arcs.Count == 0) return rotations;

            rotations = rotations.OrderBy(r => r.time).ToList();
            var arcs = eData.Arcs.OrderBy(a => a.time).ToList();

            // ---------------- ADD MISSING HEAD/TAIL MARKERS ----------------
            int addedHeads = 0, addedTails = 0;
            bool HasEventAt(float t) => rotations.Any(r => Math.Abs(r.time - t) <= TOL);

            void AddZeroEventAt(float t, string why)
            {
                Plugin.Log.Info($"[ArcFix] Inserting 0-delta marker at {t:F3}s ({why}).");
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

            Plugin.Log.Info($"[ArcFix] Start: arcs={arcs.Count}, groups={arcGroups.Count}, rotBefore={rotations.Count}, " +
                            $"addedHeads={addedHeads}, addedTails={addedTails}, allowedCumulativeRots={allowedCumulativeRots}, " +
                            $"mode={(rotationModeLate ? "Late" : "Early")}");

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
            // Late   => [start, end] if 'start' is a head; otherwise (start, end]
            bool IsGroupHead(float t, List<ESliderData> group, float tol)
                => group.Any(a => Math.Abs(a.time - t) <= tol);

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

            foreach (var group in arcGroups)
            {
                float gStart = group.Min(a => a.time);
                float gEnd = group.Max(a => a.tailTime);

                // Baseline at head (depends on Early vs Late)
                int baseCum = rotationModeLate
                    ? SumUpTo(gStart - 1e-6f) // exclude events at the head time
                    : SumUpTo(gStart);        // include events at the head time

                // Boundaries = union of heads/tails in group
                var boundarySet = new SortedSet<float>();
                foreach (var a in group) { boundarySet.Add(a.time); boundarySet.Add(a.tailTime); }
                var B = boundarySet.OrderBy(x => x).ToList();
                if (B.Count < 2) continue;

                Plugin.Log.Info($"[ArcFix][Group] Window {gStart:F3}s → {gEnd:F3}s, baseCum={baseCum}, boundaries={B.Count}, mode={(rotationModeLate ? "Late" : "Early")}");

                int D = 0; // running deviation from baseCum within the group

                float segStart = B[0];
                for (int k = 1; k < B.Count; k++)
                {
                    float segEnd = B[k];

                    // Coverage test (head-inclusion differs by mode):
                    bool covered = false;
                    foreach (var a in group)
                    {
                        bool headOK = rotationModeLate
                            ? (a.time <= segEnd + TOL)   // head-inclusive for Late
                            : (a.time < segEnd - TOL);  // head-excluded for Early
                        if (headOK && a.tailTime >= segEnd - TOL) { covered = true; break; }
                    }

                    bool includeStart = rotationModeLate && IsGroupHead(segStart, group, TOL);
                    var segIdxs = EventIdxsIn(segStart, segEnd, includeStart);

                    if (segIdxs.Count > 0)
                    {
                        Plugin.Log.Info($"[ArcFix][Seg] {segStart:F3}s → {segEnd:F3}s, covered={covered}, events={segIdxs.Count}, includeStart={includeStart}");

                        // ---- 1) Clamp pass: keep |baseCum + D + delta| <= maxDeviation ----
                        foreach (int idx in segIdxs)
                        {
                            int orig = DeltaAt(idx);
                            if (orig == 0) continue;

                            int lower = -allowedCumulativeRots - D;
                            int upper =  allowedCumulativeRots - D;

                            int newVal = orig;
                            if (orig > 0)
                            {
                                int clamped = Math.Min(orig, Math.Max(0, upper));
                                if (clamped != orig)
                                    Plugin.Log.Info($"[ArcFix][Clamp] t={rotations[idx].time:F3}s +{orig} → +{clamped} (D={D}, upper={upper})");
                                newVal = clamped;
                            }
                            else // orig < 0
                            {
                                int clamped = Math.Max(orig, Math.Min(0, lower));
                                if (clamped != orig)
                                    Plugin.Log.Info($"[ArcFix][Clamp] t={rotations[idx].time:F3}s {orig} → {clamped} (D={D}, lower={lower})");
                                newVal = clamped;
                            }

                            adjusted[idx] = newVal;
                            D += newVal;
                        }

                        // ---- 2) Net-zero covered segments so head == tail accum ----
                        // This guarantees tails match their corresponding heads inside the overlap group.
                        if (covered)
                        {
                            int segSum = 0;
                            foreach (int idx in segIdxs) segSum += DeltaAt(idx);

                            if (segSum != 0)
                            {
                                Plugin.Log.Info($"[ArcFix][Zero] Neutralizing segSum={segSum} by shaving from end.");

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
                                            Plugin.Log.Info($"[ArcFix][Zero]  t={rotations[idx].time:F3}s +{val} → +{newVal} (took {dec})");
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
                                            Plugin.Log.Info($"[ArcFix][Zero]  t={rotations[idx].time:F3}s {val} → {newVal} (gave {inc})");
                                        }
                                    }
                                    D -= segSum; // segSum is negative; restores D to seg start
                                }
                            }
                            else
                            {
                                Plugin.Log.Info($"[ArcFix][Zero] segSum already 0.");
                            }
                        }
                    }
                    else
                    {
                        Plugin.Log.Info($"[ArcFix][Seg] {segStart:F3}s → {segEnd:F3}s, covered={covered}, events=0 (nothing to adjust).");
                    }

                    segStart = segEnd;
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

                if (val == 0 && !isBoundary)
                {
                    removed++;
                    // drop interior zero
                }
                else if (val == r.rotation)
                {
                    unchanged++;
                    result.Add(r);
                }
                else
                {
                    reduced++;
                    result.Add(ERotationEventData.Create(r.time, val, r.customData));
                }
            }

            result = result.OrderBy(r => r.time).ToList();

            // ---------------- FINAL: RECOMPUTE accumRotation ----------------
            result = ERotationEventData.RecalculateAccumulatedRotations(result);

            Plugin.Log.Info($"[ArcFix] Done: rotAfter={result.Count} (reduced={reduced}, removed={removed}, unchanged={unchanged}). " +
                            $"forceZero={forceZeroCumulativeRots}, allowedCumul={allowedCumulativeRots}, mode={(rotationModeLate ? "Late" : "Early")}");

            // Optional sanity check (verifies every arc tail matches its head accum)
            SanityCheckHeadsTails(result, arcs, TOL, rotationModeLate);

            return result;
        }

        // ---- optional sanity log: verify every arc tail matches its head accum ----
        private static void SanityCheckHeadsTails(List<ERotationEventData> rotations, List<ESliderData> arcs, float TOL, bool rotationModeLate)
        {
            int AccumAtInclusive(float t)
            {
                int s = 0;
                foreach (var r in rotations)
                {
                    if (r.time <= t + TOL) s += r.rotation;
                    else break;
                }
                return s;
            }

            int AccumBefore(float t) // strictly before t
            {
                int s = 0;
                foreach (var r in rotations)
                {
                    if (r.time < t - TOL) s += r.rotation;
                    else break;
                }
                return s;
            }

            foreach (var a in arcs)
            {
                // For Early: head and tail equality uses inclusive times.
                // For Late: objects at exactly head/tail use the *previous* state, but our neutrality
                // still ensures accum RIGHT AFTER tail == accum RIGHT AFTER head. Objects AT the
                // boundary use AccumBefore(boundary).
                if (!rotationModeLate)
                {
                    int head = AccumAtInclusive(a.time);
                    int tail = AccumAtInclusive(a.tailTime);
                    if (head != tail)
                        Plugin.Log.Info($"[ArcFix][Check] EARLY MISMATCH {a.time:F3}->{a.tailTime:F3}: head={head} tail={tail}");
                    //else
                    //    Plugin.Log.Info($"[ArcFix][Check] EARLY OK {a.time:F3}->{a.tailTime:F3}: accum={head}");
                }
                else
                {
                    // Late snapping: for objects AT boundaries, engine will use AccumBefore(boundary).
                    // Our pass guarantees accum AFTER tail equals accum AFTER head (inclusive),
                    // and objects at boundaries won't consume those boundary events.
                    int headAfter = AccumAtInclusive(a.time);
                    int tailAfter = AccumAtInclusive(a.tailTime);
                    if (headAfter != tailAfter)
                        Plugin.Log.Info($"[ArcFix][Check] LATE MISMATCH {a.time:F3}->{a.tailTime:F3}: afterHead={headAfter} afterTail={tailAfter}");
                    //else
                    //   Plugin.Log.Info($"[ArcFix][Check] LATE OK {a.time:F3}->{a.tailTime:F3}: accumAfter={headAfter}");

                    // (Optional) also show what objects AT boundaries would see:
                    int headSeen = AccumBefore(a.time);
                    int tailSeen = AccumBefore(a.tailTime);
                    Plugin.Log.Info($"[ArcFix][Check] LATE boundary-seen: atHead sees {headSeen}, atTail sees {tailSeen}");
                }
            }
        }
        */











        /*
        // Working ArcFix() but doesn't correct large rotation jumps for tails set to match head rotation which occurs in ApplyPerObjectRotations(). this doesn't set tail rotation to match head rotation at all. decided it would be better to do that here so tring new technique
        public static List<ERotationEventData> ArcFix(
            List<ERotationEventData> rotations,
            EditableCBD eData) 
        {
            const float TOL = 0.001f;

            bool forceZeroCumulativeRots = false; // allow or not allow 0 cumulative rotations during an arc. when false will allow -15 to 15. (but later in ApplyPerObjectRotations() i set the tail rotation to match head rotation anyway but this does allow a few more rotations)
            int  allowedCumulativeRots = 15; // 30 seems too extreme when played

            // Early exit if no rotations
            if (rotations.Count == 0)
                return rotations;

            // Sort inputs
            rotations = rotations.OrderBy(r => r.time).ToList();
            var arcs = eData.Arcs.OrderBy(a => a.time).ToList();

            // Log start
            Plugin.Log.Info($"ArcFix() Begins with {arcs.Count} arcs and {rotations.Count} rotations to study. Force zeroCumulativeRots during an arc: {forceZeroCumulativeRots}. If false will allow +-15");

            // 1. Group overlapping arcs into contiguous intervals
            var arcGroups = new List<List<ESliderData>>();
            var currentGroup = new List<ESliderData>();
            float currentMaxTail = 0f;
            foreach (var arc in arcs)
            {
                if (currentGroup.Count == 0)
                {
                    currentGroup.Add(arc);
                    currentMaxTail = arc.tailTime;
                }
                else if (arc.time <= currentMaxTail + TOL)
                {
                    currentGroup.Add(arc);
                    currentMaxTail = Math.Max(currentMaxTail, arc.tailTime);
                }
                else
                {
                    arcGroups.Add(new List<ESliderData>(currentGroup));
                    currentGroup.Clear();
                    currentGroup.Add(arc);
                    currentMaxTail = arc.tailTime;
                }
            }
            if (currentGroup.Count > 0)
                arcGroups.Add(currentGroup);

            Plugin.Log.Info($"ArcFix() Identified {arcGroups.Count} slider group(s) from {arcs.Count} arc(s) (each group is a single slider or an overlapping set of sliders).");

            // Lists to accumulate
            var allowedRotations = new List<ERotationEventData>();
            var allSliderRotations = new List<ERotationEventData>();

            // 2. For each merged arc group...
            foreach (var group in arcGroups)
            {
                // Define the interval from earliest head to latest tail
                float groupStart = group.Min(s => s.time);
                float groupEnd = group.Max(s => s.tailTime);

                // A) Always keep the head rotation(s)
                var headEvents = rotations
                    .Where(r => Math.Abs(r.time - groupStart) <= TOL)
                    .OrderBy(r => r.time)
                    .ToList();
                allowedRotations.AddRange(headEvents);

                // B) Collect all rotations that fall within the arc window
                var groupRotations = rotations
                    .Where(r => r.time >= groupStart - TOL && r.time <= groupEnd + TOL)
                    .OrderBy(r => r.time)
                    .ToList();
                allSliderRotations.AddRange(groupRotations);

                // C) Compute cumulative rotation up to and including the head
                int initialCum = rotations
                    .Where(r => r.time <= groupStart + TOL)
                    .Sum(r => r.rotation);

                // D) Only consider interior rotations (strictly between head and including the tail)
                var interior = rotations
                    .Where(r => r.time > groupStart + TOL && r.time <= groupEnd + TOL)
                    .OrderBy(r => r.time)
                    .ToList();


                // Balance interior events back to the head's cumulative
                var keptInterior = ProcessSegment(interior, initialCum);
                allowedRotations.AddRange(keptInterior);
            }

            // Helper: keeps only those runs of rotations that return to the initialCum
            List<ERotationEventData> ProcessSegment(
                List<ERotationEventData> segRotations,
                int initialCum)
            {
                var allowedSegment = new List<ERotationEventData>();
                var currentRun = new List<ERotationEventData>();
                float cumulative = initialCum;

                foreach (var rot in segRotations)
                {
                    currentRun.Add(rot);
                    cumulative += rot.rotation;
                    //Plugin.Log.Info($" --- ProcessSegment: Adding rotation at time: {rot.time:F2} rotation: {rot.rotation} -> cumulative: {cumulative}");

                    // If we exceed ±30°, drop the oldest in this run
                    while (Math.Abs(cumulative - initialCum) > 15f && currentRun.Count > 1)
                    {
                        var removed = currentRun[0];
                        cumulative -= removed.rotation;
                        //Plugin.Log.Info($" --- ProcessSegment: Removing rotation at time: {removed.time:F2} rotation: {removed.rotation} to fit within limit -> new cumulative: {cumulative}");
                        currentRun.RemoveAt(0);
                    }

                    if (forceZeroCumulativeRots)
                    {
                        // If we've returned exactly to the head's cumulative, keep this run
                        if (Math.Abs(cumulative - initialCum) < 0.0001f)
                        {
                            //Plugin.Log.Info($" --- ProcessSegment: Cumulative returned to initial ({initialCum}). Finalizing group of {currentRun.Count} rotation(s).");
                            allowedSegment.AddRange(currentRun);
                            currentRun.Clear();
                            cumulative = initialCum;
                        }
                    }
                }

                if (!forceZeroCumulativeRots)
                {
                    // If cumulative at the end is within ±30 of the head, allow the whole group
                    if (currentRun.Count > 0 && Math.Abs(cumulative - initialCum) <= allowedCumulativeRots)
                    {
                        //Plugin.Log.Info($" --- ProcessSegment: Allowed unbalanced group with cumulative {cumulative} (initial {initialCum}).");
                        allowedSegment.AddRange(currentRun);
                        currentRun.Clear();
                    }
                    // Else: leftovers outside ±30 discarded (optional: log here)
                }
                else
                {
                    if (currentRun.Count > 0)
                    {
                        //Plugin.Log.Info($" --- ProcessSegment: Discarding leftover group with cumulative {cumulative} (initial {initialCum}).");
                        currentRun.Clear();
                    }
                }

                return allowedSegment;
            }

            // 3. Remove any rotations inside arcs that weren't allowed
            allSliderRotations = allSliderRotations
                .Distinct()
                .OrderBy(r => r.time)
                .ToList();
            allowedRotations = allowedRotations
                .Distinct()
                .OrderBy(r => r.time)
                .ToList();

            var rotationsToRemove = allSliderRotations
                .Where(r => !allowedRotations.Contains(r))
                .ToList();

            rotations = rotations
                .Where(r => !rotationsToRemove.Contains(r))
                .OrderBy(r => r.time)
                .ToList();

            // Final log
            Plugin.Log.Info($"ArcFix() After Finish there are {rotations.Count} rotation(s) (removed {rotationsToRemove.Count} rotation(s)).");

            return rotations;
        }


        */
    }
}