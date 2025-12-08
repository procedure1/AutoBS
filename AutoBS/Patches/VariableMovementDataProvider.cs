using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace AutoBS.Patches
{
    // Used for my version of AutoNjsFixer
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
            //__state = WillOverride; ?? fix???
            if (!WillOverride) return;

            Plugin.Log.Debug($"[VariableMovementDataProvider][AutoNjsFixer] Called...");

            try
            {
                // 1) Safely grab BS_Utils level data
                var lvlData = BS_Utils.Plugin.LevelData;
                if (lvlData == null) return;
                if (!lvlData.IsSet) return;
                if (BS_Utils.Gameplay.Gamemode.IsIsolatedLevel) return;

                // 2) Safely grab your config
                if (!Utils.IsEnabledAutoNjsFixer()) return;

                // 3) Only Standard or Multiplayer
                if (lvlData.Mode != BS_Utils.Gameplay.Mode.Standard
                 && lvlData.Mode != BS_Utils.Gameplay.Mode.Multiplayer)
                    return;

                // 4) Practice check
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

            /*
            // have to recalculate since user may have changed desiredNJS or desiredJD in config menu and replayed the same map which measn SetContent will not update the new values or set the registry for changes.
            (float finalNJS, float finalJD, float originalJD) = AutoNjsFixer.Fix(originalNJS, originalNJO, bpm);

            if (finalNJS <= 0 || finalJD <= 0)
            {
                Plugin.Log.Warn($"[VariableMovementDataProvider][AutoNjsFixer] Aborting AutoNjsFixer because finalNJS or finalJD is zero or negative. finalNJS: {finalNJS}, finalJD: {finalJD}");
                return;
            }
            */
            /*
            // Store the values in the registry for other patches to use
            AutoNjsRegistry.byKey[TransitionPatcher.CurrentPlayKey] =
                new AutoNjsRegistry.Data
                {
                    original_njs = originalNJS,
                    original_jd = originalJD,
                    autoNjs_njs = finalNJS,
                    autoNjs_jd = finalJD
                };
            */
            //TransitionPatcher.NoteJumpMovementSpeed = finalNJS; // store for other patches
            //TransitionPatcher.JumpDistance = finalJD; // store for other patches

            //var autoData = AutoNjsRegistry.findByKey(TransitionPatcher.CurrentPlayKey); // not using 

            //if (autoData == null) return;

            //float finalNJS = autoData.autoNjs_njs;
            //float finalJD  = autoData.autoNjs_jd;


            // disablescore if cheating down
            //if (BS_Utils.Plugin.LevelData.Mode == BS_Utils.Gameplay.Mode.Standard && finalNJS < originalNJS)
            //    BS_Utils.Gameplay.ScoreSubmission.DisableSubmission("Auto NJS Fixer");

            noteJumpMovementSpeed = TransitionPatcher.FinalNoteJumpMovementSpeed;
            noteJumpValueType = BeatmapObjectSpawnMovementData.NoteJumpValueType.JumpDuration;
            noteJumpValue = TransitionPatcher.FinalJumpDistance / TransitionPatcher.FinalNoteJumpMovementSpeed / 2f;
            //noteJumpValue = finalJD / finalNJS / 2f;

            bool maintainVelocity = (Config.Instance.AutoNjsFixerMode == Config.AutoNjsFixerModeType.MaintainNoteSpeed) ? true : false; // if false , it's ForceNJS mode

            if (maintainVelocity)
            {
                Plugin.Log.Debug($"[VariableMovementDataProvider][AutoNjsFixer] MAINTAIN PERCEIVED VELOCITY MODE: NJS:{noteJumpMovementSpeed:F2} JD:{TransitionPatcher.FinalJumpDistance:F2} -- (original NJS: {originalNJS})");
            }
            else
            {
                Plugin.Log.Debug($"[VariableMovementDataProvider][AutoNjsFixer] FORCE NJS MODE: NJS:{noteJumpMovementSpeed} JD:{TransitionPatcher.FinalJumpDistance} -- (original NJS: {originalNJS}) ");
            }
        }
    }
}