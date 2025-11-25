using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace AutoBS
{
    // This is a logger for built-in maps. I was able to find the JSON for beatmap and lightshow. I was NOT able to find BPM/NJO/NJS
    public static class BuiltInBeatmapInspector
    {
        public static void LogBeatmapLevelsDataContentsDetailed()
        {
            string root = Path.Combine(Application.streamingAssetsPath, "BeatmapLevelsData");
            Plugin.Log.Info($"[FS] StreamingAssets/BeatmapLevelsData = {root}");

            if (!Directory.Exists(root))
            {
                Plugin.Log.Warn("[FS] That directory does not exist!");
                return;
            }

            // 1) Top-level listing
            Plugin.Log.Info("[FS] Top-level entries:");
            foreach (var entry in Directory.EnumerateFileSystemEntries(root))
            {
                bool isDir = Directory.Exists(entry);
                Plugin.Log.Info($"[FS] {(isDir ? "DIR " : "FILE")} {Path.GetFileName(entry)}");
            }

            // Limit inspection to 3 bundles
            int inspected = 0;
            const int MaxBundles = 3;

            // 2) For each file that looks like a bundle, open and inspect
            foreach (var file in Directory.EnumerateFiles(root))
            {
                // Many Beat Saber bundles under StreamingAssets are bare files without extension.
                // We'll just attempt to open everything and catch failures.
                InspectBundle(file);
                inspected++;
                if (inspected >= MaxBundles)
                {
                    Plugin.Log.Warn($"[FS] Reached limit of {MaxBundles} bundles — stopping early to avoid freezing.");
                    break;
                }
            }

            // 3) Also check subdirectories (some installs nest bundles in folders)
            foreach (var dir in Directory.GetDirectories(root))
            {
                Plugin.Log.Info($"[FS] -- Scanning subdir: {Path.GetFileName(dir)}");
                foreach (var sub in Directory.EnumerateFileSystemEntries(dir))
                    Plugin.Log.Info($"[FS] ---- {(Directory.Exists(sub) ? "DIR " : "FILE")} {Path.GetFileName(sub)}");

                InspectBundle(dir);
                inspected++;
                if (inspected >= MaxBundles)
                {
                    Plugin.Log.Warn($"[FS] Reached limit of {MaxBundles} bundles — stopping early to avoid freezing.");
                    return;
                }
            }
        }

        private static void InspectBundle(string bundlePath)
        {
            // Reflect Unity types and methods (do it once per call for safety)
            var abType = FindType("UnityEngine.AssetBundle");
            var unityObjType = FindType("UnityEngine.Object");
            var textAssetType = FindType("UnityEngine.TextAsset");

            // Use exact pickers (prevents AmbiguousMatchException on generic overloads)
            var loadFromFile = GetExactStaticMethod(abType, "LoadFromFile", typeof(string));
            var getAllAssetNames = GetExactInstanceMethod(abType, "GetAllAssetNames");   // 0 params, non-generic
            var getAllScenePaths = GetExactInstanceMethod(abType, "GetAllScenePaths");   // 0 params, non-generic
            var loadAllAssetsT = GetExactInstanceMethod(abType, "LoadAllAssets", typeof(Type)); // LoadAllAssets(Type)
            var loadAllAssets = GetExactInstanceMethod(abType, "LoadAllAssets");      // non-generic, 0 params
            var loadAsset = GetExactInstanceMethod(abType, "LoadAsset", typeof(string), typeof(Type));
            var unload = GetExactInstanceMethod(abType, "Unload", typeof(bool));

            if (loadFromFile == null || unload == null)
            {
                Plugin.Log.Warn("[Bundle] Required AssetBundle methods not found.");
                return;
            }


            object bundle = null;
            try
            {
                bundle = loadFromFile.Invoke(null, new object[] { bundlePath });
                if (bundle == null)
                {
                    Plugin.Log.Warn($"[Bundle] Could not load: {Path.GetFileName(bundlePath)}");
                    return;
                }

                Plugin.Log.Info($"[Bundle] ===== {Path.GetFileName(bundlePath)} =====");

                // List GetAllAssetNames
                if (getAllAssetNames != null)
                {
                    try
                    {
                        var names = (string[])getAllAssetNames.Invoke(bundle, null);
                        Plugin.Log.Info($"[Bundle] GetAllAssetNames count = {names.Length}");
                        foreach (var n in names) Plugin.Log.Info($"[Bundle]   → {n}");
                    }
                    catch (Exception ex) { Plugin.Log.Warn($"[Bundle] GetAllAssetNames error: {ex.Message}"); }
                }

                // List GetAllScenePaths (if any)
                if (getAllScenePaths != null)
                {
                    try
                    {
                        var paths = (string[])getAllScenePaths.Invoke(bundle, null);
                        if (paths != null && paths.Length > 0)
                        {
                            Plugin.Log.Info($"[Bundle] GetAllScenePaths count = {paths.Length}");
                            foreach (var p in paths) Plugin.Log.Info($"[Bundle]   (scene) {p}");
                        }
                    }
                    catch (Exception ex) { Plugin.Log.Warn($"[Bundle] GetAllScenePaths error: {ex.Message}"); }
                }

                // Load all TextAssets first (fast path to beatmap/lightshow JSONs)
                if (textAssetType != null && loadAllAssetsT != null)
                {
                    try
                    {
                        var taObjs = loadAllAssetsT.Invoke(bundle, new object[] { textAssetType }) as Array;
                        var cnt = taObjs?.Length ?? 0;
                        Plugin.Log.Info($"[Bundle] TextAssets found = {cnt}");
                        if (cnt > 0)
                        {
                            foreach (var ta in taObjs)
                                LogTextAssetSummary(ta);
                        }
                    }
                    catch (Exception ex) { Plugin.Log.Warn($"[Bundle] LoadAllAssets(TextAsset) error: {ex.Message}"); }
                }

                // Fall back: LoadAllAssets() (may include ScriptableObjects referencing TextAssets)
                if (loadAllAssets != null)
                {
                    try
                    {
                        var all = loadAllAssets.Invoke(bundle, null) as Array;
                        var total = all?.Length ?? 0;
                        Plugin.Log.Info($"[Bundle] LoadAllAssets() count = {total}");

                        if (all != null)
                        {
                            foreach (var obj in all)
                            {
                                if (obj == null)
                                    continue;

                                LogUnityObjectSummary(obj, textAssetType);

                                var type = obj.GetType();
                                // ✅ Check if it’s a BeatmapLevelDataSO
                                if (type.Name == "BeatmapLevelDataSO")
                                {
                                    Plugin.Log.Info($"[Inspector] Found BeatmapLevelDataSO → extracting beatmap JSONs...");
                                    try
                                    {
                                        DumpBeatmapJsonFromLevelDataSO(obj);
                                    }
                                    catch (Exception ex)
                                    {
                                        Plugin.Log.Warn($"[Inspector] DumpBeatmapJsonFromLevelDataSO error: {ex}");
                                    }
                                }

                                // 🔍 Optional: keep scanning for nested TextAssets
                                if (textAssetType != null && !textAssetType.IsAssignableFrom(type))
                                {
                                    ScanObjectForNestedClues(obj, textAssetType, maxDepth: 2);
                                }
                            }
                        }

                    }
                    catch (Exception ex) { Plugin.Log.Warn($"[Bundle] LoadAllAssets() error: {ex.Message}"); }
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.Warn($"[Bundle] Exception loading {Path.GetFileName(bundlePath)}: {ex}");
            }
            finally
            {
                if (bundle != null && unload != null)
                {
                    try { unload.Invoke(bundle, new object[] { false }); } catch { /* ignore */ }
                }
            }
        }

        private static void LogUnityObjectSummary(object unityObj, Type textAssetType)
        {
            if (unityObj == null) return;
            var t = unityObj.GetType();
            string name = SafeGetName(unityObj);
            if (textAssetType != null && textAssetType.IsAssignableFrom(t))
            {
                // TextAssets handled separately
                return;
            }

            Plugin.Log.Info($"[Asset] {t.FullName} name='{name}'");
        }

        private static void LogTextAssetSummary(object ta)
        {
            if (ta == null) return;
            string name = SafeGetName(ta);

            int textLen = -1;
            int byteLen = -1;
            bool gz = false;
            try
            {
                var t = ta.GetType();
                var textProp = t.GetProperty("text", BindingFlags.Public | BindingFlags.Instance);
                var bytesProp = t.GetProperty("bytes", BindingFlags.Public | BindingFlags.Instance);

                if (textProp?.GetValue(ta) is string s) textLen = s.Length;
                if (bytesProp?.GetValue(ta) is byte[] b)
                {
                    byteLen = b.Length;
                    gz = b.Length > 2 && b[0] == 0x1F && b[1] == 0x8B;
                }
            }
            catch { }

            Plugin.Log.Info($"[TextAsset] name='{name}', textLen={textLen}, byteLen={byteLen}, gzip={gz}");
        }

        private static void ScanObjectForNestedClues(object obj, Type textAssetType, int maxDepth)
        {
            if (obj == null || maxDepth < 0) return;

            var seen = new HashSet<object>(ReferenceEqualityComparer.Instance);
            var q = new Queue<(object node, int depth)>();
            void Enq(object o, int d)
            {
                if (o == null || d < 0) return;
                if (!seen.Add(o)) return;
                // avoid flooding with primitives/strings
                var ot = o.GetType();
                if (ot.IsPrimitive || o is string) return;
                q.Enqueue((o, d));
            }

            Enq(obj, maxDepth);

            while (q.Count > 0)
            {
                var (node, depth) = q.Dequeue();
                var type = node.GetType();

                // If we hit a TextAsset, log a one-line summary
                if (textAssetType.IsAssignableFrom(type))
                {
                    LogTextAssetSummary(node);
                    continue;
                }

                // Strings that look like JSON (don’t log full content)
                foreach (var f in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                {
                    try
                    {
                        var val = f.GetValue(node);
                        if (val == null) continue;

                        if (val is string s && LooksLikeBeatmapJson(s))
                            Plugin.Log.Info($"[Nested] {type.Name}.{f.Name} ← string(len={s.Length}) looks like JSON");
                        else if (val is Array arr)
                        {
                            int taCount = 0, strCount = 0;
                            foreach (var it in arr)
                            {
                                if (it == null) continue;
                                if (textAssetType.IsInstanceOfType(it)) taCount++;
                                else if (it is string) strCount++;
                                Enq(it, depth - 1);
                            }
                            if (taCount > 0 || strCount > 0)
                                Plugin.Log.Info($"[Nested] {type.Name}.{f.Name} ← array size={arr.Length} (TextAsset={taCount}, string={strCount})");
                        }
                        else if (!f.FieldType.IsPrimitive && !(val is string))
                        {
                            Enq(val, depth - 1);
                        }
                    }
                    catch { /* continue */ }
                }

                // Simple properties
                foreach (var p in type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                {
                    try
                    {
                        if (!p.CanRead || p.GetIndexParameters().Length != 0) continue;
                        var val = p.GetValue(node, null);
                        if (val == null) continue;

                        if (val is string ps && LooksLikeBeatmapJson(ps))
                            Plugin.Log.Info($"[Nested] {type.Name}.{p.Name} ← string(len={ps.Length}) looks like JSON");
                        else if (val is Array arr)
                        {
                            int taCount = 0, strCount = 0;
                            foreach (var it in arr)
                            {
                                if (it == null) continue;
                                if (textAssetType.IsInstanceOfType(it)) taCount++;
                                else if (it is string) strCount++;
                                Enq(it, depth - 1);
                            }
                            if (taCount > 0 || strCount > 0)
                                Plugin.Log.Info($"[Nested] {type.Name}.{p.Name} ← array size={arr.Length} (TextAsset={taCount}, string={strCount})");
                        }
                        else if (!p.PropertyType.IsPrimitive && !(val is string))
                        {
                            Enq(val, depth - 1);
                        }
                    }
                    catch { /* skip risky props */ }
                }
            }
        }

        // --- helpers ---

        private static Type FindType(string fullName) =>
            AppDomain.CurrentDomain
                     .GetAssemblies()
                     .Select(a => a.GetType(fullName))
                     .FirstOrDefault(t => t != null);

        private static string SafeGetName(object unityObj)
        {
            try
            {
                var p = unityObj.GetType().GetProperty("name", BindingFlags.Public | BindingFlags.Instance);
                if (p != null && p.GetGetMethod() != null)
                {
                    return p.GetValue(unityObj) as string ?? "<unnamed>";
                }
            }
            catch { }
            return "<unnamed>";
        }

        private sealed class ReferenceEqualityComparer : IEqualityComparer<object>
        {
            public static readonly ReferenceEqualityComparer Instance = new ReferenceEqualityComparer();
            public bool Equals(object x, object y) => ReferenceEquals(x, y);
            public int GetHashCode(object obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
        }


        private static MethodInfo GetExactInstanceMethod(Type t, string name, params Type[] paramTypes) =>
    t.GetMethods(BindingFlags.Public | BindingFlags.Instance)
     .FirstOrDefault(m =>
         m.Name == name &&
         !m.IsGenericMethod &&
         ParametersExactly(m.GetParameters(), paramTypes));

        private static MethodInfo GetExactStaticMethod(Type t, string name, params Type[] paramTypes) =>
            t.GetMethods(BindingFlags.Public | BindingFlags.Static)
             .FirstOrDefault(m =>
                 m.Name == name &&
                 !m.IsGenericMethod &&
                 ParametersExactly(m.GetParameters(), paramTypes));

        private static bool ParametersExactly(ParameterInfo[] pars, Type[] want)
        {
            if (pars.Length != want.Length) return false;
            for (int i = 0; i < pars.Length; i++)
            {
                // Require an exact match (no assignability); avoids surprises with object/Type/etc.
                if (pars[i].ParameterType != want[i]) return false;
            }
            return true;
        }

        private static void DumpBeatmapJsonFromLevelDataSO(object levelDataSO)
        {
            var textAssetType = FindType("UnityEngine.TextAsset");
            if (textAssetType == null) { Plugin.Log.Warn("[Inspector] UnityEngine.TextAsset not found."); return; }

            var t = levelDataSO.GetType();

            // Find sets: BeatmapLevelDataSO+DifficultyBeatmapSet[]
            var setsField = t.GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance)
                             .FirstOrDefault(f => f.FieldType.IsArray &&
                                                  f.FieldType.GetElementType()?.Name.Contains("DifficultyBeatmapSet") == true);
            if (setsField == null)
            {
                Plugin.Log.Warn("[Inspector] Could not find DifficultyBeatmapSet[] on BeatmapLevelDataSO.");
                LogAllFields("[Inspector] BeatmapLevelDataSO fields:", t, levelDataSO);
                return;
            }

            var sets = setsField.GetValue(levelDataSO) as Array;
            if (sets == null) { Plugin.Log.Warn("[Inspector] sets is null."); return; }

            foreach (var set in sets)
            {
                if (set == null) continue;
                var setType = set.GetType();

                // characteristic name is now a serialized string on the set
                string charName = "(unknown)";
                try
                {
                    var charNameField = setType.GetField("_beatmapCharacteristicSerializedName",
                        BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
                    if (charNameField?.GetValue(set) is string s && !string.IsNullOrEmpty(s))
                        charName = s;
                }
                catch { /* ignore */ }
                Plugin.Log.Info($"[Set] Characteristic = {charName}");

                // Find difficulties: List<BeatmapLevelDataSO+DifficultyBeatmap>
                var diffsListField = setType.GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance)
                    .FirstOrDefault(f =>
                        f.FieldType.IsGenericType &&
                        f.FieldType.GetGenericTypeDefinition() == typeof(List<>) &&
                        f.FieldType.GetGenericArguments()[0].Name.Contains("DifficultyBeatmap"));

                if (diffsListField == null)
                {
                    Plugin.Log.Warn("[Inspector] Could not find List<DifficultyBeatmap> on set.");
                    LogAllFields("[Inspector] DifficultyBeatmapSet fields:", setType, set);
                    continue;
                }

                var diffsListObj = diffsListField.GetValue(set);
                if (diffsListObj is System.Collections.IEnumerable diffsEnum)
                {
                    foreach (var diff in diffsEnum)
                    {
                        if (diff == null) continue;
                        var diffType = diff.GetType();

                        // Difficulty label (enum or string)
                        try
                        {
                            var dField = diffType.GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance)
                                                 .FirstOrDefault(f => f.FieldType.IsEnum ||
                                                                      f.FieldType.Name.Contains("BeatmapDifficulty"));
                            var dVal = dField?.GetValue(diff)?.ToString() ?? "(unknown)";
                            Plugin.Log.Info($"  [Diff] {dVal}");
                        }
                        catch { }

                        // Read ALL TextAssets on this difficulty
                        var taFields = diffType.GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance)
                                               .Where(f => textAssetType.IsAssignableFrom(f.FieldType))
                                               .ToArray();

                        if (taFields.Length == 0)
                        {
                            Plugin.Log.Warn("    [Diff] No TextAsset fields found; logging string-like clues then all fields.");
                            // Log string-ish clues first
                            foreach (var f in diffType.GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance))
                            {
                                if (f.FieldType == typeof(string))
                                {
                                    try
                                    {
                                        var val = f.GetValue(diff) as string;
                                        if (LooksLikeBeatmapJson(val))
                                            Plugin.Log.Info($"    [Clue] {f.Name} ← JSON string len={val.Length}");
                                        else if (!string.IsNullOrEmpty(val) &&
                                                (val.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ||
                                                 val.EndsWith(".dat", StringComparison.OrdinalIgnoreCase) ||
                                                 val.EndsWith(".gz", StringComparison.OrdinalIgnoreCase) ||
                                                 val.Contains("packages/") || val.Contains("/so/")))
                                            Plugin.Log.Info($"    [Clue] {f.Name} = '{val}'");
                                    }
                                    catch { }
                                }
                            }
                            // Full field dump for this diff
                            LogAllFields("    [Diff fields]", diffType, diff);
                        }
                        else
                        {
                            foreach (var f in taFields)
                            {
                                object ta = null;
                                try { ta = f.GetValue(diff); } catch { }
                                if (ta == null) continue;

                                var taName = SafeGetName(ta);
                                if (TryReadTextAssetJson(ta, out var json))
                                {
                                    var preview = json.Length > 240 ? json.Substring(0, 240) + " …" : json;
                                    Plugin.Log.Info($"    [JSON] {f.Name} (TextAsset='{taName}') len={json.Length} preview: {preview}");
                                }
                                else
                                {
                                    var stats = GetTextAssetStats(ta);
                                    Plugin.Log.Info($"    [TA] {f.Name} (TextAsset='{stats.Name}') textLen={stats.TextLen} bytes={(stats.Bytes?.Length ?? -1)} gz={stats.Gz}");
                                }
                            }
                        }
                    }
                }
                else
                {
                    Plugin.Log.Warn("[Inspector] _difficultyBeatmaps is not IEnumerable.");
                }
            }
        }


        // Reads quick stats from a TextAsset (name, text length, byte length, gzip flag)
        private static (string Name, int TextLen, byte[] Bytes, bool Gz) GetTextAssetStats(object ta)
        {
            string name = SafeGetName(ta);
            int textLen = -1;
            byte[] bytes = null;
            bool gz = false;

            try
            {
                var t = ta.GetType();
                var textProp = t.GetProperty("text", BindingFlags.Public | BindingFlags.Instance);
                var bytesProp = t.GetProperty("bytes", BindingFlags.Public | BindingFlags.Instance);

                if (textProp?.GetValue(ta) is string s)
                    textLen = s.Length;

                if (bytesProp?.GetValue(ta) is byte[] b)
                {
                    bytes = b;
                    gz = b.Length > 2 && b[0] == 0x1F && b[1] == 0x8B; // GZip header 1F 8B
                }
            }
            catch { /* ignore reflection errors */ }

            return (name, textLen, bytes, gz);
        }


        // Utility: log every field name/type/value (truncated) for a given object
        private static void LogAllFields(string header, Type type, object instance)
        {
            Plugin.Log.Info(header);
            foreach (var f in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                string v = "";
                try
                {
                    var val = f.GetValue(instance);
                    if (val == null) v = "null";
                    else
                    {
                        if (val is Array arr) v = $"Array[{arr.Length}] of {f.FieldType.GetElementType()?.Name}";
                        else if (val is string s) v = $"string len={s.Length}";
                        else v = val.GetType().Name;
                    }
                }
                catch { v = "(error reading)"; }
                Plugin.Log.Info($"    - {f.FieldType.FullName} {f.Name} = {v}");
            }
        }


        // Reads JSON text from a UnityEngine.TextAsset (handles .text and .bytes + gzip)
        private static bool TryReadTextAssetJson(object textAsset, out string json)
        {
            json = null;
            if (textAsset == null)
                return false;

            var taType = textAsset.GetType();
            var textProp = taType.GetProperty("text", BindingFlags.Public | BindingFlags.Instance);
            var bytesProp = taType.GetProperty("bytes", BindingFlags.Public | BindingFlags.Instance);

            // 1️⃣ Try the .text property first
            if (textProp != null)
            {
                try
                {
                    var s = textProp.GetValue(textAsset) as string;
                    if (LooksLikeBeatmapJson(s))
                    {
                        json = s;
                        return true;
                    }
                }
                catch { /* ignored */ }
            }

            // 2️⃣ Fallback to .bytes (may be gzipped)
            if (bytesProp != null)
            {
                try
                {
                    if (bytesProp.GetValue(textAsset) is byte[] data && data.Length > 0)
                    {
                        // gzip header 1F 8B
                        if (data.Length > 2 && data[0] == 0x1F && data[1] == 0x8B)
                        {
                            using var ms = new MemoryStream(data);
                            using var gz = new System.IO.Compression.GZipStream(ms, System.IO.Compression.CompressionMode.Decompress);
                            using var sr = new StreamReader(gz, System.Text.Encoding.UTF8);
                            var s = sr.ReadToEnd();
                            if (LooksLikeBeatmapJson(s))
                            {
                                json = s;
                                return true;
                            }
                        }
                        else
                        {
                            var s = System.Text.Encoding.UTF8.GetString(data);
                            if (LooksLikeBeatmapJson(s))
                            {
                                json = s;
                                return true;
                            }
                        }
                    }
                }
                catch { /* ignored */ }
            }

            return false;
        }

        // Simple heuristic: valid beatmap/lightshow JSON starts with '{' and common keys
        private static bool LooksLikeBeatmapJson(string s)
        {
            if (string.IsNullOrEmpty(s))
                return false;
            if (s[0] != '{')
                return false;

            return s.Contains("\"version\"") ||
                   s.Contains("\"_version\"") ||
                   s.Contains("\"colorNotes\"") ||
                   s.Contains("\"basicBeatmapEvents\"") ||
                   s.Contains("\"lightshow\"") ||
                   s.Contains("\"beatmapData\"");
        }



    }

}
