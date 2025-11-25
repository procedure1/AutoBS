using CustomJSONData.CustomBeatmap;
using HarmonyLib;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace AutoBS.Patches
{
    // OLD ONE - KEPT FOR REFERENCE
    // Postfix FileDifficultyBeatmap.GetBeatmapStringAsync - WORKS! Inject rotation events into the JSON string before it is parsed by Beat Saber after TransitionPatcher - WORKS for JSON maps only (not Gen360)
    // Chat GPT "RotationTimeProcessr" conversation
    // approach sidesteps all timing-and-cache puzzles by always putting your rotations into the very JSON that BeatmapDataTransformHelper parses—guaranteeing the built-in rotation scheduler and RotationTimeProcessor will pick them up
    // occurs after MenuTransitionsHelper TransitionPatcher so after presses play
    // async not working anymore - switched to sync version below
    /*
    [HarmonyPatch(typeof(FileDifficultyBeatmap), nameof(FileDifficultyBeatmap.GetBeatmapStringAsync))]
    static class GetBeatmapString_Patch_ASYNC
    {
        static void Postfix(FileDifficultyBeatmap __instance, ref Task<string> __result)
        {
            if (!Config.Instance.EnablePlugin) return;

            // Wrap the original task so we can await it safely.
            var original = __result;
            __result = WrapAsync(original);
        }

        static async Task<string> WrapAsync(Task<string> original)
        {
            Plugin.Log.Info("[GetBeatmapStringAsync] starting.");

            if (!TransitionPatcher.UserSelectedMapToInject)
            {
                Plugin.Log.Info("[GetBeatmapStringAsync] User has NOT selected a Gen 360 map yet.");
                return await original.ConfigureAwait(false);
            }

            try
            {
                var raw = await original.ConfigureAwait(false);
                if (string.IsNullOrEmpty(raw))
                {
                    Plugin.Log.Warn("[GetBeatmapStringAsync] raw JSON string is null or empty.");
                    return raw;
                }

                var (beatmapCustom, levelCustom, njo) = ExtractBeatmapInfo(raw);
                // Optionally return a modified string
                return raw;
            }
            catch (Exception ex)
            {
                Plugin.Log.Error($"[GetBeatmapStringAsync] Exception: {ex}");
                throw;
            }
        }



        public static (CustomData beatmapCustom, CustomData levelCustom, float njo) ExtractBeatmapInfo(string rawJson)
        {
            var jObj = JObject.Parse(rawJson);

            var levelCustomRaw = jObj["_customData"] as JObject;
            var levelCustomData = levelCustomRaw != null
                ? new CustomData(levelCustomRaw.ToObject<Dictionary<string, object>>())
                : new CustomData();

            var diffSet = jObj["_difficultyBeatmapSets"]?.FirstOrDefault();
            var beatmap = diffSet?["_difficultyBeatmaps"]?.FirstOrDefault();

            var beatmapCustomRaw = beatmap?["_customData"] as JObject;
            var beatmapCustomData = beatmapCustomRaw != null
                ? new CustomData(beatmapCustomRaw.ToObject<Dictionary<string, object>>())
                : new CustomData();

            //float njs = beatmap?["_noteJumpMovementSpeed"]?.Value<float>() ?? 10f;
            float njo = beatmap?["_noteJumpStartBeatOffset"]?.Value<float>() ?? 0f;


            return (beatmapCustomData, levelCustomData, njo);
        }

    }
    */


    // I am trying to go back to an older approach that was sending a json string directly to CreateTranformHelper. but before i already added rotations and arcs to it.
    // i'm doing this since required mods like noodle and chroma do not work with Gen360 maps if i use BeatmapDataLoader.LoadBeatmapDataAsync.
    // This is running now but crashes beat saber.
    // i get these 2 logs only:
    //[GetBeatmapString][SYNC] starting.
    //[[GetBeatmapString][SYNC] User has NOT selected a Gen 360 map yet.
    // FIGURE THIS OUT!!!!!!!!!!!!!!
    /*
    [HarmonyPatch(typeof(FileDifficultyBeatmap), nameof(FileDifficultyBeatmap.GetBeatmapString))]
    static class GetBeatmapString_Patch_SYNC
    {
        static void Postfix(FileDifficultyBeatmap __instance, ref string __result)
        {
            if (!Config.Instance.EnablePlugin) return;

            Plugin.Log.Info("[GetBeatmapString][SYNC] starting.");

            if (!TransitionPatcher.UserSelectedMapToInject)
            {
                Plugin.Log.Info("[GetBeatmapString][SYNC] User has NOT selected a Gen 360 map yet.");
                return;
            }

            try
            {
                var raw = __result; // it's already a string
                if (string.IsNullOrEmpty(raw))
                {
                    Plugin.Log.Warn("[GetBeatmapString][SYNC] raw JSON string is null or empty.");
                    return;
                }

                var (beatmapCustom, levelCustom, njo) = ExtractBeatmapInfo(raw);
                // Optionally modify __result = modifiedRaw;
            }
            catch (Exception ex)
            {
                Plugin.Log.Error($"[GetBeatmapString][SYNC] Exception: {ex}");
            }
        }
        public static (CustomData beatmapCustom, CustomData levelCustom, float njo) ExtractBeatmapInfo(string rawJson)
        {
            var jObj = JObject.Parse(rawJson);

            var levelCustomRaw = jObj["_customData"] as JObject;
            var levelCustomData = levelCustomRaw != null
                ? new CustomData(levelCustomRaw.ToObject<Dictionary<string, object>>())
                : new CustomData();

            var diffSet = jObj["_difficultyBeatmapSets"]?.FirstOrDefault();
            var beatmap = diffSet?["_difficultyBeatmaps"]?.FirstOrDefault();

            var beatmapCustomRaw = beatmap?["_customData"] as JObject;
            var beatmapCustomData = beatmapCustomRaw != null
                ? new CustomData(beatmapCustomRaw.ToObject<Dictionary<string, object>>())
                : new CustomData();

            //float njs = beatmap?["_noteJumpMovementSpeed"]?.Value<float>() ?? 10f;
            float njo = beatmap?["_noteJumpStartBeatOffset"]?.Value<float>() ?? 0f;


            return (beatmapCustomData, levelCustomData, njo);
        }
    }

    */
    /*
    // Clear Cache so can play Standard or Gen 360 maps of the same difficulty multiple times without having to restart the game. this is a problem since Beat Saber is given the same key for 360 and Standard maps since we have trick beat saber into thinking the 360 map has a json file since we cannot add functioning rotations otherwise.
    // this fires just before GetBeatmapStringAsync
    // Patch the async loader before its cache check
    [HarmonyPatch(typeof(BeatmapDataLoader), nameof(BeatmapDataLoader.LoadBeatmapDataAsync))]
    static class ConditionalEvictCachePatch
    {
        // remember what our last injection state was
        static bool? _lastShouldInject = null;

        static void Prefix(BeatmapDataLoader __instance)
        {
            bool currentShouldInject = TransitionPatcher.UserSelectedMapToInject;

            // if we have a previous value and it hasn't changed, bail out
            if (_lastShouldInject.HasValue && _lastShouldInject.Value == currentShouldInject)
            {
                Plugin.Log.Info("[EvictCache] Didn't need to clear cache since not playing the same map 2x");
                return;
            }

            // update for next time
            _lastShouldInject = currentShouldInject;

            // Now clear the one-entry cache so that the loader will re-run GetBeatmapStringAsync
            var cacheField = AccessTools.Field(typeof(BeatmapDataLoader), "_lastUsedBeatmapDataCache");
            if (cacheField == null)
            {
                Plugin.Log.Warn("[EvictCache] Couldn't find LastUsedBeatmapDataCache field!");
                return;
            }

            object defaultCache = Activator.CreateInstance(cacheField.FieldType);
            cacheField.SetValue(__instance, defaultCache);

            Plugin.Log.Debug($"[EvictCache] Cleared cache because ShouldInject changed to {currentShouldInject}");
        }
    }
    */
   
}
