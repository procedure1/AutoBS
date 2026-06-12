using AutoBS.Patches;
using HarmonyLib;
using System;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace AutoBS // required adding reference to UnityEngine.AudioModule
{
    internal static class LiveAudioRuntimeState
    {
        internal static AudioManager ActiveAudioManager;

        internal static int PendingVolumeSteps = 0;
        internal static bool HasStartingMainVolumeDb = false;
        internal static float StartingMainVolumeDb = 0f;
        internal static float LiveVolumeOffsetDb = 0f;
        internal static bool MainVolumeWasAdjusted = false;

        internal static void ResetLive()
        {
            PendingVolumeSteps = 0;
            HasStartingMainVolumeDb = false;
            StartingMainVolumeDb = 0f;
            LiveVolumeOffsetDb = 0f;
            MainVolumeWasAdjusted = false;
        }

        internal static void CaptureAudioManager(AudioManager audioManager)
        {
            if (audioManager != null)
                ActiveAudioManager = audioManager;
        }

        internal static bool TryCaptureAudioManager()
        {
            return ActiveAudioManager != null;
        }

        internal static void CaptureStartingMainVolumeDb(float db)
        {
            if (HasStartingMainVolumeDb)
                return;

            StartingMainVolumeDb = db;
            HasStartingMainVolumeDb = true;
            LiveVolumeOffsetDb = 0f;
        }
    }

    [HarmonyPatch(typeof(AudioManager), "set_mainVolume")]
    public static class AudioManagerSetMainVolumePatch
    {
        public static void Prefix(AudioManager __instance)
        {
            LiveAudioRuntimeState.CaptureAudioManager(__instance);
        }
    }

    public static class AudioManagerConstructorPatch
    {
        static MethodBase TargetMethod()
        {
            return AccessTools.GetDeclaredConstructors(typeof(AudioManager)).FirstOrDefault();
        }

        public static void Postfix(AudioManager __instance)
        {
            LiveAudioRuntimeState.CaptureAudioManager(__instance);
        }
    }

    // This patch listens for the start of the song and prepares live input, live volume, and the in-game HUD.
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

                if (!Config.Instance.EnablePlugin)
                    return;

                LiveGameplayRuntimeState.SongRunning = true;

                if (LiveAudioRuntimeState.TryCaptureAudioManager())
                    LiveAudioRuntimeState.CaptureStartingMainVolumeDb(LiveAudioRuntimeState.ActiveAudioManager.mainVolume);
                else
                    Plugin.Log.Info("[LiveVolumeAdjust] Could not cache AudioManager.");

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
            }
            catch (Exception ex)
            {
                Plugin.Log.Error($"[LiveVolumeAdjust] StartSong patch failed: {ex}");
            }
        }
    }

    public class LiveVolumeApplier : MonoBehaviour
    {
        private const float MinMainVolumeDb = -20f;
        private const float MaxBoostAboveStartDb = 6f;

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

            if (!LiveAudioRuntimeState.TryCaptureAudioManager())
            {
                Plugin.Log.Info("[LiveVolume] No AudioManager.");
                return;
            }

            var audioManager = LiveAudioRuntimeState.ActiveAudioManager;
            float currentDb = audioManager.mainVolume;
            LiveAudioRuntimeState.CaptureStartingMainVolumeDb(currentDb);

            int rounded = Mathf.RoundToInt(currentDb);
            float stepSize = (Mathf.Abs(rounded) % 2 == 1) ? 1f : 2f; // force odd numbers back to even, otherwise move by 2 dB.

            float deltaDb = steps * stepSize;
            float maxMainVolumeDb = LiveAudioRuntimeState.StartingMainVolumeDb + MaxBoostAboveStartDb;
            float newDb = Mathf.Clamp(rounded + deltaDb, MinMainVolumeDb, maxMainVolumeDb);

            if (!Mathf.Approximately(currentDb, newDb))
            {
                LiveAudioHelper.SetMainVolumeDb(newDb);
                LiveAudioRuntimeState.MainVolumeWasAdjusted = true;
                LiveAudioRuntimeState.LiveVolumeOffsetDb = newDb - LiveAudioRuntimeState.StartingMainVolumeDb;

                Plugin.Log.Info($"[LiveVolume] MainVolume {currentDb:F2} dB -> {newDb:F2} dB (offset {LiveAudioRuntimeState.LiveVolumeOffsetDb:+0.##;-0.##;0} dB)");

                LiveAdjustHudRuntime.Instance?.ShowVolumeOffset(LiveAudioRuntimeState.LiveVolumeOffsetDb);
            }
        }
    }

    internal static class LiveAudioHelper
    {
        public static void SetMainVolumeDb(float db)
        {
            try
            {
                if (!LiveAudioRuntimeState.TryCaptureAudioManager())
                {
                    Plugin.LogDebug($"[LiveAudioHelper] No AudioManager. Could not set MainVolume to {db:F2} dB");
                    return;
                }

                LiveAudioRuntimeState.ActiveAudioManager.mainVolume = db;
            }
            catch (Exception ex)
            {
                Plugin.Log.Error($"[LiveAudio] SetMainVolumeDb failed: {ex}");
            }
        }

        public static void RestoreMainVolume()
        {
            if (!LiveAudioRuntimeState.HasStartingMainVolumeDb || !LiveAudioRuntimeState.MainVolumeWasAdjusted)
                return;

            SetMainVolumeDb(LiveAudioRuntimeState.StartingMainVolumeDb);
            LiveAudioRuntimeState.LiveVolumeOffsetDb = 0f;
            LiveAudioRuntimeState.MainVolumeWasAdjusted = false;

            Plugin.Log.Info($"[LiveVolume] Restored MainVolume to starting value {LiveAudioRuntimeState.StartingMainVolumeDb:F2} dB.");
        }

        public static void ResetMusicVolumeToZero()
        {
            RestoreMainVolume();
        }

        public static Action<StandardLevelScenesTransitionSetupDataSO, LevelCompletionResults> WrapLevelEndCallback(
            Action<StandardLevelScenesTransitionSetupDataSO, LevelCompletionResults> callback)
        {
            return (transitionSetupData, levelCompletionResults) =>
            {
                RestoreMainVolume();
                callback?.Invoke(transitionSetupData, levelCompletionResults);
            };
        }

        /*
         * ORIGINAL LIVE VOLUME CONTROL REFERENCE - MUSIC MIXER ONLY
         *
         * This is the approach used before the mainVolume experiment. It cached the active
         * AudioMixer from AudioTimeSyncController._audioSource, adjusted the exposed
         * "MusicVolume" parameter during gameplay, and restored that same music mixer
         * value after gameplay. Sound effects were not included.
         *
         * Required active-code usings:
         * using System.Reflection;
         * using UnityEngine.Audio;
         *
         * internal static class LiveAudioRuntimeState
         * {
         *     internal static AudioMixer ActiveMixer;
         *     internal const string MusicVolumeParam = "MusicVolume";
         *
         *     internal static int PendingVolumeSteps = 0;
         *     internal static bool HasStartingMusicVolumeDb = false;
         *     internal static float StartingMusicVolumeDb = 0f;
         *     internal static float LiveVolumeOffsetDb = 0f;
         *
         *     internal static void ResetLive()
         *     {
         *         ActiveMixer = null;
         *         PendingVolumeSteps = 0;
         *         HasStartingMusicVolumeDb = false;
         *         StartingMusicVolumeDb = 0f;
         *         LiveVolumeOffsetDb = 0f;
         *     }
         *
         *     internal static void CaptureStartingMusicVolumeDb(float db)
         *     {
         *         if (HasStartingMusicVolumeDb)
         *             return;
         *
         *         StartingMusicVolumeDb = db;
         *         HasStartingMusicVolumeDb = true;
         *         LiveVolumeOffsetDb = 0f;
         *     }
         * }
         *
         * [HarmonyPatch(typeof(AudioTimeSyncController), "StartSong")]
         * public class AudioTimeSyncController_StartSong_Patch
         * {
         *     static void Postfix(AudioTimeSyncController __instance)
         *     {
         *         try
         *         {
         *             LiveGameplayRuntimeState.Reset();
         *             AutoNjsRuntimeState.ResetLive();
         *             LiveAudioRuntimeState.ResetLive();
         *
         *             LiveGameplayRuntimeState.SongRunning = true;
         *
         *             FieldInfo audioSourceField = typeof(AudioTimeSyncController).GetField("_audioSource", BindingFlags.NonPublic | BindingFlags.Instance);
         *             if (audioSourceField != null)
         *             {
         *                 AudioSource audioSource = audioSourceField.GetValue(__instance) as AudioSource;
         *                 if (audioSource != null && audioSource.outputAudioMixerGroup != null)
         *                 {
         *                     LiveAudioRuntimeState.ActiveMixer = audioSource.outputAudioMixerGroup.audioMixer;
         *                     if (LiveAudioRuntimeState.ActiveMixer.GetFloat(LiveAudioRuntimeState.MusicVolumeParam, out float startingDb))
         *                         LiveAudioRuntimeState.CaptureStartingMusicVolumeDb(startingDb);
         *                 }
         *                 else
         *                 {
         *                     Plugin.Log.Info("[LiveVolumeAdjust] Could not cache active audio mixer.");
         *                 }
         *             }
         *             else
         *             {
         *                 Plugin.Log.Info("[LiveVolumeAdjust] Failed to find _audioSource field.");
         *             }
         *
         *             if (__instance.gameObject.GetComponent<LiveGameplayInputListener>() == null)
         *                 __instance.gameObject.AddComponent<LiveGameplayInputListener>();
         *
         *             if (__instance.gameObject.GetComponent<LiveVolumeApplier>() == null)
         *                 __instance.gameObject.AddComponent<LiveVolumeApplier>();
         *
         *             var hud = __instance.gameObject.GetComponent<LiveAdjustHud>();
         *             if (hud == null)
         *                 hud = __instance.gameObject.AddComponent<LiveAdjustHud>();
         *
         *             hud.UseViewLockedMode = TransitionPatcher.IsGen360 || TransitionPatcher.SelectedSerializedName == "360Degree" || TransitionPatcher.SelectedSerializedName == "90Degree";
         *             hud.Initialize();
         *             hud.HideNow();
         *             LiveAdjustHudRuntime.Instance = hud;
         *         }
         *         catch (Exception ex)
         *         {
         *             Plugin.Log.Error($"[LiveVolumeAdjust] StartSong patch failed: {ex}");
         *         }
         *     }
         * }
         *
         * public class LiveVolumeApplier : MonoBehaviour
         * {
         *     private void Update()
         *     {
         *         if (!LiveGameplayRuntimeState.SongRunning)
         *             return;
         *
         *         if (!Config.Instance.EnablePlugin ||
         *             !Config.Instance.EnableLiveVolumeControl ||
         *             Config.Instance.LiveVolumeControl == Config.LiveControlModeType.Off)
         *         {
         *             LiveAudioRuntimeState.PendingVolumeSteps = 0;
         *             return;
         *         }
         *
         *         int steps = LiveAudioRuntimeState.PendingVolumeSteps;
         *         if (steps == 0)
         *             return;
         *
         *         LiveAudioRuntimeState.PendingVolumeSteps = 0;
         *
         *         var mixer = LiveAudioRuntimeState.ActiveMixer;
         *         if (mixer == null)
         *         {
         *             Plugin.Log.Info("[LiveVolume] No active mixer.");
         *             return;
         *         }
         *
         *         if (!mixer.GetFloat(LiveAudioRuntimeState.MusicVolumeParam, out float currentDb))
         *         {
         *             Plugin.Log.Info("[LiveVolume] Failed to read MusicVolume.");
         *             return;
         *         }
         *
         *         LiveAudioRuntimeState.CaptureStartingMusicVolumeDb(currentDb);
         *
         *         int rounded = Mathf.RoundToInt(currentDb);
         *         float stepSize = (Mathf.Abs(rounded) % 2 == 1) ? 1f : 2f;
         *         float deltaDb = steps * stepSize;
         *         float newDb = Mathf.Clamp(rounded + deltaDb, -20f, 6f);
         *
         *         if (!Mathf.Approximately(currentDb, newDb))
         *         {
         *             mixer.SetFloat(LiveAudioRuntimeState.MusicVolumeParam, newDb);
         *             LiveAudioRuntimeState.LiveVolumeOffsetDb = newDb;
         *             Plugin.Log.Info($"[LiveVolume] MusicVolume {currentDb:F2} dB -> {newDb:F2} dB");
         *             LiveAdjustHudRuntime.Instance?.ShowVolumeOffset(LiveAudioRuntimeState.LiveVolumeOffsetDb);
         *         }
         *     }
         * }
         *
         * public static void SetMusicVolumeDb(float db)
         * {
         *     try
         *     {
         *         var mixer = LiveAudioRuntimeState.ActiveMixer;
         *         if (mixer == null)
         *         {
         *             Plugin.LogDebug($"[LiveAudioHelper] No active mixer. Could not set MusicVolume to {db:F2} dB");
         *             return;
         *         }
         *
         *         mixer.SetFloat(LiveAudioRuntimeState.MusicVolumeParam, db);
         *         LiveAudioRuntimeState.LiveVolumeOffsetDb = db;
         *     }
         *     catch (Exception ex)
         *     {
         *         Plugin.Log.Error($"[LiveAudio] SetMusicVolumeDb failed: {ex}");
         *     }
         * }
         *
         * public static void ResetMusicVolumeToZero()
         * {
         *     float db = LiveAudioRuntimeState.HasStartingMusicVolumeDb
         *         ? LiveAudioRuntimeState.StartingMusicVolumeDb
         *         : 0f;
         *
         *     SetMusicVolumeDb(db);
         *     LiveAudioRuntimeState.LiveVolumeOffsetDb = 0f;
         * }
         */

        // Remove menu music
        // Works but disabling
        [HarmonyPatch(typeof(SongPreviewPlayer), "Awake")]
        public static class MenuMusicPatch
        {
            private static AudioClip _silentMenuClip;

            public static void Postfix(ref AudioClip ____defaultAudioClip)
            {
                if (Config.Instance.EnablePlugin && Config.Instance.RemoveMenuMusic)
                    ____defaultAudioClip = GetSilentMenuClip();
            }

            internal static AudioClip GetSilentMenuClip()
            {
                return _silentMenuClip ??= AudioClip.Create(
                    "AutoBS_SilentMenuMusic",
                    44100,
                    1,
                    44100,
                    false
                );
            }
        }

        // Remove bad cut sound or Miss sound - not using this.
        [HarmonyPatch(typeof(NoteCutSoundEffect), nameof(NoteCutSoundEffect.Init))]
        public static class NoteCutSoundEffectInitPatch
        {
            public static void Prefix(ref bool ignoreBadCuts)
            {
                if (Config.Instance.EnablePlugin && Config.Instance.RemoveBadCutSound)
                    ignoreBadCuts = true;
            }
        }
    }
}
