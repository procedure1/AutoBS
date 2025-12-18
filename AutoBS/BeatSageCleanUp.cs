using AutoBS;
using AutoBS.Patches;
using BS_Utils.Gameplay;
using CustomJSONData.CustomBeatmap;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Permissions;
using static BeatmapSaveDataVersion2_6_0AndEarlier.BeatmapSaveData;
using static NoteData;

namespace AutoBS
{
    internal class BeatSageCleanUp
    {
        public static bool DisableScoreSubmission = false;

        //REMOVE NOTES (and bombs) located at the same space and also remove any notes side by side with cutDirections facing out 
        public static void Clean(EditableCBD eData) // is reference so don't need to return it (unless you create a new instance of EditableCBD and then return that)
        {
            Plugin.LogDebug($"[BeatSageCleanUp] Beat Sage Map being cleaned!");

            int originalNoteCount = eData.ColorNotes.Count();

            BeatSageStrayNoteCleaner.RemoveStragglers(eData, TransitionPatcher.bpm, minPauseSeconds: Config.Instance.StrayNoteCleanerOffset, maxStragglers: 4);

            eData = notesAdjustment(eData);
            eData = wallsAdjustment(eData);

            if (eData.ColorNotes.Count() < originalNoteCount)
            {
                DisableScoreSubmission = true;
            }
            else
                DisableScoreSubmission = false;
        }

        #region Adjust Notes
        private static EditableCBD notesAdjustment(EditableCBD eData)
        {
            List<ENoteData> notes = eData.ColorNotes.ToList();

            int originalNoteCount = notes.Count();

            List<int> indicesToRemove = new List<int>();

            Plugin.LogDebug($"[BeatSageCleanUp] Studying {notes.Count} notes.");

            for (int i = 0; i < notes.Count; i++)
            {
                ENoteData currentNote = notes[i];

                // Iterate through the next three notes
                for (int j = i + 1; j < Math.Min(i + 4, notes.Count); j++)
                {
                    //Plugin.Log.Info($"Beat Sage j: {j}"); 

                    ENoteData nextNote = notes[j];

                    //if (Math.Round(currentNote.time, 2) == 17.58 && Math.Round(nextNote.time, 2) == 17.58)
                    //    Plugin.Log.Info($"BW 1 ********Found the offending notes!!!!!*******************");

                    // Check if the 2 notes are the same time or within .0001 sec of each other so they appear to almost overlap
                    if (nextNote.time - currentNote.time <= 0.05f)//0.03 seems good. 0.08 will start to catch notes from different beats.
                    {

                        // FIX: same-color, near-simultaneous, same-direction, but misaligned for a single stroke → remove later note (j)
                        // FIX: same-color, near-simultaneous pair rules
                        if (currentNote.gameplayType == GameplayType.Normal &&
                            nextNote.gameplayType == GameplayType.Normal &&
                            currentNote.colorType == nextNote.colorType &&
                            currentNote.cutDirection != NoteCutDirection.Any &&
                            nextNote.cutDirection != NoteCutDirection.Any)
                        {
                            var cutDirCurrentNote = currentNote.cutDirection;
                            var cutDirNextNote = nextNote.cutDirection;

                            bool bothHoriz =
                                (cutDirCurrentNote == NoteCutDirection.Left || cutDirCurrentNote == NoteCutDirection.Right) &&
                                (cutDirNextNote == NoteCutDirection.Left || cutDirNextNote == NoteCutDirection.Right);

                            bool bothVert =
                                (cutDirCurrentNote == NoteCutDirection.Up || cutDirCurrentNote == NoteCutDirection.Down) &&
                                (cutDirNextNote == NoteCutDirection.Up || cutDirNextNote == NoteCutDirection.Down);

                            bool sameDirection = cutDirCurrentNote == cutDirNextNote;

                            // Horizontal same-color pair rules
                            if (bothHoriz)
                            {
                                // Must match direction & layer
                                if (!sameDirection || currentNote.layer != nextNote.layer)
                                {
                                    indicesToRemove.Add(j);
                                    Plugin.LogDebug(
                                        $"[BeatSageCleanUp] horiz same-color mismatch dir or layer at {currentNote.time:F} → rm later " +
                                        $"(dirA={cutDirCurrentNote}, dirB={cutDirNextNote}, layerA={currentNote.layer}, layerB={nextNote.layer})"
                                    );
                                    j++;
                                    continue;
                                }
                            }

                            // Vertical same-color pair rules
                            if (bothVert)
                            {
                                // Must match direction & index
                                if (!sameDirection || currentNote.line != nextNote.line)
                                {
                                    indicesToRemove.Add(j);
                                    Plugin.LogDebug(
                                        $"[BeatSageCleanUp] vert same-color mismatch dir or index at {currentNote.time:F} → rm later " +
                                        $"(dirA={cutDirCurrentNote}, dirB={cutDirNextNote}, lineA={currentNote.line}, lineB={nextNote.line})"
                                    );
                                    j++;
                                    continue;
                                }
                            }
                        }




                        //Plugin.Log.Info($"Beat Sage found 2 notes at the exact same time (or close) of {currentNote.time} current note: {currentNote.gameplayType} index: {currentNote.line} layer: {currentNote.layer} --- Nextnote: {nextNote.gameplayType} index: {nextNote.line} layer: {nextNote.layer}");

                        // Check for SIDE-BY-SIDE Notes. -- Check if the two notes (not any bombs) have the same layer, and different index (they may be side-by-side)
                        if (currentNote.layer == nextNote.layer && // Check for same layer
                            Math.Abs(currentNote.line - nextNote.line) == 1 && //side-by-side based on index values
                            currentNote.gameplayType == GameplayType.Normal && // Check if both are "Normal" notes
                            nextNote.gameplayType == GameplayType.Normal)
                        {
                            //if (Math.Round(currentNote.time, 2) == 17.58 && Math.Round(nextNote.time, 2) == 17.58)
                            //    Plugin.Log.Info($"BW 3 ********Found the offending notes!!!!!*******************");

                            // 2 side-by-side notes of the SAME color must both cut left or right
                            if (currentNote.colorType == nextNote.colorType)
                            {
                                var a = currentNote.cutDirection;
                                var b = nextNote.cutDirection;
                                bool bothLeft = a == NoteCutDirection.Left && b == NoteCutDirection.Left;
                                bool bothRight = a == NoteCutDirection.Right && b == NoteCutDirection.Right;
                                bool goodPair = bothLeft || bothRight;

                                if (!goodPair)
                                {
                                    indicesToRemove.Add(i);
                                    Plugin.LogDebug($"Beat Sage 1 - remove note side-by-side SAME COLOR with another note in impossible cutDirection at {currentNote.time:F}/{nextNote.time:F} - {currentNote.cutDirection} - {nextNote.cutDirection}");
                                }
                            }
                            // 2 side-by-side notes of different colors - Check if the leftmost note has cutDirection Left and the rightmost note has cutDirection Right - and other impossible configurations
                            else if (currentNote.line < nextNote.line)
                            {

                                if ((currentNote.cutDirection == NoteCutDirection.Left) ||
                                    (currentNote.cutDirection == NoteCutDirection.Right) ||
                                   ((currentNote.cutDirection == NoteCutDirection.DownLeft) && (nextNote.cutDirection != NoteCutDirection.DownLeft && nextNote.cutDirection != NoteCutDirection.UpRight)) ||
                                   ((currentNote.cutDirection == NoteCutDirection.UpLeft) && (nextNote.cutDirection != NoteCutDirection.UpLeft && nextNote.cutDirection != NoteCutDirection.DownRight)))
                                {
                                    indicesToRemove.Add(i);
                                    Plugin.LogDebug($"Beat Sage 2 - remove left note side-by-side with another note in impossible cutDirection at {currentNote.time:F}/{nextNote.time:F} - {currentNote.cutDirection} - {nextNote.cutDirection}");
                                }
                            }
                            else
                            {
                                if ((currentNote.cutDirection == NoteCutDirection.Left) ||
                                    (currentNote.cutDirection == NoteCutDirection.Right) ||
                                   ((currentNote.cutDirection == NoteCutDirection.DownRight) && (nextNote.cutDirection != NoteCutDirection.DownRight && nextNote.cutDirection != NoteCutDirection.UpLeft)) ||
                                   ((currentNote.cutDirection == NoteCutDirection.UpRight) && (nextNote.cutDirection != NoteCutDirection.UpRight && nextNote.cutDirection != NoteCutDirection.DownLeft)))
                                {
                                    indicesToRemove.Add(i);
                                    Plugin.LogDebug($"Beat Sage 3 - remove right note side-by-side with another note in impossible cutDirection at {currentNote.time:F}/{nextNote.time:F} - {currentNote.cutDirection} - {nextNote.cutDirection}");
                                }
                            }
                        }
                        // Check for ONE-ABOVE-THE-OTHER Notes. -- Check if the two notes (not any bombs) have the same index, and different layer (they may be one-above-the-other)
                        else if (currentNote.line == nextNote.line && // Check for same index
                                 Math.Abs(currentNote.layer - nextNote.layer) == 1 && //one-above-the-other based on layer values
                                 currentNote.gameplayType == GameplayType.Normal && // Check if both are "Normal" notes
                                 nextNote.gameplayType == GameplayType.Normal)
                        {
                            // 2 ONE-ABOVE-THE-OTHER notes of the same color must both cut up or down
                            if (currentNote.colorType == nextNote.colorType)
                            {
                                var a = currentNote.cutDirection;
                                var b = nextNote.cutDirection;
                                bool bothUp = a == NoteCutDirection.Up && b == NoteCutDirection.Up;
                                bool bothDown = a == NoteCutDirection.Down && b == NoteCutDirection.Down;
                                bool goodPair = bothUp || bothDown;

                                if (!goodPair)
                                {
                                    indicesToRemove.Add(i);
                                    Plugin.LogDebug($"Beat Sage 1 - remove note one-above-the-other SAME COLOR with other note in impossible cutDirection at {currentNote.time:F}/{nextNote.time:F} - {currentNote.cutDirection} - {nextNote.cutDirection}");
                                }
                            }
                            // 2 ONE-ABOVE-THE-OTHER notes of different colors - Check if the bottommost note has cutDirection Down and the uppermost note has cutDirection Up - and other impossible configurations
                            else if (currentNote.layer < nextNote.layer)
                            {
                                if ((currentNote.cutDirection == NoteCutDirection.Down) ||
                                    (currentNote.cutDirection == NoteCutDirection.Up) ||
                                   ((currentNote.cutDirection == NoteCutDirection.DownLeft) && (nextNote.cutDirection != NoteCutDirection.DownLeft && nextNote.cutDirection != NoteCutDirection.UpRight)) ||
                                   ((currentNote.cutDirection == NoteCutDirection.DownRight) && (nextNote.cutDirection != NoteCutDirection.DownRight && nextNote.cutDirection != NoteCutDirection.UpLeft)))
                                {
                                    indicesToRemove.Add(i);
                                    Plugin.LogDebug($"Beat Sage 2 - remove bottom note one-above-the-other of DIFFERENT COLORS with another note in impossible cutDirection at {currentNote.time:F}/{nextNote.time:F} - {currentNote.cutDirection} - {nextNote.cutDirection}");
                                }
                            }
                            else
                            {
                                if ((currentNote.cutDirection == NoteCutDirection.Down) ||
                                    (currentNote.cutDirection == NoteCutDirection.Up) ||
                                   ((currentNote.cutDirection == NoteCutDirection.UpLeft) && (nextNote.cutDirection != NoteCutDirection.UpLeft && nextNote.cutDirection != NoteCutDirection.DownRight)) ||
                                   ((currentNote.cutDirection == NoteCutDirection.UpRight) && (nextNote.cutDirection != NoteCutDirection.UpRight && nextNote.cutDirection != NoteCutDirection.DownLeft)))
                                {
                                    indicesToRemove.Add(i);
                                    Plugin.LogDebug($"Beat Sage 3 - remove top note one-above-the-other of DIFFERENT COLORS with another note in impossible cutDirection at {currentNote.time:F}/{nextNote.time:F} - {currentNote.cutDirection} - {nextNote.cutDirection}");
                                }
                            }
                        }
                        // Check for OVERLAPPING NOTES. -- Check if the two notes have the same lineIndex, and noteLineLayer
                        else if (currentNote.line == nextNote.line &&
                                 currentNote.layer == nextNote.layer)
                        {
                            //Plugin.Log.Info($"Found overlapping notes at: {currentNote.time:F} of type: {currentNote.gameplayType} & {nextNote.gameplayType}. Should delete one of them in next log.");
                            // Check if either of the notes is a bomb
                            if (currentNote.gameplayType == GameplayType.Bomb)
                            {
                                // Remove the bomb note (1st note)
                                indicesToRemove.Add(i);
                                Plugin.LogDebug($"Beat Sage 1 - remove bomb overlapping a 2nd bomb/note at {currentNote.time:F}/{nextNote.time:F}");
                            }
                            else
                            {
                                // remove the 2nd note whether a bomb or not
                                indicesToRemove.Add(j);
                                j++; //since j is removed, skip it in the next iteration
                                Plugin.LogDebug($"Beat Sage 2 - remove type: {nextNote.gameplayType} overlapping a note at {currentNote.time:F}/{nextNote.time:F}");
                            }
                        }
                    }
                    else
                        break;//exits loop if a notes has time beyond .03sec from the currentNote
                }
            }

            if (indicesToRemove.Count == 0)
            {
                Plugin.LogDebug($"[BeatSageCleanUp] - No notes to clean!!!!!! - Remaining notes: {notes.Count}");
            }
            else
            {
                // Remove the duplicate notes from the original list in reverse order to avoid index issues
                for (int i = indicesToRemove.Count - 1; i >= 0; i--)
                {
                    int indexToRemove = indicesToRemove[i];
                    ENoteData noteToRemove = notes[indexToRemove]; // Get the reference before removing it from notes
                    //Plugin.Log.Info($"Beat Sage Map had a note/bomb cut at: {noteToRemove.time:F}");

                    notes.RemoveAt(indexToRemove); // Remove from notes list
                                                   // Directly remove the note object from allBeatmapDataItems
                    if (eData.ColorNotes.Contains(noteToRemove))
                    {
                        eData.ColorNotes.Remove(noteToRemove);
                    }
                    else
                    {
                        //Plugin.Log.Info($"Note at {noteToRemove.time:F} was not found in allBeatmapDataItems and could not be removed.");
                    }
                }

                notes = eData.ColorNotes.ToList(); // do this since notes have been changed above

                Plugin.Log.Info($"[BeatSageCleanUp] - Notes Removed {indicesToRemove.Count} - Remaining notes: {notes.Count}");
            }
                
            return eData;

        }
        #endregion

        #region Adjust Walls
        private static EditableCBD wallsAdjustment(EditableCBD eData)
        {
            Plugin.LogDebug($"[BeatSageCleanUp] Adjusting Walls in Beat Sage Map!");
            List<EObstacleData> obs = eData.Obstacles.ToList();
            List<ENoteData> notes = eData.ColorNotes.ToList();
            //Plugin.Log.Info($"[BeatSageCleanUp] Found {obs.Count} obstacles and {notes.Count} notes in Beat Sage Map!");

            //----------------------------------------------------------------------------------------------------
            // Adjust walls based on notes and other walls
            //----------------------------------------------------------------------------------------------------
            float minTimeBetweenWalls = .3f; // User-set variable for minimum time between walls
            float minTimeBetweenWallsAndNotes = .25f; //.1 was maybe a little too short but ok
            float minWallDuration = Config.Instance.MinWallDuration;

            List<EObstacleData> finalWalls = new List<EObstacleData>();

            EObstacleData prevLeanOrCrouchWall = null;

            int obCnt = 0;

            int originalObstacleCount = obs.Count();

            int lastProcessedIndex = 0;

            foreach (var ob in obs)
            {
                obCnt++;
                float startTime = ob.time;
                float duration = ob.duration;
                int line = ob.line; // indexes outside of 0-3 do not work!!!!!
                float endTime = ob.time + ob.duration;

                int right = line;
                int left  = line + ob.width - 1;
                bool outsideRange = (left < 0) || (right > 3);

                if (outsideRange)
                {
                    finalWalls.Add(ob);  // keep original as-is
                    continue;
                }


                EObstacleData customObstacle = null;

                for (int i = lastProcessedIndex; i < notes.Count; i++) // Check if there is already a wall
                {
                    ENoteData note = notes[i];

                    if (note.time < startTime - minTimeBetweenWallsAndNotes)
                    {
                        lastProcessedIndex = i;
                        continue;
                    }
                    else if (note.time > startTime + duration + 5f) // not sure why but need a large buffer like 2 to 5 sec here to prevent missing some walls with this break.
                    {
                        break;
                    }
                    //if (startTime > 171f && startTime < 174f)
                    //{
                    //    Plugin.Log.Info($"--- {obCnt} Beat Sage Map Studying wall at: {startTime:F} dur: {duration:F} line: {line} layer: {ob.layer} width: {ob.width} - note:time {note.time} line: {note.line} layer: {note.layer}");
                    //}
                    // Check if a note is too close to the beginning of a wall or just inside the beginning
                    if (note.time > startTime - minTimeBetweenWallsAndNotes && // *** have to use startTime, duration, etc here since if alter wall for a note, the next notes need to see the new version of the wall.
                        note.time < startTime + minTimeBetweenWallsAndNotes &&
                        //(int)note.layer + 1 >= (int)ob.layer && // Note is inside or behind wall a note at layer 1 will be inside a wall at layer 2 since walls hang lower than their layer
                         ((note.line >= line && line > 1) ||
                          (note.line <= line && line < 2)))
                    {

                        if (endTime > note.time + minTimeBetweenWallsAndNotes)
                        {
                            if (duration - minTimeBetweenWallsAndNotes > minWallDuration)
                            {
                                float oldStartTime = startTime;
                                startTime = note.time + minTimeBetweenWallsAndNotes;

                                //Plugin.Log.Info(
                                //    $"--- {obCnt} Beat Sage Map NEW START TIME - wall: {oldStartTime:F} dur: {duration:F} had a note too close to the beginning of a wall. note: {note.time:F} and will cut beginning of the wall to start at: {startTime:F}");

                                customObstacle = EObstacleData.Create(startTime,
                                    line, ob.layer, duration, ob.width, ob.height
                                 );

                            }
                            else
                            {
                                //Plugin.Log.Info(
                                //    $"--- {obCnt} Beat Sage Map DELETE WALL -    wall: {startTime:F} dur: {duration:F}  had a note too close to the beginning of a wall. note: {note.time:F}. New duration is too short so DELETING WALL!!!!1");
                                break; // no need to check anymore notes on the deleted ob
                            }
                        }
                        else
                        {
                            //Plugin.Log.Info(
                            //       $"--- {obCnt} Beat Sage Map DELETE WALL -    wall: {startTime:F} dur: {duration:F} had a note too close to the beginning of a wall. note: {note.time:F}. New duration is too short so DELETING WALL!!!!2");
                            break;
                        }

                    }
                    // Check if a note is too close to the end of a wall or just inside the end
                    else if (note.time > endTime - minTimeBetweenWallsAndNotes &&
                              note.time < endTime + minTimeBetweenWallsAndNotes &&
                              //(int)note.layer + 1 >= (int)ob.layer && // Note is inside or behind wall a note at layer 1 will be inside a wall at layer 2 since walls hang lower than their layer
                              ((note.line >= line && line > 1) ||
                               (note.line <= line && line < 2)))
                    {
                        if (startTime < note.time - minTimeBetweenWallsAndNotes)
                        {
                            endTime = note.time - minTimeBetweenWallsAndNotes;
                            float oldDuration = duration;
                            float newDuration = endTime - startTime;

                            if (newDuration > minWallDuration)
                            {
                                duration = newDuration;

                                //Plugin.Log.Info(
                                //    $"--- {obCnt} Beat Sage Map NEW DURATION -   wall: {startTime:F} dur: {oldDuration:F} had a note too close to the end of a wall. wall end: {endTime} note: {note.time:F} New duration: {duration:F}");
                                customObstacle = EObstacleData.Create(startTime,
                                    line, ob.layer, duration, ob.width, ob.height);

                            }
                            else
                            {
                                //Plugin.Log.Info(
                                //    $"--- {obCnt} Beat Sage Map DELETE WALL -    wall: {startTime:F} dur: {duration:F} had a note too close to the end of a wall. wall end: {endTime} note: {note.time:F}. New duration is too short so DELETING WALL!!!!");
                                break;
                            }
                        }
                    }
                    // Check if note is inside or behind wall
                    else if ((note.time >= startTime && note.time <= endTime) &&
                             (int)note.layer + 1 >= ob.layer) // Check if the note is within the time and lineLayer boundaries of the obstacle
                    {
                        if (note.line <= (line + ob.width - 1) && line < 2) // note is inside a left-side wall (width has to be taken into account)
                        {
                            line = note.line - 1 - (ob.width - 1); // have to take width into account only on left side  //FIX!!!! Being around you expl wall at 148sec index0 width2 has note inside but when move wall to -1 it seems to double the wall and note is still inside.

                            //newObstacleEndTime = note.time - .3f;
                            //newDuration = newObstacleEndTime - newStartTime;

                            //Plugin.Log.Info(
                            //        $"--- {obCnt} Beat Sage Map NEW LINE INDEX - wall: {startTime:F} dur: {duration:F} had a note/bomb inside a left wall. note: {note.time:F} and will move wall to lineIndex: {lineIndex}");// shorten wall to end here: {newObstacleEndTime}");
                            customObstacle = EObstacleData.Create(startTime,
                                line, ob.layer, duration, ob.width, ob.height);
                        }
                        else if (note.line >= line && line > 1) // note is inside a right-side wall (width doesn't have to be taken into account)
                        {
                            line = note.line + 1;

                            //newObstacleEndTime = note.time - .3f;
                            //newDuration = newObstacleEndTime - newStartTime;

                            //Plugin.Log.Info(
                            //        $"--- {obCnt} Beat Sage Map NEW LINE INDEX - wall: {startTime:F} dur: {duration:F} had a note/bomb inside a right wall. note: {note.time:F} and will move wall to lineIndex: {lineIndex}");// shorten wall to end here: {newObstacleEndTime}");
                            customObstacle = EObstacleData.Create(startTime,
                                line, ob.layer, duration, ob.width, ob.height);
                        }
                    }
                    else if (note.time > endTime) // wall is fine and unaffected by note. so add old wall to new collection
                    {
                        customObstacle = EObstacleData.Create(startTime,
                            line, ob.layer, duration, ob.width, ob.height);
                    }
                }

                //-------------------------------------------------------------

                bool isLeanWall = false;
                bool isCrouchWall = false;

                if (ob.layer == 2 &&
                    ((line == 0 && ob.width > 2) || (line == 1 && ob.width > 1)))
                {
                    isCrouchWall = true;
                    //Plugin.Log.Info(
                    //    $"--- {obCnt} Beat Sage Map ---------------- Found CROUCH Wall - wall: {startTime:F} duration: {duration:F} i: {lineIndex} w:{ob.width} l: {(int)ob.lineLayer} h: {ob.height}");
                }
                else if ((ob.width > 1 && line == 2) ||
                         (ob.width == 2 && line == 0) ||
                         (ob.width == 1 && (line == 1 || line == 2)))
                {
                    isLeanWall = true;
                    //Plugin.Log.Info(
                    //    $"--- {obCnt} Beat Sage Map ---------------- Found LEAN Wall - wall: {startTime:F} duration: {duration:F} i: {lineIndex} w:{ob.width} l: {(int)ob.lineLayer} h: {ob.height}");
                }

                // Make sure lean or crouch walls are not close together and positioned, so it's hard for the player to move between them.
                if (prevLeanOrCrouchWall != null && (isCrouchWall || isLeanWall))
                {
                    float prevObstacleEndTime = prevLeanOrCrouchWall.time + prevLeanOrCrouchWall.duration;

                    bool oneLaneApart = false; // if one lane apart then hard for player to avoid, perhaps. if on the same lane or if more than 1 lane apart then don't have to move your body to avoid the wall so don't need to alter the wall

                    EObstacleData leftObst = null;
                    EObstacleData rightObst = null;

                    if (prevLeanOrCrouchWall.line < line)
                    {
                        leftObst = prevLeanOrCrouchWall;
                        rightObst = customObstacle;
                    }
                    else if (prevLeanOrCrouchWall.line > line)
                    {
                        rightObst = prevLeanOrCrouchWall;
                        leftObst = customObstacle;
                    }
                    if (leftObst != null & rightObst != null)
                    {
                        oneLaneApart = (rightObst.line) - (leftObst.line + leftObst.width - 1) == 1;
                    }

                    if (oneLaneApart && startTime - prevObstacleEndTime < minTimeBetweenWalls)
                    {
                        float oldStartTime = startTime;
                        float oldDuration = duration;
                        float newStartTime = prevObstacleEndTime + minTimeBetweenWalls;
                        // Recalculate the new duration based on the adjusted start time
                        float newDuration = duration + (oldStartTime - startTime);// (newStartTime + newDuration) - newStartTime;

                        if (duration > minWallDuration)
                        {
                            startTime = newStartTime;
                            duration = newDuration;

                            //Plugin.Log.Info(
                            //        $"--- {obCnt} Beat Sage Map NEW START TIME - wall: {oldStartTime:F} dur: {oldDuration:F} is too close to another wall. New time: {startTime:F} new dur: {duration:F}!");
                            customObstacle = EObstacleData.Create(startTime,
                                line, ob.layer, duration, ob.width, ob.height);
                        }
                        else
                        {
                            //Plugin.Log.Info(
                            //        $"--- {obCnt} Beat Sage Map DELETED WALL -   wall: {startTime:F} dur: {duration:F} is too close to another wall.  New duration is too short!!!!!!!");
                            break;
                        }

                    }
                }


                if (!isLeanWall && !isCrouchWall && duration > minWallDuration)
                {
                    if (customObstacle != null)
                        finalWalls.Add(customObstacle);

                    continue; // Non-crouch/lean walls are just passed through
                }

                // Apply the maxCrouchWallDuration limit after adjusting for proximity to ensure it's not overwritten
                if (isCrouchWall)
                {

                    // Adjust the duration for crouch walls, ensuring it does not exceed maxCrouchWallDuration
                    // and is not negative due to start time adjustment
                    float oldDuration = duration;
                    float newDuration = Math.Min(Config.Instance.MaxCrouchWallDuration, Math.Max(0, duration));

                    if (oldDuration > newDuration)
                    {
                        duration = newDuration;

                        //Plugin.Log.Info($"--- {obCnt} Beat Sage Map NEW DURATION - wall: {startTime:F} duration: {oldDuration:F} a crouch wall that was too long. New dur: {duration:F}!");
                        customObstacle = EObstacleData.Create(startTime,
                            line, ob.layer, duration, ob.width, ob.height);
                    }
                }

                // Skip adding this wall if the adjusted duration is 0, effectively deleting it
                if (duration <= minWallDuration)
                {
                    //Plugin.Log.Info($"--- {obCnt} Beat Sage Map DELETED WALL -   wall: {startTime:F} duration: {duration:F} a crouch or lean wall that is too close to another at time. New duration would be too short!!!!");
                    break;
                    // This wall is skipped because its adjusted duration is invalid
                }
                if (customObstacle != null)
                    finalWalls.Add(customObstacle);

                if (isCrouchWall || isLeanWall)
                    prevLeanOrCrouchWall = customObstacle;
            }

            finalWalls.Sort((a, b) => a.time.CompareTo(b.time));

            foreach (var ob in obs) // Clear existing obstacles from BeatmapData so obstacles are empty
            {
                eData.Obstacles.Remove(ob);
            }


            int theCount = 0;
            foreach (var ob in finalWalls)
            {
                theCount++;
                eData.Obstacles.Add(ob);
                //Plugin.Log.Info($"--- {theCount} Time: {ob.time:F} dur: {ob.duration:F} i: {ob.line} l: {(int)ob.lineLayer} w: {ob.width} h: {ob.height}");
            }

            eData.Obstacles = eData.Obstacles.OrderBy(o => o.time).ToList(); // Sort the obstacles by time

            Plugin.Log.Info($"[BeatSageCleanUp] - Original Obstacles Cleaned: {originalObstacleCount}. Final Count: {theCount}.");

            return eData;
        }


        private static class BeatSageStrayNoteCleaner
        {
            public static float bps = 120f;
            /// <summary>
            /// Remove small clusters (1..maxStragglers) of notes at the beginning/end of a song
            /// when they are separated from the main body by a long pause (>= minPauseSeconds).
            /// Operates on eData.ColorNotes (ENoteData has .time in beats).
            /// </summary>
            /// <returns>Total notes removed</returns>
            public static int RemoveStragglers(
                EditableCBD eData,
                float bpm,
                float minPauseSeconds = 6f, // if user sets to 0 , then disable
                int maxStragglers = 4)
            {
                Plugin.LogDebug($"[BeatSage-StrayNoteCleaner] Called! bpm: {bpm} minPauseSeconds: {minPauseSeconds} maxStragglers: {maxStragglers} Note Count: {eData.ColorNotes?.Count}");
                if (minPauseSeconds == 0 || eData == null || eData.ColorNotes == null || eData.ColorNotes.Count == 0)
                {
                    return 0;
                }

                bps = bpm / 60f;

                // Work on a sorted copy (by time), then remove from the original list by reference
                var notes = eData.ColorNotes.OrderBy(n => n.time).ToList();
                int totalRemoved = 0;
                bool changed;

                do
                {
                    changed = false;
                    if (notes.Count == 0) break;

                    // Build segments separated by big gaps (>= pauseBeats)
                    var segments = BuildSegments(notes, minPauseSeconds);
                    if (segments.Count <= 1)
                        break; // nothing to do: no large pauses splitting the map

                    // Try to remove leading straggler segment
                    var first = segments[0];
                    if (SegmentSize(first) <= maxStragglers)
                    {
                        int removed = RemoveSegment(eData.ColorNotes, notes, first);
                        if (removed > 0)
                        {
                            totalRemoved += removed;
                            changed = true;
                            continue; // rebuild segments next iteration
                        }
                    }

                    // Try to remove trailing straggler segment
                    var last = segments[segments.Count - 1];
                    if (SegmentSize(last) <= maxStragglers)
                    {
                        int removed = RemoveSegment(eData.ColorNotes, notes, last);
                        if (removed > 0)
                        {
                            totalRemoved += removed;
                            changed = true;
                            continue; // rebuild segments next iteration
                        }
                    }

                } while (changed);

                Plugin.Log.Info($"[BeatSageCleanUp][StrayNoteCleaner] Notes Removed: {totalRemoved}.");
                return totalRemoved;
            }

            // A segment is [startIndex, endIndex] inclusive in the sorted 'notes' list
            private struct Seg { public int S; public int E; public Seg(int s, int e) { S = s; E = e; } }
            private static int SegmentSize(Seg seg) => seg.E >= seg.S ? (seg.E - seg.S + 1) : 0;

            private static List<Seg> BuildSegments(List<ENoteData> notes, float pauseBeats)
            {
                var segments = new List<Seg>();
                int n = notes.Count;
                int start = 0;

                for (int i = 0; i < n - 1; i++)
                {
                    float gap = notes[i + 1].time - notes[i].time;
                    if (gap >= pauseBeats)
                    {
                        segments.Add(new Seg(start, i));
                        start = i + 1;
                    }
                }
                segments.Add(new Seg(start, n - 1));
                return segments;
            }

            private static int RemoveSegment(List<ENoteData> originalList, List<ENoteData> sortedList, Seg seg)
            {
                if (seg.S > seg.E) return 0;

                // Collect references to remove (match by reference, not by value)
                var toRemove = new HashSet<ENoteData>();
                for (int i = seg.S; i <= seg.E; i++)
                    toRemove.Add(sortedList[i]);

                int before = originalList.Count;
                originalList.RemoveAll(n => toRemove.Contains(n));
                int removed = before - originalList.Count;

                // Also remove from our working sorted list
                sortedList.RemoveAll(n => toRemove.Contains(n));

                if (removed > 0)
                {
                    float segStartBeat = toRemove.Min(n => n.time);
                    float segEndBeat = toRemove.Max(n => n.time);
                    //Plugin.Log.Info($"[BeatSage-StrayNoteCleaner] Removed {removed} note(s) at {segStartBeat / bps:F3}–{segEndBeat / bps:F3}");
                }
                return removed;
            }
        }

        #endregion
    }
}