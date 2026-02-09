using CustomJSONData.CustomBeatmap;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoBS
{
    public static class MenuDataRegistry // used to populate Gen 360 difficulties stats in the menu. 
    {
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
    }

    //v1.42 could have done this for v1.40. Decide if need boosts when eData is generated and count how many env color boost events there are
    /*
    public static class AlreadyUsingEnvColorBoostRegistry
    {
        public static Dictionary<BeatmapKey, bool> findByKey
            = new Dictionary<BeatmapKey, bool>();
    }
    */
    public static class MapAlreadyUsesChainsRegistry
    {
        public static Dictionary<BeatmapKey, bool> findByKey
            = new Dictionary<BeatmapKey, bool>();
    }
    public static class MapAlreadyUsesArcsRegistry
    {
        public static Dictionary<BeatmapKey, bool> findByKey
            = new Dictionary<BeatmapKey, bool>();
    }
    public static class NotesPerSecRegistry
    {
        public static Dictionary<BeatmapKey, float> findByKey
            = new Dictionary<BeatmapKey, float>();
    }
    public static class NJORegistry
    {
        public static Dictionary<BeatmapKey, float> findByKey
            = new Dictionary<BeatmapKey, float>();
    }

    // holds the v3 SaveData rotation events since cbm and beatmapData do not contain RotationEventData. its only held in SaveData. only needed for v3 since v2 uses basic events for rotation and v4 uses per object rotation which is held in beatmapData
    //This hold v3 rotation events from a loaded beatmapData JSON file
    internal static class RotationV3Registry
    {
        internal sealed class V3RotationRecord
        {
            public float beat;
            public int rotation;
            public int execution; // 0 early 1 late
        }

        public static readonly Dictionary<BeatmapKey, List<V3RotationRecord>> RotationEventsByKey
            = new Dictionary<BeatmapKey, List<V3RotationRecord>>();
    }
    /// <summary>
    /// This is used by SetContent() to store the original v3 JSON map. 
    /// This is only used to convert a v3 difficulty into a v3 json difficulty file for output since we need all the GLS lighting content that never goes into customBeatmapData.
    /// Could convert to v4 but going to wait for v4 support in CustomJSONData (not sure that matters since eventBoxes don't end up in customBeatmapData. They are in SaveData i think.)
    /// </summary>
    public static class OriginalV3JsonRegistry
    {
        public static readonly Dictionary<BeatmapKey, string> findByKey
            = new Dictionary<BeatmapKey, string>();
    }


    //v1.42 Don't need to store beatmapData now. BeatmapDataLoader.LoadBeatmapDataAsync now is able to load basedOn beatmapData
    public static class BeatmapVersionRegistry
    {
        // Holds beatmapData for each generated Gen360 map
        //public static Dictionary<BeatmapKey, BeatmapData> beatmapDataByKey
        //    = new Dictionary<BeatmapKey, BeatmapData>();
        public static Dictionary<BeatmapKey, Version> versionByKey
            = new Dictionary<BeatmapKey, Version>();
    }

    public sealed class BeatmapMetadata
    {
        public HashSet<string> Requirements { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> Suggestions { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> Warnings { get; }    = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> Information { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    }

    public static class MetadataRegistry
    {
        public static readonly Dictionary<BeatmapKey, BeatmapMetadata> findByKey = new Dictionary<BeatmapKey, BeatmapMetadata>();

        public static BeatmapMetadata GetOrCreate(BeatmapKey key)
        {
            if (!findByKey.TryGetValue(key, out var m))
                findByKey[key] = m = new BeatmapMetadata();
            return m;
        }
    }


    // Stores metadata about all available (including custom) difficulty sets for each level ID.
    /*
    public static class CustomBeatmapMetadataRegistry //v1.40 Stores IDifficultyBeatmapSet (doesn't exist in 1.40 so i re-created it) by levelID
    {
        // Stores metadata about all available (including custom) difficulty sets for each level ID.
        public static readonly Dictionary<string, List<IDifficultyBeatmapSet>> CustomSetsByLevelID = new Dictionary<string, List<IDifficultyBeatmapSet>>();
    }
    */
    public class IDifficultyBeatmapSet //v1.40 IDifficultyBeatmapSet no longer exists so replaced with this so could keep my code similar to old version
    {
        public BeatmapCharacteristicSO characteristic;
        public List<(BeatmapDifficulty difficulty, BeatmapBasicData data)> difficultyBeatmaps;
    }
}
