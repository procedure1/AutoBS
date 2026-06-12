using HarmonyLib;
using System;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace AutoBS.Patches
{
    internal static class LiveGameplayRuntimeState
    {
        internal static bool SongRunning;

        internal static void Reset()
        {
            SongRunning = false;
        }
    }
    internal static class AutoNjsRuntimeState
    {
        internal class State
        {
            public bool Active;
            public bool AutoNjsFixerActive;

            public float OriginalNjs;
            public float FinalBaseNjs;
            public float FinalBaseJd;
            public float BaseJumpDuration;

            public bool LiveNjsScoreGateTriggered;

            public int LastStepLogBucket = -1;

            // Last values actually pushed into VariableMovementDataProvider
            public bool HasAppliedValues;
            public float LastAppliedNjs;
            public float LastAppliedJd;

            // Capture actual provider baseline once, after Init has finished
            public bool BaselineFromProviderCaptured;
        }

        internal static readonly ConditionalWeakTable<VariableMovementDataProvider, State> Table =
            new ConditionalWeakTable<VariableMovementDataProvider, State>();

        internal static bool FlexibleDuration = true;

        internal static float LiveNjsOffset = 0f;
        internal static int PendingNjsSteps = 0;

        internal static float LiveJdOffset = 0f;
        internal static int PendingJdSteps = 0;

        public static bool AutoNjsFixerEnabled;

        internal static void ResetLive()
        {
            LiveNjsOffset = 0f;
            PendingNjsSteps = 0;
            LiveJdOffset = 0f;
            PendingJdSteps = 0;
        }
    }

    [HarmonyPatch(typeof(VariableMovementDataProvider), "Init")]
    static class VariableMovementDataProviderInitPatch
    {
        public static void Prefix(
            VariableMovementDataProvider __instance,
            ref float startHalfJumpDurationInBeats,
            ref float maxHalfJumpDistance,
            ref float noteJumpMovementSpeed,
            ref float minRelativeNoteJumpSpeed,
            ref float bpm,
            ref BeatmapObjectSpawnMovementData.NoteJumpValueType noteJumpValueType,
            ref float noteJumpValue,
            ref Vector3 centerPosition,
            ref Vector3 forwardVector
        )
        {
            if (!Config.Instance.EnablePlugin)
                return;

            /*
            if (ScoreSubmission.ScoreGate.ForceLiveNjsReasonForTesting)
            {
                ScoreSubmission.ScoreGate.AddReason("Live NJS");
                Plugin.LogDebug("[ScoreGate][TEST] Forced Live NJS score-disable reason during movement provider initialization.");
            }
            */

            bool liveNjsEnabled = Config.Instance.EnableLiveNjsJdControl &&
                                  Config.Instance.LiveNjsControl != Config.LiveControlModeType.Off;
            bool liveJdEnabled = Config.Instance.EnableLiveNjsJdControl &&
                                 Config.Instance.LiveJdControl != Config.LiveControlModeType.Off;
            bool wantsLiveControl = liveNjsEnabled || liveJdEnabled;

            bool autoNjsFixerEnabled = AutoNjsRuntimeState.AutoNjsFixerEnabled;

            // If neither AutoNjsFixer nor live controller adjustments are in use, do nothing.
            if (!autoNjsFixerEnabled && !wantsLiveControl)
                return;

            float realBpm = bpm == 0 ? TransitionPatcher.bpm : bpm;

            float givenNJS = noteJumpMovementSpeed; // could be autoNjsFixer provided or original if disabled.
            float originalNJO = TransitionPatcher.OriginalNoteJumpOffset;
            float givenJD = AutoNjsFixer.GetJumpDistance(realBpm, givenNJS, originalNJO); // could be autoNjsFixer provided or original if disabled.

            var state = AutoNjsRuntimeState.Table.GetOrCreateValue(__instance);

            // Baseline defaults: game/original values
            state.Active = true;
            state.AutoNjsFixerActive = false;
            state.OriginalNjs = TransitionPatcher.OriginalNoteJumpMovementSpeed;
            state.FinalBaseNjs = givenNJS;
            state.FinalBaseJd = givenJD;
            state.BaseJumpDuration = givenNJS > 0.01f ? (givenJD / givenNJS) : 0f;
            state.LiveNjsScoreGateTriggered = TransitionPatcher.SelectedSerializedName == GameModeHelper.GENERATED_360DEGREE_MODE; // force off for gen 360

            state.HasAppliedValues = false;
            state.LastAppliedNjs = 0f;
            state.LastAppliedJd = 0f;
            state.BaselineFromProviderCaptured = false;

            // If AutoNjsFixer is not enabled, baseline is enough for live controls.
            if (!autoNjsFixerEnabled)
            {
                Plugin.LogDebug(
                    $"[VariableMovementDataProvider][LiveAdjust] Baseline only. " +
                    $"NJS:{state.FinalBaseNjs:F2} JD:{state.FinalBaseJd:F2} JumpDur:{state.BaseJumpDuration:F3}");
                return;
            }

            bool willOverride =
                BS_Utils.Plugin.LevelData.IsSet &&
                !BS_Utils.Gameplay.Gamemode.IsIsolatedLevel &&
                (Config.Instance.EnabledInPractice ||
                 BS_Utils.Plugin.LevelData.GameplayCoreSceneSetupData.practiceSettings == null);

            if (!willOverride)
                return;

            try
            {
                var lvlData = BS_Utils.Plugin.LevelData;
                if (lvlData == null) return;
                if (!lvlData.IsSet) return;
                if (BS_Utils.Gameplay.Gamemode.IsIsolatedLevel) return;

                if (lvlData.Mode != BS_Utils.Gameplay.Mode.Standard &&
                    lvlData.Mode != BS_Utils.Gameplay.Mode.Multiplayer)
                    return;

                if ((!Config.Instance.EnabledInPractice || TransitionPatcher.AutoNJSPracticeModeDisabledByConflictingMod) &&
                    lvlData.GameplayCoreSceneSetupData?.practiceSettings != null)
                    return;
            }
            catch (Exception ex)
            {
                Plugin.Log.Error($"[VariableMovementDataProvider][AutoNjsFixer] Init patch failed: {ex}");
                return;
            }

            float finalNJS = TransitionPatcher.FinalNoteJumpMovementSpeed > 0
                ? TransitionPatcher.FinalNoteJumpMovementSpeed
                : givenNJS;

            float finalJD = TransitionPatcher.FinalJumpDistance > 0
                ? TransitionPatcher.FinalJumpDistance
                : givenJD;

            bool njsChanged = Math.Abs(finalNJS - givenNJS) > 0.01f;
            bool jdChanged = Math.Abs(finalJD - givenJD) > 0.01f;

            if (!njsChanged && !jdChanged)
            {
                Plugin.LogDebug("[VariableMovementDataProvider][AutoNjsFixer] (final == original). Using original baseline.");
                return;
            }

            state.AutoNjsFixerActive = true;
            state.FinalBaseNjs = finalNJS;
            state.FinalBaseJd = finalJD;
            state.BaseJumpDuration = finalNJS > 0.01f ? (finalJD / finalNJS) : 0f;

            noteJumpMovementSpeed = finalNJS;
            noteJumpValueType = BeatmapObjectSpawnMovementData.NoteJumpValueType.JumpDuration;
            noteJumpValue = state.BaseJumpDuration / 2f;

            bool preserveTravelTime =
                Config.Instance.AutoNjsFixerMode == Config.AutoNjsFixerModeType.PreserveTravelTime;

            string liveMode = AutoNjsRuntimeState.FlexibleDuration
                ? "Live: Fixed JD / Flexible Duration"
                : "Live: Fixed Duration / Flexible JD";

            if (preserveTravelTime)
            {
                Plugin.LogDebug(
                    $"[VariableMovementDataProvider][AutoNjsFixer] Preserve Travel Time MODE: " +
                    $"NJS:{finalNJS:F2} JD:{finalJD:F2} JumpDur:{state.BaseJumpDuration:F3} " +
                    $"-- (original NJS:{givenNJS:F2} original JD:{givenJD:F2}) -- {liveMode}");
            }
            else
            {
                Plugin.LogDebug(
                    $"[VariableMovementDataProvider][AutoNjsFixer] Set Note Speed MODE: " +
                    $"NJS:{finalNJS:F2} JD:{finalJD:F2} JumpDur:{state.BaseJumpDuration:F3} " +
                    $"-- (original NJS:{givenNJS:F2} original JD:{givenJD:F2}) -- {liveMode}");
            }
        }
    }

    //This is for realtime in game njs jd updates
    [HarmonyPatch(typeof(VariableMovementDataProvider), "ManualUpdate")]
    static class VariableMovementDataProviderManualUpdatePatch
    {
        static readonly AccessTools.FieldRef<VariableMovementDataProvider, float> _targetNoteJumpMovementSpeed =
            AccessTools.FieldRefAccess<VariableMovementDataProvider, float>("_targetNoteJumpMovementSpeed");

        static readonly AccessTools.FieldRef<VariableMovementDataProvider, float> _noteJumpMovementSpeed =
            AccessTools.FieldRefAccess<VariableMovementDataProvider, float>("_noteJumpMovementSpeed");

        static readonly AccessTools.FieldRef<VariableMovementDataProvider, float> _prevNoteJumpMovementSpeed =
            AccessTools.FieldRefAccess<VariableMovementDataProvider, float>("_prevNoteJumpMovementSpeed");

        static readonly AccessTools.FieldRef<VariableMovementDataProvider, float> _jumpDistance =
            AccessTools.FieldRefAccess<VariableMovementDataProvider, float>("_jumpDistance");

        static readonly AccessTools.FieldRef<VariableMovementDataProvider, float> _halfJumpDistance =
            AccessTools.FieldRefAccess<VariableMovementDataProvider, float>("_halfJumpDistance");

        static readonly AccessTools.FieldRef<VariableMovementDataProvider, float> _jumpDuration =
            AccessTools.FieldRefAccess<VariableMovementDataProvider, float>("_jumpDuration");

        static readonly AccessTools.FieldRef<VariableMovementDataProvider, float> _halfJumpDuration =
            AccessTools.FieldRefAccess<VariableMovementDataProvider, float>("_halfJumpDuration");

        static readonly AccessTools.FieldRef<VariableMovementDataProvider, float> _waitingDuration =
            AccessTools.FieldRefAccess<VariableMovementDataProvider, float>("_waitingDuration");

        static readonly AccessTools.FieldRef<VariableMovementDataProvider, float> _spawnAheadTime =
            AccessTools.FieldRefAccess<VariableMovementDataProvider, float>("_spawnAheadTime");

        static readonly AccessTools.FieldRef<VariableMovementDataProvider, Vector3> _centerPosition =
            AccessTools.FieldRefAccess<VariableMovementDataProvider, Vector3>("_centerPosition");

        static readonly AccessTools.FieldRef<VariableMovementDataProvider, Vector3> _forwardVector =
            AccessTools.FieldRefAccess<VariableMovementDataProvider, Vector3>("_forwardVector");

        static readonly AccessTools.FieldRef<VariableMovementDataProvider, Vector3> _moveStartPosition =
            AccessTools.FieldRefAccess<VariableMovementDataProvider, Vector3>("_moveStartPosition");

        static readonly AccessTools.FieldRef<VariableMovementDataProvider, Vector3> _moveEndPosition =
            AccessTools.FieldRefAccess<VariableMovementDataProvider, Vector3>("_moveEndPosition");

        static readonly AccessTools.FieldRef<VariableMovementDataProvider, Vector3> _jumpEndPosition =
            AccessTools.FieldRefAccess<VariableMovementDataProvider, Vector3>("_jumpEndPosition");

        private const float NjsEpsilon = 0.0001f;
        private const float JdEpsilon = 0.0001f;

        public static void Postfix(VariableMovementDataProvider __instance, float songTime)
        {
            if (!Config.Instance.EnablePlugin || !LiveGameplayRuntimeState.SongRunning)
            {
                AutoNjsRuntimeState.PendingNjsSteps = 0;
                AutoNjsRuntimeState.PendingJdSteps = 0;
                return;
            }

            bool autoNjsFixerEnabled = AutoNjsRuntimeState.AutoNjsFixerEnabled;
            bool liveNJSEnabled = Config.Instance.EnableLiveNjsJdControl &&
                                  Config.Instance.LiveNjsControl != Config.LiveControlModeType.Off;
            bool liveJDEnabled = Config.Instance.EnableLiveNjsJdControl &&
                                 Config.Instance.LiveJdControl != Config.LiveControlModeType.Off;

            if (!autoNjsFixerEnabled && !liveNJSEnabled && !liveJDEnabled)
            {
                AutoNjsRuntimeState.PendingNjsSteps = 0;
                AutoNjsRuntimeState.PendingJdSteps = 0;
                return;
            }

            if (!liveNJSEnabled)
            {
                AutoNjsRuntimeState.PendingNjsSteps = 0;
                AutoNjsRuntimeState.LiveNjsOffset = 0f;
            }

            if (!liveJDEnabled)
            {
                AutoNjsRuntimeState.PendingJdSteps = 0;
                AutoNjsRuntimeState.LiveJdOffset = 0f;
            }

            if (!AutoNjsRuntimeState.Table.TryGetValue(__instance, out var state))
                return;

            if (!state.Active)
                return;

            if (!state.BaselineFromProviderCaptured)
            {
                float providerNjs = _noteJumpMovementSpeed(__instance);
                float providerJd = _jumpDistance(__instance);
                float providerJumpDuration = _jumpDuration(__instance);

                state.FinalBaseNjs = Mathf.Max(1f, providerNjs);
                state.FinalBaseJd = Mathf.Max(1f, providerJd);
                state.BaseJumpDuration = providerJumpDuration > 0.0001f
                    ? providerJumpDuration
                    : (state.FinalBaseJd / Mathf.Max(1f, state.FinalBaseNjs));

                state.BaselineFromProviderCaptured = true;

                Plugin.LogDebug(
                    $"[VariableMovementDataProvider][ManualUpdate] " +
                    $"Captured movement baseline from provider. " +
                    $"BaseNJS:{state.FinalBaseNjs:F2} BaseJD:{state.FinalBaseJd:F2} BaseJumpDuration:{state.BaseJumpDuration:F3}");
            }

            try
            {
                bool showNjsHud = false;
                bool showJdHud = false;

                if (liveNJSEnabled)
                {
                    int pendingNjsSteps = AutoNjsRuntimeState.PendingNjsSteps;
                    if (pendingNjsSteps != 0)
                    {
                        AutoNjsRuntimeState.PendingNjsSteps = 0;
                        AutoNjsRuntimeState.LiveNjsOffset += pendingNjsSteps;

                        Plugin.Log.Info(
                            $"[VariableMovementDataProvider][ManualUpdate] " +
                            $"Applied queued NJS step(s): {pendingNjsSteps:+#;-#;0} " +
                            $"LiveNjsOffset now: {AutoNjsRuntimeState.LiveNjsOffset:+0;-0;0}");

                        showNjsHud = true;
                    }
                }

                if (liveJDEnabled)
                {
                    int pendingJdSteps = AutoNjsRuntimeState.PendingJdSteps;
                    if (pendingJdSteps != 0)
                    {
                        AutoNjsRuntimeState.PendingJdSteps = 0;
                        AutoNjsRuntimeState.LiveJdOffset += pendingJdSteps * 2f;

                        Plugin.Log.Info(
                            $"[VariableMovementDataProvider][ManualUpdate] " +
                            $"Applied queued JD step(s): {pendingJdSteps:+#;-#;0} " +
                            $"LiveJdOffset now: {AutoNjsRuntimeState.LiveJdOffset:+0.0;-0.0;0}");

                        showJdHud = true;
                    }
                }

                float effectiveNjs = state.FinalBaseNjs + AutoNjsRuntimeState.LiveNjsOffset;
                effectiveNjs = Mathf.Clamp(effectiveNjs, 1f, 40f);

                float effectiveJd = state.FinalBaseJd + AutoNjsRuntimeState.LiveJdOffset;
                effectiveJd = Mathf.Clamp(effectiveJd, 1f, 100f);

                bool flexibleDuration = AutoNjsRuntimeState.FlexibleDuration;

                if (!state.LiveNjsScoreGateTriggered &&
                    liveNJSEnabled &&
                    Mathf.Abs(AutoNjsRuntimeState.LiveNjsOffset) > 0.0001f)
                {
                    state.LiveNjsScoreGateTriggered = true;
                    ScoreSubmission.ScoreGate.AddReason("Live NJS");

                    Plugin.Log.Info(
                        $"[ScoreGate] Disabled by live NJS change. " +
                        $"Base NJS:{state.FinalBaseNjs:F2} NJS when disabled:{effectiveNjs:F2}");
                }

                bool valuesChanged =
                    !state.HasAppliedValues ||
                    Mathf.Abs(state.LastAppliedNjs - effectiveNjs) > NjsEpsilon ||
                    Mathf.Abs(state.LastAppliedJd - effectiveJd) > JdEpsilon;

                if (!valuesChanged)
                {
                    if (showJdHud)
                        LiveAdjustHudRuntime.Instance?.ShowJdValue(effectiveJd);
                    else if (showNjsHud)
                        LiveAdjustHudRuntime.Instance?.ShowNjsValue(effectiveNjs);

                    return;
                }

                float appliedNjs = effectiveNjs;
                float jumpDuration;
                float halfJumpDuration;
                float jumpDistance;
                float halfJumpDistance;

                if (flexibleDuration)
                {
                    jumpDistance = effectiveJd;
                    halfJumpDistance = jumpDistance * 0.5f;
                    jumpDuration = jumpDistance / appliedNjs;
                    halfJumpDuration = jumpDuration * 0.5f;
                }
                else
                {
                    jumpDuration = state.BaseJumpDuration;
                    halfJumpDuration = jumpDuration * 0.5f;

                    jumpDistance = effectiveJd;
                    halfJumpDistance = jumpDistance * 0.5f;

                    if (jumpDuration > 0.0001f)
                        appliedNjs = jumpDistance / jumpDuration;
                }

                float prevNjs = _noteJumpMovementSpeed(__instance);

                _prevNoteJumpMovementSpeed(__instance) = prevNjs;
                _targetNoteJumpMovementSpeed(__instance) = appliedNjs;
                _noteJumpMovementSpeed(__instance) = appliedNjs;

                _jumpDistance(__instance) = jumpDistance;
                _halfJumpDistance(__instance) = halfJumpDistance;
                _jumpDuration(__instance) = jumpDuration;
                _halfJumpDuration(__instance) = halfJumpDuration;

                _spawnAheadTime(__instance) = 0.5f + halfJumpDuration;
                _waitingDuration(__instance) = _spawnAheadTime(__instance) - 0.5f - halfJumpDuration;

                Vector3 center = _centerPosition(__instance);
                Vector3 forward = _forwardVector(__instance);

                _moveStartPosition(__instance) = center + forward * (100f + halfJumpDistance);
                _moveEndPosition(__instance) = center + forward * halfJumpDistance;
                _jumpEndPosition(__instance) = center - forward * halfJumpDistance;

                state.HasAppliedValues = true;
                state.LastAppliedNjs = effectiveNjs;
                state.LastAppliedJd = effectiveJd;

                if (showJdHud)
                    LiveAdjustHudRuntime.Instance?.ShowJdValue(effectiveJd);
                else if (showNjsHud)
                    LiveAdjustHudRuntime.Instance?.ShowNjsValue(appliedNjs);
            }
            catch (Exception ex)
            {
                Plugin.Log.Error($"[VariableMovementDataProvider][AutoNjsFixer] ManualUpdate patch failed: {ex}");
            }
        }
    }











    // OLD ----------------------


    // attempts to preserve jump duration during NJS events by keeping the base NJS constant and allowing JD to vary with current NJS.
    /// <summary>
    /// AutoNjsFixer patch. Overrides the initial note jump movement speed and jump distance. 
    /// Works with NJS events but unlike vanilla, JD is changed to keep jump duration the same across events. (This was the only working solution I could find.)
    /// </summary>
    /// <remarks></remarks>
    ///
    /*
    // USE THIS if don't use manual update patch for allowing user to vary njs with a button press.
    [HarmonyPatch(typeof(VariableMovementDataProvider), "Init")]
    static class VariableMovementDataProviderInitPatch
    {
        public static void Prefix(
            ref float startHalfJumpDurationInBeats,
            ref float maxHalfJumpDistance,
            ref float noteJumpMovementSpeed,
            ref float minRelativeNoteJumpSpeed,
            ref float bpm,
            ref BeatmapObjectSpawnMovementData.NoteJumpValueType noteJumpValueType,
            ref float noteJumpValue,
            ref Vector3 centerPosition,
            ref Vector3 forwardVector
        )
        {
            if (!Config.Instance.EnablePlugin) return;
            if (!Utils.IsEnabledAutoNjsFixer()) return; // checks for conflicting mods too

            bool willOverride =
                BS_Utils.Plugin.LevelData.IsSet &&
                !BS_Utils.Gameplay.Gamemode.IsIsolatedLevel &&
                (BS_Utils.Plugin.LevelData.Mode == BS_Utils.Gameplay.Mode.Standard ||
                 BS_Utils.Plugin.LevelData.Mode == BS_Utils.Gameplay.Mode.Multiplayer) &&
                (Config.Instance.EnabledInPractice ||
                 BS_Utils.Plugin.LevelData.GameplayCoreSceneSetupData.practiceSettings == null);

            if (!willOverride) return;

            try
            {
                var lvlData = BS_Utils.Plugin.LevelData;
                if (lvlData == null) return;
                if (!lvlData.IsSet) return;
                if (BS_Utils.Gameplay.Gamemode.IsIsolatedLevel) return;

                // Only Standard or Multiplayer
                if (lvlData.Mode != BS_Utils.Gameplay.Mode.Standard
                 && lvlData.Mode != BS_Utils.Gameplay.Mode.Multiplayer)
                    return;

                // Practice check
                if ((!Config.Instance.EnabledInPractice || TransitionPatcher.AutoNJSPracticeModeDisabledByConflictingMod) &&
                    lvlData.GameplayCoreSceneSetupData?.practiceSettings != null)
                    return;
            }
            catch (System.Exception ex)
            {
                Plugin.Log.Error($"[VariableMovementDataProvider][AutoNjsFixer] Init patch failed: {ex}");
                return;
            }

            float realBpm = bpm == 0 ? TransitionPatcher.bpm : bpm;

            float originalNJS = noteJumpMovementSpeed;
            float originalNJO = TransitionPatcher.OriginalNoteJumpOffset;
            float originalJD = AutoNjsFixer.GetJumpDistance(realBpm, originalNJS, originalNJO);

            float finalNJS = TransitionPatcher.FinalNoteJumpMovementSpeed > 0
                ? TransitionPatcher.FinalNoteJumpMovementSpeed
                : originalNJS;

            float finalJD = TransitionPatcher.FinalJumpDistance > 0
                ? TransitionPatcher.FinalJumpDistance
                : originalJD;

            bool njsChanged = Math.Abs(finalNJS - originalNJS) > 0.01f;
            bool jdChanged = Math.Abs(finalJD - originalJD) > 0.01f;

            if (!njsChanged && !jdChanged)
            {
                Plugin.LogDebug("[VariableMovementDataProvider][AutoNjsFixer] (final == original). NJS JD unchanged!");
                return;
            }

            noteJumpMovementSpeed = finalNJS;
            noteJumpValueType = BeatmapObjectSpawnMovementData.NoteJumpValueType.JumpDuration;
            noteJumpValue = finalJD / finalNJS / 2f;

            bool preserveTravelTime =
                Config.Instance.AutoNjsFixerMode == Config.AutoNjsFixerModeType.PreserveTravelTime;

            if (preserveTravelTime)
            {
                Plugin.LogDebug(
                    $"[VariableMovementDataProvider][AutoNjsFixer] Preserve Travel Time MODE: " +
                    $"NJS:{finalNJS:F2} JD:{finalJD:F2} JumpDur:{(finalJD / finalNJS):F3} " +
                    $"-- (original NJS:{originalNJS:F2} original JD:{originalJD:F2})");
            }
            else
            {
                Plugin.LogDebug(
                    $"[VariableMovementDataProvider][AutoNjsFixer] Set Note Speed MODE: " +
                    $"NJS:{finalNJS:F2} JD:{finalJD:F2} JumpDur:{(finalJD / finalNJS):F3} " +
                    $"-- (original NJS:{originalNJS:F2} original JD:{originalJD:F2})");
            }
        }
    }
    */
    /*
    //Tries to preserve JD during NJS event, but visually notes for low njs spawn closer and without bounce animation. so it is not working.
    [HarmonyPatch(typeof(VariableMovementDataProvider), "Init")]
    static class VariableMovementDataProviderInitPatch
    {
        public static void Prefix(
            VariableMovementDataProvider __instance,
            ref float startHalfJumpDurationInBeats,
            ref float maxHalfJumpDistance,
            ref float noteJumpMovementSpeed,
            ref float minRelativeNoteJumpSpeed,
            ref float bpm,
            ref BeatmapObjectSpawnMovementData.NoteJumpValueType noteJumpValueType,
            ref float noteJumpValue,
            ref Vector3 centerPosition,
            ref Vector3 forwardVector
        )
        {
            if (!Config.Instance.EnablePlugin) return;
            if (!Utils.IsEnabledAutoNjsFixer()) return; // checks for conflicting mods too

            bool WillOverride = BS_Utils.Plugin.LevelData.IsSet && !BS_Utils.Gameplay.Gamemode.IsIsolatedLevel
                && Utils.IsEnabledAutoNjsFixer()
                && (BS_Utils.Plugin.LevelData.Mode == BS_Utils.Gameplay.Mode.Standard || BS_Utils.Plugin.LevelData.Mode == BS_Utils.Gameplay.Mode.Multiplayer)
                && (Config.Instance.EnabledInPractice || BS_Utils.Plugin.LevelData.GameplayCoreSceneSetupData.practiceSettings == null);

            if (!WillOverride) return;

            try
            {
                var lvlData = BS_Utils.Plugin.LevelData;
                if (lvlData == null) return;
                if (!lvlData.IsSet) return;
                if (BS_Utils.Gameplay.Gamemode.IsIsolatedLevel) return;

                if (!Utils.IsEnabledAutoNjsFixer()) return;

                if (lvlData.Mode != BS_Utils.Gameplay.Mode.Standard
                 && lvlData.Mode != BS_Utils.Gameplay.Mode.Multiplayer)
                    return;

                if ((!Config.Instance.EnabledInPractice || TransitionPatcher.AutoNJSPracticeModeDisabledByConflictingMod) &&
                    lvlData.GameplayCoreSceneSetupData?.practiceSettings != null)
                    return;
            }
            catch (Exception ex)
            {
                Plugin.Log.Error($"[VariableMovementDataProvider][AutoNjsFixer] Init patch failed: {ex}");
            }

            float originalNJS = noteJumpMovementSpeed;
            float originalNJO = TransitionPatcher.OriginalNoteJumpOffset;
            float realBpm = bpm == 0 ? TransitionPatcher.bpm : bpm;
            float originalJD = AutoNjsFixer.GetJumpDistance(realBpm, originalNJS, originalNJO);

            float finalNJS = TransitionPatcher.FinalNoteJumpMovementSpeed > 0 ? TransitionPatcher.FinalNoteJumpMovementSpeed : originalNJS;
            float finalJD = TransitionPatcher.FinalJumpDistance > 0 ? TransitionPatcher.FinalJumpDistance : originalJD;

            bool njsChanged = Math.Abs(finalNJS - originalNJS) > 0.01f;
            bool jdChanged = Math.Abs(finalJD - originalJD) > 0.01f;

            var state = AutoNjsRuntimeState.Table.GetOrCreateValue(__instance);
            state.Active = njsChanged || jdChanged;
            state.FinalBaseNjs = finalNJS;
            state.FinalBaseJd = finalJD;

            if (!state.Active)
            {
                Plugin.LogDebug($"[VariableMovementDataProvider][AutoNjsFixer] Init (final == original). NJS JD unchanged!");
                return;
            }

            // Always enforce base NJS and base JD at init
            noteJumpMovementSpeed = finalNJS;
            noteJumpValueType = BeatmapObjectSpawnMovementData.NoteJumpValueType.JumpDuration;
            noteJumpValue = finalJD / finalNJS / 2f;

            Plugin.LogDebug(
                $"[VariableMovementDataProvider][AutoNjsFixer] Init Override: " +
                $"Base NJS:{finalNJS:F2} Base JD:{finalJD:F2} " +
                $"-- (original NJS:{originalNJS:F2} original JD:{originalJD:F2})");
        }
    }
    [HarmonyPatch(typeof(VariableMovementDataProvider), "ManualUpdate")]
    static class VariableMovementDataProviderManualUpdatePatch
    {
        static readonly AccessTools.FieldRef<VariableMovementDataProvider, float> _targetNoteJumpMovementSpeed =
            AccessTools.FieldRefAccess<VariableMovementDataProvider, float>("_targetNoteJumpMovementSpeed");

        static readonly AccessTools.FieldRef<VariableMovementDataProvider, float> _noteJumpMovementSpeed =
            AccessTools.FieldRefAccess<VariableMovementDataProvider, float>("_noteJumpMovementSpeed");

        static readonly AccessTools.FieldRef<VariableMovementDataProvider, float> _prevNoteJumpMovementSpeed =
            AccessTools.FieldRefAccess<VariableMovementDataProvider, float>("_prevNoteJumpMovementSpeed");

        static readonly AccessTools.FieldRef<VariableMovementDataProvider, float> _jumpDistance =
            AccessTools.FieldRefAccess<VariableMovementDataProvider, float>("_jumpDistance");

        static readonly AccessTools.FieldRef<VariableMovementDataProvider, float> _halfJumpDistance =
            AccessTools.FieldRefAccess<VariableMovementDataProvider, float>("_halfJumpDistance");

        static readonly AccessTools.FieldRef<VariableMovementDataProvider, float> _jumpDuration =
            AccessTools.FieldRefAccess<VariableMovementDataProvider, float>("_jumpDuration");

        static readonly AccessTools.FieldRef<VariableMovementDataProvider, float> _halfJumpDuration =
            AccessTools.FieldRefAccess<VariableMovementDataProvider, float>("_halfJumpDuration");

        static readonly AccessTools.FieldRef<VariableMovementDataProvider, float> _waitingDuration =
            AccessTools.FieldRefAccess<VariableMovementDataProvider, float>("_waitingDuration");

        static readonly AccessTools.FieldRef<VariableMovementDataProvider, float> _spawnAheadTime =
            AccessTools.FieldRefAccess<VariableMovementDataProvider, float>("_spawnAheadTime");

        static readonly AccessTools.FieldRef<VariableMovementDataProvider, Vector3> _centerPosition =
            AccessTools.FieldRefAccess<VariableMovementDataProvider, Vector3>("_centerPosition");

        static readonly AccessTools.FieldRef<VariableMovementDataProvider, Vector3> _forwardVector =
            AccessTools.FieldRefAccess<VariableMovementDataProvider, Vector3>("_forwardVector");

        static readonly AccessTools.FieldRef<VariableMovementDataProvider, Vector3> _moveStartPosition =
            AccessTools.FieldRefAccess<VariableMovementDataProvider, Vector3>("_moveStartPosition");

        static readonly AccessTools.FieldRef<VariableMovementDataProvider, Vector3> _moveEndPosition =
            AccessTools.FieldRefAccess<VariableMovementDataProvider, Vector3>("_moveEndPosition");

        static readonly AccessTools.FieldRef<VariableMovementDataProvider, Vector3> _jumpEndPosition =
            AccessTools.FieldRefAccess<VariableMovementDataProvider, Vector3>("_jumpEndPosition");

        static readonly AccessTools.FieldRef<VariableMovementDataProvider, bool> _wasUpdatedThisFrame =
            AccessTools.FieldRefAccess<VariableMovementDataProvider, bool>("<wasUpdatedThisFrame>k__BackingField");

        public static void Postfix(VariableMovementDataProvider __instance, float songTime)
        {
            if (!Config.Instance.EnablePlugin) return;
            if (!Utils.IsEnabledAutoNjsFixer()) return;

            if (!AutoNjsRuntimeState.Table.TryGetValue(__instance, out var state)) return;
            if (!state.Active) return;

            try
            {
                // At this point the game's original ManualUpdate has already run.
                // Since Init already replaced the provider's base NJS with our final base NJS,
                // _targetNoteJumpMovementSpeed already includes live NJS event deltas around that base.

                float currentNJS = _targetNoteJumpMovementSpeed(__instance);

                if (currentNJS < 0.01f)
                    currentNJS = 0.01f;

                float fixedJD = state.FinalBaseJd;
                float jumpDuration = fixedJD / currentNJS;
                float halfJumpDuration = jumpDuration * 0.5f;
                float halfJumpDistance = fixedJD * 0.5f;

                float prevNjs = _noteJumpMovementSpeed(__instance);

                bool changed =
                    Math.Abs(prevNjs - currentNJS) > 0.0001f ||
                    Math.Abs(_jumpDistance(__instance) - fixedJD) > 0.0001f ||
                    Math.Abs(_jumpDuration(__instance) - jumpDuration) > 0.0001f;

                if (!changed)
                    return;

                _prevNoteJumpMovementSpeed(__instance) = prevNjs;
                _targetNoteJumpMovementSpeed(__instance) = currentNJS;
                _noteJumpMovementSpeed(__instance) = currentNJS;

                _jumpDistance(__instance) = fixedJD;
                _halfJumpDistance(__instance) = halfJumpDistance;
                _jumpDuration(__instance) = jumpDuration;
                _halfJumpDuration(__instance) = halfJumpDuration;

                // In JumpDuration mode this is the internally consistent value
                _spawnAheadTime(__instance) = 0.5f + halfJumpDuration;
                _waitingDuration(__instance) = _spawnAheadTime(__instance) - 0.5f - halfJumpDuration;

                Vector3 center = _centerPosition(__instance);
                Vector3 forward = _forwardVector(__instance);

                _moveStartPosition(__instance) = center + forward * (100f + halfJumpDistance);
                _moveEndPosition(__instance) = center + forward * halfJumpDistance;
                _jumpEndPosition(__instance) = center - forward * halfJumpDistance;

                _wasUpdatedThisFrame(__instance) = true;

                Plugin.Log.Info(
                    $"[AutoNJS][ManualUpdate] songTime:{songTime:F2} " +
                    $"targetNJS:{_targetNoteJumpMovementSpeed(__instance):F2} " +
                    $"currentNJS:{_noteJumpMovementSpeed(__instance):F2} " +
                    $"JD:{_jumpDistance(__instance):F2} " +
                    $"JumpDur:{_jumpDuration(__instance):F3} " +
                    $"HalfJumpDur:{_halfJumpDuration(__instance):F3} " +
                    $"SpawnAhead:{_spawnAheadTime(__instance):F3} " +
                    $"MoveEndZ:{_moveEndPosition(__instance).z:F2}");

                 //Plugin.LogDebug(
                 //    $"[VariableMovementDataProvider][AutoNjsFixer] ManualUpdate: " +
                 //   $"songTime:{songTime:F2} NJS:{currentNJS:F2} JD:{fixedJD:F2} " +
                 //   $"JumpDur:{jumpDuration:F3}");
            }
            catch (Exception ex)
            {
                Plugin.Log.Error($"[VariableMovementDataProvider][AutoNjsFixer] ManualUpdate patch failed: {ex}");
            }
        }
   
    }
    */


    /*
    // works great but ignors NJS events
    // Used for AutoNjsFixer
        [HarmonyPatch(typeof(VariableMovementDataProvider), "Init")]
        static class VariableMovementDataProviderInitPatch
        {
            public static void Prefix(
                ref float startHalfJumpDurationInBeats,
                ref float maxHalfJumpDistance,
                ref float noteJumpMovementSpeed,
                ref float minRelativeNoteJumpSpeed,
                ref float bpm,
                ref BeatmapObjectSpawnMovementData.NoteJumpValueType noteJumpValueType,
                ref float noteJumpValue,
                ref Vector3 centerPosition,
                ref Vector3 forwardVector
            )
            {
                if (!Config.Instance.EnablePlugin) return;
                if (!Utils.IsEnabledAutoNjsFixer()) return; //checks for conflicting mods too

                bool WillOverride = BS_Utils.Plugin.LevelData.IsSet && !BS_Utils.Gameplay.Gamemode.IsIsolatedLevel
                    && Utils.IsEnabledAutoNjsFixer() && (BS_Utils.Plugin.LevelData.Mode == BS_Utils.Gameplay.Mode.Standard || BS_Utils.Plugin.LevelData.Mode == BS_Utils.Gameplay.Mode.Multiplayer) && (Config.Instance.EnabledInPractice || BS_Utils.Plugin.LevelData.GameplayCoreSceneSetupData.practiceSettings == null);

                if (!WillOverride) return;

                //Plugin.LogDebug($"[VariableMovementDataProvider][AutoNjsFixer] Called...");

                try
                {
                    // grab BS_Utils level data
                    var lvlData = BS_Utils.Plugin.LevelData;
                    if (lvlData == null) return;
                    if (!lvlData.IsSet) return;
                    if (BS_Utils.Gameplay.Gamemode.IsIsolatedLevel) return;

                    if (!Utils.IsEnabledAutoNjsFixer()) return;

                    // Only Standard or Multiplayer
                    if (lvlData.Mode != BS_Utils.Gameplay.Mode.Standard
                     && lvlData.Mode != BS_Utils.Gameplay.Mode.Multiplayer)
                        return;

                    // Practice check
                    if ((!Config.Instance.EnabledInPractice || TransitionPatcher.AutoNJSPracticeModeDisabledByConflictingMod) &&
                        lvlData.GameplayCoreSceneSetupData?.practiceSettings != null)
                        return;
                }
                catch (System.Exception ex)
                {
                    Plugin.Log.Error($"[VariableMovementDataProvider][AutoNjsFixer] patch failed: {ex}");
                }

                float originalNJS = noteJumpMovementSpeed; // from prefix argument
                float originalNJO = TransitionPatcher.OriginalNoteJumpOffset;
                float originalJD = AutoNjsFixer.GetJumpDistance(bpm == 0 ? TransitionPatcher.bpm : bpm, originalNJS, originalNJO);

                float finalNJS = TransitionPatcher.FinalNoteJumpMovementSpeed > 0 ? TransitionPatcher.FinalNoteJumpMovementSpeed : originalNJS;
                float finalJD = TransitionPatcher.FinalJumpDistance > 0 ? TransitionPatcher.FinalJumpDistance : originalJD;

                //Plugin.LogDebug($"[VariableMovementDataProvider][AutoNjsFixer] Original NJS: {originalNJS} Original JD: {originalJD} -- (bpm: {(bpm == 0 ? TransitionPatcher.bpm : bpm)})");

                bool njsChanged = Math.Abs(finalNJS - originalNJS) > 0.01f;
                bool jdChanged  = Math.Abs(finalJD - originalJD) > 0.01f;

                if (!njsChanged && !jdChanged)
                {
                    Plugin.LogDebug($"[VariableMovementDataProvider][AutoNjsFixer] (final == original). NJS JD Unchanged!");
                    return;
                }

                noteJumpMovementSpeed = finalNJS;
                noteJumpValueType = BeatmapObjectSpawnMovementData.NoteJumpValueType.JumpDuration;
                noteJumpValue = finalJD / finalNJS / 2f;

                bool preserveTravelTime = (Config.Instance.AutoNjsFixerMode == Config.AutoNjsFixerModeType.PreserveTravelTime) ? true : false; // if false , it's ForceNJS mode

                if (preserveTravelTime)
                {
                    Plugin.LogDebug($"[VariableMovementDataProvider][AutoNjsFixer] Preserve Travel Time MODE: NJS:{noteJumpMovementSpeed:F2} JD:{TransitionPatcher.FinalJumpDistance:F2} -- (original NJS: {originalNJS} original JD: {originalJD})");
                }
                else
                {
                    Plugin.LogDebug($"[VariableMovementDataProvider][AutoNjsFixer] Set Note Speed MODE: NJS:{noteJumpMovementSpeed} JD:{TransitionPatcher.FinalJumpDistance} -- (original NJS: {originalNJS} original JD: {originalJD}) ");
                }
            }
    }
    */
}
