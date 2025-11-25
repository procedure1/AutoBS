using System;
using System.Collections.Generic;
using AutoBS.Patches;
using CustomJSONData.CustomBeatmap;
using static BeatmapSaveDataVersion3.BeatmapSaveData;

using Tweening;
using static IndexFilter;
using static BloomPrePassRenderDataSO;
using System.Linq;

namespace AutoBS
{
    /*
    public static class GLSConverter
    {
        public static void ConvertToGLSEvents(List<CustomBasicBeatmapEventData> v2lights, string envName)
        {
            // Ensure we're working with "The Second" environment
            if (envName != "TheSecondEnvironment")
            {
                Plugin.Log.Info($"[GLSConverter] Skipping conversion, environment is not The Second: {envName}");
                return;
            }

            // Create a new CustomBeatmapSaveData (or get a reference to your beatmap's save data)
            // Retrieve the stored beatmap save data
            CustomBeatmapSaveData beatmapSaveData = HarmonyPatches.CurrentBeatmapSaveData;

            if (beatmapSaveData == null)
            {
                Plugin.Log.Error("[GLSConverter] No CustomBeatmapSaveData found! Cannot convert events.");
                return;
            }

            
            Plugin.Log.Info("---- LIGHT COLOR EVENT BOX GROUPS Before Starting Conversion ----");
            foreach (var group in beatmapSaveData.lightColorEventBoxGroups)
            {
                Plugin.Log.Info($"Group {group.groupId} at Beat {group.beat}, Events: {group.eventBoxes.Count}");
            }

            Plugin.Log.Info("---- LIGHT ROTATION EVENT BOX GROUPS Before Starting Conversion ----");
            foreach (var group in beatmapSaveData.lightRotationEventBoxGroups)
            {
                Plugin.Log.Info($"Group {group.groupId} at Beat {group.beat}, Events: {group.eventBoxes.Count}");
            }
            
            Plugin.Log.Info($"[GLSConverter] Converting v2 events into GLS events for environment '{envName}'");

            //List<BeatmapSaveDataVersion3.BeatmapSaveData.BasicEventData> basicEvents = new List<BeatmapSaveDataVersion3.BeatmapSaveData.BasicEventData>();

            foreach (var v2Event in v2lights)
            {
                // Nothing!
                // TEST use basic event data inside of BeatmapSaveData
                // Convert the V2 event to BasicEventData
                
                var basicEvent = new BeatmapSaveDataVersion3.BeatmapSaveData.BasicEventData(
                    v2Event.time, // Beat time of the event
                    (BeatmapSaveDataVersion2_6_0AndEarlier.BeatmapSaveData.BeatmapEventType)v2Event.type, // Event type conversion
                    v2Event.value, // Event value (brightness, color, etc.)
                    v2Event.floatValue // Float value (e.g., transition timing)
                );
                // Add to the basic event list
                basicEvents.Add(basicEvent);
                Plugin.Log.Info($"Inserted BasicEventData at Beat {v2Event.time}, Type {v2Event.type}, Value {v2Event.value}");
                


                float beat = v2Event.time;
                int groupId = GetGroupID(v2Event.basicBeatmapEventType); // New function

                // Brightness Scaling Fix
                float scaledBrightness = Math.Clamp(v2Event.floatValue * 1.5f, 0.1f, 1.0f);

                //var convertedTransitionType = (BeatmapSaveDataVersion3.BeatmapSaveData.EaseType)EaseType.InOutQuad;

                // Create Light Color Event Box
                var eventBox = new BeatmapSaveDataVersion3.BeatmapSaveData.LightColorEventBox(
                new BeatmapSaveDataVersion3.BeatmapSaveData.IndexFilter(
                    type: BeatmapSaveDataVersion3.BeatmapSaveData.IndexFilter.IndexFilterType.StepAndOffset,
                    param0: 0,
                    param1: 1,
                    reversed: false,
                    random: BeatmapSaveDataVersion3.BeatmapSaveData.IndexFilterRandomType.NoRandom,
                    seed: 0,
                    chunks: 0,
                    limit: 1f,
                    limitAlsoAffectsType: 0
                ),
                beatDistributionParam: 0.5f,
                beatDistributionParamType: BeatmapSaveDataVersion3.BeatmapSaveData.EventBox.DistributionParamType.Wave,
                brightnessDistributionParam: 2f,
                brightnessDistributionShouldAffectFirstBaseEvent: true,
                brightnessDistributionParamType: BeatmapSaveDataVersion3.BeatmapSaveData.EventBox.DistributionParamType.Wave,
                brightnessDistributionEaseType: BeatmapSaveDataVersion3.BeatmapSaveData.EaseType.Linear,
                lightColorBaseDataList: new List<BeatmapSaveDataVersion3.BeatmapSaveData.LightColorBaseData>
                {
                    new BeatmapSaveDataVersion3.BeatmapSaveData.LightColorBaseData(
                        beat,
                        BeatmapSaveDataVersion3.BeatmapSaveData.TransitionType.Instant,
                        BeatmapSaveDataVersion3.BeatmapSaveData.EnvironmentColorType.Color0,
                        scaledBrightness,
                        0,
                        0f,
                        false)
                }
                );


                // Create Event Box Group
                LightColorEventBoxGroup lightColorEventBoxGroup = new LightColorEventBoxGroup(beat, groupId, new List<LightColorEventBox> { eventBox });

                // Add to beatmap data
                beatmapSaveData.lightColorEventBoxGroups.Add(lightColorEventBoxGroup);

                //Plugin.Log.Info($"[GLSConverter] Inserted LightColorEventBoxGroup at Beat {beat} for Group {groupId}, Brightness {scaledBrightness}");

            }
            foreach (var v2Event in v2lights)
            {
                float beat = v2Event.time;
                int groupId = GetGroupID(v2Event.basicBeatmapEventType);

                var rotationEventBox = new BeatmapSaveDataVersion3.BeatmapSaveData.LightRotationEventBox(
                new BeatmapSaveDataVersion3.BeatmapSaveData.IndexFilter(
                    type: BeatmapSaveDataVersion3.BeatmapSaveData.IndexFilter.IndexFilterType.StepAndOffset, // Use correct enum
                    param0: 1,  // Start index
                    param1: 1,  // Step
                    reversed: false, // Not reversed
                    random: BeatmapSaveDataVersion3.BeatmapSaveData.IndexFilterRandomType.NoRandom, // No randomness
                    seed: 0, // Default seed
                    chunks: 0, // Default chunk size
                    limit: 1f, // Full visibility
                    limitAlsoAffectsType: BeatmapSaveDataVersion3.BeatmapSaveData.IndexFilterLimitAlsoAffectsType.None // No limit effect
                ),
                beatDistributionParam: 0.5f, // Weight
                beatDistributionParamType: BeatmapSaveDataVersion3.BeatmapSaveData.EventBox.DistributionParamType.Step, // Distribution type
                rotationDistributionParam: 2f, // Rotation distribution
                rotationDistributionParamType: BeatmapSaveDataVersion3.BeatmapSaveData.EventBox.DistributionParamType.Step, // Rotation distribution type
                rotationDistributionShouldAffectFirstBaseEvent: true, // Affect first base event
                rotationDistributionEaseType: BeatmapSaveDataVersion3.BeatmapSaveData.EaseType.Linear, // Ease type for distribution
                axis: Axis.X, // Choose correct axis (X, Y, Z)
                flipRotation: false, // No flip rotation
                lightRotationBaseDataList: new List<BeatmapSaveDataVersion3.BeatmapSaveData.LightRotationBaseData>
                {
                    new BeatmapSaveDataVersion3.BeatmapSaveData.LightRotationBaseData(
                        beat,
                        false, // Do not use previous event rotation
                        BeatmapSaveDataVersion3.BeatmapSaveData.EaseType.InOutQuad,
                        0, // Loops count
                        30f, // Rotation angle
                        BeatmapSaveDataVersion3.BeatmapSaveData.LightRotationBaseData.RotationDirection.Clockwise // Rotation direction
                    )
                }
            );


                var lightRotationEventBoxGroup = new LightRotationEventBoxGroup(beat, groupId, new List<LightRotationEventBox> { rotationEventBox });

                beatmapSaveData.lightRotationEventBoxGroups.Add(lightRotationEventBoxGroup);

                //Plugin.Log.Info($"[GLSConverter] Inserted LightRotationEventBoxGroup at Beat {beat} for Group {groupId}");
            }

            Plugin.Log.Info("---- FINAL LIGHT COLOR EVENT BOX GROUPS ----");
            foreach (var group in beatmapSaveData.lightColorEventBoxGroups)
            {
                Plugin.Log.Info($"Group {group.groupId} at Beat {group.beat}, Events: {group.eventBoxes.Count}");
            }

            Plugin.Log.Info("---- FINAL LIGHT ROTATION EVENT BOX GROUPS ----");
            foreach (var group in beatmapSaveData.lightRotationEventBoxGroups)
            {
                Plugin.Log.Info($"Group {group.groupId} at Beat {group.beat}, Events: {group.eventBoxes.Count}");
            }


        }
        private static int GetGroupID(BasicBeatmapEventType eventType)
        {
            switch (eventType)
            {
                case BasicBeatmapEventType.Event0: return 4; // Back Lasers
                case BasicBeatmapEventType.Event1: return 3; // Ring Lights
                case BasicBeatmapEventType.Event2: return 6; // Left Lasers
                case BasicBeatmapEventType.Event3: return 7; // Right Lasers
                case BasicBeatmapEventType.Event4: return 5; // Center Lasers
                case BasicBeatmapEventType.Event12: return 2; // Speed Left
                case BasicBeatmapEventType.Event13: return 2; // Speed Right
                case BasicBeatmapEventType.Event8: return 1; // Ring Spin
                case BasicBeatmapEventType.Event9: return 0; // Ring Zoom
                default: return 1; // Default group
            }
        }


        private static void ConvertForTheSecond(List<CustomBasicBeatmapEventData> v2Events, CustomBeatmapSaveData beatmapData)
        {
            foreach (CustomBasicBeatmapEventData v2 in v2Events)
            {
                BasicEventData basicEvent = new BasicEventData(v2.time, (EventType)v2.basicBeatmapEventType, (EventValue)v2.value, v2.floatValue);

                if (basicEvent.eventType == EventType.BACK || basicEvent.eventType == EventType.RING ||
                    basicEvent.eventType == EventType.LEFT || basicEvent.eventType == EventType.RIGHT ||
                    basicEvent.eventType == EventType.CENTER)
                {
                    LightColorEventBoxGroup glsGroup = ConvertV2ToGLS_LightColor(basicEvent);
                    beatmapData.lightColorEventBoxGroups.Add(glsGroup);
                }
                else if (basicEvent.eventType == EventType.RING_SPIN || basicEvent.eventType == EventType.RING_ZOOM)
                {
                    LightRotationEventBoxGroup glsRotation = ConvertV2ToGLS_LightRotation(basicEvent);
                    beatmapData.lightRotationEventBoxGroups.Add(glsRotation);
                }
            }

            foreach (var group in beatmapData.lightColorEventBoxGroups)
            {
                Plugin.Log.Info($"[GLSConverter] created LightColorEventBoxGroup: Beat: {group.beat}, GroupID: {group.groupId}, BaseEventBoxes: {group.baseEventBoxes}, EventBoxes: {group.eventBoxes} ");
                foreach (var bas in group.baseEventBoxes)
                {
                    Plugin.Log.Info($" ---- Base Event Boxes: IndexFiler: {bas.indexFilter}");
                }
                foreach (var e in group.eventBoxes)
                {
                    Plugin.Log.Info($" ---- Event Boxes: IndexFiler: {e.brightnessDistributionParam} IndexFiler: {e.brightnessDistributionParam}");
                }
            }
            foreach (var group in beatmapData.lightRotationEventBoxGroups)
            {
                Plugin.Log.Info($"[GLSConverter] created LightColorEventBoxGroup: Beat: {group.beat}, GroupID: {group.groupId}, BaseEventBoxes: {group.baseEventBoxes}, EventBoxes: {group.eventBoxes} ");
                foreach (var bas in group.baseEventBoxes)
                {
                    Plugin.Log.Info($" ---- Base Event Boxes: IndexFiler: {bas.indexFilter}");
                }
                foreach (var e in group.eventBoxes)
                {
                    Plugin.Log.Info($" ---- Event Boxes: IndexFiler: {e.indexFilter}, Axis: {e.axis}, lightRotationBaseDataList: {e.lightRotationBaseDataList}, rotationDistributionParam: {e.rotationDistributionParam},");
                }
            }

            Plugin.Log.Info("[GLSConverter] Finished converting events for 'the second'.");
        }
        public static LightColorEventBoxGroup ConvertV2ToGLS_LightColor(BasicEventData v2Event)
        {
            EnvironmentColorType color = (v2Event.eventValue == EventValue.RED_ON) ? EnvironmentColorType.Color0 : EnvironmentColorType.Color1;
            float beat = v2Event.time;

            var convertedColor = (BeatmapSaveDataVersion3.BeatmapSaveData.EnvironmentColorType)(int)color;

            var baseData = new BeatmapSaveDataVersion3.BeatmapSaveData.LightColorBaseData(
                beat,
                BeatmapSaveDataVersion3.BeatmapSaveData.TransitionType.Instant,  // Check if it's called `TransitionType`
                convertedColor,
                v2Event.floatValue,
                0,
                0f,
                false
            );

            var convertedEaseType = (BeatmapSaveDataVersion3.BeatmapSaveData.EaseType)EaseType.Linear;

            var eventBox = new LightColorEventBox(null, 0f, EventBox.DistributionParamType.Step, 0f, false, EventBox.DistributionParamType.Step, convertedEaseType, new List<BeatmapSaveDataVersion3.BeatmapSaveData.LightColorBaseData> { baseData });
            return new LightColorEventBoxGroup(beat, 1, new List<LightColorEventBox> { eventBox });
        }

        public static LightRotationEventBoxGroup ConvertV2ToGLS_LightRotation(BasicEventData v2Event)
        {
            float beat = v2Event.time;

            var convertedEaseType = (BeatmapSaveDataVersion3.BeatmapSaveData.EaseType)EaseType.InOutQuad;
            var convertedDirection = (BeatmapSaveDataVersion3.BeatmapSaveData.LightRotationBaseData.RotationDirection)LightRotationDirection.Clockwise;

            var rotationBaseData = new List<BeatmapSaveDataVersion3.BeatmapSaveData.LightRotationBaseData>
            {
                new BeatmapSaveDataVersion3.BeatmapSaveData.LightRotationBaseData(beat, false, convertedEaseType, 1, 180, convertedDirection)

            };

            convertedEaseType = (BeatmapSaveDataVersion3.BeatmapSaveData.EaseType)EaseType.Linear;

            var rotationEventBox = new LightRotationEventBox(null, 0f, EventBox.DistributionParamType.Step, 0f, EventBox.DistributionParamType.Step, false, convertedEaseType, Axis.Y, false, rotationBaseData);
            return new LightRotationEventBoxGroup(beat, 2, new List<LightRotationEventBox> { rotationEventBox });
        }
    }
    */
}
