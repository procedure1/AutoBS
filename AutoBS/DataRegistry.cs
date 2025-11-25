using CustomJSONData.CustomBeatmap;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoBS
{
    public static class MenuDataRegistry // used to populate Gen 360 difficulties stats in the menu. 
    {
        //public static readonly Dictionary<string, List<BeatmapCharacteristicSO>> CustomCharacteristicsByLevelID = new Dictionary<string, List<BeatmapCharacteristicSO>>();

        public class Stats
        {
            public int notesCount = 0;
            public int obstaclesCount = 0;
            public int bombsCount = 0;
            public float notesPerSecond = 0;
        }

        // Dictionary lookup
        public static Dictionary<BeatmapKey, Stats> statsByKey = new Dictionary<BeatmapKey, Stats>(); // used to store stats

        public static Stats GetStatsForKey(BeatmapKey key) // used to retrieve stats
        {
            if (statsByKey.TryGetValue(key, out var stats))
                return stats;
            return new Stats();
        }

        // Populate statsByKey when you generate Gen360 difficulties!
    }
    /*
    public static class AutoNjsRegistry // used to populate Gen 360 difficulties stats in the menu. 
    {
        public class Data
        {
            public float original_njs = -99;  //note jump speed used -99 to signal uninitialized which happens for nonGen non basedOn maps. setContent will not set anything for those maps. so do it later in transition patcher
            public float original_jd = 0; //jump distance
            public float original_njo = 0; //note jump offset used to get jd
            public float autoNjs_njs = 0;
            public float autoNjs_jd = 0;
        }

        // Dictionary lookup
        public static Dictionary<BeatmapKey, Data> byKey = new Dictionary<BeatmapKey, Data>(); // used to store data

        public static Data findByKey(BeatmapKey key) // used to retrieve data
        {
            if (byKey.TryGetValue(key, out var data))
                return data;
            return new Data();
        }
    }
    */
    /*
    public static class RequirementsRegistry
    {
        // Holds mod requirements for each key (and thus by difficulty)
        public static Dictionary<BeatmapKey, string[]> findByKey
            = new Dictionary<BeatmapKey, string[]>();
    }

    public static class SuggestionsRegistry
    {
        // Holds mod suggestions for each key (and thus by difficulty)
        public static Dictionary<BeatmapKey, string[]> findByKey
            = new Dictionary<BeatmapKey, string[]>();
    }
    */
    public static class AlreadyUsingEnvColorBoostRegistry
    {
        // Holds mod suggestions for each key (and thus by difficulty)
        public static Dictionary<BeatmapKey, bool> findByKey
            = new Dictionary<BeatmapKey, bool>();
    }
    /*
    public static class MapAlreadyUsesMappingExtensionsRegistry
    {
        // Holds mod suggestions for each key (and thus by difficulty)
        public static Dictionary<BeatmapKey, bool> findByKey
            = new Dictionary<BeatmapKey, bool>();
    }
    */
    public static class MapAlreadyUsesChainsRegistry
    {
        // Holds mod suggestions for each key (and thus by difficulty)
        public static Dictionary<BeatmapKey, bool> findByKey
            = new Dictionary<BeatmapKey, bool>();
    }
    public static class NotesPerSecRegistry
    {
        // Holds mod suggestions for each key (and thus by difficulty)
        public static Dictionary<BeatmapKey, float> findByKey
            = new Dictionary<BeatmapKey, float>();
    }

    // holds the SaveData to get the full JSON data from a difficulty
    public static class SaveDataRegistry
    {
        // Holds lightweight cjdSaveData for each generated Gen360 map
        public static Dictionary<BeatmapKey, Version2_6_0AndEarlierCustomBeatmapSaveData> cjdSaveDataByKey
            = new Dictionary<BeatmapKey, Version2_6_0AndEarlierCustomBeatmapSaveData>();
    }
    public static class BeatmapDataRegistry
    {
        // Holds beatmapData for each generated Gen360 map
        public static Dictionary<BeatmapKey, BeatmapData> beatmapDataByKey
            = new Dictionary<BeatmapKey, BeatmapData>();
        public static Dictionary<BeatmapKey, Version> versionByKey
            = new Dictionary<BeatmapKey, Version>();
    }

    // Stores metadata about all available (including custom) difficulty sets for each level ID.
    public static class CustomBeatmapMetadataRegistry //v1.40 Stores IDifficultyBeatmapSet (doesn't exist in 1.40 so i re-created it) by levelID
    {
        // Stores metadata about all available (including custom) difficulty sets for each level ID.
        public static readonly Dictionary<string, List<IDifficultyBeatmapSet>> CustomSetsByLevelID = new Dictionary<string, List<IDifficultyBeatmapSet>>();
    }

    public class IDifficultyBeatmapSet //v1.40 IDifficultyBeatmapSet no longer exists so replaced with this so could keep my code similar to old version
    {
        public BeatmapCharacteristicSO characteristic;
        public List<(BeatmapDifficulty difficulty, BeatmapBasicData data)> difficultyBeatmaps;
    }

    // Stores the actual, fully-parsed beatmap data (IReadonlyBeatmapData) for each unique (levelId, characteristic, difficulty) triple. actual map — all the note and wall data
    /*
    public static class CustomBeatmapDataRegistry
    {
        // key: (levelID, charSO.serializedName, difficulty) → your generated BeatmapData
        private static readonly Dictionary<(string, string, BeatmapDifficulty), IReadonlyBeatmapData> _maps
            = new Dictionary<(string, string, BeatmapDifficulty), IReadonlyBeatmapData>();

        public static CustomData LevelCustomData;

        public static void Store(
            string levelId,
            BeatmapCharacteristicSO charSO,
            BeatmapDifficulty diff,
            IReadonlyBeatmapData data,
            CustomData levelCustomData)
        {
            _maps[(levelId, charSO.serializedName, diff)] = data;

            LevelCustomData = levelCustomData; //store the custom data for the level

            Plugin.Log.Info($"[CustomBeatmapDataRegistry] Stored map: ({levelId}, {charSO.serializedName}, {diff})");
            // Optional: print all keys
            //Plugin.Log.Info("[CustomBeatmapDataRegistry] Current keys:");
            //foreach (var k in _maps.Keys)
            //    Plugin.Log.Info($"    {k}");

            //Log(data, "Stored");
        }

        public static bool TryGet(
            string levelId,
            BeatmapCharacteristicSO charSO,
            BeatmapDifficulty diff,
            out IReadonlyBeatmapData data)
        {
            var found = _maps.TryGetValue((levelId, charSO.serializedName, diff), out data);

            //Log(data, "TryGet");

            return found;
        }
        public static void ClearForLevel(string levelId, string characteristicSerializedName)
        {
            var keysToRemove = _maps.Keys
                .Where(k => k.Item1 == levelId && k.Item2 == characteristicSerializedName)
                .ToList();

            foreach (var key in keysToRemove)
            {
                _maps.Remove(key);
            }

            Plugin.Log.Info($"[CustomBeatmapDataRegistry] Cleared registry for {levelId}, {characteristicSerializedName}. Removed {keysToRemove.Count} entries.");
        }


        private static void Log(IReadonlyBeatmapData data, string flag)
        {
            if (data == null)
            {
                Plugin.Log.Error($"[CustomBeatmapDataRegistry] {flag} ERROR: data is NULL!");
            }
            else
            {
                if (data.allBeatmapDataItems.Count == 0)
                {
                    Plugin.Log.Error($"[CustomBeatmapDataRegistry] {flag} ERROR: data.allBeatmapDataItems is empty!");
                    return;
                }
                // Dump first 10 sec of items
                int count = 0;
                foreach (var item in data.allBeatmapDataItems)
                {
                    if (item.time < 10)
                    {
                        Plugin.Log.Info($"[CustomBeatmapDataRegistry] {flag} - BeatmapDataItem: {item.GetType()} time: {item.time}");
                        count++;
                        if (count >= 10) break;
                    }
                }
                if (count == 0)
                {
                    Plugin.Log.Warn($"[CustomBeatmapDataRegistry] {flag} - No BeatmapDataItems with time < 10 found.");
                }
            }
        }
    }
    */

}
