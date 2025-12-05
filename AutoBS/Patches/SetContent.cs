// custom colors are showing on beat sage maps for standard but not all gen 360 levels. weird. might be the override in color scheme
using BeatmapSaveDataVersion2_6_0AndEarlier;
using BeatmapSaveDataVersion3;
using CustomJSONData.CustomBeatmap;
using HarmonyLib;
using Newtonsoft.Json.Linq;
using SongCore;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading;
using UnityEngine;
using static IPA.Logging.Logger;
using DiffData = SongCore.Data.SongData.DifficultyData;
using MapColor = SongCore.Data.SongData.MapColor;
using SongData = SongCore.Data.SongData;

namespace AutoBS.Patches
{
    //StandardLevelDetailView.SetContent - Called when a song's been selected and its levels are displayed in the right menu
    //BW 1st item that runs. This calls GameModeHelper.cs and creates Standard map duplicate with 360 characteristics
    //Called when a song's been selected and its levels are displayed in the right menu (but not when another difficulty is selected from the same song)

    [HarmonyPatch(typeof(StandardLevelDetailView), nameof(StandardLevelDetailView.SetContent))]
    [HarmonyPatch(new Type[] {
        typeof(BeatmapLevel),
        typeof(BeatmapDifficultyMask),
        typeof(HashSet<BeatmapCharacteristicSO>),
        typeof(BeatmapDifficulty),
        typeof(BeatmapCharacteristicSO),
        typeof(PlayerData)
    })]

    public class SetContent
    {
        public static string SongName;

        public static string SongFolderPath; //path to the folder containing the song selected. used to output generated JSON files if needed

        public static string basedOn;

        public static bool IsCustomLevel = false;
        //public static int beatmapVersion;

        public static bool IsBeatSageMap = false;
        public static Dictionary<BeatmapDifficulty, CustomData> BeatmapCustomData = new Dictionary<BeatmapDifficulty, CustomData>();
        public static bool IsMappingExtensionsInstalled = false;

        //public static readonly Dictionary<BeatmapKey, BeatmapKey> generatedToStandardKey = new Dictionary<BeatmapKey, BeatmapKey>(); // don't need anymore! store the matching standard key for each generated key so when the player choses the gen 360 map we can give beatsaber the stardard corresponding key

        public static CustomBeatmapData cjBeatmapData;
        public static PlayerSpecificSettings PlayerSpecificSettings;

        static void Prefix(StandardLevelDetailView __instance,
                   BeatmapLevel level,
                   BeatmapDifficultyMask allowedBeatmapDifficultyMask,
                   HashSet<BeatmapCharacteristicSO> notAllowedCharacteristics,
                   BeatmapDifficulty defaultDifficulty,
                   BeatmapCharacteristicSO defaultBeatmapCharacteristic,
                   PlayerData playerData) // SetContent

        {
            if (!Config.Instance.EnablePlugin) return;


            SongName = level.songName;
            float bpm = level.beatsPerMinute;

            basedOn = "";
            //beatmapVersion = level.version; // will be -1!!!
            //float njs;
            //float njo;

            PlayerSpecificSettings = playerData.playerSpecificSettings;

            // Reset these since SetContent is called for new song selections and will cause error to try and add contents to same containers used by previous song

            //RequiresNoodle = new Dictionary<BeatmapDifficulty, bool>();
            //RequiresVivify = new Dictionary<BeatmapDifficulty, bool>();
            //AlreadyUsingEnvColorBoost = new Dictionary<BeatmapDifficulty, bool>();
            BeatmapCustomData = new Dictionary<BeatmapDifficulty, CustomData>();


            // === LOG GENERAL SONG INFO ===
            Plugin.Log.Info($".");
            Plugin.Log.Info($"[SetContent] Song Name: {level.songName} - {defaultBeatmapCharacteristic.serializedName} -------------------------------------------- ");
            Plugin.Log.Info($"[SetContent] LevelID: {level.levelID} BPM: {level.beatsPerMinute}, Duration: {level.songDuration}, Musician: {level.songAuthorName}"); // all accurate for custom and built-in levels

            IsMappingExtensionsInstalled = AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name == "MappingExtensions");
            if (IsMappingExtensionsInstalled)
            {
                Plugin.Log.Info("[SetContent] Mapping Extensions is installed.");
            }
            else
            {
                Plugin.Log.Info("[SetContent] Mapping Extensions is NOT installed.");
            }

            CreateGen360DifficultySet(level);


            // === Add Directional Markers to Menu Environment
            if (GlassEnvironmentFinder.Instance == null)// && !RequiresVivify)
            {
                new UnityEngine.GameObject("GlassEnvironmentFinder").AddComponent<GlassEnvironmentFinder>();
            }

        }

        public static class SongCoreBridge
        {
            public static SongData TryGetSongCoreSongData(BeatmapLevel level)
            {
                // Prefer hash (works for customs), fall back to levelID (covers edge cases)
                string hash = Collections.GetCustomLevelHash(level.levelID);
                var data = Collections.GetCustomLevelSongData(hash);
                if (data == null)
                    data = Collections.GetCustomLevelSongData(level.levelID);
                return data; // null for official maps; that’s fine.
            }
        }

        /// <summary>
        /// Utility for converting standard level difficulties set into a Gen 360 Difficulty set.
        /// Adds BasicData, Song Core Data, and BeatmapData
        /// Called by SetContent to create all difficulties independent of which difficulty is selected by the user.
        /// </summary>
        public static void CreateGen360DifficultySet(BeatmapLevel level)//, BeatmapCharacteristicSO characteristicSO, BeatmapDifficulty selectedDifficulty)
        {
            IsCustomLevel = level.levelID.StartsWith("custom_level_");

            if (IsCustomLevel)
                Plugin.Log.Info("[CreateGen360DifficultySet] Custom Level Found.");
            else
            {
                Plugin.Log.Info("[CreateGen360DifficultySet] Vanilla Built-in level Found.");

                BuiltInMapJsonLoader.PrimeBuiltInLevel(level.levelID);
            }

            // === BASESET LOGIC ===
            basedOn = (Config.Instance.BasedOn.ToString() == "NinetyDegree") ? "90Degree" : Config.Instance.BasedOn.ToString();
            basedOn = basedOn == "ThreeSixtyDegree" ? "360Degree" : basedOn;

            var hasBasedOn = level.GetCharacteristics().Any(c => string.Equals(c.serializedName, basedOn, StringComparison.OrdinalIgnoreCase));

            if (basedOn == "Standard" && !hasBasedOn)
            {
                Plugin.Log.Info($"[CreateGen360DifficultySet] Based On: {basedOn} NOT FOUND. Will try LAWLESS");
                if (basedOn == "Standard") basedOn = "Lawless";
            }

            BeatmapCharacteristicSO BasedOnCharacteristicSO = level.GetCharacteristics().FirstOrDefault(c => c.serializedName == basedOn);
            List<IDifficultyBeatmapSet> sets = level.GetCharacteristics()
                .Select(characteristic => new IDifficultyBeatmapSet
                {
                    characteristic = characteristic,
                    difficultyBeatmaps = level.GetDifficulties(characteristic)
                        .Select(difficulty => (difficulty, level.GetDifficultyBeatmapData(characteristic, difficulty)))
                        .ToList()
                }).ToList();

            float songLength = level.songDuration; // Or calculate from notes if needed
            float bpm = level.beatsPerMinute;

            // === SET UP GAMEMODE CLONES (360/90) ===

            // 1) Grab the SongCore-level SongData)
            //string songHash = Collections.GetCustomLevelHash(level.levelID);
            //Plugin.Log.Info($"[CreateGen360DifficultySet] Song Hash: {songHash} (levelID: {level.levelID})");
            var songCoreExtraData = SongCoreBridge.TryGetSongCoreSongData(level); // will work on song core's live cached object

            string[] mappers = level.allMappers ?? Array.Empty<string>(); //empty for vanilla but maybe newer songs have it
            string[] lighters = level.allLighters ?? Array.Empty<string>();//empty for vanilla

            if (songCoreExtraData != null)
            {
                // All contributors by role
                var contributorMappers = songCoreExtraData.contributors.Where(c => c._role?.ToLower() == "mapper").Select(c => c._name).ToArray();
                if (contributorMappers.Length > 0) mappers = contributorMappers;
                var contributorLighters = songCoreExtraData.contributors.Where(c => c._role?.ToLower() == "lighter").Select(c => c._name).ToArray();
                if (contributorLighters.Length > 0) lighters = contributorLighters;
                //var contributorAuthors = songCoreExtraData.contributors.Where(c => c._role?.ToLower() == "author").Select(c => c._name).ToArray();
                //if (contributorAuthors.Length > 0) authors = contributorAuthors;
            }

            //Plugin.Log.Info($"[CreateGen360DifficultySet] Authors: {string.Join(", ", authors)}");
            Plugin.Log.Info($"[CreateGen360DifficultySet] Mappers: {string.Join(", ", mappers)}"); //empty for vanilla
            Plugin.Log.Info($"[CreateGen360DifficultySet] Lighters: {string.Join(", ", lighters)}"); //empty for vanilla

            IsBeatSageMap = false; // will be true for all difficulties

            if (mappers.Contains("Beat Sage"))
            {
                Plugin.Log.Info($"[CreateGen360DifficultySet] Beat Sage map!");
                IsBeatSageMap = true;
            }
            else
            {
                Plugin.Log.Info($"[CreateGen360DifficultySet] Not a Beat Sage map.");
                IsBeatSageMap = false;
            }

            // Determine the base gamemode name.
            var baseSet = sets.FirstOrDefault(s => s.characteristic.serializedName == basedOn);

            // Search for the base set in the existing difficulty beatmap sets.
            //If such an object is found, it is assigned to the basedOnGameMode variable. If not, basedOnGameMode will be null.
            if (baseSet == null || baseSet.difficultyBeatmaps.Count == 0)
            {
                Plugin.Log.Error("[CreateGen360DifficultySet] No base set found for copy!");
                return;
            }

            List<BeatmapCharacteristicSO> toGenerate = new List<BeatmapCharacteristicSO>(); // empty set


            if (Config.Instance.Enable360fyer)//!RequiresVivify)
                toGenerate.Add(GameModeHelper.GetGenerated360GameMode()); // this should set 360 requirments
            //if (Config.Instance.ShowGenerated90 && !RequiresVivify)
            //    toGenerate.Add(GameModeHelper.GetGenerated90GameMode());

            // Generate each custom gamemode from the list in 'toGenerate'
            foreach (var customGameMode in toGenerate) // toGenerate should have all the 360 characteristics already
            {

                var diffs = level.GetDifficulties(customGameMode)?.ToArray() ?? Array.Empty<BeatmapDifficulty>();

                if (diffs.Length == 0)
                    Plugin.Log.Warn($"[CreateGen360DifficultySet] {customGameMode.serializedName} set is empty/missing; generating now.");
                else
                {
                    Plugin.Log.Info($"[CreateGen360DifficultySet] {customGameMode.serializedName} already exists with {diffs.Length} diffs; skipping generation.");
                    continue;
                }

                // --- Only generate BeatmapData for the generated modes! ---
                if (customGameMode.serializedName != GameModeHelper.GENERATED_360DEGREE_MODE)
                {
                    continue; // Skip non-generated modes
                }

                //Plugin.Log.Info($"\nBW LevelUpdatePatcher toGenerate Count: {toGenerate.Count}");
                //JObject jObj = null;

                var newDiffs = new List<(BeatmapDifficulty difficulty, BeatmapBasicData data)>();

                foreach (var (difficulty, origBasicData) in baseSet.difficultyBeatmaps)
                {
                    Plugin.Log.Info($"[CreateGen360DifficultySet] -- {difficulty} START -----------------------");

                    //var songCoreExtraDataByDiff = GetExtraSongData(level, BasedOnCharacteristicSO, difficulty, songCoreExtraData); // for basedOn standard level!
                    var difficultyData = songCoreExtraData?._difficulties.FirstOrDefault(d =>
                                        d._beatmapCharacteristicName == BasedOnCharacteristicSO.serializedName &&
                                        d._difficulty == difficulty);

                    var additionalDiffData = difficultyData?.additionalDifficultyData;

                    if (additionalDiffData != null && additionalDiffData._requirements.Contains("Vivify"))
                    {
                        Plugin.Log.Error($"[CreateGen360DifficultySet] -- Vivify required for {level.songName} difficulty {difficulty}. Skipping.");
                        continue; // Skip this difficulty if Vivify is required
                    }

                    float originalNJS = 0;
                    float originalNJO = 0;

                    // Figure out the effective SongCore scheme for this based-on difficulty
                    (var effectiveSongCoreScheme, bool usesAuthorCustomColors) = ColorExtensions.GetEffectiveSongCoreScheme(songCoreExtraData, difficultyData);

                    // Convert to Beat Saber scheme (your helper handles null -> defaults)
                    var beatSaberColorScheme = ColorExtensions.ConvertToBeatSaberColorScheme(effectiveSongCoreScheme);

                    if (usesAuthorCustomColors)
                        Plugin.Log.Info($"[CreateGen360DifficultySet] -- {difficulty} uses author Custom Colors.");
                    else
                        Plugin.Log.Info($"[CreateGen360DifficultySet] -- {difficulty} NO author Custom Colors found.");


                    //if (difficulty != defaultDifficulty) continue; // only process the selected difficulty -- this causes every new map selected to only have the difficulty available in Gen360 the same as the first map selected

                    Plugin.Log.Info($"[CreateGen360DifficultySet] -- BeatmapBasicData for {baseSet.characteristic.serializedName}-{difficulty} NJS: {origBasicData.noteJumpMovementSpeed} NJO: {origBasicData.noteJumpStartBeatOffset}");

                    // no other meta data is populated at this time so need JSON for the rest.

                    //Plugin.Log.Info($"   Env: {(origData.environmentName != null ? origData.environmentName.ToString() : "null")}");
                    //Plugin.Log.Info($"   ColorSchemeID: {(origData.beatmapColorScheme != null ? origData.beatmapColorScheme.colorSchemeId : "null")}");
                    //Plugin.Log.Info($"   NotesCount: {origData.notesCount}");
                    //Plugin.Log.Info($"   CuttableObjectsCount: {origData.cuttableObjectsCount}");
                    //Plugin.Log.Info($"   ObstaclesCount: {origData.obstaclesCount}");
                    //Plugin.Log.Info($"   BombsCount: {origData.bombsCount}");
                    //if (origData.mappers != null)
                    //    Plugin.Log.Info($"   Mappers: {string.Join(", ", origData.mappers)}");
                    //if (origData.lighters != null)
                    //   Plugin.Log.Info($"   Lighters: {string.Join(", ", origData.lighters)}");

                    int noteCount = 0;
                    int obstacleCount = 0;
                    int bombCount = 0;
                    CustomData levelCustomData = new CustomData();     // the info.dat _customData
                    CustomData beatmapCustomData = new CustomData();

                    // 1) Build the true Standard key for this difficulty:
                    var stdKey = new BeatmapKey(
                        level.levelID,
                        baseSet.characteristic,    // e.g. “Standard”
                        difficulty                 // e.g. Normal, Hard, Expert…
                    );

                    // 2) Build the Generated360 key for the same difficulty:
                    var genKey = new BeatmapKey(
                        level.levelID,
                        customGameMode,            // i.e. GameModeHelper.GetGenerated360GameMode()
                        difficulty
                    );

                    if (songCoreExtraData != null && songCoreExtraData._difficulties != null &&
                        (difficultyData._envColorLeftBoost != null || difficultyData._envColorRightBoost != null))
                    {
                        Plugin.Log.Info($"[CreateGen360DifficultySet] -- {difficulty} Author already uses _envColorLeftBoost/_envColorRightBoost.");
                        AlreadyUsingEnvColorBoostRegistry.findByKey[genKey] = true; AlreadyUsingEnvColorBoostRegistry.findByKey[stdKey] = true;
                    }
                    else
                    {
                        Plugin.Log.Info($"[CreateGen360DifficultySet] -- {difficulty} Author NOT using _envColorLeftBoost/_envColorRightBoost.");
                        AlreadyUsingEnvColorBoostRegistry.findByKey[genKey] = false; AlreadyUsingEnvColorBoostRegistry.findByKey[stdKey] = false;
                    }



                    //no longer needed i think
                    //generatedToStandardKey[genKey] = stdKey;
                    //Plugin.Log.Info($"[CreateGen360DifficultySet] -- mapped GEN→STD: {genKey.beatmapCharacteristic.serializedName}-{genKey.difficulty} → {stdKey.beatmapCharacteristic.serializedName}-{stdKey.difficulty}");

                    (string beatmapJson, string lightshowJson, string audioDataJson, Version version) = GetJson(level, difficulty, stdKey); //works for custom and built-in levels

                    if (!string.IsNullOrEmpty(beatmapJson))// v2/v3 custom map have no lightshowJson && !string.IsNullOrEmpty(lightshowJson))
                    {
                        Plugin.Log.Info($"[CreateGen360DifficultySet] -- Difficulty: {difficulty} JSON length={beatmapJson.Length}");

                        BeatmapDataRegistry.versionByKey[stdKey] = version; BeatmapDataRegistry.versionByKey[genKey] = version;

                        noteCount = 0;
                        bombCount = 0;
                        obstacleCount = 0;

                        int chainsCount = 0;
                        int eventsCount = 0;

                        var env = GetUsableEnvironment(basedOn);


                        if (env == null)
                        {
                            Plugin.Log.Error("[CreateGen360DifficultySet] No EnvironmentInfoSO found; cannot load beatmap.");
                            return;
                        }

                        string defaultLightshowJson = null;

                        var envDefaultTA = env.defaultLightshowAsset; // TextAsset (e.g., "Static.lightshow")
                        if (envDefaultTA != null)
                        {
                            if (BuiltInMapJsonLoader_TryRead.TryReadTextAssetText(envDefaultTA, out var dflJson))
                            {
                                defaultLightshowJson = dflJson;
                                //Plugin.Log.Info($"[CreateGen360DifficultySet] env default lightshow len={defaultLightshowJson.Length} name='{envDefaultTA.name}'");
                            }
                            else
                            {
                                //Plugin.Log.Warn("[CreateGen360DifficultySet] Could not read env.defaultLightshowAsset; falling back to per-diff lightshow as default.");
                                defaultLightshowJson = lightshowJson; // last resort
                            }
                        }
                        else
                        {
                            Plugin.Log.Warn("[CreateGen360DifficultySet] env.defaultLightshowAsset is NULL; falling back to per-diff lightshow as default.");
                            defaultLightshowJson = lightshowJson; // last resort
                        }

                        //Plugin.Log.Info($"[V4] lightEventConverter built: {(lightEventConverter == null ? "NULL" : lightEventConverter.GetType().FullName)}");

                        //LogBeatmapV4Inputs(audioDataJson, beatmapJson, lightshowJson, defaultLightshowJson, difficulty, env, SetContent.PlayerSpecificSettings);

                        // now call the static loader to get a real BeatmapData
                        BeatmapData originalBeatmapData;
                        if (version.Major == 2)
                        {
                            //according to chatGPT CustomJSONData.HarmonyPatches.BeatmapDataLoaderV2_6_0AndEarlierCustomify  CustomJSONData.HarmonyPatches.BeatmapDataLoaderV3Customify have patched the BeatmapDataLoader to keep the customData
                            originalBeatmapData = BeatmapDataLoaderVersion2_6_0AndEarlier.BeatmapDataLoader
                                .GetBeatmapDataFromSaveDataJson(
                                    beatmapJson,
                                    defaultLightshowJson,
                                    difficulty,
                                    level.beatsPerMinute,
                                    false,
                                    env, //null works
                                    BeatmapLevelDataVersion.Original,
                                    SetContent.PlayerSpecificSettings,
                                    new NoOpLightEventConverter()
                                );
                        }
                        else if (version.Major == 3)
                        {
                            originalBeatmapData = BeatmapDataLoaderVersion3.BeatmapDataLoader
                                .GetBeatmapDataFromSaveDataJson(
                                    beatmapJson,
                                    defaultLightshowJson,
                                    difficulty,
                                    level.beatsPerMinute,
                                    false,
                                    env, //null works
                                    BeatmapLevelDataVersion.Original,
                                    SetContent.PlayerSpecificSettings,
                                    new NoOpLightEventConverter()
                                );
                        }
                        else // v4 is the only one that crashes with null lightEventConverter. if null, then can't make built-in maps
                        {
                            originalBeatmapData = BeatmapDataLoaderVersion4.BeatmapDataLoader.GetBeatmapDataFromSaveDataJson(
                                audioDataJson,
                                lightshowJson: lightshowJson,
                                defaultLightshowJson: defaultLightshowJson,
                                beatmapJson: beatmapJson,
                                beatmapDifficulty: difficulty,
                                loadingForDesignatedEnvironment: false,
                                targetEnvironmentInfo: env,
                                originalEnvironmentInfo: env,
                                beatmapLevelDataVersion: BeatmapLevelDataVersion.Original,
                                gameplayModifiers: null,
                                playerSpecificSettings: SetContent.PlayerSpecificSettings,
                                lightEventConverter: new NoOpLightEventConverter()
                            );
                        }

                        int originalBpmEventsCount = originalBeatmapData.allBeatmapDataItems.OfType<BpmChangeEventData>().Count();
                        int customBpmEventsCount = 0;
                        if (IsCustomLevel && version.Major < 4) // CustomJsonData does not support v4
                        {
                            //if (version.Major >= 4)
                            //{
                            //    Plugin.Log.Info($"[CreateGen360DifficultySet] -- Retrieved BeatmapData v{version} from JSON - {difficulty}. v4 custom maps are not suported;
                            //    return; // custom levels in v4 now exist "Ascension to Heaven" with NJS events, but CustomJsonData does not support v4
                            //}


                            CustomBeatmapData customBeatmapData = originalBeatmapData as CustomBeatmapData;

                            customBpmEventsCount = customBeatmapData.allBeatmapDataItems.OfType<CustomBPMChangeBeatmapEventData>().Count();

                            BeatmapDataRegistry.beatmapDataByKey[genKey] = customBeatmapData;

                            noteCount = customBeatmapData.allBeatmapDataItems
                                .OfType<NoteData>()
                                .Count(n => n.colorType != ColorType.None); // excludes bombs

                            // Count bombs separately
                            bombCount = customBeatmapData.allBeatmapDataItems
                                .OfType<NoteData>()
                                .Count(n => n.colorType == ColorType.None);

                            chainsCount = customBeatmapData.allBeatmapDataItems
                                .OfType<SliderData>()
                                .Count(s => s.sliderType == SliderData.Type.Burst);

                            eventsCount = originalBeatmapData.allBeatmapDataItems.OfType<BasicBeatmapEventData>().Count() + originalBeatmapData.allBeatmapDataItems.OfType<EventData>().Count();

                            //(int)b.cutDirection == (int)NoteCutDirection.None).Count();//cutDirection not working for this
                            //noteCount -= bombCount; // subtract bombs from notes count // if (note["_type"]?.Value<int>() == 3)
                            obstacleCount = customBeatmapData.allBeatmapDataItems.OfType<ObstacleData>().Count();//cjdSaveData.obstacles?.Count ?? 0;

                            levelCustomData = customBeatmapData.levelCustomData;     // the info.dat _customData

                            beatmapCustomData = customBeatmapData.beatmapCustomData;   // the beatmap _customData
                        }
                        else
                        {
                            BeatmapDataRegistry.beatmapDataByKey[genKey] = originalBeatmapData;

                            // Count total notes (excluding bombs)
                            noteCount = originalBeatmapData.allBeatmapDataItems
                                .OfType<NoteData>()
                                .Count(n => n.colorType != ColorType.None); // excludes bombs

                            // Count bombs separately
                            bombCount = originalBeatmapData.allBeatmapDataItems
                                .OfType<NoteData>()
                                .Count(n => n.colorType == ColorType.None);

                            chainsCount = originalBeatmapData.allBeatmapDataItems
                                .OfType<SliderData>()
                                .Count(s => s.sliderType == SliderData.Type.Burst);

                            eventsCount = originalBeatmapData.allBeatmapDataItems.OfType<BasicBeatmapEventData>().Count() + originalBeatmapData.allBeatmapDataItems.OfType<EventData>().Count(); // not sure if this is correct really

                            //(int)b.cutDirection == (int)NoteCutDirection.None).Count();//cutDirection not working for this
                            //noteCount -= bombCount; // subtract bombs from notes count // if (note["_type"]?.Value<int>() == 3)
                            obstacleCount = originalBeatmapData.allBeatmapDataItems.OfType<ObstacleData>().Count();//cjdSaveData.obstacles?.Count ?? 0;
                        }


                        Plugin.Log.Info($"[CreateGen360DifficultySet] -- Retrieved BeatmapData v{version} from JSON - {difficulty} - notes: {noteCount} bombs: {bombCount} obstacles: {obstacleCount} events: {eventsCount} bpm change events: {originalBpmEventsCount} {customBpmEventsCount}.");

                        if (chainsCount > 0)
                        {
                            MapAlreadyUsesChainsRegistry.findByKey[stdKey] = true;
                            MapAlreadyUsesChainsRegistry.findByKey[genKey] = true;
                        }
                        else
                        {
                            MapAlreadyUsesChainsRegistry.findByKey[stdKey] = false;
                            MapAlreadyUsesChainsRegistry.findByKey[genKey] = false;
                        }


                        float notesPerSecond = (songLength > 0f) ? (noteCount / songLength) : 0f;

                        NotesPerSecRegistry.findByKey[genKey] = notesPerSecond;// NotesPerSecRegistry.findByKey[stdKey] = notesPerSecond;

                        MenuDataRegistry.statsByKey[genKey] =
                            new MenuDataRegistry.Stats
                            {
                                notesCount = noteCount,
                                obstaclesCount = obstacleCount,
                                bombsCount = bombCount,
                                notesPerSecond = notesPerSecond,
                            };

                        if (origBasicData.noteJumpMovementSpeed == 0)
                            Plugin.Log.Info($"[CreateGen360DifficultySet] -- original default NJS from json: 0 so must be computed from difficulty chart.");

                        originalNJS = NoteJumpMovementSpeed(difficulty, origBasicData.noteJumpMovementSpeed); // some built-in levels send a default of 0
                        originalNJO = origBasicData.noteJumpStartBeatOffset;

                        //These are all accurate (custom maps)
                        Plugin.Log.Info($"[CreateGen360DifficultySet] -- Notes Per Second: {notesPerSecond}");
                        Plugin.Log.Info($"[CreateGen360DifficultySet] -- bpm: {bpm}");
                        Plugin.Log.Info($"[CreateGen360DifficultySet] -- original NJS: {originalNJS} (may be recomputed from defaults for built-in levels)");
                        Plugin.Log.Info($"[CreateGen360DifficultySet] -- original NJO: {originalNJO}");
                        /*
                        (float autoNJS, float autoJD, float originalJD) = AutoNjsFixer.Fix(originalNJS, originalNJO, level.beatsPerMinute);

                        float finalNJS = autoNJS > 0f ? autoNJS : originalNJS;
                        float finalJD = autoJD > 0f ? autoJD : originalJD;

                        if (!Utils.IsEnabledAutoNjsFixer())
                        {
                            finalNJS = originalNJS;
                            finalJD = originalJD;
                        }
                        
                        AutoNjsRegistry.byKey[genKey] =
                            new AutoNjsRegistry.Data
                            {
                                original_njs = originalNJS,
                                original_jd = originalJD,
                                original_njo = originalNJO,
                                autoNjs_njs = finalNJS,
                                autoNjs_jd = finalJD
                            };
                        AutoNjsRegistry.byKey[stdKey] =
                            new AutoNjsRegistry.Data
                            {
                                original_njs = originalNJS,
                                original_jd = originalJD,
                                original_njo = originalNJO,
                                autoNjs_njs = finalNJS,
                                autoNjs_jd = finalJD
                            };
                        */
                        Plugin.Log.Info($"[CreateGen360DifficultySet] -- original NJS: {originalNJS} (may be recomputed from defaults for built-in levels)");// -- final NJS: {finalNJS} if use AutoNjsFixer");
                        Plugin.Log.Info($"[CreateGen360DifficultySet] -- original NJO: {originalNJO}");
                        //Plugin.Log.Info($"[CreateGen360DifficultySet] -- original  JD: {originalJD}  --  final JD: {finalJD} if use AutoNjsFixer)");

                    }


                    if (IsCustomLevel)
                    {
                        // not needed i think
                        // === INJECT EXTRA SONG DATA FOR GENERATED 360 DEGREE MODE === intented to be used by the game to display stats in the menu and gameplay but it is not working
                        // 1) Grab the SongCore-level SongData (this is never null, since you saw “FOUND”)
                        //string songHash = Collections.GetCustomLevelHash(level.levelID);


                        // 2) Log what we’ve got (for debug)

                        //var songCoreSongData = Collections.GetCustomLevelSongData(songHash);
                        // Try hash first, then levelID

                        var songCorediffs = songCoreExtraData._difficulties ?? Array.Empty<DiffData>();

                        // Even if SongCore says it exists, ensure Level has BasicData
                        var levelHasThisGenDiff =
                            (level.GetDifficulties(customGameMode)?.Contains(difficulty) ?? false);

                        if (levelHasThisGenDiff)
                        {
                            Plugin.Log.Info($"[CreateGen360DifficultySet] -- Level already has {GameModeHelper.GENERATED_360DEGREE_MODE}-{difficulty}, skipping clone.");
                        }
                        else
                        {
                            var baseEntry = songCorediffs.FirstOrDefault(dd => dd._beatmapCharacteristicName == basedOn && dd._difficulty == difficulty);

                            if (baseEntry == null)
                            {
                                Plugin.Log.Warn($"[CreateGen360DifficultySet] -- Could not find base stats for char={basedOn}, diff={difficulty}");
                            }
                            else
                            {
                                // Build fresh RequirementData = union(base + standard) and force NE if needed
                                var reqs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                                var suggs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                                var warns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                                var info = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                                void Union(SongData.RequirementData r)
                                {
                                    if (r == null) return;
                                    if (r._requirements != null) foreach (var x in r._requirements) reqs.Add(x);
                                    if (r._suggestions != null) foreach (var x in r._suggestions) suggs.Add(x);
                                    if (r._warnings != null) foreach (var x in r._warnings) warns.Add(x);
                                    if (r._information != null) foreach (var x in r._information) info.Add(x);
                                }
                                Union(additionalDiffData);

                                Plugin.Log.Info($"[CreateGen360DifficultySet] -- Base requirements: [{string.Join(",", reqs)}]");
                                Plugin.Log.Info($"[CreateGen360DifficultySet] -- Base suggestions: [{string.Join(",", suggs)}]");

                                var reqArr = reqs.Count == 0 ? Array.Empty<string>() : reqs.ToArray();
                                var suggsArr = suggs.Count == 0 ? Array.Empty<string>() : suggs.ToArray();
                                var warnsArr = warns.Count == 0 ? Array.Empty<string>() : warns.ToArray();
                                var infoArr = info.Count == 0 ? Array.Empty<string>() : info.ToArray();

                                //Requirements[difficulty] = reqArr;
                                //Suggestions[difficulty] = suggsArr;

                                // do for genKey and stdKey since TransitionPatcher will still make decisions based on the actual key.
                                //RequirementsRegistry.findByKey[genKey] = reqArr; //RequirementsRegistry.findByKey[stdKey] = reqArr;
                                //SuggestionsRegistry.findByKey[genKey] = suggsArr; //SuggestionsRegistry.findByKey[stdKey] = suggsArr;
                                //MapAlreadyUsesMappingExtensionsRegistry.findByKey[genKey] = reqArr.Contains("Mapping Extensions") || reqArr.Contains("MappingExtensions");
                                //MapAlreadyUsesMappingExtensionsRegistry.findByKey[stdKey] = reqArr.Contains("Mapping Extensions") || reqArr.Contains("MappingExtensions");

                                //Plugin.Log.Info($"[CreateGen360DifficultySet] Requires Noodle: {RequirementsRegistry.findByKey[genKey].Contains("Noodle")} 2: {RequirementsRegistry.findByKey[genKey].Contains("Noodle Extensions")} 3: {RequirementsRegistry.findByKey.TryGetValue(genKey, out var foundValue) && foundValue.Any(r => r.Contains("Noodle", StringComparison.OrdinalIgnoreCase))} 4: {RequirementsRegistry.findByKey.TryGetValue(genKey, out var foundValue1) && foundValue1.Any(r => r.Contains("Noodle Extensions", StringComparison.OrdinalIgnoreCase))}");

                                var addedData = new SongData.RequirementData
                                {
                                    _requirements = reqArr, //Array.Empty<string>(),
                                    _suggestions = suggsArr, //Array.Empty<string>(),
                                    _warnings = warnsArr,
                                    _information = infoArr
                                };

                                var src = baseEntry; // pick something non-null for cosmetic fields
                                var clone = new DiffData // meta data but doesn't change map colors. basic data color scheme effects colors
                                {
                                    _beatmapCharacteristicName = GameModeHelper.GENERATED_360DEGREE_MODE,
                                    _difficulty = difficulty,
                                    _difficultyLabel = src?._difficultyLabel,
                                    additionalDifficultyData = addedData, // requirements and this works
                                    _colorLeft = src?._colorLeft,// != null ? src?._colorLeft : new MapColor(1,0,0), // will cause map to say its got a custom color scheme even if it doesn't
                                    _colorRight = src?._colorRight,// != null ? src?._colorRight : new MapColor(0, 0, 1),
                                    _envColorLeft = src?._envColorLeft,
                                    _envColorRight = src?._envColorRight,
                                    _envColorWhite = src?._envColorWhite,
                                    _envColorLeftBoost = src?._envColorLeftBoost,
                                    _envColorRightBoost = src?._envColorRightBoost,
                                    _envColorWhiteBoost = src?._envColorWhiteBoost,
                                    _obstacleColor = src?._obstacleColor,
                                    _beatmapColorSchemeIdx = -1,
                                    _environmentNameIdx = src?._environmentNameIdx,
                                    _oneSaber = src?._oneSaber,
                                    _showRotationNoteSpawnLines = src?._showRotationNoteSpawnLines,
                                    _styleTags = src?._styleTags
                                };
                                Plugin.Log.Info($"[CreateGen360DifficultySet] -- Cloned DifficultyData. envronementNameIdx: {clone._environmentNameIdx} _colorLeft: {clone._colorLeft} _colorRight: {clone._colorRight} _envColorLeft: {clone._envColorLeft}, Right: {clone._envColorRight},White: {clone._envColorWhite}");

                                //ColorExtensions.FillMissingWithDefaults(clone);


                                var newList = songCorediffs.ToList();
                                newList.Add(clone);
                                songCoreExtraData._difficulties = newList.ToArray();

                                Plugin.Log.Info($"[CreateGen360DifficultySet] -- Added {GameModeHelper.GENERATED_360DEGREE_MODE}-{difficulty} with reqs=[{string.Join(",", addedData._requirements)}]");

                                SetContent.BeatmapCustomData.Add(difficulty, beatmapCustomData);

                                //beatSaberColorScheme = null; // if use null, will give the game default scheme for the environment. strangely, GlassDesertEnvironment uses yellow/majenta by default

                                //EnvironmentName environmentName = "GlassDesertEnvironment";

                                var basicData = new CustomBeatmapBasicData(
                                    origBasicData.noteJumpMovementSpeed,
                                    origBasicData.noteJumpStartBeatOffset,
                                    "GlassDesertEnvironment",    // your 360 environment
                                    beatSaberColorScheme,     // This is where the colors are really controlled
                                    noteCount,
                                    noteCount,
                                    obstacleCount,
                                    bombCount,
                                    mappers,
                                    lighters,
                                    levelCustomData,
                                    beatmapCustomData
                                );
                                // Register for main game so menu and loader recognize it:
                                level.AddBeatmapBasicData(customGameMode, difficulty, basicData);

                                // Now add to your local newDiffs list for later
                                newDiffs.Add((difficulty, basicData));

                                Plugin.Log.Info($"[CreateGen360DifficultySet] -- Created BeatmapBasicData for {customGameMode.serializedName}-{difficulty}: notes={noteCount}, obstacles={obstacleCount}, bombs={bombCount}");
                                Plugin.Log.Info($"[CreateGen360DifficultySet] -- {difficulty} END -------------------");
                            }
                        }
                    }
                    else // BUILT-IN vanilla
                    {
                        //string beatmapJson = ExternalFileReader.ReadTextAsync(beatmapPath).GetAwaiter().GetResult();
                        //string lightshowJson = ExternalFileReader.ReadTextAsync(lightshowPath).GetAwaiter().GetResult();

                        MapAlreadyUsesChainsRegistry.findByKey[stdKey] = false;
                        MapAlreadyUsesChainsRegistry.findByKey[genKey] = false;
                        //RequirementsRegistry.findByKey[genKey] = Array.Empty<string>(); RequirementsRegistry.findByKey[stdKey] = Array.Empty<string>();
                        //SuggestionsRegistry.findByKey[genKey] = Array.Empty<string>(); SuggestionsRegistry.findByKey[stdKey] = Array.Empty<string>();
                        //MapAlreadyUsesMappingExtensionsRegistry.findByKey[genKey] = false;
                        //MapAlreadyUsesMappingExtensionsRegistry.findByKey[stdKey] = false;

                        var vanillaBeatSaberColorScheme = ColorExtensions.ConvertToBeatSaberColorScheme(null);
                        var basicData = new CustomBeatmapBasicData(
                                        originalNJS,
                                        originalNJO,
                                        "GlassDesertEnvironment",    // your 360 environment
                                        vanillaBeatSaberColorScheme,     // color scheme
                                        noteCount,
                                        noteCount,
                                        obstacleCount,
                                        bombCount,
                                        mappers,
                                        lighters,
                                        levelCustomData,
                                        beatmapCustomData
                                    );

                        // Register for main game so menu and loader recognize it:
                        level.AddBeatmapBasicData(customGameMode, difficulty, basicData);
                        //Plugin.Log.Info($"[CreateGen360DifficultySet] Added fake content for Built-in map!!!!!!!!!!!!");
                        // Now add to your local newDiffs list for later
                        newDiffs.Add((difficulty, basicData));
                    }

                }


                // Register new set
                if (newDiffs.Count > 0)
                {
                    sets.Add(new IDifficultyBeatmapSet
                    {
                        characteristic = customGameMode,
                        difficultyBeatmaps = newDiffs
                    });
                    Plugin.Log.Info($"[CreateGen360DifficultySet] Added set for {customGameMode.serializedName}, {newDiffs.Count} difficulties. FINISHED!");
                }
                else
                {
                    Plugin.Log.Warn($"[CreateGen360DifficultySet] Empty Set. No difficulties built for {customGameMode.serializedName}. FINISHED!");
                }
            }


        }
        public static (string, string, string, Version) GetJson(BeatmapLevel level, BeatmapDifficulty difficulty, BeatmapKey beatmapKey)
        {
            string beatmapJson = null;
            string lightshowJson = null;
            string audioDataJson = null; //v4 built-in

            Version version = new Version(0, 0, 0);

            if (level.levelID.StartsWith("custom_level_"))
            {
                try
                {
                    var beatmapLevelsModel = SongCore.Loader.BeatmapLevelsModelSO;
                    if (beatmapLevelsModel == null)
                    {
                        Plugin.Log.Error("[CreateGen360DifficultySet] BeatmapLevelsModelSO is null.");
                        return (beatmapJson, lightshowJson, audioDataJson, version);
                    }
                    // === LOAD RAW JSON FOR THIS DIFFICULTY used to create metadata for BeatmapBasicData and get Note, Obstacle, etc data  ===
                    var levelDataResult = beatmapLevelsModel
                        .LoadBeatmapLevelDataAsync(level.levelID, BeatmapLevelDataVersion.Original, CancellationToken.None)
                        .GetAwaiter()
                        .GetResult();
                    var beatmapLevelData = levelDataResult.beatmapLevelData;

                    if (beatmapLevelData != null)
                    {
                        beatmapJson = beatmapLevelData.GetBeatmapString(in beatmapKey);
                        lightshowJson = beatmapLevelData.GetLightshowString(in beatmapKey);
                        audioDataJson = beatmapLevelData.GetAudioDataString();

                        version = GetBeatMapDataJsonVersion(beatmapJson); // 0,0,0 if json empty or null

                        SongFolderPath = SongFolderUtils.TryGetSongFolder(level.levelID);

                        /*
                        if (SongFolderPath == null)
                        {
                            Plugin.Log.Error($"[CreateGen360DifficultySet] Could not find song folder.");
                        }
                        else
                        {
                            Plugin.Log.Info($"[CreateGen360DifficultySet] Found song folder at '{SongFolderPath}'");
                        }
                        */

                        return (beatmapJson, lightshowJson, audioDataJson, version);
                    }

                    
                }
                catch (Exception ex)
                {
                    Plugin.Log.Error($"[CreateGen360DifficultySet] -- JSON parse error: {ex}");
                }
            }
            else // BUILT-IN maps
            {
                //(beatmapJson, lightshowJson) = BuiltInMapJsonLoader.LoadBuiltInJson(level.levelID, basedOn, difficulty.ToString());
                if (BuiltInMapJsonLoader.TryGetBuiltInJson(level.levelID, basedOn, difficulty.ToString(), out beatmapJson, out lightshowJson, out audioDataJson))
                {
                    //(beatmapJson, lightshowJson, audioDataJson) = BuiltInMapJsonLoader.LoadBuiltInJson(level.levelID, basedOn, difficulty.ToString());
                    //Plugin.Log.Info(beatmapJson);
                    //Plugin.Log.Info(lightshowJson);
                    //Plugin.Log.Info(audioDataJson);
                    //var p = audioDataJson.Length > 200 ? audioDataJson.Substring(0, 200) + " …" : audioDataJson;
                    //Plugin.Log.Info($"[CreateGen360DifficultySet] -- audioData.json len={audioDataJson.Length}");// preview: {p}");
                    version = GetBeatMapDataJsonVersion(beatmapJson); // 0,0,0 if json empty or null
                    return (beatmapJson, lightshowJson, audioDataJson, version);
                }
            }
            return (beatmapJson, lightshowJson, audioDataJson, version);
        }
        static Version GetBeatMapDataJsonVersion(string beatmapJson)
        {
            if (beatmapJson == null || beatmapJson == string.Empty)
            {
                Plugin.Log.Warn($"[CreateGen360DifficultySet][GetBeatMapDataJsonVersion] beatmapJson is NULL or empty");
                return new Version(0, 0, 0);
            }
            Version version = BeatmapSaveDataHelpers.GetVersion(beatmapJson);
            Plugin.Log.Info($"[CreateGen360DifficultySet][GetBeatMapDataJsonVersion] found version={version} (if v0 then its missing from the dat file)");

            if (version.Major == 0) // version missing from json file (will get error loading 360fyer difficulties if missing version)
            {
                JObject beatmapObj = JObject.Parse(beatmapJson);
                int majorVersion = beatmapObj["_notes"] != null ? 2 : 0; //2
                if (majorVersion == 0)
                {
                    majorVersion = beatmapObj["colorNotes"] != null ? 3 : 0; //v3 or v4 but guess v3
                }
                if (majorVersion == 3)
                {
                    majorVersion = beatmapObj["colorNotesData"] != null ? 4 : 0; //v4
                }

                if (majorVersion == 0 || majorVersion == 2)
                {
                    version = new Version(2, 6, 0);
                }
                else if (majorVersion == 3)
                {
                    version = new Version(3, 3, 0);
                }
                else // majorVersion ==4
                {
                    version = new Version(majorVersion, 0, 0);
                }
                Plugin.Log.Info($"[CreateGen360DifficultySet][GetBeatMapDataJsonVersion] Used JSON to find missing version={version}");
            }
            return version;
        }

        // directly taken from BeatmapDifficultyMethods() v1.40. chatGPT says fastNotes is no longer used and always false. it is still present in GameplayModifiers
        public static float NoteJumpMovementSpeed(BeatmapDifficulty difficulty, float noteJumpMovementSpeed, bool fastNotes = false)
        {
            if (fastNotes)
            {
                return 20f;
            }

            if (noteJumpMovementSpeed > 0f)
            {
                return noteJumpMovementSpeed;
            }

            return difficulty.DefaultNoteJumpMovementSpeed();
        }

        // directly taken from BeatmapDifficultyMethods() v1.40
        private static float DefaultNoteJumpMovementSpeed(BeatmapDifficulty difficulty)
        {
            return difficulty switch
            {
                BeatmapDifficulty.Expert => 12f,
                BeatmapDifficulty.ExpertPlus => 16f,
                _ => 10f,
            };
        }

        public static class SongFolderUtils
        {
            public static string? TryGetSongFolder(string levelId)
            {
                // CustomLevels
                foreach (var kvp in SongCore.Loader.CustomLevels)
                    if (kvp.Value.levelID == levelId)
                        return kvp.Key;

                // WIP
                foreach (var kvp in SongCore.Loader.CustomWIPLevels)
                    if (kvp.Value.levelID == levelId)
                        return kvp.Key;

                // Cached WIP
                foreach (var kvp in SongCore.Loader.CachedWIPLevels)
                    if (kvp.Value.levelID == levelId)
                        return kvp.Key;

                return null; // OST/DLC or not found
            }
        }


        private static void LogBeatmapV4Inputs(
            string audioDataJson,
            string beatmapJson,
            string lightshowJson,
            string defaultLightshowJson,
            BeatmapDifficulty difficulty,
            IEnvironmentInfo env,
            PlayerSpecificSettings pss)
        {
            // --- Sizes & short previews ---
            Plugin.Log.Info($"[V4] Inputs for difficulty={difficulty}");
            Plugin.Log.Info($"[V4] audioDataJson len={(audioDataJson?.Length ?? 0)}");
            Plugin.Log.Info($"[V4] beatmapJson    len={(beatmapJson?.Length ?? 0)}");
            Plugin.Log.Info($"[V4] lightshowJson  len={(lightshowJson?.Length ?? 0)}");
            Plugin.Log.Info($"[V4] defaultLSJson  len={(defaultLightshowJson?.Length ?? 0)}");

            string Prev(string s) => string.IsNullOrEmpty(s) ? "(null/empty)" : (s.Length > 160 ? s.Substring(0, 160) + " …" : s);
            Plugin.Log.Info($"[V4] audio preview: {Prev(audioDataJson)}");
            Plugin.Log.Info($"[V4] lshow preview: {Prev(lightshowJson)}");
            if (!string.IsNullOrEmpty(defaultLightshowJson))
                Plugin.Log.Info($"[V4] dfltL preview: {Prev(defaultLightshowJson)}");

            // --- Environment sanity ---
            if (env == null)
            {
                Plugin.Log.Error("[V4] IEnvironmentInfo is NULL (will crash V4 loader).");
            }
            else
            {
                var envName = BuiltInMapJsonLoader.SafeGetName(env);
                Plugin.Log.Info($"[V4][ENV] type={env.GetType().FullName} name='{envName}'");
                var dlsa = env.defaultLightshowAsset;
                Plugin.Log.Info($"[V4][ENV] defaultLightshowAsset={(dlsa ? dlsa.name : "(null)")}");
                var kws = env.environmentKeywords;
                Plugin.Log.Info($"[V4][ENV] keywords count={(kws?.Count ?? 0)} sample=[{string.Join(", ", (kws ?? Array.Empty<string>()).Take(8))}]");
                var groups = env.environmentLightGroups;
                var list = groups?.lightGroups?.ToList();
                Plugin.Log.Info($"[V4][ENV] lightGroups count={(list?.Count ?? 0)}");
                if (list != null) foreach (var g in list.Take(8))
                        Plugin.Log.Info($"[V4][ENV]  groupId={g.groupId} elements={g.numberOfElements}");
            }

            // --- JSON validity (Newtonsoft) ---
            JObject audio = TryParse(audioDataJson);
            JObject beat = TryParse(beatmapJson);
            JObject lshow = TryParse(lightshowJson);
            JObject dfltL = TryParse(defaultLightshowJson);

            Plugin.Log.Info($"[V4][PARSE] AudioSaveData null? {audio == null}");
            Plugin.Log.Info($"[V4][PARSE] BeatmapSaveData null? {beat == null}");
            Plugin.Log.Info($"[V4][PARSE] LightshowSaveData null? {lshow == null}");
            Plugin.Log.Info($"[V4][PARSE] DefaultLightshowSaveData null? {dfltL == null}");

            // Optional: compute a base BPM from audioDataJson (bpmData) just to confirm it’s sensible
            if (audio != null)
            {
                var sr = (audio["songFrequency"] ?? 44100).Value<double>();
                var seg = audio["bpmData"]?.FirstOrDefault() as JObject;
                if (seg != null)
                {
                    double si = (seg["si"] ?? 0).Value<double>();
                    double ei = (seg["ei"] ?? 0).Value<double>();
                    double sb = (seg["sb"] ?? 0).Value<double>();
                    double eb = (seg["eb"] ?? 0).Value<double>();
                    double seconds = (ei - si) / Math.Max(sr, 1);
                    if (seconds > 0)
                    {
                        double bpm = 60.0 * (eb - sb) / seconds;
                        Plugin.Log.Info($"[V4][AUDIO] Derived BPM ≈ {bpm:0.###}  (sr={sr}, si={si}, ei={ei}, sb={sb}, eb={eb})");
                    }
                }
            }

            static JObject TryParse(string json)
            {
                if (string.IsNullOrWhiteSpace(json)) return null;
                try { return JObject.Parse(json); }
                catch (Exception ex)
                {
                    Plugin.Log.Error($"[V4][PARSE] Invalid JSON: {ex.Message}");
                    return null;
                }
            }

            if (pss == null) Plugin.Log.Warn("[V4] PlayerSpecificSettings is NULL.");
            else Plugin.Log.Info($"[V4] PlayerSpecificSettings ok (leftHanded={pss.leftHanded}, sfxVol={pss.sfxVolume})");
        }

        // Put this somewhere in your project (same assembly/namespace can be anything).
        // It implements the exact v4 interface you pasted and simply emits no events.
        


        // Call on Unity's main thread.
        static IEnvironmentInfo GetUsableEnvironment(string characteristic)
        {
            bool wantsAllDir = characteristic.Equals("90Degree", StringComparison.OrdinalIgnoreCase)
                            || characteristic.Equals("360Degree", StringComparison.OrdinalIgnoreCase);

            var all = Resources.FindObjectsOfTypeAll<UnityEngine.Object>();

            // Helper to test viability
            bool IsUsable(IEnvironmentInfo e) =>
                e != null &&
                e.defaultLightshowAsset != null &&
                (e.environmentKeywords?.Count ?? 0) > 0 &&
                (e.environmentLightGroups?.lightGroups?.Any() ?? false);

            // Prefer all-directions if needed
            var allDirs = all.OfType<IEnvironmentInfo>()
                .Where(e => e.GetType().Name.Contains("AllDirections", StringComparison.OrdinalIgnoreCase))
                .Where(IsUsable)
                .ToList(); 

            if (wantsAllDir && allDirs.Count > 0)
                return allDirs.First();

            // Prefer “DefaultEnvironment” (and exclude Multiplayer)
            var singles = all.OfType<IEnvironmentInfo>()
                .Where(e => !e.GetType().Name.Contains("AllDirections", StringComparison.OrdinalIgnoreCase))
                .Where(e => !(BuiltInMapJsonLoader.SafeGetName(e) ?? "").Contains("Multiplayer", StringComparison.OrdinalIgnoreCase))
                .OrderBy(e =>
                {
                    var n = BuiltInMapJsonLoader.SafeGetName(e) ?? "";
                    return n.Contains("DefaultEnvironment", StringComparison.OrdinalIgnoreCase) ? 0 : 1;
                })
                .Where(IsUsable)
                .ToList();

            return singles.FirstOrDefault() ?? allDirs.FirstOrDefault(); // last resort
        }

       

        /*
        static string GetMonoBehaviourDir()
            => System.IO.Path.Combine(UnityEngine.Application.streamingAssetsPath, "Assets", "MonoBehaviour");

        static string ToDiffSuffix(BeatmapDifficulty d) => d switch
        {
            BeatmapDifficulty.Easy => "Easy",
            BeatmapDifficulty.Normal => "Normal",
            BeatmapDifficulty.Hard => "Hard",
            BeatmapDifficulty.Expert => "Expert",
            BeatmapDifficulty.ExpertPlus => "ExpertPlus",
            _ => throw new ArgumentOutOfRangeException(nameof(d))
        };

        static string CharToken(BeatmapCharacteristicSO ch)
        {
            // serializedName usually: "Standard" | "NoArrows" | "OneSaber" | "90Degree" | "360Degree"
            var s = ch.serializedName;
            if (string.Equals(s, "Standard", StringComparison.Ordinal)) return ""; // Standard omits token
            return s; // others use the name directly as the middle token
        }

        // e.g. "100BillsExpertBeatmapData.asset", "100BillsNoArrowsHardBeatmapData.asset", "100Bills360DegreeNormalBeatmapData.asset"
        static string ComposeBeatmapAssetName(string levelId, BeatmapCharacteristicSO ch, BeatmapDifficulty diff)
            => $"{levelId}{CharToken(ch)}{ToDiffSuffix(diff)}BeatmapData.asset";

        // Info "level" asset (acts like Info.dat)
        static string ComposeLevelAssetName(string levelId)
            => $"{levelId}Level.asset";

        static string ExtractJsonPayload(string assetText)
        {
            if (string.IsNullOrEmpty(assetText)) return null;
            int start = assetText.IndexOf('{');
            int end = assetText.LastIndexOf('}');
            if (start < 0 || end <= start) return null;
            return assetText.Substring(start, end - start + 1);
        }

        static (string beatmapJson1, string lightshowJson) LoadOstJsonForDiff(
            string levelId, BeatmapCharacteristicSO ch, BeatmapDifficulty diff)
        {
            string dir = GetMonoBehaviourDir();
            string name = ComposeBeatmapAssetName(levelId, ch, diff);

            // Try exact case; if missing, try a lowercase levelId variant
            string path = System.IO.Path.Combine(dir, name);
            if (!System.IO.File.Exists(path))
            {
                string lowerName = ComposeBeatmapAssetName(levelId.ToLowerInvariant(), ch, diff);
                string lowerPath = System.IO.Path.Combine(dir, lowerName);
                if (System.IO.File.Exists(lowerPath)) path = lowerPath;
            }

            if (!System.IO.File.Exists(path))
                return (null, null);

            string raw = System.IO.File.ReadAllText(path);
            string json = ExtractJsonPayload(raw);
            if (string.IsNullOrEmpty(json))
                return (null, null);

            // Many OST assets embed both “beatmapData” and “lightshowData” inside one JSON.
            // Your existing v4 parser likely accepts separate strings;
            // if needed, split them here, or just pass the same JSON to a parser that reads both.
            return (json, json);
        }

        */
        private static void LogAllGameObjects(GameObject parent)
        {
            Plugin.Log.Info("Logging all root and child GameObjects:");

            foreach (GameObject root in parent.scene.GetRootGameObjects())
            {
                Plugin.Log.Info($"Root GameObject: {root.name}");
                LogChildGameObjects(root.transform, "--");
            }
        }

        private static void LogChildGameObjects(Transform parentTransform, string indent)
        {
            foreach (Transform child in parentTransform)
            {
                Plugin.Log.Info($"{indent} Child GameObject: {child.gameObject.name}");
                LogChildGameObjects(child, indent + "--");
            }
        }


        /*
        public static class BuiltInBeatmapLoader
        {
            public static string LoadJson(string levelID)
            {
                // 1) Find the .assetbundle file on disk
                string bundlePath = Path.Combine(
                    Application.streamingAssetsPath,
                    "BeatmapLevelsData",
                    levelID.ToLower()              // bundle names are always lowercase
                );
                if (!File.Exists(bundlePath))
                    throw new FileNotFoundException($"No AssetBundle at {bundlePath}");

                // 2) Reflectively load UnityEngine.AssetBundle
                Type abType = AppDomain.CurrentDomain.GetAssemblies()
                    .Select(a => a.GetType("UnityEngine.AssetBundle"))
                    .First(t => t != null);
                var loadMBF = abType.GetMethod("LoadFromFile", new[] { typeof(string) });
                object bundle = loadMBF.Invoke(null, new object[] { bundlePath });
                if (bundle == null)
                    throw new Exception($"AssetBundle.LoadFromFile returned null for {bundlePath}");

                // 3) List its contents (for debug)
                var getNames = abType.GetMethod("GetAllAssetNames", BindingFlags.Public | BindingFlags.Instance);
                var names = (string[])getNames.Invoke(bundle, null);
                Plugin.Log.Info($"[BundleInspect:{levelID}] contains {names.Length} assets:");
                foreach (var n in names) Plugin.Log.Info("  → " + n);

                // 4) Prepare LoadAsset(name, Type) for UnityEngine.Object
                var loadNonGen = abType.GetMethod(
                    "LoadAsset",
                    new[] { typeof(string), typeof(Type) }
                );
                Type unityObjType = AppDomain.CurrentDomain.GetAssemblies()
                    .Select(a => a.GetType("UnityEngine.Object"))
                    .First(t => t != null);

                // 5) Try each asset key, stripping “.asset”
                foreach (var full in names)
                {
                    if (!full.EndsWith(".asset", StringComparison.OrdinalIgnoreCase))
                        continue;
                    string key = full.Substring(0, full.Length - ".asset".Length);

                    object assetObj = null;
                    try
                    {
                        assetObj = loadNonGen.Invoke(
                            bundle,
                            new object[] { key, unityObjType }
                        );
                    }
                    catch { // ignore failures }

                    if (assetObj == null)
                        continue;

                    // 6) Reflect into its private _jsonData field
                    var field = assetObj.GetType()
                        .GetField("_jsonData", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (field == null)
                        continue;

                    string json = (string)field.GetValue(assetObj);

                    // 7) Unload bundle (keep the loaded asset instance alive)
                    abType.GetMethod("Unload", new[] { typeof(bool) })
                          .Invoke(bundle, new object[] { false });

                    return json;
                }

                // Nothing matched
                abType.GetMethod("Unload", new[] { typeof(bool) })
                      .Invoke(bundle, new object[] { false });
                throw new Exception("No MonoBehaviour with a _jsonData field found in bundle");
            }
        }

        public static void LogBeatmapLevelsDataContents()
        {
            string root = Path.Combine(Application.streamingAssetsPath, "BeatmapLevelsData");
            Plugin.Log.Info($"[FS] StreamingAssets/BeatmapLevelsData = {root}");

            if (!Directory.Exists(root))
            {
                Plugin.Log.Warn("[FS] That directory does not exist!");
                return;
            }

            // Top‐level entries under BeatmapLevelsData
            foreach (var entry in Directory.EnumerateFileSystemEntries(root))
            {
                bool isDir = Directory.Exists(entry);
                Plugin.Log.Info($"[FS] {(isDir ? "DIR " : "FILE")} {Path.GetFileName(entry)}");
            }

            // If there are subdirectories, list *their* contents too
            foreach (var dir in Directory.GetDirectories(root))
            {
                Plugin.Log.Info($"[FS] -- Contents of subdir {Path.GetFileName(dir)}:");
                foreach (var sub in Directory.EnumerateFileSystemEntries(dir))
                    Plugin.Log.Info($"[FS] ---- {(Directory.Exists(sub) ? "DIR " : "FILE")} {Path.GetFileName(sub)}");
            }
        }
       */

        /*
        /// <summary>
        /// Used to make simple customBeatmapData to send to CreateTransformBeatmapData which will have a NULL error with any content. 
        /// It uses correct metadata but only has one note in it.
        /// </summary>
        /// <param name="cjdSaveData"></param>
        public static CustomBeatmapData ProduceSimpleCJD(Version2_6_0AndEarlierCustomBeatmapSaveData cjdSaveData)
        {
            //Created from JSON so all _time fields are in beats.

            bool found = SetContent.BeatmapCustomData.TryGetValue(TransitionPatcher.userSelectedDifficulty, out CustomData beatmapCustomData);
            if (!found)
                beatmapCustomData = new CustomData();

            cjBeatmapData = new CustomBeatmapData(
                4, // number of lines (lanes in the map)
                beatmapCustomData,    // from info.dat, or new CustomData()
                cjdSaveData.customData, // from the map file itself
                new CustomData(),     // customData for your own use
                new Version(cjdSaveData.version)//new Version(2, 0, 0)  // map version
            );

            CustomNoteData customNote = CustomNoteData.CreateCustomBasicNoteData(
                0,
                0,
                0, // rotation
                0,
                (NoteLineLayer)0,
                ColorType.ColorA,
                (NoteCutDirection)0,
                new CustomData(),
                new Version(cjdSaveData.version)
            );

            cjBeatmapData.AddBeatmapObjectDataInOrder(customNote);
            return cjBeatmapData;
        }
        */


        /*
        public static CustomBeatmapData ProduceCJD(Version2_6_0AndEarlierCustomBeatmapSaveData cjdSaveData)
        {
            //Created from JSON so all _time fields are in beats.

            bool found = SetContent.BeatmapCustomData.TryGetValue(TransitionPatcher.userSelectedDifficulty, out CustomData beatmapCustomData);
            if (!found)
                beatmapCustomData = new CustomData();

            float bps = SetContent.bpm/60f;

            cjBeatmapData = new CustomBeatmapData(
                4, // number of lines (lanes in the map)
                beatmapCustomData,    // from info.dat, or new CustomData()
                cjdSaveData.customData, // from the map file itself
                new CustomData(),     // customData for your own use
                new Version(cjdSaveData.version)//new Version(2, 0, 0)  // map version
            );


            foreach (Version2_6_0AndEarlierCustomBeatmapSaveData.NoteSaveData note in cjdSaveData.notes)
            {
                CustomNoteData customNote = null;

                // Figure out note type (you might have a field for this already)
                if (note.type == NoteType.Bomb) // Or whatever your bomb type is
                {
                    customNote = CustomNoteData.CreateCustomBombNoteData(
                        note.time / bps,
                        note.time,
                        0, // rotation
                        note.lineIndex,
                        (NoteLineLayer)note.lineLayer,
                        note.customData,
                        new Version(cjdSaveData.version)
                    );
                }
                else // Default to regular note
                {
                    customNote = CustomNoteData.CreateCustomBasicNoteData(
                        note.time / bps,
                        note.time,
                        0, // rotation
                        note.lineIndex,
                        (NoteLineLayer)note.lineLayer,
                        note.type == NoteType.NoteA ? ColorType.ColorA : note.type == NoteType.NoteB ? ColorType.ColorB : ColorType.None,
                        (NoteCutDirection)note.cutDirection,
                        note.customData,
                        new Version(cjdSaveData.version)
                    );
                }

                cjBeatmapData.AddBeatmapObjectDataInOrder(customNote);

                //Automatic Cross-Over Visuals
                //When two notes of opposite color are spawned at the same time but on opposite sides(e.g., Red on the right, Blue on the left), Beat Saber automatically animates their spawn positions so they "cross" each other in midair.This is called the mirrored / crossed flip animation.
                //flipLineIndex controls whether a note spawns in its own column (normal), or is mirrored to another column on spawn (for the “cross” effect).
                // example
                //| Color | Lane | flipLineIndex | Effect |
                //| ----- | ---- | ------------- | ----------------- |
                //| Red | 2 | 1 | Crosses to lane 2 |
                //| Blue | 1 | 2 | Crosses to lane 1 |
                //| Red | 2 | 2 | Flies straight |
                //| Blue | 1 | 1 | Flies straight |

            }


            //foreach (var obs in beatmapData.allBeatmapDataItems.OfType<ObstacleData>())
            //    cjBeatmapData.AddBeatmapObjectDataInOrder(obs);
            foreach (Version2_6_0AndEarlierCustomBeatmapSaveData.ObstacleSaveData obs in cjdSaveData.obstacles)
            {
                var customObs = new CustomObstacleData(// there is no createCustomObstacleData method in CustomNoteData
                    obs.time / bps,
                    obs.beat,
                    obs.time / bps + obs.duration / bps,
                    0,
                    obs.lineIndex,
                    NoteLineLayer.Base,
                    obs.duration / bps,
                    obs.width,
                    5,
                    obs.customData, // or pass null if not used
                    new Version(cjdSaveData.version)//obs is IVersionable versioned ? versioned.version : new Version(2, 6, 0)
                    );

                cjBeatmapData.AddBeatmapObjectDataInOrder(customObs);
            }

            foreach (Version2_6_0AndEarlierCustomBeatmapSaveData.EventSaveData evt in cjdSaveData.events)
            {
                var eventData = new CustomBasicBeatmapEventData(
                    evt.time / bps,
                    (BasicBeatmapEventType)evt.type,
                    evt.value,
                    evt.floatValue,
                    evt.customData, // or pass null if not used
                    new Version(cjdSaveData.version)
                );
                //Plugin.Log.Info($"[SetContent] Adding event: time={evt.time}, type={evt.type}, value={evt.value}, floatValue={evt.floatValue}");
                cjBeatmapData.InsertBeatmapEventDataInOrder(eventData);
            }

            foreach (Version2_6_0AndEarlierCustomBeatmapSaveData.SliderSaveData slider in cjdSaveData.sliders)
            {
                //Version version = (slider is IVersionable versioned) ? versioned.version : new Version(2, 0, 0);

                // Now construct the CustomSliderData object with all the fields
                var customSlider = CustomSliderData.CreateCustomSliderData(
                    (ColorType)slider.colorType,
                    slider.time / bps,                     // headTime
                    slider.time,                           // headBeat
                    0,                                     // rotation
                    slider.headLineIndex,
                    (NoteLineLayer)slider.headLineLayer,
                    (NoteLineLayer)slider.headLineLayer,   // headBeforeJumpLineLayer
                    slider.headControlPointLengthMultiplier,
                    (NoteCutDirection)slider.headCutDirection,
                    slider.tailTime / bps,                 // tailTime
                    0,                                     // tailRotation
                    slider.tailLineIndex,
                    (NoteLineLayer)slider.tailLineLayer,
                    (NoteLineLayer)slider.tailLineLayer,   // tailBeforeJumpLineLayer
                    slider.tailControlPointLengthMultiplier,
                    (NoteCutDirection)slider.tailCutDirection,
                    (SliderMidAnchorMode)slider.sliderMidAnchorMode,
                    slider.customData,                      // Or slider.customData if you have it
                    new Version(cjdSaveData.version)
                );

                cjBeatmapData.AddBeatmapObjectDataInOrder(customSlider);
            }

            foreach (Version2_6_0AndEarlierCustomBeatmapSaveData.CustomEventSaveData cEvt in cjdSaveData.customEvents)
            {
                BasicBeatmapEventType eventType = BasicBeatmapEventType.Event0; // Default

                // Try parsing as enum name (e.g., "Event4")
                if (!Enum.TryParse<BasicBeatmapEventType>(cEvt.type, true, out eventType))
                {
                    // If that fails, try parsing as an integer value (e.g., "4" or "1000")
                    if (int.TryParse(cEvt.type, out int intVal) && Enum.IsDefined(typeof(BasicBeatmapEventType), intVal))
                        eventType = (BasicBeatmapEventType)intVal;
                    else
                        eventType = BasicBeatmapEventType.Event0;
                }

                var eventData = new CustomBasicBeatmapEventData(
                    cEvt.time / bps,
                    eventType,
                    0,
                    0,
                    cEvt.customData, // or pass null if not used
                    new Version(cjdSaveData.version)
                );
                cjBeatmapData.InsertBeatmapEventDataInOrder(eventData);
            }

            Plugin.Log.Info($"[ProduceCJD] Parsed and stored CustomBeatmapData: notes={cjdSaveData.notes?.Count ?? 0}, obstacles={cjdSaveData.obstacles?.Count ?? 0}, events={cjdSaveData.events?.Count ?? 0}");

            return cjBeatmapData;
        }
        */

        /*
        //[CRITICAL @ 16:10:01 | UnityEngine] NullReferenceException: Object reference not set to an instance of an object if you later try to add customJSONData to the map. the only solution is send in CJD initially
        private static (BeatmapData, int) produceVanilla(string beatmapJson)
        {
            BeatmapData beatmapData = new BeatmapData(4);
            int bombCount = 0;

            try
            {
                var jObj = JObject.Parse(beatmapJson);

                string version = jObj["_version"]?.ToString() ?? "2.0.0";
                Plugin.Log.Info($"[GEN360]: Detected map version = {version}");

                // Level custom data (on the difficulty .dat file)
                //JToken levelCustomDataToken = jObj["_customData"];
                //LevelCustomData = (levelCustomDataToken != null) ? CustomData.FromJSON(levelCustomDataToken.ToString()) : new CustomData();

                if (version.StartsWith("2"))
                {
                    // --- v2 MAP ---


                    // Parse notes
                    var notesArray = (JArray)(jObj["_notes"] ?? jObj["notes"]);
                    int noteCount = notesArray is JArray arr ? arr.Count : 0;

                    Plugin.Log.Info($"[GEN360]: JSON notes array count = {noteCount}");

                    foreach (JObject note in notesArray)
                    {
                        if (note["_type"]?.Value<int>() == 3)
                            bombCount++;

                        // You may want to map all fields, including cutDirection, etc.
                        ColorType colorType = note["_type"]?.Value<int>() == 0 ? ColorType.ColorA :
                        note["_type"]?.Value<int>() == 1 ? ColorType.ColorB : ColorType.None;
                        NoteCutDirection cutDirection = (NoteCutDirection)(note["_cutDirection"]?.Value<int>() ?? 0);
                        NoteLineLayer noteLineLayer = (NoteLineLayer)(note["_lineLayer"]?.Value<int>() ?? 0);

                        // Construct your NoteData
                        var noteData = new NoteData(
                            time: note["_time"]?.Value<float>() ?? 0f,
                            beat: note["_time"]?.Value<float>() ?? 0f, // don't convert to beat, use time directly since dat files are in beats
                            rotation: 0,
                            lineIndex: note["_lineIndex"]?.Value<int>() ?? 0,
                            noteLineLayer: noteLineLayer,
                            beforeJumpNoteLineLayer: noteLineLayer,
                            gameplayType: GameplayType.Normal,
                            scoringType: ScoringType.Normal,
                            colorType: colorType,
                            cutDirection: cutDirection,
                            timeToNextColorNote: 0f,
                            timeToPrevColorNote: 0f,
                            flipLineIndex: note["_lineIndex"]?.Value<int>() ?? 0,
                            flipYSide: 0f,
                            cutDirectionAngleOffset: 0f,
                            cutSfxVolumeMultiplier: 1f
                        );

                        beatmapData.AddBeatmapObjectDataInOrder(noteData);
                    }

                    Plugin.Log.Info($"[GEN360]: JSON bombs count = {bombCount}");

                    // Parse obstacles
                    var obstaclesArray = (JArray)(jObj["_obstacles"] ?? jObj["obstacles"]);
                    int obstacleCount = obstaclesArray is JArray arr1 ? arr1.Count : 0;

                    Plugin.Log.Info($"[GEN360]: JSON obstacles array count = {obstacleCount}");

                    foreach (JObject obstacle in obstaclesArray)
                    {
                        int obstType = obstacle["_type"]?.Value<int>() ?? 0;
                        int obstHeight = (obstType == 0) ? 5 : 3;
                        NoteLineLayer obstLayer = (obstType == 0) ? NoteLineLayer.Base : NoteLineLayer.Upper;

                        var obsData = new ObstacleData(
                            time: obstacle["_time"]?.Value<float>() ?? 0f,
                            startBeat: obstacle["_time"]?.Value<float>() ?? 0f, // don't convert to beat, use time directly since dat files are in beats
                            endBeat: (obstacle["_time"]?.Value<float>() ?? 0f) + (obstacle["_duration"]?.Value<float>() ?? 1f),
                            rotation: 0,
                            lineIndex: obstacle["_lineIndex"]?.Value<int>() ?? 0,
                            lineLayer: obstLayer,
                            duration: obstacle["_duration"]?.Value<float>() ?? 1f,
                            width: obstacle["_width"]?.Value<int>() ?? 1,
                            height: obstHeight
                        );


                        beatmapData.AddBeatmapObjectDataInOrder(obsData);
                    }

                    // Parse events
                    var eventsArray = (JArray)(jObj["_events"] ?? jObj["events"]);
                    foreach (JObject evt in eventsArray)
                    {
                        float time = evt["_time"]?.Value<float>() ?? 0f;
                        int type = evt["_type"]?.Value<int>() ?? 0;
                        int value = evt["_value"]?.Value<int>() ?? 0;
                        float floatValue = evt["_floatValue"]?.Value<float>() ?? 0f;

                        // You may need to use a factory for BeatmapEventData depending on your mod setup
                        var eventData = new BasicBeatmapEventData(
                            time,
                            (BasicBeatmapEventType)type,
                            value,
                            floatValue);

                        beatmapData.InsertBeatmapEventDataInOrder(eventData);
                    }

                    // Add test rotation Events

                    //if (basedOn != "90Degree") // insert rotation event only for standard maps that already have any rotations events. this is needed to fool beat saber into allowing this to have rotations.

                    beatmapData.InsertBeatmapEventDataInOrder(
                    new BasicBeatmapEventData(
                        1f,
                        BasicBeatmapEventType.Event15,
                        6, // value v2 maps 4 is 15 degrees and 3 is -15 degrees etc (maybe not used in v3)
                        1 // floatValue 1 for v2 maps (maybe v3 is angle in degrees)
                        )
                    );
                    beatmapData.InsertBeatmapEventDataInOrder(
                    new BasicBeatmapEventData(
                        4f,
                        BasicBeatmapEventType.Event15,
                        5, // value v2 maps 4 is 15 degrees and 3 is -15 degrees etc (maybe not used in v3)
                        1 // floatValue 1 for v2 maps (maybe v3 is angle in degrees)
                        )
                    );
                    beatmapData.InsertBeatmapEventDataInOrder(
                    new BasicBeatmapEventData(
                        7f,
                        BasicBeatmapEventType.Event15,
                        4, // value v2 maps 4 is 15 degrees and 3 is -15 degrees etc (maybe not used in v3)
                        1 // floatValue 1 for v2 maps (maybe v3 is angle in degrees)
                        )
                    );

                    
                    // log rotation events for debugging
                    //foreach (var evt in beatmapData.allBeatmapDataItems.OfType<BasicBeatmapEventData>())
                    //{
                    //    BasicBeatmapEventType? eventType = null;

                    //    if (evt is CustomBasicBeatmapEventData cEvt)
                    //    {
                    //        eventType = cEvt.basicBeatmapEventType;
                    //    }
                    //    if (eventType == BasicBeatmapEventType.Event14 || eventType == BasicBeatmapEventType.Event15)
                    //    {
                    //        Plugin.Log.Info($"[ROT] time={evt.time:F2} type={(int)eventType} value={evt.value} floatValue={evt.floatValue}");
                    //    }
                    //}
                    
                    // Immediately log all BeatmapDataItems with time < 10
                    //foreach (var note in beatmapData.allBeatmapDataItems.OfType<CustomNoteData>())
                    //{
                    //    if (note.time < 10)
                    //        Plugin.Log.Info($"[SetContent] After JSON Parse: Note: {note.colorType} time: {note.time}");
                    //}

                    // If none exist, log an error for debugging
                    //if (beatmapData.allBeatmapDataItems.OfType<CustomNoteData>().Count() == 0)
                    //{
                    //    Plugin.Log.Error("[SetContent] After JSON Parse: No Notes found in the beatmap data!");
                    //}
                    


                    Plugin.Log.Info($"[GEN360]: Parsed and stored BeatmapData: notes={notesArray?.Count ?? 0}, obstacles={obstaclesArray?.Count ?? 0}, events={eventsArray?.Count ?? 0}");

                }
                else if (version.StartsWith("3"))
                {
                    // --- v3 MAP ---
                    Plugin.Log.Info($"[GEN360]: v3 parsing is not yet implemented.");
                    // Leave this block for your future implementation
                }
                else
                {
                    Plugin.Log.Warn($"[GEN360]: Unknown beatmap version '{version}'! Not parsed.");
                }

            }
            catch (Exception ex)
            {
                Plugin.Log.Error($"[GEN360] JSON parse error for: {ex}");
            }

            return (beatmapData, bombCount);
        }
        */

    }

    public static class ColorExtensions
    {
        // https://bsmg.wiki/mapping/lighting-defaults.html
        static readonly MapColor DEFAULT_NOTE_LEFT = MC(0.78431374f, 0.078431375f, 0.078431375f); //red
        static readonly MapColor DEFAULT_NOTE_RIGHT = MC(0.15686274f, 0.5568627f, 0.8235294f); //blue
        static readonly MapColor DEFAULT_ENV_LEFT = MC(0.85f, 0.08499997f, 0.08499997f); //red
        static readonly MapColor DEFAULT_ENV_RIGHT = MC(0.1882353f, 0.675294f, 1f); //blue
        static readonly MapColor DEFAULT_ENV_WHITE = MC(0.7254902f, 0.7254902f, 0.7254902f); //white
        static readonly MapColor DEFAULT_BOOST_LEFT = MC(1f, 0, 0); //red
        static readonly MapColor DEFAULT_BOOST_RIGHT = MC(0, 0, 1f); //blue
        static readonly MapColor DEFAULT_BOOST_WHITE = MC(1f, 1f, 1f); //blue
        static readonly MapColor DEFAULT_OBSTACLE = MC(1f, 0.18823531f, 0.18823531f); //red
        static readonly Color DEFAULT_NOTE_LEFT_c = new Color(0.78431374f, 0.078431375f, 0.078431375f); //red
        static readonly Color DEFAULT_NOTE_RIGHT_c = new Color(0.15686274f, 0.5568627f, 0.8235294f); //blue
        static readonly Color DEFAULT_ENV_LEFT_c = new Color(0.85f, 0.08499997f, 0.08499997f); //red
        static readonly Color DEFAULT_ENV_RIGHT_c = new Color(0.1882353f, 0.675294f, 1f); //blue
        static readonly Color DEFAULT_ENV_WHITE_c = new Color(0.7254902f, 0.7254902f, 0.7254902f); //white
        static readonly Color DEFAULT_BOOST_LEFT_c = new Color(1f, 0, 0); //red
        static readonly Color DEFAULT_BOOST_RIGHT_c = new Color(0, 0, 1f); //blue
        static readonly Color DEFAULT_OBSTACLE_c = new Color(1f, 0.18823531f, 0.18823531f); //red

        /// <summary>
        /// Determines the effective color scheme for a song and difficulty, considering author-defined colors and
        /// overrides. Returns bool for author defined colors
        /// </summary>
        /// <remarks>This method prioritizes per-difficulty color overrides if they exist. If no
        /// per-difficulty overrides are found,  it attempts to use a song-level color scheme that is marked to
        /// override. As a final fallback, it synthesizes  a color scheme from the difficulty data, ensuring that the
        /// resulting scheme is always marked to override.</remarks>
        /// <param name="songCoreExtraData">The song's additional metadata, including any defined color schemes.</param>
        /// <param name="diffData">The difficulty-specific data, which may include per-difficulty color overrides.</param>
        /// <returns>A tuple containing the effective <see cref="SongCore.Data.SongData.ColorScheme"/> and a boolean indicating 
        /// whether any author-defined colors were present. The color scheme will always have <see
        /// cref="SongCore.Data.SongData.ColorScheme.useOverride"/>  set to <see langword="true"/> to ensure it takes
        /// precedence.</returns>
        public static (SongData.ColorScheme, bool) GetEffectiveSongCoreScheme(
            SongData songCoreExtraData,
            DiffData diffData // the based-on song core difficultyData
)
        {
            bool anyAuthorColors = false;

            // If the diff has any per-diff colors, synthesize a scheme from them and force override.
            if (diffData != null)
            {
                anyAuthorColors = HasAnyAuthorPerDiffColors(diffData);

                if (anyAuthorColors)
                {
                    var s = BuildSongCoreSchemeFromDifficulty(diffData);
                    if (s != null) s.useOverride = true;    // <-- important
                    return (s, anyAuthorColors);
                }

                // Else try scheme index if it points at a song-level scheme that already overrides.
                int idx = diffData._beatmapColorSchemeIdx ?? -1;
                var schemes = songCoreExtraData?._colorSchemes;
                if (schemes != null && idx >= 0 && idx < schemes.Length && schemes[idx]?.useOverride == false)
                    return (schemes[idx], anyAuthorColors);
            }

            // Final fallback: synthesize from diff (even if mostly empty) and force override so we control colors.
            var fallback = ColorExtensions.BuildSongCoreSchemeFromDifficulty(diffData);
            if (fallback != null) fallback.useOverride = false;
            return (fallback, anyAuthorColors);
        }

        public static bool HasAnyAuthorPerDiffColors(DiffData d)
        {
            if (d == null) return false;
            return d._colorLeft != null || d._colorRight != null ||
                   d._envColorLeft != null || d._envColorRight != null || d._envColorWhite != null ||
                   d._envColorLeftBoost != null || d._envColorRightBoost != null || d._envColorWhiteBoost != null ||
                   d._obstacleColor != null;
        }

        static MapColor MC(float r, float g, float b, float a = 1f) => new MapColor(r, g, b, a);

        // treat "empty" as null or RGB ~ 0 (ignore alpha)
        static bool ApproximatelyZero(float x) => x <= 0.0001f && x >= -0.0001f;
        static bool IsEmpty(MapColor c) =>
            c == null || (ApproximatelyZero(c.r) && ApproximatelyZero(c.g) && ApproximatelyZero(c.b));

        // simple clamp + lighten that don't need UnityEngine
        static float Clamp01(float x) => x < 0f ? 0f : (x > 1f ? 1f : x);
        
        static MapColor Lighten(MapColor c, float t) // can use this for boost
            => MC(Clamp01(c.r + (1f - c.r) * t),
                  Clamp01(c.g + (1f - c.g) * t),
                  Clamp01(c.b + (1f - c.b) * t),
                  c.a);



        public static Color ToUnityColor(this MapColor mc)
        {
            return new Color(mc.r, mc.g, mc.b, mc.a);
        }

        // Convert SongCore color scheme to Beat Saber ColorScheme
        public static ColorScheme ConvertToBeatSaberColorScheme(SongData.ColorScheme sc)
        {
            // If sc is null, return a fully-defaulted scheme
            if (sc == null)
            {
                return new ColorScheme(
                    colorSchemeId: "SongCoreDefaultID",
                    colorSchemeNameLocalizationKey: "SongCoreDefaultID",
                    useNonLocalizedName: true,
                    nonLocalizedName: "Generated 360",
                    isEditable: false,
                    overrideNotes: true,
                    saberAColor: DEFAULT_NOTE_LEFT_c,
                    saberBColor: DEFAULT_NOTE_RIGHT_c,
                    overrideLights: true,
                    environmentColor0: DEFAULT_ENV_LEFT_c,
                    environmentColor1: DEFAULT_ENV_RIGHT_c,
                    environmentColorW: DEFAULT_ENV_WHITE_c,
                    supportsEnvironmentColorBoost: true,
                    environmentColor0Boost: DEFAULT_BOOST_LEFT_c,
                    environmentColor1Boost: DEFAULT_BOOST_RIGHT_c,
                    environmentColorWBoost: Color.white,
                    obstaclesColor: DEFAULT_OBSTACLE_c
                );
            }

            return new ColorScheme(
                colorSchemeId: sc.colorSchemeId ?? "SongCoreDefaultID",
                colorSchemeNameLocalizationKey: sc.colorSchemeId ?? "SongCore",
                useNonLocalizedName: true,
                nonLocalizedName: "Generated 360",
                isEditable: false,
                overrideNotes: true,
                saberAColor: sc.saberAColor?.ToUnityColor() ?? DEFAULT_NOTE_LEFT_c,
                saberBColor: sc.saberBColor?.ToUnityColor() ?? DEFAULT_NOTE_RIGHT_c,
                overrideLights: true,
                environmentColor0: sc.environmentColor0?.ToUnityColor() ?? (sc.saberAColor?.ToUnityColor() ?? DEFAULT_ENV_LEFT_c),
                environmentColor1: sc.environmentColor1?.ToUnityColor() ?? (sc.saberBColor?.ToUnityColor() ?? DEFAULT_ENV_RIGHT_c),
                environmentColorW: sc.environmentColorW?.ToUnityColor() ?? DEFAULT_ENV_WHITE_c,
                supportsEnvironmentColorBoost: true,
                environmentColor0Boost: sc.environmentColor0Boost?.ToUnityColor() ?? DEFAULT_BOOST_LEFT_c,
                environmentColor1Boost: sc.environmentColor1Boost?.ToUnityColor() ?? DEFAULT_BOOST_RIGHT_c,
                environmentColorWBoost: sc.environmentColorWBoost?.ToUnityColor() ?? Color.white,
                obstaclesColor: sc.obstaclesColor?.ToUnityColor() ?? DEFAULT_OBSTACLE_c
            );
        }
        /// Get the scheme for a specific difficulty (uses _beatmapColorSchemeIdx).
        public static SongData.ColorScheme BuildSongCoreSchemeFromDifficulty(
            SongCore.Data.SongData.DifficultyData d,
            string id = "gen360-from-diff",
            bool fillMissingLocally = true)   // new flag
        {
            if (d == null) return null;

            // Local copies so we never mutate d
            var left = d._colorLeft;
            var right = d._colorRight;
            var env0 = d._envColorLeft;
            var env1 = d._envColorRight;
            var envW = d._envColorWhite;
            var env0B = d._envColorLeftBoost;
            var env1B = d._envColorRightBoost;
            var envWB = d._envColorWhiteBoost;
            var obst = d._obstacleColor;

            if (fillMissingLocally) // previously i created a new color scheme with filled items but that triggers the flag in game for custom colors (even when just creating a default scheme)
            {
                // Use your existing defaults, but apply to the *locals*:
                if (IsEmpty(left)) left = DEFAULT_NOTE_LEFT;
                if (IsEmpty(right)) right = DEFAULT_NOTE_RIGHT;
                if (IsEmpty(obst)) obst = DEFAULT_OBSTACLE;

                if (IsEmpty(env0)) env0 = DEFAULT_ENV_LEFT;
                if (IsEmpty(env1)) env1 = DEFAULT_ENV_RIGHT;
                if (IsEmpty(envW)) envW = DEFAULT_ENV_WHITE;

                if (IsEmpty(env0B)) env0B = DEFAULT_BOOST_LEFT;
                if (IsEmpty(env1B)) env1B = DEFAULT_BOOST_RIGHT;
                if (IsEmpty(envWB)) envWB = DEFAULT_BOOST_WHITE;
            }

            return new SongData.ColorScheme
            {
                colorSchemeId = id,
                // DO NOT set useOverride here; let caller decide
                saberAColor = left,
                saberBColor = right,
                environmentColor0 = env0,
                environmentColor1 = env1,
                environmentColorW = envW,
                environmentColor0Boost = env0B,
                environmentColor1Boost = env1B,
                environmentColorWBoost = envWB,
                obstaclesColor = obst
            };
        }


        // Maybe need to return a colorScheme if found
        public static bool TryGetCustomColorsForDifficulty(
            SongData songData,
            DiffData diffData,
            out MapColor saberA,
            out MapColor saberB,
            out MapColor env0,
            out MapColor env1,
            out MapColor envW,
            out MapColor env0Boost,
            out MapColor env1Boost,
            out MapColor envWBoost,
            out MapColor obstacle)
        {
            saberA = saberB = env0 = env1 = envW = env0Boost = env1Boost = envWBoost = obstacle = null;

            if (diffData == null) return false;

            // 1) Prefer per-difficulty colors if any are set (SongCore populates these from
            //    difficulty customData unless a ColorScheme override was chosen).
            bool anyPerDiff =
                diffData._colorLeft != null || diffData._colorRight != null ||
                diffData._envColorLeft != null || diffData._envColorRight != null ||
                diffData._envColorWhite != null ||
                diffData._envColorLeftBoost != null || diffData._envColorRightBoost != null ||
                diffData._envColorWhiteBoost != null ||
                diffData._obstacleColor != null;

            if (anyPerDiff)
            {
                saberA = diffData._colorLeft;
                saberB = diffData._colorRight;
                env0 = diffData._envColorLeft;
                env1 = diffData._envColorRight;
                envW = diffData._envColorWhite;
                env0Boost = diffData._envColorLeftBoost;
                env1Boost = diffData._envColorRightBoost;
                envWBoost = diffData._envColorWhiteBoost;
                obstacle = diffData._obstacleColor;
                return true;
            }


            // 2) Else, if a color scheme index is present and that scheme says useOverride == true,
            //    use that scheme's colors.
            if (diffData._beatmapColorSchemeIdx is int idx &&
                idx >= 0 && idx < (songData._colorSchemes?.Length ?? 0))
            {
                var scheme = songData._colorSchemes[idx];
                if (scheme != null && scheme.useOverride)
                {
                    saberA = scheme.saberAColor;
                    saberB = scheme.saberBColor;
                    env0 = scheme.environmentColor0;
                    env1 = scheme.environmentColor1;
                    envW = scheme.environmentColorW;
                    env0Boost = scheme.environmentColor0Boost;
                    env1Boost = scheme.environmentColor1Boost;
                    envWBoost = scheme.environmentColorWBoost;
                    obstacle = scheme.obstaclesColor;
                    return true;
                }
            }

            // 3) Nothing custom — use vanilla/default.
            return false;
        }

    }

    /*
    // Causes TransitionPatcher to no longer run!!!!!!!!!!
    // Cache so you can use it in SetContent later
    internal static class LevelDataCache
    {
        public static readonly Dictionary<string, BeatmapLevelDataSO> byLevelId = new Dictionary<string, BeatmapLevelDataSO>();
    }

    [HarmonyPatch(typeof(BeatmapLevel), "SetBeatmapLevelDataProvider")]
    static class Patch_BeatmapLevel_SetProvider
    {
        static void Postfix(BeatmapLevel __instance, object provider) // IBeatmapLevelDataProvider
        {
            if (provider == null) return;

            // Most providers have a non-public GetBeatmapLevelData() that returns BeatmapLevelDataSO
            var getData = AccessTools.Method(provider.GetType(), "GetBeatmapLevelData");
            if (getData == null) return;

            var so = getData.Invoke(provider, null) as BeatmapLevelDataSO;
            if (so != null)
            {
                LevelDataCache.byLevelId[__instance.levelID] = so;
                Plugin.Log.Info($"[Provider] Cached SO for {__instance.levelID} ({so.difficultyBeatmapSets?.Length ?? 0} sets).");
            }
        }
    }
    */

    // No namespace here on purpose — matches a global-namespace interface
    internal sealed class NoOpLightEventConverter : global::IBeatmapLightEventConverter
    {
        public void ConvertBasicBeatmapEvent(
            System.Collections.Generic.List<BeatmapEventData> output,
            int subtypeIdentifier,
            float time,
            global::BasicBeatmapEventType basicBeatmapEventType,
            int value,
            float floatValue)
        {
            // no-op
        }

        public void ConvertLightColorBeatmapEvent(
            System.Collections.Generic.List<BeatmapEventData> output,
            int subtypeIdentifier,
            float time,
            int groupId,
            int elementId,
            bool usePreviousValue,
            global::EaseType easeType,
            global::EnvironmentColorType colorType,
            float brightness,
            int strobeBeatFrequency,
            float strobeBrightness,
            bool strobeFade)
        {
            // no-op
        }

        public void ConvertLightRotationBeatmapEvent(
            System.Collections.Generic.List<BeatmapEventData> output,
            int subtypeIdentifier,
            float time,
            int groupId,
            int elementId,
            bool usePreviousEventValue,
            global::EaseType easeType,
            global::LightAxis axis,
            float rotation,
            int loopCount,
            global::LightRotationDirection rotationDirection)
        {
            // no-op
        }
    }


}