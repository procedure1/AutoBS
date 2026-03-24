using AutoBS.Patches;
using HarmonyLib;
using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.Audio;
using static UnityEngine.GraphicsBuffer;

namespace AutoBS // required adding reference to UnityEngine.AudioModule
{
    internal static class LiveAudioRuntimeState
    {
        internal static AudioMixer ActiveMixer;
        internal const string MusicVolumeParam = "MusicVolume";

        internal static int PendingVolumeSteps = 0;
        internal static float LiveVolumeOffsetDb = 0f;

        internal static void ResetLive()
        {
            ActiveMixer = null;
            PendingVolumeSteps = 0;
            LiveVolumeOffsetDb = 0f;
        }
    }
    // This patch listens for the start of the song and then caches the active audio mixer for use in volume adjustments during gameplay.
    // It also adds the LiveGameplayInputListener and LiveVolumeApplier components to the same GameObject if they are not already present.
    [HarmonyPatch(typeof(AudioTimeSyncController), "StartSong")]
    public class AudioTimeSyncController_StartSong_Patch
    {
        static void Postfix(AudioTimeSyncController __instance)
        {
            try
            {
                LiveGameplayRuntimeState.Reset();
                AutoNjsRuntimeState.ResetLive();
                LiveAudioRuntimeState.ResetLive();

                LiveGameplayRuntimeState.SongRunning = true;

                FieldInfo audioSourceField = typeof(AudioTimeSyncController).GetField("_audioSource", BindingFlags.NonPublic | BindingFlags.Instance);
                if (audioSourceField != null)
                {
                    AudioSource audioSource = audioSourceField.GetValue(__instance) as AudioSource;
                    if (audioSource != null && audioSource.outputAudioMixerGroup != null)
                    {
                        LiveAudioRuntimeState.ActiveMixer = audioSource.outputAudioMixerGroup.audioMixer;
                        //Plugin.Log.Info("[LiveVolumeAdjust] Cached active audio mixer.");
                    }
                    else
                    {
                        Plugin.Log.Info("[LiveVolumeAdjust] Could not cache active audio mixer.");
                    }
                }
                else
                {
                    Plugin.Log.Info("[LiveVolumeAdjust] Failed to find _audioSource field.");
                }

                if (__instance.gameObject.GetComponent<LiveGameplayInputListener>() == null)
                    __instance.gameObject.AddComponent<LiveGameplayInputListener>();

                if (__instance.gameObject.GetComponent<LiveVolumeApplier>() == null)
                    __instance.gameObject.AddComponent<LiveVolumeApplier>();

                var hud = __instance.gameObject.GetComponent<LiveAdjustHud>();
                if (hud == null)
                    hud = __instance.gameObject.AddComponent<LiveAdjustHud>();

                hud.UseViewLockedMode = TransitionPatcher.IsGen360 || TransitionPatcher.SelectedSerializedName == "360Degree" || TransitionPatcher.SelectedSerializedName == "90Degree";
                hud.Initialize();
                hud.HideNow();
                LiveAdjustHudRuntime.Instance = hud;

                //Plugin.Log.Info($"[LiveVolumeAdjust] Input listener + volume applier + HUD ready. HUD mode: {(hud.UseViewLockedMode ? "ViewLocked" : "WorldSpace")}");
            }
            catch (Exception ex)
            {
                Plugin.Log.Error($"[LiveVolumeAdjust] StartSong patch failed: {ex}");
            }
        }
    }

    public class LiveVolumeApplier : MonoBehaviour
    {
        private void Update()
        {
            if (!LiveGameplayRuntimeState.SongRunning)
                return;

            if (!Config.Instance.EnablePlugin)
            {
                LiveAudioRuntimeState.PendingVolumeSteps = 0;
                return;
            }
            if (!Config.Instance.EnableLiveVolumeControl)
            {
                LiveAudioRuntimeState.PendingVolumeSteps = 0;
                return;
            }
            if (Config.Instance.LiveVolumeControl == Config.LiveControlModeType.Off)
            {
                LiveAudioRuntimeState.PendingVolumeSteps = 0;
                return;
            }

            int steps = LiveAudioRuntimeState.PendingVolumeSteps;
            if (steps == 0)
                return;

            LiveAudioRuntimeState.PendingVolumeSteps = 0;

            var mixer = LiveAudioRuntimeState.ActiveMixer;
            if (mixer == null)
            {
                Plugin.Log.Info("[LiveVolume] No active mixer.");
                return;
            }

            if (!mixer.GetFloat(LiveAudioRuntimeState.MusicVolumeParam, out float currentDb))
            {
                Plugin.Log.Info("[LiveVolume] Failed to read MusicVolume.");
                return;
            }

            int rounded = Mathf.RoundToInt(currentDb);
            float stepSize = (Mathf.Abs(rounded) % 2 == 1) ? 1f : 2f; // will force odd number to be even by bumping by 1 if odd. and 2 if even to stay even.

            float deltaDb = steps * stepSize;
            float newDb = Mathf.Clamp(rounded + deltaDb, -20f, 6f);

            if (!Mathf.Approximately(currentDb, newDb))
            {
                mixer.SetFloat(LiveAudioRuntimeState.MusicVolumeParam, newDb);

                // Keep cumulative offset from baseline (baseline is 0 dB at song start)
                LiveAudioRuntimeState.LiveVolumeOffsetDb = newDb;

                Plugin.Log.Info($"[LiveVolume] MusicVolume {currentDb:F2} dB -> {newDb:F2} dB");

                LiveAdjustHudRuntime.Instance?.ShowVolumeOffset(LiveAudioRuntimeState.LiveVolumeOffsetDb);
            }
        }
    }

    // reset volume to 0db between songs in TransitionPatcher.
    internal static class LiveAudioHelper
    {
        public static void SetMusicVolumeDb(float db)
        {
            try
            {
                var mixer = LiveAudioRuntimeState.ActiveMixer;
                if (mixer == null)
                {
                    Plugin.LogDebug($"[LiveAudioHelper] No active mixer. Could not set MusicVolume to {db:F2} dB");
                    return;
                }

                mixer.SetFloat(LiveAudioRuntimeState.MusicVolumeParam, db);
                //Plugin.LogDebug($"[LiveAudio] MusicVolume set to {db:F2} dB");
            }
            catch (Exception ex)
            {
                //Plugin.Log.Error($"[LiveAudio] SetMusicVolumeDb failed: {ex}");
            }
        }

        public static void ResetMusicVolumeToZero()
        {
            SetMusicVolumeDb(0f);
        }
    }












    // OLD -------------------------------------------------------------

    // UNUSED but works! but changes volume on all sounds
    /*
    [HarmonyPatch(typeof(AudioManagerSO), "set_mainVolume")]
    public class Volume_Changer
    {
        static void Prefix(ref float value)
        {
            value += Config.Instance.VolumeAdjuster; // changes vol by db
            Plugin.LogDebug($"Adjusted audio volume {Config.Instance.VolumeAdjuster} dB louder.");
        }
    }
    */
    // USE THIS ONE!!!! WORKS!!! but prefer Verbose Volume
    /*
    public class SoundRemover // from sound replacer -- MADE THIS SINCE WAS NOT WORKING ON LEVEL CLEARED so just replaced all the sounds i wanted and can remove soundreplacer.dll
    {
        // Remove Level Cleared or Failed Audio
        //
        [HarmonyPatch(typeof(ResultsViewController), "DidActivate", MethodType.Normal)]
        public class LevelEndPatch
        {
            public static void Postfix(bool addedToHierarchy, bool screenSystemEnabling, ref SongPreviewPlayer ____songPreviewPlayer, ref LevelCompletionResults ____levelCompletionResults)
            {
                if (!Config.Instance.EnablePlugin) return;

                if (!addedToHierarchy)
                    return;

                if (____levelCompletionResults.levelEndStateType == LevelCompletionResults.LevelEndStateType.Cleared ||
                    ____levelCompletionResults.levelEndStateType == LevelCompletionResults.LevelEndStateType.Failed)
                {
                    ____songPreviewPlayer.CrossfadeTo(null, 0f, 0f, 0f, null);
                    Plugin.LogDebug($"Level End Cleared or Success sound removed!");
                }
            }
        }
        */





    // Remove menu music
    /* Works but disabling
    [HarmonyPatch(typeof(SongPreviewPlayer), "Awake")]
    public class MenuMusicPatch
    {
        public static void Postfix(ref AudioClip ____defaultAudioClip)
        {
            ____defaultAudioClip = GetEmptyClip();
        }
    }
    */
    // Remove bad hit sound - not using this.

    //[HarmonyPatch(typeof(NoteCutSoundEffect), "Awake")]
    //public class BadCutSoundPatch
    //{
    //    public static void Prefix(ref AudioClip[] ____badCutSoundEffectAudioClips)
    //    {
    //        ____badCutSoundEffectAudioClips = new AudioClip[] { GetEmptyClip() };
    //    }
    //}

    // Helper method to get an empty AudioClip

    /*
    private static AudioClip GetEmptyClip()
    {
        return AudioClip.Create("Silence", 1, 1, 44100, false);
    }
    */
    //}




    // I used this version.
    // Works! during playback music volume is changed only - so preview of song is not louder
    /*
    [HarmonyPatch(typeof(AudioTimeSyncController), "StartSong")]
    public class AudioTimeSyncController_StartSong_Patch
    {
        static void Postfix(AudioTimeSyncController __instance)
        {
            // Use reflection to get the private _audioSource field
            FieldInfo audioSourceField = typeof(AudioTimeSyncController).GetField("_audioSource", BindingFlags.NonPublic | BindingFlags.Instance);
            if (audioSourceField != null)
            {
                AudioSource audioSource = audioSourceField.GetValue(__instance) as AudioSource;
                if (audioSource != null)
                {
                    if (audioSource.outputAudioMixerGroup.audioMixer.GetFloat("MusicVolume", out var currentVolume))
                    {
                        // Set the music volume based on the base volume and adjuster value
                        float newVolume = currentVolume + Config.Instance.VolumeAdjuster;
                        audioSource.outputAudioMixerGroup.audioMixer.SetFloat("MusicVolume", newVolume);
                        Plugin.LogDebug($"Adjusted music volume {Config.Instance.VolumeAdjuster} dB louder.");
                    }
                }
                else
                {
                    Plugin.LogDebug("Failed to retrieve AudioSource from AudioTimeSyncController.");
                }
            }
            else
            {
                Plugin.LogDebug("Failed to find _audioSource field in AudioTimeSyncController.");
            }
        }
    }
    */
    /*
    // test to see if volume can be changed live during playback. happens after 5s. works!
    using System.Collections;
    using System.Reflection;
    using HarmonyLib;
    using UnityEngine;
    using UnityEngine.Audio;

    [HarmonyPatch(typeof(AudioTimeSyncController), "StartSong")]
    public class AudioTimeSyncController_StartSong_Patch
    {
        static void Postfix(AudioTimeSyncController __instance)
        {
            __instance.StartCoroutine(ChangeVolumeAfterDelay(__instance, 5f, 12f));
        }

        private static IEnumerator ChangeVolumeAfterDelay(AudioTimeSyncController controller, float delay, float dbChange)
        {
            yield return new WaitForSeconds(delay);

            FieldInfo audioSourceField = typeof(AudioTimeSyncController).GetField("_audioSource", BindingFlags.NonPublic | BindingFlags.Instance);
            if (audioSourceField == null)
            {
                Plugin.LogDebug("Failed to find _audioSource field in AudioTimeSyncController.");
                yield break;
            }

            AudioSource audioSource = audioSourceField.GetValue(controller) as AudioSource;
            if (audioSource == null)
            {
                Plugin.LogDebug("Failed to retrieve AudioSource from AudioTimeSyncController.");
                yield break;
            }

            AudioMixerGroup mixerGroup = audioSource.outputAudioMixerGroup;
            if (mixerGroup == null)
            {
                Plugin.LogDebug("AudioSource has no outputAudioMixerGroup.");
                yield break;
            }

            AudioMixer mixer = mixerGroup.audioMixer;
            if (mixer == null)
            {
                Plugin.LogDebug("AudioMixerGroup has no AudioMixer.");
                yield break;
            }

            if (mixer.GetFloat("MusicVolume", out float currentVolume))
            {
                float newVolume = currentVolume + dbChange;

                // Optional clamp, depending on Beat Saber's mixer range
                newVolume = Mathf.Clamp(newVolume, -80f, 20f);

                mixer.SetFloat("MusicVolume", newVolume);
                Plugin.LogDebug($"Changed MusicVolume live after {delay:F1}s. Old: {currentVolume:F2} dB New: {newVolume:F2} dB");
            }
            else
            {
                Plugin.LogDebug("Failed to read MusicVolume from AudioMixer.");
            }
        }
    }
    */

}
