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
    //runs after SetContent when you click play button - so this is the first place to know the chosen difficulty
    [HarmonyPatch(typeof(MenuTransitionsHelper))]
    public class TransitionPatcher
    {
        //v1.42 new TargetMethod
        static MethodBase TargetMethod()
        {
            // There is only one StartStandardLevel in 1.42, so this is stable.
            return AccessTools.DeclaredMethod(typeof(MenuTransitionsHelper), nameof(MenuTransitionsHelper.StartStandardLevel));
        }
        public static BeatmapKey SelectedPlayKey;
        public static BeatmapKey BasedOnKey;
        public static Version SelectedBeatmapVersion = new Version(2, 6, 0);
        public static bool UserSelectedMapToInject = false; // Only inject once, for the map the player actually "Starts" instead of all difficulties in the set
        public static string SelectedSerializedName;//will be "Generated360Degree" for gen 360
        public static BeatmapCharacteristicSO SelectedCharacteristicSO;
        public static BeatmapLevel SelectedBeatmapLevel;
        public static BeatmapDifficulty SelectedDifficulty;

        public static string EnvironmentName;
        public static ColorScheme theOverrideColorScheme;
        public static CustomBeatmapData beatMapDataCBforColorScheme;

        public static float OriginalNoteJumpMovementSpeed = 0;
        public static float FinalNoteJumpMovementSpeed = 0;
        public static float NoteJumpOffset; //noteJumpStartBeatOffset
        public static float FinalJumpDistance;
        public static bool AutoNJSDisabledByConflictingMod = false;
        public static bool AutoNJSPracticeModeDisabledByConflictingMod = false;

        public static float bpm;
        public static float NotesPerSecond;

        public static bool MapAlreadyUsesMappingExtensions = false; // used by generator360.cs to turn off automated extended walls for maps already using mapping extensions

        //v1.42 moved to EdibleCBD
        //public static bool MapAlreadyUsesEnvColorBoost = false;
        //public static bool MapAlreadyUsesChains = false;
        //public static bool MapAlreadyUsesArcs   = false;

        public static bool IsBeatSageMap = false;

        public static bool RequiresNoodle = false;
        public static bool RequiresChroma = false;
        public static bool RequiresVivify = false;

        public static bool NoodleProblemNotes = false;
        public static bool NoodleProblemObstacles = false;

        public static string ScoreSubmissionDisableText = "";

        //v1.42 parameter change
        static void Prefix(
            MenuTransitionsHelper __instance,
            string gameMode,
            in BeatmapKey beatmapKey,
            BeatmapLevel beatmapLevel,
            OverrideEnvironmentSettings? overrideEnvironmentSettings,
            ColorScheme? playerOverrideColorScheme,
            bool playerOverrideLightshowColors,
            GameplayModifiers gameplayModifiers,
            PlayerSpecificSettings playerSpecificSettings,
            PracticeSettings? practiceSettings,
            EnvironmentsListModel environmentsListModel,
            GameplayAdditionalInformation gameplayAdditionalInformation,
            Action? beforeSceneSwitchToGameplayCallback,
            Action<Zenject.DiContainer>? afterSceneSwitchToGameplayCallback,
            Action<StandardLevelScenesTransitionSetupDataSO, LevelCompletionResults>? levelFinishedCallback,
            Action<StandardLevelScenesTransitionSetupDataSO, LevelCompletionResults>? levelRestartedCallback,
            IBeatmapLevelData? beatmapLevelData = null,
            RecordingToolManager.SetupData? recordingToolData = null)
        {
            if (!Config.Instance.EnablePlugin) return;

            SelectedSerializedName = beatmapKey.beatmapCharacteristic.serializedName;

            if (!Utils.IsEnabledForGeneralFeatures()) return; // have to have the serialized name from TransitionPatcher for this to work

            ScoreGate.Clear();

            SelectedCharacteristicSO = beatmapKey.beatmapCharacteristic;
            SelectedDifficulty = beatmapKey.difficulty;
            SelectedBeatmapLevel = beatmapLevel;
            SelectedPlayKey = beatmapKey;
            bpm = beatmapLevel.beatsPerMinute;

            if (SelectedSerializedName == GameModeHelper.GENERATED_360DEGREE_MODE)
            {
                if (!SetContent.GeneratedToStandardKey.TryGetValue(beatmapKey.SerializedName(), out BasedOnKey))
                {
                    Plugin.LogDebug($"[TransitionPatcher] No generated→standard mapping for {beatmapKey.SerializedName()}");
                    var hasBasedOn = beatmapLevel.GetCharacteristics().Any(c => string.Equals(c.serializedName, basedOn, StringComparison.OrdinalIgnoreCase));
                    BeatmapCharacteristicSO BasedOnCharacteristicSO;
                    if (hasBasedOn)
                    {
                        BasedOnCharacteristicSO = beatmapLevel.GetCharacteristics().FirstOrDefault(c => c.serializedName == basedOn);
                        BasedOnKey = new BeatmapKey(beatmapKey.levelId, BasedOnCharacteristicSO, beatmapKey.difficulty);
                    }
                }
            }


            bool isCustomLevel = beatmapLevel.levelID.StartsWith("custom_level_");//v1.42


            Plugin.LogDebug(".");
            Plugin.LogDebug($"[TransitionPatcher] User selected:  {beatmapKey.beatmapCharacteristic.serializedName} {SelectedDifficulty} - ID: {beatmapKey.levelId} -------------------------------------"); // will be solo and standard, Generater360Degree, Generated90Degree, 360Degree, 90Degree, Lightshow, etc

            bool isGen360 = SelectedSerializedName == GameModeHelper.GENERATED_360DEGREE_MODE;
            UserSelectedMapToInject = isGen360;

            // using generated beatmapkey. same levelId is fine between standard and gen 360, but changing the characteristic (Standard → Generated360Degree) makes it a distinct beatmap for scoring and most plugins.
            Plugin.LogDebug($"[TransitionPatcher] Will inject? {UserSelectedMapToInject} for {beatmapKey.beatmapCharacteristic.serializedName} - {beatmapKey.difficulty}");

            // For noodle standard maps, need to do it this way. but this works for all maps so use this instead of the registry lookup. not checked in SetContent.
            if (isCustomLevel)
            {
                RequiresNoodle = CheckForExternalModRequirement(beatmapLevel, "Noodle Extensions");
                RequiresChroma = CheckForExternalModRequirement(beatmapLevel, "Chroma");
                RequiresVivify = CheckForExternalModRequirement(beatmapLevel, "Vivify");

                Plugin.LogDebug($"[TransitionPatcher] Requirements for this map: Noodle={RequiresNoodle}, Chroma={RequiresChroma}, Vivify={RequiresVivify}");
            }

            

            EnvironmentName = GetEnvironmentName(beatmapKey, beatmapLevel, overrideEnvironmentSettings);

            IsBeatSageMap = false;


            // BOTH of these are done in SetContent now for gen360 and basedOn maps. so only need to do it here for nonGen non basedOn maps. so should restore autoNjs registry for those maps only and can also leave beat sage in setcontent
            var basic = beatmapLevel.GetDifficultyBeatmapData(SelectedCharacteristicSO, SelectedDifficulty);
            if (basic != null)
            {
                OriginalNoteJumpMovementSpeed = NoteJumpMovementSpeed(SelectedDifficulty, basic.noteJumpMovementSpeed);
                NoteJumpOffset = basic.noteJumpStartBeatOffset;
                float originalJD;
                (FinalNoteJumpMovementSpeed, FinalJumpDistance, originalJD) = AutoNjsFixer.Fix(OriginalNoteJumpMovementSpeed, NoteJumpOffset, bpm);
                Plugin.LogDebug($"[TransitionPatcher] BasicBeatmapData - Original NJS: {OriginalNoteJumpMovementSpeed} Original NJO: {NoteJumpOffset}, Original JD: {originalJD} -- AutoNjsFixer NJS: {FinalNoteJumpMovementSpeed}, AutoNjsFixer JD: {FinalJumpDistance}");
                IsBeatSageMap = basic.mappers.Contains("Beat Sage");
                Plugin.LogDebug($"[TransitionPatcher] BasicBeatmapData - Beat Sage Map: {IsBeatSageMap}");
            }

            bool isBasedOn = SelectedSerializedName == basedOn;
            if (isGen360 || isBasedOn)
            {
                //v1.42 MapAlreadyUsesEnvColorBoost = AlreadyUsingEnvColorBoostRegistry.findByKey.TryGetValue(CurrentPlayKey, out var foundValue4) && foundValue4 == true;
                //MapAlreadyUsesArcs          = MapAlreadyUsesArcsRegistry.findByKey.TryGetValue(SelectedPlayKey, out var foundArcs) ? foundArcs : false;
                //MapAlreadyUsesChains        = MapAlreadyUsesChainsRegistry.findByKey.TryGetValue(SelectedPlayKey, out var foundChains) ? foundChains : false;

                NotesPerSecond = NotesPerSecRegistry.findByKey.TryGetValue(BasedOnKey, out var nps) ? nps : 0f;

                //Plugin.LogDebug($"[TransitionPatcher] Gen or BasedOn Map - Retrieved from Registries. AlreadyUsingEnvColorBoost: {MapAlreadyUsesEnvColorBoost}, MapAlreadyUsesArcs: {MapAlreadyUsesArcs}, MapAlreadyUsesChains: {MapAlreadyUsesChains}, NotesPerSecond: {NotesPerSecond}");
            }
            else 
            {
                //MapAlreadyUsesArcs   = false;
                //MapAlreadyUsesChains = false;
                //v1.42 MapAlreadyUsesEnvColorBoost = false;
                NotesPerSecond = 0;

                //v1.42
                string beatmapJson = ""; string lightshowJson = ""; string audioDataJson = ""; Version version = new Version();
                if (isCustomLevel)
                    (beatmapJson, lightshowJson, audioDataJson, version) = GetJsonForCustomLevel(beatmapLevel, SelectedDifficulty, SelectedPlayKey); //no longer works for built-in levels
                
                JObject beatmapObj = JObject.Parse(beatmapJson);

                //v1.42 moved to EditableCBD
                /*
                bool hasNormalSliders = beatmapObj["sliders"] != null ? beatmapObj["sliders"].Count() > 0 : false; // v3 (v2 can have sliders but i've never seen them and they don't work)
                bool hasArcs = beatmapObj["arcs"] != null ? beatmapObj["arcs"].Count() > 0 : false; //v4
                if (hasNormalSliders || hasArcs)
                {
                    MapAlreadyUsesArcs = true;
                    Plugin.LogDebug($"[TransitionPatcher] NonGen and Non BasedOn Map - Detected Original Arcs in JSON. MapAlreadyUsesArcs");
                }

                bool hasBurstSliders = beatmapObj["burstSliders"] != null ? beatmapObj["burstSliders"].Count() > 0 : false; //v3 (v2 has no burst sliders only arcs)
                bool hasChains = beatmapObj["chains"] != null ? beatmapObj["chains"].Count() > 0 : false; //v4
                if (hasBurstSliders || hasChains)
                {
                    MapAlreadyUsesChains = true;
                    Plugin.LogDebug($"[TransitionPatcher] NonGen and Non BasedOn Map - Detected Original Chains in JSON. MapAlreadyUsesChains");
                }
                */
                var songCoreExtraData = SongCoreBridge.TryGetSongCoreSongData(beatmapLevel);
                var difficultyData = songCoreExtraData?._difficulties.FirstOrDefault(d =>
                                        d._beatmapCharacteristicName == SelectedSerializedName &&
                                        d._difficulty == SelectedDifficulty);

                SetContent.SongFolderPath = SongFolderUtils.TryGetSongFolder(beatmapLevel.levelID);
                
                //v1.42
                /*
                if (songCoreExtraData != null && songCoreExtraData._difficulties != null && difficultyData != null &&
                        (difficultyData._envColorLeftBoost != null || difficultyData._envColorRightBoost != null))
                {
                    MapAlreadyUsesEnvColorBoost = true;
                    Plugin.LogDebug($"[TransitionPatcher] NonGen and Non BasedOn Map - Detected Boosts in SongCore. AlreadyUsingEnvColorBoost");
                }
                else
                {            
                    int colorBoostCount = 0;
                    
                    var events = beatmapObj["_events"] as JArray; // v2 maps store all beatmap events in "_events"
                    if (events != null)
                        colorBoostCount = events.Where(e => (int?)e["_type"] == 5).Count(); // ColorBoost events are type 5 in v2

                    int boosts = beatmapObj["colorBoostBeatmapEvents"] != null ? beatmapObj["colorBoostBeatmapEvents"].Count() : colorBoostCount;

                    if (boosts == 0)
                    {
                        JObject lightShowObj = null;
                        if (!string.IsNullOrWhiteSpace(lightShowJson))
                        {
                            lightShowObj = JObject.Parse(lightShowJson);
                            MapAlreadyUsesEnvColorBoost = lightShowObj["colorBoostEvents"] != null ? lightShowObj["colorBoostEvents"].Count() > 0 : false; //v4
                        }
                    }
                    else
                        MapAlreadyUsesEnvColorBoost = true;

                    Plugin.LogDebug($"[TransitionPatcher] NonGen and Non BasedOn Map - Detecting in beatmapJson or lightShowJson. AlreadyUsingEnvColorBoost = {MapAlreadyUsesEnvColorBoost}");
                }
                */
                float songLength = beatmapLevel.songDuration;
                int _notesCount = beatmapObj["_notes"] != null ? beatmapObj["_notes"].Count() : 0; //2
                int noteCount = beatmapObj["colorNotes"] != null ? beatmapObj["colorNotes"].Count() : _notesCount; //v3 or 4

                NotesPerSecond = (songLength > 0f) ? (noteCount / songLength) : 0f;

                Plugin.LogDebug($"[TransitionPatcher] NonGen and Non BasedOn Map - Calculated NotesPerSecond: {NotesPerSecond} from {noteCount} notes over {songLength} seconds.");
            }

            SelectedBeatmapVersion = BeatmapVersionRegistry.versionByKey.TryGetValue(BasedOnKey, out var v) ? v : new Version(0, 0, 0);
            if (!isCustomLevel && SelectedBeatmapVersion.Major == 0)
                SelectedBeatmapVersion = new Version(4, 0, 0);

            Plugin.LogDebug($"[TransitionPatcher] Map Version: v{SelectedBeatmapVersion}"); 

            CheckConflictingMods();

            ForceActivatePatches.MappingExtensionsForceActivate();

        }

        // Doesn't check if the mod itself is self-enabled. so JDFixer and NJSFixer may be disabled but will be considered enabled here.
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

            if (Utils.IsModEnabled("JDFixer") || Utils.IsModEnabled("NJSFixer")) // I tested this!!! installed is all that matters. can't detect if enabled or not.
                AutoNJSDisabledByConflictingMod = true;
            else
                AutoNJSDisabledByConflictingMod = false;

            if (Utils.IsModEnabled("PracticePlugin"))
                AutoNJSPracticeModeDisabledByConflictingMod = true;
            else
                AutoNJSPracticeModeDisabledByConflictingMod = false;

            //NjsTweaks (Quest)
            if (str != string.Empty)
                Plugin.LogDebug("[TransitionPatcher] Conficting Mods Enabled: " + str + ". Will Disable AutoNJS! (or practice mode for PracticePlugin");
        }

        public static bool CheckForExternalModRequirement(BeatmapLevel level, string requirementName)
        {
            //Plugin.Log.Info($"Checking for {requirementName} for {level.levelID}.");

            var songCoreExtraData = SongCoreBridge.TryGetSongCoreSongData(level);

            if (songCoreExtraData == null)
            {
                Plugin.LogDebug("ExtraSongData not found for the given hash.");
                return false;
            }
            var difficultyData = songCoreExtraData?._difficulties.FirstOrDefault(d =>
                d._beatmapCharacteristicName == SelectedSerializedName &&
                d._difficulty == SelectedDifficulty);

            if (difficultyData.additionalDifficultyData._requirements.Contains(requirementName) || difficultyData.additionalDifficultyData._suggestions.Contains(requirementName))
            {
                Plugin.LogDebug($"[TransitionPatcher][CheckForExternalModRequirement] {requirementName} requirement/suggesion found.");

                return true; // Found the requirement, no need to check further
            }


            Plugin.LogDebug($"[TransitionPatcher][CheckForExternalModRequirement] {requirementName} requirement/suggestion not found.");

            return false; // "Mapping Extensions" not found in any difficulty
        }
        
        //v1.40
        public static string GetEnvironmentName(BeatmapKey beatmapKey, BeatmapLevel beatmapLevel, OverrideEnvironmentSettings overrideEnvironmentSettings)
        {
            // If this is Generated360, always use GlassDesert:
            if (TransitionPatcher.UserSelectedMapToInject)
            {
                Plugin.LogDebug("[TransitionPatcher][GetEnvironmentname] Forcing GlassDesertEnvironment on injected 360 map");
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
                    Plugin.LogDebug($"[TransitionPatcher][GetEnvironmentname] Using OVERRIDDEN environment: {environmentName}");
                }
                else
                {
                    Plugin.LogDebug($"[TransitionPatcher][GetEnvironmentname] No override found for environment type {environmentType}, using default: {environmentName}");
                }
            }

            return environmentName;
        }

        public static void LogSongCoreForKey(BeatmapKey key)
        {
            try
            {
                Plugin.LogDebug($"[LogSongCoreForKey] Selected BeatmapKey → {key.levelId}, {key.beatmapCharacteristic.serializedName}, {key.difficulty}");

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

                Plugin.LogDebug($"[LogSongCoreForKey] Requirements[{reqs.Length}]: {string.Join(", ", reqs)}");
                Plugin.LogDebug($"[LogSongCoreForKey] Suggestions[{sugg.Length}]: {string.Join(", ", sugg)}");
                Plugin.LogDebug($"[LogSongCoreForKey] Warnings[{warn.Length}]: {string.Join(", ", warn)}");

                // Show color scheme presence too (helps with Chroma diagnostics)
                if (extras._colorSchemes != null)
                    Plugin.LogDebug($"[LogSongCoreForKey] SongData._colorSchemes length: {extras._colorSchemes.Length}");
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
