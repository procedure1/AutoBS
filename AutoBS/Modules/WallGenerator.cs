using AutoBS.Patches;
using AutoBS.UI;
using BeatSaberMarkupLanguage.Animations.APNG.Chunks;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using UnityEngine;
using static System.Windows.Forms.LinkLabel;
//using static HMUI.IconSegmentedControl;//v1.34

namespace AutoBS
{
    internal class WallGenerator
    {
        /// <summary>
        /// Amount of time in seconds to cut of the front of a wall when rotating towards it.
        /// Player will see the front tip of a wall crossing their vision after a rotation
        /// </summary>
        public static float WallFrontCut { get; set; } = .2f;//.2  .5 worked well for me on creep, unraeval .7 still full wall crossing vision at 1.30
        /// <summary>
        /// Amount of time in seconds to cut of the back of a wall when rotating towards it.
        /// Player will see the back tip of a wall crossing their vision after a rotation
        /// </summary>
        public static float WallBackCut { get; set; } = .45f;//.45  .5 worked well for me on creep, unraval .7 still full wall crossing vision at 1.30
        /// <summary>
        /// The minimum duration of a wall before it gets discarded
        /// </summary>
        public static float minWallDuration { get; set; } = 0.001f;//BW try shorter duration walls because i like the cool short walls that some authors use default: 0.1f;
        public static float minDistanceBetweenNotesAndWalls = .2f;

        public static float lastTunnelWallTime; // Initialize this before your loop starts, used to prevent tunnel walls from overlapping
        public static float lastWindowPaneWallTime; // Initialize this before your loop starts, used to prevent tunnel walls from overlapping
        public static bool tunnelWallsHappening; // used to avoid overlapping tunnels with other walls. important since tunnels occur over periods of time whereas most wall groups are at a single moment
        public static bool paneWallsHappening;  // used to avoid overlapping window pans with other walls.

        public static bool gridWallWide = true;

        public static int distantCount; public static int columnCount; public static int rowCount; public static int tunnelCount; public static int gridCount; public static int paneCount;

        public static int ToggleCityScape = 0; // toggle between tiles and cityscape
        public static int ToggleSpires = 0; // tall and thin
        public static int ToggleSmall = 0; // smaller tiles
        public static int windowPaneTallToggle = 0; // smaller tiles
        public static int lastHeight = 0;

        private static int divisorCounter = 0; // need an incrementing counter for the percentage to work correctly

        private static int lastProcessedIndex = 0; // make the loop more efficient

        public static int originalWallCount = -1; // used so can see how many walls before any removals

        private static float _startTime = -1; // End time of first note so start adding walls
        private static float _endTime = -1; // End time of last note so stop adding walls

        public static List<EObstacleData> originalWalls = new List<EObstacleData>();

        public static List<EObstacleData> generatedStandardWalls = new List<EObstacleData>();

        private static List<EObstacleData> tempOriginalAndStandardWalls = new List<EObstacleData>(); // just for updating a loop

        public static List<EObstacleData> generatedExtensionWalls = new List<EObstacleData>();

        //private static List<EObstacleData> _allWallsExceptFloorsAndParticles = new List<EObstacleData>();

        private static List<EObstacleData> particleWalls = new List<EObstacleData>();

        private static List<EObstacleData> floorWalls = new List<EObstacleData>();

        public static List<EObstacleData> allWalls = new List<EObstacleData>();

        private static bool allWallsContainsOriginalWalls = false;
        private static bool allWallsContainsStandardWalls = false;
        private static bool allWallsContainsExtensionWalls = false;
        private static bool allWallsContainsParticleWalls = false;
        private static bool allWallsContainsFloorWalls = false;

        private static bool failedWallRemovalForRotations1x = false;
        private static bool failedWallRemovalForRotations2x = false;
        private static bool failedWallRemovalForRotations3x = false;

        private static float StandardWallsMultiplier = Config.Instance.StandardWallsMultiplier;
        private static float DistantExtensionWallsMultiplier = Config.Instance.DistantExtensionWallsMultiplier;
        private static float ColumnWallsMultiplier = Config.Instance.ColumnWallsMultiplier;
        private static float RowWallsMultiplier = Config.Instance.RowWallsMultiplier;
        private static float TunnelWallsMultiplier = Config.Instance.TunnelWallsMultiplier;
        private static float GridWallsMultiplier = Config.Instance.GridWallsMultiplier;
        private static float WindowPaneWallsMultiplier = Config.Instance.WindowPaneWallsMultiplier;
        private static float ParticleWallsMultiplier = Config.Instance.ParticleWallsMultiplier;
        private static float FloorWallsMultiplier = Config.Instance.FloorWallsMultiplier;

        public static bool ExtensionMappingWallsGenerated = false;

        public static bool IsMappingExtensionsInstalled = GameplaySetupView.IsMappingExtensionsInstalled;

        public static float BeatDuration = 60f / TransitionPatcher.bpm;

        private static bool IsCityScapeMode => ToggleCityScape == 0; // 1 in 3
        private static bool IsSpiresMode => ToggleSpires == 0;    // 1 in 4
        private static bool IsSmallMode => ToggleSmall == 0;     // 1 in 5
        // Any "special skyline" mode means the skyscraper branch is active.
        private static bool IsSkyTileMode => IsCityScapeMode || IsSpiresMode;


        public static void SetOriginalWalls(EditableCBD eData)
        {
            originalWalls.Clear();
            foreach (var obstacle in eData.Obstacles) // Clear existing obstacles from BeatmapData so obstacles are empty
            {
                originalWalls.Add(obstacle); // add original obstacles into the list
                //Plugin.LogDebug($"[SetOriginalWalls] Original wall at time: {obstacle.time}, duration: {obstacle.duration}, layer: {obstacle.layer}, line: {obstacle.line}, width: {obstacle.width}, height: {obstacle.height}");
            }
            originalWalls.Sort((a, b) => a.time.CompareTo(b.time));
            originalWallCount = originalWalls.Count;

            eData.Obstacles.Clear();

            Plugin.LogDebug($"[SetOriginalWalls] Original wall count: {originalWallCount}");
        }
        public static void ResetWalls(EditableCBD eData)
        {
            if (TransitionPatcher.SelectedSerializedName == GameModeHelper.GENERATED_360DEGREE_MODE ||
                TransitionPatcher.SelectedSerializedName == "360Degree" ||
                TransitionPatcher.SelectedSerializedName == "90Degree")
            {
                StandardWallsMultiplier = Config.Instance.StandardWallsMultiplier;
                DistantExtensionWallsMultiplier = Config.Instance.DistantExtensionWallsMultiplier;
                ColumnWallsMultiplier = Config.Instance.ColumnWallsMultiplier;
                RowWallsMultiplier = Config.Instance.RowWallsMultiplier;
                TunnelWallsMultiplier = Config.Instance.TunnelWallsMultiplier;
                GridWallsMultiplier = Config.Instance.GridWallsMultiplier;
                WindowPaneWallsMultiplier = Config.Instance.WindowPaneWallsMultiplier;
                ParticleWallsMultiplier = Config.Instance.ParticleWallsMultiplier;
                FloorWallsMultiplier = Config.Instance.FloorWallsMultiplier;

                //  Plugin.LogDebug($"[WallGenerator][ResetWalls] - {TransitionPatcher.SelectedSerializedName} {TransitionPatcher.SelectedDifficulty} -  360/90 maps get all full wall multipliers.");

            }
            else // reduce number of wall in standard map since without rotations, it makes millions of walls that don't get removed
            {
                float mult = Config.Instance.StandardLevelWallMultiplier;

                StandardWallsMultiplier = Config.Instance.StandardWallsMultiplier * mult;
                DistantExtensionWallsMultiplier = Config.Instance.DistantExtensionWallsMultiplier * mult;
                ColumnWallsMultiplier = Config.Instance.ColumnWallsMultiplier * mult;
                RowWallsMultiplier = Config.Instance.RowWallsMultiplier * mult;
                TunnelWallsMultiplier = Config.Instance.TunnelWallsMultiplier * mult;
                GridWallsMultiplier = Config.Instance.GridWallsMultiplier * mult;
                WindowPaneWallsMultiplier = Config.Instance.WindowPaneWallsMultiplier * mult;
                ParticleWallsMultiplier = Config.Instance.ParticleWallsMultiplier * mult;
                FloorWallsMultiplier = Config.Instance.FloorWallsMultiplier * mult;

                Plugin.LogDebug($"[WallGenerator][ResetWalls] - {TransitionPatcher.SelectedSerializedName} {TransitionPatcher.SelectedDifficulty} - Standard maps get all wall multipliers reduced by {mult}.");
            }

            divisorCounter = 0;

            lastProcessedIndex = 0; // make the loop more efficient

            if (eData.ColorNotes.Count() > 0)
            {
                _startTime = eData.ColorNotes.First().time;
                _endTime = eData.ColorNotes.Last().time;
            }

            //Plugin.LogDebug($"[WallGenerator][ResetWalls] StartTime: {_startTime:F}, EndTime: {_endTime:F} - ColorNotes.Count: {eData.ColorNotes.Count()} - Obstacles.Count: {eData.Obstacles.Count()}");


            tempOriginalAndStandardWalls.Clear();
            foreach (var obstacle in originalWalls) // Clear existing obstacles from BeatmapData so obstacles are empty
            {
                tempOriginalAndStandardWalls.Add(obstacle); // add original obstacles into the list
            }

            generatedStandardWalls.Clear();
            generatedExtensionWalls.Clear();
            particleWalls.Clear();
            floorWalls.Clear();
            allWalls.Clear();

            allWallsContainsOriginalWalls = false;
            allWallsContainsStandardWalls = false;
            allWallsContainsExtensionWalls = false;
            allWallsContainsParticleWalls = false;
            allWallsContainsFloorWalls = false;

            //Plugin.LogDebug($"[WallGenerator] Walls RESET (except orginalWalls).");
        }

        public static void WallGen(int i, float wallTime, float wallDuration, ENoteData afterLastNote, List<ENoteData> notesInBarBeat, List<ENoteData> notesInBar, float nextNoteLeftTime, float nextNoteRightTime)

        {
            ExtensionMappingWallsGenerated = false;

            static bool LayerOverlap(int noteLine, int wallStartCol, int wallWidth)
            {
                int start = wallStartCol;
                int end = wallStartCol + Math.Max(1, wallWidth) - 1;
                if (start > end) { var t = start; start = end; end = t; }
                return noteLine >= start && noteLine <= end;
            }

            static float FindNearestFutureSameLayerNoteTime(
                IEnumerable<ENoteData> notesInBar,
                int wallStartCol, int wallWidth,
                float tFrom)
            {
                float nearest = float.PositiveInfinity;
                foreach (var n in notesInBar)
                {
                    if (!LayerOverlap(n.line, wallStartCol, wallWidth)) continue;
                    if (n.time >= tFrom && n.time < nearest) nearest = n.time;
                }
                return nearest; // may be +∞
            }

            /// <summary> 
            /// Clamp wall end time against nearest same-column note in-bar or after-bar note
            /// prevent notes inside walls or touching end of wall by clamping wall end time against nearest same-column note in-bar or after-bar note
            /// </summary>
            static void ClampWallEndAgainstNextNote(
                ref float start, ref float dur,
                IEnumerable<ENoteData> notesInBar,   // local scope for this bar
                float nextNoteAfterBarTime,          // pass float.PositiveInfinity if none
                int wallLayer, int wallWidth              // true: drop if would become a sliver
                )
            {
                // Find the earliest guard: either in-bar same-column, or after-bar guard
                float nearestInBar = FindNearestFutureSameLayerNoteTime(notesInBar, wallLayer, wallWidth, start);
                float guardTime = Math.Min(nearestInBar, nextNoteAfterBarTime);

                float end = start + dur;
                /*
                if (start >= 0 && start <= 20 && dur >= minWallDuration)
                    Plugin.Log.Info($"[WallGen][WallClamp start={start:F3}, dur={dur:F3}, end={end:F3}, " +
                                    $"nearestInBar={(float.IsPositiveInfinity(nearestInBar) ? float.PositiveInfinity : nearestInBar):F3}, " +
                                    $"nextAfterBar={(float.IsPositiveInfinity(nextNoteAfterBarTime) ? float.PositiveInfinity : nextNoteAfterBarTime):F3}, " +
                                    $"minDistanceBetweenNotesAndWalls={minDistanceBetweenNotesAndWalls:F3}");
                */
                if (float.IsPositiveInfinity(guardTime)) return; // nothing to clamp against

                float latestAllowedEnd = guardTime - minDistanceBetweenNotesAndWalls;
                if (end <= latestAllowedEnd) return; // already safe

                float newDur = Math.Max(0f, latestAllowedEnd - start);

                if (newDur < minWallDuration)
                {
                    // Signal caller to skip this wall by making dur negative (or just return and let caller check)
                    dur = -1f; // caller: if (wallDuration < 0) return;
                    //if (start >= 0 && start <= 20) Plugin.Log.Info($"[WallGen][WallClamp] DROPPED (would be sliver or negative duration). latestAllowedEnd={latestAllowedEnd:F3}");
                    return;
                }

                if (newDur < minWallDuration) newDur = minWallDuration;

                //if (start >= 0 && start <= 20) Plugin.Log.Info($"[WallGen][WallClamp] TRIM: oldEnd={end:F3} -> newEnd={(start + newDur):F3} (dur {dur:F3}->{newDur:F3})");

                dur = newDur;
            }

            minWallDuration = Config.Instance.MinWallDuration;

            int standardWallsMinDistance = (int)Config.Instance.StandardWallsMinDistance;

            int offsetRightWall = 0; // used to offset randomly right or left wall by 2 so they both will not be at 0 offset, otherwise that is 2 leaning walls touching each other with no gap between.
            int offsetLeftWall = 0;
            if (standardWallsMinDistance == 0)
            {
                offsetRightWall = TransitionPatcher.RepeatableRandom.Next(2) == 0 ? 2 : 0; // if false, will offset left wall instead. if i use 1 offset, then there is a tiny 1 lane tunnel between 2 walls. but if use 2 then one wall is lean and the other either -1 or 4.
                offsetLeftWall = offsetRightWall == 2 ? 0 : 2;
            }

            if (TransitionPatcher.SelectedSerializedName == GameModeHelper.GENERATED_360DEGREE_MODE || TransitionPatcher.SelectedSerializedName == "360Degree" || TransitionPatcher.SelectedSerializedName == "90Degree") // was not setting this for 360 before
            {
                minDistanceBetweenNotesAndWalls = Config.Instance.MinDistanceBetweenNotesAndWalls * Config.Instance.RotationSpeedMultiplier; // was .5f then .7f
                wallTime += minDistanceBetweenNotesAndWalls;
            }
            {
                minDistanceBetweenNotesAndWalls = .2f;
                wallTime += minDistanceBetweenNotesAndWalls / 2;
            }

            //Plugin.Log.Info($"WallGen: i: {i} wallTime: {wallTime:F} wallDuration: {wallDuration:F} afterLastNote: {afterLastNote?.time:F} notesInBarBeat.Count: {notesInBarBeat.Count} notesInBar.Count: {notesInBar.Count}");

            //Plugin.Log.Info($"WallGenerator: containsCustomWalls: {BeatmapDataTransformHelperPatcher.containsCustomWalls}");

            string generatedBigWall = "none"; // used to prevent bigWalls overlapping with columns of walls and rows of walls
            string generatedWall = "none";    // used to prevent standard walls overlapping with columns of walls and rows of walls

            divisorCounter++;
            int divisor = (int)(100 / StandardWallsMultiplier); // Get the divisor based on the user input percentage
            if (divisorCounter % divisor != 0) return;

            //Plugin.Log.Info($"Config.Instance.StandardWallsMultiplier: {Config.Instance.StandardWallsMultiplier} divisor: {divisor}");

            lastTunnelWallTime = 0; // must reset this here or will keep last setting from previous song play through
            lastWindowPaneWallTime = 0;
            tunnelWallsHappening = false;
            paneWallsHappening = false;

            bool generateWall = true;

            float minGapBetweenWalls = .2f; // seconds

            //Plugin.Log.Info($"Wall Gen - wallTime: {wallTime:F} wallDuration: {wallDuration}");

            for (int k = lastProcessedIndex; k < tempOriginalAndStandardWalls.Count; k++) // Check if there is already a wall
            {
                EObstacleData obs = tempOriginalAndStandardWalls[k];

                //Plugin.Log.Info($"- Checking obstacle {k}: obs.time: {obs.time}, obs.duration: {obs.duration}, wallTime: {wallTime}, wallDuration: {wallDuration}, minGapBetweenWalls: {minGapBetweenWalls}");

                if (obs.time > wallTime) // don't use duration here since may change wallDuration later. duration is not set yet.
                {
                    //Plugin.Log.Info($"--- YES can possibly generate new wall - Obstacle time {obs.time} is after wall start: {wallTime}"); generateWall = true;
                    break; // can generate a new wall since no walls found
                }
                if (obs.time + obs.duration + minGapBetweenWalls >= wallTime && obs.time < wallTime + wallDuration + minGapBetweenWalls)
                {
                    //Plugin.Log.Info($"--- NO cannot generate new wall - Obstacle time + duration + gap {obs.time + obs.duration + minGapBetweenWalls} overlaps with wall time {wallTime}");
                    generateWall = false;
                    break;
                }

                // Log if the obstacle does not affect wall generation
                //Plugin.Log.Info($"----- Obstacle {k} time: {obs.time} does not affect wall generation. Moving to the next obstacle.");

                lastProcessedIndex = k; // Update the last processed index
            }

            if (generateWall && afterLastNote != null)
            {
                // ===== RIGHT WALLS =====
                if (!notesInBarBeat.Any(e => e.line == 3))
                {
                    int widthRight = 1;
                    int wallHeightR = notesInBarBeat.Any(e => e.line == 2) ? 1 : 3;
                    int lineLayerR = wallHeightR == 1 ? 2 : 0;

                    // decide width first (uses your existing cadence)
                    if (i % 3 == 0 || i % 7 == 0)
                    {
                        if (Config.Instance.EnableBigWalls) { widthRight = 12; generatedBigWall = "right"; }
                    }
                    else
                    {
                        if (Config.Instance.EnableStandardWalls) { widthRight = 1; generatedWall = "right"; }
                    }

                    // compute start col for the clamp BEFORE calling it
                    int startColRight = 2 + standardWallsMinDistance + offsetRightWall;

                    // use local start/dur so this side doesn’t affect the other
                    float rStart = wallTime;
                    float rDur = wallDuration;

                    if (afterLastNote.line == 3 && !(wallHeightR == 1 && afterLastNote.layer == 0))
                    {
                        rDur = afterLastNote.time - WallBackCut - rStart;
                        if (rDur < minWallDuration) goto LEFT_WALLS;
                    }

                    // clamp against in-bar and next-bar guards (RIGHT uses nextNoteRightTime)
                    ClampWallEndAgainstNextNote(ref rStart, ref rDur, notesInBar, nextNoteRightTime, startColRight, widthRight);
                    if (rDur < 0f) goto LEFT_WALLS;

                    if ((Config.Instance.EnableStandardWalls && widthRight == 1) || (Config.Instance.EnableBigWalls && widthRight == 12))
                    {
                        int heightR = 5;

                        if (widthRight == 12 && i % 21 == 0) { widthRight = 50; heightR = 50; rDur = Math.Min(rDur / 10f, .1f); }

                        var customObsDataR = EObstacleData.Create(rStart, startColRight, lineLayerR, rDur, widthRight, heightR);
                        generatedStandardWalls.Add(customObsDataR);

                        //Plugin.LogDebug($"[WallGen] Generated RIGHT wall at time {rStart:F}, duration {rDur:F2}, line {startColRight}, width {widthRight}, height {heightR}");

                        int idxR = tempOriginalAndStandardWalls.BinarySearch(customObsDataR, Comparer<EObstacleData>.Create((x, y) => x.time.CompareTo(y.time)));
                        if (idxR < 0) idxR = ~idxR;
                        tempOriginalAndStandardWalls.Insert(idxR, customObsDataR);
                    }
                }

                // ===== LEFT WALLS =====
                LEFT_WALLS:
                if (!notesInBarBeat.Any(e => e.line == 0))
                {
                    int widthLeft = 1;
                    int wallHeightL = notesInBarBeat.Any(e => e.line == 1) ? 1 : 3;
                    int lineLayerL = wallHeightL == 1 ? 2 : 0;

                    if (i % 4 == 0 || i % 6 == 0)
                    {
                        if (Config.Instance.EnableBigWalls) { widthLeft = 12; generatedBigWall = generatedBigWall == "right" ? "both" : "left"; }
                    }
                    else
                    {
                        if (Config.Instance.EnableStandardWalls) { widthLeft = 1; generatedWall = generatedWall == "right" ? "both" : "left"; }
                    }

                    // compute LEFT start column FIRST; big walls sit farther left
                    int startColLeft = (widthLeft == 1 ? 1 : -10) - standardWallsMinDistance - offsetLeftWall;

                    // side-local start/dur
                    float lStart = wallTime;
                    float lDur = wallDuration;
                    bool skip = false;

                    if (afterLastNote.line == 0 && !(wallHeightL == 1 && afterLastNote.layer == 0))
                    {
                        lDur = afterLastNote.time - WallBackCut - lStart;
                        if (lDur < minWallDuration) skip = true; // skip left, continue
                    }
                    if (!skip)
                    {
                        ClampWallEndAgainstNextNote(ref lStart, ref lDur, notesInBar, nextNoteLeftTime, startColLeft, widthLeft);
                        if (lDur < 0f) skip = true;
                    }

                    if (!skip && ((Config.Instance.EnableStandardWalls && widthLeft == 1) || (Config.Instance.EnableBigWalls && widthLeft == 12)))
                    {
                        int heightL = 5;

                        if (widthLeft == 12 && i % 24 == 0) { widthLeft = 50; heightL = 50; startColLeft = -49; lDur = Math.Min(lDur / 10f, .1f); }

                        var customObsDataL = EObstacleData.Create(lStart, startColLeft, lineLayerL, lDur, widthLeft, heightL);
                        generatedStandardWalls.Add(customObsDataL);

                        //Plugin.LogDebug($"[WallGen] Generated LEFT wall at time {lStart:F}, duration {lDur:F2}, line {startColLeft}, width {widthLeft}, height {heightL}");

                        int idxL = tempOriginalAndStandardWalls.BinarySearch(customObsDataL, Comparer<EObstacleData>.Create((x, y) => x.time.CompareTo(y.time)));
                        if (idxL < 0) idxL = ~idxL;
                        tempOriginalAndStandardWalls.Insert(idxL, customObsDataL);
                    }
                }
            }
            //Plugin.Log.Info($"WallTime: {wallTime} GeneratedWall: {generatedWall} Time: Count: {genWallCount} - GenderatedBigWall: {generatedBigWall} Count: {genBigWallCount}");

            if (Config.Instance.EnableDistantExtensionWalls ||
                Config.Instance.EnableColumnWalls || Config.Instance.EnableRowWalls ||
                Config.Instance.EnableTunnelWalls || Config.Instance.EnableGridWalls ||
                Config.Instance.EnableWindowPaneWalls)
            {
                CreateExtensionWalls(i, wallTime, wallDuration, generatedWall, generatedBigWall);
            }
            //Plugin.Log.Info($"Map doesn't NOT already use Mapping Extensions");
        }



        // Extension Walls ------------------------------------------------------------------------------------------------------

        #region Extension Walls

        // [WARNING @ 12:53:12 | UnityEngine] BoxColliders does not support negative scale or size.
        // Caused by Window Panes and Floor Walls sometimes (360 or standard). couln't find any negative inputs from window panes anyway so stopped looking
        public static void CreateExtensionWalls(int i, float wallTime, float wallDuration, string alreadyHasGenWall, string alreadyHasBigWall) // big walls (not particle walls) - using walltime so on the beat
        {
            //v1.42 allow these walls without Mapping Extensions mod

            // give the appearance of randomness
            int[] hiLineLayer = { 10, 18, 20, 22 };// { 10, 20, 25, 30 }
            int[] loLineLayer = { 0, 1, 2 };
            int[] hiLineIndex = { 3, 5, 7 };
            int[] hiWidth = { 2, 4, 7, 12 };
            int[] height = { 3, 4, 5, 30000, 40000, 50000 };
            int[] dur = { 2, 5, 10, 15 };
            bool[] hiBoth = { true, true, false, true, false, false, false, true };
            bool[] loBoth = { false, true, false, true, false, false, false, true, true };
            int[] allWalls = { 1, 2, 3 }; // 1 is just low walls, 2 is just high walls, 3 is all walls
            int loWidth = 1;
            int sign = 1;

            int[] gridWallWidth = { 1000, 2000, 3000, 4000 };
            int[] gridWallHeight = { 1500, 2500, 3500, 1500, 2000 };

            int[] columnsLineIndexMultiplier = { 1, 2, 8, 10, 12, 15, 20 }; //distance of column groups from player
            int[] numOfColumns = { 3, 4, 5, 5, 7, 10, 12 };

            int[] tunnelWallCount = { 1, 2 };//,  3 }; // just couldn't get 3 to work properly
            int[] tunnelWallDurationMult = { 1, 2, 3 };

            int[] windowPaneWallSize = { 1, 2, 2 }; //{ 1, 2, 3, 2, 2 };
            int[] windowPaneWallLineLayer = { 0, 1, 2, 3, 4, 5, 6, 2, 3 };
            int[] windowPaneWallLineIndex = { 0, 1, 2, 3, 0, 0 };

            if (i % 2 == 0)
                sign = -1;

            // Using a hash function to determine indices
            int hash = (i * 31 + 17) % 251; // Example hash function

            string alreadyHasExtendedWalls = "none";

            //These are distant low walls lineIndex 15 or -15 or very high walls so don't need to check for existing walls
            if (Config.Instance.EnableDistantExtensionWalls) // now works witn non-ME
            {
                int divisorOne = Math.Max((int)Math.Round(5 / DistantExtensionWallsMultiplier), 1); // Use Math.Round: Ensure that you round the result of your division to get meaningful divisors for the modulo operation.This avoids erroneous behavior from using floating-point division directly in integer contexts. Check for Zero Divisor: Ensure that the divisor does not round to zero, as dividing by zero will throw an exception.
                int divisorTwo = Math.Max((int)Math.Round(8 / DistantExtensionWallsMultiplier), 1);
                int divisorThr = Math.Max((int)Math.Round(11 / DistantExtensionWallsMultiplier), 1);

                if (i % divisorOne == 0 || i % divisorTwo == 0 || i % divisorThr == 0)
                {
                    int hiLayer = hiLineLayer[hash % hiLineLayer.Length];
                    int loLayer = loLineLayer[hash % loLineLayer.Length];
                    int hiLineIndx = hiLineIndex[hash % hiLineIndex.Length];
                    int hiWidth1 = hiWidth[hash % hiWidth.Length];
                    int height1 = height[hash % height.Length];
                    float duration = Math.Min(dur[(hash % dur.Length)], wallDuration); // could alter this to lengthen walls but causes overlap of walls sometimes i think
                    bool hiBoth1 = hiBoth[hash % hiBoth.Length];
                    bool loBoth1 = loBoth[hash % loBoth.Length];
                    int allWalls1 = allWalls[hash % allWalls.Length];

                    int lineIndex = sign * 15;

                    EObstacleData customObsData;

                    // Low Distant Walls
                    if (allWalls1 == 1 || allWalls1 == 3)
                    {
                        customObsData = EObstacleData.Create(wallTime, lineIndex, loLayer, duration, loWidth, height1);
                        //Plugin.Log.Info($"Wall EXTENSION Lo Rt: Time: {wallTime}, Index:{indexx}, Layer: {loLayer}, Dur: {duration}, Width: {loWidth}, Height: {height1}");
                        if (lineIndex < 2)
                        {
                            //Plugin.Log.Info($"Wall EXTENSION Lo Rt: Time: {wallTime}, Index:{indexx}, Layer: {loLayer}, Dur: {duration}, Width: {loWidth}, Height: {height1}");
                            generatedExtensionWalls.Add(customObsData);
                            alreadyHasExtendedWalls = "left";
                        }
                        else
                        {
                            //Plugin.Log.Info($"Wall EXTENSION Lo Rt: Time: {wallTime}, Index:{indexx}, Layer: {loLayer}, Dur: {duration}, Width: {loWidth}, Height: {height1}");
                            generatedExtensionWalls.Add(customObsData);
                            alreadyHasExtendedWalls = "right";
                        }
                        //data.AddBeatmapObjectDataInOrder(customObsData);
                        distantCount++;

                        if (loBoth1)
                        {
                            customObsData = EObstacleData.Create(wallTime, -lineIndex, loLayer, duration, loWidth, height1);
                            if (lineIndex < 2)
                            {
                                generatedExtensionWalls.Add(customObsData);
                                alreadyHasExtendedWalls = alreadyHasExtendedWalls == "right" ? "both" : "left";
                            }
                            else
                            {
                                generatedExtensionWalls.Add(customObsData);
                                alreadyHasExtendedWalls = alreadyHasExtendedWalls == "left" ? "both" : "right";
                            }
                            //Plugin.Log.Info($"Wall EXTENSION Lo Lt: Time: {wallTime}, Index:{-indexx}, Layer: {loLayer}, Dur: {duration}, Width: {loWidth}, Height: {height1}");
                            //data.AddBeatmapObjectDataInOrder(customObsData);
                            distantCount++;

                        }


                    }
                    // high distant walls
                    if (allWalls1 == 2 || allWalls1 == 3)
                    {
                        if (height1 < 1000)
                            hiWidth1 = Math.Min(4, hiWidth1);

                        if (hiLayer >= 20)
                        {
                            duration *= 4f;
                            hiWidth1 = 4;
                        }

                        if (!IsMappingExtensionsInstalled) hiLineIndx *= 3; // if Mapping Extensions not enabled, move walls further out since collide with standard walls otherwise

                        lineIndex = sign * hiLineIndx;

                        customObsData = EObstacleData.Create(wallTime, lineIndex, hiLayer, duration, hiWidth1, height1);
                        if (lineIndex < 2)
                        {
                            generatedExtensionWalls.Add(customObsData);
                            alreadyHasExtendedWalls = alreadyHasExtendedWalls == "right" ? "both" : "left";
                        }
                        else
                        {
                            generatedExtensionWalls.Add(customObsData);
                            alreadyHasExtendedWalls = alreadyHasExtendedWalls == "left" ? "both" : "right";
                        }
                        //Plugin.LogDebug($"[DistantWalls] Hi Time: {wallTime:F}, Index:{lineIndex}, Layer: {hiLayer}, Dur: {duration}, Width: {hiWidth1}, Height: {height1}");
                        //data.AddBeatmapObjectDataInOrder(customObsData);
                        distantCount++;

                        if (hiBoth1)
                        {
                            customObsData = EObstacleData.Create(wallTime, -lineIndex, hiLayer, duration, hiWidth1, height1);
                            if (lineIndex < 2)
                            {
                                generatedExtensionWalls.Add(customObsData);
                                alreadyHasExtendedWalls = alreadyHasExtendedWalls == "right" ? "both" : "left";
                            }
                            else
                            {
                                generatedExtensionWalls.Add(customObsData);
                                alreadyHasExtendedWalls = alreadyHasExtendedWalls == "left" ? "both" : "right";
                            }
                            //Plugin.LogDebug($"[DistantWalls] Hi Time: {wallTime:F}, Index:{-lineIndex}, Layer: {hiLayer}, Dur: {duration}, Width: {hiWidth1}, Height: {height1}");
                            //data.AddBeatmapObjectDataInOrder(customObsData);
                            distantCount++;
                        }
                    }

                }
            }

            int columnsLineIndexMult = columnsLineIndexMultiplier[hash % columnsLineIndexMultiplier.Length];
            int numberOfColumns = numOfColumns[hash % numOfColumns.Length];

            int durationMult = tunnelWallDurationMult[(int)wallTime % tunnelWallDurationMult.Length];
            int wallCount = tunnelWallCount[(int)wallTime % tunnelWallCount.Length];

            int paneWidth = windowPaneWallSize[(int)wallTime % windowPaneWallSize.Length];// using HASH caused only 2 options to be selected always from entire list  same for every song !!!!

            int divisorCol = Math.Max((int)Math.Round(15 / ColumnWallsMultiplier), 1);
            int divisorRow = Math.Max((int)Math.Round(17 / RowWallsMultiplier), 1);
            int divisorTunnel = Math.Max((int)Math.Round(5 / TunnelWallsMultiplier), 1);
            int divisorGrid = Math.Max((int)Math.Round(18 / GridWallsMultiplier), 1);// was 21
            int divisorPane = Math.Max((int)Math.Round(9 / WindowPaneWallsMultiplier), 1);

            //Plugin.Log.Info($"[GridWalls DEBUG] hash: {hash}, divisorGrid: {divisorGrid}, hash % divisorGrid: {hash % divisorGrid}");
            //Plugin.Log.Info($"Hash Outcomes - numOfColumns: {numberOfColumns}, Hash: {hash}, Hash % 4: {hash % 4}, Hash % numOfColumns.Length: {hash % numOfColumns.Length}");

            if (alreadyHasBigWall != "both" && alreadyHasExtendedWalls != "both")
            {
                if (Config.Instance.EnableColumnWalls && hash % divisorCol == 0)
                {
                    ColumnWalls(wallTime, alreadyHasBigWall, numberOfColumns, columnsLineIndexMult, alreadyHasExtendedWalls);
                }
                else if (Config.Instance.EnableRowWalls && hash % divisorRow == 0)
                {
                    RowWalls(wallTime, alreadyHasBigWall, numberOfColumns, alreadyHasExtendedWalls);
                }
                else if (IsMappingExtensionsInstalled && Config.Instance.EnableTunnelWalls && !paneWallsHappening && hash % divisorTunnel == 0) // tunnel box walls surrounding the player
                {
                    TunnelWalls(wallTime, alreadyHasGenWall, alreadyHasBigWall, wallCount, numberOfColumns, durationMult); // ME only
                }
                else if (Config.Instance.EnableGridWalls && hash % divisorGrid == 0) // 14 grid walls parallel to player (not perpendicular which requires using time)
                {
                    GridWalls(wallTime, alreadyHasBigWall, gridWallWidth, gridWallHeight, numberOfColumns, alreadyHasExtendedWalls);
                }
                else if (Config.Instance.EnableWindowPaneWalls && !tunnelWallsHappening && hash % divisorPane == 0) // window pane walls
                {
                    WindowPaneWalls(wallTime, alreadyHasGenWall, alreadyHasBigWall, paneWidth, windowPaneWallLineLayer, windowPaneWallLineIndex, numberOfColumns);
                }

            }
            if (IsMappingExtensionsInstalled && generatedExtensionWalls.Count > 0)
                ExtensionMappingWallsGenerated = true;
            else
                ExtensionMappingWallsGenerated = false;
        }
        private static void ColumnWalls(float wallTime, string alreadyHasBigWall, int numberOfColumns,
            int columnsLineIndexMult, string alreadyHasExtendedWalls) // works with non-ME but is cut off at the top
        {
            int width = 1;

            for (int j = 0; j <= numberOfColumns * 2; j++) // columns of tall walls
            {
                int rndHeight = TransitionPatcher.RepeatableRandom.Next(8) + 13;
                bool rndHeightOption2 = TransitionPatcher.RepeatableRandom.Next(2) == 0;
                if (IsMappingExtensionsInstalled && rndHeightOption2)
                    rndHeight = 20;


                int layer = j;
                int height = rndHeight + j;

                //Plugin.Log.Info($"Wall Columns: Time: {wallTime}, Number of cols: {numberOfColumns} indexMultiplier: {columnsLineIndexMult} divisorOne: {divisorOne}");
                int line = j * 2 + (int)Config.Instance.ColumnWallsMinDistance - 2 + columnsLineIndexMult;

                if (alreadyHasBigWall != "left" && alreadyHasExtendedWalls != "left")
                {
                    EObstacleData customObsData = EObstacleData.Create(wallTime, -line, layer, .001f, width, height); // -2, -4, -6, -8 columns with 1 space between
                    generatedExtensionWalls.Add(customObsData);
                    //Plugin.Log.Info($"Wall Column Time: {wallTime}, Index:{-k}, Layer: {j}, Dur: .001f, Width: 1, Height: {10 + j}");

                    columnCount++;
                }
                if (alreadyHasBigWall != "right" && alreadyHasExtendedWalls != "right")
                {
                    EObstacleData customObsData = EObstacleData.Create(wallTime, (line + 3), layer, .001f, width, height); // 5, 7, 9, 11 columns with 1 space between
                    generatedExtensionWalls.Add(customObsData);
                    //Plugin.Log.Info($"Wall Column Time: {wallTime}, Index:{k + 3}, Layer: {j}, Dur: .001f, Width: 1, Height: {10 + j}");

                    columnCount++;
                }
            }
        }

        private static void RowWalls(float wallTime, string alreadyHasBigWall, int numberOfRows,
            string alreadyHasExtendedWalls) // works with non-ME walls
        {
            float dur = .03f;

            numberOfRows = IsMappingExtensionsInstalled ? numberOfRows * 2 : 4; // non-ME cannot go high up so limit rows to 4. will be cut off strangely if above layer 4

            bool isV1 = false;
            if (IsMappingExtensionsInstalled)
            {
                int rnd = TransitionPatcher.RepeatableRandom.Next(3);
                if (rnd == 0 || rnd == 1) isV1 = true;
            }

            // Decide loop bounds and step based on ME
            int maxJ = IsMappingExtensionsInstalled && isV1 ? numberOfRows * 2 : numberOfRows;
            int step = IsMappingExtensionsInstalled && isV1 ? 1 : 2;    // ME: every row; non-ME: every other row

            for (int j = 0; j <= maxJ; j += step)
            {
                // v1
                if (isV1) // ME only Thin original version
                {
                    int k = j * 2 + 2;

                    int leftLineIndexCalc = (int)Config.Instance.RowWallsMinDistance * 1000 + 20000; // 24000 default
                    int rightLineIndexCalc = (int)Config.Instance.RowWallsMinDistance * 1000 + 5000;  // 9000 default

                    if (alreadyHasBigWall != "left" && alreadyHasExtendedWalls != "left")
                    {
                        int lineLeft = -leftLineIndexCalc - (j * 500);

                        EObstacleData customObsData = EObstacleData.Create(
                            wallTime,
                            lineLeft,
                            k,
                            0.03f,           // keep your original ME duration
                            20 + j,          // original width pattern
                            1200             // height
                        );

                        generatedExtensionWalls.Add(customObsData);
                        //Plugin.Log.Info(
                        //    $"[RowWalls] Lt: Time: {wallTime:F}, Index: {lineLeft}, Layer: {k}, Dur: 0.03f, Width: {20 + j}, Height: 1200");
                    }

                    if (alreadyHasBigWall != "right" && alreadyHasExtendedWalls != "right")
                    {
                        int lineRight = rightLineIndexCalc - (j * 500);

                        EObstacleData customObsData = EObstacleData.Create(
                            wallTime,
                            lineRight,
                            k,
                            0.03f,
                            20 + j,
                            1200
                        );

                        generatedExtensionWalls.Add(customObsData);
                        //Plugin.Log.Info(
                        //    $"[RowWalls] Rt: Time: {wallTime:F}, Index: {lineRight}, Layer: {k}, Dur: 0.03f, Width: {20 + j}, Height: 1200");
                    }
                }
                else // v2 ME or Non-ME fat version with misc widths
                {

                    int rndWidth = TransitionPatcher.RepeatableRandom.Next(20) + 1; // avoid 0
                    bool wider = TransitionPatcher.RepeatableRandom.Next(2) == 0;

                    if (wider)
                        rndWidth = (int)(rndWidth * 1.5f);  // make non-ME walls wider sometimes

                    int width = rndWidth;
                    int layer = j;
                    int lineSpacing = j;

                    int leftLineIndexCalc = (int)Config.Instance.RowWallsMinDistance + rndWidth;
                    int rightLineIndexCalc = (int)Config.Instance.RowWallsMinDistance + 5;

                    int height = 1;// not working on my test ME plugin anyway IsMappingExtensionsInstalled ? 1800 : 1;

                    // For non-ME we keep everything in "normal" line index space:
                    int lineLeft = -leftLineIndexCalc - lineSpacing;
                    int lineRight = rightLineIndexCalc + lineSpacing;

                    if (alreadyHasBigWall != "left" && alreadyHasExtendedWalls != "left")
                    {
                        EObstacleData customObsData = EObstacleData.Create(
                            wallTime,
                            lineLeft,
                            layer,
                            dur,
                            width,
                            height
                        );
                        generatedExtensionWalls.Add(customObsData);
                        //Plugin.Log.Info(
                        //    $"[RowWalls] Lt: Time: {wallTime:F}, Index:{lineLeft}, Layer: {layer}, Dur: {dur}, Width: {width}, Height: {height}");
                    }

                    if (alreadyHasBigWall != "right" && alreadyHasExtendedWalls != "right")
                    {
                        EObstacleData customObsData = EObstacleData.Create(
                            wallTime,
                            lineRight,
                            layer,
                            dur,
                            width,
                            height
                        );
                        generatedExtensionWalls.Add(customObsData);
                        //Plugin.Log.Info(
                        //    $"[RowWalls] Rt: Time: {wallTime:F}, Index:{lineRight}, Layer: {layer}, Dur: {dur}, Width: {width}, Height: {height}");
                    }
                }
            }
        }


        private static void GridWalls(
            float wallTime,
            string alreadyHasBigWall,
            int[] gridWallWidth,
            int[] gridWallHeight,
            int numberOfColumns,
            string alreadyHasExtendedWalls,
            int MaxLayer = 12) // Works with non-ME walls, Highest allowed layer when ME is OFF (0 or less = no cap)
        {
            // --- Helpers (kept local for readability) ---
            static int ClampInt(int v, int min, int max) => v < min ? min : (v > max ? max : v);

            // Converts ME-style integers (e.g., 6500) -> coarse units (e.g., 7)
            static int QuantizeFromME(int vME) => (int)MathF.Round(vME / 1000f);

            // Converts ME-style sizes (e.g., 2000) -> coarse size (e.g., 2)
            static int SizeFromME(int sizeME) => Math.Max(1, (int)MathF.Round(sizeME / 1000f));

            // --- Pick width/height pattern from arrays (these are ME-style values like 1000/2000/3000/4000) ---
            int widthME = gridWallWidth[(int)wallTime % gridWallWidth.Length];
            int heightME = gridWallHeight[(int)wallTime % gridWallHeight.Length];

            // --- Spacing in ME space ---
            // ME ON: keep original small precision gap
            // ME OFF: use 1000 so that after quantization it becomes a gap of 1 unit
            int gapME = IsMappingExtensionsInstalled ? 300 : 1000;

            // --- Anchor positions (ME space) ---
            int leftLineIndexME = (int)Config.Instance.GridWallsMinDistance * 1000 + 2500;
            int rightLineIndexME = (int)Config.Instance.GridWallsMinDistance * 1000 + 4500;

            int adjustLineIndexME = 0;
            if (widthME > 2000)
                adjustLineIndexME = 1000;

            // --- Grid sizing (your existing logic) ---
            float mult = 3;
            if (GridWallsMultiplier > 1)
                mult = 4 - GridWallsMultiplier;

            int gridColumns = (int)(mult * (gridWallWide ? 3 : 1));
            int gridRows = (int)(mult * (gridWallWide ? 1.5 : 5));
            gridWallWide = !gridWallWide;

            // --- RNG / time jitter ---
            // Your previous code re-created Random() repeatedly; this can reduce randomness.
            // This seed makes the jitter vary across different calls and cells, while staying stable enough for testing.
            int baseSeed = unchecked((int)(wallTime * 1000f)) ^ (gridCount * 397);
            System.Random rand = new System.Random(baseSeed);

            // Wider time jitter in non-ME to reduce visible bunching/stacking
            float timeJitter = IsMappingExtensionsInstalled ? 0.03f : 0.12f; // +/- seconds

            for (int j = 0; j <= gridColumns; j++) // columns
            {
                for (int k = 0; k <= gridRows; k++) // rows
                {
                    float randomDuration = 0.0001f + (float)(rand.NextDouble() * 0.002f);

                    float randomTimeOffset = (float)(rand.NextDouble() * (timeJitter * 2f)) - timeJitter;
                    float adjustedWallTime = wallTime + randomTimeOffset;

                    // --- Compute positions in ME space first (keeps the "ME look") ---
                    int lineLeftME = -leftLineIndexME - (j * (widthME + gapME)) - adjustLineIndexME;
                    int lineRightME = rightLineIndexME + (j * (widthME + gapME));

                    // Vertical position:
                    // - ME ON: keep original behavior (matches your working ME grids)
                    // - ME OFF: we compute Y directly in coarse units from the coarse height, to guarantee a gap of 1 layer
                    int layerME = (k * heightME + gapME);

                    // =========================
                    // LEFT GRID WALL
                    // =========================
                    if (alreadyHasBigWall != "left" && alreadyHasExtendedWalls != "left")
                    {
                        int line, layer, w, h;

                        if (IsMappingExtensionsInstalled)
                        {
                            // Original ME behavior
                            line = lineLeftME;
                            layer = layerME;
                            w = widthME;
                            h = heightME;
                        }
                        else
                        {
                            // Non-ME behavior:
                            // Convert X to coarse lanes
                            line = QuantizeFromME(lineLeftME);
                            w = ClampInt(SizeFromME(widthME), 1, 2); // max width 3
                            h = 1;// ClampInt(SizeFromME(heightME), 1, 2); // max height 2

                            // Vertical stacking with a guaranteed 1-layer empty gap:
                            // row start layers: 0, (h+1), 2*(h+1), ...
                            int layerGap = 0;
                            layer = ClampInt(k * (h + layerGap), 0, 4);

                            if (layer > 6) h = 1; // above layer 6, reduce height to 1 since layers are weird up high

                            // Apply MaxLayer as a STOP, not a clamp (prevents top-layer pile-ups)
                            if (MaxLayer > 0 && layer > MaxLayer)
                                break; // k only increases, so we can stop making higher rows for this column
                        }

                        //Plugin.Log.Info($"Grid Wall EXTENSION Lt: Time: {adjustedWallTime}, Index:{x}, Layer: {y}, Dur: {randomDuration}, Width: {w}, Height: {h}");

                        EObstacleData leftObs = EObstacleData.Create(adjustedWallTime, line, layer, randomDuration, w, h);
                        generatedExtensionWalls.Add(leftObs);
                        gridCount++;
                    }

                    // =========================
                    // RIGHT GRID WALL
                    // =========================
                    if (alreadyHasBigWall != "right" && alreadyHasExtendedWalls != "right")
                    {
                        int line, layer, w, h;

                        if (IsMappingExtensionsInstalled)
                        {
                            // Original ME behavior: right side uses height 2000
                            line = lineRightME;
                            layer = layerME;
                            w = widthME;
                            h = 2000;
                        }
                        else
                        {
                            line = QuantizeFromME(lineRightME);
                            w = ClampInt(SizeFromME(widthME), 1, 2);
                            h = 1;

                            // Same vertical stacking rule; guarantees 1-layer gap
                            layer = ClampInt(k * (h + 1), 0, 4);

                            if (layer > 6) h = 1; // above layer 6, reduce height to 1 since layers are weird up high

                            // Stop at MaxLayer
                            if (MaxLayer > 0 && layer > MaxLayer)
                                break;
                        }

                        //Plugin.Log.Info($"Grid Wall EXTENSION Rt: Time: {adjustedWallTime}, Index:{x}, Layer: {y}, Dur: {randomDuration}, Width: {w}, Height: {h}");

                        EObstacleData rightObs = EObstacleData.Create(adjustedWallTime, line, layer, randomDuration, w, h);
                        generatedExtensionWalls.Add(rightObs);
                        gridCount++;
                    }
                }
            }
        }

        private static void TunnelWalls(float wallTime, string alreadyHasGenWall, string alreadyHasBigWall,
                int wallCount, int numberOfColumns, int durationMult)
        {
            tunnelWallsHappening = true; // set to false at begin of main loop if time is already past the last tunnel wall

            int layer2 = 0;
            //int layer3 = 0;

            int height1 = 4;
            int gap = 500; // .5

            if (wallCount == 2)// || wallCount == 3)
            {
                height1 = 2500;
                layer2 = height1 + gap;
                //Plugin.Log.Info($"2 Box Walls: Time: {wallTime}");
            }

            int topLineLayer = (int)Config.Instance.TunnelWallsMinDistance * 1000 + 6750; // this value does not work for my mapping extension mod that i fixed to test 1.42. not sure why but this value makes the wall disapper. if i change to 5 it appear.
            int leftLineIndex = (int)Config.Instance.TunnelWallsMinDistance * 1000 + 2250; // 1500 default which is 1 (1.5) away basically from player
            int rightLineIndex = (int)Config.Instance.TunnelWallsMinDistance * 1000 + 5750; // 5000 default which is 1 away basically from player

            for (int j = 0; j <= Math.Max(numberOfColumns * 2, 6); j++)
            {
                float newWallStartTime = wallTime + (j * 0.06f * durationMult);
                float newWallEndTime = newWallStartTime + .03f * durationMult;  // Assuming duration is the length of the wall in time

                // since walls are adding into the future from wallTime, the next loop may likely overlap walls so avoid this
                if (newWallStartTime > lastTunnelWallTime && newWallStartTime < _endTime)
                {
                    // Top Wall
                    if (alreadyHasGenWall == "none" && alreadyHasBigWall == "none")  // top wall - dont' check for alreadyHasExtendedWalls since those are distant walls
                    {
                        EObstacleData customObsData = EObstacleData.Create(newWallStartTime, 0, topLineLayer, .03f * durationMult, 4500, 1010); // top wall  //Plugin.Log.Info($"Wall EXTENSION Hi Lt: Time: {wallTime}, Index:{-indexx}, Layer: {hiLayer}, Dur: {duration}, Width: {hiWidth1}, Height: {height1}");
                        generatedExtensionWalls.Add(customObsData);
                        //Plugin.Log.Info($"[TunnelWalls] TOP Time: {newWallStartTime:F2}, line: 0, Layer: {topLineLayer}, Width: 4500, Height: 1010");

                        tunnelCount++;
                    }
                    // left wall
                    if (alreadyHasGenWall != "left" && alreadyHasBigWall != "left")
                    {
                        EObstacleData customObsData = EObstacleData.Create(newWallStartTime, -leftLineIndex, 0, .03f * durationMult, 1010, height1); // left wall //Plugin.Log.Info($"Wall EXTENSION Hi Lt: Time: {wallTime}, Index:{-indexx}, Layer: {hiLayer}, Dur: {duration}, Width: {hiWidth1}, Height: {height1}");
                        generatedExtensionWalls.Add(customObsData);
                        //Plugin.Log.Info($"Tunnel Wall: Time: {newWallStartTime:F2}, line: {-leftLineIndex}, Layer: 0, Width: 1010, Height: {height1}");

                        if (wallCount == 2)// || wallCount == 3)
                        {
                            customObsData = EObstacleData.Create(newWallStartTime, -leftLineIndex, layer2, .03f * durationMult, 1010, height1); // left wall //Plugin.Log.Info($"Wall EXTENSION Hi Lt: Time: {wallTime}, Index:{-indexx}, Layer: {hiLayer}, Dur: {duration}, Width: {hiWidth1}, Height: {height1}");
                            generatedExtensionWalls.Add(customObsData);
                            //Plugin.Log.Info($"[TunnelWalls] Left Time: {newWallStartTime:F2}, line: {-leftLineIndex}, Layer: {layer2}, Width: 1010, Height: {height1}");

                            tunnelCount++;
                        }

                    }
                    // right wall
                    if (alreadyHasGenWall != "right" && alreadyHasBigWall != "right")
                    {
                        EObstacleData customObsData = EObstacleData.Create(newWallStartTime, rightLineIndex, 0, .03f * durationMult, 1010, height1); // right wall //Plugin.Log.Info($"Wall EXTENSION Hi Lt: Time: {wallTime}, Index:{-indexx}, Layer: {hiLayer}, Dur: {duration}, Width: {hiWidth1}, Height: {height1}");
                        generatedExtensionWalls.Add(customObsData);



                        if (wallCount == 2)// || wallCount == 3)
                        {
                            customObsData = EObstacleData.Create(newWallStartTime, rightLineIndex, layer2, .03f * durationMult, 1010, height1); // right wall //Plugin.Log.Info($"Wall EXTENSION Hi Lt: Time: {wallTime}, Index:{-indexx}, Layer: {hiLayer}, Dur: {duration}, Width: {hiWidth1}, Height: {height1}");
                            generatedExtensionWalls.Add(customObsData);
                            //Plugin.Log.Info($"[TunnelWalls] Right Time: {newWallStartTime:F2}, line: {rightLineIndex}, Layer: {layer2}, Width: 1010, Height: {height1}");

                            tunnelCount++;
                        }
                    }
                    // Update lastTunnelWallTime to the latest end time of the walls added
                    lastTunnelWallTime = newWallEndTime;
                }
                //Plugin.Log.Info($"Tunnel Walls wallTime:{wallTime} i:{i} j:{j} wallCount:{wallCount}");
            }
            tunnelWallsHappening = false;
        }

        private static void WindowPaneWalls(float wallTime, string alreadyHasGenWall,
            string alreadyHasBigWall, int width, int[] windowPaneWallLineLayer, int[] windowPaneWallLineIndex,
            int numberOfColumns)
        {
            paneWallsHappening = true;

            //int width = windowPaneWallSize[(int)wallTime % windowPaneWallSize.Length];// using HASH caused only 2 options to be selected always from entire list  same for every song !!!!
            int layer = windowPaneWallLineLayer[(int)wallTime % windowPaneWallLineLayer.Length]; // using wallTime works but causes output to increase to next value each time!!!
            int indexAdjust = windowPaneWallLineIndex[(int)wallTime % windowPaneWallLineIndex.Length];

            if (width > 3 && layer < 4) // large panes should be high up
                layer += 3;

            int height1 = (int)(width * 1.5 * 1000) + 1000;

            int leftLineIndex = (int)Config.Instance.WindowPaneWallsMinDistance + width + indexAdjust; // 2500 default which is 1 (2.5) away basically from player
            int rightLineIndex = (int)Config.Instance.WindowPaneWallsMinDistance + 4 + indexAdjust; // 5000 default which is 1 away basically from player

            int windowPaneCount = Math.Max(numberOfColumns, 6);// * 2, 6);

            string levelName = TransitionPatcher.SelectedSerializedName;

            //v1.42 reduce for standard levels since makes so many repeating walls
            if (levelName != GameModeHelper.GENERATED_360DEGREE_MODE && levelName != "360Degree" && levelName != "90Degree")
                windowPaneCount /= 3;


            windowPaneTallToggle = (windowPaneTallToggle + 1) % 3; // 1 in 3 times

            if (windowPaneTallToggle == 0)
            {
                leftLineIndex = (int)Config.Instance.WindowPaneWallsMinDistance + 1;
                rightLineIndex = (int)Config.Instance.WindowPaneWallsMinDistance + 4;
                layer = 0;
                width = 1;
                height1 = 5;
            }

            for (int j = 0; j <= windowPaneCount; j++)
            {
                float newWallStartTime = wallTime + (j * .1f);//0.07f);
                float newWallEndTime = newWallStartTime + .0002f;  // Assuming duration is the length of the wall in time


                // since walls are adding into the future from wallTime, the next loop may likely overlap walls so avoid this
                if (newWallStartTime > lastWindowPaneWallTime && newWallStartTime < _endTime)
                {
                    if (alreadyHasGenWall != "left" && alreadyHasBigWall != "left") // left wall
                    {
                        //Plugin.Log.Info($"Window Panes left - Time: {newWallStartTime}, Index:{-leftLineIndex}, Layer: {layer}, Dur: .0000001f, Width: {width}, Height: {height1}");
                        EObstacleData customObsData = EObstacleData.Create(newWallStartTime, -leftLineIndex, layer, .000001f, width, height1); // left wall //Plugin.Log.Info($"Wall EXTENSION Hi Lt: Time: {wallTime}, Index:{-indexx}, Layer: {hiLayer}, Dur: {duration}, Width: {hiWidth1}, Height: {height1}");
                        generatedExtensionWalls.Add(customObsData);

                        paneCount++;

                    }
                    if (alreadyHasGenWall != "right" && alreadyHasBigWall != "right") // right wall
                    {
                        //Plugin.Log.Info($"Window Panes right - Time: {newWallStartTime}, Index:{rightLineIndex}, Layer: {layer}, Dur: .0000001f, Width: {width}, Height: {height1}");
                        EObstacleData customObsData = EObstacleData.Create(newWallStartTime, rightLineIndex, layer, .0000001f, width, height1); // right wall //Plugin.Log.Info($"Wall EXTENSION Hi Lt: Time: {wallTime}, Index:{-indexx}, Layer: {hiLayer}, Dur: {duration}, Width: {hiWidth1}, Height: {height1}");
                        generatedExtensionWalls.Add(customObsData);

                        paneCount++;
                    }
                    // Update lastTunnelWallTime to the latest end time of the walls added
                    lastWindowPaneWallTime = newWallEndTime;
                }
                //Plugin.Log.Info($"Window Pane Walls wallTime:{newWallStartTime} j:{j} indexAdjust: {indexAdjust} layer: {layer} width: {width} height: {height1}");
            }
            paneWallsHappening = false;
        }

        public static void ParticleWalls(int repeatLimit = -1) // not using wallTime so not on the beat. if don't send in a repeatLimit, then will default to the user set ParticleWallsBatchSize
        {
            if (Config.Instance.EnableParticleWalls)
            {
                int divisorOne = Math.Max((int)Math.Round(3 / ParticleWallsMultiplier), 1); // Use Math.Round: Ensure that you round the result of your division to get meaningful divisors for the modulo operation.This avoids erroneous behavior from using floating-point division directly in integer contexts. Check for Zero Divisor: Ensure that the divisor does not round to zero, as dividing by zero will throw an exception.
                int divisorTwo = Math.Max((int)Math.Round(5 / ParticleWallsMultiplier), 1);
                int divisorThr = Math.Max((int)Math.Round(8 / ParticleWallsMultiplier), 1);

                if (repeatLimit == -1)
                    repeatLimit = (int)Config.Instance.ParticleWallsBatchSize;

                float timeBase = .25f;

                float time = _startTime + 1f; // Start time

                int cycleIndex = 0; // To cycle through predefined values

                while (time <= _endTime)
                {
                    if ((int)(time) % divisorOne == 0 || (int)(time) % divisorTwo == 0 || (int)(time) % divisorThr == 0)
                    {
                        //Plugin.Log.Info($"Wall particle Start Time: {time}");
                        int repeatCount = 1 + ((int)time % repeatLimit); // Results in a value between 1 and 40

                        int constantSize = 3 + (int)(Math.Abs(Math.Sin(time) * 14));

                        for (int repeat = 0; repeat < repeatCount; repeat++)
                        {
                            // Use a sine function to determine particle count for a semblance of randomness

                            int particlesCount = 2 + (int)(Math.Abs(Math.Sin(time) * 11)); // Results in a value between 2 and 13

                            for (int j = 0; j < particlesCount; j++)
                            {
                                cycleIndex = (cycleIndex + 1) % 4; // Cycle through 4 different sets of values

                                if (time < _endTime)
                                    AddParticleWall(time, constantSize, j);
                            }
                            if (particlesCount < 4)
                                time += timeBase;
                            else if (particlesCount < 8)
                                time += timeBase + .1f;
                            else
                                time += timeBase + .15f;

                        }
                        //Plugin.Log.Info($"Wall particle End Time: {time}");
                    }
                    else
                    {
                        time += timeBase;
                    }

                    // Use a modulus to pseudo-randomly determine pause duration
                    time += 1 + (int)(time) % 7; // Pause for 1 to 9 seconds
                }
                Plugin.LogDebug($"[WallGenerator][ParticleWalls] Wall particle Count: {particleWalls.Count}");

                if (particleWalls.Count > 0 && IsMappingExtensionsInstalled)
                    ExtensionMappingWallsGenerated = true;
            }
            //return (leftParticles, rightParticles);
        }

        private static void AddParticleWall(float time, int i, int j)
        {
            //int width = 1; int height = 1; float duration = 0.03f;

            int minDis = (int)Config.Instance.ParticleWallsMinDistance;

            int[] widthAndHeight = { 1050, 1010, 1300, 1300, 1300, 1500, 2500, 1300, 1400, 1100, 1500, 1200 }; // { 1100, 1200, 1300, 1500, 1700, 1, 2500 }; // 1100 = .1, 1700 = .7, 1 or 2000 = 1, 2500 = 1.5
            int[] widthAndHeight1 = { 2500, 1010, 1200, 1400, 1500, 1050, 1300, 1100, 1200, 1300, 1100 };
            int[] widthAndHeightNonME = { 1, 2, 1, 1, 1, 1, 2 };
            float[] dur = { .02f, .02f, .02f, .02f, 1f, .02f, .05f, .5f, 1f, .02f, .02f, .02f };
            float[] dur1 = { 1f, .02f, 1f, .02f, .05f, .02f, .02f, .02f, .02f, .03f, .02f, .5f };

            // Cycle through predetermined values instead of random generation
            int[] lineLayers = { 9, 1, 2, 7, 6, 8, 3, 4, 5 }; // { 1, 2, 3, 4, 5, 6, 7, 8, 9 }; // all line layers
            int[] lineLayersNonME = { 1, 2, 4, 6, 7, 3, 4, 8, 9, 2 }; //5 are ceiling tiles. 6 is short flat but ok. 7, 8, 9 get taller and taller 
            int[] lineIndexes1 = { 11, 3, -3, -8, 5, 7, 8, -9, 0, 4, -2, 13, -1, 9, -7, 6, 1, 10, 3, -5, -6, 12, -4 }; // { -9, -8, -7, -6, -5, -4, -3, -2, -1, 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13 }; // all line indexes
            int[] lineIndexes2 = { -4, 13, -3, 5, 9, -9, 11, 6, -7, 7, 8, 12, -8, -1, -2, -5, 10, -6 }; // { -9, -8, -7, -6, -5, -4, -3, -2, -1, 5, 6, 7, 8, 9, 10, 11, 12, 13 }; // line indexes outside of the main area

            // Utilizing an oscillation function of wallTime for variability
            int variableLayer = (int)(Math.Sin(time + j) * 1000);

            int variableIndex1;
            int variableWHD;
            if (IsMappingExtensionsInstalled)
            {
                if (j % 2 == 0)
                {
                    variableIndex1 = (int)(Math.Sin(time) * 1000) + j;
                    variableWHD = (int)(Math.Sin(time) * 1000) + i;
                }
                else
                {
                    variableIndex1 = (int)(Math.Cos(time) * 1000) + j;
                    variableWHD = (int)(Math.Sin(time) * 1000) + i;
                }
                if (i % 3 == 0)
                    widthAndHeight = widthAndHeight1;
            }
            else
            {
                variableIndex1 = TransitionPatcher.RepeatableRandom.Next(-11, 12) + j;
                variableWHD = TransitionPatcher.RepeatableRandom.Next(-11, 12) + i;
                widthAndHeight = widthAndHeightNonME;
            }

            if (i % 5 == 0)
                dur = dur1;

            // Calculating cycle indexes
            int cycleIndexForLineLayer = Math.Abs(variableLayer) % lineLayers.Length;
            int layer = lineLayersNonME[cycleIndexForLineLayer] + minDis;
            if (IsMappingExtensionsInstalled)
            {
                layer = lineLayers[cycleIndexForLineLayer] + minDis;
                if (layer == 5) layer = 6; // never 5. that is a ceiling tile for floor walls
            }

            int cycleIndexForLineIndex;
            int line;
            if (layer <= 4) // if low wall then skip area around player
            {
                cycleIndexForLineIndex = Math.Abs(variableIndex1) % lineIndexes2.Length;
                line = lineIndexes2[cycleIndexForLineIndex];
            }
            else // if high wall then can be anywhere left, right or above player
            {
                cycleIndexForLineIndex = Math.Abs(variableIndex1) % lineIndexes1.Length;
                line = lineIndexes1[cycleIndexForLineIndex];
            }
            if (line < 2)
                line -= (int)minDis;
            else
                line += (int)minDis;

            int cycleIndexForWidthHeight = Math.Abs(variableWHD) % widthAndHeight.Length;
            int cycleIndexForDuration = Math.Abs(variableWHD) % dur.Length;

            int widthHeight = widthAndHeight[cycleIndexForWidthHeight];
            float duration = dur[cycleIndexForDuration];

            if (widthHeight > 1300 || widthHeight == 1 || widthHeight == 2) // fatter particle walls
            {
                //duration = .03f; 
                duration = TransitionPatcher.RepeatableRandom.Next(1, 3) * .01f; // should be short since long ones look uncool IMO
                if (line < 0)
                    line -= 1; // fatter particle wall on the left are getting closer to the player so move them further out
            }
            if (IsMappingExtensionsInstalled)
            {
                if (duration >= 1)
                    widthHeight = 1100;
                else if (duration == .5f)
                    widthHeight = Math.Min(widthHeight, 1100);
            }

            if (!Config.Instance.EnableLargeParticleWalls && widthHeight > 1700)
                widthHeight = 1500;

            //Plugin.Log.Info($"---Variable Index: {Math.Abs(variableIndex1)} widthHeight: {widthHeight}");
            if (!IsMappingExtensionsInstalled)
            {
                if (!NonMEWallAllowed(
                    time,
                    line,
                    widthHeight,
                    ref _particleNonMeLastRowTime,
                    _particleNonMeOccupiedCols,
                    "ParticleWalls"))
                {
                    // Do NOT add this particle wall; it would touch an existing one.
                    return;
                }
            }


            EObstacleData customObsData = EObstacleData.Create(time, line, layer, duration, widthHeight, widthHeight); // lineLayer 4 & lower square. 5 is flat no height. 6 is normal 7 is very tall, 9 is long thin taller even

            if (TransitionPatcher.RequiresNoodle)
                customObsData = ConvertToNoodleWall(customObsData);

            //Plugin.Log.Info($"[ParticleWalls] Time: {time:F}, Line:{lineIndex}, Layer: {lineLayer}, Dur: {duration}, Width/Height: {widthHeight}");

            //Plugin.Log.Info($"Wall EXTENSION Lo Rt: Time: {wallTime}, Index:{indexx}, Layer: {loLayer}, Dur: {duration}, Width: {loWidth}, Height: {height1}");

            particleWalls.Add(customObsData);
            //Plugin.Log.Info($"[ParticleWalls] Time: {time:F}, Line:{line}, Layer: {layer}, Dur: {duration}, Width/Height: {widthHeight}");
        }

        public static void FloorWalls(List<TimeGap> gaps, int repeatLimit = -1) // not using wallTime so not on the beat
        {
            if (Config.Instance.EnableFloorWalls)
            {
                _generatedSkyCells.Clear();  // reset for this map/difficulty

                int divisorOne = Math.Max((int)Math.Round(4 / FloorWallsMultiplier), 1); // Use Math.Round: Ensure that you round the result of your division to get meaningful divisors for the modulo operation.This avoids erroneous behavior from using floating-point division directly in integer contexts. Check for Zero Divisor: Ensure that the divisor does not round to zero, as dividing by zero will throw an exception.
                int divisorTwo = Math.Max((int)Math.Round(6 / FloorWallsMultiplier), 1);
                int divisorThr = Math.Max((int)Math.Round(9 / FloorWallsMultiplier), 1);

                if (repeatLimit == -1)
                    repeatLimit = (int)Config.Instance.FloorWallsBatchSize;

                float timeBase = .1f;// .25f;

                float time = _startTime + 1f;//data.allBeatmapDataItems.OfType<ENoteData>().First().time + 1f; // Start time
                float lastWallAdded = time - 1; // Initialize to ensure it is less than `time` - this helps make sure the loops don't overlap in time allowing the multiple types of floor walls to overlap


                int[] lineIndexes1 = { -8, -6, -4, -2, 0, 2, 4, 6, 8, 10 }; // narrow
                int[] lineIndexes2 = { -14, -12, -10, -8, -6, -4, -2, 0, 2, 4, 6, 8, 10, 12, 14, 16, 18 }; //wide

                int cycleIndex = 0; // To cycle through predefined values

                while (time <= _endTime)
                {
                    if (time > lastWallAdded && ((int)(time) % divisorOne == 0 || (int)(time) % divisorTwo == 0 || (int)(time) % divisorThr == 0))
                    {
                        ToggleCityScape = (ToggleCityScape + 1) % 3; // 1 in 3 times
                        ToggleSpires = (ToggleSpires + 1) % 4;
                        ToggleSmall = (ToggleSmall + 1) % 5;

                        //Plugin.Log.Info($"--Floor Wall Start Time: {time}");

                        int repeatCount = 1 + ((int)time % repeatLimit); // Results in a value between 1 and 40

                        int lineIndexList = TransitionPatcher.RepeatableRandom.Next(1, 3); // between 1 and 2 since -- >= minValue, < maxValue

                        for (int repeat = 0; repeat < repeatCount; repeat++) // groups of particles at different times
                        {
                            HashSet<int> usedLineIndexes = new HashSet<int>(); // To store used lineIndex values

                            // Use a sine function to determine particle count for a semblance of randomness
                            int particlesCount = 2 + (int)(Math.Abs(Math.Sin(time) * 21)); // Results in a value between 2 and 23 (was 13)
                            //Plugin.Log.Info($"Floor Walls all at same time {time}:");

                            for (int j = 0; j < particlesCount; j++) // particles all at same time
                            {
                                int randomListIndex;
                                int lineIndex;

                                if (lineIndexList == 1)
                                {
                                    randomListIndex = TransitionPatcher.RepeatableRandom.Next(0, lineIndexes1.Length);
                                    lineIndex = lineIndexes1[randomListIndex];
                                }
                                else
                                {
                                    randomListIndex = TransitionPatcher.RepeatableRandom.Next(0, lineIndexes2.Length);
                                    lineIndex = lineIndexes2[randomListIndex];
                                }

                                if (lineIndex < 2)
                                    lineIndex -= (int)Config.Instance.FloorWallsMinDistance;
                                else
                                    lineIndex += (int)Config.Instance.FloorWallsMinDistance;

                                if (usedLineIndexes.Contains(lineIndex))
                                {
                                    //Plugin.Log.Info($"---Floor Wall Skipped for same index: {time} i: {lineIndex} using list: {lineIndexList}");
                                    continue;
                                }
                                else
                                {
                                    //Plugin.Log.Info($"---Floor Wall added: {time} i: {lineIndex} using list: {lineIndexList}");
                                    usedLineIndexes.Add(lineIndex);
                                }

                                cycleIndex = (cycleIndex + 1) % 4; // Cycle through 4 different sets of values

                                if (time < _endTime) 
                                {
                                    AddFloorWall(time, j, gaps, lineIndex, lineIndexList == 1 ? lineIndexes1 : lineIndexes2, new HashSet<int>());
                                    //Plugin.LogDebug($"[FloorWalls] Added: {time:F}");
                                }

                                lastWallAdded = time + .08f; // i added the longest possible duration .08f
                            }
                            if (particlesCount < 4)
                                time += timeBase;
                            else if (particlesCount < 8)
                                time += timeBase + .1f;
                            else
                                time += timeBase + .15f;
                        }
                        //Plugin.Log.Info($"--Floor Wall End Time: {time}");
                    }
                    else
                    {
                        time += timeBase;
                    }

                    // Use a modulus to pseudo-randomly determine pause duration
                    time += 1 + (int)(time) % 7; // Pause for 1 to 7 seconds
                }
                Plugin.LogDebug($"[WallGenerator][FloorWalls] Count: {floorWalls.Count}");

                if (floorWalls.Count > 0 && IsMappingExtensionsInstalled)
                    ExtensionMappingWallsGenerated = true;
            }
        }
        private static void AddFloorWallOld(float time, int j, List<TimeGap> gaps, int lineIndex, int[] activeLineList, HashSet<int> generatedSkyRows)
        {
            // floor walls
            float[] dur = { .04f, .02f, .02f, .08f };
            int[] widths = { 1900, 2900, 1400 };//, { 2000, 3000 };
            int[] heights = { 1001 };

            int height = 1001;

            // skyscraper walls
            if (ToggleCityScape == 0 || ToggleSpires == 0)
            {
                dur = new float[] { .02f };
                heights = new int[] { 1200, 1400, 1400, 1800, 1800, 2000, 2000, 2200, 2600, 3000 };

                height = heights[TransitionPatcher.RepeatableRandom.Next(heights.Length)];
            }

            if (lineIndex < 2)
                lineIndex -= (int)Config.Instance.FloorWallsMinDistance;
            else
                lineIndex += (int)Config.Instance.FloorWallsMinDistance;

            int variableWidths;

            if (time % 3 == 0)
            {
                variableWidths = (int)(Math.Sin(time) * 1000) + j;
            }
            else
                variableWidths = (int)(Math.Cos(time) * 1000) + j;

            int cycleIndexForWidth = Math.Abs(variableWidths) % widths.Length;
            int cycleIndexForDuration = Math.Abs(variableWidths) % dur.Length;

            int width = widths[cycleIndexForWidth];

            float duration = dur[cycleIndexForDuration];

            if (width <= 1500 && duration > .04f) // tiny walls should have short duration
                duration = .04f;

            int minDis = (int)Config.Instance.FloorWallsMinDistance;

            if (ToggleCityScape == 0)
            {
                if (height < 1500)
                    width = (int)(height * 1.5f);
                else
                    width = (int)(height * .66f);

                if (width <= 1000)
                    duration /= 8f;
                else if (width >= 1800)
                    duration *= 1.3f;

            }
            else if (ToggleSpires == 0)
            {
                height = (int)(height * 1.5f);
                width = 1300;
                duration /= 2.5f;
                //Plugin.Log.Info($"-- Wall Floor: SPIRES! Time: {time}");
            }
            else if (ToggleSmall == 0)
            {
                //Plugin.Log.Info($"Small Floor time: {time}");
                width = (width - 1000) / 2 + 1000; // this will half the size
                if (width > 1700)
                    width = (width - 1000) / 2 + 1000;
                duration /= 2.5f;
            }

            //Plugin.Log.Info($"---Variable Index: {Math.Abs(variableIndex1)} widthHeight: {widthHeight}");
            EObstacleData customObsData = EObstacleData.Create(time, lineIndex, 0, duration, width, height); // lineLayer 4 & lower square. 5 is flat no height. 6 is normal 7 is very tall, 9 is long thin taller even

            if (TransitionPatcher.RequiresNoodle)
                customObsData = ConvertToNoodleWall(customObsData);

            //Plugin.Log.Info($"-- Wall Floor: Time: {time}, Index:{lineIndex}, Layer: 0, Dur: {duration}, Width: {width}, Height: {height}");
            if (ToggleCityScape == 0 || ToggleSpires == 0)
            {
                //Plugin.Log.Info($"Wall EXTENSION Lo Rt: Time: {wallTime}, Index:{indexx}, Layer: {loLayer}, Dur: {duration}, Width: {loWidth}, Height: {height1}");
                if (lineIndex < -minDis || lineIndex > 3 + minDis)
                    generatedExtensionWalls.Add(customObsData); // these are standard walls not floor walls really
            }
            else
            {
                //Plugin.Log.Info($"Wall EXTENSION Lo Rt: Time: {wallTime}, Index:{indexx}, Layer: {loLayer}, Dur: {duration}, Width: {loWidth}, Height: {height1}");
                floorWalls.Add(customObsData);
            }

            if (gaps.Count == 0 || gaps.Any(g => g.WithinGap(time))) // sky walls
            {
                int layer = 8;// 8500
                int heightSky = 1001;
                customObsData = EObstacleData.Create(time, lineIndex, layer, duration, width, heightSky); // lineLayer 4 & lower square. 5 is flat no height. 6 is normal 7 is very tall, 9 is long thin taller even
                //Plugin.Log.Info($"-- Wall Floor: Time: {time}, Index:{lineIndex}, Layer: 0, Dur: {duration}, Width: {width}, Height: 1001");

                Plugin.LogDebug($"[WallFloor] SkyTile - Time: {time:F3}, Line:{lineIndex}, Layer: {layer}, Dur: {duration}, Width: {width}, Height: {heightSky}");
                floorWalls.Add(customObsData);

            }
            lastHeight = height;

        }


        private static void AddFloorWall(float time, int j, List<TimeGap> gaps, int line, int[] activeLineList, HashSet<int> generatedSkyRows)
        {
            // floor walls
            float[] dur = { .04f, .02f, .02f, .08f };
            int[] widths = { 1900, 2900, 1400 };//, { 2000, 3000 };
            int[] heights = { 1001 };
            //int[] lineIndexes1 = { -8, -6, -4, -2, 0, 2, 4, 6, 8, 10 }; // narrow
            //int[] lineIndexes2 = { -14, -12, -10, -8, -6, -4, -2, 0, 2, 4, 6, 8, 10, 12, 14, 16, 18 }; //wide

            int height = 1001;

            // skyscraper walls
            if (IsCityScapeMode || IsSpiresMode)
            {
                dur = new float[] { .02f };
                heights = new int[] { 1200, 1400, 1400, 1800, 1800, 2000, 2000, 2200, 2600, 3000 };

                if (!IsMappingExtensionsInstalled)
                {
                    heights = new int[] { 1, 2, 3, 1, 1, 2, -1, -2, -3 }; // negative without ME forces partially below floor and is longer the gr
                    widths = new int[] { 1, 2, 1 }; // not using since 2 is too wide sometimes
                }

                height = heights[TransitionPatcher.RepeatableRandom.Next(heights.Length)];
            }

            int minDis = (int)Config.Instance.FloorWallsMinDistance;

            if (line < 2)
                line -= minDis;
            else
                line += minDis;

            int variableWidths;
            if (IsMappingExtensionsInstalled)
            {
                if (time % 3 == 0)
                {
                    variableWidths = (int)(Math.Sin(time) * 1000) + j;
                }
                else
                    variableWidths = (int)(Math.Cos(time) * 1000) + j;
            }
            else
            {
                variableWidths = 1;
            }


            int cycleIndexForWidth = Math.Abs(variableWidths) % widths.Length;
            int cycleIndexForDuration = Math.Abs(variableWidths) % dur.Length;

            int width = widths[cycleIndexForWidth];

            float duration = dur[cycleIndexForDuration];

            if (width <= 1500 && duration > .04f) // tiny walls should have short duration
                duration = .04f;

            bool skip = false;



            if (IsMappingExtensionsInstalled)
            {
                if (IsCityScapeMode)
                {
                    if (height < 1500)
                        width = (int)(height * 1.5f);
                    else
                        width = (int)(height * .66f);

                    if (width <= 1000)
                        duration /= 8f;
                    else if (width >= 1800)
                        duration *= 1.3f;
                }
                else if (IsSpiresMode)
                {
                    height = (int)(height * 1.5f);
                    width = 1300;
                    duration /= 2f;
                    //Plugin.Log.Info($"-- Wall Floor: SPIRES! Time: {time}");
                }
                else if (IsSmallMode)
                {
                    //Plugin.Log.Info($"Small Floor time: {time}");
                    width = (width - 1000) / 2 + 1000; // this will half the size
                    if (width > 1700)
                        width = (width - 1000) / 2 + 1000;
                    duration /= 2.5f;
                }
            }
            else
            {
                if (!NonMEWallAllowed(time, line, width, ref _floorNonMeLastRowTime, _floorNonMeOccupiedCols, "FloorWalls"))
                    skip = true;
            }

            //Plugin.Log.Info($"---Variable Index: {Math.Abs(variableIndex1)} widthHeight: {widthHeight}");

            EObstacleData customObsData = EObstacleData.Create(time, line, 0, duration, width, height); // lineLayer 4 & lower square. 5 is flat no height. 6 is normal 7 is very tall, 9 is long thin taller even


            if (TransitionPatcher.RequiresNoodle)
                customObsData = ConvertToNoodleWall(customObsData);



            //Plugin.Log.Info($"-- Wall Floor: Time: {time}, Index:{lineIndex}, Layer: 0, Dur: {duration}, Width: {width}, Height: {height}");

            if (IsSpiresMode || IsCityScapeMode)
            {
                //Plugin.Log.Info($"[FloorWalls] Rt: Time: {time:F}, Index:{lineIndex}, Layer: {0}, Dur: {duration}, Width: {width}, Height: {height}");
                if ((line < -minDis || line > 3 + minDis) && !skip)
                    generatedExtensionWalls.Add(customObsData); // these are standard walls not floor walls really
            }
            else if (IsMappingExtensionsInstalled) // floor matte walls!
            {
                //Plugin.Log.Info($"Wall EXTENSION Lo Rt: Time: {wallTime}, Index:{indexx}, Layer: {loLayer}, Dur: {duration}, Width: {loWidth}, Height: {height1}");
                floorWalls.Add(customObsData);
            }

            // SKY TILES (happen during large gaps in notes so off beat)
            if (gaps.Count == 0 || gaps.Any(g => g.WithinGap(time)))
            {
                bool skyTileV1 = (int)time % 3 == 0;
                bool skyTileV2Type1 = (int)time % 2 == 0;
                int durMult = TransitionPatcher.RepeatableRandom.Next(2) + 1;

                if (!skyTileV1) // Random style placement
                {
                    int skyWidth = IsMappingExtensionsInstalled ? 1500 : 1;
                    int skyLayer = IsMappingExtensionsInstalled ? 8 : 5; //8500
                    int skyHeight = IsMappingExtensionsInstalled ? 1001 : 1;

                    skyLayer += minDis;

                    float widthUnits = GetWidthUnits(skyWidth, IsMappingExtensionsInstalled);

                    // “Square” duration in seconds (optionally *0.9f to create a gap)
                    float skyDuration = SquareDurationSeconds(widthUnits) * 0.9f;

                    customObsData = EObstacleData.Create(time, line, skyLayer, skyDuration, skyWidth, skyHeight);
                    floorWalls.Add(customObsData);
                    //Plugin.LogDebug($"[WallFloor] SkyTile v1 - Time: {time:F3}, Line:{line}, Layer: {skyLayer}, Dur: {skyDuration}, Width: {skyWidth}, Height: {skyHeight}");
                }
                else
                {
                    if (IsMappingExtensionsInstalled && skyTileV2Type1) // Random style placement
                    {
                        int layer = 7 + minDis; //8500
                        float dur1 = duration * durMult;
                        // your existing ME sky wall logic unchanged
                        customObsData = EObstacleData.Create(time, line, layer, dur1, width, 1001); //8500
                        floorWalls.Add(customObsData);

                        //Plugin.LogDebug($"[WallFloor] SkyTile v2 - Time: {time:F3}, Line:{line}, Layer: {layer}, Dur: {dur1}, Width: {width}, Height: 1001");
                    }
                    else // Orderly Looking Rows Style 
                    {
                        const float SKY_SPEED_SCALE = 0.70f;
                        const int skyBaseWidth = 1;

                        // njs should be the effective NJS for the current difficulty
                        float secondsPerUnit = 1f / MathF.Max(1f, (TransitionPatcher.FinalNoteJumpMovementSpeed * SKY_SPEED_SCALE));
                        float tileDur = secondsPerUnit * skyBaseWidth;

                        // Row spacing: >1 means a gap between rows; =1 means rows touch in time.
                        const float rowGapFactor = 1.10f;
                        float rowPeriod = tileDur * rowGapFactor;

                        // Convert this wall's time to a stable "row cell" on the sky grid
                        int cell = (int)MathF.Floor((time + 0.0001f) / rowPeriod);

                        // Guard: if we already generated this row cell, do nothing
                        if (generatedSkyRows.Contains(cell))
                            return;

                        generatedSkyRows.Add(cell);

                        // Quantized row time for this cell
                        float rowTime = cell * rowPeriod;

                        // Create the full fairly ordered row of sky tiles for this bucket
                        AddOrderlySkyRowForTime(rowTime, floorWalls);

                        //Plugin.LogDebug($"[WallFloor] SkyTile v2 - Time:{rowTime:F3}, Idx:{idx}, Width:{width1}, Dur:{tileDur:F3}");
                    }
                }
            }
            //Plugin.LogDebug($"[WallFloor] SkyLine Tile v3 - Time: {(row * cellTime):F3}, Index:{skyLine}, Layer: {skyLayer}, Dur: {tileDur}, Width: {skyWidth}, Height: {skyHeight}");
            lastHeight = height;
        }


        private static float lastNonExtRowTime = float.NegativeInfinity;
        private static HashSet<int> occupiedColsForRow = new HashSet<int>();
        private static float lastTime = float.NegativeInfinity;
        private static int lastRightEdge = int.MinValue;
        // How "square" looks on screen for width = 1.
        // Tweak this by eye (0.03–0.05 is a good range).
        private const float SKY_BASE_TILE_DURATION = 0.04f;

        // Min / max width for sky tiles
        private const int SKY_MIN_WIDTH = 1;
        private const int SKY_MAX_WIDTH = 4;

        // >1 => gap in time between rows.
        // Rows are spaced based on *max width* to avoid time overlap.
        private const float SKY_ROW_GAP_FACTOR = 1.10f;

        // One row per "cell" in time
        private static readonly HashSet<int> _generatedSkyCells = new HashSet<int>();

        private static int GetSkyTileWidth(int cell, int idx)
        {
            // Simple deterministic hash using two primes
            int hash = cell * 73856093 ^ idx * 19349663;
            if (hash < 0) hash = -hash;

            int range = SKY_MAX_WIDTH - SKY_MIN_WIDTH + 1;
            return SKY_MIN_WIDTH + (hash % range);
        }

        private static void AddOrderlySkyRowForTime(float time, List<EObstacleData> floorWalls)
        {
            int skyLayer = IsMappingExtensionsInstalled ? 7 + (int)Config.Instance.FloorWallsMinDistance : 5;
            //const int floorLayer = 0;
            int skyHeight = IsMappingExtensionsInstalled ? 1001 : 1;

            int[] floorHeightX = { -2, -1 };

            // Longest possible tile (width = SKY_MAX_WIDTH)
            float maxTileDur = SKY_BASE_TILE_DURATION * SKY_MAX_WIDTH;

            // Row spacing in time: based on *max* tile duration so rows never overlap
            float rowPeriod = maxTileDur * SKY_ROW_GAP_FACTOR;

            // Quantize time into a row "cell"
            int cell = (int)MathF.Floor((time + 0.0001f) / rowPeriod);

            // Only generate once per cell
            if (!_generatedSkyCells.Add(cell))
                return;

            float rowTime = cell * rowPeriod;

            int[] minLineX = { -14, -7, -21, -31 };
            int[] maxLineX = { 18, 11, 25, 35 };

            int rnd = TransitionPatcher.RepeatableRandom.Next(minLineX.Length);
            int minLine = minLineX[rnd];
            int maxLine = maxLineX[rnd];

            int rnd1 = TransitionPatcher.RepeatableRandom.Next(floorHeightX.Length);
            int floorHeight = floorHeightX[rnd1];

            // Checkerboard-ish offset by row parity (optional, but looks nice)
            int start = minLine + ((cell & 1) == 1 ? 0 : 1);

            int idx = start;

            while (idx <= maxLine)
            {
                // Choose a width 1..SKY_MAX_WIDTH, deterministic but "random"
                int width = GetSkyTileWidth(cell, idx);

                // Do not exceed the allowed index range
                if (idx + width - 1 > maxLine)
                {
                    width = Math.Max(1, maxLine - idx + 1);
                }

                // Duration is proportional to width so tiles look square:
                // width = w → duration = w * SKY_BASE_TILE_DURATION
                float tileDur = SKY_BASE_TILE_DURATION * width;

                floorWalls.Add(EObstacleData.Create(
                    rowTime,
                    idx,
                    skyLayer,
                    tileDur,
                    width,
                    skyHeight));

                //Plugin.LogDebug($"SkyTile -   Time:{rowTime:F3}, Idx:{idx}, Layer: {skyLayer} Width:{width}, Height: {skyHeight} Dur:{tileDur:F3}");
                // 1-unit gap after each tile
                idx += width + 1;
            }
        }

        // Floor sky tiles (non-ME)
        private static float _floorNonMeLastRowTime = float.NegativeInfinity;
        private static readonly HashSet<int> _floorNonMeOccupiedCols = new HashSet<int>();

        // Particle walls (non-ME)
        private static float _particleNonMeLastRowTime = float.NegativeInfinity;
        private static readonly HashSet<int> _particleNonMeOccupiedCols = new HashSet<int>();


        // Shared helper for NON-ME walls (floors, particles, etc.)
        // Guarantees: in a given time row, no two walls come within 1 column of each other.
        // Returns true if placement is allowed; false if this wall should be skipped.
        private static bool NonMEWallAllowed(
            float time,
            int line,
            int widthUnits,
            ref float lastRowTime,
            HashSet<int> occupiedColsForRow,
            string debugTag)
        {
            const float TOL = 0.0005f;

            // New time row? Reset the occupancy
            if (Math.Abs(time - lastRowTime) > TOL)
            {
                lastRowTime = time;
                occupiedColsForRow.Clear();
            }

            int left = line;
            int right = line + widthUnits - 1; // inclusive

            // Enforce a 1-unit gap between walls on the same row:
            // previous wall occupies [L..R]; we forbid [L-1..R+1] for new walls
            for (int col = left - 1; col <= right + 1; col++)
            {
                if (occupiedColsForRow.Contains(col))
                {
                    //Plugin.Log.Debug($"[{debugTag}] Skipping (time={time:F3}, line={line}, width={widthUnits}) – " +$"touch/overlap with occupied column {col}");
                    return false; // reject this wall
                }
            }

            // Mark this wall's footprint as occupied
            for (int col = left; col <= right; col++)
            {
                occupiedColsForRow.Add(col);
            }

            return true;
        }


        static float GetWidthUnits(int width, bool isME)
        {
            if (!isME) return width;               // 1,2,3,...
            return (width - 1000) / 1000f;         // 1500->0.5, 2000->1, ...
        }
        static float SquareDurationSeconds(float widthUnits) // tries to set duration so will creates size dimension matching the width since a sky wall is time x width for its dimension
        {
            float njs = Math.Max(0.01f, TransitionPatcher.FinalNoteJumpMovementSpeed);
            return widthUnits / njs;
        }

        public static void MegaWalls(List<TimeGap> gaps) //without mapping extensions, height is restricted to 10 or less.
        {
            int divisor = 4;

            int maxPairCount = 4;  // only allow this many sets of walls

            // Tweakables
            const float StepSeconds = 0.75f;   // how often we "roll the dice" inside a gap
            //const float SpawnChance = 0.35f;   // probability per step to spawn a pair
            const float MinPairSpacing = 4.0f;    // minimum spacing between pairs (seconds)
            const float EdgeBuffer = 0.25f;   // keep walls away from gap edges

            //Plugin.Log.Info($"Mega Wall Gaps: {gaps.Count}");

            if (!Config.Instance.EnableBigWalls) return;
            if (gaps == null || gaps.Count == 0) return;

            // Deterministic RNG so the same seed yields the same mega walls for a given map.
            // Replace seed source as you like (e.g., hash of levelId / difficulty).
            //var rng = new System.Random(1337);

            // Optional: normalize/merge gaps if needed (assumes gaps sorted & merged already).
            // If not guaranteed, consider sorting by StartTime and merging overlaps first.

            float lastSpawnTime = float.NegativeInfinity;
            float noSpawnUntil = float.NegativeInfinity;
            float coolDownTime = 30f; // 45 seconds
            int pairCount = 0;

            foreach (var g in gaps)
            {
                // Valid window inside the gap with an "edge buffer"
                float windowStart = MathF.Max(g.StartTime + EdgeBuffer, _startTime);
                float windowEnd = MathF.Min(g.EndTime - EdgeBuffer, _endTime);


                if (windowEnd <= windowStart) continue; // too small after buffering

                if (noSpawnUntil > windowStart) windowStart = noSpawnUntil;

                // Walk forward in fixed steps
                for (float t = windowStart; t <= windowEnd; t += StepSeconds)
                {
                    // If we're still inside cooldown, fast-forward t once
                    if (t < noSpawnUntil)
                    {
                        t = noSpawnUntil;
                        if (t > windowEnd) break; // this gap is done
                    }

                    // Must still be inside the gap (in case of tight buffers)
                    if (!(t >= g.StartTime && t <= g.EndTime)) continue;

                    // Respect global spacing across all gaps
                    if (t - lastSpawnTime < MinPairSpacing) continue;

                    // Roll the dice
                    if ((int)t % divisor == 0)
                    {
                        // Create right and left mega walls at the same time
                        var right = EObstacleData.Create(t, 4, 0, .02f, 50, 50);
                        var left = EObstacleData.Create(t, -50, 0, .02f, 50, 50);
                        generatedStandardWalls.Add(right);
                        generatedStandardWalls.Add(left);

                        //Plugin.Log.Info($"Mega Wall Pair: {t:F}");

                        pairCount++;
                        lastSpawnTime = t;

                        // Skip ahead a bit so we don’t immediately re-check right after a spawn
                        // (optional; MinPairSpacing already guards this)
                        t += MathF.Max(0, MinPairSpacing - StepSeconds);

                        // Hit the current cap? Double it and set a 45s cooldown
                        if (pairCount >= maxPairCount)
                        {
                            maxPairCount += 3;       // double the allowed total
                            noSpawnUntil = t + coolDownTime;  // skip searching until this time
                                                              // Jump t to the cooldown end to avoid pointless iterations
                            if (noSpawnUntil > t) t = noSpawnUntil;
                        }

                    }
                }

                if (pairCount >= maxPairCount) break; // exit loop
            }

            Plugin.LogDebug($"[WallGenerator][MegaWalls] Mega Wall Pairs: {pairCount} (Total walls: {pairCount * 2})");
        }



        #endregion

        // mapping extensions precision width band is 1000-2000. 1000 = 0, 1001 = .001, ..., 2000 = 1
        // convert ME width to NE scale fraction
        /// <summary>
        /// Mapping Extension walls with width 1000-2000 (particle and floor walls only) will appear super wide instead of tiny in maps with Noodle Extensions. Must convert them to Noodle Walls with _scale customData
        /// This happens since Noodle Extensions precision wall scale will supercede the Mapping Extensions precision wall width. 
        /// </summary>
        /// <param name="obs"></param>
        /// <returns></returns>
        public static EObstacleData ConvertToNoodleWall(EObstacleData obs)
        {
            // Mapping Extensions precision width band 1000–2000
            // 1000 = 0, 2000 = 1
            float f = (obs.width - 1000f) / 1000f;
            float frac = MathF.Max(0f, MathF.Min(1f, f));

            //frac /= 1000f; doesn't help. still too wide. also tried _localScale but no help

            obs.width = 1;
            obs.customData["_scale"] = new List<object> { frac, 1f, 1f }; //v3 scale not _scale!

            return obs;
        }

        public static void LogWallCount(EditableCBD eData)
        {
            int origWallsCount = originalWalls.Count > 0 ? originalWalls.Count : eData.Obstacles.Count; // if wall gen is off, then _original walls are never populated. // Github Issue #2 Walls gone when using autolights
            Plugin.LogDebug(
                $"[WallGenerator][LogWallCount] Walls Before Rotational Removal Tools - in eData: Total: {eData.Obstacles.Count()} -- Original: {origWallsCount} Standard: {generatedStandardWalls.Count} Distant: {distantCount} Column: {columnCount} Row: {rowCount} Tunnel: {tunnelCount} Grid: {gridCount} Pane: {paneCount} Particle: {particleWalls.Count} Floor: {floorWalls.Count} ^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^");
        }



        // ------------ Wall Alter and Remove ---- They occur in this order ---------------------------------------------------------------------------------------------

        /// <summary>
        /// Removes Lean and Crouch Walls for Standard and 360/90 Degree Maps if EnableWallsGen... is enabled. Can turn off all walls individually and it still removes the lean and crouch walls
        /// </summary>
        public static bool LeanCrouchWallRemoval() // works with _originalWalls only. only will remove major lean walls that are covering 2 lineIndexes (0 and 1, or 2 and 3)
        {
            originalWalls.Sort((a, b) => a.time.CompareTo(b.time));

            //BW noodle extensions causes BS crash in the section somewhere below. Could drill down and figure out why. Haven't figured out how to test for noodle extensions but noodle extension have custom walls that crash Beat Saber so BW added test for custom walls.
            Queue<EObstacleData> obs = new Queue<EObstacleData>(originalWalls); // since removing items

            int removedWallCount = 0;

            while (obs.Count > 0)
            {
                EObstacleData ob = obs.Dequeue();

                if (ob.duration <= 0f) continue;

                if (!IsCustomNoodleWall(ob))
                {
                    if (TransitionPatcher.SelectedSerializedName == GameModeHelper.GENERATED_360DEGREE_MODE &&
                        ((ob.line == 1 || ob.line == 2) &&
                        ob.width == 1 &&
                        ob.layer < 3)) //Lean wall of width 1. Hard to see coming in 360 ---- these are removed even if user selects to allow lean walls!!!!!!!!!!!!!!!!!
                    {
                        //Plugin.Log.Info($"Remove Lean Wall of width 1: Time: {ob.time:F} cutTime: {cutTime}");
                        originalWalls.Remove(ob);
                        removedWallCount++;
                        continue;
                    }
                    else if (!Config.Instance.AllowLeanWalls &&
                             ((ob.line == 0 && ob.width == 2) || (ob.line == 2 && ob.width > 1)) &&
                             (int)ob.layer < 3) //Lean walls of width 2.
                    {
                        //Plugin.Log.Info($"Remove Lean Wall: Time: {ob.time } cutTime: {cutTime}");
                        originalWalls.Remove(ob);
                        removedWallCount++;
                        continue;
                    }
                    else if (!Config.Instance.AllowCrouchWalls &&
                             (ob.line == 0 && ob.width > 2 && ob.layer == 2)) //Crouch walls
                    {
                        //Plugin.Log.Info($"Remove Crouch Wall: Time: {ob.time:F} cutTime: {cutTime}");
                        originalWalls.Remove(ob);
                        removedWallCount++;
                        continue;
                    }
                }
            }
            originalWalls.Sort((a, b) => a.time.CompareTo(b.time));

            allWalls.AddRange(originalWalls); // update eData with the modified walls

            allWallsContainsOriginalWalls = true;

            Plugin.LogDebug($"[LeanCrouchWallRemoval] End --- Original Standard Wall Count: {originalWalls.Count} - Walls Removed: {removedWallCount} -- allWalls Count: {allWalls.Count}");

            if (removedWallCount > 0)
                return true;
            else
                return false;
        }

        public static bool MoveWallsBlockingChainTail(EditableCBD eData) // works with _originalWalls and _generatedStandardWalls only
        {
            List<ESliderData> chains = eData.Chains;
            int rotationEventsCount = eData.RotationEvents.Count;

            // For each chain, flag if a rotation occurs at its head or during the chain.
            (bool, float)[] chainHasRotation = new (bool hasRot, float amount)[chains.Count];

            if (rotationEventsCount > 1)
            {
                int rotIndex = 0;
                for (int i = 0; i < chains.Count; i++)
                {
                    var chain = chains[i];
                    // Advance past rotation events before the chain starts.
                    while (rotIndex < rotationEventsCount && eData.RotationEvents[rotIndex].time < chain.time)
                        rotIndex++;
                    if (rotIndex >= rotationEventsCount) rotIndex = rotationEventsCount - 1;
                    //v1.34
                    //float rotationAmount = rotIndex == 0 ? rotationEvents[rotIndex].rotation : (rotationEvents[rotIndex].rotation - rotationEvents[rotIndex - 1].rotation);
                    //v1.40
                    int rotationAmount1 = eData.RotationEvents[rotIndex].rotation;
                    int rotationAmount2 = (rotIndex > 0) ? eData.RotationEvents[rotIndex - 1].rotation : 0;
                    float rotationAmount = rotIndex == 0 ? rotationAmount1 : (rotationAmount1 - rotationAmount2);

                    // If the next rotation event occurs before or at the chain's tail, mark the chain.
                    chainHasRotation[i] = ((rotIndex < rotationEventsCount && eData.RotationEvents[rotIndex].time <= chain.tailTime), rotationAmount);
                }
            }

            int currentIndex = 0;         // start from the first chain for the first obstacle
            float minDistance = 0.2f;    // minimum distance between wall and chain was 0.15f

            if (!allWallsContainsOriginalWalls)
            {
                allWalls.AddRange(originalWalls);
                allWallsContainsOriginalWalls = true;
            }
            if (!allWallsContainsStandardWalls)
            {
                allWalls.AddRange(generatedStandardWalls);
                allWallsContainsStandardWalls = true;
            }

            allWalls.Sort((a, b) => a.time.CompareTo(b.time));

            Plugin.LogDebug($"[MoveWallsBlockingChainTail] Potentially ADJUSTING {allWalls.Count} Walls now for {chains.Count} chains.");

            int adjustedCount = 0;

            foreach (var ob in allWalls)
            {
                if (originalWalls.Contains(ob) && IsCustomNoodleWall(ob))
                    continue;

                for (int i = currentIndex; i < chains.Count; i++)
                {
                    // If the chain starts after the obstacle ends (with a margin), no further chains will match.
                    if (chains[i].time > ob.endTime + minDistance)
                        break;

                    // If the chain's tail is before the obstacle starts, skip to the next chain.
                    if (chains[i].tailTime < ob.time - minDistance)
                    {
                        currentIndex = i + 1;
                        continue;
                    }

                    // Compute new line index for the wall.
                    int newLineIndex = chains[i].tailLine;
                    int offset = 0;
                    if (rotationEventsCount > 0 && chainHasRotation[i].Item1)
                    {
                        int theOffset = Math.Abs(chainHasRotation[i].Item2) > 15 ? 18 : 15; //15 : 12;
                        // For chains with 30 degree rotation, offset it alot! since rotation still will instect the chain otherwise. less so for 15 degree rotation.
                        // Decide based on the wall's current lineIndex relative to the chain's tail.
                        if (ob.line <= chains[i].tailLine)
                        {
                            if (ob.line < 0)
                                offset = ob.line - theOffset; // if its a left big wall (with a lineIndex of -11 or so, need to offset it from original wall lineIndex so it will will stay further away from the chain
                            else
                                offset = -theOffset; // just offset it from chain tail index
                        }
                        {
                            offset = theOffset; // big walls don't matter on the right side
                        }
                    }
                    else
                    {
                        // For non-rotated chains, adjust only when there is an intersection.
                        if (ob.line >= chains[i].tailLine && ob.line < 2)
                            offset = -1;
                        else if (ob.line <= chains[i].tailLine && ob.line > 1)
                            offset = 1;
                    }

                    newLineIndex += offset;

                    // Only adjust if the new line index differs from the original tail line index.
                    if (newLineIndex != chains[i].tailLine)
                    {
                        ob.line = newLineIndex;
                        adjustedCount++;

                        //string rot = chainHasRotation.Count() > 0 ? $" - has rotation: {chainHasRotation[i].Item2}" : "";
                        //Plugin.Log.Info($" -- Chain {i} ADJUSTED wall at time {ob.time:F} w: {ob.width} (dur: {ob.duration:F}) for chain at {chains[i].time:F}. Old x: {ob.line}, new x: {newLineIndex} -- y: {ob.layer} ");
                    }
                }
            }
            Plugin.LogDebug($"[MoveWallsBlockingChainTail] ADJUSTED {adjustedCount} Walls.");

            if (adjustedCount > 0)
                return true;
            else
                return false;
        }
        public static bool RemoveCrouchWallsBlockingChains(EditableCBD eData)
        {
            // If user globally disallows crouch walls, you are already removing them elsewhere.
            if (!Config.Instance.AllowCrouchWalls)
                return false;

            if (eData.Chains == null || eData.Chains.Count == 0 || originalWallCount == 0)
                return false;

            bool isBeatSageMap = TransitionPatcher.IsBeatSageMap;
            if (!isBeatSageMap && eData.MapAlreadyUsesChains) // only remove generated chains
                return false;

            //Plugin.LogDebug("[RemoveCrouchWallsBlockingChains] called...");

            // Define what a “crouch wall” is in your scheme
            bool IsCrouchWall(EObstacleData ob) =>
                ob.line == 0 &&            // starts at far left
                ob.width > 2 &&            // spans across center
                ob.width < 1000 &&         // not an ME precision monster
                ob.layer == 2;             // top layer (classic crouch)

            // Collect crouch walls from the obstacles actually in play
            var crouchWalls = originalWalls
                .Where(IsCrouchWall)
                .ToList();

            if (crouchWalls.Count == 0)
                return false;

            int removedCrouchWalls = 0;
            int removedChains = 0;

            foreach (var wall in crouchWalls)
            {
                float wallStart = wall.time;
                float wallEnd = wall.time + wall.duration;

                // Find all chains whose [headTime, tailTime] overlaps this wall.
                var blockingChains = eData.Chains
                    .Where(chain =>
                    {
                        float chainStart = chain.time;
                        float chainEnd = chain.tailTime;
                        // simple interval overlap test (non-touching is OK)
                        return chainEnd > wallStart && chainStart < wallEnd;
                    })
                    .ToList();

                if (blockingChains.Count == 0)
                    continue;

                if (isBeatSageMap)
                {
                    Plugin.LogDebug($"[RemoveCrouchWallsBlockingChains] -- Removed Crouch Wall blocking chains: {wall.time:F} Dur: {wall.duration:F}."); 
                    // Beat Sage: keep the chains, delete the crouch wall.
                    originalWalls?.Remove(wall);
                    allWalls?.Remove(wall);
                    removedCrouchWalls++;
                }
                else
                {
                    // Non-BeatSage: keep the crouch wall, delete the overlapping chains.
                    foreach (var chain in blockingChains)
                    {
                        if (chain.headNote != null)
                        {
                             chain.headNote.headNoteChain = null;
                             chain.headNote.scoringType   = NoteData.ScoringType.Normal;
                             chain.headNote.gameplayType  = NoteData.GameplayType.Normal;
                        }
                        Plugin.LogDebug($"[RemoveCrouchWallsBlockingChains] -- Removed Chain under crouch wall: {wall.time:F} Dur: {wall.duration:F} -- Chain: {chain.time:F}");

                        eData.Chains.Remove(chain);
                        removedChains++;
                    }
                }
            }
            if (removedCrouchWalls > 0)
                Plugin.LogDebug($"[RemoveCrouchWallsBlockingChains] removed {removedCrouchWalls} crouch walls (Beat Sage map)");
            else if (removedChains > 0)
                Plugin.LogDebug($"[RemoveCrouchWallsBlockingChains] removed {removedChains} chains (non-beat sage map)");

            if (removedCrouchWalls > 0) eData.ObstaclesChanged = true;

            return removedCrouchWalls > 0;
        }





        public static bool MoveWallsBlockingArc(EditableCBD eData)
        {
            // Retrieve all arcs (normal sliders) from the beatmap.
            // Here we assume that SliderData.Type.Normal represents an arc.

            int currentIndex = 0;            // Used to skip arcs that occur entirely before a wall.
            float minDistance = 0.15f;       // A small time buffer between wall and arc.

            // Ensure _allWalls includes both the original and generated standard walls.
            if (!allWallsContainsOriginalWalls)
            {
                allWalls.AddRange(originalWalls);
                allWallsContainsOriginalWalls = true;
            }
            if (!allWallsContainsStandardWalls)
            {
                allWalls.AddRange(generatedStandardWalls);
                allWallsContainsStandardWalls = true;
            }
            allWalls.Sort((a, b) => a.time.CompareTo(b.time));

            Plugin.LogDebug($"[MoveWallsBlockingArc] ADJUSTING {allWalls.Count} Walls now for {eData.Arcs.Count} arcs.");

            int adjustedWalls = 0;
            // Loop through each wall.
            foreach (var ob in allWalls)
            {
                if (originalWalls.Contains(ob) && IsCustomNoodleWall(ob))
                    continue;

                // For each wall, loop through arcs starting from currentIndex.
                for (int i = currentIndex; i < eData.Arcs.Count; i++)
                {
                    // If the arc’s head time is after this wall’s end time (with a margin), break.
                    if (eData.Arcs[i].time > ob.endTime + minDistance)
                    {
                        break;
                    }

                    // If the arc’s tail time is before the wall’s start time, this arc is fully before the wall.
                    // Update currentIndex and continue to the next arc.
                    if (eData.Arcs[i].tailTime < ob.time - minDistance)
                    {
                        currentIndex = i + 1;
                        continue;
                    }

                    // Now the arc’s time (from head to tail) overlaps with the wall’s duration.
                    // Calculate the lane range for the arc based on its head and tail note.
                    int arcMinIndex = Math.Min(eData.Arcs[i].line, eData.Arcs[i].tailLine);
                    int arcMaxIndex = Math.Max(eData.Arcs[i].line, eData.Arcs[i].tailLine);

                    // Check if the wall's lane (lineIndex) is within the arc's lane range.
                    if (ob.line >= arcMinIndex && ob.line <= arcMaxIndex)
                    {
                        int newLineIndex = ob.line;

                        // Determine whether the wall is closer to the left or right side of the arc.

                        if (ob.line < 2)
                        {
                            newLineIndex = arcMinIndex - 1;
                        }
                        else
                        {
                            newLineIndex = arcMaxIndex + 1;
                        }

                        // If we have a new valid lane index, create an adjusted obstacle.
                        if (newLineIndex != ob.line)
                        {
                            ob.line = newLineIndex;
                            adjustedWalls++;
                            //Plugin.Log.Info($" -- {i} ADJUSTED a wall intersecting an arc. Wall time: {ob.time:F}, width: {ob.width} layer: {(int)ob.lineLayer} dur: {ob.duration:F}; Arc head time: {arcs[i].time:F} index: {arcs[i].headLineIndex}, tail time: {arcs[i].tailTime:F} index: {arcs[i].tailLine}. Wall old lineIndex: {ob.lineIndex}, new lineIndex: {newLineIndex}");
                        }
                    }
                }
            }
            Plugin.LogDebug($"[MoveWallsBlockingArc] ADJUSTED {adjustedWalls} Walls.");
            if (adjustedWalls > 0)
                return true;
            else
                return false;   
        }

        public static void RemoveIntersectingWalls() // works with _originalWalls, _generatedStandardWalls and _generatedExtensionWalls only. Added this since adding extension walls created tons of intersecting walls
        {
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Restart();

            if (!allWallsContainsOriginalWalls)
            {
                allWalls.AddRange(originalWalls);
                allWallsContainsOriginalWalls = true;
            }
            if (!allWallsContainsStandardWalls)
            {
                allWalls.AddRange(generatedStandardWalls);
                allWallsContainsStandardWalls = true;
            }

            if (!allWallsContainsExtensionWalls)
            {
                allWalls.AddRange(generatedExtensionWalls);
                allWallsContainsExtensionWalls = true;
            }

            if (!IsMappingExtensionsInstalled && !allWallsContainsParticleWalls)
            {
                allWalls.AddRange(particleWalls);
                allWallsContainsParticleWalls = true;
            }
            if (!IsMappingExtensionsInstalled && !allWallsContainsFloorWalls)
            {
                allWalls.AddRange(floorWalls);
                allWallsContainsFloorWalls = true;
            }


            allWalls.Sort((a, b) => a.time.CompareTo(b.time));

            var leftWalls = new List<EObstacleData>();
            var rightWalls = new List<EObstacleData>();
            var obstaclesToDelete = new List<EObstacleData>();

            foreach (var obs in allWalls)
            {
                if (obs.line > 1) rightWalls.Add(obs);
                else leftWalls.Add(obs);
            }

            RemoveIntersectingWallsByList(leftWalls);
            RemoveIntersectingWallsByList(rightWalls);

            allWalls.Clear();

            allWalls.AddRange(leftWalls);
            allWalls.AddRange(rightWalls);

            foreach (var obs in obstaclesToDelete)
            {
                allWalls.Remove(obs);
            }

            Plugin.LogDebug($"[RemoveIntersectingWalls] --- Remaining Walls: {allWalls.Count} --- Total Removed: {obstaclesToDelete.Count}");

            //Plugin.LogDebug($" ------- Time Elapsed: {stopwatch.ElapsedMilliseconds / 1000.0:F1}.");
            stopwatch.Stop();

            void RemoveIntersectingWallsByList(List<EObstacleData> obs)
            {
                for (int i = 0; i < obs.Count; i++)
                {
                    if (stopwatch.ElapsedMilliseconds >= Config.Instance.MaxWaitTime * 1000)
                    {
                        Plugin.LogDebug($"[RemoveIntersectingWalls] End ------- WARNING -- TOOK TOO LONG so had to prematurely terminate!");
                        break;
                    }

                    EObstacleData currentObstacle = obs[i];

                    for (int j = i + 1; j < obs.Count; j++)
                    {
                        EObstacleData comparingObstacle = obs[j];

                        if (comparingObstacle.time > currentObstacle.endTime)
                        {
                            break;
                        }

                        // Check if the obstacles overlap in time, position, and grid space
                        (bool isOverlapping, bool isTouching, int targetWall) = IsOverlapping(currentObstacle, comparingObstacle);

                        if (isOverlapping)
                        {
                            // Determine which obstacle has the shorter duration and mark it for deletion
                            if (currentObstacle.duration <= comparingObstacle.duration)
                            {
                                if (!obstaclesToDelete.Contains(currentObstacle))
                                {
                                    obstaclesToDelete.Add(currentObstacle);
                                }
                            }
                            else if (currentObstacle.duration > comparingObstacle.duration)
                            {
                                if (!obstaclesToDelete.Contains(comparingObstacle))
                                {
                                    obstaclesToDelete.Add(comparingObstacle);
                                }
                            }
                            // If durations are equal, you could decide based on other criteria or leave them as is
                        }
                    }
                }
            }

            // Helper method to determine if two obstacles overlap
            (bool, bool, int) IsOverlapping(EObstacleData ob1, EObstacleData ob2)
            {

                // per kyle lineLayer and height -- 1000 is linelayer 0, 2500 should be linelayer 1.5 etc.
                int ob1X = 0; int ob1W = 0; int ob2X = 0; int ob1H = 0;
                int ob1Y = 0; int ob2W = 0; int ob2Y = 0; int ob2H = 0;

                if (Math.Abs((int)ob1.layer) < 1000)
                    ob1Y = (int)ob1.layer * 1000;
                else
                    ob1Y = (int)ob1.layer;

                if (Math.Abs((int)ob2.layer) < 1000)
                    ob2Y = (int)ob2.layer * 1000;
                else
                    ob2Y = (int)ob2.layer;

                if (Math.Abs(ob1.height) < 1000)
                    ob1H = ob1.height * 1000;
                else
                    ob1H = ob1.height;

                if (Math.Abs(ob2.height) < 1000)
                    ob2H = ob2.height * 1000;
                else
                    ob2H = ob2.height;

                // Assuming 'height' is a property indicating how many layers up the wall extends
                // and 'lineLayer' indicates the starting layer of the wall
                bool verticalOverlap = (ob1Y + ob1H > ob2Y) && (ob2Y + ob2H > ob1Y); // april 8, just added '=' here

                bool touching = false; // not overlapping but top of a wall touches the bottom of a wall
                int targetWall = 0; // 1 wall one under, 2 wall two under, 3 wall one left, 4 wall two left

                if (ob1Y + ob1H == ob2Y)
                {
                    touching = true;
                    targetWall = 1; // 1 wall one under
                }
                else if (ob2Y + ob2H == ob1Y)
                {
                    touching = true;
                    targetWall = 2; // 2 wall two under
                }

                if (Math.Abs(ob1.line) < 1000)
                    ob1X = ob1.line * 1000;
                else
                    ob1X = ob1.line;

                if (Math.Abs(ob2.line) < 1000)
                    ob2X = ob2.line * 1000;
                else
                    ob2X = ob2.line;

                if (Math.Abs(ob1.width) < 1000)
                    ob1W = ob1.width * 1000;
                else
                    ob1W = ob1.width;

                if (Math.Abs(ob2.width) < 1000)
                    ob2W = ob2.width * 1000;
                else
                    ob2W = ob2.width;

                // Check if they fall on the same part of the grid horizontally
                bool horizontalOverlap = (ob1X + ob1W > ob2X) && (ob2X + ob2W > ob1X); // april 8, just added '=' here

                if (ob1X + ob1W == ob2X)
                {
                    touching = true;
                    targetWall = 3; // 3 wall one left
                }
                else if (ob2X + ob2W == ob1X)
                {
                    touching = true;
                    targetWall = 4; // 3 wall two left
                }


                // Check if the time intervals overlap
                bool timeOverlap = ob1.time < ob2.endTime && ob2.time < ob1.endTime; // don't overlap and a wall doesn't end when another begins
                if (timeOverlap)
                {
                    /*
                    if (timeOverlap)
                    {
                        Plugin.Log.Info($"----- Time Overlap --- ob1.time {ob1.time:F} ob1EndTime {ob1EndTime} -- ob2.time {ob2.time:F} ob2EndTime {ob2EndTime}");
                        Plugin.Log.Info($"----- Vertical   Overlap --- ob1Y: {ob1Y} ob1H: {ob1H} -- ob2Y: {ob2Y} ob2H: {ob2H}");
                        Plugin.Log.Info($"----- Horizontal Overlap --- ob1X: {ob1X} ob1W: {ob1W} -- ob2X: {ob2X} ob2W: {ob2W}");
                    }
                    */
                    if (horizontalOverlap && verticalOverlap)
                        return (true, false, 0); // overlapping, not touching, no target wall
                    else if (touching)
                        return (false, true, targetWall); // not overlapping, touching, target wall
                }
                return (false, false, 0); // not overlapping, not touching, no target wall
            }
        }

        public static List<ERotationEventData> RemoveCrouchWallRotations(EditableCBD eData)
        {
            List<ERotationEventData> rotations = eData.RotationEvents;

            List<EObstacleData> crouchWalls = new List<EObstacleData>();

            if (Config.Instance.AllowCrouchWalls) 
            {
                foreach (var ob in originalWalls) 
                { 
                    if (ob.line == 0 && ob.width > 2 && ob.width < 1000 && ob.layer == 2)
                        crouchWalls.Add(ob); 
                } 
            }
            if (crouchWalls.Count == 0)
            {
                //Plugin.LogDebug("[RemoveCrouchWallRotations] No crouch wall found. No rotations to remove.");
                return rotations;
            }
            else
            {
                //Plugin.LogDebug($"[RemoveCrouchWallRotations] Crouch walls found: {crouchWalls.Count}.");
                //foreach (var ob in crouchWalls)
                //    Plugin.LogDebug($"[RemoveCrouchWallRotations] -- time: {ob.time:F} dur: {ob.duration:F} end: {(ob.time + ob.duration):F} -- x:{ob.line} y:{ob.layer} w: {ob.width} h:{ob.height}");
            }

            // 1) Build + merge crouch intervals
            var intervals = MergeCrouchIntervals(crouchWalls);

            if (intervals.Count == 0 || rotations == null || rotations.Count == 0)
                return rotations;

            // 2) Sort rotation times (but keep original order via index)
            var indexed = new (int idx, float t)[rotations.Count];
            for (int i = 0; i < rotations.Count; i++)
                indexed[i] = (i, rotations[i].time);

            Array.Sort(indexed, (a, b) => a.t.CompareTo(b.t));

            // 3) Single pass: advance interval pointer as rotation time increases
            var remove = new bool[rotations.Count];
            int j = 0;

            const float EPS = 1e-4f;

            for (int k = 0; k < indexed.Length; k++)
            {
                float t = indexed[k].t;
                int iOrig = indexed[k].idx;

                // Move interval pointer forward while current interval ends before t
                while (j < intervals.Count && intervals[j].end + EPS < t) j++;
                if (j >= intervals.Count) break; // no more intervals can cover future rotations

                // If current interval starts after t, this rotation is safe; continue
                if (intervals[j].start - EPS > t)
                    continue;

                // We are within [start, end] window of interval j (considering EPS)
                // Apply Late/Early rules:
                //bool inLate  = (t > intervals[j].start + EPS) && (t < intervals[j].end - EPS);
                bool inEarly = (t >= intervals[j].start - EPS) && (t <= intervals[j].end + EPS);

                //if ((late && inLate) || (!late && inEarly))
                //    remove[iOrig] = true;

                if (inEarly)
                    remove[iOrig] = true;
            }

            // 4) Return filtered in original order
            var result = new List<ERotationEventData>(rotations.Count);
            int count = 0;
            for (int i = 0; i < rotations.Count; i++)
                if (!remove[i])
                    result.Add(rotations[i]);
                else
                {
                    //Plugin.LogDebug($"[RemoveCrouchWallRotations] ---- Removed Rotation at: {rotations[i].time:F}");
                    count++;
                }

            Plugin.LogDebug($"[RemoveCrouchWallRotations] Total Rotations Removed: {count} (crouch walls count: {crouchWalls.Count}).");

            if (count > 0)
                eData.RotationEventsChanged = true;

            return result;
        }

        // Helper: merge [time, time+duration] of crouch walls into disjoint intervals
        // finds all time ranges where the player is effectively crouching and returns the smallest possible set of continuous “no-rotation” windows.
        private static List<(float start, float end)> MergeCrouchIntervals(List<EObstacleData> crouchWalls)
        {
            float prePad  = .3f; //seconds  // e.g. 0.10f-0.20f before and after crouch wall also remove rotations
            float postPad = .3f;

            var arr = new (float s, float e)[crouchWalls.Count];
            for (int i = 0; i < crouchWalls.Count; i++)
            {
                float s = crouchWalls[i].time - prePad;
                float e = crouchWalls[i].time + crouchWalls[i].duration + postPad;

                if (e < s) { var tmp = s; s = e; e = tmp; }
                arr[i] = (s, e);
            }

            Array.Sort(arr, (a, b) => a.s.CompareTo(b.s));

            var merged = new List<(float start, float end)>(arr.Length);
            const float EPS = 1e-4f;

            for (int i = 0; i < arr.Length; i++)
            {
                if (merged.Count == 0) { merged.Add((arr[i].s, arr[i].e)); continue; }

                var last = merged[merged.Count - 1];
                if (arr[i].s <= last.end + EPS)
                    merged[merged.Count - 1] = (last.start, Math.Max(last.end, arr[i].e));
                else
                    merged.Add((arr[i].s, arr[i].e));
            }

            return merged;
        }


        public static void FinalizeWallsToMap(EditableCBD eData)
        {
            //Stopwatch stopwatch = new Stopwatch();
            //stopwatch.Restart();
            if (originalWalls.Count == 0) // Github Issue #2 Walls gone when using autolights
            {
                allWalls.AddRange(eData.Obstacles); // in case not already done
                allWallsContainsOriginalWalls = true;
            }

            if (!allWallsContainsOriginalWalls)
            {
                allWalls.AddRange(originalWalls);
                allWallsContainsOriginalWalls = true;
            }
            if (!allWallsContainsStandardWalls)
            {
                allWalls.AddRange(generatedStandardWalls);
                allWallsContainsStandardWalls = true;
            }
            if (!allWallsContainsExtensionWalls)
            {
                allWalls.AddRange(generatedExtensionWalls);
                allWallsContainsExtensionWalls = true;
            }
            if (!allWallsContainsParticleWalls)
            {
                allWalls.AddRange(particleWalls);
                allWallsContainsParticleWalls = true;
            }
            if (!allWallsContainsFloorWalls)
            {
                allWalls.AddRange(floorWalls);
                allWallsContainsFloorWalls = true;
            }

            eData.Obstacles = allWalls;

            Plugin.LogDebug($"[FinalizeWallsToMap] Walls Finalized Count: {eData.Obstacles.Count}");

            //Plugin.LogDebug($" ------- Add all walls Time Elapsed: {stopwatch.ElapsedMilliseconds / 1000.0:F1}.");
            //stopwatch.Stop();
        }
        public static void FinalizeOriginalOnlyWallsToMap(EditableCBD eData)
        {
            // Github Issue #2 Walls gone when using autolights
            if (originalWalls.Count == 0)
            {
                allWalls.AddRange(eData.Obstacles); // in case not already done
                allWallsContainsOriginalWalls = true;
            }

            if (!allWallsContainsOriginalWalls)
            {
                allWalls.AddRange(originalWalls);
                allWallsContainsOriginalWalls = true;
            }
            

            allWalls.Sort((a, b) => a.time.CompareTo(b.time));

            eData.Obstacles = allWalls;

            Plugin.LogDebug($"[FinalizeWallsToMap] Walls Finalized Count: {eData.Obstacles.Count} (only original walls since walls count > 5000)");
        }

        public static bool IsCustomNoodleWall(EObstacleData ob)
        {
            if (!TransitionPatcher.NoodleProblemObstacles)
                return false;
            return ob is EObstacleData customOb && // this will let us avoid work on those 12 or less custom walls
               ((customOb.customData?.ContainsKey("_position") ?? false) ||
                (customOb.customData?.ContainsKey("_definitePosition") ?? false) ||
                (customOb.customData?.ContainsKey("_rotation") ?? false) ||
                (customOb.customData?.ContainsKey("_localRotation") ?? false) ||
                (customOb.customData?.ContainsKey("_scale") ?? false) ||
                (customOb.customData?.ContainsKey("_track") ?? false) ||
                (customOb.customData?.ContainsKey("_animation") ?? false) ||
                customOb.line > 999 || customOb.line < -999 ||
                customOb.layer > 999 ||
                customOb.layer < (-999) ||
                customOb.width > 999 || customOb.width < -999 ||
                customOb.height > 999 || customOb.height < -999);
        }

        /// <summary>
        ///  Detect gaps between walls (times when there are no walls) for sky floor walls to be added
        ///  Looks for time gaps between rotation moments that are at least a certain length (minGapDuration), and returns those gaps as TimeGap objects.
        /// </summary>
        public static List<TimeGap> FindGapsUsingRotations(List<(float time, int rotation)> wallCutMoments, float minGapDuration)
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
    }
}