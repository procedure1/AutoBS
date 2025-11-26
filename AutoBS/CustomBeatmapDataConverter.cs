using AutoBS.Patches;
using CustomJSONData.CustomBeatmap;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
        public static string ToJsonStringFile(CustomBeatmapData data, int preferredVersion = 3, bool outputSecondsToBeats = true, EditableCBD eData = null) // if converted to seconds for FromJsonString then need this
        {
            // ───────── Common setup ─────────────────────────────────────
            float timeMult = 1;
            if (outputSecondsToBeats)
                timeMult = TransitionPatcher.bpm / 60f; // convert from bpm to bps

            int majorVersion = data.version.Major;

            JObject root = new JObject();

            if (preferredVersion == 2 && majorVersion < 3)
                root = JsonV2Output(data, timeMult);
            else if (preferredVersion >= 3)
                root = JsonV3Output(data, timeMult, eData.RotationEvents);

            string jsonString = JsonConvert.SerializeObject(root, Formatting.None);

            string path = @"D:\" + SetContent.SongName + "_" + TransitionPatcher.SelectedDifficulty.ToString() + "_" + TransitionPatcher.SelectedCharacteristicSO.serializedName + "_v" + preferredVersion + ".dat";

            Plugin.Log.Info($"[ToJsonStringFile] Original Map v{data.version}. Outputing JSON file to: {path}");

            System.IO.File.WriteAllText(path, jsonString); // Test

            return jsonString;

        }

        public static JObject JsonV2Output(CustomBeatmapData data, float timeMult) //v2 maps
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
                var o = new JObject
                {
                    ["_time"] = w.time * timeMult,
                    ["_lineIndex"] = w.lineIndex,
                    ["_type"] = GetLegacyWallType(w),
                    ["_duration"] = w.duration * timeMult,
                    ["_width"] = w.width,
                    //["_height"] = w.height
                };
                if (w.rotation != 0) o["_rotation"] = w.rotation;
                if (w.customData.Count > 0)
                    o["_customData"] = JObject.FromObject(w.customData);
                obs.Add(o);
            }
            root["_obstacles"] = obs;

            int GetLegacyWallType(CustomObstacleData obstacle)
            {
                if ((int)obstacle.lineLayer == 0 && obstacle.height == 5)
                    return 0; // Full-height wall
                if ((int)obstacle.lineLayer == 2 && obstacle.height == 3)
                    return 1; // Crouch wall
                return 2;     // Free wall (V3-style)
            }


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

            // ───────── EVENTS & CUSTOM EVENTS ───────────────────────────
            var evts = new JArray();
            foreach (var e in data.beatmapEventDatas.OfType<CustomBasicBeatmapEventData>())
            {
                var o = new JObject
                {
                    ["_time"] = e.time * timeMult,
                    ["_type"] = (int)e.basicBeatmapEventType,
                    ["_value"] = e.value,
                    //["_floatValue"] = e.floatValue
                };
                if (e.customData.Count > 0)
                    o["_customData"] = JObject.FromObject(e.customData);
                evts.Add(o);
            }
            root["_events"] = evts;

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

        public static JObject JsonV3Output(CustomBeatmapData data, float timeMult, List <ERotationEventData> rotationEvents1) //v3 maps
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
            foreach (var e in data.beatmapEventDatas
                    .OfType<CustomBasicBeatmapEventData>()
                    .Where(e => (int)e.basicBeatmapEventType != 14 && (int)e.basicBeatmapEventType != 15))
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
