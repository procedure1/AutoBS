using HarmonyLib;
using System.Reflection;
using UnityEngine;

namespace AutoBS // required adding reference to UnityEngine.AudioModule
{

    // USE THIS ONE!!!! WORKS!!! but prefer Verbose Volume
    
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
                    Plugin.Log.Info($"Level End Cleared or Success sound removed!");
                }
            }
        }
        
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

        private static AudioClip GetEmptyClip()
        {
            return AudioClip.Create("Silence", 1, 1, 44100, false);
        }
    }
    










    // UNUSED but works! but changes volume on all sounds
    /*
    [HarmonyPatch(typeof(AudioManagerSO), "set_mainVolume")]
    public class Volume_Changer
    {
        static void Prefix(ref float value)
        {
            value += Config.Instance.VolumeAdjuster; // changes vol by db
            Plugin.Log.Info($"Adjusted audio volume {Config.Instance.VolumeAdjuster} dB louder.");
        }
    }
    */

        /*
        // I used this version.
        // Works! during playback music volume is changed only - so preview of song is not louder
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
                            Plugin.Log.Info($"Adjusted music volume {Config.Instance.VolumeAdjuster} dB louder.");
                        }
                    }
                    else
                    {
                        Plugin.Log.Info("Failed to retrieve AudioSource from AudioTimeSyncController.");
                    }
                }
                else
                {
                    Plugin.Log.Info("Failed to find _audioSource field in AudioTimeSyncController.");
                }
            }
        }
        */

    }
