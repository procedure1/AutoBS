using AutoBS.Modules;
using AutoBS.Patches;
using CustomJSONData.CustomBeatmap;
using System;
using System.Collections.Generic;
using System.Drawing.Text;
using System.Linq;
using UnityEngine;

namespace AutoBS
{
    internal struct PipelineResult
    {
        public bool OriginalMapAltered;
        public bool IsCustom;
        public CustomBeatmapData Custom;
        public BeatmapData Vanilla;
        public string PipelineScoreDisabledReason;
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

            AutoNjsRuntimeState.ResetLive(); // these reset for live njs disabled text.
            LiveAudioRuntimeState.ResetLive();
            LiveGameplayRuntimeState.Reset();
            string PipelineScoreDisabledReason = ScoreSubmission.DetermineScoreSubmissionDisabledReason(eData);

            //int offset = (int)Config.Instance.RotationOriginOffsetForRecording360;
            //foreach (var evt in eData.RotationEvents)
            //{
            //    evt.accumRotation += offset;
            //}

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


            bool beatSageMapAltered = TransitionPatcher.IsBeatSageMap &&
                                      (BeatSageCleanUp.DisableScoreSubmission ||
                                       eData.ObstaclesChangedByBeatSageCleanUp);
            Plugin.LogDebug($"[PipelineResult] 1 beatSageMapAltered: {beatSageMapAltered}.");

            bool arcsEnabled = Utils.IsEnabledArcs();
            bool arcsAdded = arcsEnabled && eData.ArcsChangedByArcitect;
            Plugin.LogDebug($"[PipelineResult] 2 arcsAdded: {arcsAdded}.");

            bool chainsEnabled = Utils.IsEnabledChains();
            bool chainsAdded = chainsEnabled && eData.ChainsChangedByArcitect;
            Plugin.LogDebug($"[PipelineResult] 3 chainsAdded: {chainsAdded}.");

            bool autoDifficultyReducerNotesChanged = Config.Instance.EnableDiffReducer && TransitionPatcher.DifficultyReducerRemovedNotes;
            bool autoDifficultyReducerArcsChanged = Config.Instance.EnableDiffReducer && eData.ArcsChangedByDifficultyReducer;
            bool autoDifficultyReducerChainsChanged = Config.Instance.EnableDiffReducer && eData.ChainsChangedByDifficultyReducer;
            bool autoDifficultyReducerAltered =
                autoDifficultyReducerNotesChanged ||
                autoDifficultyReducerArcsChanged ||
                autoDifficultyReducerChainsChanged;
            Plugin.LogDebug(
                $"[PipelineResult] 4 autoDifficultyReducerAltered: {autoDifficultyReducerAltered} " +
                $"(notesChanged: {autoDifficultyReducerNotesChanged}, arcsChanged: {autoDifficultyReducerArcsChanged}, chainsChanged: {autoDifficultyReducerChainsChanged}).");

            var rotAfter = eData.RotationEvents
                .OrderBy(r => r.time)
                .Select(r => (t: MathF.Round(r.time, 4), rot: r.rotation))
                .ToList();
            bool rotationsChanged = originalRotations.Count != rotAfter.Count || !originalRotations.SequenceEqual(rotAfter); // compares starting rotations to final rotations
            Plugin.LogDebug($"[PipelineResult] 5 rotationsChanged: {rotationsChanged} Rotation Events Count: {eData.RotationEvents.Count()}.");
            if (rotationsChanged) eData.RotationEventsChanged = true;

            bool lightsAdded = Utils.IsEnabledLighting() && LightsGenerator.LightEventsAdded;
            Plugin.LogDebug($"[PipelineResult] 6 lightsAdded: {lightsAdded}.");

            bool boostAdded = Config.Instance.BoostLighting &&
                              eData.ColorBoostEvents.Count > 0 &&
                              !eData.MapAlreadyUsesEnvColorBoost;
            Plugin.LogDebug($"[PipelineResult] 7 boostAdded: {boostAdded} (boost events: {eData.ColorBoostEvents.Count} MapAlreadyUsesEnvColorBoost: {eData.MapAlreadyUsesEnvColorBoost})");

            bool autoWallsAltered = Utils.IsEnabledWalls() && eData.ObstaclesChangedByWallGenerator;
            Plugin.LogDebug($"[PipelineResult] 8 autoWallsAltered: {autoWallsAltered} (Original Count: {originalWallCount} Final Count: {eData.Obstacles.Count}, beatSageWallsChanged: {eData.ObstaclesChangedByBeatSageCleanUp}, arcitectWallsChanged: {eData.ObstaclesChangedByArcitect})");
            if (eData.IsNative360or90 && eData.RotationEventsChanged) // if rotations are altered, then original data with its per object rotations will change! (basic event data is turned into per object rotation)  
            {
                if (eData.ColorNotes.Count > 0) eData.ColorNotesChanged = true;
                if (eData.BombNotes.Count > 0)  eData.BombNotesChanged = true;
                if (eData.Obstacles.Count > 0)  eData.ObstaclesChanged = true;
            }

            Plugin.LogDebug($"[PipelineResult] ColorNotesChanged: {eData.ColorNotesChanged}, BombNotesChanged: {eData.BombNotesChanged}, ObstaclesChanged: {eData.ObstaclesChanged}, ArcsChanged: {eData.ArcsChanged}, ChainsChanged: {eData.ChainsChanged}, BasicEventsChanged: {eData.BasicEventsChanged}, ColorBoostEventsChanged: {eData.ColorBoostEventsChanged}, RotationEventsChanged: {eData.RotationEventsChanged}, CustomEventsChanged: {eData.CustomEventsChanged}");
            Plugin.LogDebug($"[PipelineResult] Change sources: ObstaclesByWallGenerator: {eData.ObstaclesChangedByWallGenerator}, ObstaclesByBeatSageCleanUp: {eData.ObstaclesChangedByBeatSageCleanUp}, ObstaclesByArcitect: {eData.ObstaclesChangedByArcitect}, ArcsByArcitect: {eData.ArcsChangedByArcitect}, ChainsByArcitect: {eData.ChainsChangedByArcitect}, ArcsByDifficultyReducer: {eData.ArcsChangedByDifficultyReducer}, ChainsByDifficultyReducer: {eData.ChainsChangedByDifficultyReducer}");

            if (eData.RotationEventsChanged) // this adds per object rotation to all objects so they are altered
            {
                if (eData.ColorNotes.Count > 0)  eData.ColorNotesChanged = true;
                if (eData.BombNotes.Count > 0)   eData.BombNotesChanged = true;
                if (eData.Obstacles.Count > 0)   eData.ObstaclesChanged = true;
                if (eData.Arcs.Count > 0)        eData.ArcsChanged = true;
                if (eData.Chains.Count > 0)      eData.ChainsChanged = true;
                if (eData.BasicEvents.Count > 0) eData.BasicEventsChanged = true; // added this since v2/v3 maps may have rotation events tied to basic events which are removed. but not sure if this is needed really
            }

            
            if (!eData.ColorNotesChanged && !eData.BombNotesChanged && !eData.ObstaclesChanged && !eData.ArcsChanged && !eData.ChainsChanged && !eData.BasicEventsChanged && !eData.ColorBoostEventsChanged && !eData.RotationEventsChanged && !eData.CustomEventsChanged)
            {
                Plugin.LogDebug("[PipelineResult] Original map NOT altered! Pass original customBeatmapData or beatmapData!");
                return new PipelineResult
                {
                    OriginalMapAltered = false,
                    IsCustom = (eData.OriginalCBData != null),
                    Custom   = eData.OriginalCBData,
                    Vanilla  = eData.OriginalBData,
                    PipelineScoreDisabledReason = PipelineScoreDisabledReason
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
                Vanilla  = (eData.OriginalCBData == null) ? ConvertEditableCBD.ConvertVanilla(eData) : null,
                PipelineScoreDisabledReason = PipelineScoreDisabledReason
            };
        }

        /// <summary>
        /// Reset per-run state that can otherwise leak across maps or when optional modules are disabled.
        /// </summary>
        private static void ResetRunState(EditableCBD eData)
        {
            // The pipeline can be invoked more than once for one Play action. Always rewind
            // the shared stream, while individual generators use their own named streams.
            TransitionPatcher.ResetRepeatableRandom();

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
            eData.ObstaclesChangedByWallGenerator = false;
            eData.ObstaclesChangedByBeatSageCleanUp = false;
            eData.ObstaclesChangedByArcitect = false;
            eData.ArcsChangedByArcitect = false;
            eData.ChainsChangedByArcitect = false;
            eData.ArcsChangedByDifficultyReducer = false;
            eData.ChainsChangedByDifficultyReducer = false;
            eData.BasicEventsChanged = false;
            eData.ColorBoostEventsChanged = false;
            eData.CustomEventsChanged = false;

            // Reset module-level/static flags that can otherwise go stale between runs.
            // These are safe even if some module is disabled this run.
            LightsGenerator.LightEventsAdded = false;
            BeatSageCleanUp.DisableScoreSubmission = false;
            WallGenerator.ResetRunState();

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
            if (originalNjs == 0f)
            {
                Plugin.LogDebug($"[InitializeMovementData] Init skipped because original NJS was 0.");
                return; // this means we never got valid data for the current map, so skip initialization to avoid writing stale data.
            }

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
            bool enableDiffRed = Config.Instance.EnableForAllMaps || eligibleHighNpsSong;

            if (!enableDiffRed) return;

            int originalNotesCount = eData.ColorNotes.Count;
            int originalBombCount = eData.BombNotes.Count;

            AutoDifficultyReducer.InitialCalculations(eData, TransitionPatcher.bpm, TransitionPatcher.NotesPerSecond);

            eData.ColorNotes = AutoDifficultyReducer.SimplifyBeatmap(eData);

            AutoDifficultyReducer.ApplyArcAndChainEndpointChanges(eData, eData.ColorNotes);

            eData.ColorNotes = eData.ColorNotes.OrderBy(n => n.time).ToList();
            if (eData.Arcs != null)
                eData.Arcs = eData.Arcs.OrderBy(a => a.time).ToList();

            if (eData.Chains != null)
                eData.Chains = eData.Chains.OrderBy(c => c.time).ToList();

            int finalNotesCount = eData.ColorNotes.Count;
            int finalBombCount = eData.BombNotes.Count;
            Plugin.LogDebug($"[Pipeline DiffReducer] Inital Note Count: {originalNotesCount}, Bomb Count: {originalBombCount} -- Final Note Count: {finalNotesCount}, Bomb Count: {finalBombCount}");// ColorA: {eData.ColorNotes.Where(r => r.colorType == ColorType.ColorA).Count()} ColorB: {eData.ColorNotes.Where(r => r.colorType == ColorType.ColorB).Count()}");
            if (originalNotesCount != finalNotesCount)
            {
                eData.ColorNotesChanged = true;
                TransitionPatcher.DifficultyReducerRemovedNotes = true;
            }
            if (originalBombCount != finalBombCount)
            {
                eData.BombNotesChanged = true;
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

            bool wallsDisabled = !Utils.IsEnabledWalls();

            bool wallHelpersNeedOriginalWalls =
                wallsDisabled &&
                ((addArcs && eData.ArcsChanged) || (addChains && eData.ChainsChanged));

            if (wallHelpersNeedOriginalWalls)
                WallGenerator.PrepareOriginalWallsOnly(eData);

            if (addArcs && eData.ArcsChanged)
            {
                if (wallsDisabled)
                    moveWallsBlockingArc = WallGenerator.MoveWallsBlockingArc(eData);
            }

            if (addChains && eData.ChainsChanged)
            {
                if (wallsDisabled)
                    moveWallsBlockingChainTail = WallGenerator.MoveWallsBlockingChainTail(eData);
            }

            if (wallHelpersNeedOriginalWalls)
                WallGenerator.FinalizeOriginalWallsOnly(eData);

            if (moveWallsBlockingArc || moveWallsBlockingChainTail)
            {
                eData.ObstaclesChanged = true;
                eData.ObstaclesChangedByArcitect = true;
            }
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

            var obstaclesBeforeWallGenerator = eData.Obstacles
                .OrderBy(o => o.time)
                .Select(o => (
                    t: MathF.Round(o.time, 4),
                    d: MathF.Round(o.duration, 4),
                    l: o.line,
                    y: o.layer,
                    w: o.width,
                    h: o.height,
                    r: o.rotation))
                .ToList();

            WallGenerator.SetOriginalWalls(eData);
            WallGenerator.ResetWalls(eData);

            RunWallLoop(eData);

            FinalizeWallGeneration(eData);

            var obstaclesAfterWallGenerator = eData.Obstacles
                .OrderBy(o => o.time)
                .Select(o => (
                    t: MathF.Round(o.time, 4),
                    d: MathF.Round(o.duration, 4),
                    l: o.line,
                    y: o.layer,
                    w: o.width,
                    h: o.height,
                    r: o.rotation))
                .ToList();

            if (obstaclesBeforeWallGenerator.Count != obstaclesAfterWallGenerator.Count ||
                !obstaclesBeforeWallGenerator.SequenceEqual(obstaclesAfterWallGenerator))
            {
                eData.ObstaclesChanged = true;
                eData.ObstaclesChangedByWallGenerator = true;
            }

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

                float size = 2.0f;
                if (eData.WallCutMoments.Count > 0)
                {
                    gaps = WallGenerator.FindGapsUsingRotations(eData.WallCutMoments, size);
                    Plugin.LogDebug($"[FinalizeWallGeneration][FindGapsUsingRotations] using WallCutMoments found {gaps.Count} gaps with size {size}s to help set floor walls and mega walls.");
                }
                else
                {
                    gaps = WallGenerator.FindGapsUsingNotes(eData.ColorNotes, size);
                    Plugin.LogDebug($"[FinalizeWallGeneration][FindGapsUsingNotes] No WallCutMoments! So using notes found {gaps.Count} gaps with size {size}s to help set floor walls and mega walls.");
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

                //if (WallGenerator.allWalls.Count > 0) // Github Issue #2 Walls gone when using autolights
                WallGenerator.FinalizeWallsToMap(eData);

                //if (WallGenerator._originalWalls.Count > 0 || WallGenerator._allWalls.Count > 0) // Github Issue #2 Walls gone when using autolights
                //    WallGenerator.FinalizeOriginalOnlyWallsToMap(eData);

                eData.Obstacles = eData.Obstacles.OrderBy(o => o.time).ToList();


                if (leanCrouchWallRemoval ||
                    moveWallsBlockingArc ||
                    moveWallsBlockingChainTail ||
                    removeCrouchWallsBlockingChains ||
                    eData.OriginalObstacleCount != eData.Obstacles.Count)
                {
                    eData.ObstaclesChanged = true;
                }
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

            //bool shouldAdjustRotations =
            //    RotationGenerator.needsRotationLimitAdjustment &&
            //    !Config.Instance.Wireless360;

            if (!Config.Instance.Wireless360)
             eData.RotationEvents = RotationGenerator.AdjustRotationsToLimit(eData.RotationEvents); // moved this here since wallGenerator can change rotations for crouch walls

            eData.RotationEvents = ERotationEventData.RecalculateAccumulatedRotations(eData.RotationEvents);

            eData.ColorBoostEvents = eData.ColorBoostEvents.OrderBy(b => b.time).ToList();
            eData.BasicEvents = eData.BasicEvents.OrderBy(e => e.time).ToList();
            eData.Arcs = eData.Arcs.OrderBy(a => a.time).ToList();
            eData.Chains = eData.Chains.OrderBy(c => c.time).ToList();
        }

    }
}
