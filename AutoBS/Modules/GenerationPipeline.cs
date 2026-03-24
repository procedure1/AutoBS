using AutoBS.Modules;
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

    /// <summary>
    /// Per-run pipeline context.
    /// This makes hidden dependencies more explicit and reduces reliance on stale static state.
    /// </summary>
    internal sealed class PipelineContext
    {
        public float OriginalNjs;
        public float OriginalJd;
        public float originalNjo;

        public float AutoNjsFixerNjs;
        public float AutoNjsFixerJd;
       
        // Useful if you want to inspect/debug which modules produced shared data this run.
        public bool njsAndNjoDataInitialized;
        public bool AutoNjsApplied;
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

            // Per-run shared context for values that multiple modules may depend on.
            var ctx = new PipelineContext();

            // ----- Reset per-run state FIRST -----
            // Important: disabled modules should not leave stale values from a previous run.
            ResetRunState(eData);

            RunAutoDifficultyReducer(eData);     

            // ----- Always initialize baseline movement data for the current map used by some modules like walls and rotations and vision blocking fix, etc -----
            InitializeNJSandJD(ctx);

            // ----- Run pipeline modules (mutates eData) -----
            RunAutoNjsFixer(ctx); // can be anywhere on the list. but njs and jd are needed by many modules.
            RunBeatSageCleanup(eData); // must be first since it may delete notes/walls
            RunArcitect(eData); // best before rotations since i have some code to fix arcs with rotations in rotation generator. runs ArcFix for non-gen 360/90 maps
            RunLightAutoMapper(eData);
            RunColorBoostGenerator(eData);
            RunRotationGenerator(eData); // runs ArcFix for gen 360 maps
            RunWallGenerator(eData);
            FinalNormalize(eData);


            ScoreGate.Clear();
            AutoNjsRuntimeState.ResetLive(); // these reset for live njs disabled text.
            LiveAudioRuntimeState.ResetLive();
            LiveGameplayRuntimeState.Reset();
            string disabledText = DetermineScoreSubmissionReason(eData);
            if (!string.IsNullOrEmpty(disabledText))
                ScoreGate.Set(disabledText);


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

            bool lightsNotAdded = (Utils.IsEnabledLighting() && !LightsGenerator.LightEventsAdded) || !Utils.IsEnabledLighting();
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

            Plugin.LogDebug($"[PipelineResult] ColorNotesChanged: {eData.ColorNotesChanged}, BombNotesChanged: {eData.BombNotesChanged}, ObstaclesChanged: {eData.ObstaclesChanged}, ArcsChanged: {eData.ArcsChanged}, BasicEventsChanged: {eData.BasicEventsChanged}, ColorBoostEventsChanged: {eData.ColorBoostEventsChanged}, RotationEventsChanged: {eData.RotationEventsChanged}, CustomEventsChanged: {eData.CustomEventsChanged}");

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

        /// <summary>
        /// Reset per-run state that can otherwise leak across maps or when optional modules are disabled.
        /// </summary>
        private static void ResetRunState(EditableCBD eData)
        {
            // This replaces RunState.wallCutMoments.
            // RotationGenerator can fill it (or you can fill it elsewhere).
            // IMPORTANT: must be reset per run or old values can affect gap/wall logic.
            if (eData.WallCutMoments == null)
                eData.WallCutMoments = new List<(float time, int rotationSteps)>();
            else
                eData.WallCutMoments.Clear();

            eData.RotationEventsChanged = false;// this will cause Color Notes, Bomb Notes, Arcs, and Chains to change since will add per object rotations

            eData.ColorNotesChanged = false;
            eData.BombNotesChanged = false;
            eData.ObstaclesChanged = false;
            eData.ArcsChanged = false;
            eData.ChainsChanged = false;
            eData.BasicEventsChanged = false;
            eData.ColorBoostEventsChanged = false;
            eData.CustomEventsChanged = false;

            // Reset module-level/static flags that can otherwise go stale between runs.
            // These are safe even if some module is disabled this run.
            LightsGenerator.LightEventsAdded = false;
            BeatSageCleanUp.DisableScoreSubmission = false;

            // Always initialize these to baseline-safe values later in InitializeMovementData().
            // Doing a reset here helps avoid stale data if initialization returns early.
            TransitionPatcher.FinalNoteJumpMovementSpeed = 0f;
            TransitionPatcher.FinalJumpDistance = 0f;

            TransitionPatcher.DifficultyReducerRemovedNotes = false;

            // Optional: if WallGenerator has internal static lists, this is the correct place to hard reset them.
            // Add a method like WallGenerator.ResetRunState() and call it here.
        }

        /// <summary>
        /// Always compute the movement values for the current map, even if AutoNjsFixer is disabled.
        /// Optional modules should not be responsible for required shared initialization.
        /// </summary>
        private static void InitializeNJSandJD(PipelineContext ctx)
        {
            /*
            var level = TransitionPatcher.SelectedBeatmapLevel;
            var characteristic = TransitionPatcher.SelectedCharacteristicSO;
            var difficulty = TransitionPatcher.SelectedDifficulty;

            if (level == null || characteristic == null)
            {
                Plugin.LogDebug("[InitializeMovementData] skipped because level or characteristic was null.");
                return;
            }

            var basic = level.GetDifficultyBeatmapData(characteristic, difficulty);
            if (basic == null)
            {
                Plugin.LogDebug("[InitializeMovementData] skipped because difficulty beatmap data was null.");
                return;
            }

            float originalNjs = SetContent.GetNoteJumpMovementSpeed(difficulty, basic.noteJumpMovementSpeed);
            float njo = basic.noteJumpStartBeatOffset;
            */

            float originalNjs = TransitionPatcher.OriginalNoteJumpMovementSpeed;
            if (originalNjs == 0f) return; // this means we never got valid data for the current map, so skip initialization to avoid writing stale data.

            float originalNjo = TransitionPatcher.OriginalNoteJumpOffset;

            // Reuse your existing calculation path so original JD matches your current logic.
            (float finalNjs, float finalJd, float originalJd) = AutoNjsFixer.Calculate(originalNjs, originalNjo, TransitionPatcher.bpm);

            ctx.OriginalNjs = originalNjs;
            ctx.OriginalJd = originalJd;
            ctx.originalNjo = originalNjo;

            ctx.AutoNjsFixerNjs = finalNjs; // set even though may not be used if auto njs fixer is disabled and thus not applied to TransitionPatcher.FinalNoteJumpMovementSpeed
            ctx.AutoNjsFixerJd = finalJd;   // set even though may not be used if auto njs fixer is disabled and thus not applied to TransitionPatcher.FinalJumpDistance
            
            ctx.njsAndNjoDataInitialized = true;

            // This is starting as though AutoNJS is disabled.
            TransitionPatcher.OriginalNoteJumpMovementSpeed = originalNjs;
            TransitionPatcher.FinalNoteJumpMovementSpeed = originalNjs; // <- orginal still applied as default.
            TransitionPatcher.FinalJumpDistance = originalJd;           // <- orginal still applied as default.

            //Plugin.LogDebug($"[InitializeMovementData] Baseline NJS init -> Original NJS:{originalNjs} NJO:{originalNjo}, Original JD:{originalJd} (Final defaults to original when AutoNjsFixer disabled)");
        }
        private static void RunAutoDifficultyReducer(EditableCBD eData)
        {
            if (!Config.Instance.EnableDiffReducer) return;
            
            bool eligibleHighNpsSong = TransitionPatcher.NotesPerSecond > Config.Instance.PreferredFinalNps;
            bool enableDiffRed = Config.Instance.EnableForAllSongs || eligibleHighNpsSong;

            if (!enableDiffRed) return;

            int originalCount = eData.ColorNotes.Count;
            Plugin.LogDebug($"[Pipeline DiffReducer] Inital Note Count: {originalCount}");

            DifficultyReducer.InitialCalculations(eData, TransitionPatcher.bpm, TransitionPatcher.NotesPerSecond);

            eData.ColorNotes = DifficultyReducer.SimplifyBeatmap(eData);

            DifficultyReducer.ApplyArcAndChainEndpointChanges(eData, eData.ColorNotes);

            eData.ColorNotes = eData.ColorNotes.OrderBy(n => n.time).ToList();
            if (eData.Arcs != null)
                eData.Arcs = eData.Arcs.OrderBy(a => a.time).ToList();

            if (eData.Chains != null)
                eData.Chains = eData.Chains.OrderBy(c => c.time).ToList();

            int finalCount = eData.ColorNotes.Count;
            Plugin.LogDebug($"[Pipeline DiffReducer] Inital Note Count: {originalCount} -- Final Note Count: {eData.ColorNotes.Count}");// ColorA: {eData.ColorNotes.Where(r => r.colorType == ColorType.ColorA).Count()} ColorB: {eData.ColorNotes.Where(r => r.colorType == ColorType.ColorB).Count()}");
            if (originalCount != finalCount)
            {
                eData.ColorNotesChanged = true;
                TransitionPatcher.DifficultyReducerRemovedNotes = true;
            }

        }
        private static void RunAutoNjsFixer(PipelineContext ctx)
        {
            if (!ctx.njsAndNjoDataInitialized)
            {
                Plugin.LogDebug("[RunAutoNjsFixer] skipped because movement data was not initialized.");
                return;
            }

            if (!Utils.IsEnabledAutoNjsFixer()) return;

            ctx.AutoNjsApplied = true;

            TransitionPatcher.FinalNoteJumpMovementSpeed = ctx.AutoNjsFixerNjs;
            TransitionPatcher.FinalJumpDistance = ctx.AutoNjsFixerJd;

            Plugin.LogDebug($"[AutoNjs] Original NJS:{ctx.OriginalNjs} NJO:{ctx.originalNjo}, Original JD:{ctx.OriginalJd} -> AutoNjsFixer NJS:{ctx.AutoNjsFixerNjs}, AutoNjsFixer JD:{ctx.AutoNjsFixerJd}");
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

            LightsGenerator.Start(eData);
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
        }

        private static void RunWallGenerator(EditableCBD eData)
        {
            if (!Utils.IsEnabledWalls()) return;
            if (eData.Obstacles != null && eData.Obstacles.Count >= 1000) return;

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
                bool removeCrouchWallsBlockingChains = false;
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
                {
                    moveWallsBlockingChainTail = WallGenerator.MoveWallsBlockingChainTail(eData);
                    removeCrouchWallsBlockingChains = WallGenerator.RemoveCrouchWallsBlockingChains(eData);
                }
                else
                {
                    Plugin.LogDebug($"[MoveWallsBlockingChainTail] & [RemoveCrouchWallsBlockingChains] NOT CALLED!!");
                }

                if (Utils.IsEnabledArcs())// && !BeatmapDataTransformHelperPatcher.NoodleProblemObstacles)// && Config.Instance.EnableWallGenerator && (Config.Instance.EnableStandardWalls || Config.Instance.EnableBigWalls))
                    moveWallsBlockingArc = WallGenerator.MoveWallsBlockingArc(eData);
                else
                {
                    Plugin.LogDebug($"[MoveWallsBlockingArc] NOT CALLED!!");
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

        public static string DetermineScoreSubmissionReason(EditableCBD eData)
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
                    str += (str != "" ? " | " : "") + "Rotation Mult Low";
                }
                if (!Config.Instance.Wireless360 && Config.Instance.LimitRotations360 < 90)
                {
                    str += (str != "" ? " | " : "") + "Rotations Limited";
                }
            }
            if (BS_Utils.Plugin.LevelData.Mode == BS_Utils.Gameplay.Mode.Standard &&
                Config.Instance.EnableDiffReducer && TransitionPatcher.DifficultyReducerRemovedNotes)
            {
                str += (str != "" ? " | " : "") + "Auto Diff Reducer";
            }
            if (BS_Utils.Plugin.LevelData.Mode == BS_Utils.Gameplay.Mode.Standard &&
                Utils.IsEnabledAutoNjsFixer() &&
                !TransitionPatcher.AutoNJSDisabledByConflictingMod &&
                TransitionPatcher.OriginalNoteJumpMovementSpeed > TransitionPatcher.FinalNoteJumpMovementSpeed)
            {
                str += (str != "" ? " | " : "") + "Auto NJS Fixer";
            }

            if (Utils.IsEnabledChains() && !eData.MapAlreadyUsesChains && eData.Chains.Count > 0)
            {
                str += (str != "" ? " | " : "") + "Architect Chains";
            }

            if (Config.Instance.EnableCleanBeatSage && (TransitionPatcher.IsBeatSageMap) && BeatSageCleanUp.DisableScoreSubmission)
            {
                str += (str != "" ? " | " : "") + "Beat Sage Cleaner";
            }

            if (str != "")
                str = "AutoBS—" + str; // prefix once

            return str;
        }
    }
}
