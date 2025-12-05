using AutoBS.UI;
using BS_Utils.Gameplay;
using CustomJSONData.CustomBeatmap;
using HarmonyLib;
using Newtonsoft.Json.Linq;
using SongCore;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Threading.Tasks;
using Zenject;
using static AutoBS.Patches.SetContent;
using static IPA.Logging.Logger;

namespace AutoBS.Patches
{

    #region Prefix - MenuTransitionsHelper.StartStandardLevel - TransitionPatcher Runs when you click play button
    //BW 2nd item that runs after LevelUpdatePatcher & GameModeHelper
    //Runs when you click play button
    //BW v1.31.0 Class MenuTransitionsHelper method StartStandardLevel has 1 new item added. After 'ColorScheme overrideColorScheme', 'ColorScheme beatmapOverrideColorScheme' has been added. so i added: typeof(ColorScheme) after typeof(ColorScheme)
    //v1.34
    //[HarmonyPatch(typeof(MenuTransitionsHelper))]
    //[HarmonyPatch("StartStandardLevel", new[] { typeof(string), typeof(IDifficultyBeatmap), typeof(IPreviewBeatmapLevel), typeof(OverrideEnvironmentSettings), typeof(ColorScheme), typeof(ColorScheme), typeof(GameplayModifiers), typeof(PlayerSpecificSettings), typeof(PracticeSettings), typeof(string), typeof(bool), typeof(bool), typeof(Action), typeof(Action<DiContainer>), typeof(Action<StandardLevelScenesTransitionSetupDataSO, LevelCompletionResults>), typeof(Action<LevelScenesTransitionSetupDataSO, LevelCompletionResults>), typeof(RecordingToolManager.SetupData) })]//v1.34 added last item
    //v1.40
    [HarmonyPatch(typeof(MenuTransitionsHelper))]
    public class TransitionPatcher
    {

        public static MethodBase TargetMethod()
        {
            return typeof(MenuTransitionsHelper).GetMethod(
                "StartStandardLevel",
                BindingFlags.Instance | BindingFlags.Public,
                null,
                new[] {
            typeof(string),
            typeof(BeatmapKey).MakeByRefType(),
            typeof(BeatmapLevel),
            typeof(OverrideEnvironmentSettings),
            typeof(ColorScheme),
            typeof(bool), // playerOverrideLightshowColors
            typeof(ColorScheme),
            typeof(GameplayModifiers),
            typeof(PlayerSpecificSettings),
            typeof(PracticeSettings),
            typeof(EnvironmentsListModel),
            typeof(string),
            typeof(bool),
            typeof(bool),
            typeof(Action),
            typeof(Action<DiContainer>),
            typeof(Action<StandardLevelScenesTransitionSetupDataSO, LevelCompletionResults>),
            typeof(Action<StandardLevelScenesTransitionSetupDataSO, LevelCompletionResults>),
            typeof(Nullable<>).MakeGenericType(typeof(RecordingToolManager.SetupData))
                },
                null
            );
        }

        //v1.40
        public static BeatmapKey CurrentPlayKey;  // store the key here
        public static Version CurrentBeatmapVersion;
        // Only inject once, for the map the player actually "Starts" instead of all difficulties in the set
        public static bool UserSelectedMapToInject = false;
        //public static string startingGameMode;
        public static string SelectedSerializedName;//will be "Generated360Degree" for gen 360
        public static BeatmapCharacteristicSO SelectedCharacteristicSO;
        public static string EnvironmentName;
        public static BeatmapDifficulty SelectedDifficulty;
        public static ColorScheme theOverrideColorScheme;
        public static CustomBeatmapData beatMapDataCBforColorScheme;

        public static BeatmapLevel SelectedBeatmapLevel;

        public static float OriginalNoteJumpMovementSpeed = 0;
        public static float FinalNoteJumpMovementSpeed = 0;
        public static float NoteJumpOffset; //noteJumpStartBeatOffset
        public static float FinalJumpDistance;
        public static bool AutoNJSDisabledByConflictingMod = false;
        public static bool AutoNJSPracticeModeDisabledByConflictingMod = false;

        //public static int majorVersion = 3;
        public static Version LevelVersion = new Version(2, 6, 0);

        public static float bpm;
        public static float NotesPerSecond;

        public static bool MapAlreadyUsesMappingExtensions = false; // used by generator360.cs to turn off automated extended walls for maps already using mapping extensions
        public static bool AlreadyUsingEnvColorBoost = false;
        public static bool MapAlreadyUsesChains = false;

        public static bool IsBeatSageMap = false;

        public static bool RequiresNoodle = false;
        public static bool RequiresChroma = false;
        public static bool RequiresVivify = false;

        public static bool NoodleProblemNotes = false;
        public static bool NoodleProblemObstacles = false;

        public static string ScoreSubmissionDisableText = "";

        //v1.34
        //static void Prefix(string gameMode, IDifficultyBeatmap difficultyBeatmap, IPreviewBeatmapLevel previewBeatmapLevel, OverrideEnvironmentSettings overrideEnvironmentSettings, ColorScheme overrideColorScheme, GameplayModifiers gameplayModifiers, PlayerSpecificSettings playerSpecificSettings, PracticeSettings practiceSettings, string backButtonText, bool useTestNoteCutSoundEffects, bool startPaused, Action beforeSceneSwitchCallback, Action<DiContainer> afterSceneSwitchCallback, Action<StandardLevelScenesTransitionSetupDataSO, LevelCompletionResults> levelFinishedCallback, Action<LevelScenesTransitionSetupDataSO, LevelCompletionResults> levelRestartedCallback)
        //v1.40
        static void Prefix(
            string gameMode,
            ref BeatmapKey beatmapKey, // was in
            BeatmapLevel beatmapLevel,
            OverrideEnvironmentSettings overrideEnvironmentSettings,
            ColorScheme playerOverrideColorScheme,
            bool playerOverrideLightshowColors,
            ColorScheme beatmapOverrideColorScheme,
            GameplayModifiers gameplayModifiers,
            PlayerSpecificSettings playerSpecificSettings,
            PracticeSettings practiceSettings,
            EnvironmentsListModel environmentsListModel,
            string backButtonText,
            bool useTestNoteCutSoundEffects,
            bool startPaused,
            Action beforeSceneSwitchToGameplayCallback,
            Action<DiContainer> afterSceneSwitchToGameplayCallback,
            Action<StandardLevelScenesTransitionSetupDataSO, LevelCompletionResults> levelFinishedCallback,
            Action<StandardLevelScenesTransitionSetupDataSO, LevelCompletionResults> levelRestartedCallback,
            RecordingToolManager.SetupData? recordingToolData)
        {
            Plugin.Log.Info($"[TransitionPatcher] Called!");
            if (!Config.Instance.EnablePlugin) return;

            SelectedSerializedName = beatmapKey.beatmapCharacteristic.serializedName;

            if (!Utils.IsEnabledForGeneralFeatures()) return; // have to have the serialized name for this to work

            //Plugin.Log.Info("[ScoreGate] Resetting at StartStandardLevel");
            ScoreGate.Clear();

            SelectedCharacteristicSO = beatmapKey.beatmapCharacteristic;
            SelectedDifficulty = beatmapKey.difficulty;
            SelectedBeatmapLevel = beatmapLevel;
            CurrentPlayKey = beatmapKey;
            bpm = beatmapLevel.beatsPerMinute;

            CurrentBeatmapVersion = BeatmapDataRegistry.versionByKey.TryGetValue(CurrentPlayKey, out Version foundVersion) ? foundVersion : new Version(2,6,0);

            Plugin.Log.Info(".");
            Plugin.Log.Info($"[TransitionPatcher] User selected:  {beatmapKey.beatmapCharacteristic.serializedName} {SelectedDifficulty} v{CurrentBeatmapVersion} - ID: {beatmapKey.levelId} -------------------------------------"); // will be solo and standard, Generater360Degree, Generated90Degree, 360Degree, 90Degree, Lightshow, etc

            bool isGen360 = SelectedSerializedName == GameModeHelper.GENERATED_360DEGREE_MODE;
            UserSelectedMapToInject = isGen360;

            // using generated beatmapkey. same levelId is fine between standard and gen 360, but changing the characteristic (Standard → Generated360Degree) makes it a distinct beatmap for scoring and most plugins.
            Plugin.Log.Info($"[TransitionPatcher] Will inject? {UserSelectedMapToInject} for {beatmapKey.beatmapCharacteristic.serializedName} - {beatmapKey.difficulty}");

            //RequiresNoodle = RequirementsRegistry.findByKey.TryGetValue(CurrentPlayKey, out var foundValue) && foundValue.Any(r => r.Contains("Noodle Extensions", StringComparison.OrdinalIgnoreCase));
            //RequiresChroma = ((RequirementsRegistry.findByKey.TryGetValue(CurrentPlayKey, out var foundValue1) && foundValue1.Any(r => r.Contains("Chroma", StringComparison.OrdinalIgnoreCase))) ||
            //                  (SuggestionsRegistry.findByKey.TryGetValue(CurrentPlayKey, out var foundValue2)  && foundValue2.Any(r => r.Contains("Chroma", StringComparison.OrdinalIgnoreCase))));
            //RequiresVivify = RequirementsRegistry.findByKey.TryGetValue(CurrentPlayKey, out var foundValue3) && foundValue3.Any(r => r.Contains("Vivify", StringComparison.OrdinalIgnoreCase));
            //MapAlreadyUsesMappingExtensions = MapAlreadyUsesMappingExtensionsRegistry.findByKey.TryGetValue(CurrentPlayKey, out var foundValue5) && foundValue5 == true;
            



            // For noodle standard maps, need to do it this way. but this works for all maps so use this instead of the registry lookup.
            //if (SetContent.basedOn != SelectedSerializedName)
            {
                RequiresNoodle = CheckForExternalModRequirement(beatmapLevel, "Noodle Extensions");
                RequiresChroma = CheckForExternalModRequirement(beatmapLevel, "Chroma");
                RequiresVivify = CheckForExternalModRequirement(beatmapLevel, "Vivify");
            }

            Plugin.Log.Info($"[TransitionPatcher] Requirements for this map: Noodle={RequiresNoodle}, Chroma={RequiresChroma}, Vivify={RequiresVivify}");

            EnvironmentName = GetEnvironmentName(beatmapKey, beatmapLevel, overrideEnvironmentSettings);

            //OriginalNoteJumpMovementSpeed = AutoNjsRegistry.findByKey(CurrentPlayKey).original_njs;
            //NoteJumpMovementSpeed = AutoNjsRegistry.findByKey(CurrentPlayKey).autoNjs_njs; // final speed. will be originalNJS and JD if autoNJS is disabled 
            //JumpDistance = AutoNjsRegistry.findByKey(CurrentPlayKey).autoNjs_jd;

            IsBeatSageMap = false;

            //if (OriginalNoteJumpMovementSpeed == -99) // nonGen map that is not a basedOn map
            {
                // BOTH of these are done in setContent now for gen360 and basedOn maps. so only need to do it here for nonGen non basedOn maps. so should restore autoNjs registry for those maps only and can also leave beat sage in setcontent
                var basic = beatmapLevel.GetDifficultyBeatmapData(SelectedCharacteristicSO, SelectedDifficulty);
                if (basic != null)
                {
                    OriginalNoteJumpMovementSpeed = NoteJumpMovementSpeed(SelectedDifficulty, basic.noteJumpMovementSpeed);
                    NoteJumpOffset = basic.noteJumpStartBeatOffset;
                    float originalJD;
                    (FinalNoteJumpMovementSpeed, FinalJumpDistance, originalJD) = AutoNjsFixer.Fix(OriginalNoteJumpMovementSpeed, NoteJumpOffset, bpm);
                    Plugin.Log.Info($"[TransitionPatcher] BasicBeatmapData - Original NJS: {OriginalNoteJumpMovementSpeed} Original NJO: {NoteJumpOffset}, AutoNjsFixer NJS: {FinalNoteJumpMovementSpeed}, Original JD: {originalJD}, AutoNjsFixer JD: {FinalJumpDistance}");
                    IsBeatSageMap = basic.mappers.Contains("Beat Sage");
                    Plugin.Log.Info($"[TransitionPatcher] BasicBeatmapData - Beat Sage Map: {IsBeatSageMap}");
                }
            }
            bool isBasedOn = SelectedSerializedName == basedOn;
            if (isGen360 || isBasedOn)
            {
                AlreadyUsingEnvColorBoost = AlreadyUsingEnvColorBoostRegistry.findByKey.TryGetValue(CurrentPlayKey, out var foundValue4) && foundValue4 == true;
                MapAlreadyUsesChains = MapAlreadyUsesChainsRegistry.findByKey.TryGetValue(CurrentPlayKey, out var foundChains) ? foundChains : false;
                NotesPerSecond = NotesPerSecRegistry.findByKey.TryGetValue(CurrentPlayKey, out var nps) ? nps : 0f; // used by generator to reduce rotations for nigh density maps
                Plugin.Log.Info($"[TransitionPatcher] Gen or BasedOn Map - Retrieved from Registries. AlreadyUsingEnvColorBoost: {AlreadyUsingEnvColorBoost}, MapAlreadyUsesChains: {MapAlreadyUsesChains}, NotesPerSecond: {NotesPerSecond}");
            }
            else
            {
                MapAlreadyUsesChains = false;
                AlreadyUsingEnvColorBoost = false;
                NotesPerSecond = 0;

                (string beatmapJson, string lightShowJson, string a, Version version) = GetJson(beatmapLevel, SelectedDifficulty, CurrentPlayKey); //works for custom and built-in levels

                CurrentBeatmapVersion = version;

                JObject beatmapObj = JObject.Parse(beatmapJson);
                
                bool hasBurstSliders = beatmapObj["burstSliders"] != null ? beatmapObj["burstSliders"].Count() > 0 : false; //v3 (v2 has no burst sliders only arcs)
                bool hasChains = beatmapObj["chains"] != null ? beatmapObj["chains"].Count() > 0 : false; //v4
                if (hasBurstSliders || hasChains)
                {
                    MapAlreadyUsesChains = true;
                    Plugin.Log.Info($"[TransitionPatcher] NonGen and Non BasedOn Map - Detected Original Chains in JSON. MapAlreadyUsesChains");
                }
                var songCoreExtraData = SongCoreBridge.TryGetSongCoreSongData(beatmapLevel);
                var difficultyData = songCoreExtraData?._difficulties.FirstOrDefault(d =>
                                        d._beatmapCharacteristicName == SelectedSerializedName &&
                                        d._difficulty == SelectedDifficulty);

                SetContent.SongFolderPath = SongFolderUtils.TryGetSongFolder(beatmapLevel.levelID);
                
                if (songCoreExtraData != null && songCoreExtraData._difficulties != null && difficultyData != null &&
                        (difficultyData._envColorLeftBoost != null || difficultyData._envColorRightBoost != null))
                {
                    AlreadyUsingEnvColorBoost = true;
                    Plugin.Log.Info($"[TransitionPatcher] NonGen and Non BasedOn Map - Detected Boosts in SongCore. AlreadyUsingEnvColorBoost");
                }
                else
                {            
                    int colorBoostCount = 0;
                    // v2 maps store all beatmap events in "_events"
                    var events = beatmapObj["_events"] as JArray;
                    if (events != null)
                        colorBoostCount = events.Where(e => (int?)e["_type"] == 5).Count(); // ColorBoost events are type 5 in v2

                    int boosts = beatmapObj["colorBoostBeatmapEvents"] != null ? beatmapObj["colorBoostBeatmapEvents"].Count() : colorBoostCount;

                    if (boosts == 0)
                    {
                        JObject lightShowObj = null;
                        if (!string.IsNullOrWhiteSpace(lightShowJson))
                        {
                            lightShowObj = JObject.Parse(lightShowJson);
                            AlreadyUsingEnvColorBoost = lightShowObj["colorBoostEvents"] != null ? lightShowObj["colorBoostEvents"].Count() > 0 : false; //v4
                        }
                    }
                    else
                        AlreadyUsingEnvColorBoost = true;

                    Plugin.Log.Info($"[TransitionPatcher] NonGen and Non BasedOn Map - Detecting in beatmapJson or lightShowJson. AlreadyUsingEnvColorBoost = {AlreadyUsingEnvColorBoost}");
                }
                
                float songLength = beatmapLevel.songDuration;
                int _notesCount = beatmapObj["_notes"] != null ? beatmapObj["_notes"].Count() : 0; //2
                int noteCount = beatmapObj["colorNotes"] != null ? beatmapObj["colorNotes"].Count() : _notesCount; //v3 or 4

                NotesPerSecond = (songLength > 0f) ? (noteCount / songLength) : 0f;

                Plugin.Log.Info($"[TransitionPatcher] NonGen and Non BasedOn Map - Calculated NotesPerSecond: {NotesPerSecond} from {noteCount} notes over {songLength} seconds.");
            }


            CheckConflictingMods();
            //DetermineScoreSubmission();

            //string songHash = Collections.GetCustomLevelHash(beatmapLevel.levelID);

            ForceActivatePatches.MappingExtensionsForceActivate();

        }

        [HarmonyPatch(typeof(MenuTransitionsHelper), nameof(MenuTransitionsHelper.StartStandardLevel))]
        static class StartStandardLevel_FallbackGate
        {
            static void Postfix(/* capture what you need: level, diff, etc. */)
            {
                if (!ScoreGate.IsDisabledThisRun)
                {
                    string disabledText = TransitionPatcher.DetermineScoreSubmissionReason(BeatSageCleanUp.DisableScoreSubmission, 0);
                    if (!string.IsNullOrEmpty(disabledText))
                    {
                        ScoreGate.Set(disabledText);
                        Plugin.Log.Info($"[TransitionPatcher][ScoreGate][Fallback] Disabled – Reason: {ScoreGate.ReasonThisRun}");
                    }
                }
            }
        }


        // Doesn't check if the mod itself is self-enabled. so JDFixer and NJSFixer start disabled but will be considered enabled here.
        public static void CheckConflictingMods()
        {
            string str = "";

            if (Utils.IsModEnabled("JDFixer"))
                str = "JDFixer ";
            if (Utils.IsModEnabled("NJSFixer"))
                str += "NJSFixer ";
            if (Utils.IsModEnabled("LevelTweaks"))
                str += "LevelTweaks ";
            if (Utils.IsModEnabled("PracticePlugin"))
                str += "PracticePlugin ";

            if (Utils.IsModEnabled("JDFixer") || Utils.IsModEnabled("NJSFixer"))
                AutoNJSDisabledByConflictingMod = true;
            else
                AutoNJSDisabledByConflictingMod = false;

            if (Utils.IsModEnabled("PracticePlugin"))
                AutoNJSPracticeModeDisabledByConflictingMod = true;
            else
                AutoNJSPracticeModeDisabledByConflictingMod = false;

            //NjsTweaks (Quest)
            if (str != string.Empty)
                Plugin.Log.Info("[TransitionPatcher] Conficting Mods Enabled: " + str + ". Will Disable AutoNJS! (or practice mode for PracticePlugin");
        }

        public static string DetermineScoreSubmissionReason(bool beatSageDisableScoreSubmission, int chainsCount)
        {
            string str = "";

            if (SelectedSerializedName == GameModeHelper.GENERATED_360DEGREE_MODE)
            {
                if (Config.Instance.BasedOn != Config.Base.Standard)
                {
                    str = "Base Map Not Standard";
                }
                if (Config.Instance.RotationSpeedMultiplier < 0.3f)
                {
                    str += (str != "" ? ", " : "") + "Rotation Mult Low";
                }
                if (!Config.Instance.Wireless360 && Config.Instance.LimitRotations360 < 90)
                {
                    str += (str != "" ? ", " : "") + "Rotations Limited";
                }
            }

            if (BS_Utils.Plugin.LevelData.Mode == BS_Utils.Gameplay.Mode.Standard &&
                Utils.IsEnabledAutoNjsFixer() &&
                !AutoNJSDisabledByConflictingMod &&
                OriginalNoteJumpMovementSpeed > FinalNoteJumpMovementSpeed)
            {
                str += (str != "" ? ", " : "") + "Auto NJS Fixer";
            }

            if (Utils.IsEnabledChains() && !MapAlreadyUsesChains && chainsCount > 0)
            {
                str += (str != "" ? ", " : "") + "Architect Chains";
            }

            if (Config.Instance.EnableCleanBeatSage && (SetContent.IsBeatSageMap || IsBeatSageMap) && beatSageDisableScoreSubmission)
            {
                str += (str != "" ? ", " : "") + "Beat Sage Cleaner";
            }

            if (str != "")
                str = "AutoBS—" + str; // prefix once

            return str;
        }


        public static bool CheckForExternalModRequirement(BeatmapLevel level, string requirementName)
        {
            //Plugin.Log.Info($"Checking for {requirementName} for {level.levelID}.");

            var songCoreExtraData = SongCoreBridge.TryGetSongCoreSongData(level);

            if (songCoreExtraData == null)
            {
                Plugin.Log.Info("ExtraSongData not found for the given hash.");
                return false;
            }
            var difficultyData = songCoreExtraData?._difficulties.FirstOrDefault(d =>
                d._beatmapCharacteristicName == SelectedSerializedName &&
                d._difficulty == SelectedDifficulty);

            if (difficultyData.additionalDifficultyData._requirements.Contains(requirementName) || difficultyData.additionalDifficultyData._suggestions.Contains(requirementName))
            {
                Plugin.Log.Info($"[TransitionPatcher][CheckForExternalModRequirement] {requirementName} requirement/suggesion found.");
                //mapAlreadyUsesMappingExtensions = true;
                return true; // Found the requirement, no need to check further
            }


            Plugin.Log.Info($"[TransitionPatcher][CheckForExternalModRequirement] {requirementName} requirement/suggestion not found.");

            return false; // "Mapping Extensions" not found in any difficulty
        }
        
        //v1.40
        public static string GetEnvironmentName(BeatmapKey beatmapKey, BeatmapLevel beatmapLevel, OverrideEnvironmentSettings overrideEnvironmentSettings)
        {
            // If this is one of our Generated360 maps, always use GlassDesert:
            if (TransitionPatcher.UserSelectedMapToInject)
            {
                Plugin.Log.Info("[TransitionPatcher][GetEnvironmentname] Forcing GlassDesertEnvironment on injected 360 map");
                return "GlassDesertEnvironment";
            }


            string environmentName = beatmapLevel.GetEnvironmentName(beatmapKey.beatmapCharacteristic, beatmapKey.difficulty);

            EnvironmentType environmentType = EnvironmentType.Normal;
            if (beatmapKey.beatmapCharacteristic.requires360Movement) environmentType = EnvironmentType.Circle;

            if (overrideEnvironmentSettings != null && overrideEnvironmentSettings.overrideEnvironments)
            {
                EnvironmentInfoSO overrideEnv = overrideEnvironmentSettings.GetOverrideEnvironmentInfoForType(environmentType);
                if (overrideEnv != null)
                {
                    environmentName = overrideEnv.serializedName;
                    Plugin.Log.Info($"[TransitionPatcher][GetEnvironmentname] Using OVERRIDDEN environment: {environmentName}");
                }
                else
                {
                    Plugin.Log.Info($"[TransitionPatcher][GetEnvironmentname] No override found for environment type {environmentType}, using default: {environmentName}");
                }
            }

            return environmentName;
        }

        public static void LogSongCoreForKey(BeatmapKey key)
        {
            try
            {
                Plugin.Log.Info($"[LogSongCoreForKey] Selected BeatmapKey → {key.levelId}, {key.beatmapCharacteristic.serializedName}, {key.difficulty}");

                // Hash → SongCore extras
                var hash = SongCore.Collections.GetCustomLevelHash(key.levelId);
                var extras = SongCore.Collections.GetCustomLevelSongData(hash);
                if (extras == null)
                {
                    Plugin.Log.Warn("[LogSongCoreForKey] SongCore extras == null (hash not found?)");
                    return;
                }

                // Find the exact difficulty entry for THIS characteristic+difficulty
                var dd = extras._difficulties.FirstOrDefault(d =>
                                        d._beatmapCharacteristicName == key.beatmapCharacteristic.serializedName &&
                                        d._difficulty == key.difficulty);

                if (dd == null)
                {
                    Plugin.Log.Warn("[LogSongCoreForKey] No DifficultyData in SongCore for this BeatmapKey");
                    return;
                }

                var reqs = dd.additionalDifficultyData?._requirements ?? Array.Empty<string>();
                var sugg = dd.additionalDifficultyData?._suggestions ?? Array.Empty<string>();
                var warn = dd.additionalDifficultyData?._warnings ?? Array.Empty<string>();

                Plugin.Log.Info($"[LogSongCoreForKey] Requirements[{reqs.Length}]: {string.Join(", ", reqs)}");
                Plugin.Log.Info($"[LogSongCoreForKey] Suggestions[{sugg.Length}]: {string.Join(", ", sugg)}");
                Plugin.Log.Info($"[LogSongCoreForKey] Warnings[{warn.Length}]: {string.Join(", ", warn)}");

                // Show color scheme presence too (helps with Chroma diagnostics)
                if (extras._colorSchemes != null)
                    Plugin.Log.Info($"[LogSongCoreForKey] SongData._colorSchemes length: {extras._colorSchemes.Length}");
            }
            catch (Exception ex)
            {
                Plugin.Log.Error($"[LogSongCoreForKey] Exception while logging SongCore data: {ex}");
            }
        }

        public static void ProbeSongCoreEntry(string levelId)
        {
            var hashFromSC = SongCore.Collections.GetCustomLevelHash(levelId);
            Plugin.Log.Info($"[Probe] levelId={levelId}");
            Plugin.Log.Info($"[Probe] hashFromSC={hashFromSC ?? "<null>"}  AreSongsLoaded={SongCore.Loader.AreSongsLoaded}");

            // Fallback parser (robust vs casing/prefix)
            string parsed = null;
            const string prefix = "custom_level_";
            if (!string.IsNullOrEmpty(levelId) && levelId.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                parsed = levelId.Substring(prefix.Length);

            var hashUpper = parsed?.ToUpperInvariant();
            var hashLower = parsed?.ToLowerInvariant();

            // Try all three ways to get SongData
            var extrasA = hashFromSC != null ? SongCore.Collections.GetCustomLevelSongData(hashFromSC) : null;
            var extrasB = hashUpper != null ? SongCore.Collections.GetCustomLevelSongData(hashUpper) : null;
            var extrasC = hashLower != null ? SongCore.Collections.GetCustomLevelSongData(hashLower) : null;

            Plugin.Log.Info($"[Probe] extrasA(bySC)={(extrasA != null)} extrasB(UPPER)={(extrasB != null)} extrasC(lower)={(extrasC != null)}");
        }



    }
    #endregion

}
