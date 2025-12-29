using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace AutoBS
{
    //v1.42 can't access built-in asset bundles anymore since says they are already loaded and we can't get access to the cached bundle 
    /*
    // Used by SetContent to load Built-in vanilla json maps
    public static class BuiltInMapJsonLoader
    {
        // ------------ public API ------------
        public static void PrimeBuiltInLevel(string levelID)
        {
            lock (_gate)
            {
                if (_levels.TryGetValue(levelID.ToLowerInvariant(), out var cached) && cached.IsComplete)
                {
                    Plugin.LogDebug($"[CACHE] '{levelID}' already primed.");
                    return;
                }

                //Plugin.Log.Info($"[CACHE] Priming '{levelID}' (open bundle once, extract everything) …");

                // Open bundle once
                var bundlePath = Path.Combine(Application.streamingAssetsPath, "BeatmapLevelsData", levelID.ToLowerInvariant());
                if (!File.Exists(bundlePath)) throw new FileNotFoundException($"No AssetBundle at {bundlePath}");

                var abType = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("UnityEngine.AssetBundle")).First(t => t != null);
                var textAssetType = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("UnityEngine.TextAsset")).First(t => t != null);

                var loadFromFile = abType
                    .GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .First(m => m.Name == "LoadFromFile"
                             && !m.IsGenericMethod
                             && m.GetParameters().Length == 1
                             && m.GetParameters()[0].ParameterType == typeof(string));

                var unload = abType
                    .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .First(m => m.Name == "Unload"
                             && !m.IsGenericMethod
                             && m.GetParameters().Length == 1
                             && m.GetParameters()[0].ParameterType == typeof(bool));

                var loadAllAssets = abType
                    .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .First(m => m.Name == "LoadAllAssets"
                             && !m.IsGenericMethod
                             && m.GetParameters().Length == 0);


                object bundle = null;
                try
                {
                    bundle = loadFromFile.Invoke(null, new object[] { bundlePath });
                    if (bundle == null) throw new Exception("AssetBundle.LoadFromFile returned null.");

                    var all = (Array)loadAllAssets.Invoke(bundle, null);
                    var levelDataSO = all.Cast<object>().FirstOrDefault(o => o != null && o.GetType().Name == "BeatmapLevelDataSO");
                    if (levelDataSO == null) throw new Exception("BeatmapLevelDataSO not found.");

                    // Get audioDataJson once
                    var audioJson = BuiltInMapJsonLoader_Tiny.TryGetAudioJsonFast(levelDataSO, textAssetType, all, abType, bundle);

                    // Walk all sets/diffs and extract all JSONs once
                    var sets = BuiltInMapJsonLoader_Tiny.GetDifficultyBeatmapSetsFast(levelDataSO) ?? Array.Empty<object>();
                    var storage = new LevelStore { audioDataJson = audioJson };

                    foreach (var set in sets)
                    {
                        var charName = BuiltInMapJsonLoader_Tiny.GetCharacteristicSerializedNameFast(set) ?? "(unknown)";
                        var diffs = BuiltInMapJsonLoader_Tiny.GetDifficultyBeatmapsFast(set) ?? new List<object>();
                        foreach (var diff in diffs)
                        {
                            var dlabel = BuiltInMapJsonLoader_Tiny.GetDifficultyLabelFast(diff) ?? "(unknown)";
                            var (bm, ls) = BuiltInMapJsonLoader_Tiny.ReadBeatmapAndLightshowFast(diff, textAssetType);
                            if (!storage.byChar.TryGetValue(charName, out var map)) storage.byChar[charName] = map = new Dictionary<string, (string bm, string ls)>(StringComparer.OrdinalIgnoreCase);
                            map[dlabel] = (bm, ls);
                        }
                    }

                    storage.IsComplete = true;
                    _levels[levelID.ToLowerInvariant()] = storage;
                    Plugin.LogDebug($"[BuiltInMapJsonLoader][CACHE] Primed '{levelID}': chars={storage.byChar.Count}, audioDataJsonLen={(audioJson?.Length ?? 0)}");
                }
                finally
                {
                    try { if (bundle != null) abType.GetMethod("Unload", new[] { typeof(bool) })?.Invoke(bundle, new object[] { false }); } catch { }
                }
            }
        }

        public static bool TryGetBuiltInJson(string levelID, string characteristic, string difficulty,
            out string beatmapJson, out string lightshowJson, out string audioDataJson)
        {
            beatmapJson = lightshowJson = audioDataJson = null;
            lock (_gate)
            {
                if (!_levels.TryGetValue(levelID.ToLowerInvariant(), out var store)) return false;
                audioDataJson = store.audioDataJson;
                if (store.byChar.TryGetValue(characteristic, out var map) && map.TryGetValue(difficulty, out var pair))
                {
                    beatmapJson = pair.bm; lightshowJson = pair.ls; return true;
                }
                return false;
            }
        }

        // Optional: clear everything (e.g., when leaving song selection)
        public static void ClearAll()
        {
            lock (_gate) { _levels.Clear(); }
        }

        // ------------ storage ------------
        private sealed class LevelStore
        {
            public string audioDataJson;
            public bool IsComplete;
            public readonly Dictionary<string, Dictionary<string, (string bm, string ls)>> byChar =
                new Dictionary<string, Dictionary<string, (string bm, string ls)>>(StringComparer.OrdinalIgnoreCase);
        }

        private static readonly object _gate = new object();
        private static readonly Dictionary<string, LevelStore> _levels = new Dictionary<string, LevelStore>(StringComparer.OrdinalIgnoreCase);

        public static string SafeGetName(object unityObj)
        {
            if (unityObj == null) return null;

            var t = unityObj.GetType();

            // Try common Unity pattern: Object.name (lowercase) first
            try
            {
                var p = t.GetProperty("name", BindingFlags.Public | BindingFlags.Instance);
                if (p != null && p.PropertyType == typeof(string))
                    return p.GetValue(unityObj, null) as string;
            }
            catch {  }

            // Some wrappers/exotics might expose "Name"
            try
            {
                var p = t.GetProperty("Name", BindingFlags.Public | BindingFlags.Instance);
                if (p != null && p.PropertyType == typeof(string))
                    return p.GetValue(unityObj, null) as string;
            }
            catch {  }

            // Last resort: ToString() (often "TextAsset: foo" or just type name)
            try
            {
                var s = unityObj.ToString();
                // Clean up common "Type: name" format if present
                if (!string.IsNullOrEmpty(s))
                {
                    int colon = s.IndexOf(':');
                    if (colon >= 0 && colon + 1 < s.Length)
                        return s.Substring(colon + 1).Trim();
                    return s;
                }
            }
            catch {  }

            return null;
        }
    }

    // helpers that reuse your existing logic but without timers/log spam.
    internal static class BuiltInMapJsonLoader_Tiny
    {
        public static string TryGetAudioJsonFast(object levelDataSO, Type textAssetType, Array all, Type abType, object bundle)
            => BuiltInMapJsonLoader_TryRead.TryGetAudioDataJson(levelDataSO, textAssetType, all, abType, bundle);

        public static object[] GetDifficultyBeatmapSetsFast(object levelDataSO)
            => BuiltInMapJsonLoader_TryRead.GetDifficultyBeatmapSets(levelDataSO) ?? Array.Empty<object>();

        public static List<object> GetDifficultyBeatmapMaps(object set)
            => BuiltInMapJsonLoader_TryRead.GetDifficultyBeatmaps(set) ?? new List<object>();

        public static string GetCharacteristicSerializedNameFast(object set)
            => BuiltInMapJsonLoader_TryRead.GetCharacteristicSerializedName(set);

        public static List<object> GetDifficultyBeatmapFast(object set)
            => BuiltInMapJsonLoader_TryRead.GetDifficultyBeatmaps(set);

        public static List<object> GetDifficultyBeatmapsFast(object set)
            => BuiltInMapJsonLoader_TryRead.GetDifficultyBeatmaps(set);

        public static string GetDifficultyLabelFast(object diff)
            => BuiltInMapJsonLoader_TryRead.GetDifficultyLabel(diff);

        public static (string bm, string ls) ReadBeatmapAndLightshowFast(object diff, Type textAssetType)
        {
            string bm = null, ls = null;
            var taFields = diff.GetType()
                .GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(f => textAssetType.IsAssignableFrom(f.FieldType))
                .ToArray();

            foreach (var f in taFields)
            {
                var ta = f.GetValue(diff);
                if (ta == null) continue;
                if (BuiltInMapJsonLoader_TryRead.TryReadTextAssetJson(ta, out var json))
                {
                    if (bm == null && (f.Name.IndexOf("beatmap", StringComparison.OrdinalIgnoreCase) >= 0 || BuiltInMapJsonLoader_TryRead.LooksLikeBeatmapJson(json)))
                        bm = json;
                    else if (ls == null && (f.Name.IndexOf("lightshow", StringComparison.OrdinalIgnoreCase) >= 0 || BuiltInMapJsonLoader_TryRead.LooksLikeLightshowJson(json)))
                        ls = json;
                }
            }

            // final fallback by content
            if (bm == null || ls == null)
            {
                foreach (var f in taFields)
                {
                    var ta = f.GetValue(diff);
                    if (ta == null) continue;
                    if (!BuiltInMapJsonLoader_TryRead.TryReadTextAssetJson(ta, out var json)) continue;
                    if (bm == null && BuiltInMapJsonLoader_TryRead.LooksLikeBeatmapJson(json)) bm = json;
                    else if (ls == null && BuiltInMapJsonLoader_TryRead.LooksLikeLightshowJson(json)) ls = json;
                }
            }

            return (bm, ls);
        }
    }

    // Reuse the routines from your existing loader (no timers). You can
    // put these as 'internal' methods on your current class instead.
    internal static class BuiltInMapJsonLoader_TryRead
    {
        public static string TryGetAudioDataJson(object levelDataSO, Type textAssetType, Array all, Type abType, object bundle)
        {
            try
            {
                var f = levelDataSO.GetType().GetField("_audioDataAsset", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
                var ta = f?.GetValue(levelDataSO);
                if (ta != null && textAssetType.IsInstanceOfType(ta) && TryReadTextAssetText(ta, out var json))
                    return json;
            }
            catch { }

            // light fallback pass: scan loaded TextAssets for "*audio*"
            try
            {
                foreach (var obj in all.Cast<object>())
                {
                    if (obj == null) continue;
                    if (!textAssetType.IsAssignableFrom(obj.GetType())) continue;
                    var name = BuiltInMapJsonLoader.SafeGetName(obj) ?? "";
                    if ((name.IndexOf("audio", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         name.EndsWith(".audio.gz", StringComparison.OrdinalIgnoreCase)) &&
                        TryReadTextAssetText(obj, out var json))
                        return json;
                }
            }
            catch { }

            return null;
        }

        public static object[] GetDifficultyBeatmapSets(object levelDataSO)
        {
            var t = levelDataSO.GetType();
            var setsField = t.GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance)
                .FirstOrDefault(f => f.FieldType.IsArray &&
                                     f.FieldType.GetElementType()?.Name.Contains("DifficultyBeatmapSet") == true);
            return (setsField?.GetValue(levelDataSO) as Array)?.Cast<object>().ToArray();
        }

        public static string GetCharacteristicSerializedName(object set)
        {
            var setType = set.GetType();
            var charNameField = setType.GetField("_beatmapCharacteristicSerializedName",
                BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
            if (charNameField?.GetValue(set) is string s && !string.IsNullOrEmpty(s)) return s;
            var charField = setType.GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance)
                .FirstOrDefault(f => f.FieldType.Name.Contains("Characteristic"));
            var charSO = charField?.GetValue(set);
            var nameProp = charSO?.GetType().GetProperty("serializedName", BindingFlags.Public | BindingFlags.Instance);
            return nameProp?.GetValue(charSO) as string;
        }

        public static List<object> GetDifficultyBeatmapsets(object set) => GetDifficultyBeatmaps(set);

        public static List<object> GetDifficultyBeatmaps(object set)
        {
            var setType = set.GetType();
            var diffsListField = setType.GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance)
                .FirstOrDefault(f =>
                    f.FieldType.IsGenericType &&
                    f.FieldType.GetGenericTypeDefinition() == typeof(List<>) &&
                    f.FieldType.GetGenericArguments()[0].Name.Contains("DifficultyBeatmap"));
            if (diffsListField?.GetValue(set) is IEnumerable ie) return ie.Cast<object>().ToList();

            var diffsArrayField = setType.GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance)
                .FirstOrDefault(f => f.FieldType.IsArray &&
                                     f.FieldType.GetElementType()?.Name.Contains("DifficultyBeatmap") == true &&
                                     !f.FieldType.GetElementType().Name.Contains("Set"));
            return (diffsArrayField?.GetValue(set) as Array)?.Cast<object>().ToList();
        }

        public static string GetDifficultyLabel(object diff)
        {
            var dt = diff.GetType();
            var enumField = dt.GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance)
                            .FirstOrDefault(f => f.FieldType.IsEnum || f.FieldType.Name.Contains("BeatmapDifficulty"));
            var s = enumField?.GetValue(diff)?.ToString();
            if (!string.IsNullOrEmpty(s)) return s;

            var strField = dt.GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance)
                           .FirstOrDefault(f => f.FieldType == typeof(string) && f.Name.IndexOf("difficulty", StringComparison.OrdinalIgnoreCase) >= 0);
            return strField?.GetValue(diff) as string;
        }

        public static bool TryReadTextAssetJson(object textAsset, out string json)
        {
            if (TryReadTextAssetText(textAsset, out var s) && LooksLikeAnyMapJson(s))
            { json = s; return true; }
            json = null; return false;
        }

        public static bool TryReadTextAssetText(object textAsset, out string text)
        {
            text = null; if (textAsset == null) return false;
            var t = textAsset.GetType();
            var bytesProp = t.GetProperty("bytes", BindingFlags.Public | BindingFlags.Instance);
            var textProp = t.GetProperty("text", BindingFlags.Public | BindingFlags.Instance);

            try
            {
                if (bytesProp?.GetValue(textAsset) is byte[] data && data.Length > 0)
                {
                    if (data.Length > 2 && data[0] == 0x1F && data[1] == 0x8B)
                    {
                        using var ms = new MemoryStream(data);
                        using var gz = new System.IO.Compression.GZipStream(ms, System.IO.Compression.CompressionMode.Decompress);
                        using var sr = new StreamReader(gz, System.Text.Encoding.UTF8);
                        text = sr.ReadToEnd();
                        return !string.IsNullOrEmpty(text);
                    }
                    else
                    {
                        text = System.Text.Encoding.UTF8.GetString(data);
                        return !string.IsNullOrEmpty(text) && text.TrimStart().StartsWith("{");
                    }
                }
            }
            catch { }

            try
            {
                if (textProp?.GetValue(textAsset) is string s && !string.IsNullOrEmpty(s) && s.TrimStart().StartsWith("{"))
                { text = s; return true; }
            }
            catch { }

            return false;
        }

        public static bool LooksLikeAnyMapJson(string s) => LooksLikeBeatmapJson(s) || LooksLikeLightshowJson(s);
        public static bool LooksLikeBeatmapJson(string s)
            => !string.IsNullOrEmpty(s) && s[0] == '{' && s.Contains("\"version\"") &&
               (s.Contains("\"colorNotes\"") || s.Contains("\"notes\"") || s.Contains("\"sliders\""));
        public static bool LooksLikeLightshowJson(string s)
            => !string.IsNullOrEmpty(s) && s[0] == '{' && s.Contains("\"version\"") &&
               (s.Contains("\"basicEvents\"") || s.Contains("\"lightshow\"") || s.Contains("\"events\""));


        private static void LogAudioFieldSummary(object levelDataSO, Type textAssetType)
        {
            var t = levelDataSO.GetType();
            var audioField = t.GetField("_audioDataAsset", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
            if (audioField == null)
            {
                Plugin.LogDebug("[AudioMeta] _audioDataAsset field not found on BeatmapLevelDataSO.");
                return;
            }
            var val = audioField.GetValue(levelDataSO);
            if (val == null) { Plugin.LogDebug("[AudioMeta] _audioDataAsset is NULL."); return; }

            var typeName = val.GetType().FullName;
            var name = BuiltInMapJsonLoader.SafeGetName(val);
            //Plugin.LogDebug($"[AudioMeta] _audioDataAsset → {typeName} name='{name ?? "(null)"}'");
        }

    }
    */
}
