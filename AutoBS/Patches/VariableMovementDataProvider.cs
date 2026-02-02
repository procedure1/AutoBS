using HarmonyLib;
using System;
using UnityEngine;

namespace AutoBS.Patches
{
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

            Plugin.LogDebug($"[VariableMovementDataProvider][AutoNjsFixer] Called...");

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
            float originalNJO = TransitionPatcher.NoteJumpOffset;
            float originalJD = AutoNjsFixer.GetJumpDistance(bpm == 0 ? TransitionPatcher.bpm : bpm, originalNJS, originalNJO);

            float finalNJS = TransitionPatcher.FinalNoteJumpMovementSpeed;
            float finalJD = TransitionPatcher.FinalJumpDistance;

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
                Plugin.LogDebug($"[VariableMovementDataProvider][AutoNjsFixer] Preserve Travel Time MODE: NJS:{noteJumpMovementSpeed:F2} JD:{TransitionPatcher.FinalJumpDistance:F2} -- (original NJS: {originalNJS})");
            }
            else
            {
                Plugin.LogDebug($"[VariableMovementDataProvider][AutoNjsFixer] Set Note Speed MODE: NJS:{noteJumpMovementSpeed} JD:{TransitionPatcher.FinalJumpDistance} -- (original NJS: {originalNJS}) ");
            }
        }
    }
}