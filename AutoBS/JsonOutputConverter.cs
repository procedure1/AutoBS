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
    public static class JsonOutputConverter
    {
        // Set this if update mod!!! this is for customData metadata
        public static string ModName = "AutoBS v1.0.0";

        /// <summary>
        /// Produce a JSON string from CustomBeatmapData. Works for v2 and v3 maps.
        /// </summary>
        /// <param name="data"></param>
        /// <param name="preferredVersion"></param>
        /// <param name="outputSecondsToBeats"></param>
        /// <returns></returns>
        public static bool ToJsonFile(CustomBeatmapData data, EditableCBD eData, bool outputSecondsToBeats = true) // if converted to seconds for FromJsonString then need this
        {
            // ───────── Common setup ─────────────────────────────────────
            float timeMult = 1;
            if (outputSecondsToBeats)
                timeMult = TransitionPatcher.bpm / 60f; // convert from bpm to bps

            int majorVersion = data.version.Major;

            JObject root = new JObject();

            var rotationEvents = eData.RotationEvents;

            // Output V2 (no arcs/chains) if requested
            if (Config.Instance.OutputV2JsonToSongFolder_NoArcsNoChainsNoMapExtWalls)
            {
                Plugin.Log.Info($"[ToJsonStringFile] Outputting V2 JSON...");
                root = JsonV2Output(eData, timeMult);//eData, timeMult, rotationEvents);
                createFile(root, 2);
            }

            // Output V3 if requested
            if (Config.Instance.OutputV3JsonToSongFolder)
            {
                Plugin.Log.Info($"[ToJsonStringFile] Outputting V3 JSON...");
                root = JsonV3Output(data, timeMult, eData);
                //root = JsonV3Output(eData, timeMult);
                createFile(root, 3);
            }
            if (Config.Instance.OutputV4JsonToSongFolder)
            {
                Plugin.Log.Info($"[ToJsonStringFile] Outputting V4 JSON...");
                root = JsonV4BeatmapOutput(eData, timeMult);
                createFile(root, 4, "Beatmap");
                root = JsonV4LightshowOutput(eData, timeMult);
                createFile(root, 4, "Lightshow");

                root = JsonV4AudioDataOutput(TransitionPatcher.SelectedBeatmapLevel, Config.Instance.OutputV4JsonSongSampleRate); //default is 44100!
                createFile(root, 4, "AudioData");
            }

            // Reset only if "turn off after one play" is enabled
            if (Config.Instance.TurnOffJSONDatOutputAfterOneMapPlay)
            {
                Config.Instance.OutputV2JsonToSongFolder_NoArcsNoChainsNoMapExtWalls = false;
                Config.Instance.OutputV3JsonToSongFolder = false;
                Config.Instance.OutputV4JsonToSongFolder = false;
                //Config.Instance.Changed(); // don't need it since Config.Instance = conf.Generated<Config>(); makes every change a re-write anyway
            }

            void createFile(JObject root, int version, string outputType = "Beatmap")
            {
                string jsonString = JsonConvert.SerializeObject(root, Formatting.None);

                string fileName = $"{TransitionPatcher.SelectedCharacteristicSO.serializedName}{TransitionPatcher.SelectedDifficulty}.{outputType}_v{version}_AutoBS_Generator.dat";

                string path;

                if (!string.IsNullOrEmpty(SetContent.SongFolderPath))
                {
                    path = Path.Combine(SetContent.SongFolderPath, fileName);
                }
                else
                {
                    path = Path.Combine(@"D:\", fileName);
                }

                Plugin.Log.Info($"[ToJsonStringFile] Original Map v{data.version}. Outputing JSON file to: {path}");

                File.WriteAllText(path, jsonString);
            }

            return true;
        }

        static float R4(float v) => (float)Math.Round(v, 4); //round to 4 digits

        public static JObject JsonV2Output(EditableCBD eData, float timeMult) // v2 maps
        {
            if (eData == null) throw new ArgumentNullException(nameof(eData));

            // -------- VERSION (ensure at least 2.5.0 so _floatValue is valid) --------
            var baseVersion = new Version(2, 5, 0);

            var root = new JObject
            {
                ["_version"] = baseVersion.ToString() // v2.5.0+ (no v2 sliders enabled)
            };

            // -------- BEATMAP CUSTOM DATA --------
            if (eData.BeatmapCustomData != null && eData.BeatmapCustomData.Count > 0)
                root["_customData"] = JObject.FromObject(eData.BeatmapCustomData);

            // ─────────────────────────────────────────────────────────────
            // 1) NOTES (color notes + bombs) from eData
            //    Build them separately, then join + sort by time.
            // ─────────────────────────────────────────────────────────────

            // Color notes (left/right sabers)
            IEnumerable<JObject> colorNoteObjects = Enumerable.Empty<JObject>();
            if (eData.ColorNotes != null)
            {
                colorNoteObjects = eData.ColorNotes.Select(n =>
                {
                    var o = new JObject
                    {
                        ["_time"] = R4(n.time * timeMult),
                        ["_lineIndex"] = n.line,
                        ["_lineLayer"] = n.layer,
                        ["_type"] = (int)n.colorType,   // 0 = left, 1 = right
                        ["_cutDirection"] = (int)n.cutDirection
                    };

                    if (n.customData != null && n.customData.Count > 0)
                        o["_customData"] = JObject.FromObject(n.customData);

                    return o;
                });
            }

            // Bomb notes → v2 bombs are _type = 3
            IEnumerable<JObject> bombNoteObjects = Enumerable.Empty<JObject>();
            if (eData.BombNotes != null)
            {
                bombNoteObjects = eData.BombNotes.Select(b =>
                {
                    var o = new JObject
                    {
                        ["_time"] = R4(b.time * timeMult),
                        ["_lineIndex"] = b.line,
                        ["_lineLayer"] = b.layer,
                        ["_type"] = 3,   // bomb
                        ["_cutDirection"] = 0   // ignored for bombs
                    };

                    if (b.customData != null && b.customData.Count > 0)
                        o["_customData"] = JObject.FromObject(b.customData);

                    return o;
                });
            }

            // Join color notes + bombs and sort by time (like _events)
            var allNotes = colorNoteObjects
                .Concat(bombNoteObjects)
                .OrderBy(o => (float)o["_time"]);

            root["_notes"] = new JArray(allNotes);

            // ─────────────────────────────────────────────────────────────
            // 2) OBSTACLES from eData (with ME-filter & legacy type mapping)
            // ─────────────────────────────────────────────────────────────
            var obs = new JArray();

            if (eData.Obstacles != null)
            {
                foreach (var w in eData.Obstacles
                         .OrderBy(w => w.time)
                         .ThenBy(w => w.line)
                         .ThenBy(w => w.layer))
                {
                    if (!RemoveMappingExtensionWalls(w, out int legacyType))
                        continue; // skip Mapping Extensions / unsupported walls

                    var o = new JObject
                    {
                        ["_time"] = R4(w.time * timeMult),
                        ["_lineIndex"] = w.line,          // safe (we filtered ≥1000)
                        ["_type"] = legacyType,      // 0 = full, 1 = crouch
                        ["_duration"] = R4(Math.Max(w.duration * timeMult,.001f)),
                        ["_width"] = w.width          // safe (we filtered ≥1000)
                    };

                    if (w.customData != null && w.customData.Count > 0)
                        o["_customData"] = JObject.FromObject(w.customData);

                    obs.Add(o);
                }
            }

            root["_obstacles"] = obs;

            // v2 wall filter + legacy type mapping, now using EObstacleData
            bool RemoveMappingExtensionWalls(EObstacleData obstacle, out int legacyType)
            {
                legacyType = 0;

                // 1) Reject Mapping Extensions–style "precise" walls

                // ME precise lanes / out-of-bounds
                if (Math.Abs(obstacle.line) >= 1000)
                    return false;

                // ME precise widths
                if (obstacle.width >= 1000)
                    return false;

                // ME-coded heights
                if (obstacle.height >= 1000)
                    return false;

                // If you ALSO want to reject extension walls (far left/right lanes),
                // uncomment this:
                // if (obstacle.line < 0 || obstacle.line > 3)
                //     return false;

                // 2) Only accept vanilla-like shapes that map to v2:
                //    layer 0 + height 5  => full-height
                //    layer 2 + height 3  => crouch

                int layer = obstacle.layer;
                int height = obstacle.height;

                if (layer == 0 && height == 5)
                {
                    legacyType = 0; // full-height v2 wall
                    return true;
                }

                if (layer == 2 && height == 3)
                {
                    legacyType = 1; // crouch v2 wall
                    return true;
                }

                // Everything else not safely representable → drop it
                return false;
            }

            // (Sliders / arcs are still disabled for v2; see your comment.)
            // If you ever want to re-enable v2 sliders, you’d base them on eData.Arcs
            // here and keep the map version at 2.6.0+.

            // ─────────────────────────────────────────────────────────────
            // 3) EVENTS (_events) — basic + color boost + rotation
            // ─────────────────────────────────────────────────────────────

            // Basic events (no type 5, no rotation — those are split out into eData lists)
            IEnumerable<JObject> basicEvents = Enumerable.Empty<JObject>();
            if (eData.BasicEvents != null)
            {
                basicEvents = eData.BasicEvents.Select(e =>
                {
                    var o = new JObject
                    {
                        ["_time"] = R4(e.time * timeMult),
                        ["_type"] = (int)e.basicBeatmapEventType,
                        ["_value"] = e.value,
                        ["_floatValue"] = e.floatValue
                    };

                    if (e.customData != null && e.customData.Count > 0)
                        o["_customData"] = JObject.FromObject(e.customData);

                    return o;
                });
            }

            // Color boost events (v2 type 5)
            IEnumerable<JObject> colorBoostEventObjects = Enumerable.Empty<JObject>();
            if (eData.ColorBoostEvents != null)
            {
                colorBoostEventObjects = eData.ColorBoostEvents.Select(cb =>
                {
                    var o = new JObject
                    {
                        ["_time"] = R4(cb.time * timeMult),
                        ["_type"] = 5,                        // Color boost
                        ["_value"] = cb.boostColorsAreOn ? 1 : 0,
                        //["_floatValue"] = 1f
                    };

                    if (cb.customData != null && cb.customData.Count > 0)
                        o["_customData"] = JObject.FromObject(cb.customData);

                    return o;
                });
            }

            // Rotation events (v2: 14 = early; if you later track early/late, split 14/15)
            IEnumerable<JObject> rotationEvents = Enumerable.Empty<JObject>();
            if (eData.RotationEvents != null)
            {
                rotationEvents = eData.RotationEvents.Select(r =>
                {
                    var o = new JObject
                    {
                        ["_time"] = R4(r.time * timeMult),
                        ["_type"] = 14, // you can later branch 14/15 if you add "late" info
                        ["_value"] = Generator.SpawnRotationDegreesToValue(r.rotation),
                        //["_floatValue"] = 1f
                    };

                    if (r.customData != null && r.customData.Count > 0)
                        o["_customData"] = JObject.FromObject(r.customData);

                    return o;
                });
            }

            // Combine all event types & sort by time
            var allEvents = basicEvents
                .Concat(colorBoostEventObjects)
                .Concat(rotationEvents)
                .OrderBy(o => (float)o["_time"]);

            root["_events"] = new JArray(allEvents);

            // ─────────────────────────────────────────────────────────────
            // 4) CUSTOM EVENTS (from eData)
            // ─────────────────────────────────────────────────────────────
            if (eData.CustomEvents != null && eData.CustomEvents.Count > 0)
            {
                var ce = new JArray();
                foreach (var c in eData.CustomEvents)
                {
                    var o = new JObject
                    {
                        ["_time"] = R4(c.time * timeMult),
                        ["_type"] = c.eventType
                    };

                    if (c.customData != null && c.customData.Count > 0)
                        o["_customData"] = JObject.FromObject(c.customData);

                    ce.Add(o);
                }

                root["_customEvents"] = ce;
            }

            AddAutoBSMetadataV2(root); // Add AutoBS metadata without overwriting existing _customData keys

            return root;
        }

       
        //customData entries still have "_" in front of items like _position if copied from v2. but that should be ok apparently.
        public static JObject JsonV3Output(CustomBeatmapData data, float timeMult, EditableCBD eData) //v3 maps - rotations are stripped out of events. Not using eData since arcs/chains don't link well to rotation events. they only work with per object rotations
        {
            var rotationEventsData = eData.RotationEvents;
            var colorBoostEventsData = eData.ColorBoostEvents;

            // ---- v3.3.0 format ----
            var root = new JObject();
            root["version"] = new Version(3, 3, 0).ToString();

            // Root-level customData (beatmap-level) from EditableCBD, if present
            if (eData.BeatmapCustomData != null && eData.BeatmapCustomData.Count > 0)
                root["customData"] = JObject.FromObject(eData.BeatmapCustomData);


            // 1. colorNotes
            var notes = new JArray();
            foreach (var n in data.beatmapObjectDatas.OfType<CustomNoteData>()
                    .Where(n => (int)n.colorType == 0 || (int)n.colorType == 1))
            {
                var o = new JObject
                {
                    ["b"] = R4(n.time * timeMult),
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
                    ["b"] = R4(b.time * timeMult),
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
                    ["b"] = R4(w.time * timeMult),
                    ["x"] = w.lineIndex,
                    ["y"] = (int)w.lineLayer,
                    ["d"] = R4(Math.Max(w.duration * timeMult, 0.001f)), // for window pane walls. thinner than this causes a gray wall glitch
                    ["w"] = w.width,
                    ["h"] = w.height
                };
                if (w.customData.Count > 0)
                    o["customData"] = JObject.FromObject(w.customData);
                obs.Add(o);
            }
            root["obstacles"] = obs;

            var boosts = new JArray();
            foreach (var bo in colorBoostEventsData)//data.beatmapObjectDatas.OfType<CustomColorBoostBeatmapEventData>()) // color boost stripped out from cbd not sure why. must use eData
            {
                var o = new JObject
                {
                    ["b"] = R4(bo.time * timeMult),
                    ["o"] = bo.boostColorsAreOn, // use bool here for v3!!!! not int!!!!!
                };
                if (bo.customData.Count > 0)
                    o["customData"] = JObject.FromObject(bo.customData);
                boosts.Add(o);
            }
            root["colorBoostBeatmapEvents"] = boosts;

            // 4. Arcs (sliders)
            var arcs = new JArray();
            foreach (var s in data.beatmapObjectDatas
                .OfType<CustomSliderData>()
                .Where(x => x.sliderType == SliderData.Type.Normal))
            {
                var o = new JObject
                {
                    ["c"] = (int)s.colorType,
                    ["b"] = R4(s.time * timeMult),
                    ["x"] = s.headLineIndex,
                    ["y"] = (int)s.headLineLayer,
                    ["d"] = (int)s.headCutDirection,
                    ["mu"] = s.headControlPointLengthMultiplier,
                    ["tb"] = R4(s.tailTime * timeMult),
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
                    ["b"] = R4(s.time * timeMult),
                    ["x"] = s.headLineIndex,
                    ["y"] = (int)s.headLineLayer,
                    ["d"] = (int)s.headCutDirection,
                    ["tb"] = R4(s.tailTime * timeMult),
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
                    ["b"] = R4(e.time * timeMult),
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
                    ["b"] = R4(r.time * timeMult),
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
                    ["b"] = R4(bp.time * timeMult),
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
                        ["b"] = R4(c.time * timeMult),
                        ["t"] = c.eventType
                    };
                    if (c.customData.Count > 0)
                        o["customData"] = JObject.FromObject(c.customData);
                    customEvents.Add(o);
                }
                root["customEvents"] = customEvents;
            }

            AddAutoBSMetadataV3OrV4(root); // Add AutoBS metadata without overwriting existing customData keys

            return root;
        }


        /// <summary>
        /// Creates a v4.1.0 beatmap JObject from EditableCBD.
        /// - Dedupes all metadata arrays (colorNotesData, bombNotesData,
        ///   obstaclesData, arcsData, chainsData).
        /// - Uses eData for notes, bombs, obstacles, arcs, chains.
        /// - Uses OriginalCBData for BPM events.
        /// </summary>
        public static JObject JsonV4BeatmapOutput(EditableCBD eData, float timeMult)
        {
            if (eData == null) throw new ArgumentNullException(nameof(eData));

            JObject root = new JObject
            {
                ["version"] = "4.0.0" //4.1.0 has njs events but customJSONData doesn't work for v4
            };

            // Root-level customData (beatmap-level) from EditableCBD, if present
            if (eData.BeatmapCustomData != null && eData.BeatmapCustomData.Count > 0)
                root["customData"] = JObject.FromObject(eData.BeatmapCustomData);


            // ============================================================
            // 1) COLOR NOTES (with deduped colorNotesData)
            //
            // colorNotes    : instances (time, rotation, metadata index)
            // colorNotesData: metadata deduped by (x, y, color, cutDir, angleOffset)
            // ============================================================
            var colorNotes = new JArray();
            var colorNotesData = new JArray();

            // key -> metadata index
            var colorMetaMap = new Dictionary<(int x, int y, int c, int d, float a), int>();

            int GetColorMetaIndex(ColorType color, int x, int y, NoteCutDirection cutDir, float angleOffset)
            {
                var key = (x, y, (int)color, (int)cutDir, angleOffset);

                if (!colorMetaMap.TryGetValue(key, out int idx))
                {
                    idx = colorMetaMap.Count;
                    colorMetaMap[key] = idx;

                    colorNotesData.Add(new JObject
                    {
                        ["x"] = x,
                        ["y"] = y,
                        ["c"] = (int)color,
                        ["d"] = (int)cutDir,
                        ["a"] = angleOffset
                    });
                }

                return idx;
            }

            if (eData.ColorNotes != null)
            {
                foreach (var n in eData.ColorNotes
                         .OrderBy(n => n.time)
                         .ThenBy(n => n.line)
                         .ThenBy(n => n.layer))
                {
                    // If you ever store angle offset in ENoteData, plug it in here.
                    float angleOffset = 0f;

                    int metaIndex = GetColorMetaIndex(
                        n.colorType,
                        n.line,
                        n.layer,
                        n.cutDirection,
                        angleOffset
                    );

                    colorNotes.Add(new JObject
                    {
                        ["b"] = R4(n.time * timeMult),
                        ["r"] = n.rotation,
                        ["i"] = metaIndex
                    });
                }
            }

            root["colorNotes"] = colorNotes;
            root["colorNotesData"] = colorNotesData;

            // ============================================================
            // 2) BOMB NOTES (with deduped bombNotesData)
            //
            // bombNotes    : instances (time, rotation, metadata index)
            // bombNotesData: metadata deduped by (x, y)
            // ============================================================
            var bombNotes = new JArray();
            var bombNotesData = new JArray();

            var bombMetaMap = new Dictionary<(int x, int y), int>();

            int GetBombMetaIndex(int x, int y)
            {
                var key = (x, y);

                if (!bombMetaMap.TryGetValue(key, out int idx))
                {
                    idx = bombMetaMap.Count;
                    bombMetaMap[key] = idx;

                    bombNotesData.Add(new JObject
                    {
                        ["x"] = x,
                        ["y"] = y
                    });
                }

                return idx;
            }

            if (eData.BombNotes != null)
            {
                foreach (var b in eData.BombNotes
                         .OrderBy(b => b.time)
                         .ThenBy(b => b.line)
                         .ThenBy(b => b.layer))
                {
                    int metaIndex = GetBombMetaIndex(b.line, b.layer);

                    bombNotes.Add(new JObject
                    {
                        ["b"] = R4(b.time * timeMult),
                        ["r"] = b.rotation,
                        ["i"] = metaIndex
                    });
                }
            }

            root["bombNotes"] = bombNotes;
            root["bombNotesData"] = bombNotesData;

            // ============================================================
            // 3) OBSTACLES (with deduped obstaclesData)
            //
            // obstacles    : instances (time, rotation, metadata index)
            // obstaclesData: metadata deduped by (duration, x, y, w, h)
            // ============================================================
            var obstacles = new JArray();
            var obstaclesData = new JArray();

            var obstacleMetaMap = new Dictionary<(float d, int x, int y, int w, int h), int>();

            int GetObstacleMetaIndex(float duration, int x, int y, int w, int h)
            {
                // If float precision ever bothers you, you can round duration here.
                var key = (duration, x, y, w, h);

                if (!obstacleMetaMap.TryGetValue(key, out int idx))
                {
                    idx = obstacleMetaMap.Count;
                    obstacleMetaMap[key] = idx;

                    obstaclesData.Add(new JObject
                    {
                        ["d"] = duration, // already clamped and rounded to 4 digits seems to be correct
                        ["x"] = x,
                        ["y"] = y,
                        ["w"] = w,
                        ["h"] = h
                    });
                }

                return idx;
            }

            if (eData.Obstacles != null)
            {
                foreach (var o in eData.Obstacles
                         .OrderBy(o => o.time)
                         .ThenBy(o => o.line)
                         .ThenBy(o => o.layer))
                {
                    float durationBeats = R4(Math.Max(o.duration * timeMult, 0.001f)); // for window pane walls. thinner than this causes a gray wall glitch

                    int metaIndex = GetObstacleMetaIndex(
                        durationBeats,
                        o.line,
                        o.layer,
                        o.width,
                        o.height
                    );

                    obstacles.Add(new JObject
                    {
                        ["b"] = R4(o.time * timeMult),
                        ["r"] = o.rotation,
                        ["i"] = metaIndex
                    });
                }
            }

            root["obstacles"] = obstacles;
            root["obstaclesData"] = obstaclesData;

            // ============================================================
            // 4) ARCS (NORMAL SLIDERS) + deduped arcsData
            //
            // arcs    : one per arc, with hb/tb/hr/tr/hi/ti/ai
            // arcsData: deduped by (headCP, tailCP, midAnchorMode)
            //
            // hi/ti are metadata indices into colorNotesData, reusing the
            // same meta table as notes by calling GetColorMetaIndex().
            // ============================================================
            var arcs = new JArray();
            var arcsData = new JArray();

            var arcMetaMap = new Dictionary<(float m, float tm, SliderMidAnchorMode mode), int>();

            int GetArcMetaIndex(float headCP, float tailCP, SliderMidAnchorMode mode)
            {
                var key = (headCP, tailCP, mode);

                if (!arcMetaMap.TryGetValue(key, out int idx))
                {
                    idx = arcMetaMap.Count;
                    arcMetaMap[key] = idx;

                    arcsData.Add(new JObject
                    {
                        ["m"] = headCP,
                        ["tm"] = tailCP,
                        ["a"] = (int)mode
                    });
                }

                return idx;
            }

            if (eData.Arcs != null)
            {
                foreach (var s in eData.Arcs
                         .OrderBy(a => a.time)
                         .ThenBy(a => a.line)
                         .ThenBy(a => a.layer))
                {
                    if (s.sliderType != ESliderType.Arc)
                        continue;

                    // Use the slider's own lane/layer/color/cutDir for head/tail.
                    // This guarantees that arcs refer to the same metadata shape
                    // as the notes that share those positions.
                    float headAngleOffset = 0f;
                    float tailAngleOffset = 0f;

                    int hi = GetColorMetaIndex(
                        s.colorType,
                        s.line,
                        s.layer,
                        s.cutDirection,
                        headAngleOffset
                    );

                    int ti = GetColorMetaIndex(
                        s.colorType,
                        s.tailLine,
                        s.tailLayer,
                        s.tailCutDirection,
                        tailAngleOffset
                    );

                    int ai = GetArcMetaIndex(
                        s.headControlPointLengthMultiplier,
                        s.tailControlPointLengthMultiplier,
                        s.sliderMidAnchorMode
                    );

                    arcs.Add(new JObject
                    {
                        ["hb"] = R4(s.time * timeMult),
                        ["tb"] = R4(s.tailTime * timeMult),
                        ["hr"] = s.rotation,
                        ["tr"] = s.tailRotation,
                        ["hi"] = hi,
                        ["ti"] = ti,
                        ["ai"] = ai
                    });
                }
            }

            root["arcs"] = arcs;
            root["arcsData"] = arcsData;

            // ============================================================
            // 5) CHAINS (BURST SLIDERS) + deduped chainsData
            //
            // chains    : one per chain, with hb/tb/hr/tr/i/ci
            // chainsData: deduped by (tx, ty, sliceCount, squish)
            //
            // "i" is the metadata index in colorNotesData for the head.
            // ============================================================
            var chains = new JArray();
            var chainsData = new JArray();

            var chainMetaMap = new Dictionary<(int tx, int ty, int count, float squish), int>();

            int GetChainMetaIndex(int tx, int ty, int count, float squish)
            {
                var key = (tx, ty, count, squish);

                if (!chainMetaMap.TryGetValue(key, out int idx))
                {
                    idx = chainMetaMap.Count;
                    chainMetaMap[key] = idx;

                    chainsData.Add(new JObject
                    {
                        ["tx"] = tx,
                        ["ty"] = ty,
                        ["c"] = count,
                        ["s"] = squish
                    });
                }

                return idx;
            }

            if (eData.Chains != null)
            {
                foreach (var s in eData.Chains
                         .OrderBy(c => c.time)
                         .ThenBy(c => c.line)
                         .ThenBy(c => c.layer))
                {
                    if (s.sliderType != ESliderType.Chain)
                        continue;

                    float headAngleOffset = 0f;

                    int headMetaIndex = GetColorMetaIndex(
                        s.colorType,
                        s.line,
                        s.layer,
                        s.cutDirection,
                        headAngleOffset
                    );

                    int ci = GetChainMetaIndex(
                        s.tailLine,
                        s.tailLayer,
                        s.sliceCount,
                        s.squishAmount
                    );

                    chains.Add(new JObject
                    {
                        ["hb"] = R4(s.time * timeMult),
                        ["tb"] = R4(s.tailTime * timeMult),
                        ["hr"] = s.rotation,
                        ["tr"] = s.tailRotation,
                        ["i"] = headMetaIndex,
                        ["ci"] = ci
                    });
                }
            }

            root["chains"] = chains;
            root["chainsData"] = chainsData;

            // ============================================================
            // 6) BPM EVENTS (from OriginalCBData)
            //
            // Keep what you already had: export any BPM change events
            // from the original CustomBeatmapData.
            // ============================================================
            var bpmEvents = new JArray();

            if (eData.OriginalCBData != null &&
                eData.OriginalCBData.beatmapEventDatas != null)
            {
                foreach (var bp in eData.OriginalCBData.beatmapEventDatas
                             .OfType<CustomBPMChangeBeatmapEventData>()
                             .OrderBy(ev => ev.time))
                {
                    bpmEvents.Add(new JObject
                    {
                        ["b"] = R4(bp.time * timeMult),
                        ["m"] = bp.bpm
                    });
                }
            }

            root["bpmEvents"] = bpmEvents;

            // ============================================================
            // 7) NJS EVENTS (not used yet – leave empty but present)
            // ============================================================
            root["njsEvents"] = new JArray();
            root["njsEventData"] = new JArray();

            AddAutoBSMetadataV3OrV4(root); // Add/merge AutoBS generator metadata at root (customData)

            return root;
        }



        /// <summary>
        /// Creates a v4 lightshow JObject from EditableCBD (preferred path).
        /// Uses:
        ///   - eData.BasicEvents      → basicEvents/basicEventsData
        ///   - eData.ColorBoostEvents → colorBoostEvents/colorBoostEventsData
        ///   - eData.OriginalCBData   → waypoints (since eData doesn't track them)
        /// </summary>
        public static JObject JsonV4LightshowOutput(EditableCBD eData, float timeMult)
        {
            if (eData == null) throw new ArgumentNullException(nameof(eData));

            JObject root = new JObject
            {
                ["version"] = "4.0.0"
            };

            // ============================================================
            // 1) BASIC EVENTS (legacy lights, rings, lasers, etc.)
            //
            // basicEvents     : [{ "b": time, "i": metaIndex }]
            // basicEventsData : deduped by (t, value, floatValue)
            //
            // We skip:
            //   - legacy rotation events (14, 15)
            //   - color boost (5) – exported separately below
            // ============================================================
            var basicEvents = new JArray();
            var basicEventsData = new JArray();

            // key -> metadata index
            var basicMetaMap = new Dictionary<(int t, int v, float f), int>();

            int GetBasicMetaIndex(int t, int v, float f)
            {
                var key = (t, v, f);

                if (!basicMetaMap.TryGetValue(key, out int idx))
                {
                    idx = basicMetaMap.Count;
                    basicMetaMap[key] = idx;

                    basicEventsData.Add(new JObject
                    {
                        ["t"] = t,
                        ["i"] = v,
                        ["f"] = f
                    });
                }

                return idx;
            }

            if (eData.BasicEvents != null)
            {
                foreach (var e in eData.BasicEvents
                         .OrderBy(ev => ev.time)
                         .ThenBy(ev => (int)ev.basicBeatmapEventType))
                {
                    int eventType = (int)e.basicBeatmapEventType;

                    // Skip legacy rotation events (we handle rotations elsewhere)
                    if (eventType == 14 || eventType == 15)
                        continue;

                    // Skip color boost here; handled separately
                    if (eventType == 5)
                        continue;

                    int metaIndex = GetBasicMetaIndex(
                        eventType,
                        e.value,
                        e.floatValue
                    );

                    basicEvents.Add(new JObject
                    {
                        ["b"] = R4(e.time * timeMult),
                        ["i"] = metaIndex
                    });
                }
            }

            root["basicEvents"] = basicEvents;
            root["basicEventsData"] = basicEventsData;

            // ============================================================
            // 2) COLOR BOOST EVENTS (legacy type 5)
            //
            // colorBoostEvents     : [{ "b": time, "i": metaIndex }]
            // colorBoostEventsData : deduped by "b" (0 or 1)
            //
            // eData.ColorBoostEvents already has a bool "boostColorsAreOn".
            // ============================================================
            var colorBoostEvents = new JArray();
            var colorBoostEventsData = new JArray();

            var cbMetaMap = new Dictionary<int, int>(); // key = 0/1

            int GetColorBoostMetaIndex(bool boostOn)
            {
                int bVal = boostOn ? 1 : 0;

                if (!cbMetaMap.TryGetValue(bVal, out int idx))
                {
                    idx = cbMetaMap.Count;
                    cbMetaMap[bVal] = idx;

                    colorBoostEventsData.Add(new JObject
                    {
                        ["b"] = bVal
                    });
                }

                return idx;
            }

            if (eData.ColorBoostEvents != null)
            {
                foreach (var e in eData.ColorBoostEvents
                         .OrderBy(ev => ev.time))
                {
                    int metaIndex = GetColorBoostMetaIndex(e.boostColorsAreOn);

                    colorBoostEvents.Add(new JObject
                    {
                        ["b"] = R4(e.time * timeMult),
                        ["i"] = metaIndex
                    });
                }
            }

            root["colorBoostEvents"] = colorBoostEvents;
            root["colorBoostEventsData"] = colorBoostEventsData;

            // ============================================================
            // 3) WAYPOINTS
            //
            // We still pull these from OriginalCBData because EditableCBD
            // doesn’t track them (and you don’t need to modify them).
            //
            // waypoints     : [{ "b": time, "i": metaIndex }]
            // waypointsData : deduped by (x, y, d)
            // ============================================================
            var waypoints = new JArray();
            var waypointsData = new JArray();

            var wpMetaMap = new Dictionary<(int x, int y, int d), int>();

            int GetWaypointMetaIndex(int x, int y, int d)
            {
                var key = (x, y, d);

                if (!wpMetaMap.TryGetValue(key, out int idx))
                {
                    idx = wpMetaMap.Count;
                    wpMetaMap[key] = idx;

                    waypointsData.Add(new JObject
                    {
                        ["x"] = x,
                        ["y"] = y,
                        ["d"] = d
                    });
                }

                return idx;
            }

            var cbData = eData.OriginalCBData;
            if (cbData != null && cbData.beatmapObjectDatas != null)
            {
                foreach (var w in cbData.beatmapObjectDatas
                                         .OfType<WaypointData>()
                                         .OrderBy(w => w.time)
                                         .ThenBy(w => w.lineIndex)
                                         .ThenBy(w => (int)w.lineLayer))
                {
                    int metaIndex = GetWaypointMetaIndex(
                        w.lineIndex,
                        (int)w.lineLayer,
                        (int)w.offsetDirection
                    );

                    waypoints.Add(new JObject
                    {
                        ["b"] = R4(w.time * timeMult),
                        ["i"] = metaIndex
                    });
                }
            }

            root["waypoints"] = waypoints;
            root["waypointsData"] = waypointsData;

            // ============================================================
            // 4) COMPAT FLAG
            //
            // This tells v4 to treat these old basic events as "compatible"
            // events so they behave like classic v2/v3 lights.
            // ============================================================
            root["useNormalEventsAsCompatibleEvents"] = true;

            return root;
        }


        /// <summary>
        /// Creates a v4 AudioData JObject. Must set Config.Instance.OutputV4JsonSongFrequency if not 44100!
        /// </summary>
        /// <param name="songSampleCount">Total audio samples in the song.</param>
        /// <param name="songFrequency">Sample rate (e.g. 44100).</param>
        /// <param name="bpm">Song BPM.</param>
        /// <param name="songChecksum">Optional checksum string (can be "").</param>
        /// <param name="lufs">Approximate loudness (LUFS). 0 is fine if unknown.</param>
        public static JObject JsonV4AudioDataOutput(BeatmapLevel level, int songFrequency)
        {
            string songChecksum = "";
            int songSampleCount = (int)(level.songDuration * songFrequency);
            float bpm = level.beatsPerMinute;
            float lufs = level.integratedLufs;

            JObject root = new JObject();
            root["version"] = "4.0.0";
            root["songChecksum"] = songChecksum;
            root["songSampleCount"] = songSampleCount;
            root["songFrequency"] = songFrequency;

            // --- BPM segment ---
            // Compute end beat = seconds * bpm / 60
            float lengthSeconds = (float)songSampleCount / (float)songFrequency;
            float endBeat = lengthSeconds * bpm / 60f;

            JArray bpmData = new JArray();
            JObject bpmSeg = new JObject();
            bpmSeg["si"] = 0;                 // Start sample index
            bpmSeg["ei"] = songSampleCount;   // End sample index
            bpmSeg["sb"] = 0f;                // Start beat
            bpmSeg["eb"] = endBeat;           // End beat
            bpmData.Add(bpmSeg);
            root["bpmData"] = bpmData;

            // --- LUFS segment ---
            JArray lufsData = new JArray();
            JObject lufsSeg = new JObject();
            lufsSeg["si"] = 0;
            lufsSeg["ei"] = songSampleCount;
            lufsSeg["l"] = lufs;
            lufsData.Add(lufsSeg);
            root["lufsData"] = lufsData;

            return root;
        }

        public static JObject JsonV2OutputOLD(CustomBeatmapData data, EditableCBD eData, float timeMult) //v2 maps
        {

            var rotationEventsData = eData.RotationEvents;
            var colorBoostEvents = eData.ColorBoostEvents;


            var root = new JObject //v2.5.0 added floatValue. v2.6.0 added arcs but doesn't work
            {
                ["_version"] = (data.version < new Version(2, 5, 0)
                                ? new Version(2, 5, 0)
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
                    ["_time"] = R4(n.time * timeMult),
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
                if (!RemoveMappingExtensionWalls(w, out int legacyType))
                   continue; // skip Mapping Extensions / weird walls
                //if (!TryGetV2ObstacleType(w, out int legacyType)) // NOT WORKING
                //    continue;

                var o = new JObject
                {
                    ["_time"] = R4(w.time * timeMult),
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

            /*
            bool TryGetV2ObstacleType(CustomObstacleData o, out int type)
            {
                type = 0;

                int layerInt = (int)o.lineLayer;
                int heightInt = o.height;

                // Vanilla v2 (safe and exact)
                if (layerInt == 0 && heightInt == 5) { type = 0; return true; }
                if (layerInt == 2 && heightInt == 3) { type = 1; return true; }

                // Detect "ME / precision" obstacles by any of these signals
                bool isME =
                    Math.Abs(o.lineIndex) >= 1000 ||
                    Math.Abs(o.width) >= 1000 ||
                    Math.Abs(layerInt) >= 1000 ||
                    Math.Abs(heightInt) >= 1000 ||
                    heightInt > 2; // (ME loader patches also act on height > 2 sometimes)

                if (!isME)
                    return false; // not representable in v2 without guessing

                // We can only round-trip correctly if your current obstacle fields
                // are in the same "decoded int" space ME uses:
                //  - lineLayer like 3560, 5254...
                //  - height like 1160...
                // If you instead have generator-style height codes (1100, 1500, 2500),
                // you need a different encoder (see note below).

                // If heightInt looks like generator-style (e.g. 1100,1500,2500),
                // convert it into ME-decoded heightInt (e.g. 1160 for 0.16) first:
                // ME-decoded heightInt = 1000 + 1000 * trackHeight
                // generator trackHeight = (h - 1000)/1000
                // => decoded heightInt = h (so 1100 stays 1100). That’s OK.
                // But to match tiny v2 particles like your sample (heightInt=1160),
                // you need heightInt that already reflects the v2-derived height.
                //
                // So we’ll treat both cases:
                // - If heightInt is close to "1000 + 5*k" we can invert cleanly.
                // - Otherwise we’ll derive obsHeight from trackHeight.

                int obsHeight;

                // Case A: already ME-decoded heightInt from v2 loader: heightInt = 1000 + 5*obsHeight
                if (heightInt >= 1000 && (heightInt - 1000) % 5 == 0)
                {
                    obsHeight = (int)Math.Round((heightInt - 1000) / 5f);
                }
                else
                {
                    // Case B: generator-style ME height code: trackHeight = (heightInt - 1000)/1000
                    // v2 final trackHeight = 5*obsHeight/1000 => obsHeight = trackHeight*1000/5
                    float trackHeight =
                        heightInt >= 1000 ? (heightInt - 1000) / 1000f :
                        heightInt <= -1000 ? (heightInt + 2000) / 1000f :
                        heightInt > 2 ? heightInt : 0f;

                    obsHeight = (int)Math.Round(trackHeight * 1000f / 5f);
                }

                obsHeight = Math.Clamp(obsHeight, 0, 4000);

                // startHeight from layerInt:
                // If layerInt looks like ME-decoded (>=1000), invert it.
                // Otherwise (layer 0..9 etc.), you can choose startHeight=0 or map it.
                int startHeight;
                if (Math.Abs(layerInt) >= 1000)
                {
                    startHeight = (int)Math.Round((layerInt - 1334f) * 750f / 5000f);
                }
                else
                {
                    // If your generator uses lineLayer 0..9 and you want vertical placement,
                    // keep it simple: startHeight=0 (particles centered), or map:
                    // startHeight ≈ 150*layer - 200 (clamped)
                    startHeight = (int)Math.Round(150f * layerInt - 200f);
                }

                startHeight = Math.Clamp(startHeight, 0, 999);

                // PreciseHeightStart encoding
                type = 4001 + obsHeight * 1000 + startHeight;
                return true;
            }
            */

            bool RemoveMappingExtensionWalls(CustomObstacleData obstacle, out int legacyType)
            {
                legacyType = 0;

                // 1) Reject Mapping Extensions

                // ME precise lanes / out-of-bounds 
                if (Math.Abs(obstacle.lineIndex) >= 1000)
                    return false;

                // ME precise widths 
                if (obstacle.width >= 1000)
                    return false;

                // ME-coded heights
                if (obstacle.height >= 1000)
                    return false;

                // If you ALSO want to reject extension walls (far left/right lanes),
                // if (obstacle.lineIndex < 0 || obstacle.lineIndex > 3)
                //     return false;

                // 2) Now only accept "real" v2 shapes

                int layer = (int)obstacle.lineLayer;
                int height = obstacle.height;

                // Full-height wall
                if (layer == 0 && height == 5)
                {
                    legacyType = 0; // v2 full-height
                    return true;
                }

                // Crouch wall
                if (layer == 2 && height == 3)
                {
                    legacyType = 1; // v2 crouch
                    return true;
                }

                // Everything else = not safely representable in plain v2
                return false;
            }
            /*
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
            */
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
                        ["_floatValue"] = e.floatValue
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
        /*
           public static JObject JsonV3OutputUNUSED(EditableCBD eData, float timeMult) // arc chain rotation problems 
           {
               if (eData == null) throw new ArgumentNullException(nameof(eData));

               var root = new JObject();
               root["version"] = new Version(3, 3, 0).ToString();

               // ─────────────────────────────────────────────────────────
               // 1) COLOR NOTES (left/right)
               //   colorNotes: [{ b,x,y,c,d,customData? }]
               // ─────────────────────────────────────────────────────────
               var colorNotes = new JArray();

               if (eData.ColorNotes != null)
               {
                   foreach (var n in eData.ColorNotes
                            .OrderBy(n => n.time)
                            .ThenBy(n => n.line)
                            .ThenBy(n => n.layer))
                   {
                       var o = new JObject
                       {
                           ["b"] = R4(n.time * timeMult),
                           ["x"] = n.line,
                           ["y"] = n.layer,
                           ["c"] = (int)n.colorType,     // 0 / 1
                           ["d"] = (int)n.cutDirection
                       };

                       if (n.customData != null && n.customData.Count > 0)
                           o["customData"] = JObject.FromObject(n.customData);

                       colorNotes.Add(o);
                   }
               }

               root["colorNotes"] = colorNotes;

               // ─────────────────────────────────────────────────────────
               // 2) BOMB NOTES
               //   bombNotes: [{ b,x,y,customData? }]
               // ─────────────────────────────────────────────────────────
               var bombNotes = new JArray();

               if (eData.BombNotes != null)
               {
                   foreach (var b in eData.BombNotes
                            .OrderBy(bm => bm.time)
                            .ThenBy(bm => bm.line)
                            .ThenBy(bm => bm.layer))
                   {
                       var o = new JObject
                       {
                           ["b"] = R4(b.time * timeMult),
                           ["x"] = b.line,
                           ["y"] = b.layer
                       };

                       if (b.customData != null && b.customData.Count > 0)
                           o["customData"] = JObject.FromObject(b.customData);

                       bombNotes.Add(o);
                   }
               }

               root["bombNotes"] = bombNotes;

               // ─────────────────────────────────────────────────────────
               // 3) OBSTACLES
               //   obstacles: [{ b,x,y,d,w,h,customData? }]
               // ─────────────────────────────────────────────────────────
               var obstacles = new JArray();

               if (eData.Obstacles != null)
               {
                   foreach (var w in eData.Obstacles
                            .OrderBy(o => o.time)
                            .ThenBy(o => o.line)
                            .ThenBy(o => o.layer))
                   {
                       var o = new JObject
                       {
                           ["b"] = R4(w.time * timeMult),
                           ["x"] = w.line,
                           ["y"] = w.layer,
                           ["d"] = R4(w.duration * timeMult),
                           ["w"] = w.width,
                           ["h"] = w.height
                       };

                       if (w.customData != null && w.customData.Count > 0)
                           o["customData"] = JObject.FromObject(w.customData);

                       obstacles.Add(o);
                   }
               }

               root["obstacles"] = obstacles;

               // ─────────────────────────────────────────────────────────
               // 4) COLOR BOOST EVENTS
               //   colorBoostBeatmapEvents: [{ b,o,customData? }]
               // ─────────────────────────────────────────────────────────
               var boostEvents = new JArray();

               if (eData.ColorBoostEvents != null)
               {
                   foreach (var bo in eData.ColorBoostEvents
                            .OrderBy(e => e.time))
                   {
                       var o = new JObject
                       {
                           ["b"] = R4(bo.time * timeMult),
                           ["o"] = bo.boostColorsAreOn
                       };

                       if (bo.customData != null && bo.customData.Count > 0)
                           o["customData"] = JObject.FromObject(bo.customData);

                       boostEvents.Add(o);
                   }
               }

               root["colorBoostBeatmapEvents"] = boostEvents;

               // ─────────────────────────────────────────────────────────
               // 5) ARCS (sliders)
               //   sliders: [{ c,b,x,y,d,mu,tb,tx,ty,tc,tmu,m,customData? }]
               // ─────────────────────────────────────────────────────────
               var sliders = new JArray();

               if (eData.Arcs != null)
               {
                   foreach (var s in eData.Arcs
                            .Where(a => a.sliderType == ESliderType.Arc)
                            .OrderBy(a => a.time)
                            .ThenBy(a => a.line)
                            .ThenBy(a => a.layer))
                   {
                       var o = new JObject
                       {
                           ["c"] = (int)s.colorType,
                           ["b"] = R4(s.time * timeMult),
                           ["x"] = s.line,
                           ["y"] = s.layer,
                           ["d"] = (int)s.cutDirection,
                           ["mu"] = s.headControlPointLengthMultiplier,
                           ["tb"] = R4(s.tailTime * timeMult),
                           ["tx"] = s.tailLine,
                           ["ty"] = s.tailLayer,
                           ["tc"] = (int)s.tailCutDirection,
                           ["tmu"] = s.tailControlPointLengthMultiplier,
                           ["m"] = (int)s.sliderMidAnchorMode
                       };

                       if (s.customData != null && s.customData.Count > 0)
                           o["customData"] = JObject.FromObject(s.customData);

                       sliders.Add(o);
                   }
               }

               root["sliders"] = sliders;

               // ─────────────────────────────────────────────────────────
               // 6) CHAINS (burst sliders)
               //   burstSliders: [{ c,b,x,y,d,tb,tx,ty,sc,s,customData? }]
               // ─────────────────────────────────────────────────────────
               var burstSliders = new JArray();

               if (eData.Chains != null)
               {
                   foreach (var s in eData.Chains
                            .Where(c => c.sliderType == ESliderType.Chain)
                            .OrderBy(c => c.time)
                            .ThenBy(c => c.line)
                            .ThenBy(c => c.layer))
                   {
                       var o = new JObject
                       {
                           ["c"] = (int)s.colorType,
                           ["b"] = R4(s.time * timeMult),
                           ["x"] = s.line,
                           ["y"] = s.layer,
                           ["d"] = (int)s.cutDirection,
                           ["tb"] = R4(s.tailTime * timeMult),
                           ["tx"] = s.tailLine,
                           ["ty"] = s.tailLayer,
                           ["sc"] = s.sliceCount,
                           ["s"] = s.squishAmount
                       };

                       if (s.customData != null && s.customData.Count > 0)
                           o["customData"] = JObject.FromObject(s.customData);

                       burstSliders.Add(o);
                   }
               }

               root["burstSliders"] = burstSliders;

               // ─────────────────────────────────────────────────────────
               // 7) BASIC EVENTS (lighting, rings, lasers, etc.)
               //   basicBeatmapEvents: [{ b,et,i,f,customData? }]
               // ─────────────────────────────────────────────────────────
               var basicEvents = new JArray();

               if (eData.BasicEvents != null)
               {
                   foreach (var e in eData.BasicEvents
                            .OrderBy(ev => ev.time)
                            .ThenBy(ev => (int)ev.basicBeatmapEventType))
                   {
                       var o = new JObject
                       {
                           ["b"] = R4(e.time * timeMult),
                           ["et"] = (int)e.basicBeatmapEventType,
                           ["i"] = e.value,
                           ["f"] = e.floatValue
                       };

                       if (e.customData != null && e.customData.Count > 0)
                           o["customData"] = JObject.FromObject(e.customData);

                       basicEvents.Add(o);
                   }
               }

               root["basicBeatmapEvents"] = basicEvents;

               // ─────────────────────────────────────────────────────────
               // 8) ROTATION EVENTS
               //   rotationEvents: [{ b,e,r,customData? }]
               //   - We treat all as "early" (e = 0) for now, using r.rotation
               //     which is already in degrees from eData.
               // ─────────────────────────────────────────────────────────
               var rotationEvents = new JArray();

               if (eData.RotationEvents != null)
               {
                   foreach (var r in eData.RotationEvents
                            .OrderBy(ev => ev.time))
                   {
                       var o = new JObject
                       {
                           ["b"] = R4(r.time * timeMult),
                           ["e"] = 0,           // 0 = early; 1 = late if you add that later
                           ["r"] = r.rotation   // already degrees in your pipeline
                       };

                       if (r.customData != null && r.customData.Count > 0)
                           o["customData"] = JObject.FromObject(r.customData);

                       rotationEvents.Add(o);
                   }
               }

               root["rotationEvents"] = rotationEvents;

               // ─────────────────────────────────────────────────────────
               // 9) BPM EVENTS (from original CBData)
               //   bpmEvents: [{ b,m,customData? }]
               // ─────────────────────────────────────────────────────────
               var bpmEvents = new JArray();

               if (eData.OriginalCBData != null &&
                   eData.OriginalCBData.beatmapEventDatas != null)
               {
                   foreach (var bp in eData.OriginalCBData.beatmapEventDatas
                                .OfType<CustomBPMChangeBeatmapEventData>()
                                .OrderBy(ev => ev.time))
                   {
                       var o = new JObject
                       {
                           ["b"] = R4(bp.time * timeMult),
                           ["m"] = bp.bpm
                       };

                       if (bp.customData != null && bp.customData.Count > 0)
                           o["customData"] = JObject.FromObject(bp.customData);

                       bpmEvents.Add(o);
                   }
               }

               root["bpmEvents"] = bpmEvents;

               // ─────────────────────────────────────────────────────────
               // 10) CUSTOM EVENTS (if present)
               //   customEvents: [{ b,t,customData? }]
               // ─────────────────────────────────────────────────────────
               if (eData.CustomEvents != null && eData.CustomEvents.Count > 0)
               {
                   var customEvents = new JArray();

                   foreach (var c in eData.CustomEvents
                            .OrderBy(ev => ev.time))
                   {
                       var o = new JObject
                       {
                           ["b"] = R4(c.time * timeMult),
                           ["t"] = c.eventType
                       };

                       if (c.customData != null && c.customData.Count > 0)
                           o["customData"] = JObject.FromObject(c.customData);

                       customEvents.Add(o);
                   }

                   root["customEvents"] = customEvents;
               }

               return root;
           }
           */

        // ----------------------------------------------------------
        // AutoBS metadata helpers
        // ----------------------------------------------------------
        
        /// <summary>
        /// Ensure v2 root has _customData and add/update "generatedBy".
        /// Keeps any existing _customData keys intact.
        /// </summary>
        static void AddAutoBSMetadataV2(JObject root)
        {
            if (root == null) return;

            var cd = root["_customData"] as JObject;
            if (cd == null)
            {
                cd = new JObject();
                root["_customData"] = cd;
            }

            cd["generatedBy"] = $"{ModName}";
        }

        /// <summary>
        /// Ensure v3/v4 root has customData and add/update "generatedBy".
        /// Keeps any existing customData keys intact.
        /// </summary>
        static void AddAutoBSMetadataV3OrV4(JObject root)
        {
            if (root == null) return;

            var cd = root["customData"] as JObject;
            if (cd == null)
            {
                cd = new JObject();
                root["customData"] = cd;
            }

            cd["generatedBy"] = $"{ModName}";
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
