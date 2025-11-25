using AutoBS.Patches;
using BeatSaberMarkupLanguage;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components;
using BeatSaberMarkupLanguage.FloatingScreen;
using BeatSaberMarkupLanguage.Parser;
using BeatSaberMarkupLanguage.ViewControllers;
using HMUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.UI;

namespace AutoBS.UI
{
    internal class GameplaySetupView : BSMLAutomaticViewController//BW added BSMLAutomaticViewController so could use NotifyPropertyChanged() which is needed for interactable bsml
    {
        // --- Safety wrapper for NotifyPropertyChanged to prevent errors when deactivated/reactivated ---
        private bool _isBound;

        protected override void DidActivate(
            bool firstActivation,
            bool addedToHierarchy,
            bool screenSystemEnabling)
        {
            base.DidActivate(firstActivation, addedToHierarchy, screenSystemEnabling);
            _isBound = true;
        }

        protected override void DidDeactivate(
            bool removedFromHierarchy,
            bool screenSystemDisabling)
        {
            _isBound = false;
            base.DidDeactivate(removedFromHierarchy, screenSystemDisabling);
        }

        // Hide the base method with a safe wrapper
        protected new void NotifyPropertyChanged(
            [CallerMemberName] string propertyName = null)
        {
            // Unity "fake null" check
            if (!this || gameObject == null)
            {
                Plugin.Log?.Warn(
                    $"[GameplaySetupView] NotifyPropertyChanged('{propertyName}') on destroyed view – ignoring");
                return;
            }

            // Avoid spamming after we’ve been deactivated
            if (!_isBound || !isActiveAndEnabled)
                return;

            base.NotifyPropertyChanged(propertyName);
        }
        //-----------------------------------------------



        // Needed this since can't determine if MappingExtensions is installed or not until first song is selected by the user otherwise
        public static bool IsMappingExtensionsInstalledNow =>
            AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name == "MappingExtensions");



        [UIValue("EnablePlugin")]
        public bool EnablePlugin
        {
            get => Config.Instance.EnablePlugin;
            set
            {
                Config.Instance.EnablePlugin = value;
                UpdateArcsUI(); 
                UpdateChainsUI(); 
                UpdateWallGeneratorUI(); 
                UpdateLightingGeneratorUI(); 
                UpdateCleanBeatSageUI();

                ActiveEnablePlugin = !value;
                NotifyPropertyChanged();
                NotifyPropertyChanged("ActiveEnablePlugin");
                NotifyPropertyChanged("EnablerEnablePlugin");
                NotifyPropertyChanged("FontColorEnablePlugin");
                NotifyPropertyChanged(nameof(EnablerRotationSettings));
                NotifyPropertyChanged(nameof(FontColorRotationSettings));
                NotifyPropertyChanged(nameof(EnablerLimitRotations360));
                NotifyPropertyChanged(nameof(FontColorLimitRotations360));
                NotifyPropertyChanged(nameof(EnablerArcs));
                NotifyPropertyChanged(nameof(FontColorArcs));
                NotifyPropertyChanged(nameof(EnablerChains));
                NotifyPropertyChanged(nameof(FontColorChains));
                NotifyPropertyChanged(nameof(EnablerWallGenerator));
                NotifyPropertyChanged(nameof(FontColorWallGenerator));
                NotifyPropertyChanged(nameof(EnablerExtWallGenerator));
                NotifyPropertyChanged(nameof(FontColorExtWallGenerator));
                NotifyPropertyChanged(nameof(EnablerStandard));
                NotifyPropertyChanged(nameof(FontColorStandard));
                NotifyPropertyChanged(nameof(EnablerDistant));
                NotifyPropertyChanged(nameof(FontColorDistant));
                NotifyPropertyChanged(nameof(EnablerColumns));
                NotifyPropertyChanged(nameof(FontColorColumns));
                NotifyPropertyChanged(nameof(EnablerRows));
                NotifyPropertyChanged(nameof(FontColorRows));
                NotifyPropertyChanged(nameof(EnablerTunnels));
                NotifyPropertyChanged(nameof(FontColorTunnels));
                NotifyPropertyChanged(nameof(EnablerGrids));
                NotifyPropertyChanged(nameof(FontColorGrids));
                NotifyPropertyChanged(nameof(EnablerPanes));
                NotifyPropertyChanged(nameof(FontColorPanes));
                NotifyPropertyChanged(nameof(EnablerParticles));
                NotifyPropertyChanged(nameof(FontColorParticles));
                NotifyPropertyChanged(nameof(EnablerFloors));
                NotifyPropertyChanged(nameof(FontColorFloors));
                NotifyPropertyChanged(nameof(EnablerCleanBeatSage));
                NotifyPropertyChanged(nameof(FontColorCleanBeatSage));
                NotifyPropertyChanged(nameof(EnablerLightingGenerator));
                NotifyPropertyChanged(nameof(FontColorLightingGenerator));
                NotifyPropertyChanged(nameof(EnablerLightAutoMapper));
                NotifyPropertyChanged(nameof(FontColorLightAutoMapper));
                NotifyPropertyChanged(nameof(Enabler90));
                NotifyPropertyChanged(nameof(FontColor90));
            }
        }

        // Architect arcs and chains

        [UIValue("EnableArcsGenerator")]
        public bool EnableArcsGenerator
        {
            get => Config.Instance.EnablePlugin && (Config.Instance.EnableArcsGen360 || Config.Instance.EnableArcsNonGen360 || Config.Instance.EnableArcsStandard);
        }

        [UIValue("EnableArcsGen360")]
        public bool EnableArcsGen360
        {
            get => Config.Instance.EnableArcsGen360;
            set
            {
                Config.Instance.EnableArcsGen360 = value;
                UpdateArcsUI();
            }
        }
        [UIValue("EnableArcsNonGen360")]
        public bool EnableArcsNonGen360
        {
            get => Config.Instance.EnableArcsNonGen360;
            set
            {
                Config.Instance.EnableArcsNonGen360 = value;
                UpdateArcsUI();
            }
        }
        [UIValue("EnableArcsStandard")]
        public bool EnableArcsStandard
        {
            get => Config.Instance.EnableArcsStandard;
            set
            {
                Config.Instance.EnableArcsStandard = value;
                UpdateArcsUI();
            }
        }


        [UIValue("EnableChainsGenerator")]
        public bool EnableChainsGenerator
        {
            get => Config.Instance.EnablePlugin && (Config.Instance.EnableChainsGen360 || Config.Instance.EnableChainsNonGen360 || Config.Instance.EnableChainsStandard);
        }

        [UIValue("EnableChainsGen360")]
        public bool EnableChainsGen360
        {
            get => Config.Instance.EnableChainsGen360;
            set
            {
                Config.Instance.EnableChainsGen360 = value;
                UpdateChainsUI();
            }
        }
        [UIValue("EnableChainsNonGen360")]
        public bool EnableChainsNonGen360
        {
            get => Config.Instance.EnableChainsNonGen360;
            set
            {
                Config.Instance.EnableChainsNonGen360 = value;
                UpdateChainsUI();
            }
        }
        [UIValue("EnableChainsStandard")]
        public bool EnableChainsStandard
        {
            get => Config.Instance.EnableChainsStandard;
            set
            {
                Config.Instance.EnableChainsStandard = value;
                UpdateChainsUI();
            }
        }

        // Walls

        [UIValue("EnableWallGenerator")]
        public bool EnableWallGenerator
        {
            get => Config.Instance.EnableWallsGen360 || Config.Instance.EnableWallsNonGen360 || Config.Instance.EnableWallsStandard;
        }

        [UIValue("EnableWallsGen360")]
        public bool EnableWallsGen360
        {
            get => Config.Instance.EnableWallsGen360;
            set
            {
                Config.Instance.EnableWallsGen360 = value;
                UpdateWallGeneratorUI();
            }
        }
        [UIValue("EnableWallsNonGen360")]
        public bool EnableWallsNonGen360
        {
            get => Config.Instance.EnableWallsNonGen360;
            set
            {
                Config.Instance.EnableWallsNonGen360 = value;
                UpdateWallGeneratorUI();
            }
        }
        [UIValue("EnableWallsStandard")]
        public bool EnableWallsStandard
        {
            get => Config.Instance.EnableWallsStandard;
            set
            {
                Config.Instance.EnableWallsStandard = value;
                UpdateWallGeneratorUI();
            }
        }

        // Lighting

        [UIValue("EnableLightingGenerator")] // generator and manipulator
        public bool EnableLightingGenerator
        {
            get => Config.Instance.EnableLightingGen360 || Config.Instance.EnableLightingNonGen360 || Config.Instance.EnableLightingStandard;
        }

        [UIValue("EnableLightingGen360")]
        public bool EnableLightingGen360
        {
            get => Config.Instance.EnableLightingGen360;
            set
            {
                Config.Instance.EnableLightingGen360 = value;
                UpdateLightingGeneratorUI();
            }
        }
        [UIValue("EnableLightingNonGen360")]
        public bool EnableLightingNonGen360
        {
            get => Config.Instance.EnableLightingNonGen360;
            set
            {
                Config.Instance.EnableLightingNonGen360 = value;
                UpdateLightingGeneratorUI();
            }
        }
        [UIValue("EnableLightingStandard")]
        public bool EnableLightingStandard
        {
            get => Config.Instance.EnableLightingStandard;
            set
            {
                Config.Instance.EnableLightingStandard = value;
                UpdateLightingGeneratorUI();
            }
        }


        [UIValue("EnableAutoNjsFixer")]
        public bool EnableAutoNjsFixer
        {
            get => Config.Instance.EnablePlugin && (Config.Instance.EnableAutoNjsFixerGen360 || Config.Instance.EnableAutoNjsFixerNonGen360 || Config.Instance.EnableAutoNjsFixerStandard);
        }

        [UIValue("EnableAutoNjsFixerGen360")]
        public bool EnableAutoNjsFixerGen360
        {
            get => Config.Instance.EnableAutoNjsFixerGen360;
            set
            {
                Config.Instance.EnableAutoNjsFixerGen360 = value;
                UpdateAutoNjsFixerUI();
            }
        }
        [UIValue("EnableAutoNjsFixerNonGen360")]
        public bool EnableAutoNjsFixerNonGen360
        {
            get => Config.Instance.EnableAutoNjsFixerNonGen360;
            set
            {
                Config.Instance.EnableAutoNjsFixerNonGen360 = value;
                UpdateAutoNjsFixerUI();
            }
        }
        [UIValue("EnableAutoNjsFixerStandard")]
        public bool EnableAutoNjsFixerStandard
        {
            get => Config.Instance.EnableAutoNjsFixerStandard;
            set
            {
                Config.Instance.EnableAutoNjsFixerStandard = value;
                UpdateAutoNjsFixerUI();
            }
        }

        [UIValue("EnabledInPractice")]
        public bool EnabledInPractice
        {
            get => Config.Instance.EnabledInPractice;
            set => Config.Instance.EnabledInPractice = value;
        }
        [UIValue("DesiredNJS")]
        public float DesiredNJS
        {
            get => Config.Instance.DesiredNJS;
            set => Config.Instance.DesiredNJS = value;
        }
        [UIValue("DesiredJD")]
        public float DesiredJD
        {
            get => Config.Instance.DesiredJD;
            set => Config.Instance.DesiredJD = value;
        }

        /*
        [UIValue("OriginalNJS")]
        private string _originalNjs = "—";

        [UIValue("OriginalJD")]
        private string _originalJd  = "—";

        // Called after BSML is parsed; good place for initial fill
        [UIAction("#post-parse")]
        private void PostParse()
        {
            RefreshOriginals();
        }

        // Call this whenever TransitionPatcher values may have changed
        private void RefreshOriginals()
        {
            _originalNjs = $"{TransitionPatcher.NoteJumpMovementSpeed:F1}";
            _originalJd  = $"{TransitionPatcher.JumpDistance:F1}";

            NotifyPropertyChanged(nameof(_originalNjs));
            NotifyPropertyChanged(nameof(_originalJd));
        }

        private void OnDestroy()
        {
            OriginalStatsHub.OriginalsChanged -= OnOriginalsChanged;
        }

        private void OnOriginalsChanged(float njs, float jd)
        {
            _originalNjs = $"{njs:F1}";
            _originalJd = $"{jd:F1}";
            NotifyPropertyChanged(nameof(_originalNjs));
            NotifyPropertyChanged(nameof(_originalJd));
        }
        */
        /*
        [UIValue("EnableFeaturesForNonGen360Maps")]
        public bool EnableFeaturesForNonGen360Maps
        {
            get => Config.Instance.EnableFeaturesForNonGen360Maps;
            set => Config.Instance.EnableFeaturesForNonGen360Maps = value;
        }
        [UIValue("EnableFeaturesForStandardMaps")]
        public bool EnableFeaturesForStandardMaps
        {
            get => Config.Instance.EnableFeaturesForStandardMaps;
            set => Config.Instance.EnableFeaturesForStandardMaps = value;
        }
        */


        // Volume
        /*
        //private float _baseMainVolume;
        private float _baseMusicVolume;

        [UIAction("volFormat")]
        private string VolFormat(float value) => $"{value} dB";

        [UIValue("VolumeAdjuster")]
        public float VolumeAdjuster
        {
            get => Config.Instance.VolumeAdjuster;
            set
            {
                Config.Instance.VolumeAdjuster = value;
                ApplyVolumeChange(); // Apply the volume change immediately
                NotifyPropertyChanged();
            }
        }
        */
        /*
        private void Start()
        {
            var audioTimeSyncController = Resources.FindObjectsOfTypeAll<AudioTimeSyncController>().FirstOrDefault();
            if (audioTimeSyncController != null)
            {
                FieldInfo audioSourceField = typeof(AudioTimeSyncController).GetField("_audioSource", BindingFlags.NonPublic | BindingFlags.Instance);
                if (audioSourceField != null)
                {
                    AudioSource audioSource = audioSourceField.GetValue(audioTimeSyncController) as AudioSource;
                    if (audioSource != null)
                    {
                        if (audioSource.outputAudioMixerGroup.audioMixer.GetFloat("MusicVolume", out var currentMusicVolume))
                        {
                            _baseMusicVolume = currentMusicVolume;
                        }
                    }
                }
            }
        }
        */
        /*
        private void ApplyVolumeChange()
        {
            // Get the instance of AudioTimeSyncController
            var audioTimeSyncController = Resources.FindObjectsOfTypeAll<AudioTimeSyncController>().FirstOrDefault();
            if (audioTimeSyncController != null)
            {
                // Use reflection to get the private _audioSource field
                FieldInfo audioSourceField = typeof(AudioTimeSyncController).GetField("_audioSource", BindingFlags.NonPublic | BindingFlags.Instance);
                if (audioSourceField != null)
                {
                    AudioSource audioSource = audioSourceField.GetValue(audioTimeSyncController) as AudioSource;
                    if (audioSource != null)
                    {
                        // Set the music volume to the base volume plus the adjuster value
                        audioSource.outputAudioMixerGroup.audioMixer.SetFloat("MusicVolume", _baseMusicVolume + Config.Instance.VolumeAdjuster);
                    }
                }
            }
        }
        */

        // DIDN'T use this version below in the end...--- works for raising game main volume
        /*
        [UIAction("volFormat")]
        private string VolFormat(float value) => $"{value} dB";

        private void Start()
        {
            // Initialize _baseVolume with the current main volume when the game starts
            var audioManagerSO = Resources.FindObjectsOfTypeAll<AudioManagerSO>().FirstOrDefault();
            if (audioManagerSO != null)
            {
                PropertyInfo mainVolumeProperty = typeof(AudioManagerSO).GetProperty("mainVolume", BindingFlags.Public | BindingFlags.Instance);
                if (mainVolumeProperty != null)
                {
                    _baseMainVolume = (float)mainVolumeProperty.GetValue(audioManagerSO);
                }
            }
        }
        private void ApplyVolumeChange()
        {
            // Get the instance of AudioManagerSO
            var audioManagerSO = Resources.FindObjectsOfTypeAll<AudioManagerSO>().FirstOrDefault();
            if (audioManagerSO != null)
            {
                // Access the mainVolume property and apply the new volume
                PropertyInfo mainVolumeProperty = typeof(AudioManagerSO).GetProperty("mainVolume", BindingFlags.Public | BindingFlags.Instance);
                if (mainVolumeProperty != null)
                {
                    // Set the volume to the base volume plus the adjuster value
                    mainVolumeProperty.SetValue(audioManagerSO, _baseMainVolume + Config.Instance.VolumeAdjuster);
                }
            }
        }
        */

        // 360

        [UIValue("Enable360fyer")]
        public bool Enable360fyer
        {
            get => Config.Instance.Enable360fyer;
            set
            {
                if (Config.Instance.Enable360fyer == value) return;
                Config.Instance.Enable360fyer = value;
                UpdateRotationSettingsUI();   // ← this forces the dim/undim refresh
            }
        }


        [UIValue("Wireless360")]
        public bool Wireless360
        {
            get => Config.Instance.Wireless360;
            set
            {
                Config.Instance.Wireless360 = value;

                // Notify that the dependent properties changed
                NotifyPropertyChanged(nameof(EnablerLimitRotations360));
                NotifyPropertyChanged(nameof(FontColorLimitRotations360));
                NotifyPropertyChanged(nameof(FontColorWireless360));
                NotifyPropertyChanged(); // for Wireless360 itself
            }
        }


        [UIValue("LimitRotations360")]
        public float LimitRotations360
        {
            get => Config.Instance.LimitRotations360;
            set => Config.Instance.LimitRotations360 = value;
        }

        // Rotations

        [UIValue("RotationSpeedMultiplier")]
        public float RotationSpeedMultiplier
        {
            get => Config.Instance.RotationSpeedMultiplier;
            set => Config.Instance.RotationSpeedMultiplier = value;
        }
        [UIValue("AddExtraRotation")]
        public bool AddExtraRotation
        {
            get => Config.Instance.AddExtraRotation;
            set => Config.Instance.AddExtraRotation = value;
        }
        /*
        [UIValue("AddExtraRotationV2")]
        public bool AddExtraRotationV2
        {
            get => Config.Instance.AddExtraRotationV2;
            set => Config.Instance.AddExtraRotationV2 = value;
        }
        */
        [UIValue("MinRotationSize")]
        public float MinRotationSize
        {
            get => Config.Instance.MinRotationSize;
            set => Config.Instance.MinRotationSize = value;
        }
        [UIValue("MaxRotationSize")]
        public float MaxRotationSize
        {
            get => Config.Instance.MaxRotationSize;
            set => Config.Instance.MaxRotationSize = value;
        }
        [UIValue("FOV")]
        public float FOV
        {
            get => Config.Instance.FOV;
            set => Config.Instance.FOV = value;
        }
        [UIValue("TimeWindow")]
        public float TimeWindow
        {
            get => Config.Instance.TimeWindow;
            set => Config.Instance.TimeWindow = value;
        }
        
        [UIValue("VisionBlockingWallRemovalMult")]
        public float VisionBlockingWallRemovalMult
        {
            get => Config.Instance.VisionBlockingWallRemovalMult;
            set => Config.Instance.VisionBlockingWallRemovalMult = value;
        }
        [UIValue("MinDistanceBetweenNotesAndWalls")]
        public float MinDistanceBetweenNotesAndWalls
        {
            get => Config.Instance.MinDistanceBetweenNotesAndWalls;
            set => Config.Instance.MinDistanceBetweenNotesAndWalls = value;
        }
        
        /*
        [UIValue("RotationGroupSize")]
        public float RotationGroupSize
        {
            get => Config.Instance.RotationGroupSize;
            set => Config.Instance.RotationGroupSize = value;
        }
        */

        //Arcs & Chains

        /*
        [UIValue("EnableArcs")]
        public bool EnableArcs
        {
            get => Config.Instance.EnableArcs;
            set
            {
                Config.Instance.EnableArcs = value;
                EnablerArcs = value;
                if (EnablerArcs) { FontColorArcs = OnColor; EnablerArcs = true; } else { FontColorArcs = OffColor; EnablerArcs = false; };
                NotifyPropertyChanged();
            }
        }
        */
        /*
        [UIValue("ArcFixFull")]
        public bool ArcFix
        {
            get => Config.Instance.ArcFixFull;
            set => Config.Instance.ArcFixFull = value;
        }
        */


        // supply list of display names for the dropdown
        public List<object> ArcRotationChoices { get; set; } = new List<object>
        {
            "Force Zero",      // Prevents any rotations in arcs
            "Net Zero",        // Rotations allowed, but arc ends where it starts
            "No Restriction"   // Arcs can have rotations
        };

        [UIValue("ForceNaturalArcs")]
        public bool ForceNaturalArcs
        {
            get => Config.Instance.ForceNaturalArcs;
            set => Config.Instance.ForceNaturalArcs = value;
        }
        [UIValue("PreferredArcCountPerMin")]
        public float PreferredArcCountPerMin
        {
            get => Config.Instance.PreferredArcCountPerMin;
            set => Config.Instance.PreferredArcCountPerMin = value;
        }
        /*
        [UIValue("ControlPointLength")]
        public float ControlPointLength
        {
            get => Config.Instance.ControlPointLength;
            set => Config.Instance.ControlPointLength = value;
        }
        */
        [UIValue("MinArcDuration")]
        public float MinArcDuration
        {
            get => Config.Instance.MinArcDuration;
            set => Config.Instance.MinArcDuration = value;
        }
        [UIValue("MaxArcDuration")]
        public float MaxArcDuration
        {
            get => Config.Instance.MaxArcDuration;
            set => Config.Instance.MaxArcDuration = value;
        }

        // Chains
        /*
        [UIValue("EnableChains")]
        public bool EnableChains
        {
            get => Config.Instance.EnableChains;
            set
            {
                Config.Instance.EnableChains = value;
                EnablerChains = value;
                if (EnablerChains) { FontColorChains = OnColor; EnablerChains = true; } else { FontColorChains = OffColor; EnablerChains = false; };
                NotifyPropertyChanged();
            }
        }
        */
        [UIValue("PreferredChainCountPerMin")]
        public float PreferredChainCountPerMin
        {
            get => Config.Instance.PreferredChainCountPerMin;
            set => Config.Instance.PreferredChainCountPerMin = value;
        }
        [UIValue("ForceMoreChains")]
        public bool ForceMoreChains
        {
            get => Config.Instance.ForceMoreChains;
            set => Config.Instance.ForceMoreChains = value;
        }
        [UIValue("ChainTimeBumper")]
        public float ChainTimeBumper
        {
            get => Config.Instance.ChainTimeBumper;
            set => Config.Instance.ChainTimeBumper = value;
        }
        [UIValue("EnableLongChains")]
        public bool EnableLongChains
        {
            get => Config.Instance.EnableLongChains;
            set => Config.Instance.EnableLongChains = value;
        }
        [UIValue("LongChainMaxDuration")]
        public float LongChainMaxDuration
        {
            get => Config.Instance.LongChainMaxDuration;
            set => Config.Instance.LongChainMaxDuration = value;
        }
        /*

        /// <summary>
        /// Updates all the UI settings to enable/disable and dim/undim the text.
        /// </summary>
        private void UpdateArchitectUI()
        {
            NotifyPropertyChanged(nameof(EnablerArcs));

            // Ensure all dependent UI elements update
            NotifyPropertyChanged(nameof(EnablerArcs));
            NotifyPropertyChanged(nameof(FontColorArcs));

            NotifyPropertyChanged(nameof(EnablerChains));
            NotifyPropertyChanged(nameof(FontColorChains));
        }
        */
        /*
        [UIValue("PauseDetection")]
        public bool PauseDetection
        {
            get => Config.Instance.PauseDetection;
            set => Config.Instance.PauseDetection = value;
        }
        [UIValue("AlterNotes")]
        public bool AlterNotes
        {
            get => Config.Instance.AlterNotes;
            set => Config.Instance.AlterNotes = value;
        }
        */


        // Beatsage

        [UIValue("EnableCleanBeatSage")]
        public bool EnableCleanBeatSage
        {
            get => Config.Instance.EnableCleanBeatSage;
            set
            {
                Config.Instance.EnableCleanBeatSage = value;
                UpdateCleanBeatSageUI();
                NotifyPropertyChanged(); // for CleanBeatSage itself
            }
        }

        [UIValue("MaxCrouchWallDuration")]
        public float MaxCrouchWallDuration
        {
            get => Config.Instance.MaxCrouchWallDuration;
            set => Config.Instance.MaxCrouchWallDuration = value;
        }
        [UIValue("StrayNoteCleanerOffset")]
        public float StrayNoteCleanerOffset
        {
            get => Config.Instance.StrayNoteCleanerOffset;
            set => Config.Instance.StrayNoteCleanerOffset = value;
        }

        // Walls

        [UIValue("AllowCrouchWalls")]
        public bool AllowCrouchWalls
        {
            get => Config.Instance.AllowCrouchWalls;
            set => Config.Instance.AllowCrouchWalls = value;
        }
        [UIValue("AllowLeanWalls")]
        public bool AllowLeanWalls
        {
            get => Config.Instance.AllowLeanWalls;
            set => Config.Instance.AllowLeanWalls = value;
        }

        // Generated Walls

        [UIValue("EnableStandardWalls")]
        public bool EnableStandardWalls
        {
            get => Config.Instance.EnableStandardWalls;
            set
            {
                Config.Instance.EnableStandardWalls = value;
                UpdateWallGeneratorUI();
                NotifyPropertyChanged();
            }
        }

        [UIValue("EnableBigWalls")]
        public bool EnableBigWalls
        {
            get => Config.Instance.EnableBigWalls;
            set
            {
                Config.Instance.EnableBigWalls = value;
                UpdateWallGeneratorUI();
                NotifyPropertyChanged();
            }
        }
        [UIValue("StandardWallsMultiplier")]
        public float StandardWallsMultiplier
        {
            get => Config.Instance.StandardWallsMultiplier;
            set => Config.Instance.StandardWallsMultiplier = value;
        }
        [UIValue("StandardWallsMinDistance")]
        public float StandardWallsMinDistance
        {
            get => Config.Instance.StandardWallsMinDistance;
            set => Config.Instance.StandardWallsMinDistance = value;
        }

        [UIValue("EnableExtensionMappingWallsGenerator")]
        public bool EnableExtensionMappingWallsGenerator
        {
            get => Config.Instance.EnableMappingExtensionsWallsGenerator;
            set
            {
                Config.Instance.EnableMappingExtensionsWallsGenerator = value;
                UpdateWallGeneratorUI();
                NotifyPropertyChanged();
            }
        }
        [UIValue("EnableDistantExtensionWalls")]
        public bool EnableDistantExtensionWalls
        {
            get => Config.Instance.EnableDistantExtensionWalls;
            set
            {
                Config.Instance.EnableDistantExtensionWalls = value;
                UpdateWallGeneratorUI();
                NotifyPropertyChanged();
            }
        }
        [UIValue("DistantExtensionWallsMultiplier")]
        public float DistantExtensionWallsMultiplier
        {
            get => Config.Instance.DistantExtensionWallsMultiplier;
            set => Config.Instance.DistantExtensionWallsMultiplier = value;
        }


        [UIValue("EnableColumnWalls")]
        public bool EnableColumnWalls
        {
            get => Config.Instance.EnableColumnWalls;
            set
            {
                Config.Instance.EnableColumnWalls = value;
                UpdateWallGeneratorUI();
                NotifyPropertyChanged();
            }
        }
        [UIValue("ColumnWallsMultiplier")]
        public float ColumnWallsMultiplier
        {
            get => Config.Instance.ColumnWallsMultiplier;
            set => Config.Instance.ColumnWallsMultiplier = value;
        }
        [UIValue("ColumnWallsMinDistance")]
        public float ColumnWallsMinDistance
        {
            get => Config.Instance.ColumnWallsMinDistance;
            set => Config.Instance.ColumnWallsMinDistance = value;
        }


        [UIValue("EnableRowWalls")]
        public bool EnableRowWalls
        {
            get => Config.Instance.EnableRowWalls;
            set
            {
                Config.Instance.EnableRowWalls = value;
                UpdateWallGeneratorUI();
                NotifyPropertyChanged();
            }
        }
        [UIValue("RowWallsMultiplier")]
        public float RowWallsMultiplier
        {
            get => Config.Instance.RowWallsMultiplier;
            set => Config.Instance.RowWallsMultiplier = value;
        }
        [UIValue("RowWallsMinDistance")]
        public float RowWallsMinDistance
        {
            get => Config.Instance.RowWallsMinDistance;
            set => Config.Instance.RowWallsMinDistance = value;
        }


        [UIValue("EnableTunnelWalls")]
        public bool EnableTunnelWalls
        {
            get => Config.Instance.EnableTunnelWalls;
            set
            {
                Config.Instance.EnableTunnelWalls = value;
                UpdateWallGeneratorUI();
                NotifyPropertyChanged();
            }
        }
        [UIValue("TunnelWallsMultiplier")]
        public float TunnelWallsMultiplier
        {
            get => Config.Instance.TunnelWallsMultiplier;
            set => Config.Instance.TunnelWallsMultiplier = value;
        }
        [UIValue("TunnelWallsMinDistance")]
        public float TunnelWallsMinDistance
        {
            get => Config.Instance.TunnelWallsMinDistance;
            set => Config.Instance.TunnelWallsMinDistance = value;
        }


        [UIValue("EnableGridWalls")]
        public bool EnableGridWalls
        {
            get => Config.Instance.EnableGridWalls;
            set
            {
                Config.Instance.EnableGridWalls = value;
                UpdateWallGeneratorUI();
                NotifyPropertyChanged();
            }
        }
        [UIValue("GridWallsMultiplier")]
        public float GridWallsMultiplier
        {
            get => Config.Instance.GridWallsMultiplier;
            set => Config.Instance.GridWallsMultiplier = value;
        }
        [UIValue("GridWallsMinDistance")]
        public float GridWallsMinDistance
        {
            get => Config.Instance.GridWallsMinDistance;
            set => Config.Instance.GridWallsMinDistance = value;
        }


        [UIValue("EnableWindowPaneWalls")]
        public bool EnableWindowPaneWalls
        {
            get => Config.Instance.EnableWindowPaneWalls;
            set
            {
                Config.Instance.EnableWindowPaneWalls = value;
                UpdateWallGeneratorUI();
                NotifyPropertyChanged();
            }
        }
        /*
        [UIValue("WindowPaneWallsSize")]
        public float WindowPaneWallsSize
        {
            get => Config.Instance.WindowPaneWallsSize;
            set => Config.Instance.WindowPaneWallsSize = value;
        }
        [UIValue("WindowPaneWallsHeight")]
        public float WindowPaneWallsHeight
        {
            get => Config.Instance.WindowPaneWallsHeight;
            set => Config.Instance.WindowPaneWallsHeight = value;
        }
        */
        [UIValue("WindowPaneWallsMultiplier")]
        public float WindowPaneWallsMultiplier
        {
            get => Config.Instance.WindowPaneWallsMultiplier;
            set => Config.Instance.WindowPaneWallsMultiplier = value;
        }
        [UIValue("WindowPaneWallsMinDistance")]
        public float WindowPaneWallsMinDistance
        {
            get => Config.Instance.WindowPaneWallsMinDistance;
            set => Config.Instance.WindowPaneWallsMinDistance = value;
        }



        [UIValue("EnableParticleWalls")]
        public bool EnableParticleWalls
        {
            get => Config.Instance.EnableParticleWalls;
            set
            {
                Config.Instance.EnableParticleWalls = value;
                UpdateWallGeneratorUI();
                NotifyPropertyChanged();
            }
        }
        [UIValue("EnableLargeParticleWalls")]
        public bool EnableLargeParticleWalls
        {
            get => Config.Instance.EnableLargeParticleWalls;
            set => Config.Instance.EnableLargeParticleWalls = value;
        }
        [UIValue("ParticleWallsMultiplier")]
        public float ParticleWallsMultiplier
        {
            get => Config.Instance.ParticleWallsMultiplier;
            set => Config.Instance.ParticleWallsMultiplier = value;
        }
        [UIValue("ParticleWallsBatchSize")]
        public float ParticleWallsBatchSize
        {
            get => Config.Instance.ParticleWallsBatchSize;
            set => Config.Instance.ParticleWallsBatchSize = value;
        }
        [UIValue("ParticleWallsMinDistance")]
        public float ParticleWallsMinDistance
        {
            get => Config.Instance.ParticleWallsMinDistance;
            set => Config.Instance.ParticleWallsMinDistance = value;
        }


        [UIValue("EnableFloorWalls")]
        public bool EnableFloorWalls
        {
            get => Config.Instance.EnableFloorWalls;
            set
            {
                Config.Instance.EnableFloorWalls = value;
                EnablerPanes = value;
                if (EnablerFloors) { FontColorFloors = OnColor; EnablerFloors = true; } else { FontColorFloors = OffColor; EnablerFloors = false; };
                NotifyPropertyChanged();
                UpdateWallGeneratorUI();     // recalc all wall sub-groups consistently
                NotifyPropertyChanged();
            }
        }
        [UIValue("FloorWallsMultiplier")]
        public float FloorWallsMultiplier
        {
            get => Config.Instance.FloorWallsMultiplier;
            set => Config.Instance.FloorWallsMultiplier = value;
        }
        [UIValue("FloorWallsBatchSize")]
        public float FloorWallsBatchSize
        {
            get => Config.Instance.FloorWallsBatchSize;
            set => Config.Instance.FloorWallsBatchSize = value;
        }
        [UIValue("FloorWallsMinDistance")]
        public float FloorWallsMinDistance
        {
            get => Config.Instance.FloorWallsMinDistance;
            set => Config.Instance.FloorWallsMinDistance = value;
        }


        [UIValue("MaxWaitTime")]
        public float MaxWaitTime
        {
            get => Config.Instance.MaxWaitTime;
            set => Config.Instance.MaxWaitTime = value;
        }

        private void UpdateRotationSettingsUI()
        {
            NotifyPropertyChanged(nameof(Enable360fyer));
            NotifyPropertyChanged(nameof(EnablerRotationSettings));
            NotifyPropertyChanged(nameof(FontColorRotationSettings));
        }


        private void UpdateArcsUI()
        {
            bool isEnabled = EnableArcsGenerator;

            EnablerArcs = isEnabled;
            FontColorArcs = isEnabled ? OnColor : OffColor;

            NotifyPropertyChanged(nameof(EnableArcsGenerator));
            NotifyPropertyChanged(nameof(EnablerArcs));
            NotifyPropertyChanged(nameof(FontColorArcs));
        }

        private void UpdateChainsUI()
        {
            bool isEnabled = EnableChainsGenerator;

            EnablerChains = isEnabled;
            FontColorChains = isEnabled ? OnColor : OffColor;

            NotifyPropertyChanged(nameof(EnableChainsGenerator));
            NotifyPropertyChanged(nameof(EnablerChains));
            NotifyPropertyChanged(nameof(FontColorChains));
        }
        /// <summary>
        /// Updates all the UI settings to enable/disable and dim/undim the text.
        /// </summary>
        private void UpdateWallGeneratorUI()
        {
            bool isEnabled = EnableWallGenerator;

            EnablerWallGenerator = Config.Instance.EnablePlugin && isEnabled;
            FontColorWallGenerator = EnablerWallGenerator ? OnColor : OffColor;

            EnablerExtWallGenerator = EnablerWallGenerator && IsMappingExtensionsInstalledNow && Config.Instance.EnableMappingExtensionsWallsGenerator;
            FontColorExtWallGenerator = EnablerExtWallGenerator ? OnColor : OffColor;

            EnablerStandard = EnablerWallGenerator && (Config.Instance.EnableStandardWalls || Config.Instance.EnableBigWalls);
            FontColorStandard = EnablerStandard ? OnColor : OffColor;

            EnablerDistant = EnablerExtWallGenerator && Config.Instance.EnableDistantExtensionWalls;
            FontColorDistant = EnablerDistant ? OnColor : OffColor;

            EnablerColumns = EnablerExtWallGenerator && Config.Instance.EnableColumnWalls;
            FontColorColumns = EnablerColumns ? OnColor : OffColor;

            EnablerRows = EnablerExtWallGenerator && Config.Instance.EnableRowWalls;
            FontColorRows = EnablerRows ? OnColor : OffColor;

            EnablerTunnels = EnablerExtWallGenerator && Config.Instance.EnableTunnelWalls;
            FontColorTunnels = EnablerTunnels ? OnColor : OffColor;

            EnablerGrids = EnablerExtWallGenerator && Config.Instance.EnableGridWalls;
            FontColorGrids = EnablerGrids ? OnColor : OffColor;

            EnablerPanes = EnablerExtWallGenerator && Config.Instance.EnableWindowPaneWalls;
            FontColorPanes = EnablerPanes ? OnColor : OffColor;

            EnablerParticles = EnablerExtWallGenerator && Config.Instance.EnableParticleWalls;
            FontColorParticles = EnablerParticles ? OnColor : OffColor;

            EnablerFloors = EnablerExtWallGenerator && Config.Instance.EnableFloorWalls;
            FontColorFloors = EnablerFloors ? OnColor : OffColor;

            NotifyPropertyChanged(nameof(EnableWallGenerator));
            NotifyPropertyChanged(nameof(EnablerWallGenerator));
            NotifyPropertyChanged(nameof(FontColorWallGenerator));

            // Ensure all dependent UI elements update
            NotifyPropertyChanged(nameof(EnablerExtWallGenerator));
            NotifyPropertyChanged(nameof(FontColorExtWallGenerator));

            NotifyPropertyChanged(nameof(EnablerStandard));
            NotifyPropertyChanged(nameof(FontColorStandard));

            NotifyPropertyChanged(nameof(EnablerDistant));
            NotifyPropertyChanged(nameof(FontColorDistant));

            NotifyPropertyChanged(nameof(EnablerColumns));
            NotifyPropertyChanged(nameof(FontColorColumns));

            NotifyPropertyChanged(nameof(EnablerRows));
            NotifyPropertyChanged(nameof(FontColorRows));

            NotifyPropertyChanged(nameof(EnablerTunnels));
            NotifyPropertyChanged(nameof(FontColorTunnels));

            NotifyPropertyChanged(nameof(EnablerGrids));
            NotifyPropertyChanged(nameof(FontColorGrids));

            NotifyPropertyChanged(nameof(EnablerPanes));
            NotifyPropertyChanged(nameof(FontColorPanes));

            NotifyPropertyChanged(nameof(EnablerParticles));
            NotifyPropertyChanged(nameof(FontColorParticles));

            NotifyPropertyChanged(nameof(EnablerFloors));
            NotifyPropertyChanged(nameof(FontColorFloors));
        }

        private void UpdateLightingGeneratorUI()
        {
            bool isLightingEnabled = EnableLightingGenerator; // True if any lighting option is enabled
            bool isAutoMapperEnabled = Config.Instance.EnableLightAutoMapper; // Independent toggle

            EnablerLightingGenerator = isLightingEnabled;
            FontColorLightingGenerator = isLightingEnabled ? OnColor : OffColor;

            // Only allow EnableLightAutoMapper if lighting is enabled
            EnablerLightAutoMapper = isLightingEnabled && isAutoMapperEnabled;
            FontColorLightAutoMapper = EnablerLightAutoMapper ? OnColor : OffColor;

            NotifyPropertyChanged(nameof(EnableLightingGenerator));
            NotifyPropertyChanged(nameof(EnablerLightingGenerator));
            NotifyPropertyChanged(nameof(FontColorLightingGenerator));

            NotifyPropertyChanged(nameof(EnablerLightAutoMapper));
            NotifyPropertyChanged(nameof(FontColorLightAutoMapper));
        }

        private void UpdateCleanBeatSageUI()
        {
            bool isEnabled = EnableCleanBeatSage;

            EnablerCleanBeatSage = isEnabled;
            FontColorCleanBeatSage = isEnabled ? OnColor : OffColor;

            NotifyPropertyChanged(nameof(EnablerCleanBeatSage));
            NotifyPropertyChanged(nameof(FontColorCleanBeatSage));
        }

        private void UpdateAutoNjsFixerUI()
        {
            bool isEnabled = EnableAutoNjsFixer;
            EnablerAutoNjsFixer = isEnabled;
            FontColorAutoNjsFixer = isEnabled ? OnColor : OffColor;

            NotifyPropertyChanged(nameof(EnablerAutoNjsFixer));
            NotifyPropertyChanged(nameof(FontColorAutoNjsFixer));
            NotifyPropertyChanged(nameof(AutoNjsFixerMode));
        }


        /*
        [UIValue("EnableWallGenerator")]
        public bool EnableWallGenerator
        {
            get => Config.Instance.EnableWallGenerator;
            set
            {
                Config.Instance.EnableWallGenerator = value;
                EnablerWallGenerator = value;
                if (EnablerWallGenerator) { FontColorWallGenerator = OnColor; EnablerWallGenerator = true; } else { FontColorWallGenerator = OffColor; EnablerWallGenerator = false; };
                NotifyPropertyChanged();
                // Notify changes for Wall enabler and font color properties
                NotifyPropertyChanged(nameof(EnablerExtWallGenerator));
                NotifyPropertyChanged(nameof(FontColorExtWallGenerator));
                NotifyPropertyChanged(nameof(EnablerStandard));
                NotifyPropertyChanged(nameof(FontColorStandard));
                NotifyPropertyChanged(nameof(EnablerDistant));
                NotifyPropertyChanged(nameof(FontColorDistant));
                NotifyPropertyChanged(nameof(EnablerColumns));
                NotifyPropertyChanged(nameof(FontColorColumns));
                NotifyPropertyChanged(nameof(EnablerRows));
                NotifyPropertyChanged(nameof(FontColorRows));
                NotifyPropertyChanged(nameof(EnablerTunnels));
                NotifyPropertyChanged(nameof(FontColorTunnels));
                NotifyPropertyChanged(nameof(EnablerGrids));
                NotifyPropertyChanged(nameof(FontColorGrids));
                NotifyPropertyChanged(nameof(EnablerPanes));
                NotifyPropertyChanged(nameof(FontColorPanes));
                NotifyPropertyChanged(nameof(EnablerParticles));
                NotifyPropertyChanged(nameof(FontColorParticles));
                NotifyPropertyChanged(nameof(EnablerFloors));
                NotifyPropertyChanged(nameof(FontColorFloors));
            }
        }
        */
        // Lights

        [UIValue("BigLasers")]
        public bool BigLasers
        {
            get => Config.Instance.BigLasers;
            set => Config.Instance.BigLasers = value;
        }
        [UIValue("BrightLights")]
        public bool BrightLights
        {
            get => Config.Instance.BrightLights;
            set => Config.Instance.BrightLights = value;
        }
        [UIValue("BoostLighting")]//Creates a boost lighting event. if ON, will set color left to boost color left new color etc. Will only boost a color scheme that has boost colors set so works primarily with COLORS > OVERRIDE DEFAULT COLORS. Or an authors color scheme must have boost colors set (that will probably never happen since they will have boost colors set if they use boost events).
        public bool BoostLighting
        {
            get => Config.Instance.BoostLighting;
            set => Config.Instance.BoostLighting = value;
        }
        [UIValue("EnableLightAutoMapper")]//Creates a boost lighting event. if ON, will set color left to boost color left new color etc. Will only boost a color scheme that has boost colors set so works primarily with COLORS > OVERRIDE DEFAULT COLORS. Or an authors color scheme must have boost colors set (that will probably never happen since they will have boost colors set if they use boost events).
        public bool EnableLightAutoMapper
        {
            get => Config.Instance.EnableLightAutoMapper;
            set
            {
                Config.Instance.EnableLightAutoMapper = value;
                UpdateLightingGeneratorUI();
                NotifyPropertyChanged();
            }
        }
        [UIValue("LightFrequencyMultiplier")]
        public float LightFrequencyMultiplier
        {
            get => Config.Instance.LightFrequencyMultiplier;
            set => Config.Instance.LightFrequencyMultiplier = value;
        }
        [UIValue("BrightnessMultiplier")]
        public float BrightnessMultiplier
        {
            get => Config.Instance.BrightnessMultiplier;
            set => Config.Instance.BrightnessMultiplier = value;
        }

        // Dictionary for custom labels
        private readonly Dictionary<Config.Style, string> _styleLabels = new Dictionary<Config.Style, string>
        {
            { Config.Style.ON, "Fast Strobe On" },
            { Config.Style.FADE, "Med Fade" },
            { Config.Style.FLASH, "Med Flash" },
            { Config.Style.TRANSITION, "Slow Transition" }
        };

        [UIValue("available-styles")]
        private List<object> _styles = new List<object>();

        public GameplaySetupView() // Constructor with the class name
        {
            // Populate the dropdown list in the desired order
            _styles.Add(_styleLabels[Config.Style.ON]);
            _styles.Add(_styleLabels[Config.Style.FADE]);
            _styles.Add(_styleLabels[Config.Style.FLASH]);
            _styles.Add(_styleLabels[Config.Style.TRANSITION]);

            // Left → right order in the dropdown
            _arcRotationModes.Add(_arcRotationModeLabels[Config.ArcRotationModeType.ForceZero]);
            _arcRotationModes.Add(_arcRotationModeLabels[Config.ArcRotationModeType.NetZero]);
            _arcRotationModes.Add(_arcRotationModeLabels[Config.ArcRotationModeType.NoRestriction]);

            _autoNjsModes.Add(_autoNjsFixerModeLabels[Config.AutoNjsFixerModeType.MaintainNoteSpeed]);
            _autoNjsModes.Add(_autoNjsFixerModeLabels[Config.AutoNjsFixerModeType.ForceNJS]);
        }

        [UIValue("LightStyle")]
        public string LightStyle
        {
            get => _styleLabels[Config.Instance.LightStyle];
            set
            {
                if (_styleLabels.ContainsValue(value))
                {
                    Config.Style style = _styleLabels.FirstOrDefault(x => x.Value == value).Key;
                    Config.Instance.LightStyle = style;
                    NotifyPropertyChanged();
                }
            }
        }
        // In your GameplaySetupView (or the BSML host class)
        private readonly Dictionary<Config.ArcRotationModeType, string> _arcRotationModeLabels = new Dictionary<Config.ArcRotationModeType, string>
{
        { Config.ArcRotationModeType.ForceZero,     "Force Zero" },
        { Config.ArcRotationModeType.NetZero,       "Net Zero" },
        { Config.ArcRotationModeType.NoRestriction, "No Restriction" }
};

        [UIValue("available-arc-modes")]
        private List<object> _arcRotationModes = new List<object>();


        [UIValue("ArcRotationMode")]
        public string ArcRotationMode
        {
            get => _arcRotationModeLabels[Config.Instance.ArcRotationMode];
            set
            {
                if (_arcRotationModeLabels.ContainsValue(value))
                {
                    var mode = _arcRotationModeLabels.First(kv => kv.Value == value).Key;
                    Config.Instance.ArcRotationMode = mode;
                    NotifyPropertyChanged(); // updates the UI
                }
            }
        }

        private readonly Dictionary<Config.AutoNjsFixerModeType, string> _autoNjsFixerModeLabels = new Dictionary<Config.AutoNjsFixerModeType, string>
        {
            { Config.AutoNjsFixerModeType.MaintainNoteSpeed, "Maintain Map Speed" },
            { Config.AutoNjsFixerModeType.ForceNJS,         "Set Note Speed" }
        };

        [UIValue("available-auto-modes")]
        private List<object> _autoNjsModes = new List<object>();


        [UIValue("AutoNjsFixerMode")]
        public string AutoNjsFixerMode
        {
            get => _autoNjsFixerModeLabels[Config.Instance.AutoNjsFixerMode];
            set
            {
                if (_autoNjsFixerModeLabels.ContainsValue(value))
                {
                    var mode = _autoNjsFixerModeLabels.First(kv => kv.Value == value).Key;
                    Config.Instance.AutoNjsFixerMode = mode;
                    NotifyPropertyChanged(); // for AutoNjsFixerMode itself
                    NotifyPropertyChanged(nameof(EnablerDesiredNJS));
                    NotifyPropertyChanged(nameof(FontColorDesiredNJS));
                }
            }
        }

        // 90
        /*
        [UIValue("ShowGenerated90")]
        public bool ShowGenerated90
        {
            get => Config.Instance.ShowGenerated90;
            set
            {
                Config.Instance.ShowGenerated90 = value;
                NotifyPropertyChanged();
                NotifyPropertyChanged(nameof(Enabler90));
                NotifyPropertyChanged(nameof(FontColor90));
            }
        }
        [UIValue("LimitRotations90")]
        public float LimitRotations90
        {
            get => Config.Instance.LimitRotations90;
            set => Config.Instance.LimitRotations90 = value;
        }

        // One Saber Not Good so UNUSED
        /*
        [UIValue("OnlyOneSaber")]
        public bool OnlyOneSaber
        {
            get => Config.Instance.OnlyOneSaber;
            set => Config.Instance.OnlyOneSaber = value;
        }
        [UIValue("LeftHandedOneSaber")]
        public bool LeftHandedOneSaber
        {
            get => Config.Instance.LeftHandedOneSaber;
            set => Config.Instance.LeftHandedOneSaber = value;
        }
        */
        // Based On

        [UIValue("available-bases")]
        private readonly List<object> _bases = Enum.GetNames(typeof(Config.Base)).Select(x => (object)x).ToList();

        [UIValue("BasedOn")]
        public string BasedOn
        {
            get => Config.Instance.BasedOn.ToString();
            set => Config.Instance.BasedOn = (Config.Base)Enum.Parse(typeof(Config.Base), value);
        }

        // Slider Formatting ---------------------------------------------------------------------------------------------------------------------------------

        public string IntFormatter(float value)//BW This will output the text on the slider to be an integer with a degree symbol in BSML
        {
            int intValue = Mathf.RoundToInt(value);
            return $"{intValue}";
        }

        public string AngleFormatter(float value)//BW This will output the text on the slider to be an integer with a degree symbol in BSML
        {
            int intValue = Mathf.RoundToInt(value);
            return $"{intValue}°";
        }
        public string TimeFormatter(float value)//BW This will output the text on the slider with an 's' at the end in BSML
        {
            return $"{value}s";
        }
        public string MultFormatter(float value)//BW This will output the text on the slider with an 's' at the end in BSML
        {
            return $"{value:F1}x";
        }
        public string PercentFormatter(float value)//BW This will output the text on the slider with an 's' at the end in BSML
        {
            return $"{value}%";
        }
        public string SpeedFormatter(float value)//BW This will output the text on the slider with an 's' at the end in BSML
        {
            int intValue = Mathf.RoundToInt(value);
            return $"{intValue}m/s";
        }
        public string DistanceFormatter(float value)//BW This will output the text on the slider with an 's' at the end in BSML
        {
            int intValue = Mathf.RoundToInt(value);
            return $"{intValue}m";
        }




        // Item Enablers (allows a toggle to enable/disable another item ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++

        // Tried this to make vertical container active/inactive to hide all settings but when used caused the window to jump in odd ways and sometimes even be unscrollable so hid many settings and could only be reset by going to another mod settings tab
        [UIValue("ActiveEnablePlugin")]
        public bool ActiveEnablePlugin
        { get => !Config.Instance.EnablePlugin; set { NotifyPropertyChanged(); } }

        [UIValue("EnablerEnablePlugin")] // used to enable/disable many items to make the plugin appear inactive
        public bool EnablerEnablePlugin
        { get => Config.Instance.EnablePlugin; set { NotifyPropertyChanged(); } }


        [UIValue("EnablerRotationSettings")]
        public bool EnablerRotationSettings
        {
            get => Config.Instance.EnablePlugin && Config.Instance.Enable360fyer; // add && !Config.Instance.Wireless360 if you also want to lock these in Wireless mode
            set
            {
                NotifyPropertyChanged();
            }
        }







        // Wireless360 enable/gray-out
        [UIValue("EnablerWireless360")]
        public bool EnablerWireless360
        { get => Config.Instance.EnablePlugin; set { NotifyPropertyChanged(); } }




        [UIValue("FontColorWireless360")]
        public string FontColorWireless360
        { get => Config.Instance.EnablePlugin ? OnColor : OffColor; set { NotifyPropertyChanged(); } }


        /*
        [UIValue("EnablerEnableFeaturesForNonGen360Maps")] // used to enable/disable many items to make the plugin appear inactive
        public bool EnablerEnableFeaturesForNonGen360Maps
        { get => Config.Instance.EnablePlugin && Config.Instance.EnableFeaturesForNonGen360Maps; set { NotifyPropertyChanged(); } }

        [UIValue("EnablerEnableFeaturesForStandardMaps")] // used to enable/disable many items to make the plugin appear inactive
        public bool EnablerEnableFeaturesForStandardMaps
        { get => Config.Instance.EnablePlugin && Config.Instance.EnableFeaturesForStandardMaps; set { NotifyPropertyChanged(); } }
        */

        [UIValue("EnablerLimitRotations360")]
        public bool EnablerLimitRotations360
        { get => Config.Instance.EnablePlugin && !Config.Instance.Wireless360; set { NotifyPropertyChanged(); } }

        [UIValue("EnablerArcs")]
        public bool EnablerArcs
        {
            get => EnableArcsGenerator;
            set
            {
                NotifyPropertyChanged(nameof(FontColorArcs));
            }
        }

        [UIValue("EnablerChains")]
        public bool EnablerChains
        {
            get => EnableChainsGenerator;
            set
            {
                NotifyPropertyChanged(nameof(FontColorChains));
            }
        }
        /*
        [UIValue("EnablerArcs")]
        public bool EnablerArcs
        { get => Config.Instance.EnablePlugin && Config.Instance.EnableArcs; set { NotifyPropertyChanged(); } }

        [UIValue("EnablerChains")]
        public bool EnablerChains
        */
        /*
        [UIValue("EnablerWallGenerator")]
        public bool EnablerWallGenerator
        { get => Config.Instance.EnablePlugin && Config.Instance.EnableWallGenerator ? true : false; set { NotifyPropertyChanged(); } }

        [UIValue("EnablerExtWallGenerator")]
        public bool EnablerExtWallGenerator
        { get => Config.Instance.EnablePlugin && Config.Instance.EnableWallGenerator && Config.Instance.EnableExtensionMappingWallsGenerator ? true : false; set { NotifyPropertyChanged(); } }
        */
        [UIValue("EnablerWallGenerator")]
        public bool EnablerWallGenerator
        { get => Config.Instance.EnablePlugin && EnableWallGenerator ? true : false; set { NotifyPropertyChanged(); } }

        [UIValue("EnablerExtWallGenerator")]
        public bool EnablerExtWallGenerator
        {
            get => Config.Instance.EnablePlugin
                   && EnableWallGenerator
                   && IsMappingExtensionsInstalledNow
                   && Config.Instance.EnableMappingExtensionsWallsGenerator;
            set => NotifyPropertyChanged();
        }
        [UIValue("EnablerStandard")]
        public bool EnablerStandard
        { get => Config.Instance.EnablePlugin && EnableWallGenerator && (Config.Instance.EnableStandardWalls || Config.Instance.EnableBigWalls); set { NotifyPropertyChanged(); } }


        [UIValue("EnablerDistant")]
        public bool EnablerDistant
        { get => Config.Instance.EnablePlugin && EnableWallGenerator && IsMappingExtensionsInstalledNow && Config.Instance.EnableMappingExtensionsWallsGenerator && Config.Instance.EnableDistantExtensionWalls; set { NotifyPropertyChanged(); } }

        [UIValue("EnablerColumns")]
        public bool EnablerColumns
        { get => Config.Instance.EnablePlugin && EnableWallGenerator && IsMappingExtensionsInstalledNow && Config.Instance.EnableMappingExtensionsWallsGenerator && Config.Instance.EnableColumnWalls; set { NotifyPropertyChanged(); } }

        [UIValue("EnablerRows")]
        public bool EnablerRows
        { get => Config.Instance.EnablePlugin && EnableWallGenerator && IsMappingExtensionsInstalledNow && Config.Instance.EnableMappingExtensionsWallsGenerator && Config.Instance.EnableRowWalls; set { NotifyPropertyChanged(); } }

        [UIValue("EnablerTunnels")]
        public bool EnablerTunnels
        { get => Config.Instance.EnablePlugin && EnableWallGenerator && IsMappingExtensionsInstalledNow && Config.Instance.EnableMappingExtensionsWallsGenerator && Config.Instance.EnableTunnelWalls; set { NotifyPropertyChanged(); } }

        [UIValue("EnablerGrids")]
        public bool EnablerGrids
        { get => Config.Instance.EnablePlugin && EnableWallGenerator && IsMappingExtensionsInstalledNow && Config.Instance.EnableMappingExtensionsWallsGenerator && Config.Instance.EnableGridWalls; set { NotifyPropertyChanged(); } }

        [UIValue("EnablerPanes")]
        public bool EnablerPanes
        { get => Config.Instance.EnablePlugin && EnableWallGenerator && IsMappingExtensionsInstalledNow && Config.Instance.EnableMappingExtensionsWallsGenerator && Config.Instance.EnableWindowPaneWalls; set { NotifyPropertyChanged(); } }

        [UIValue("EnablerParticles")]
        public bool EnablerParticles
        { get => Config.Instance.EnablePlugin && EnableWallGenerator && IsMappingExtensionsInstalledNow && Config.Instance.EnableMappingExtensionsWallsGenerator && Config.Instance.EnableParticleWalls; set { NotifyPropertyChanged(); } }

        [UIValue("EnablerFloors")]
        public bool EnablerFloors
        { get => Config.Instance.EnablePlugin && EnableWallGenerator && IsMappingExtensionsInstalledNow && Config.Instance.EnableMappingExtensionsWallsGenerator && Config.Instance.EnableFloorWalls; set { NotifyPropertyChanged(); } }

        [UIValue("EnablerCleanBeatSage")]
        public bool EnablerCleanBeatSage
        { get => Config.Instance.EnablePlugin && Config.Instance.EnableCleanBeatSage; set { NotifyPropertyChanged(); } }


        [UIValue("EnablerLightingGenerator")]
        public bool EnablerLightingGenerator
        { get => Config.Instance.EnablePlugin && (Config.Instance.EnableLightingGen360 || Config.Instance.EnableLightingNonGen360 || Config.Instance.EnableLightingStandard); set { NotifyPropertyChanged(); } }


        [UIValue("EnablerLightAutoMapper")]
        public bool EnablerLightAutoMapper
        { get => Config.Instance.EnablePlugin && Config.Instance.EnableLightAutoMapper; set { NotifyPropertyChanged(); } }

        [UIValue("Enabler90")]
        public bool Enabler90
        { get => Config.Instance.EnablePlugin && Config.Instance.ShowGenerated90; set { NotifyPropertyChanged(); } }

        [UIValue("EnablerAutoNjsFixer")]
        public bool EnablerAutoNjsFixer
        { get => Config.Instance.EnablePlugin && (Config.Instance.EnableAutoNjsFixerGen360 || Config.Instance.EnableAutoNjsFixerNonGen360 || Config.Instance.EnableAutoNjsFixerStandard); set { NotifyPropertyChanged(); } }

        [UIValue("EnablerDesiredNJS")]
        public bool EnablerDesiredNJS
        {
            get => Config.Instance.EnablePlugin
                   && EnablerAutoNjsFixer
                   && Config.Instance.AutoNjsFixerMode == Config.AutoNjsFixerModeType.ForceNJS; // "Set Velocity"
            set => NotifyPropertyChanged();
        }

        // Item FontColors (allows a toggle to dim/undim another item #############################################################################################################

        private const string OnColor = "#dddddd";
        private const string OffColor = "#444444";

        private const string OnColorStandardMaps = "#dddd00";

        [UIValue("FontColorEnablePlugin")]
        public String FontColorEnablePlugin
        { get => Config.Instance.EnablePlugin ? OnColor : OffColor; set { NotifyPropertyChanged(); } }

        [UIValue("FontColorRotationSettings")]
        public string FontColorRotationSettings
        {
            get => (Config.Instance.EnablePlugin && Config.Instance.Enable360fyer) ? OnColor : OffColor;
            set { NotifyPropertyChanged(); }
        }

        /*
        [UIValue("FontColorEnableFeaturesForNonGen360Maps")]
        public String FontColorEnableFeaturesForNonGen360Maps
        { get => Config.Instance.EnablePlugin ? OnColorStandardMaps : OffColor; set { NotifyPropertyChanged(); } } // only change this color when EnablePlugin changes

        [UIValue("FontColorEnableFeaturesForStandardMaps")]
        public String FontColorEnableFeaturesForStandardMaps
        { get => Config.Instance.EnablePlugin ? OnColorStandardMaps : OffColor; set { NotifyPropertyChanged(); } } // only change this color when EnablePlugin changes
        */
        [UIValue("FontColorLimitRotations360")]
        public String FontColorLimitRotations360
        { 
            get => !Config.Instance.EnablePlugin ? OffColor : (Config.Instance.Wireless360 ? OffColor : OnColor); set { NotifyPropertyChanged(); } 
        }



        [UIValue("FontColorArcs")]
        public String FontColorArcs
        { get => !Config.Instance.EnablePlugin ? OffColor : (EnablerArcs ? OnColor : OffColor); set { NotifyPropertyChanged(); } }

        [UIValue("FontColorChains")]
        public String FontColorChains
        { get => !Config.Instance.EnablePlugin ? OffColor : (EnablerChains ? OnColor : OffColor); set { NotifyPropertyChanged(); } }

        [UIValue("FontColorWallGenerator")]
        public String FontColorWallGenerator
        { get => !Config.Instance.EnablePlugin ? OffColor : (EnableWallGenerator ? OnColor : OffColor); set { NotifyPropertyChanged(); } }

        [UIValue("FontColorStandard")]
        public String FontColorStandard
        { get => !Config.Instance.EnablePlugin || !EnableWallGenerator || (!Config.Instance.EnableStandardWalls && !Config.Instance.EnableBigWalls) ? OffColor : ((Config.Instance.EnableStandardWalls || Config.Instance.EnableBigWalls) ? OnColor : OffColor); set { NotifyPropertyChanged(); } }


        [UIValue("FontColorExtWallGenerator")]
        public string FontColorExtWallGenerator
        {
            get => !Config.Instance.EnablePlugin
                    || !EnableWallGenerator
                    || !IsMappingExtensionsInstalledNow
                ? OffColor
                : (Config.Instance.EnableMappingExtensionsWallsGenerator ? OnColor : OffColor);
            set => NotifyPropertyChanged();
        }

        [UIValue("FontColorDistant")]
        public String FontColorDistant
        { get => !Config.Instance.EnablePlugin || !EnableWallGenerator || !IsMappingExtensionsInstalledNow || !Config.Instance.EnableMappingExtensionsWallsGenerator ? OffColor : (Config.Instance.EnableDistantExtensionWalls ? OnColor : OffColor); set { NotifyPropertyChanged(); } }

        [UIValue("FontColorColumns")]
        public String FontColorColumns
        { get => !Config.Instance.EnablePlugin || !EnableWallGenerator || !IsMappingExtensionsInstalledNow || !Config.Instance.EnableMappingExtensionsWallsGenerator ? OffColor : (Config.Instance.EnableColumnWalls ? OnColor : OffColor); set { NotifyPropertyChanged(); } }

        [UIValue("FontColorRows")]
        public String FontColorRows
        { get => !Config.Instance.EnablePlugin || !EnableWallGenerator || !IsMappingExtensionsInstalledNow || !Config.Instance.EnableMappingExtensionsWallsGenerator ? OffColor : (Config.Instance.EnableRowWalls ? OnColor : OffColor); set { NotifyPropertyChanged(); } }

        [UIValue("FontColorTunnels")]
        public String FontColorTunnels
        { get => !Config.Instance.EnablePlugin || !EnableWallGenerator || !IsMappingExtensionsInstalledNow || !Config.Instance.EnableMappingExtensionsWallsGenerator ? OffColor : (Config.Instance.EnableTunnelWalls ? OnColor : OffColor); set { NotifyPropertyChanged(); } }

        [UIValue("FontColorGrids")]
        public String FontColorGrids
        { get => !Config.Instance.EnablePlugin || !EnableWallGenerator || !IsMappingExtensionsInstalledNow || !Config.Instance.EnableMappingExtensionsWallsGenerator ? OffColor : (Config.Instance.EnableGridWalls ? OnColor : OffColor); set { NotifyPropertyChanged(); } }

        [UIValue("FontColorPanes")]
        public String FontColorPanes
        { get => !Config.Instance.EnablePlugin || !EnableWallGenerator || !IsMappingExtensionsInstalledNow || !Config.Instance.EnableMappingExtensionsWallsGenerator ? OffColor : (Config.Instance.EnableWindowPaneWalls ? OnColor : OffColor); set { NotifyPropertyChanged(); } }

        [UIValue("FontColorParticles")]
        public String FontColorParticles
        { get => !Config.Instance.EnablePlugin || !EnableWallGenerator || !IsMappingExtensionsInstalledNow || !Config.Instance.EnableMappingExtensionsWallsGenerator ? OffColor : (Config.Instance.EnableParticleWalls ? OnColor : OffColor); set { NotifyPropertyChanged(); } }

        [UIValue("FontColorFloors")]
        public String FontColorFloors
        { get => !Config.Instance.EnablePlugin || !EnableWallGenerator || !IsMappingExtensionsInstalledNow || !Config.Instance.EnableMappingExtensionsWallsGenerator ? OffColor : (Config.Instance.EnableFloorWalls ? OnColor : OffColor); set { NotifyPropertyChanged(); } }

        [UIValue("FontColorCleanBeatSage")]
        public string FontColorCleanBeatSage
        { get => !Config.Instance.EnablePlugin ? OffColor : (EnablerCleanBeatSage ? OnColor : OffColor); set { NotifyPropertyChanged(); } }


        [UIValue("FontColorLightingGenerator")]
        public String FontColorLightingGenerator
        { get => !Config.Instance.EnablePlugin ? OffColor : (EnableLightingGenerator ? OnColor : OffColor); set { NotifyPropertyChanged(); } }

        [UIValue("FontColorLightAutoMapper")]
        public String FontColorLightAutoMapper
        { get => !Config.Instance.EnablePlugin ? OffColor : (EnablerLightAutoMapper ? OnColor : OffColor); set { NotifyPropertyChanged(); } }

        [UIValue("FontColor90")]
        public String FontColor90
        { get => !Config.Instance.EnablePlugin ? OffColor : (Config.Instance.ShowGenerated90 ? OnColor : OffColor); set { NotifyPropertyChanged(); } }

        [UIValue("FontColorAutoNjsFixer")]
        public String FontColorAutoNjsFixer
        { get => !Config.Instance.EnablePlugin ? OffColor : (EnableAutoNjsFixer ? OnColor : OffColor); set { NotifyPropertyChanged(); } }

        [UIValue("FontColorDesiredNJS")]
        public string FontColorDesiredNJS
        {
            get => EnablerDesiredNJS ? OnColor : OffColor;
            set => NotifyPropertyChanged();
        }
        /*
        [UIComponent("AllSettingsContainer")]
        public VerticalLayoutGroup AllSettingsContainer;
        public void AllSettingsContainer()
        {
            if (AllSettingsContainer != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(AllSettingsContainer.GetComponent<RectTransform>()); // AI suggested this. It helped a bit.
                //Plugin.Log.Info("Successfully REFRESHED the vertical layout!");
            }
        }
        */

    }
    /*
    //trying to display the original NJS and NJO
    public static class OriginalStatsHub
    {
        public static event Action<float, float> OriginalsChanged; // (njs, jd)

        public static void SetOriginals(float njs, float jd)
        {
            // update your own state if you keep it
            NoteJumpMovementSpeed = njs;
            JumpDistance = jd;

            OriginalsChanged?.Invoke(njs, jd);
        }

        // If you already store these:
        public static float NoteJumpMovementSpeed { get; set; }
        public static float JumpDistance { get; set; }
    }
    */
}
