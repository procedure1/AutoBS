using AutoBS.Patches;
using BeatSaberMarkupLanguage.Animations.APNG.Chunks;
using CustomJSONData.CustomBeatmap;
using IPA.Config.Data;
using Microsoft.Extensions.Primitives;
using ModestTree;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Reflection;
using System.Security.Claims;
using System.Security.Policy;
using UnityEngine;
using static BloomPrePassRenderDataSO;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TaskbarClock;
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

        public static int floorWallToggleCityScape = 0; // toggle between tiles and cityscape
        public static int floorWallToggleSpires = 0; // tall and thin
        public static int floorWallToggleSmall = 0; // smaller tiles
        public static int windowPaneTallToggle = 0; // smaller tiles
        public static int lastHeight = 0;

        private static int _divisorCounter = 0; // need an incrementing counter for the percentage to work correctly

        private static int _lastProcessedIndex = 0; // make the loop more efficient

        public static int _originalWallCount = -1; // used so can see how many walls before any removals

        private static float _startTime = -1; // End time of first note so start adding walls
        private static float _endTime = -1; // End time of last note so stop adding walls

        public static List<EObstacleData> _originalWalls = new List<EObstacleData>();

        public static List<EObstacleData> _generatedStandardWalls = new List<EObstacleData>();

        private static List<EObstacleData> _tempOriginalAndStandardWalls = new List<EObstacleData>(); // just for updating a loop

        public static List<EObstacleData> _generatedExtensionWalls = new List<EObstacleData>();

        //private static List<EObstacleData> _allWallsExceptFloorsAndParticles = new List<EObstacleData>();

        private static List<EObstacleData> _particleWalls = new List<EObstacleData>();

        private static List<EObstacleData> _floorWalls = new List<EObstacleData>();

        public static List<EObstacleData> _allWalls = new List<EObstacleData>();

        private static bool _allWallsContainsOriginalWalls = false;
        private static bool _allWallsContainsStandardWalls = false;
        private static bool _allWallsContainsExtensionWalls = false;
        private static bool _allWallsContainsParticleWalls = false;
        private static bool _allWallsContainsFloorWalls = false;

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

                Plugin.LogDebug($"[WallGenerator][ResetWalls] - {TransitionPatcher.SelectedSerializedName} {TransitionPatcher.SelectedDifficulty} -  360/90 maps get all full wall multipliers.");

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


                _divisorCounter = 0; 

            _lastProcessedIndex = 0; // make the loop more efficient

            if (eData.ColorNotes.Count() > 0)
            {
                _startTime = eData.ColorNotes.First().time;
                _endTime = eData.ColorNotes.Last().time;
            }

            Plugin.LogDebug($"ResetWalls: StartTime: {_startTime:F}, EndTime: {_endTime:F} - ColorNotes.Count: {eData.ColorNotes.Count()} - Obstacles.Count: {eData.Obstacles.Count()}");
            _originalWalls.Clear();
            foreach (var obstacle in eData.Obstacles) // Clear existing obstacles from BeatmapData so obstacles are empty
            {
                _originalWalls.Add(obstacle); // add original obstacles into the list
            }
            _originalWalls.Sort((a, b) => a.time.CompareTo(b.time));
            _originalWallCount = _originalWalls.Count;

            _tempOriginalAndStandardWalls.Clear();
            foreach (var obstacle in _originalWalls) // Clear existing obstacles from BeatmapData so obstacles are empty
            {
                _tempOriginalAndStandardWalls.Add(obstacle); // add original obstacles into the list
            }

            // Manually iterate over the LinkedList and remove obstacles safely - this is working. 0 EObstacleData and ObstacleData after this.
            eData.Obstacles.Clear();

            _generatedStandardWalls.Clear();
            _generatedExtensionWalls.Clear();
            _particleWalls.Clear();
            _floorWalls.Clear();
            _allWalls.Clear();

            _allWallsContainsOriginalWalls = false;
            _allWallsContainsStandardWalls = false;
            _allWallsContainsExtensionWalls = false;
            _allWallsContainsParticleWalls = false;
            _allWallsContainsFloorWalls = false;

            Plugin.LogDebug($"[WallGenerator] Walls RESET: original wall count: {_originalWalls.Count}");
        }

        public static void WallGen(int i, float wallTime, float wallDuration, ENoteData afterLastNote, List<ENoteData> notesInBarBeat, List<ENoteData> notesInBar, float nextNoteLeftTime, float nextNoteRightTime)

        {
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
            

            if (TransitionPatcher.SelectedSerializedName == GameModeHelper.GENERATED_360DEGREE_MODE || TransitionPatcher.SelectedSerializedName == "360Degree")
                minDistanceBetweenNotesAndWalls = Config.Instance.MinDistanceBetweenNotesAndWalls * Config.Instance.RotationSpeedMultiplier; // was .5f then .7f
            else
                minDistanceBetweenNotesAndWalls = .2f;

            //Plugin.Log.Info($"WallGen: i: {i} wallTime: {wallTime:F} wallDuration: {wallDuration:F} afterLastNote: {afterLastNote?.time:F} notesInBarBeat.Count: {notesInBarBeat.Count} notesInBar.Count: {notesInBar.Count}");

            //Plugin.Log.Info($"WallGenerator: containsCustomWalls: {BeatmapDataTransformHelperPatcher.containsCustomWalls}");

            float WallSpawnDelay = Config.Instance.MinDistanceBetweenNotesAndWalls; // seconds; set to 0f to disable
            if (TransitionPatcher.SelectedSerializedName == GameModeHelper.GENERATED_360DEGREE_MODE)
                wallTime += WallSpawnDelay; // Shift wall start time forward

            string generatedBigWall = "none"; // used to prevent bigWalls overlapping with columns of walls and rows of walls
            string generatedWall = "none";    // used to prevent standard walls overlapping with columns of walls and rows of walls

            if (!Config.Instance.EnableStandardWalls && !Config.Instance.EnableBigWalls && Config.Instance.EnableMappingExtensionsWallsGenerator) // without standard and big walls, CreateExtensionWalls won't be called
                CreateExtensionWalls(i, wallTime, wallDuration, generatedWall, generatedBigWall);

            _divisorCounter++;
            int divisor = (int)(100/StandardWallsMultiplier); // Get the divisor based on the user input percentage
            if (_divisorCounter % divisor != 0) return;

            //Plugin.Log.Info($"Config.Instance.StandardWallsMultiplier: {Config.Instance.StandardWallsMultiplier} divisor: {divisor}");

            lastTunnelWallTime = 0; // must reset this here or will keep last setting from previous song play through
            lastWindowPaneWallTime = 0;
            tunnelWallsHappening = false;
            paneWallsHappening = false;

            bool generateWall = true;

            float minGapBetweenWalls = .2f; // seconds

            //Plugin.Log.Info($"Wall Gen - wallTime: {wallTime:F} wallDuration: {wallDuration}");

            for (int k = _lastProcessedIndex; k < _tempOriginalAndStandardWalls.Count; k++) // Check if there is already a wall
            {
                EObstacleData obs = _tempOriginalAndStandardWalls[k];

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

                _lastProcessedIndex = k; // Update the last processed index
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
                    int startColRight = 3 + (int)Config.Instance.StandardWallsMinDistance;

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
                        _generatedStandardWalls.Add(customObsDataR);

                        int idxR = _tempOriginalAndStandardWalls.BinarySearch(customObsDataR, Comparer<EObstacleData>.Create((x, y) => x.time.CompareTo(y.time)));
                        if (idxR < 0) idxR = ~idxR;
                        _tempOriginalAndStandardWalls.Insert(idxR, customObsDataR);
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
                    int startColLeft = (widthLeft == 1 ? 0 : -11) - (int)Config.Instance.StandardWallsMinDistance;

                    // side-local start/dur
                    float lStart = wallTime;
                    float lDur = wallDuration;

                    if (afterLastNote.line == 0 && !(wallHeightL == 1 && afterLastNote.layer == 0))
                    {
                        lDur = afterLastNote.time - WallBackCut - lStart;
                        if (lDur < minWallDuration) goto EXT_WALLS; // skip left, continue
                    }

                    ClampWallEndAgainstNextNote(ref lStart, ref lDur, notesInBar, nextNoteLeftTime, startColLeft, widthLeft);
                    if (lDur < 0f) goto EXT_WALLS;

                    if ((Config.Instance.EnableStandardWalls && widthLeft == 1) || (Config.Instance.EnableBigWalls && widthLeft == 12))
                    {
                        int heightL = 5;

                        if (widthLeft == 12 && i % 24 == 0) { widthLeft = 50; heightL = 50; startColLeft = -49; lDur = Math.Min(lDur / 10f, .1f); }

                        var customObsDataL = EObstacleData.Create(lStart, startColLeft, lineLayerL, lDur, widthLeft, heightL);
                        _generatedStandardWalls.Add(customObsDataL);

                        int idxL = _tempOriginalAndStandardWalls.BinarySearch(customObsDataL, Comparer<EObstacleData>.Create((x, y) => x.time.CompareTo(y.time)));
                        if (idxL < 0) idxL = ~idxL;
                        _tempOriginalAndStandardWalls.Insert(idxL, customObsDataL);
                    }
                }

                EXT_WALLS:
                if (Config.Instance.EnableMappingExtensionsWallsGenerator)
                {
                    // Use original wallTime/wallDuration beat anchoring for extension walls;
                    // they no longer get unintentionally trimmed by the other side.
                    CreateExtensionWalls(i, wallTime, wallDuration, generatedWall, generatedBigWall);
                }

            }
            //Plugin.Log.Info($"WallTime: {wallTime} GeneratedWall: {generatedWall} Time: Count: {genWallCount} - GenderatedBigWall: {generatedBigWall} Count: {genBigWallCount}");

            if (Config.Instance.EnableMappingExtensionsWallsGenerator) // turn off automated extended walls for maps already using mapping extensions
            {
                CreateExtensionWalls(i, wallTime, wallDuration, generatedWall, generatedBigWall); // inside the main loop to use wallTime and wallDuration so its on the beat
                //Plugin.Log.Info($"Map doesn't NOT already use Mapping Extensions");
            }
            //else
            //Plugin.Log.Info($"CreateExtensionWalls() NOT CALLED!");

        }



        // Extension Walls ------------------------------------------------------------------------------------------------------

        #region Extension Walls

        // [WARNING @ 12:53:12 | UnityEngine] BoxColliders does not support negative scale or size.
        // Caused by Window Panes and Floor Walls sometimes (360 or standard). couln't find any negative inputs from window panes anyway so stopped looking
        public static void CreateExtensionWalls(int i, float wallTime, float wallDuration, string alreadyHasGenWall, string alreadyHasBigWall) // big walls (not particle walls) - using walltime so on the beat
        {
            if (!Utils.IsEnabledExtensionWalls()) return;

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
            if (Config.Instance.EnableDistantExtensionWalls)
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
                    if (allWalls1 == 1 || allWalls1 == 3)
                    {
                        // low walls
                        customObsData = EObstacleData.Create(wallTime, lineIndex, loLayer, duration, loWidth, height1);
                        //Plugin.Log.Info($"Wall EXTENSION Lo Rt: Time: {wallTime}, Index:{indexx}, Layer: {loLayer}, Dur: {duration}, Width: {loWidth}, Height: {height1}");
                        if (lineIndex < 2)
                        {
                            //Plugin.Log.Info($"Wall EXTENSION Lo Rt: Time: {wallTime}, Index:{indexx}, Layer: {loLayer}, Dur: {duration}, Width: {loWidth}, Height: {height1}");
                            _generatedExtensionWalls.Add(customObsData);
                            alreadyHasExtendedWalls = "left";
                        }
                        else
                        {
                            //Plugin.Log.Info($"Wall EXTENSION Lo Rt: Time: {wallTime}, Index:{indexx}, Layer: {loLayer}, Dur: {duration}, Width: {loWidth}, Height: {height1}");
                            _generatedExtensionWalls.Add(customObsData);
                            alreadyHasExtendedWalls = "right";
                        }
                        //data.AddBeatmapObjectDataInOrder(customObsData);
                        distantCount++;

                        if (loBoth1)
                        {
                            customObsData = EObstacleData.Create(wallTime, -lineIndex, loLayer, duration, loWidth, height1);
                            if (lineIndex < 2)
                            {
                                _generatedExtensionWalls.Add(customObsData);
                                alreadyHasExtendedWalls = alreadyHasExtendedWalls == "right" ? "both" : "left";
                            }
                            else
                            {
                                _generatedExtensionWalls.Add(customObsData);
                                alreadyHasExtendedWalls = alreadyHasExtendedWalls == "left" ? "both" : "right";
                            }
                            //Plugin.Log.Info($"Wall EXTENSION Lo Lt: Time: {wallTime}, Index:{-indexx}, Layer: {loLayer}, Dur: {duration}, Width: {loWidth}, Height: {height1}");
                            //data.AddBeatmapObjectDataInOrder(customObsData);
                            distantCount++;

                        }


                    }
                    if (allWalls1 == 2 || allWalls1 == 3)
                    {
                        if (height1 < 1000)
                            hiWidth1 = Math.Min(4, hiWidth1);

                        if (hiLayer >= 20)
                        {
                            duration *= 4f;
                            hiWidth1 = 4;
                        }

                        lineIndex = sign * hiLineIndx;
                        // high walls
                        customObsData = EObstacleData.Create(wallTime, lineIndex, hiLayer, duration, hiWidth1, height1);
                        if (lineIndex < 2)
                        {
                            _generatedExtensionWalls.Add(customObsData);
                            alreadyHasExtendedWalls = alreadyHasExtendedWalls == "right" ? "both" : "left";
                        }
                        else
                        {
                            _generatedExtensionWalls.Add(customObsData);
                            alreadyHasExtendedWalls = alreadyHasExtendedWalls == "left" ? "both" : "right";
                        }
                        //Plugin.Log.Info($"Wall EXTENSION Hi Rt: Time: {wallTime}, Index:{indexx}, Layer: {hiLayer}, Dur: {duration}, Width: {hiWidth1}, Height: {height1}");
                        //data.AddBeatmapObjectDataInOrder(customObsData);
                        distantCount++;

                        if (hiBoth1)
                        {
                            customObsData = EObstacleData.Create(wallTime, -lineIndex, hiLayer, duration, hiWidth1, height1);
                            if (lineIndex < 2)
                            {
                                _generatedExtensionWalls.Add(customObsData);
                                alreadyHasExtendedWalls = alreadyHasExtendedWalls == "right" ? "both" : "left";
                            }
                            else
                            {
                                _generatedExtensionWalls.Add(customObsData);
                                alreadyHasExtendedWalls = alreadyHasExtendedWalls == "left" ? "both" : "right";
                            }
                            //Plugin.Log.Info($"Wall EXTENSION Hi Lt: Time: {wallTime}, Index:{-indexx}, Layer: {hiLayer}, Dur: {duration}, Width: {hiWidth1}, Height: {height1}");
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

            int divisorCol    = Math.Max((int)Math.Round(15 / ColumnWallsMultiplier), 1);
            int divisorRow    = Math.Max((int)Math.Round(17 / RowWallsMultiplier), 1);
            int divisorTunnel = Math.Max((int)Math.Round(5 / TunnelWallsMultiplier), 1);
            int divisorGrid   = Math.Max((int)Math.Round(18 / GridWallsMultiplier), 1);// was 21
            int divisorPane   = Math.Max((int)Math.Round(9 / WindowPaneWallsMultiplier), 1);

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
                else if (Config.Instance.EnableTunnelWalls && !paneWallsHappening && hash % divisorTunnel == 0) // tunnel box walls surrounding the player
                {
                    TunnelWalls(wallTime, alreadyHasGenWall, alreadyHasBigWall, wallCount, numberOfColumns, durationMult);
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
        }
        private static void ColumnWalls(float wallTime, string alreadyHasBigWall, int numberOfColumns,
            int columnsLineIndexMult, string alreadyHasExtendedWalls)
        {
            for (int j = 0; j <= numberOfColumns * 2; j++) // columns of tall walls
            {
                //Plugin.Log.Info($"Wall Columns: Time: {wallTime}, Number of cols: {numberOfColumns} indexMultiplier: {columnsLineIndexMult} divisorOne: {divisorOne}");
                int k = j * 2 + (int)Config.Instance.ColumnWallsMinDistance - 2 + columnsLineIndexMult;

                if (alreadyHasBigWall != "left" && alreadyHasExtendedWalls != "left")
                {
                    EObstacleData customObsData = EObstacleData.Create(wallTime, -k, j, .001f, 1, 10 + j); // -2, -4, -6, -8 columns with 1 space between
                    _generatedExtensionWalls.Add(customObsData);
                    //Plugin.Log.Info($"Wall Column Time: {wallTime}, Index:{-k}, Layer: {j}, Dur: .001f, Width: 1, Height: {10 + j}");

                    columnCount++;
                }
                if (alreadyHasBigWall != "right" && alreadyHasExtendedWalls != "right")
                {
                    EObstacleData customObsData = EObstacleData.Create(wallTime, (k + 3), j, .001f, 1, 10 + j); // 5, 7, 9, 11 columns with 1 space between
                    _generatedExtensionWalls.Add(customObsData);
                    //Plugin.Log.Info($"Wall Column Time: {wallTime}, Index:{k + 3}, Layer: {j}, Dur: .001f, Width: 1, Height: {10 + j}");

                    columnCount++;
                }
            }
        }

        private static void RowWalls(float wallTime, string alreadyHasBigWall, int numberOfColumns,
            string alreadyHasExtendedWalls)
        {
            for (int j = 0; j <= numberOfColumns * 2; j++) // rows of long walls
            {
                int k = j * 2 + 2;

                int leftLineIndexCalc = (int)Config.Instance.RowWallsMinDistance * 1000 + 20000; // 24000 default
                int rightLineIndexCalc = (int)Config.Instance.RowWallsMinDistance * 1000 + 5000; // 9000 default which is 3000 away from player so use 5 for config

                //int l = j;

                //if (j % 2 == 0) l *= -1;

                if (alreadyHasBigWall != "left" && alreadyHasExtendedWalls != "left")
                {
                    //wallTime + l / 100
                    EObstacleData customObsData = EObstacleData.Create(wallTime, -leftLineIndexCalc - (j * 500), k, .03f, 20 + j, 1200); // 2, 4, 6, 8 rows with 1 space between
                    _generatedExtensionWalls.Add(customObsData);
                    //Plugin.Log.Info($"Wall EXTENSION Hi Lt: Time: {wallTime}, Index:{-indexx}, Layer: {hiLayer}, Dur: {duration}, Width: {hiWidth1}, Height: {height1}");

                    rowCount++;
                }
                if (alreadyHasBigWall != "right" && alreadyHasExtendedWalls != "right")
                {
                    EObstacleData customObsData = EObstacleData.Create(wallTime, rightLineIndexCalc - (j * 500), k, .03f, 20 + j, 1200); // 5, 7, 9, 11 columns with 1 space between
                    _generatedExtensionWalls.Add(customObsData);
                    //2100, 1600, 9000 too far away                                                                                                                 //Plugin.Log.Info($"Wall EXTENSION Hi Lt: Time: {wallTime}, Index:{-indexx}, Layer: {hiLayer}, Dur: {duration}, Width: {hiWidth1}, Height: {height1}");
                    rowCount++;
                }
            }
        }

        private static void GridWalls(float wallTime, string alreadyHasBigWall, int[] gridWallWidth,
            int[] gridWallHeight, int numberOfColumns, string alreadyHasExtendedWalls)
        {
            int width = gridWallWidth[(int)wallTime % gridWallWidth.Length];
            int height1 = gridWallHeight[(int)wallTime % gridWallHeight.Length];

            int leftLineIndex = (int)Config.Instance.GridWallsMinDistance * 1000 + 2500; //-3500 default
            int rightLineIndex = (int)Config.Instance.GridWallsMinDistance * 1000 + 4500; //6500 default

            int adjustLineIndex = 0;
            if (width > 2000)
                adjustLineIndex = 1000;

            //int width = 3000; // 1000 = 0 and 2000 = 1!
            //int height1 = 2500; // equivalent to 1
            int gap = 300; // space between walls

            // random duration (thickness) and wallTime
            System.Random rand = new System.Random(); // Initialize the random number generator

            //introduced this since must reduce the number of grid walls since slow the game down. so if this is called more frequenly, then make the cols/rows smaller
            //problem is that at 1, it makes huge grids which is cool. at higher GridWallsMultiplier's, grids are quite small
            float mult = 3; // at 1 or less
            if (GridWallsMultiplier > 1)
                mult = 4 - GridWallsMultiplier; // mult = 2 if GridWallsMultiplier = 2, mult = 1 if GridWallsMultiplier = 3

            int gridColumns = (int)(mult * (gridWallWide ? 3   : 1)); // if wide grid walls then multiplier is 4 else it is 1
            int gridRows    = (int)(mult * (gridWallWide ? 1.5 : 5)); // if wide grid walls then multiplier is 1.5 else it is 6
            gridWallWide = !gridWallWide; // swap back and forth between wide and narrow

            //Plugin.Log.Info($"Grid Walls - Time: {wallTime:F2} Cols: {gridColumns} Rows: {gridRows} based on GridWallsMultiplier: {Config.Instance.GridWallsMultiplier}");

            for (int j = 0; j <= gridColumns; j++) // cols
            {
                for (int k = 0; k <= gridRows; k++) // rows
                {
                    float randomDuration = .0001f + (float)(rand.NextDouble() * .002f); // Generate a random duration between 
                    float randomTimeOffset = (float)(rand.NextDouble() * 0.06) - 0.03f; // Generate random time adjustment between -0.01 and +0.01

                    float adjustedWallTime = wallTime + randomTimeOffset; // Adjust wallTime by the random time offset

                    if (alreadyHasBigWall != "left" && alreadyHasExtendedWalls != "left")
                    {
                        // Left grid wall placement with random duration and adjusted wall time
                        EObstacleData customObsData = EObstacleData.Create(adjustedWallTime, -leftLineIndex - (j * (width + gap)) - adjustLineIndex, (k * (height1) + gap), randomDuration, width, height1);
                        _generatedExtensionWalls.Add(customObsData);

                        gridCount++;
                    }
                    if (alreadyHasBigWall != "right" && alreadyHasExtendedWalls != "right")
                    {
                        // Right grid wall placement with random duration and adjusted wall time
                        EObstacleData customObsData = EObstacleData.Create(adjustedWallTime, rightLineIndex + (j * (width + gap)), (k * (height1) + gap), randomDuration, width, 2000);
                        _generatedExtensionWalls.Add(customObsData);

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
            // these fight and collide with other walls so i moved them out a little
            int topLineLayer = (int)Config.Instance.TunnelWallsMinDistance * 1000 + 6750; // 6000 default which is 2 away but count as 1.
            int leftLineIndex = (int)Config.Instance.TunnelWallsMinDistance * 1000 + 2250; // 1500 default which is 1 (1.5) away basically from player
            int rightLineIndex = (int)Config.Instance.TunnelWallsMinDistance * 1000 + 5750; // 5000 default which is 1 away basically from player

            for (int j = 0; j <= Math.Max(numberOfColumns * 2, 6); j++)
            {
                float newWallStartTime = wallTime + (j * 0.06f * durationMult);
                float newWallEndTime = newWallStartTime + .03f * durationMult;  // Assuming duration is the length of the wall in time

                // since walls are adding into the future from wallTime, the next loop may likely overlap walls so avoid this
                if (newWallStartTime > lastTunnelWallTime && newWallStartTime < _endTime)
                {
                    if (alreadyHasGenWall == "none" && alreadyHasBigWall == "none")  // top wall - dont' check for alreadyHasExtendedWalls since those are distant walls
                    {
                        EObstacleData customObsData = EObstacleData.Create(newWallStartTime, 0, topLineLayer, .03f * durationMult, 4500, 1010); // top wall  //Plugin.Log.Info($"Wall EXTENSION Hi Lt: Time: {wallTime}, Index:{-indexx}, Layer: {hiLayer}, Dur: {duration}, Width: {hiWidth1}, Height: {height1}");
                        _generatedExtensionWalls.Add(customObsData);
                        //Plugin.Log.Info($"Tunnel Wall: Time: {newWallStartTime:F2}, line: 0, Layer: {topLineLayer}, Width: 4500, Height: 1010");
                 
                        tunnelCount++;
                    }

                    if (alreadyHasGenWall != "left" && alreadyHasBigWall != "left") // left wall
                    {
                        EObstacleData customObsData = EObstacleData.Create(newWallStartTime, -leftLineIndex, 0, .03f * durationMult, 1010, height1); // left wall //Plugin.Log.Info($"Wall EXTENSION Hi Lt: Time: {wallTime}, Index:{-indexx}, Layer: {hiLayer}, Dur: {duration}, Width: {hiWidth1}, Height: {height1}");
                        _generatedExtensionWalls.Add(customObsData);
                        //Plugin.Log.Info($"Tunnel Wall: Time: {newWallStartTime:F2}, line: {-leftLineIndex}, Layer: 0, Width: 1010, Height: {height1}");

                        if (wallCount == 2)// || wallCount == 3)
                        {
                            customObsData = EObstacleData.Create(newWallStartTime, -leftLineIndex, layer2, .03f * durationMult, 1010, height1); // left wall //Plugin.Log.Info($"Wall EXTENSION Hi Lt: Time: {wallTime}, Index:{-indexx}, Layer: {hiLayer}, Dur: {duration}, Width: {hiWidth1}, Height: {height1}");
                            _generatedExtensionWalls.Add(customObsData);
                            //Plugin.Log.Info($"Tunnel Wall: Time: {newWallStartTime:F2}, line: {-leftLineIndex}, Layer: {layer2}, Width: 1010, Height: {height1}");
                           
                            tunnelCount++;
                        }

                    }
                    if (alreadyHasGenWall != "right" && alreadyHasBigWall != "right") // right wall
                    {
                        EObstacleData customObsData = EObstacleData.Create(newWallStartTime, rightLineIndex, 0, .03f * durationMult, 1010, height1); // right wall //Plugin.Log.Info($"Wall EXTENSION Hi Lt: Time: {wallTime}, Index:{-indexx}, Layer: {hiLayer}, Dur: {duration}, Width: {hiWidth1}, Height: {height1}");
                        _generatedExtensionWalls.Add(customObsData);



                        if (wallCount == 2)// || wallCount == 3)
                        {
                            customObsData = EObstacleData.Create(newWallStartTime, rightLineIndex, layer2, .03f * durationMult, 1010, height1); // right wall //Plugin.Log.Info($"Wall EXTENSION Hi Lt: Time: {wallTime}, Index:{-indexx}, Layer: {hiLayer}, Dur: {duration}, Width: {hiWidth1}, Height: {height1}");
                            _generatedExtensionWalls.Add(customObsData);
                            //Plugin.Log.Info($"Tunnel Wall: Time: {newWallStartTime:F2}, line: {rightLineIndex}, Layer: {layer2}, Width: 1010, Height: {height1}");

                            tunnelCount++;
                        }
                        /*
                                if (wallCount == 3)
                                {
                                    customObsData = EObstacleData.Create(wallTime + (j * .06f * durationMult), 4, layer3, .03f * durationMult, 1010, height1); // left wall //Plugin.Log.Info($"Wall EXTENSION Hi Lt: Time: {wallTime}, Index:{-indexx}, Layer: {hiLayer}, Dur: {duration}, Width: {hiWidth1}, Height: {height1}");
                                    data.AddBeatmapObjectDataInOrder(customObsData);
                                }
                                */
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

            windowPaneTallToggle = (windowPaneTallToggle + 1) % 3; // 1 in 3 times

            if (windowPaneTallToggle == 0)
            {
                leftLineIndex  = (int)Config.Instance.WindowPaneWallsMinDistance + 1;
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
                        _generatedExtensionWalls.Add(customObsData);

                        //data.AddBeatmapObjectDataInOrder(customObsData);
                        paneCount++;
                        /*
                                if (wallCount == 2)// || wallCount == 3)
                                {
                                    customObsData = EObstacleData.Create(newWallStartTime, -leftLineIndex, layer2, .00001f, 3000, height1); // left wall //Plugin.Log.Info($"Wall EXTENSION Hi Lt: Time: {wallTime}, Index:{-indexx}, Layer: {hiLayer}, Dur: {duration}, Width: {hiWidth1}, Height: {height1}");
                                    data.AddBeatmapObjectDataInOrder(customObsData);
                                    paneCount++;
                                }
                                */
                    }
                    if (alreadyHasGenWall != "right" && alreadyHasBigWall != "right") // right wall
                    {
                        //Plugin.Log.Info($"Window Panes right - Time: {newWallStartTime}, Index:{rightLineIndex}, Layer: {layer}, Dur: .0000001f, Width: {width}, Height: {height1}");
                        EObstacleData customObsData = EObstacleData.Create(newWallStartTime, rightLineIndex, layer, .0000001f, width, height1); // right wall //Plugin.Log.Info($"Wall EXTENSION Hi Lt: Time: {wallTime}, Index:{-indexx}, Layer: {hiLayer}, Dur: {duration}, Width: {hiWidth1}, Height: {height1}");
                        _generatedExtensionWalls.Add(customObsData);

                        //data.AddBeatmapObjectDataInOrder(customObsData);
                        paneCount++;
                        /*
                                if (wallCount == 2)// || wallCount == 3)
                                {
                                    customObsData = EObstacleData.Create(newWallStartTime, rightLineIndex, layer2, .00001f, 3000, height1); // right wall //Plugin.Log.Info($"Wall EXTENSION Hi Lt: Time: {wallTime}, Index:{-indexx}, Layer: {hiLayer}, Dur: {duration}, Width: {hiWidth1}, Height: {height1}");
                                    data.AddBeatmapObjectDataInOrder(customObsData);
                                    paneCount++;
                                }
                                */
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
            if (Config.Instance.EnableParticleWalls && Config.Instance.EnableMappingExtensionsWallsGenerator)
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
                Plugin.LogDebug($"[WallGenerator] Wall particle Count: {_particleWalls.Count}");
            }
            //return (leftParticles, rightParticles);
        }

        private static void AddParticleWall(float time, int i, int j)
        {
            //int width = 1; int height = 1; float duration = 0.03f;

            int[] widthAndHeight = { 1050, 1010, 1300, 1300, 1300, 1500, 2500, 1300, 1400, 1100, 1500, 1200 }; // { 1100, 1200, 1300, 1500, 1700, 1, 2500 }; // 1100 = .1, 1700 = .7, 1 or 2000 = 1, 2500 = 1.5
            float[] dur = { .02f, .02f, .02f, .02f, 1f, .02f, .05f, .5f, 1f, .02f, .02f, .02f };

            // Cycle through predetermined values instead of random generation
            int[] lineLayers = { 9, 1, 2, 7, 6, 8, 3, 4, 5 }; // { 1, 2, 3, 4, 5, 6, 7, 8, 9 }; // all line layers
            int[] lineIndexes1 = { 11, 3, -3, -8, 5, 7, 8, -9, 0, 4, -2, 13, -1, 9, -7, 6, 1, 10, 2, -5, -6, 12, -4 }; // { -9, -8, -7, -6, -5, -4, -3, -2, -1, 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13 }; // all line indexes
            int[] lineIndexes2 = { -4, 13, -3, 5, 9, -9, 11, 6, -7, 7, 8, 12, -8, -1, -2, -5, 10, -6 }; // { -9, -8, -7, -6, -5, -4, -3, -2, -1, 5, 6, 7, 8, 9, 10, 11, 12, 13 }; // line indexes outside of the main area

            // Utilizing an oscillation function of wallTime for variability
            int variableLayer = (int)(Math.Sin(time + j) * 1000);

            int variableIndex1;
            if (j % 2 == 0)
            {
                variableIndex1 = (int)(Math.Sin(time) * 1000) + j;
            }
            else
                variableIndex1 = (int)(Math.Cos(time) * 1000) + j;

            int variableWHD;
            if (i % 2 == 0)
            {
                variableWHD = (int)(Math.Sin(time) * 1000) + i;
            }
            else
                variableWHD = (int)(Math.Cos(time) * 1000) + i;


            // Calculating cycle indexes
            int cycleIndexForLineLayer = Math.Abs(variableLayer) % lineLayers.Length;
            int lineLayer = lineLayers[cycleIndexForLineLayer] + (int)Config.Instance.ParticleWallsMinDistance;

            int cycleIndexForLineIndex;
            int lineIndex;
            if (lineLayer <= 4) // if low wall then skip area around player
            {
                cycleIndexForLineIndex = Math.Abs(variableIndex1) % lineIndexes2.Length;
                lineIndex = lineIndexes2[cycleIndexForLineIndex];
            }
            else // if high wall then can be anywhere left, right or above player
            {
                cycleIndexForLineIndex = Math.Abs(variableIndex1) % lineIndexes1.Length;
                lineIndex = lineIndexes1[cycleIndexForLineIndex];
            }
            if (lineIndex < 2)
                lineIndex -= (int)Config.Instance.ParticleWallsMinDistance;
            else
                lineIndex += (int)Config.Instance.ParticleWallsMinDistance;

            int cycleIndexForWidthHeight = Math.Abs(variableWHD) % widthAndHeight.Length;
            int cycleIndexForDuration = Math.Abs(variableWHD) % dur.Length;

            int widthHeight = widthAndHeight[cycleIndexForWidthHeight];
            float duration = dur[cycleIndexForDuration];

            if (widthHeight > 1300) // fatter particle walls
            {
                duration = .03f; // should be short since long ones look uncool IMO

                if (lineIndex < 0)
                    lineIndex -= 1; // fatter particle wall on the left are getting closer to the player so move them further out
            }
            if (duration >= 1)
                widthHeight = 1100;
            else if (duration == .5f)
                widthHeight = 1200;

            if (!Config.Instance.EnableLargeParticleWalls && widthHeight > 1700)
                widthHeight = 1500;

            //if (lineLayer == 5) { height = 2000; } // lineLayer 4 & lower square. 5 is flat no height. 6 is normal 7 and higher get taller and taller. if want all 1 then use 2000. but i like the tall thin high ones.
            //Plugin.Log.Info($"---Variable Index: {Math.Abs(variableIndex1)} widthHeight: {widthHeight}");
            EObstacleData customObsData = EObstacleData.Create(time, lineIndex, lineLayer, duration, widthHeight, widthHeight); // lineLayer 4 & lower square. 5 is flat no height. 6 is normal 7 is very tall, 9 is long thin taller even

            if (TransitionPatcher.RequiresNoodle)
                customObsData = ConvertToNoodleWall(customObsData);

            //Plugin.Log.Info($"Wall particle: Time: {time}, Index:{lineIndex}, Layer: {lineLayer}, Dur: {duration}, Width: {widthHeight}, Height: {widthHeight}");

            //Plugin.Log.Info($"Wall EXTENSION Lo Rt: Time: {wallTime}, Index:{indexx}, Layer: {loLayer}, Dur: {duration}, Width: {loWidth}, Height: {height1}");
            _particleWalls.Add(customObsData);
        }

        public static void FloorWalls(List<TimeGap> gaps, int repeatLimit = -1) // not using wallTime so not on the beat
        {
            if (Config.Instance.EnableFloorWalls && Config.Instance.EnableMappingExtensionsWallsGenerator)
            {
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
                        floorWallToggleCityScape = (floorWallToggleCityScape + 1) % 3; // 1 in 3 times
                        floorWallToggleSpires = (floorWallToggleSpires + 1) % 4;
                        floorWallToggleSmall = (floorWallToggleSmall + 1) % 5;

                        //Plugin.Log.Info($"--Floor Wall Start Time: {time}");

                        int repeatCount = 1 + ((int)time % repeatLimit); // Results in a value between 1 and 40

                        int lineIndexList = new System.Random().Next(1, 3); // between 1 and 2 since -- >= minValue, < maxValue

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
                                    randomListIndex = new System.Random().Next(0, lineIndexes1.Length);
                                    lineIndex = lineIndexes1[randomListIndex];
                                }
                                else
                                {
                                    randomListIndex = new System.Random().Next(0, lineIndexes2.Length);
                                    lineIndex = lineIndexes2[randomListIndex];
                                }

                                if (usedLineIndexes.Contains(lineIndex))
                                {
                                    //Plugin.Log.Info($"---Floor Wall Skipped for same index: {time} i: {lineIndex} using list: {lineIndexList}");
                                    continue;
                                }
                                else
                                {
                                    if (floorWallToggleCityScape == 0 || floorWallToggleSpires == 0)
                                    {
                                        if (lineIndex > -1 && lineIndex < 4)
                                            continue; // out of range since will pass through player
                                    }

                                    //Plugin.Log.Info($"---Floor Wall added: {time} i: {lineIndex} using list: {lineIndexList}");
                                    usedLineIndexes.Add(lineIndex);
                                }

                                cycleIndex = (cycleIndex + 1) % 4; // Cycle through 4 different sets of values

                                if (time < _endTime)
                                    AddFloorWall(time, j, gaps, lineIndex);

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
                Plugin.LogDebug($"[WallGenerator] Wall floors Count: {_floorWalls.Count}");
            }
        }

        private static void AddFloorWall(float time, int j, List<TimeGap> gaps, int lineIndex)
        {
            // floor walls
            float[] dur   = { .04f, .02f, .02f, .08f };
            int[] widths  = { 1900, 2900, 1400 };//, { 2000, 3000 };
            int[] heights = { 1001 };
            //int[] lineIndexes1 = { -8, -6, -4, -2, 0, 2, 4, 6, 8, 10 }; // narrow
            //int[] lineIndexes2 = { -14, -12, -10, -8, -6, -4, -2, 0, 2, 4, 6, 8, 10, 12, 14, 16, 18 }; //wide

            int height = 1001;

            // skyscraper walls
            if (floorWallToggleCityScape == 0 || floorWallToggleSpires == 0)
            {
                dur = new float[] { .02f };
                heights = new int[] { 1200, 1400, 1400, 1800, 1800, 2000, 2000, 2200, 2600, 3000 };
                //lineIndexes1 = new int[] { -6, -4, -2, 4, 6, 8 }; // removed center player area
                //lineIndexes2 = new int[] { -14, -12, -10, -8, -6, -4, -2, 4, 6, 8, 10, 12, 14, 16, 18 };

                System.Random random = new System.Random();
                height = heights[random.Next(heights.Length)];
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

            if (floorWallToggleCityScape == 0)
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
            else if (floorWallToggleSpires == 0)
            {
                height = (int)(height * 1.5f);
                width = 1300;//(int)(height / 10); 

                //Plugin.Log.Info($"-- Wall Floor: SPIRES! Time: {time}");
            }
            else if (floorWallToggleSmall == 0)
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
            if (floorWallToggleCityScape == 0 || floorWallToggleSpires == 0)
            {
                //Plugin.Log.Info($"Wall EXTENSION Lo Rt: Time: {wallTime}, Index:{indexx}, Layer: {loLayer}, Dur: {duration}, Width: {loWidth}, Height: {height1}");
                _generatedExtensionWalls.Add(customObsData); // these are standard walls not floor walls really
            }
            else
            {
                //Plugin.Log.Info($"Wall EXTENSION Lo Rt: Time: {wallTime}, Index:{indexx}, Layer: {loLayer}, Dur: {duration}, Width: {loWidth}, Height: {height1}");
                _floorWalls.Add(customObsData);
            }

            if (gaps.Count == 0 || gaps.Any(g => g.WithinGap(time))) // sky walls
            {
                customObsData = EObstacleData.Create(time, lineIndex, 8500, duration, width, 1001); // lineLayer 4 & lower square. 5 is flat no height. 6 is normal 7 is very tall, 9 is long thin taller even
                //Plugin.Log.Info($"-- Wall Floor: Time: {time}, Index:{lineIndex}, Layer: 0, Dur: {duration}, Width: {width}, Height: 1001");

                //Plugin.Log.Info($"Wall EXTENSION Lo Rt: Time: {wallTime}, Index:{indexx}, Layer: {loLayer}, Dur: {duration}, Width: {loWidth}, Height: {height1}");
                _floorWalls.Add(customObsData);

            }
            lastHeight = height;

        }

        public static void MegaWalls(List<TimeGap> gaps)
        {
            int divisor = 4;

            int maxPairCount = 4;  // only allow this many sets of walls

            // Tweakables
            const float StepSeconds = 0.75f;   // how often we "roll the dice" inside a gap
            //const float SpawnChance = 0.35f;   // probability per step to spawn a pair
            const float MinPairSpacing = 4.0f;    // minimum spacing between pairs (seconds)
            const float EdgeBuffer = 0.25f;   // keep walls away from gap edges

            //Plugin.Log.Info($"Mega Wall Gaps: {gaps.Count}");

            if (!Config.Instance.EnableBigWalls || !Config.Instance.EnableMappingExtensionsWallsGenerator) return;
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
                        var right = EObstacleData.Create(t,  4, 0, .02f, 50, 50);
                        var left  = EObstacleData.Create(t, -50, 0, .02f, 50, 50);
                        _generatedStandardWalls.Add(right);
                        _generatedStandardWalls.Add(left);

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

            //Plugin.Log.Info($"Mega Wall Pairs: {pairCount} (Total walls: {pairCount * 2})");
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
            int origWallsCount = _originalWalls.Count > 0 ? _originalWalls.Count : eData.Obstacles.Count; // if wall gen is off, then _original walls are never populated. // Github Issue #2 Walls gone when using autolights
            Plugin.LogDebug(
                $"[WallGenerator] Walls Before Rotational Removal Tools - in eData: Total: {eData.Obstacles.Count()} -- Original: {origWallsCount} Standard: {_generatedStandardWalls.Count} Distant: {distantCount} Column: {columnCount} Row: {rowCount} Tunnel: {tunnelCount} Grid: {gridCount} Pane: {paneCount} Particle: {_particleWalls.Count} Floor: {_floorWalls.Count} ^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^");
        }



        // ------------ Wall Alter and Remove ---- They occur in this order ---------------------------------------------------------------------------------------------
        
        /// <summary>
        /// Removes Lean and Crouch Walls for Standard and 360/90 Degree Maps if EnableWallsGen... is enabled. Can turn off all walls individually and it still removes the lean and crouch walls
        /// </summary>
        public static void LeanCrouchWallRemoval() // works with _originalWalls only. only will remove major lean walls that are covering 2 lineIndexes (0 and 1, or 2 and 3)
        {
            if (!Utils.IsEnabledWalls()) return;
            if (_originalWallCount > 5000) return;

            _originalWalls.Sort((a, b) => a.time.CompareTo(b.time));

            //BW noodle extensions causes BS crash in the section somewhere below. Could drill down and figure out why. Haven't figured out how to test for noodle extensions but noodle extension have custom walls that crash Beat Saber so BW added test for custom walls.
            Queue<EObstacleData> obs = new Queue<EObstacleData>(_originalWalls); // since removing items

            int count = 0;

            Plugin.LogDebug($"[LeanCrouchWallRemoval] --- Original Wall Count: {obs.Count}");

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
                        _originalWalls.Remove(ob);
                        count++;
                        continue;
                    }
                    else if (!Config.Instance.AllowLeanWalls &&
                             ((ob.line == 0 && ob.width == 2) || (ob.line == 2 && ob.width > 1)) &&
                             (int)ob.layer < 3) //Lean walls of width 2.
                    {
                        //Plugin.Log.Info($"Remove Lean Wall: Time: {ob.time } cutTime: {cutTime}");
                        _originalWalls.Remove(ob);
                        count++;
                        continue;
                    }
                    else if (!Config.Instance.AllowCrouchWalls &&
                             (ob.line == 0 && ob.width > 2 && ob.layer == 2)) //Crouch walls
                    {
                        //Plugin.Log.Info($"Remove Crouch Wall: Time: {ob.time:F} cutTime: {cutTime}");
                        _originalWalls.Remove(ob);
                        count++;
                        continue;
                    }
                }
            }
            _originalWalls.Sort((a, b) => a.time.CompareTo(b.time));

            _allWalls.AddRange(_originalWalls); // update eData with the modified walls

            _allWallsContainsOriginalWalls = true;

            Plugin.LogDebug($"Lean Crouch Wall Removal End --- Original Standard Wall Count: {_originalWalls.Count} - Walls Removed: {count} -- allWalls Count: {_allWalls.Count}");
        }
        
        public static void MoveWallsBlockingChainTail(EditableCBD eData) // works with _originalWalls and _generatedStandardWalls only
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

            if (!_allWallsContainsOriginalWalls)
            {
                _allWalls.AddRange(_originalWalls);
                _allWallsContainsOriginalWalls = true;
            }
            if (!_allWallsContainsStandardWalls)
            {
                _allWalls.AddRange(_generatedStandardWalls);
                _allWallsContainsStandardWalls = true;
            }

            _allWalls.Sort((a, b) => a.time.CompareTo(b.time));

            Plugin.LogDebug($"MoveWallsBlockingChainTail() ADJUSTING {_allWalls.Count} Walls now for {chains.Count} chains.");

            foreach (var ob in _allWalls)
            {
                if (_originalWalls.Contains(ob) && IsCustomNoodleWall(ob))
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

                        string rot = chainHasRotation.Count() > 0 ? $" - has rotation: {chainHasRotation[i].Item2}" : "";
                        //Plugin.Log.Info($" -- Chain {i} ADJUSTED wall at time {ob.time:F} (dur: {ob.duration:F}) for chain at {chains[i].time:F}. Old lineIndex: {ob.lineIndex}, new: {newLineIndex} {rot}")
                    }
                }
            }
        }


       
        public static void MoveWallsBlockingArc(EditableCBD eData)
        {
            // Retrieve all arcs (normal sliders) from the beatmap.
            // Here we assume that SliderData.Type.Normal represents an arc.

            int currentIndex = 0;            // Used to skip arcs that occur entirely before a wall.
            float minDistance = 0.15f;       // A small time buffer between wall and arc.

            // Ensure _allWalls includes both the original and generated standard walls.
            if (!_allWallsContainsOriginalWalls)
            {
                _allWalls.AddRange(_originalWalls);
                _allWallsContainsOriginalWalls = true;
            }
            if (!_allWallsContainsStandardWalls)
            {
                _allWalls.AddRange(_generatedStandardWalls);
                _allWallsContainsStandardWalls = true;
            }
            _allWalls.Sort((a, b) => a.time.CompareTo(b.time));

            Plugin.LogDebug($"MoveWallsBlockingArc() ADJUSTING {_allWalls.Count} Walls now for {eData.Arcs.Count} arcs.");

            // Loop through each wall.
            foreach (var ob in _allWalls)
            {
                if (_originalWalls.Contains(ob) && IsCustomNoodleWall(ob))
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

                            //Plugin.Log.Info($" -- {i} ADJUSTED a wall intersecting an arc. Wall time: {ob.time:F}, width: {ob.width} layer: {(int)ob.lineLayer} dur: {ob.duration:F}; Arc head time: {arcs[i].time:F} index: {arcs[i].headLineIndex}, tail time: {arcs[i].tailTime:F} index: {arcs[i].tailLine}. Wall old lineIndex: {ob.lineIndex}, new lineIndex: {newLineIndex}");
                        }
                    }
                }
            }
        }

        public static void RemoveIntersectingWalls() // works with _originalWalls, _generatedStandardWalls and _generatedExtensionWalls only. Added this since adding extension walls created tons of intersecting walls
        {
            if (!Utils.IsEnabledWalls()) return;

            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Restart();

            if (!_allWallsContainsOriginalWalls)
            {
                _allWalls.AddRange(_originalWalls);
                _allWallsContainsOriginalWalls = true;
            }
            if (!_allWallsContainsStandardWalls)
            {
                _allWalls.AddRange(_generatedStandardWalls);
                _allWallsContainsStandardWalls = true;
            }

            if (!_allWallsContainsExtensionWalls)
            {
                _allWalls.AddRange(_generatedExtensionWalls);
                _allWallsContainsExtensionWalls = true;
            }

            _allWalls.Sort((a, b) => a.time.CompareTo(b.time));

            var leftWalls = new List<EObstacleData>();
            var rightWalls = new List<EObstacleData>();
            var obstaclesToDelete = new List<EObstacleData>();

            foreach (var obs in _allWalls)
            {
                if (obs.line > 1) rightWalls.Add(obs);
                else leftWalls.Add(obs);
            }

            RemoveIntersectingWallsByList(leftWalls);
            RemoveIntersectingWallsByList(rightWalls);

            _allWalls.Clear();

            _allWalls.AddRange(leftWalls);
            _allWalls.AddRange(rightWalls);

            foreach (var obs in obstaclesToDelete)
            {
                _allWalls.Remove(obs);
            }

            Plugin.LogDebug($"--- Remove Intersecting Walls --- Remaining Walls: {_allWalls.Count} --- Total Removed: {obstaclesToDelete.Count}");

            Plugin.LogDebug($" ------- Time Elapsed: {stopwatch.ElapsedMilliseconds / 1000.0:F1}.");
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

        public static List<ERotationEventData> RemoveCrouchWallRotations(List<ERotationEventData> rotations)
        {
            List<EObstacleData> crouchWalls = new List<EObstacleData>();

            if (Config.Instance.AllowCrouchWalls) 
            {
                foreach (var ob in _originalWalls) 
                { 
                    if (ob.line == 0 && ob.width > 2 && ob.layer == 2)
                        crouchWalls.Add(ob); 
                } 
            }
            if (crouchWalls.Count == 0)
            {
                Plugin.LogDebug("[RemoveCrouchWallRotations] No crouch wall found. No rotations to remove.");
                return rotations;
            }
            else
                Plugin.LogDebug($"[RemoveCrouchWallRotations] Crouch walls found: {crouchWalls.Count}.");

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
            bool late = Config.Instance.RotationModeLate;
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
                bool inLate = (t > intervals[j].start + EPS) && (t < intervals[j].end - EPS);
                bool inEarly = (t >= intervals[j].start - EPS) && (t <= intervals[j].end + EPS);

                if ((late && inLate) || (!late && inEarly))
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
                    Plugin.LogDebug($"[RemoveCrouchWallRotations] Removed Rotation at: {rotations[i].time:F}");
                    count++;
                }

            Plugin.LogDebug($"[RemoveCrouchWallRotations] Total Rotations Removed: {count}");

            return result;
        }

        // Helper: merge [time, time+duration] of crouch walls into disjoint intervals
        private static List<(float start, float end)> MergeCrouchIntervals(List<EObstacleData> crouchWalls)
        {
            // Project to intervals
            var arr = new (float s, float e)[crouchWalls.Count];
            for (int i = 0; i < crouchWalls.Count; i++)
            {
                float s = crouchWalls[i].time;
                float e = s + crouchWalls[i].duration;
                if (e < s) { var tmp = s; s = e; e = tmp; } // just in case
                arr[i] = (s, e);
            }

            // Sort by start
            Array.Sort(arr, (a, b) => a.s.CompareTo(b.s));

            // Linear merge
            var merged = new List<(float start, float end)>(arr.Length);
            const float EPS = 1e-4f;

            for (int i = 0; i < arr.Length; i++)
            {
                if (merged.Count == 0)
                {
                    merged.Add((arr[i].s, arr[i].e));
                    continue;
                }

                var last = merged[merged.Count - 1];
                if (arr[i].s <= last.end + EPS) // overlaps/adjacent → extend
                {
                    float newEnd = (arr[i].e > last.end) ? arr[i].e : last.end;
                    merged[merged.Count - 1] = (last.start, newEnd);
                }
                else
                {
                    merged.Add((arr[i].s, arr[i].e));
                }
            }

            return merged;
        }

        public static void FinalizeWallsToMap(EditableCBD eData)
        {
            //Stopwatch stopwatch = new Stopwatch();
            //stopwatch.Restart();
            if (_originalWalls.Count == 0) // Github Issue #2 Walls gone when using autolights
            {
                _allWalls.AddRange(eData.Obstacles); // in case not already done
                _allWallsContainsOriginalWalls = true;
            }

            if (!_allWallsContainsOriginalWalls)
            {
                _allWalls.AddRange(_originalWalls);
                _allWallsContainsOriginalWalls = true;
            }
            if (!_allWallsContainsStandardWalls)
            {
                _allWalls.AddRange(_generatedStandardWalls);
                _allWallsContainsStandardWalls = true;
            }
            if (!_allWallsContainsExtensionWalls)
            {
                _allWalls.AddRange(_generatedExtensionWalls);
                _allWallsContainsExtensionWalls = true;
            }
            if (!_allWallsContainsParticleWalls)
            {
                _allWalls.AddRange(_particleWalls);
                _allWallsContainsParticleWalls = true;
            }
            if (!_allWallsContainsFloorWalls)
            {
                _allWalls.AddRange(_floorWalls);
                _allWallsContainsFloorWalls = true;
            }

            _allWalls.Sort((a, b) => a.time.CompareTo(b.time));

            eData.Obstacles = _allWalls;

            Plugin.LogDebug($"[FinalizeWallsToMap] Walls Finalized Count: {eData.Obstacles.Count}");

            //Plugin.LogDebug($" ------- Add all walls Time Elapsed: {stopwatch.ElapsedMilliseconds / 1000.0:F1}.");
            //stopwatch.Stop();
        }
        public static void FinalizeOriginalOnlyWallsToMap(EditableCBD eData)
        {
            // Github Issue #2 Walls gone when using autolights
            if (_originalWalls.Count == 0)
            {
                _allWalls.AddRange(eData.Obstacles); // in case not already done
                _allWallsContainsOriginalWalls = true;
            }

            if (!_allWallsContainsOriginalWalls)
            {
                _allWalls.AddRange(_originalWalls);
                _allWallsContainsOriginalWalls = true;
            }
            

            _allWalls.Sort((a, b) => a.time.CompareTo(b.time));

            eData.Obstacles = _allWalls;

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
    }
}