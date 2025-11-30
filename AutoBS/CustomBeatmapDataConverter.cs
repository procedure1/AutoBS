using AutoBS.Patches;
using BeatmapSaveDataVersion3;
using CustomJSONData.CustomBeatmap;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace AutoBS
{
    public static class CustomBeatmapDataConverter
    {
        /// <summary>
        /// Produce a JSON string from CustomBeatmapData. Works for v2 and v3 maps.
        /// </summary>
        /// <param name="data"></param>
        /// <param name="preferredVersion"></param>
        /// <param name="outputSecondsToBeats"></param>
        /// <returns></returns>
        public static bool ToJsonStringFile(CustomBeatmapData data, List<ERotationEventData> rotationEvents, bool outputSecondsToBeats = true) // if converted to seconds for FromJsonString then need this
        {
            // ───────── Common setup ─────────────────────────────────────
            float timeMult = 1;
            if (outputSecondsToBeats)
                timeMult = TransitionPatcher.bpm / 60f; // convert from bpm to bps

            int majorVersion = data.version.Major;

            JObject root = new JObject();

            // Output V2 (no arcs/chains) if requested
            if (Config.Instance.OutputV2JsonToSongFolderNoArcsNoChainsNoMappingExtensionWalls)
            {
                Plugin.Log.Info($"[ToJsonStringFile] Outputting V2 JSON...");
                root = JsonV2Output(data, timeMult, rotationEvents);
                createFile(root, 2);
            }

            // Output V3 if requested
            if (Config.Instance.OutputV3JsonToSongFolder)
            {
                Plugin.Log.Info($"[ToJsonStringFile] Outputting V3 JSON...");
                root = JsonV3Output(data, timeMult, rotationEvents);
                createFile(root, 3);
            }

            // Reset only if "turn off after one play" is enabled
            if (Config.Instance.TurnOffJSONDatOutputAfterOneMapPlay)
            {
                Config.Instance.OutputV2JsonToSongFolderNoArcsNoChainsNoMappingExtensionWalls = false;
                Config.Instance.OutputV3JsonToSongFolder = false;
                Config.Instance.Changed();
            }

            void createFile (JObject root, int version) 
            {
                string jsonString = JsonConvert.SerializeObject(root, Formatting.None);

                string fileName = $"{TransitionPatcher.SelectedCharacteristicSO.serializedName}{TransitionPatcher.SelectedDifficulty}_v{version}_AutoBS_Generator.dat";
                

                string path;

                if (!string.IsNullOrEmpty(SetContent.SongFolderPath))
                {
                    path = Path.Combine(SetContent.SongFolderPath, fileName);
                }
                else
                {
                    path = Path.Combine(@"D:\", fileName);
                }

                File.WriteAllText(path, jsonString);

                Plugin.Log.Info($"[ToJsonStringFile] Original Map v{data.version}. Outputing JSON file to: {path}");

                File.WriteAllText(path, jsonString);
            }

            return true;
        }

        public static JObject JsonV2Output(CustomBeatmapData data, float timeMult, List<ERotationEventData> rotationEventsData) //v2 maps
        {
            var root = new JObject
            {
                ["_version"] = (data.version < new Version(2, 6, 0)
                                ? new Version(2, 6, 0)
                                : data.version
                            ).ToString()
            };

            if (data.beatmapCustomData.Count > 0)
                root["_customData"] = JObject.FromObject(data.beatmapCustomData);

            // ───────── NOTES ───────────────────────────────────────────
            var notes = new JArray();
            foreach (var n in data.beatmapObjectDatas.OfType<CustomNoteData>())
            {
                var o = new JObject
                {
                    ["_time"] = n.time * timeMult,
                    ["_lineIndex"] = n.lineIndex,
                    ["_lineLayer"] = (int)n.noteLineLayer,
                    ["_type"] = (int)n.colorType,
                    ["_cutDirection"] = (int)n.cutDirection
                };
                // ... copy your optional fields as before ...
                if (n.customData.Count > 0)
                    o["_customData"] = JObject.FromObject(n.customData);

                notes.Add(o);
            }
            root["_notes"] = notes;

            // ───────── OBSTACLES ───────────────────────────────────────
            var obs = new JArray();
            foreach (var w in data.beatmapObjectDatas.OfType<CustomObstacleData>())
            {
                if (!TryGetPlainV2ObstacleType(w, out int legacyType))
                    continue; // skip Mapping Extensions / weird walls

                var o = new JObject
                {
                    ["_time"] = w.time * timeMult,
                    ["_lineIndex"] = w.lineIndex,            // safe (we filtered ≥1000)
                    ["_type"] = legacyType,                  // 0 or 1 only
                    ["_duration"] = w.duration * timeMult,
                    ["_width"] = w.width                     // safe (we filtered ≥1000)
                };

                /*
                var o = new JObject //couldn't get this to work with Mapping Extensions walls
                {
                    ["_time"] = w.time * timeMult,
                    ["_lineIndex"] = w.lineIndex,
                    ["_lineLayer"] = (int)w.lineLayer,//v2.6 has this
                    ["_type"] = GetLegacyWallType1(w),
                    ["_duration"] = w.duration * timeMult,
                    ["_width"] = w.width,
                    ["_height"] = w.height//v2.6 has this
                };
                */

                if (w.customData.Count > 0)
                    o["_customData"] = JObject.FromObject(w.customData);
                obs.Add(o);
            }
            root["_obstacles"] = obs;

            bool TryGetPlainV2ObstacleType(CustomObstacleData obstacle, out int legacyType)
            {
                legacyType = 0;

                // 1) Reject Mapping Extensions / weird data outright

                // ME precise lanes / out-of-bounds crazy positions
                if (Math.Abs(obstacle.lineIndex) >= 1000)
                    return false;

                // ME precise widths (>= 1000 or <= -1000)
                if (Math.Abs(obstacle.width) >= 1000)
                    return false;

                // ME-coded heights (>= 1000 / <= -1000)
                if (obstacle.height >= 1000 || obstacle.height <= -1000)
                    return false;

                // If you ALSO want to reject extension walls (far left/right lanes),
                // uncomment one of these:
                // if (obstacle.lineIndex < 0 || obstacle.lineIndex > 3)
                //     return false;

                // 2) Now only accept "real" v2 shapes

                int layer = (int)obstacle.lineLayer;
                int height = obstacle.height;

                // Full-height wall: v3 style (layer 0, height 5)
                if (layer == 0 && height == 5)
                {
                    legacyType = 0; // v2 full-height
                    return true;
                }

                // Crouch wall: v3 style (layer 2, height 3)
                if (layer == 2 && height == 3)
                {
                    legacyType = 1; // v2 crouch
                    return true;
                }

                // Everything else = not safely representable in plain v2
                return false;
            }

            int GetLegacyWallType1(CustomObstacleData obstacle)
            {
                if ((int)obstacle.lineLayer == 0 && obstacle.height == 5)
                    return 0; // Full-height wall
                if ((int)obstacle.lineLayer == 2 && obstacle.height == 3)
                    return 1; // Crouch wall
                return 2;     // Free wall (V3-style)
            }
            
            int ConvertToV2Type(CustomObstacleData obstacle)
            {
                // Reverse the Mapping Extensions decoding:
                // outputHeight = obsHeight * 5 + 1000
                // outputLayer = startHeight * (5000 / 750) + 1334

                // Solve for obsHeight: obsHeight = (height - 1000) / 5
                int obsHeight = (obstacle.height - 1000) / 5;

                // Solve for startHeight: startHeight = (lineLayer - 1334) * 750 / 5000
                int startHeight = ((int)obstacle.lineLayer - 1334) * 750 / 5000;

                // Clamp to valid ranges
                if (obsHeight < 0) obsHeight = 0;
                if (startHeight < 0) startHeight = 0;
                if (startHeight > 999) startHeight = 999;

                // Encode: type = 4001 + (obsHeight * 1000) + startHeight
                return 4001 + (obsHeight * 1000) + startHeight;
            }

            int GetLegacyWallType2(CustomObstacleData obstacle)
            {
                int layer = (int)obstacle.lineLayer;
                int h = obstacle.height;

                // ── Vanilla walls ─────────────────────────────────────────
                // Full-height wall (standard v2)
                if (layer == 0 && h == 5)
                    return 0;

                // Crouch wall (standard v2)
                if (layer == 2 && h == 3)
                    return 1;

                // ── Mapping Extensions-coded heights (your particle walls etc.) ────
                float trackHeight;

                if (h >= 1000)
                {
                    // v3 ME coding: tiny walls like 1050, 1100, 1500, 2500...
                    trackHeight = (h - 1000) / 1000f;
                }
                else if (h <= -1000)
                {
                    // negative ME coding, if you ever use it
                    trackHeight = (h + 2000) / 1000f;
                }
                else if (h > 2)
                {
                    // direct "track units" height
                    trackHeight = h;
                }
                else
                {
                    // very small or weird: just give them something tiny
                    trackHeight = 0.1f;
                }

                // don't let anything go insane
                trackHeight = Mathf.Clamp(trackHeight, 0f, 5f);

                // From v2 ME math: finalHeight_v2 = 5 * obsHeight / 1000
                // => obsHeight = trackHeight * 1000 / 5
                int obsHeight = Mathf.RoundToInt(trackHeight * 1000f / 5f);
                obsHeight = Mathf.Clamp(obsHeight, 0, 4000);

                // Now map your integer v3 lineLayer -> startHeight
                // using the relation: layerInt ≈ 1334 + startHeight * 5000/750 ≈ 1000 * layer
                // ⇒ startHeight ≈ 150 * layer - 200
                float rawStart = 150f * layer - 200f;
                int startHeight = Mathf.RoundToInt(rawStart);
                startHeight = Mathf.Clamp(startHeight, 0, 999);

                // PreciseHeightStart encoding (4001+):
                // type = wallHeight * 1000 + startHeight + 4001
                int type = 4001 + obsHeight * 1000 + startHeight;

                return type;
            }

            /*
            bool useV2Sliders = false; //these do not work and cause infinite arcs. i cannot find a single v2 map that uses arcs.
            if (useV2Sliders)
            {
                // ───────── SLIDERS (ARCS) ───────────────────────────────────
                var sliders = new JArray();
                foreach (var s in data.beatmapObjectDatas
                                  .OfType<CustomSliderData>()
                                  .Where(x => x.sliderType == SliderData.Type.Normal))
                {
                    var o = new JObject
                    {
                        ["_colorType"] = (int)s.colorType,
                        ["_headTime"] = s.time * timeMult,
                        // ["_headBeat"] = s.beat,
                        // ["_rotation"] = s.rotation,
                        ["_headLineIndex"] = s.headLineIndex,
                        ["_headLineLayer"] = (int)s.headLineLayer,
                        //["_headBeforeJumpLineLayer"] = (int)s.headBeforeJumpLineLayer,
                        ["_headControlPointLengthMultiplier"] = s.headControlPointLengthMultiplier,
                        ["_headCutDirection"] = (int)s.headCutDirection,
                        ["_tailTime"] = s.tailTime * timeMult,
                        //["_tailRotation"] = s.tailRotation,
                        ["_tailLineIndex"] = s.tailLineIndex,
                        ["_tailLineLayer"] = (int)s.tailLineLayer,
                        //["_tailBeforeJumpLineLayer"] = (int)s.tailBeforeJumpLineLayer,
                        ["_tailControlPointLengthMultiplier"] = s.tailControlPointLengthMultiplier,
                        ["_tailCutDirection"] = (int)s.tailCutDirection,
                        ["_sliderMidAnchorMode"] = (int)s.midAnchorMode
                    };
                    if (s.customData.Count > 0)
                        o["_customData"] = JObject.FromObject(s.customData);
                    sliders.Add(o);
                }
                if (sliders.Count > 0)
                    root["_sliders"] = sliders;
            
            // ───────── BURST SLIDERS (CHAINS) v3 only!!!! ────────────────────────────
            //https://bsmg.wiki/mapping/map-format/beatmap.html#arcs
            }
            */

            // ───────── EVENTS & CUSTOM EVENTS ───────────────────────────
            // build basic events
            IEnumerable<JObject> basicEvents = data.beatmapEventDatas
                .OfType<CustomBasicBeatmapEventData>()
                .Select(e =>
                {
                    var o = new JObject
                    {
                        ["_time"] = e.time * timeMult,
                        ["_type"] = (int)e.basicBeatmapEventType,
                        ["_value"] = e.value,
                        // ["_floatValue"] = e.floatValue   // uncomment if you want it
                    };

                    if (e.customData != null && e.customData.Count > 0)
                        o["_customData"] = JObject.FromObject(e.customData);

                    return o;
                });

            // build rotation events (v2: 14=early, 15=late if you support both)
            IEnumerable<JObject> rotationEvents = rotationEventsData.Select(r =>
            {
                var o = new JObject
                {
                    ["_time"] = r.time * timeMult,
                    ["_type"] = 14, // or 14/15 depending on early/late if you track that
                    ["_value"] = Generator.SpawnRotationDegreesToValue(r.rotation),
                };

                if (r.customData != null && r.customData.Count > 0)
                    o["_customData"] = JObject.FromObject(r.customData);

                return o;
            });

            // combine + sort
            var allEvents = basicEvents
                .Concat(rotationEvents)
                .OrderBy(o => (float)o["_time"]);

            // finally, assign to root
            root["_events"] = new JArray(allEvents);


            if (data.customEventDatas.Count > 0)
            {
                var ce = new JArray();
                foreach (var c in data.customEventDatas)
                {
                    var o = new JObject
                    {
                        ["_time"] = c.time * timeMult,
                        ["_type"] = c.eventType
                    };
                    if (c.customData.Count > 0)
                        o["_customData"] = JObject.FromObject(c.customData);
                    ce.Add(o);
                }
                root["_customEvents"] = ce;
            }

            return root;
        }

        public static JObject JsonV3Output(CustomBeatmapData data, float timeMult, List<ERotationEventData>rotationEventsData) //v3 maps
        {
            // ---- v3.3.0 format ----
            var root = new JObject();
            root["version"] = new Version(3, 3, 0).ToString();

            // 1. colorNotes
            var notes = new JArray();
            foreach (var n in data.beatmapObjectDatas.OfType<CustomNoteData>()
                    .Where(n => (int)n.colorType == 0 || (int)n.colorType == 1))
            {
                var o = new JObject
                {
                    ["b"] = n.time * timeMult,
                    ["x"] = n.lineIndex,
                    ["y"] = (int)n.noteLineLayer,
                    ["c"] = (int)n.colorType,
                    ["d"] = (int)n.cutDirection
                };
                if (n.customData.Count > 0)
                    o["customData"] = JObject.FromObject(n.customData);
                notes.Add(o);
            }
            root["colorNotes"] = notes;

            // 2. BombNotes
            var bombs = new JArray();
            foreach (var b in data.beatmapObjectDatas.OfType<CustomNoteData>()
                    .Where(b => (int)b.colorType != 0 && (int)b.colorType != 1))
            {
                var o = new JObject
                {
                    ["b"] = b.time * timeMult,
                    ["x"] = b.lineIndex,
                    ["y"] = (int)b.noteLineLayer,
                };
                if (b.customData.Count > 0)
                    o["customData"] = JObject.FromObject(b.customData);
                bombs.Add(o);
            }
            root["bombNotes"] = bombs;

            // 3. Obstacles
            var obs = new JArray();
            foreach (var w in data.beatmapObjectDatas.OfType<CustomObstacleData>())
            {
                var o = new JObject
                {
                    ["b"] = w.time * timeMult,
                    ["x"] = w.lineIndex,
                    ["y"] = (int)w.lineLayer,
                    ["d"] = w.duration * timeMult,
                    ["w"] = w.width,
                    ["h"] = w.height
                };
                if (w.customData.Count > 0)
                    o["customData"] = JObject.FromObject(w.customData);
                obs.Add(o);
            }
            root["obstacles"] = obs;

            // 4. Arcs (sliders)
            var arcs = new JArray();
            foreach (var s in data.beatmapObjectDatas
                .OfType<CustomSliderData>()
                .Where(x => x.sliderType == SliderData.Type.Normal))
            {
                var o = new JObject
                {
                    ["c"] = (int)s.colorType,
                    ["b"] = s.time * timeMult,
                    ["x"] = s.headLineIndex,
                    ["y"] = (int)s.headLineLayer,
                    ["d"] = (int)s.headCutDirection,
                    ["mu"] = s.headControlPointLengthMultiplier,
                    ["tb"] = s.tailTime * timeMult,
                    ["tx"] = s.tailLineIndex,
                    ["ty"] = (int)s.tailLineLayer,
                    ["tc"] = (int)s.tailCutDirection,
                    ["tmu"] = s.tailControlPointLengthMultiplier,
                    ["m"] = (int)s.midAnchorMode
                };
                if (s.customData.Count > 0)
                    o["customData"] = JObject.FromObject(s.customData);
                arcs.Add(o);
            }
            root["sliders"] = arcs;

            // 5. Chains (burst sliders)
            var chains = new JArray();
            foreach (var s in data.beatmapObjectDatas
                .OfType<CustomSliderData>()
                .Where(x => x.sliderType == SliderData.Type.Burst))
            {
                var o = new JObject
                {
                    ["c"] = (int)s.colorType,
                    ["b"] = s.time * timeMult,
                    ["x"] = s.headLineIndex,
                    ["y"] = (int)s.headLineLayer,
                    ["d"] = (int)s.headCutDirection,
                    ["tb"] = s.tailTime * timeMult,
                    ["tx"] = s.tailLineIndex,
                    ["ty"] = (int)s.tailLineLayer,
                    ["sc"] = s.sliceCount,
                    ["s"] = s.squishAmount
                };
                if (s.customData.Count > 0)
                    o["customData"] = JObject.FromObject(s.customData);
                chains.Add(o);
            }
            root["burstSliders"] = chains;

            // 6. Basic Events (lighting, etc)
            var basicEvents = new JArray();
            foreach (var e in data.beatmapEventDatas.OfType<CustomBasicBeatmapEventData>())//.Where(e => (int)e.basicBeatmapEventType != 14 && (int)e.basicBeatmapEventType != 15))
            {
                var o = new JObject
                {
                    ["b"] = e.time * timeMult,
                    ["et"] = (int)e.basicBeatmapEventType,
                    ["i"] = e.value,
                    ["f"] = e.floatValue
                };
                if (e.customData.Count > 0)
                    o["customData"] = JObject.FromObject(e.customData);
                basicEvents.Add(o);
            }
            root["basicBeatmapEvents"] = basicEvents;

            // 7. Rotation Events 
            /*
            var rotationEvents = new JArray();
            foreach (var r in data.beatmapEventDatas
                .OfType<CustomBasicBeatmapEventData>()
                .Where(r => (int)r.basicBeatmapEventType == 14 || (int)r.basicBeatmapEventType == 15)) // this should work even though v3 since these legacy event types remain
            {
                var o = new JObject
                {
                    ["b"] = r.time * timeMult,
                    ["e"] = (int)r.basicBeatmapEventType == 15 ? 1 : 0,
                    ["r"] = Generator.SpawnRotationValueToDegrees(r.value)  // Magnitude (rotation in degrees)
                };
                if (r.customData.Count > 0)
                    o["customData"] = JObject.FromObject(r.customData);
                rotationEvents.Add(o);
                //Plugin.Log.Info($"Rotation Event added to JSON: {r.time:F} rotation: {Generator.SpawnRotationValueToDegreesV3(r.value)} ");
            }
            root["rotationEvents"] = rotationEvents;
            */
            var rotationsEvents = new JArray();
            foreach (var r in rotationEventsData)
            {
                var o = new JObject
                {
                    ["b"] = r.time * timeMult,
                    ["e"] = 0, //early type (14)
                    ["r"] = r.rotation//Generator.SpawnRotationValueToDegrees(r.rotation)  // Magnitude (rotation in degrees)
                };
                if (r.customData.Count > 0)
                    o["customData"] = JObject.FromObject(r.customData);
                rotationsEvents.Add(o);
                //Plugin.Log.Info($"Rotation Event added to JSON: {r.time:F} rotation: {Generator.SpawnRotationValueToDegreesV3(r.value)} ");
            }
            
            root["rotationEvents"] = rotationsEvents;
            
            // 8. Bpm Events 
            var bpmEvents = new JArray();
            foreach (var bp in data.beatmapEventDatas.OfType<CustomBPMChangeBeatmapEventData>())
            {
                var o = new JObject
                {
                    ["b"] = bp.time * timeMult,
                    ["m"] = bp.bpm
                };
                if (bp.customData.Count > 0)
                    o["customData"] = JObject.FromObject(bp.customData);
                bpmEvents.Add(o);
                //Plugin.Log.Info($"Rotation Event added to JSON: {r.time:F} rotation: {Generator.SpawnRotationValueToDegreesV3(r.value)} ");
            }
            root["bpmEvents"] = bpmEvents;

            // 9. Custom Events (if present)
            if (data.customEventDatas.Count > 0)
            {
                var customEvents = new JArray();
                foreach (var c in data.customEventDatas)
                {
                    var o = new JObject
                    {
                        ["b"] = c.time * timeMult,
                        ["t"] = c.eventType
                    };
                    if (c.customData.Count > 0)
                        o["customData"] = JObject.FromObject(c.customData);
                    customEvents.Add(o);
                }
                root["customEvents"] = customEvents;
            }

            return root;
        }
    }

    /// <summary>
    /// Produce a CustomBeatmapData from a JSON string. I don't think this works for all the possible data types for v3 maps.
    /// </summary>
    /// <param name="rawJson"></param>
    /// <param name="outputSeconds"></param>
    /// <returns></returns>
    /*
    public static CustomBeatmapData FromJson(string rawJson, bool outputSeconds = true)
    {
        // ───────── Common setup ─────────────────────────────────────
        float timeMult = 1;
        if (outputSeconds)
            timeMult = TransitionPatcher.bpm / 60f; // convert from bpm to bps

        var jObj = JObject.Parse(rawJson);

        string versionStr = jObj.Value<string>("_version")
                          ?? jObj.Value<string>("version")
                          ?? "2.6.0";
        var version = new Version(versionStr.TrimStart('v'));

        int numberOfLines = 4;

        var beatmapCustomRaw = jObj["customData"] as JObject
                             ?? jObj["_customData"] as JObject;
        var beatmapCustomData = beatmapCustomRaw != null
            ? new CustomData(beatmapCustomRaw.ToObject<Dictionary<string, object>>())
            : new CustomData();

        var levelCustomData = new CustomData();// CustomBeatmapDataRegistry.LevelCustomData;

        var customBeatmapData = new CustomBeatmapData(
            numberOfLines,
            beatmapCustomData,
            levelCustomData,
            new CustomData(),
            version
        );

        // Helper to pull per-item customData
        CustomData ExtractCustomData(JToken token) =>
            (token["customData"] as JObject
             ?? token["_customData"] as JObject) is JObject cdRaw
                ? new CustomData(cdRaw.ToObject<Dictionary<string, object>>())
                : new CustomData();

        // ───────── NOTES ───────────────────────────────────────────
        var notesArray = (jObj["notes"] as JArray)
                       ?? (jObj["_notes"] as JArray);
        if (notesArray != null)
        {
            foreach (var note in notesArray)
            {
                var cd = ExtractCustomData(note);
                int t = note.Value<int>("_type");
                CustomNoteData nd;
                if (t == 3) // bomb
                {
                    nd = CustomNoteData.CreateCustomBombNoteData(
                        note.Value<float>("_time") / timeMult,
                        note.Value<float>("_time"),
                        (int)(note.Value<float?>("_rotation") ?? 0),
                        note.Value<int>("_lineIndex"),
                        (NoteLineLayer)(note.Value<int?>("_lineLayer") ?? 0),
                        cd,
                        version
                    );
                }
                else
                {
                    nd = CustomNoteData.CreateCustomBasicNoteData(
                        note.Value<float>("_time") / timeMult,
                        note.Value<float>("_time"),
                        (int)(note.Value<float?>("_rotation") ?? 0),
                        note.Value<int>("_lineIndex"),
                        (NoteLineLayer)(note.Value<int?>("_lineLayer") ?? 0),
                        (ColorType)t,
                        (NoteCutDirection)(note.Value<int?>("_cutDirection") ?? 0),
                        cd,
                        version
                    );
                }
                customBeatmapData.AddBeatmapObjectDataInOrder(nd);
            }
        }

        // ───────── OBSTACLES ───────────────────────────────────────
        var obsArray = (jObj["obstacles"] as JArray)
                    ?? (jObj["_obstacles"] as JArray);
        if (obsArray != null)
        {
            foreach (var o in obsArray)
            {
                var cd = ExtractCustomData(o);
                int type = o.Value<int?>("_type") ?? 0;

                // Call legacy method to get layer and height
                var (lineLayer, height) = GetLegacyWallLineLayerAndHeight(type);

                var od = new CustomObstacleData(
                    o.Value<float>("_time") / timeMult,
                    o.Value<float>("_time"),
                    o.Value<float>("_time") / timeMult + o.Value<float>("_duration") / timeMult,
                    0,
                    o.Value<int>("_lineIndex"),
                    lineLayer,                  // from legacy method
                    o.Value<float>("_duration") / timeMult,
                    o.Value<int>("_width"),
                    height,                     // from legacy method
                    cd,
                    version
                );
                customBeatmapData.AddBeatmapObjectDataInOrder(od);
            }
            Plugin.Log.Info($"Obstacle Count from actual JSON: {obsArray.Count}");
        }
        (NoteLineLayer, int) GetLegacyWallLineLayerAndHeight(int type)
        {
            if (type == 0)
            {
                return (NoteLineLayer.Base, 5); // "Full-Height" Walls
            }
            else if (type == 1)
            {
                return (NoteLineLayer.Upper, 3); // "Crouch" Walls
            }
            else
            {
                return (NoteLineLayer.Base, 5); // "Free" Walls (v3-like) ?? https://bsmg.wiki/mapping/map-format/beatmap.html#obstacles-type
            }
        }

        // ───────── SLIDERS (ARCS) ───────────────────────────────────
        int arcCount = 0;
        var slidersArray = (jObj["sliders"] as JArray)
                         ?? (jObj["_sliders"] as JArray); //sliders is v3
        if (slidersArray != null)
        {
            foreach (var s in slidersArray)
            {
                var cd = ExtractCustomData(s);

                var slider = CustomSliderData.CreateCustomSliderData(
                    (ColorType)(s.Value<int?>("_colorType") ?? 0),
                    s.Value<float>("_headTime") / timeMult,
                    s.Value<float>("_headTime"),
                    0,
                    s.Value<int>("_headLineIndex"),
                    (NoteLineLayer)(s.Value<int?>("_headLineLayer") ?? 0),
                    (NoteLineLayer)(s.Value<int?>("_headLineLayer") ?? 0),
                    s.Value<float?>("_headControlPointLengthMultiplier") ?? 1f,
                    (NoteCutDirection)(s.Value<int?>("_headCutDirection") ?? 0),
                    s.Value<float>("_tailTime") / timeMult,
                    0,
                    s.Value<int>("_tailLineIndex"),
                    (NoteLineLayer)(s.Value<int?>("_tailLineLayer") ?? 0),
                    (NoteLineLayer)(s.Value<int?>("_tailLineLayer") ?? 0),
                    s.Value<float?>("_tailControlPointLengthMultiplier") ?? 1f,
                    (NoteCutDirection)(s.Value<int?>("_tailCutDirection") ?? 0),
                    (SliderMidAnchorMode)(s.Value<int?>("_sliderMidAnchorMode") ?? 0),
                    cd,
                    version
                );
                arcCount++;
                customBeatmapData.AddBeatmapObjectDataInOrder(slider);
            }
        }
        //Plugin.Log.Info($"Arc Count from actual JSON: {arcCount}");
        /*
        // ───────── BURST SLIDERS (CHAINS) ────────────────────────────
        var burstArray = (jObj["burstSliders"] as JArray)
                      ?? (jObj["_burstSliders"] as JArray);
        if (burstArray != null)
        {
            foreach (var b in burstArray)
            {
                var cd = ExtractCustomData(b);
                var headTime = b.Value<float>("_headTime");
                var headBeat = b.Value<float?>("_headBeat")
                             ?? headTime;
                var tailTime = b.Value<float>("_tailTime");

                var burst = CustomSliderData.CreateCustomBurstSliderData(
                    (ColorType)(b.Value<int?>("_colorType") ?? 0),
                    headTime,
                    headBeat,
                    b.Value<int?>("_rotation") ?? 0,
                    b.Value<int>("_headLineIndex"),
                    (NoteLineLayer)(b.Value<int?>("_headLineLayer") ?? 0),
                    (NoteLineLayer)(b.Value<int?>("_headBeforeJumpLineLayer") ?? 0),
                    (NoteCutDirection)(b.Value<int?>("_headCutDirection") ?? 0),
                    tailTime,
                    b.Value<int?>("_tailRotation") ?? 0,
                    b.Value<int>("_tailLineIndex"),
                    (NoteLineLayer)(b.Value<int?>("_tailLineLayer") ?? 0),
                    (NoteLineLayer)(b.Value<int?>("_tailBeforeJumpLineLayer") ?? 0),
                    b.Value<int>("_sliceCount"),
                    b.Value<float>("_squishAmount"),
                    cd,
                    version
                );
                customBeatmapData.AddBeatmapObjectDataInOrder(burst);
            }
        }
        */

    /*
        // ───────── EVENTS & CUSTOM EVENTS ───────────────────────────
        var eventsArray = (jObj["events"] as JArray)
                       ?? (jObj["_events"] as JArray);
        if (eventsArray != null)
        {
            foreach (var e in eventsArray)
            {
                var cd = ExtractCustomData(e);
                var ed = new CustomBasicBeatmapEventData(
                    e.Value<float>("_time") / timeMult,
                    (BasicBeatmapEventType)e.Value<int>("_type"),
                    e.Value<int>("_value"),
                    1,
                    cd,
                    version
                );
                customBeatmapData.InsertBeatmapEventDataInOrder(ed);
            }
        }

        var customEvents = (jObj["customEvents"] as JArray)
                        ?? (jObj["_customEvents"] as JArray);
        if (customEvents != null)
        {
            foreach (var ce in customEvents)
            {
                var cd = ExtractCustomData(ce);
                var c = new CustomEventData(
                    ce.Value<float>("_time") / timeMult,
                    ce.Value<string>("_type"),
                    cd,
                    version
                );
                customBeatmapData.InsertCustomEventDataInOrder(c);
            }
        }

        Plugin.Log.Info($"Obstacle Count from newly created data: {customBeatmapData.allBeatmapDataItems.OfType<CustomObstacleData>().Count()}");

        return customBeatmapData;
    }
    */


}
