using AutoBS.Patches;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static BeatmapObjectSpawnMovementData;

namespace AutoBS
{
    public class AutoNjsFixer
    {
        public static string ScoreSubmissionDisableText = "";
        public static (float finalNjs, float finalJD, float originalJD) Fix(float originalNJS, float originalNJO, float bpm)
        {
            // Calculate AutoNjsFixerFinalNJS here since we need NJS for other patches

            //if (!Utils.IsEnabledAutoNjsFixer()) return (0,0); // called before TransitionPatcher so can't use this.
            bpm = bpm == 0 ? TransitionPatcher.bpm : bpm;

            // → (10, 0) for 100Bills Easy Standard (matches menu)


            float originalJD = GetJumpDistance(bpm, originalNJS, originalNJO);

            float desiredNJS = Config.Instance.DesiredNJS > 0 ? Config.Instance.DesiredNJS : originalNJS;
            float finalNJS = desiredNJS; // maybe be different from desiredNJS if maintainVelocity is ON

            float desiredJD = Config.Instance.DesiredJD > 0 ? Config.Instance.DesiredJD : originalNJS;
            float finalJD = desiredJD;


            Plugin.Log.Info($"[AutoNjsFixer] Called... originalNJS: {originalNJS}, originalNJO: {originalNJO} originalJD: {originalJD}, desiredNJS: {desiredNJS}, desiredJD: {desiredJD}");

            bool maintainVelocity = (Config.Instance.AutoNjsFixerMode == Config.AutoNjsFixerModeType.MaintainNoteSpeed) ? true : false; // if false , it's ForceNJS mode

            // 3) Then your maintain‑velocity logic:
            if (maintainVelocity && originalJD > 0.01f)
            {
                finalNJS = originalNJS * (desiredJD / originalJD);
            }
            else if (!maintainVelocity) // forceNJS is ON
            {
                finalNJS = desiredNJS > 0 ? desiredNJS : originalNJS;
            }

            finalNJS = finalNJS > 0 ? finalNJS : originalNJS; // user sets desiredNJS to 0 then use originalNJS
            finalJD = desiredJD > 0 ? desiredJD : originalJD;

            string mode = "Force NJS";
            if (maintainVelocity) mode = "Maintain Velocity";
            Plugin.Log.Info($"[AutoNjsFixer] originalNJS: {originalNJS}, originalJD: {originalJD} => finalNJS: {finalNJS}, finalJD: {finalJD} (mode: {mode})");

            //Activate = true;

            return (finalNJS, finalJD, originalJD);
        }

        // Inputs:
        // bpm = song BPM
        // njs = _noteJumpMovementSpeed (NJS) from the map
        // njo = _noteJumpStartBeatOffset (a.k.a. Note Jump Offset), in beats

        public static float GetJumpDistance(float bpm, float njs, float njo)
        {
            float secPerBeat = 60f / bpm;

            // 1) Start from a baseline half-jump duration in BEATS
            float halfJumpBeats = 4f;

            // 2) Halve it until the spawn distance target isn’t exceeded
            //    (this “18f” is the engine’s spawn-distance target in meters for the *half* jump)
            while (njs * secPerBeat * halfJumpBeats > 18f)
                halfJumpBeats *= 0.5f;

            // 3) Apply NJO (additive, in beats), then clamp to a minimal value
            halfJumpBeats += njo;
            if (halfJumpBeats < 0.25f) halfJumpBeats = 0.25f;

            // 4) Convert to seconds and meters
            //    Full jump time = 2 * halfJumpBeats * secPerBeat
            float jumpDuration = 2f * halfJumpBeats * secPerBeat;     // seconds (spawn → hit line)
            float jumpDistance = njs * jumpDuration;           // meters traveled

            return jumpDistance;
        }

        
    }

}
