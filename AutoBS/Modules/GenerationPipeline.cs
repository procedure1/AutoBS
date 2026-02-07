using AutoBS.Patches;
using CustomJSONData.CustomBeatmap;
using System;
using System.Collections.Generic;
using System.Drawing.Text;
using System.Linq;

namespace AutoBS
{
    internal struct PipelineResult
    {
        public bool OriginalMapAltered;
        public bool IsCustom;
        public CustomBeatmapData Custom;
        public BeatmapData Vanilla;
    }

    internal static class GenerationPipeline
    {
        internal static PipelineResult Run(EditableCBD eData)
        {
            // ----- Capture "original" state for the altered-check (copied from Generator.cs) -----
            var originalRotations = eData.RotationEvents
                .OrderBy(r => r.time)
                .Select(r => (t: MathF.Round(r.time, 4), rot: r.rotation))
                .ToList();

            int originalWallCount = eData.Obstacles.Count;

            // This replaces RunState.wallCutMoments.
            // RotationGenerator can fill it (or you can fill it elsewhere).
            var wallCutMoments = new List<(float time, int rotationSteps)>();

            eData.RotationEventsChanged = false;// this will cause Color Notes, Bomb Notes, Arcs, and Chains to change since will add per object rotations
            
            eData.ColorNotesChanged = false;
            eData.BombNotesChanged = false;
            eData.ObstaclesChanged = false;
            eData.ArcsChanged = false;
            eData.ChainsChanged = false;
            eData.BasicEventsChanged = false; 
            eData.ColorBoostEventsChanged = false;

            // ----- Run pipeline modules (mutates eData) -----
            RunAutoNjsFixer(); // this one can be anywhere on the list
            RunBeatSageCleanup(eData); // must be first since it may delete notes/walls
            RunArcitect(eData); // best before rotations since i have some code to fix arcs with rotations in rotation generator. runs ArcFix for non-gen 360/90 maps
            RunLightAutoMapper(eData);
            RunColorBoostGenerator(eData);
            RunRotationGenerator(eData); // runs ArcFix for gen 360 maps
            RunWallGenerator(eData);
            FinalNormalize(eData);

            (float accumRot, float time) high = (0, 0);
            (float accumRot, float time) low = (0, 0);
            int endRot = 0;
            foreach (var rot in eData.RotationEvents)
            {
                endRot = rot.accumRotation;
                if (rot.accumRotation < low.accumRot)
                    low = (rot.accumRotation, rot.time);
                else if (rot.accumRotation > high.accumRot)
                    high = (rot.accumRotation, rot.time);
                //if (rot.time < 20)
                //    Plugin.Log.Info($"2 Rotation - Time: {rot.time} - Rotation: {rot.rotation} - Total Rotation: {rot.accumRotation}");
            }
            Plugin.Log.Info($"[PipelineResult] Rotation Count: {eData.RotationEvents.Count}, Largest '-' Rot: {low.accumRot} (time: {low.time:F}), Largest '+' Rot: {high.accumRot} (time: {high.time:F}), Final Rotation: {endRot}");


            bool beatSageMapNotAltered = (TransitionPatcher.IsBeatSageMap && !BeatSageCleanUp.DisableScoreSubmission) || !TransitionPatcher.IsBeatSageMap;
            Plugin.LogDebug($"[PipelineResult] 1 beatSageMapNotAltered: {beatSageMapNotAltered}.");

            bool arcsEnabled = Utils.IsEnabledArcs();
            bool arcsNotAdded = (arcsEnabled && eData.MapAlreadyUsesArcs) || !arcsEnabled;
            Plugin.LogDebug($"[PipelineResult] 2 arcsNotAdded: {arcsNotAdded}.");

            bool chainsEnabled = Utils.IsEnabledChains();
            bool chainsNotAdded = (chainsEnabled && eData.MapAlreadyUsesChains) || !chainsEnabled;
            Plugin.LogDebug($"[PipelineResult] 3 chainsNotAdded: {chainsNotAdded}.");

            var rotAfter = eData.RotationEvents
                .OrderBy(r => r.time)
                .Select(r => (t: MathF.Round(r.time, 4), rot: r.rotation))
                .ToList();
            bool rotationsNotchanged = originalRotations.Count == rotAfter.Count && originalRotations.SequenceEqual(rotAfter); //compares starting rotations to final rotations
            Plugin.LogDebug($"[PipelineResult] 4 rotationsNotchanged: {rotationsNotchanged} Rotation Events Count: {eData.RotationEvents.Count()}.");
            if (!rotationsNotchanged) eData.RotationEventsChanged = true;

            bool lightsNotAdded = (Utils.IsEnabledLighting() && !LightAutoMapper.LightEventsAdded) || !Utils.IsEnabledLighting();
            Plugin.LogDebug($"[PipelineResult] 5 lightsNotAdded: {lightsNotAdded}.");

            bool boostNotAdded = (Config.Instance.BoostLighting && eData.ColorBoostEvents.Count == 0) || eData.MapAlreadyUsesEnvColorBoost;
            Plugin.LogDebug($"[PipelineResult] 6 boostNotAdded: {boostNotAdded} (boost events: {eData.ColorBoostEvents.Count} MapAlreadyUsesEnvColorBoost: {eData.MapAlreadyUsesEnvColorBoost})");

            bool wallsNotAltered = ((Utils.IsEnabledWalls() && originalWallCount == eData.Obstacles.Count) || !Utils.IsEnabledWalls()) && !eData.ObstaclesChanged;
            Plugin.LogDebug($"[PipelineResult] 7 wallsNotAltered: {wallsNotAltered} (Original Count: {originalWallCount} Final Count: {eData.Obstacles.Count} -- walls may have changed start time or duration or lineLayer if beatsage cleaner used)");

            if (eData.IsNative360or90 && eData.RotationEventsChanged) // if rotations are altered, then original data with its per object rotations will change! (basic event data is turned into per object rotation)  
            {
                if (eData.ColorNotes.Count > 0) eData.ColorNotesChanged = true;
                if (eData.BombNotes.Count > 0)  eData.BombNotesChanged = true;
                if (eData.Obstacles.Count > 0)  eData.ObstaclesChanged = true;
            }

            Plugin.LogDebug($"[PipelineResult] ColorNotesChanged: {eData.ColorNotesChanged}, BombNotesChanged: {eData.BombNotesChanged}, ObstaclesChanged: {eData.ObstaclesChanged}, ArcsChanged: {eData.ArcsChanged}, BasicEventsChanged: {eData.ArcsChanged}, ColorBoostEventsChanged: {eData.ColorBoostEventsChanged}, RotationEventsChanged: {eData.RotationEventsChanged}, CustomEventsChanged: {eData.CustomEventsChanged}");

            if (eData.RotationEventsChanged) // this adds per object rotation to all objects so they are altered
            {
                if (eData.ColorNotes.Count > 0)  eData.ColorNotesChanged = true;
                if (eData.BombNotes.Count > 0)   eData.BombNotesChanged = true;
                if (eData.Obstacles.Count > 0)   eData.ObstaclesChanged = true;
                if (eData.Arcs.Count > 0)        eData.ArcsChanged = true;
                if (eData.Chains.Count > 0)      eData.ChainsChanged = true;
                if (eData.BasicEvents.Count > 0) eData.BasicEventsChanged = true; // added this since v2/v3 maps may have rotation events tied to basic events which are removed. but not sure if this is needed really
            }

            //if (beatSageMapNotAltered && arcsNotAdded && chainsNotAdded && rotationsNotchanged && lightsNotAdded && boostNotAdded && wallsNotAdded)
            if (!eData.ColorNotesChanged && !eData.BombNotesChanged && !eData.ObstaclesChanged && !eData.ArcsChanged && !eData.ChainsChanged && !eData.BasicEventsChanged && !eData.ColorBoostEventsChanged && !eData.RotationEventsChanged && !eData.CustomEventsChanged)
            {
                Plugin.LogDebug("[PipelineResult] Original map NOT altered! Pass original customBeatmapData or beatmapData!");
                return new PipelineResult
                {
                    OriginalMapAltered = false,
                    IsCustom = (eData.OriginalCBData != null),
                    Custom   = eData.OriginalCBData,
                    Vanilla  = eData.OriginalBData
                };
            }

            Plugin.LogDebug("[PipelineResult] Original map altered! New altered map will be used!");

            /*
            eData.BasicEvents.OrderBy(r => r.time).ToList();
            foreach (var light in eData.BasicEvents)
            {
                if (light.time > 59 && light.time < 80)
                    Plugin.LogDebug($"Light: {light.time:F3} type: {(EventType)light.basicBeatmapEventType} value: {(EventValue)light.value} floatValue: {light.floatValue}");
            }
            */
            ConvertEditableCBD.ApplyPerObjectRotations(eData);
            ConvertEditableCBD.ApplyWallVisionBlockingFix(eData); // will alter eData by reference

            return new PipelineResult
            {
                OriginalMapAltered = true,
                IsCustom = (eData.OriginalCBData != null),
                Custom   = (eData.OriginalCBData != null) ? ConvertEditableCBD.Convert(eData) : null,
                Vanilla  = (eData.OriginalCBData == null) ? ConvertEditableCBD.ConvertVanilla(eData) : null
            };
        }

        private static void RunAutoNjsFixer()
        {
            var level = TransitionPatcher.SelectedBeatmapLevel;
            var characteristic = TransitionPatcher.SelectedCharacteristicSO;
            var difficulty = TransitionPatcher.SelectedDifficulty;

            if (level == null || characteristic == null)
                return;

            var basic = level.GetDifficultyBeatmapData(characteristic, difficulty);
            if (basic == null)
                return;

            float originalNjs = SetContent.NoteJumpMovementSpeed(difficulty, basic.noteJumpMovementSpeed);
            float njo = basic.noteJumpStartBeatOffset;

            (float fixedNjs, float fixedJd, float originalJd) = AutoNjsFixer.Calculate(originalNjs, njo, TransitionPatcher.bpm);

            TransitionPatcher.OriginalNoteJumpMovementSpeed = originalNjs;
            TransitionPatcher.FinalNoteJumpMovementSpeed = fixedNjs;
            TransitionPatcher.FinalJumpDistance = fixedJd;

            Plugin.LogDebug(
                $"[AutoNjs] Original NJS:{originalNjs} NJO:{njo}, Original JD:{originalJd} -> AutoNjsFixer NJS:{fixedNjs}, AutoNjsFixer JD:{fixedJd}"
            );
        }

        private static void RunBeatSageCleanup(EditableCBD eData)
        {
            if (!Config.Instance.EnableCleanBeatSage) return;
            if (!TransitionPatcher.IsBeatSageMap) return;

            BeatSageCleanUp.Clean(eData);

            eData.ColorNotes = eData.ColorNotes.OrderBy(n => n.time).ToList();
            eData.BombNotes = eData.BombNotes.OrderBy(n => n.time).ToList();
            eData.Obstacles = eData.Obstacles.OrderBy(o => o.time).ToList();
        }

        private static void RunArcitect(EditableCBD eData)
        {
            bool addArcs = Utils.IsEnabledArcs() && !eData.MapAlreadyUsesArcs;
            bool addChains = Utils.IsEnabledChains() && !eData.MapAlreadyUsesChains;

            if (!addArcs && !addChains) return;

            Arcitect.CreateSliders(eData);

            string disabledText = BeatmapDataTransformHelperPatcher.DetermineScoreSubmissionReason(
                BeatSageCleanUp.DisableScoreSubmission,
                eData.MapAlreadyUsesChains,
                eData.Chains.Count
            );

            if (!string.IsNullOrEmpty(disabledText))
                ScoreGate.Set(disabledText);

            if (addArcs &&
                Config.Instance.ArcFixFull &&
                (TransitionPatcher.SelectedSerializedName == "360Degree" ||
                 TransitionPatcher.SelectedSerializedName == "90Degree"))
            {
                var allRotations = eData.RotationEvents;
                eData.RotationEvents = Arcitect.ArcFix(allRotations, eData);
            }

            bool moveWallsBlockingArc = false;
            bool moveWallsBlockingChainTail = false;

            if (addArcs && eData.ArcsChanged)
            {
                if (!Utils.IsEnabledWalls())
                    moveWallsBlockingArc = WallGenerator.MoveWallsBlockingArc(eData);
            }

            if (addChains && eData.ChainsChanged)
            {
                if (!Utils.IsEnabledWalls())
                    moveWallsBlockingChainTail = WallGenerator.MoveWallsBlockingChainTail(eData);
            }

            if (moveWallsBlockingArc || moveWallsBlockingChainTail)
                eData.ObstaclesChanged = true;
        }

        private static void RunLightAutoMapper(EditableCBD eData)
        {
            if (!Utils.IsEnabledLighting()) return;
            if (!Config.Instance.EnableLightAutoMapper) return;

            LightAutoMapper.Start(eData);
        }

        private static void RunColorBoostGenerator(EditableCBD eData)
        {
            if (!Utils.IsEnabledLighting()) return;
            if (!Config.Instance.BoostLighting) return;

            ColorBoostGenerator.Generate(eData);
        }

        private static void RunRotationGenerator(EditableCBD eData)
        {
            if (TransitionPatcher.SelectedSerializedName != GameModeHelper.GENERATED_360DEGREE_MODE) return;

            RotationGenerator.Generate(eData);

            // you mentioned rotation gen can remove bomb notes
            eData.BombNotes = eData.BombNotes.OrderBy(n => n.time).ToList();
        }

        private static void RunWallGenerator(EditableCBD eData)
        {
            if (!Utils.IsEnabledWalls()) return;
            if (eData.Obstacles != null && eData.Obstacles.Count >= 5000) return;

            Config cfg = Config.Instance;

            if ( !cfg.AllowCrouchWalls &&
                 !cfg.AllowLeanWalls &&
                 !cfg.EnableStandardWalls &&
                 !cfg.EnableBigWalls &&
                 !cfg.EnableDistantExtensionWalls &&
                 !cfg.EnableColumnWalls &&
                 !cfg.EnableRowWalls &&
                 !cfg.EnableTunnelWalls &&
                 !cfg.EnableGridWalls &&
                 !cfg.EnableWindowPaneWalls &&
                 !cfg.EnableParticleWalls &&
                 !cfg.EnableFloorWalls)
                return;

            WallGenerator.SetOriginalWalls(eData);
            WallGenerator.ResetWalls(eData);

            RunWallLoop(eData);

            FinalizeWallGeneration(eData);

            void RunWallLoop(EditableCBD eData)
            {
                float bpm = TransitionPatcher.bpm;
                float beatDuration = 60f / bpm;

                // Same barLength alignment as Generator.cs
                float barLength = beatDuration;
                float preferred = 2.75f; // Generator.PreferredBarDuration default
                float rotSpeed = (float)Math.Round(Config.Instance.RotationSpeedMultiplier, 1);
                if (rotSpeed <= 0f) rotSpeed = 1f;

                while (barLength >= preferred * 1.25f / rotSpeed) barLength /= 2f;
                while (barLength < preferred * 0.75f / rotSpeed) barLength *= 2f;

                // Build notes+bombs list like Generator.cs
                List<ENoteData> notesAndBombs = new List<ENoteData>(eData.ColorNotes.Count + eData.BombNotes.Count);
                notesAndBombs.AddRange(eData.ColorNotes);
                notesAndBombs.AddRange(eData.BombNotes);
                notesAndBombs.Sort((a, b) => a.time.CompareTo(b.time));

                if (notesAndBombs.Count == 0)
                    return;

                float firstBeatmapNoteTime = notesAndBombs[0].time;

                // Split by side (color independent), same as Generator.cs
                List<ENoteData> notesBySideLeft = new List<ENoteData>();
                List<ENoteData> notesBySideRight = new List<ENoteData>();
                for (int n = 0; n < notesAndBombs.Count; n++)
                {
                    ENoteData nd = notesAndBombs[n];
                    if (nd.line <= 1) notesBySideLeft.Add(nd);
                    else notesBySideRight.Add(nd);
                }

                int leftCur = 0;
                int rightCur = 0;

                List<ENoteData> notesInBar = new List<ENoteData>();
                List<ENoteData> notesInBarBeat = new List<ENoteData>();

                for (int i = 0; i < notesAndBombs.Count;)
                {
                    float currentBarStart =
                        Floor((notesAndBombs[i].time - firstBeatmapNoteTime) / barLength) * barLength;

                    float currentBarEnd = currentBarStart + barLength - 0.001f;

                    notesInBar.Clear();
                    for (; i < notesAndBombs.Count && notesAndBombs[i].time - firstBeatmapNoteTime < currentBarEnd; i++)
                    {
                        notesInBar.Add(notesAndBombs[i]);
                    }

                    if (notesInBar.Count == 0)
                        continue;

                    int barDivider;
                    if (notesInBar.Count >= 58) barDivider = 0;
                    else if (notesInBar.Count >= 38) barDivider = 1;
                    else if (notesInBar.Count >= 26) barDivider = 2;
                    else if (notesInBar.Count >= 8) barDivider = 4;
                    else barDivider = 8;

                    if (barDivider <= 0)
                        continue;

                    float dividedBarLength = barLength / barDivider;

                    for (int j = 0, k = 0; j < barDivider && k < notesInBar.Count; j++)
                    {
                        notesInBarBeat.Clear();

                        for (; k < notesInBar.Count &&
                               Floor((notesInBar[k].time - firstBeatmapNoteTime - currentBarStart) / dividedBarLength) == j;
                             k++)
                        {
                            notesInBarBeat.Add(notesInBar[k]);
                        }

                        if (notesInBarBeat.Count == 0)
                            continue;

                        float currentBarBeatStart = firstBeatmapNoteTime + currentBarStart + j * dividedBarLength;

                        // Same "afterLastNote" lookahead you used for wall safety
                        ENoteData afterLastNote =
                            (k < notesInBar.Count) ? notesInBar[k] :
                            (i < notesAndBombs.Count) ? notesAndBombs[i] :
                            null;

                        // guard note times (left/right) for wall clipping
                        float barEndAbs = firstBeatmapNoteTime + currentBarEnd;

                        while (leftCur < notesBySideLeft.Count && notesBySideLeft[leftCur].time < barEndAbs)
                            leftCur++;

                        while (rightCur < notesBySideRight.Count && notesBySideRight[rightCur].time < barEndAbs)
                            rightCur++;

                        float nextNoteLeftTime = (leftCur < notesBySideLeft.Count) ? notesBySideLeft[leftCur].time : -1f;
                        float nextNoteRightTime = (rightCur < notesBySideRight.Count) ? notesBySideRight[rightCur].time : -1f;

                        WallGenerator.WallGen(
                            i,
                            currentBarBeatStart,
                            dividedBarLength,
                            afterLastNote,
                            notesInBarBeat,
                            notesInBar,
                            nextNoteLeftTime,
                            nextNoteRightTime
                        );
                    }
                }
                //if (WallGenerator._allWalls.Count > 0) // Github Issue #2 Walls gone when using autolights
                //    WallGenerator.FinalizeWallsToMap(eData);
            }

            void FinalizeWallGeneration(EditableCBD eData)
            {
                eData.Obstacles = eData.Obstacles.OrderBy(o => o.time).ToList();

                List<TimeGap> gaps = new List<TimeGap>();

                if (eData.WallCutMoments.Count > 0)
                {
                    gaps = WallGenerator.FindGapsUsingRotations(eData.WallCutMoments, 2.0f);
                    Plugin.LogDebug($"[FinalizeWallGeneration][FindGapsUsingRotations] using WallCutMoments found {gaps.Count} gaps to help set floor walls and mega walls.");
                }
                else
                {
                    gaps = WallGenerator.FindGapsUsingNotes(eData.ColorNotes, 2.0f);
                    Plugin.LogDebug($"[FinalizeWallGeneration][FindGapsUsingNotes] No WallCutMoments! With notes found {gaps.Count} gaps to help set floor walls and mega walls.");
                }

                WallGenerator.ParticleWalls(); WallGenerator.FloorWalls(gaps);//outside the loop. so not using wallTime and not on the beat
                
                
                //v1.42 added mega walls outside of the extension walls since mega walls do not require ME (but height is severely restricted to about 8 or 10)
                WallGenerator.MegaWalls(gaps);

                WallGenerator.LogWallCount(eData);

                //Plugin.Log.Info($" TransitionPatcher.startingGameMode: {TransitionPatcher.startingGameMode}, GameModeHelper.GENERATED_360DEGREE_MODE: {GameModeHelper.GENERATED_360DEGREE_MODE} AllowLeanWalls: {Config.Instance.AllowLeanWalls} AllowCrouchWalls: {Config.Instance.AllowCrouchWalls}");

                bool leanCrouchWallRemoval = false;
                bool moveWallsBlockingChainTail = false;
                bool moveWallsBlockingArc = false;

                if (TransitionPatcher.SelectedSerializedName == GameModeHelper.GENERATED_360DEGREE_MODE || // will remove 1 width lean walls from all gen maps even if user allows lean walls!!!!!!!!!!!!!!!
                    !Config.Instance.AllowLeanWalls || !Config.Instance.AllowCrouchWalls)
                {
                    leanCrouchWallRemoval = WallGenerator.LeanCrouchWallRemoval();
                }
                else
                {
                    Plugin.LogDebug($"[LeanCrouchWallRemoval] NOT CALLED!!");
                }

                if (eData.RotationEvents.Count > 0 && !eData.IsNative360or90) // don't change the rotations of native 360/90 maps
                    eData.RotationEvents = WallGenerator.RemoveCrouchWallRotations(eData);

                if (Utils.IsEnabledChains())// && Config.Instance.EnableWallGenerator && (Config.Instance.EnableStandardWalls || Config.Instance.EnableBigWalls))
                    moveWallsBlockingChainTail = WallGenerator.MoveWallsBlockingChainTail(eData);
                else
                {
                    Plugin.LogDebug(
                        $"[MoveWallsBlockingChainTail] NOT CALLED!!");
                }

                if (Utils.IsEnabledArcs())// && !BeatmapDataTransformHelperPatcher.NoodleProblemObstacles)// && Config.Instance.EnableWallGenerator && (Config.Instance.EnableStandardWalls || Config.Instance.EnableBigWalls))
                    moveWallsBlockingArc = WallGenerator.MoveWallsBlockingArc(eData);
                else
                {
                    Plugin.LogDebug(
                        $"[MoveWallsBlockingArc] NOT CALLED!!");
                }

                //Plugin.LogDebug($" ------- MoveWallsBlockingChainTail() & MoveWallsBlockingArc() time elapsed: {stopwatch.ElapsedMilliseconds / 1000.0:F1}");

                bool alreadyUsingME = TransitionPatcher.RequiresMappingExtensions;

                if (Utils.IsEnabledWalls() && !alreadyUsingME)
                    WallGenerator.RemoveIntersectingWalls();

                if (WallGenerator.allWalls.Count > 0) // Github Issue #2 Walls gone when using autolights
                    WallGenerator.FinalizeWallsToMap(eData);

                //if (WallGenerator._originalWalls.Count > 0 || WallGenerator._allWalls.Count > 0) // Github Issue #2 Walls gone when using autolights
                //    WallGenerator.FinalizeOriginalOnlyWallsToMap(eData);

                eData.Obstacles = eData.Obstacles.OrderBy(o => o.time).ToList();
                

                if (leanCrouchWallRemoval || moveWallsBlockingArc || moveWallsBlockingChainTail || eData.OriginalObstacleCount != eData.Obstacles.Count)
                    eData.ObstaclesChanged = true;
            }

            int Floor(float f)
            {
                int i = (int)f;
                return (f - i >= 0.999f) ? i + 1 : i;
            }
        }

        private static void FinalNormalize(EditableCBD eData)
        {
            eData.ColorNotes = eData.ColorNotes.OrderBy(n => n.time).ToList();
            eData.BombNotes = eData.BombNotes.OrderBy(n => n.time).ToList();
            eData.Obstacles = eData.Obstacles.OrderBy(o => o.time).ToList();

            eData.RotationEvents = ERotationEventData.RecalculateAccumulatedRotations(eData.RotationEvents);

            eData.ColorBoostEvents = eData.ColorBoostEvents.OrderBy(b => b.time).ToList();
            eData.BasicEvents = eData.BasicEvents.OrderBy(e => e.time).ToList();
            eData.Arcs = eData.Arcs.OrderBy(a => a.time).ToList();
            eData.Chains = eData.Chains.OrderBy(c => c.time).ToList();
        }
    }
}
