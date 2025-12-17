using HarmonyLib;
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

            noteJumpMovementSpeed = TransitionPatcher.FinalNoteJumpMovementSpeed;
            noteJumpValueType = BeatmapObjectSpawnMovementData.NoteJumpValueType.JumpDuration;
            noteJumpValue = TransitionPatcher.FinalJumpDistance / TransitionPatcher.FinalNoteJumpMovementSpeed / 2f;

            bool maintainVelocity = (Config.Instance.AutoNjsFixerMode == Config.AutoNjsFixerModeType.MaintainNoteSpeed) ? true : false; // if false , it's ForceNJS mode

            if (maintainVelocity)
            {
                Plugin.LogDebug($"[VariableMovementDataProvider][AutoNjsFixer] MAINTAIN PERCEIVED SPEED MODE: NJS:{noteJumpMovementSpeed:F2} JD:{TransitionPatcher.FinalJumpDistance:F2} -- (original NJS: {originalNJS})");
            }
            else
            {
                Plugin.LogDebug($"[VariableMovementDataProvider][AutoNjsFixer] SET NOTE SPEED MODE: NJS:{noteJumpMovementSpeed} JD:{TransitionPatcher.FinalJumpDistance} -- (original NJS: {originalNJS}) ");
            }
        }
    }
}