using AutoBS.Patches;
using CustomJSONData.CustomBeatmap;
using SiraUtil.Zenject;
using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.Linq;
using System.Security.Cryptography;
using UnityEngine;

//https://github.com/Loloppe/ChroMapper-AutoMapper/
//@Lowoppe

// UPDATED: Added environment-based filtering for special events.
// Only special events required by Skrillex, Billie Eilish, or Lady Gaga environments will be added.
namespace AutoBS
{
    //---------------------------------------------------------------------------------------------------------------------------
    // V2 light events (not GLS v3 lights)
    public static class LightAutoMapper
    {
        public static void Start(EditableCBD eData)
        {
            // Check the environment.
            string envName = TransitionPatcher.EnvironmentName != null ? TransitionPatcher.EnvironmentName : "Default";

            // PROBLEM: if choose override same environment as default for the map, it was list as DefaultEnvironment!!!!!!!!!!!!!!!!!!!!!!!!! also, if the 2nd is overriden, it was listed as "TheSecondEnvironment" and if is defualt is listed as "The Second Environment"
            bool isSupportedEnvironment = IsV2Environment(envName);// || envName.Contains("Second");// || envName == "EDMEnvironment";

            Plugin.Log.Info($"[AutoLightMapper] {envName} Environment isSupported: {isSupportedEnvironment}"); // if v3 environment is chosen, then there is no reason to produce lights since they are not supported. Never was able to produce GLS lights

            if (!isSupportedEnvironment) return;

            // Get all original light events from the beatmap.
            List<EBasicEventData> originalLightEvents = eData.BasicEvents.ToList();


            // Initialize a Dictionary to count standard event types.
            Dictionary<BasicBeatmapEventType, int> eventTypeCounts =
                new Dictionary<BasicBeatmapEventType, int>();

            foreach (EBasicEventData lightEvent in originalLightEvents)
            {
                if (lightEvent.basicBeatmapEventType == BasicBeatmapEventType.Event0 ||
                    lightEvent.basicBeatmapEventType == BasicBeatmapEventType.Event1 ||
                    lightEvent.basicBeatmapEventType == BasicBeatmapEventType.Event2 ||
                    lightEvent.basicBeatmapEventType == BasicBeatmapEventType.Event3 ||
                    lightEvent.basicBeatmapEventType == BasicBeatmapEventType.Event4 ||
                    lightEvent.basicBeatmapEventType == BasicBeatmapEventType.Event8 ||
                    lightEvent.basicBeatmapEventType == BasicBeatmapEventType.Event9 ||
                    lightEvent.basicBeatmapEventType == BasicBeatmapEventType.Event12 ||
                    lightEvent.basicBeatmapEventType == BasicBeatmapEventType.Event13)
                {
                    if (eventTypeCounts.ContainsKey(lightEvent.basicBeatmapEventType))
                        eventTypeCounts[lightEvent.basicBeatmapEventType]++;
                    else
                        eventTypeCounts[lightEvent.basicBeatmapEventType] = 1;
                }
            }

            bool needsBACK = !eventTypeCounts.ContainsKey(BasicBeatmapEventType.Event0) ||
                             eventTypeCounts[BasicBeatmapEventType.Event0] == 0;
            bool needsRING = !eventTypeCounts.ContainsKey(BasicBeatmapEventType.Event1) ||
                             eventTypeCounts[BasicBeatmapEventType.Event1] == 0;
            bool needsLEFT = !eventTypeCounts.ContainsKey(BasicBeatmapEventType.Event2) ||
                             eventTypeCounts[BasicBeatmapEventType.Event2] == 0; //rotating laser
            bool needsRIGHT = !eventTypeCounts.ContainsKey(BasicBeatmapEventType.Event3) ||
                              eventTypeCounts[BasicBeatmapEventType.Event3] == 0; //rotating laser
            bool needsCENTER = !eventTypeCounts.ContainsKey(BasicBeatmapEventType.Event4) ||
                               eventTypeCounts[BasicBeatmapEventType.Event4] == 0;
            bool needsLEFTSPEED = !eventTypeCounts.ContainsKey(BasicBeatmapEventType.Event12) ||
                                  eventTypeCounts[BasicBeatmapEventType.Event12] == 0; // Laser rotation speed
            bool needsRIGHTSPEED = !eventTypeCounts.ContainsKey(BasicBeatmapEventType.Event13) ||
                                   eventTypeCounts[BasicBeatmapEventType.Event13] == 0; // Laser rotation speed
            bool needsRINGSPIN = !eventTypeCounts.ContainsKey(BasicBeatmapEventType.Event8) ||
                                 eventTypeCounts[BasicBeatmapEventType.Event8] == 0;
            bool needsRINGZOOM = !eventTypeCounts.ContainsKey(BasicBeatmapEventType.Event9) ||
                                 eventTypeCounts[BasicBeatmapEventType.Event9] == 0;

            Plugin.Log.Info($"[AutoLightMapper] - this map needsBACK: {needsBACK}, needsRING: {needsRING}, needsLEFT: {needsLEFT}, needsRIGHT: {needsRIGHT}, needsCENTER: {needsCENTER}, needsLEFTSPEED: {needsLEFTSPEED}, needsRIGHTSPEED: {needsRIGHTSPEED}, needsRINGSPIN: {needsRINGSPIN}, needsRINGZOOM: {needsRINGZOOM}");

            bool[] allLightTypes = { needsBACK, needsRING, needsLEFT, needsRIGHT, needsCENTER, needsLEFTSPEED, needsRIGHTSPEED, needsRINGSPIN, needsRINGZOOM };
            bool[] threeSixtyTypes = { needsBACK, needsRING, needsLEFT, needsRIGHT, needsCENTER, needsLEFTSPEED, needsRIGHTSPEED };

            //TheSecondEnvironment will have 5 events even if not added by user (back,ring,left,right, center) when using OVERRIDE. Not present otherwise. 
            //Time=0.000, Type=BACK, Value=BLUE_ON, Time=0.000, Type=RING, Value=BLUE_ON, Time=0.000, Type=LEFT, Value=BLUE_ON, Type=RIGHT, Value=BLUE_ON, Type=CENTER, Value=BLUE_ON
            //EDMEnvironment will have 2 events even if not added by user  when using OVERRIDE. Not present otherwise.

            /*
            // 1
            if (envName == "TheSecondEnvironment" && bools.Count(b => !b) == 5)
            {
                needsBACK = needsRING = needsLEFT = needsRIGHT = needsCENTER = true;
                bools[0] = bools[1] = bools[2] = bools[3] = bools[4] = true;
            }
            */

            int existingLightTypes = allLightTypes.Count(b => !b);
            Plugin.Log.Info($"[AutoLightMapper] - Existing Light Types: {existingLightTypes} count");

            //Standard maps: If 2 or more light types already exist → skip light generation
            if (existingLightTypes > 2 && //counts how many false values exist in bools. It effectively counts how many light events already exist in the map.
                TransitionPatcher.SelectedSerializedName != "Generated360Degree" &&
                TransitionPatcher.SelectedSerializedName != "Generated90Degree" &&
                TransitionPatcher.SelectedSerializedName != "360Degree" &&
                TransitionPatcher.SelectedSerializedName != "90Degree")
            {
                Plugin.Log.Info($"[AutoLightMapper] not used since there are 3 or more light events types programmed already for standard map.");
                return;
            }

            int existing360LightTypes = threeSixtyTypes.Count(b => !b);

            //360: If all 7 core types are already present, it skips generation
            if (TransitionPatcher.SelectedSerializedName == "Generated360Degree" ||
                TransitionPatcher.SelectedSerializedName == "Generated90Degree" ||
                TransitionPatcher.SelectedSerializedName == "360Degree" ||
                TransitionPatcher.SelectedSerializedName == "90Degree")
            {
                if (existing360LightTypes < 7)
                {
                    needsRINGSPIN = false;
                    needsRINGZOOM = false;
                }
                else
                {
                    Plugin.Log.Info($"[AutoLightMapper] not used since all seven 360 light event types are already programmed.");
                    return;
                }
            }

            // Generate new events if needed.
            List<EBasicEventData> v2lights = CreateLight(originalLightEvents,
                eData, needsBACK, needsRING, needsLEFT, needsRIGHT, needsCENTER, needsLEFTSPEED,
                needsRIGHTSPEED, needsRINGSPIN, needsRINGZOOM);

            /*
            // 2
            //  "Light Parser" for  "Cross Environment Compatible Lightshows" for ALL environments announced in Beat Games Dev Blog 12/2024. So don't need this hopefully.
            if (!IsV2Environment(envName) || envName.Contains("Second"))
            {
                // Instead of inserting the v2 events, convert them to GLS events.
                Plugin.Log.Info($"[AutoLightMapper] Converting v2 events into GLS events for environment '{envName}'");
                GLSConverter.ConvertToGLSEvents(v2lights, envName);
                return;
            }
            */
            /*
            // Important change!!!!! Test this. with this removed, now only new lights from missing types will be added and original lights will still exist.
            foreach (var e in originalLightEvents)
            {
                data.allBeatmapDataItems.Remove(e);
            }
            */
            
            foreach (EBasicEventData light in v2lights)
            {
                //Plugin.Log.Info($"[AutoLightMapper] Inserting event: Time={light.time:F3}, Type={(EventType)light.basicBeatmapEventType}, Value={(EventValue)light.value}, Brightness={light.floatValue:F2}");
                //data.InsertBeatmapEventDataInOrder(light);
                eData.BasicEvents.Add(light);
            }

            //return data;
        }

        // A helper method to check if the environment is v2-based.
        public static bool IsV2Environment(string environmentName)
        {
            // List of keywords for v2 environments
            string[] v2Keywords = new string[] {
                "Default", // had to put this since if you override a non-first env using the "first" it will be listed as "DefaultEnvironment" and not "TheFirstEnvironment". but i think this may lead some v3 env being mistaken as supported
                "The First", "TheFirst", "Origins", "Triangle","Nice", "BigMirror", "Big Mirror", "Dragons","KDA", "Monstercat","CrabRave", "Crab Rave",
                "Panic", "Rocket", "GreenDay", "Green Day", "Timbaland", "FitBeat", "Fit Beat", "LinkinPark", "Linkin Park", "BTS", "Kaleidoscope",
                "Interscope", "Skrillex", "Billie", "Halloween", "Gaga", "GlassDesert",  "Glass Desert"
            };
            //"The First", "Triangle", "Nice", "Big Mirror", "K/DA", "Monstercat", "Crab Rave","Imagine Dragons", "Origins", "Panic! at the Disco", "Rocket League", "Green Day","Green Day Grenade", "Timbaland", "FitBeat", "Linkin Park", "BTS", "Kaleidoscope","Interscope", "Skrillex", "Billie Eilish", "Spooky", "Lady Gaga", "Glass Desert"
            
            return v2Keywords.Any(keyword => environmentName.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        // CreateLight generates new light events (standard, special, rotation speed, and ambient effects).
        public static List<EBasicEventData> CreateLight(List<EBasicEventData> originalLightEvents, EditableCBD eData, bool needsBACK, bool needsRING, bool needsLEFT, bool needsRIGHT, bool needsCENTER, bool needsLEFTSPEED, bool needsRIGHTSPEED, bool needsRINGSPIN, bool needsRINGZOOM)
        {
            List<ENoteData> notes = eData.ColorNotes.ToList();
            List<ESliderData> sliders = eData.Arcs.ToList();

            // Initialize dictionary to track which special events exist
            Dictionary<BasicBeatmapEventType, bool> originalSpecialEvents = new Dictionary<BasicBeatmapEventType, bool>
            {
                { BasicBeatmapEventType.Event6,  false },
                { BasicBeatmapEventType.Event7,  false },
                { BasicBeatmapEventType.Event10, false },
                { BasicBeatmapEventType.Event11, false },
                { BasicBeatmapEventType.Event16, false },
                { BasicBeatmapEventType.Event17, false },
                { BasicBeatmapEventType.Event18, false },
                { BasicBeatmapEventType.Event19, false }
            };

            // Check which special events exist in the original beatmap
            foreach (var e in originalLightEvents)
            {
                if (originalSpecialEvents.ContainsKey(e.basicBeatmapEventType))
                {
                    originalSpecialEvents[e.basicBeatmapEventType] = true;
                }
            }
            /*
            foreach (var s in originalSpecialEvents)
            {
                Plugin.Log.Info($"[AutoLightMapper] Original special event: Type={s.Key}, Exists={s.Value}");
            }
            */
            Dictionary<EventType, EventValue> lastEventColors = new Dictionary<EventType, EventValue>();

            LightEventType lightStyle = (LightEventType)Config.Instance.LightStyle;

            float brightnessMultiplier = Config.Instance.BrightnessMultiplier;

            float frequencyMultiplier = Config.Instance.LightFrequencyMultiplier;

            // --- NEW: Determine allowed special events based on environment ---
            string environmentName = TransitionPatcher.EnvironmentName != null ? TransitionPatcher.EnvironmentName : "Default";
            //Plugin.Log.Info($"[AutoLightMapper] Songname: {HarmonyPatches.SongName} --- Current environment name: {environmentName} -------------------");
            Plugin.Log.Info($" -------------------");

            List<EventType> allowedSpecialEventTypes = new List<EventType>();
            if (environmentName.IndexOf("Skrillex", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                allowedSpecialEventTypes.Add(EventType.SPECIAL_6);
                allowedSpecialEventTypes.Add(EventType.SPECIAL_7);
            }
            else if (environmentName.IndexOf("Billie", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                allowedSpecialEventTypes.Add(EventType.SPECIAL_6);
                allowedSpecialEventTypes.Add(EventType.SPECIAL_7);
                allowedSpecialEventTypes.Add(EventType.SPECIAL_10);
                allowedSpecialEventTypes.Add(EventType.SPECIAL_11);
            }
            else if (environmentName.IndexOf("Gaga", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                allowedSpecialEventTypes.Add(EventType.SPECIAL_6);
                allowedSpecialEventTypes.Add(EventType.SPECIAL_7);
                allowedSpecialEventTypes.Add(EventType.SPECIAL_10);
                allowedSpecialEventTypes.Add(EventType.SPECIAL_11);
                allowedSpecialEventTypes.Add(EventType.SPECIAL_16);
                allowedSpecialEventTypes.Add(EventType.SPECIAL_17);
                allowedSpecialEventTypes.Add(EventType.SPECIAL_18);
                allowedSpecialEventTypes.Add(EventType.SPECIAL_19);
            }
            else
            {
                Plugin.Log.Info($"[AutoLightMapper] Environment '{environmentName}' does not support special events. No special events will be added.");
            }
            Plugin.Log.Info($"[AutoLightMapper] Allowed special event types: {string.Join(", ", allowedSpecialEventTypes)}");
            // --- End of environment check ---

            // Use a counter to track when to trigger a light event based on the multiplier
            float lightEventMultiplierCounter;

            // Bunch of var to keep timing in check
            float last = 0f;

            ///<summary>
            /// This array is central to your timing logic — it's a sliding window used to track the most recent note timings in order to:
            /// Detect note gaps(short or long)
            /// Calculate rotation speeds based on timing
            /// Determine if two notes happened at the same time(e.g., doubles)
            /// Position events like fades, flashes, and special events
            /// </summary>
            float[] time = new float[4];
            int[] light = new int[3];
            float offset = notes[0].time;
            float firstNote = 0;
            bool doubleOn = false; // If double notes lights are on

            // For laser speed calculation.
            int currentSpeed = 3;
            float lastSpeed = 0;

            // To not light up Double twice
            float nextDouble = 0;

            // Slider-related variables.
            bool firstSlider = false;
            float nextSlider = 0;
            List<int> sliderLight = new List<int>() { 4, 3, 2, 1, 0 }; // Order for slider lights.
            int sliderIndex = 0;
            float sliderNoteCount = 0;
            bool wasSlider = false;

            // Pattern for cycling through standard events.
            List<int> pattern = new List<int>(Enumerable.Range(0, 7)); //event0-9 (excpet5 boost)
            int patternIndex = 0;
            int patternCount = 20;

            // NEW: Variables for generating special events.
            float lastSpecialTriggerTime = -0.2f;
            int specialEventIndex = 0;

            // List to hold all new light events.
            List<EBasicEventData> lightEvents = new List<EBasicEventData>();

            // For slider timing detection.
            List<ESliderData> sliderTiming = new List<ESliderData>();
            //notes = notes.OrderBy(o => o.time).ToList();

            void ResetTimer()
            {
                firstNote = notes[0].time;
                offset = firstNote;
                for (int i = 0; i < 2; i++) 
                {
                    time[i] = 0.0f;
                    light[i] = 0;
                }
                time[2] = 0.0f;
                time[3] = 0.0f;
            }

            ResetTimer();
            bool found = false;
            ResetTimer();

            for (int i = 1; i < sliders.Count; i++)
            {
                if (sliders[i].time - sliders[i - 1].time <= 0.125 && sliders[i].time - sliders[i - 1].time > 0 &&
                    (sliders[i].cutDirection == sliders[i - 1].cutDirection || (int)sliders[i].cutDirection == 8 || (int)sliders[i - 1].cutDirection == 8))
                {
                    sliderTiming.Add(sliders[i - 1]);
                    found = true;
                }
                else if (found)
                {
                    sliderTiming.Add(sliders[i - 1]);
                    found = false;
                }
            }

            #region Foreach Note Process specific light using time - OFF events

            lightEventMultiplierCounter = 0.0f;
            foreach (ENoteData note in notes)
            {
                float now = note.time;
                time[0] = now;
                // Accumulate based on frequency multiplier.
                lightEventMultiplierCounter += frequencyMultiplier;

                if (lightEventMultiplierCounter >= 1.0f)
                {
                    lightEventMultiplierCounter -= 1.0f;

                    if (!Light.NerfStrobes && doubleOn && now != last)
                    {
                        if (now - last >= 1)
                        {
                            if (needsBACK)
                            {
                                float t = now - (now - last) / 2;
                                //Plugin.Log.Info($"[AutoLightMapper] Generated OFF event (BACK) at time {t:F3}");
                                lightEvents.Add(EBasicEventData.Create(t,EventType.BACK, (int)EventValue.OFF));
                            }
                            if (needsRING)
                            {
                                float t = now - (now - last) / 2;
                                //Plugin.Log.Info($"[AutoLightMapper] Generated OFF event (RING) at time {t:F3}");
                                lightEvents.Add(EBasicEventData.Create(t, EventType.RING, EventValue.OFF));
                            }
                            if (needsLEFT)
                            {
                                float t = now - (now - last) / 2;
                                //Plugin.Log.Info($"[AutoLightMapper] Generated OFF event (LEFT) at time {t:F3}");
                                lightEvents.Add(EBasicEventData.Create(t, EventType.LEFT, EventValue.OFF));
                            }
                            if (needsRIGHT)
                            {
                                float t = now - (now - last) / 2;
                                //Plugin.Log.Info($"[AutoLightMapper] Generated OFF event (RIGHT) at time {t:F3}");
                                lightEvents.Add(EBasicEventData.Create(t, EventType.RIGHT, EventValue.OFF));
                            }
                            if (needsCENTER)
                            {
                                float t = now - (now - last) / 2;
                                //Plugin.Log.Info($"[AutoLightMapper] Generated OFF event (CENTER) at time {t:F3}");
                                lightEvents.Add(EBasicEventData.Create(t, EventType.CENTER, EventValue.OFF));
                            }
                            if (needsRINGSPIN)
                            {
                                float t = now - (now - last) / 2;
                                //Plugin.Log.Info($"[AutoLightMapper] Generated OFF event (RING_SPIN) at time {t:F3}");
                                lightEvents.Add(EBasicEventData.Create(t, EventType.RING_SPIN, EventValue.OFF));
                            }
                            if (needsRINGZOOM)
                            {
                                float t = now - (now - last) / 2;
                                //Plugin.Log.Info($"[AutoLightMapper] Generated OFF event (RING_ZOOM) at time {t:F3}");
                                lightEvents.Add(EBasicEventData.Create(t, EventType.RING_ZOOM, EventValue.OFF));
                            }
                            //Plugin.Log.Info($"[AutoLightMapper] Off events (group 1) added at time {now:F3}");
                        }
                        else
                        {
                            if (needsBACK)
                            {
                                //Plugin.Log.Info($"[AutoLightMapper] Generated OFF event (BACK) at time {now:F3}");
                                lightEvents.Add(EBasicEventData.Create(now, EventType.BACK, EventValue.OFF));
                            }
                            if (needsRING)
                            {
                                //Plugin.Log.Info($"[AutoLightMapper] Generated OFF event (RING) at time {now:F3}");
                                lightEvents.Add(EBasicEventData.Create(now, EventType.RING, EventValue.OFF));
                            }
                            if (needsLEFT)
                            {
                                //Plugin.Log.Info($"[AutoLightMapper] Generated OFF event (LEFT) at time {now:F3}");
                                lightEvents.Add(EBasicEventData.Create(now, EventType.LEFT, EventValue.OFF));
                            }
                            if (needsRIGHT)
                            {
                                //Plugin.Log.Info($"[AutoLightMapper] Generated OFF event (RIGHT) at time {now:F3}");
                                lightEvents.Add(EBasicEventData.Create(now, EventType.RIGHT, EventValue.OFF));
                            }
                            if (needsCENTER)
                            {
                                //Plugin.Log.Info($"[AutoLightMapper] Generated OFF event (CENTER) at time {now:F3}");
                                lightEvents.Add(EBasicEventData.Create(now, EventType.CENTER, EventValue.OFF));
                            }
                            if (needsRINGSPIN)
                            {
                                //Plugin.Log.Info($"[AutoLightMapper] Generated OFF event (RING_SPIN) at time {now:F3}");
                                lightEvents.Add( EBasicEventData.Create(now, EventType.RING_SPIN, EventValue.OFF));
                            }
                            if (needsRINGZOOM)
                            {
                                //Plugin.Log.Info($"[AutoLightMapper] Generated OFF event (RING_ZOOM) at time {now:F3}");
                                lightEvents.Add(EBasicEventData.Create(now, EventType.RING_ZOOM, EventValue.OFF));
                            }
                            //Plugin.Log.Info($"[AutoLightMapper] Off events (group 2) added at time {now:F3}");
                        }
                        doubleOn = false;
                    }

                    if ((now == time[1] || (now - time[1] <= 0.02 && time[1] != time[2])) && (time[1] != 0.0D && now != last) &&
                        !sliderTiming.Exists(e => e.time == now))
                    {
                        if (needsBACK)
                        {
                            (EventValue color, float floatValue) = FindColor(notes.First().time, time[0], lightStyle, true);
                            //Plugin.Log.Info($"[AutoLightMapper] Generated BACK event at time {now:F3} with color {color} and brightness {floatValue * brightnessMultiplier:F2}");
                            lightEvents.Add(EBasicEventData.Create(now, EventType.BACK, color, floatValue * brightnessMultiplier));
                        }
                        if (needsRING)
                        {
                            (EventValue color, float floatValue) = FindColor(notes.First().time, time[0], lightStyle, true);
                            //Plugin.Log.Info($"[AutoLightMapper] Generated RING event at time {now:F3} with color {color} and brightness {floatValue * brightnessMultiplier:F2}");
                            lightEvents.Add(EBasicEventData.Create(now, EventType.RING, color, floatValue * brightnessMultiplier));
                        }
                        if (needsLEFT || needsRIGHT)
                        {
                            (EventValue color, float floatValue) = FindColor(notes.First().time, time[0], lightStyle);
                            if (needsLEFT)
                            {
                                //Plugin.Log.Info($"[AutoLightMapper] Generated LEFT event at time {now:F3} with color {color} and brightness {floatValue * brightnessMultiplier:F2}");
                                lightEvents.Add(EBasicEventData.Create(now, EventType.LEFT, color, floatValue * brightnessMultiplier));
                            }
                            if (needsRIGHT)
                            {
                                //Plugin.Log.Info($"[AutoLightMapper] Generated RIGHT event at time {now:F3} with color {color} and brightness {floatValue * brightnessMultiplier:F2}");
                                lightEvents.Add(EBasicEventData.Create(now, EventType.RIGHT, color, floatValue * brightnessMultiplier));
                            }
                        }
                        if (needsCENTER)
                        {
                            (EventValue color, float floatValue) = FindColor(notes.First().time, time[0], lightStyle, true);
                            //Plugin.Log.Info($"[AutoLightMapper] Generated CENTER event at time {now:F3} with color {color} and brightness {floatValue * brightnessMultiplier:F2}");
                            lightEvents.Add(EBasicEventData.Create(now, EventType.CENTER, color, floatValue * brightnessMultiplier));
                        }
                        if (needsRINGSPIN)
                        {
                            //Plugin.Log.Info($"[AutoLightMapper] Generated RING_SPIN event at time {now:F3}");
                            lightEvents.Add(EBasicEventData.Create(now, EventType.RING_SPIN, EventValue.OFF));
                        }
                        if (needsRINGZOOM)
                        {
                            (EventValue color, float floatValue) = FindColor(notes.First().time, time[0], lightStyle, true);
                            //Plugin.Log.Info($"[AutoLightMapper] Generated RING_ZOOM event at time {now:F3}");
                            lightEvents.Add(EBasicEventData.Create(now, EventType.RING_ZOOM, EventValue.OFF));
                        }
                        doubleOn = true;
                        last = now;
                    }

                    for (int i = 3; i > 0; i--)
                    {
                        time[i] = time[i - 1];
                    }
                }
            }
            #endregion

            nextSlider = 0f;
            #region Convert quick light color swap
            if (Light.NerfStrobes)
            {
                float lastTimeTop = 100;
                float lastTimeRing = 100;
                float lastTimeCenter = 100;
                float lastTimeLeft = 100;
                float lastTimeRight = 100;

                foreach (EBasicEventData x in lightEvents)
                {
                    if (x.eventType == EventType.BACK)
                    {
                        if (x.time - lastTimeTop <= 0.5)
                        {
                            x.eventValue = Light.Swap(x.eventValue);
                        }
                        lastTimeTop = x.time;
                    }
                    else if (x.eventType == EventType.RING)
                    {
                        if (x.time - lastTimeRing <= 0.5)
                        {
                            x.eventValue = Light.Swap(x.eventValue);
                        }
                        lastTimeRing = x.time;
                    }
                    else if (x.eventType == EventType.CENTER)
                    {
                        if (x.time - lastTimeCenter <= 0.5)
                        {
                            x.eventValue = Light.Swap(x.eventValue);
                        }
                        lastTimeCenter = x.time;
                    }
                    else if (x.eventType == EventType.LEFT)
                    {
                        if (x.time - lastTimeLeft <= 0.5)
                        {
                            x.eventValue = Light.Swap(x.eventValue);
                        }
                        lastTimeLeft = x.time;
                    }
                    else if (x.eventType == EventType.RIGHT)
                    {
                        if (x.time - lastTimeRight <= 0.5)
                        {
                            x.eventValue = Light.Swap(x.eventValue);
                        }
                        lastTimeRight = x.time;
                    }
                }
            }
            #endregion

            ResetTimer();
            #region Process all notes using time

            // NEW: Laser rotation speed variables.
            lightEventMultiplierCounter = 0.0f;
            int lastLeftSpeed = -1;
            int lastRightSpeed = -1;
            EventValue lastLeftColor = EventValue.OFF;
            EventValue lastRightColor = EventValue.OFF;
            EventValue lastBackColor = EventValue.OFF;
            EventValue lastRingColor = EventValue.OFF;
            EventValue lastCenterColor = EventValue.OFF;

            float lastSpinTriggerTime = -0.2f;
            float lastZoomTriggerTime = -0.2f;

            bool useBlueFade = true; // use to alernate between blue and red fade for time gaps
            int closeNoteCounter = 0;

            bool currentlyStrobing = false;
            bool useBackStrobe = false;
            float burstEndTime = -1f;

            bool IsSuppressedByStrobe(EventType type)
            {
                if (!currentlyStrobing) return false;
                return useBackStrobe ? type == EventType.BACK : (type == EventType.LEFT || type == EventType.RIGHT);
            }


            foreach (ENoteData note in notes)
            {
                if (note.time > burstEndTime) currentlyStrobing = false;

                int index = notes.FindIndex(n => n.time == note.time);
                bool closeNotes = index > 0 && (notes[index].time - notes[index - 1].time) < 0.2;
                bool isSlider = sliders.Any(s => s.time == note.time);

                // New: Occasional strobe burst for a dense note cluster.
                if (closeNotes)
                {
                    closeNoteCounter++;

                    #region Strobe effect

                    if (closeNoteCounter % 40 == 0) // Every 40th dense note triggers a strobe
                    {
                        float burstDuration = UnityEngine.Random.Range(0.2f, 0.7f);
                        burstEndTime = note.time + burstDuration;

                        currentlyStrobing = true;

                        useBackStrobe = (closeNoteCounter / 40) % 3 == 0; // 33% chance: BACK, else LEFT + RIGHT

                        //Plugin.Log.Info($"[AutoLightMapper] Generated strobe burst at time {note.time:F3} with duration {burstDuration:F3} seconds, useBack: {useBackStrobe}");

                        // Use time between this and previous note as the interval
                        float strobeInterval = 0.125f; // fallback interval
                        if (index > 0)
                            strobeInterval = Mathf.Max(0.05f, notes[index].time - notes[index - 1].time); // Clamp to avoid super small

                        int strobeStep = 0;
                        float strobeTime = note.time;
                        while (strobeTime < note.time + burstDuration)
                        {
                            EventValue strobeColor = (strobeStep % 2 == 0)
                                ? (UnityEngine.Random.value < 0.5f ? EventValue.FLASH : EventValue.ON)
                                : EventValue.OFF;

                            if (useBackStrobe)
                            {
                                //Plugin.Log.Info($"[AutoLightMapper] Generated strobe event (BACK) at time {strobeTime:F3} with color {strobeColor}");
                                lightEvents.Add(EBasicEventData.Create(strobeTime, EventType.BACK, strobeColor));
                            }
                            else
                            {
                                //Plugin.Log.Info($"[AutoLightMapper] Generated strobe event (LEFT and RIGHT) at time {strobeTime:F3} with color {strobeColor}");
                                lightEvents.Add(EBasicEventData.Create(strobeTime, EventType.LEFT, strobeColor));
                                lightEvents.Add(EBasicEventData.Create(strobeTime, EventType.RIGHT, strobeColor));
                            }

                            strobeStep++;
                            strobeTime += strobeInterval;
                        }

                        // Skip normal processing for this note to avoid doubling up.
                        continue;
                    }
                }

                #endregion


                if ((closeNotes || isSlider) && (note.time - lastSpinTriggerTime >= 0.1))
                {
                    if (needsRINGSPIN)
                    {
                        //Plugin.Log.Info($"[AutoLightMapper] Generated RING_SPIN event at time {note.time:F3} OFF");
                        lightEvents.Add(EBasicEventData.Create(note.time, EventType.RING_SPIN, EventValue.OFF));
                        lastSpinTriggerTime = note.time;
                    }
                }

                if ((closeNotes || isSlider) && (note.time - lastZoomTriggerTime >= 0.2))
                {
                    if (needsRINGZOOM && (index % 2 == 0 || index % 3 == 0))
                    {
                       // Plugin.Log.Info($"[AutoLightMapper] Generated RING_ZOOM event at time {note.time:F3} OFF");
                        lightEvents.Add(EBasicEventData.Create(note.time, EventType.RING_ZOOM, EventValue.OFF));
                        lastZoomTriggerTime = note.time;
                    }
                }

                // Special events: Only add if allowed by the environment and if don't exist already.
                // Rotate through missing special events rather than adding just the first one
                List<EventType> missingSpecialEvents = originalSpecialEvents
                    .Where(e => !e.Value && allowedSpecialEventTypes.Contains((EventType)e.Key))
                    .Select(e => (EventType)e.Key)
                    .ToList();

                if (missingSpecialEvents.Count > 0 && (closeNotes || isSlider) && (note.time - lastSpecialTriggerTime >= 0.15f))
                {
                    (EventValue specColor, float specBrightness) = FindColor(notes.First().time, note.time, lightStyle, true);
                    EventType specialEventType = missingSpecialEvents[specialEventIndex];  // Rotate through missing ones

                    //Plugin.Log.Info($"[AutoLightMapper] Generated SPECIAL event: Type={specialEventType}, Time={note.time:F3}, Value={specColor}, Brightness={specBrightness * brightnessMultiplier:F2}");

                    lightEvents.Add(EBasicEventData.Create(note.time, specialEventType, specColor, specBrightness * brightnessMultiplier));

                    // Move to next special event in the list (cycling)
                    specialEventIndex = (specialEventIndex + 1) % missingSpecialEvents.Count;
                    lastSpecialTriggerTime = note.time;
                }



                for (int i = 3; i > 0; i--)
                {
                    time[i] = time[i - 1];
                }
                time[0] = note.time;
                lightEventMultiplierCounter += frequencyMultiplier;
                if (lightEventMultiplierCounter >= 1.0f)
                {
                    lightEventMultiplierCounter -= 1.0f;
                    if (wasSlider)
                    {
                        if (sliderNoteCount != 0)
                        {
                            sliderNoteCount--;
                            continue;
                        }
                        else
                        {
                            wasSlider = false;
                        }
                    }
                    if (firstSlider)
                    {
                        firstSlider = false;
                        continue;
                    }
                    if (time[0] >= nextDouble)
                    {
                        for (int i = notes.FindIndex(n => n == note); i < notes.Count - 1; i++)
                        {
                            if (i != 0)
                            {
                                if (notes[i].time == notes[i - 1].time)
                                {
                                    nextDouble = notes[i].time;
                                    break;
                                }
                            }
                        }
                    }
                    if (time[0] >= nextSlider)
                    {
                        sliderNoteCount = 0;
                        for (int i = notes.FindIndex(n => n == note); i < notes.Count - 1; i++)
                        {
                            if (i != 0 && i < notes.Count)
                            {
                                if (notes[i].time - notes[i - 1].time <= 0.125 && notes[i].time - notes[i - 1].time > 0 &&
                                    (notes[i].cutDirection == notes[i - 1].cutDirection || (int)notes[i].cutDirection == 8))
                                {
                                    if (sliderNoteCount == 0)
                                    {
                                        nextSlider = notes[i - 1].time;
                                    }
                                    sliderNoteCount++;
                                }
                                else if (sliderNoteCount != 0)
                                {
                                    break;
                                }
                            }
                        }
                    }
                    if (nextSlider == note.time)
                    {
                        // Take a light between neon, side or backlight and strobes it via On/Flash
                        if (sliderIndex == -1)
                        {
                            sliderIndex = 4;
                        }
                        EventType et = EventType.CENTER;

                        if (sliderLight[sliderIndex] == 4)
                            et = EventType.CENTER;
                        else if (sliderLight[sliderIndex] == 1)
                            et = EventType.RING;
                        else if (sliderLight[sliderIndex] == 0)
                            et = EventType.BACK;
                        else if (sliderLight[sliderIndex] == 2)
                            et = EventType.RING_SPIN;
                        else if (sliderLight[sliderIndex] == 3)
                            et = EventType.RING_ZOOM;

                        // Place light
                        (EventValue color, float floatValue) = FindColor(notes.First().time, time[0], lightStyle);

                        //Plugin.Log.Info($"[AutoLightMapper] Generated SLIDER event ({et}) at time {time[0]:F3} with color {color} and brightness {floatValue * brightnessMultiplier:F2}");

                        if ((needsCENTER && et == EventType.CENTER) || (needsRING && et == EventType.RING) || (needsBACK && et == EventType.BACK))
                        {
                            if (!IsSuppressedByStrobe(et))
                            {
                                //Plugin.Log.Info($"[AutoLightMapper] Generated SLIDER event ({et}) at time {time[0]:F3} with color {color} and brightness {floatValue * brightnessMultiplier:F2}");
                                lightEvents.Add(EBasicEventData.Create(time[0], et, (color - 2), floatValue * brightnessMultiplier));
                                lightEvents.Add(EBasicEventData.Create(time[0] + 0.125f, et, (color - 1), floatValue * brightnessMultiplier));
                                lightEvents.Add(EBasicEventData.Create(time[0] + 0.25f, et, (color - 2), floatValue * brightnessMultiplier));
                                lightEvents.Add(EBasicEventData.Create(time[0] + 0.375f, et, (color - 1), floatValue * brightnessMultiplier));
                                lightEvents.Add(EBasicEventData.Create(time[0] + 0.5f, et, 0));
                            }
                        }
                        if ((needsRINGSPIN && et == EventType.RING_SPIN) || (needsRINGZOOM && et == EventType.RING_ZOOM))
                        {
                            lightEvents.Add(EBasicEventData.Create(time[0], et, EventValue.OFF));
                            lightEvents.Add(EBasicEventData.Create(time[0] + 0.125f, et, EventValue.OFF));
                            lightEvents.Add(EBasicEventData.Create(time[0] + 0.25f, et, EventValue.OFF));
                            lightEvents.Add(EBasicEventData.Create(time[0] + 0.375f, et, EventValue.OFF));
                            lightEvents.Add(EBasicEventData.Create(time[0] + 0.5f, et, EventValue.OFF));
                        }
                        sliderIndex--;
                        wasSlider = true;
                    }
                    else if (time[0] != nextDouble)
                    {
                        if (time[1] - time[2] >= lastSpeed + 0.02 || time[1] - time[2] <= lastSpeed - 0.02 || patternCount == 20)
                        {
                            // New pattern
                            int old = patternIndex != 0 ? pattern[patternIndex - 1] : pattern[4];
                            do
                            {
                                pattern.Shuffle();
                            } while (pattern[0] == old);
                            patternIndex = 0;
                            patternCount = 0;
                        }
                        // Place the next light
                        if ((needsBACK && (EventType)pattern[patternIndex] == EventType.BACK) ||
                            (needsRING && (EventType)pattern[patternIndex] == EventType.RING) ||
                            (needsLEFT && (EventType)pattern[patternIndex] == EventType.LEFT) ||
                            (needsRIGHT && (EventType)pattern[patternIndex] == EventType.RIGHT) ||
                            (needsCENTER && (EventType)pattern[patternIndex] == EventType.CENTER) ||
                            (needsRINGSPIN && (EventType)pattern[patternIndex] == EventType.RING_SPIN) ||
                            (needsRINGZOOM && (EventType)pattern[patternIndex] == EventType.RING_ZOOM))
                        {
                            (EventValue color, float floatValue) = FindColor(notes.First().time, time[0], lightStyle);
                            //Plugin.Log.Info($"[AutoLightMapper] Generated PATTERN event ({(EventType)pattern[patternIndex]}) at time {time[0]:F3} with color {color} and brightness {floatValue * brightnessMultiplier:F2}.");

                            if (!IsSuppressedByStrobe((EventType)pattern[patternIndex]))
                            {
                                //Plugin.Log.Info($"[AutoLightMapper] Generated PATTERN event ({(EventType)pattern[patternIndex]}) at time {time[0]:F3} with color {color} and brightness {floatValue * brightnessMultiplier:F2}.");
                                lightEvents.Add(EBasicEventData.Create(time[0], (EventType)pattern[patternIndex], color, floatValue * brightnessMultiplier));
                            }

                            if (lightStyle == LightEventType.FLASH || lightStyle == LightEventType.TRANSITION)
                            {
                                if ((EventType)pattern[patternIndex] == EventType.LEFT)
                                    lastLeftColor = color;
                                else if ((EventType)pattern[patternIndex] == EventType.RIGHT)
                                    lastRightColor = color;
                                else if ((EventType)pattern[patternIndex] == EventType.BACK)
                                    lastBackColor = color;
                                else if ((EventType)pattern[patternIndex] == EventType.RING)
                                    lastRingColor = color;
                                else if ((EventType)pattern[patternIndex] == EventType.CENTER)
                                    lastCenterColor = color;
                            }
                        }
                        if (notes[notes.Count - 1].time != note.time)
                        {
                            if (notes[notes.FindIndex(n => n == note) + 1].time == nextDouble)
                            {
                                if (notes[notes.FindIndex(n => n == note) + 1].time - time[0] <= 2)
                                {
                                    float value = (notes[notes.FindIndex(n => n == note) + 1].time - notes[notes.FindIndex(n => n == note)].time) / 2;
                                    if ((needsBACK && (EventType)pattern[patternIndex] == EventType.BACK) || (needsRING && (EventType)pattern[patternIndex] == EventType.RING) ||
                                        (needsLEFT && (EventType)pattern[patternIndex] == EventType.LEFT) || (needsRIGHT && (EventType)pattern[patternIndex] == EventType.RIGHT) ||
                                        (needsCENTER && (EventType)pattern[patternIndex] == EventType.CENTER))
                                    {
                                        if (!IsSuppressedByStrobe((EventType)pattern[patternIndex]))
                                        {
                                            if (lightStyle == LightEventType.FLASH || lightStyle == LightEventType.TRANSITION)
                                            {
                                                if ((EventType)pattern[patternIndex] == EventType.LEFT)
                                                {
                                                    //Plugin.Log.Info($"[AutoLightMapper] Generated FADE (LEFT) event at time {notes[notes.FindIndex(n => n == note)].time + value:F3}");
                                                    lightEvents.Add(EBasicEventData.Create(notes[notes.FindIndex(n => n == note)].time + value, (EventType)pattern[patternIndex], FadeEvent(lastLeftColor)));
                                                }
                                                else if ((EventType)pattern[patternIndex] == EventType.RIGHT)
                                                {
                                                    //Plugin.Log.Info($"[AutoLightMapper] Generated FADE (RIGHT) event at time {notes[notes.FindIndex(n => n == note)].time + value:F3}");
                                                    lightEvents.Add(EBasicEventData.Create(notes[notes.FindIndex(n => n == note)].time + value, (EventType)pattern[patternIndex], FadeEvent(lastRightColor)));
                                                }
                                                else if ((EventType)pattern[patternIndex] == EventType.BACK)
                                                {
                                                    //Plugin.Log.Info($"[AutoLightMapper] Generated FADE (BACK) event at time {notes[notes.FindIndex(n => n == note)].time + value:F3}");
                                                    lightEvents.Add(EBasicEventData.Create(notes[notes.FindIndex(n => n == note)].time + value, (EventType)pattern[patternIndex], FadeEvent(lastBackColor)));
                                                }
                                                else if ((EventType)pattern[patternIndex] == EventType.RING)
                                                {
                                                    //Plugin.Log.Info($"[AutoLightMapper] Generated FADE (RING) event at time {notes[notes.FindIndex(n => n == note)].time + value:F3}");
                                                    lightEvents.Add(EBasicEventData.Create(notes[notes.FindIndex(n => n == note)].time + value, (EventType)pattern[patternIndex], FadeEvent(lastRingColor)));
                                                }
                                                else if ((EventType)pattern[patternIndex] == EventType.CENTER)
                                                {
                                                    //Plugin.Log.Info($"[AutoLightMapper] Generated FADE (CENTER) event at time {notes[notes.FindIndex(n => n == note)].time + value:F3}");
                                                    lightEvents.Add(EBasicEventData.Create(notes[notes.FindIndex(n => n == note)].time + value, (EventType)pattern[patternIndex], FadeEvent(lastCenterColor)));
                                                }
                                            }
                                            else
                                            {
                                                //Plugin.Log.Info($"[AutoLightMapper] Generated OFF (pattern) event at time {notes[notes.FindIndex(n => n == note)].time + value:F3}");
                                                lightEvents.Add(EBasicEventData.Create(notes[notes.FindIndex(n => n == note)].time + value, (EventType)pattern[patternIndex], EventValue.OFF));
                                            }
                                        }
                                    }
                                }
                            }
                            else
                            {
                                if ((needsBACK && (EventType)pattern[patternIndex] == EventType.BACK) ||
                                    (needsRING && (EventType)pattern[patternIndex] == EventType.RING) ||
                                    (needsLEFT && (EventType)pattern[patternIndex] == EventType.LEFT) ||
                                    (needsRIGHT && (EventType)pattern[patternIndex] == EventType.RIGHT) ||
                                    (needsCENTER && (EventType)pattern[patternIndex] == EventType.CENTER) ||
                                    (needsRINGSPIN && (EventType)pattern[patternIndex] == EventType.RING_SPIN) ||
                                    (needsRINGZOOM && (EventType)pattern[patternIndex] == EventType.RING_ZOOM))
                                {
                                    if (!IsSuppressedByStrobe((EventType)pattern[patternIndex]))
                                    {
                                        if (lightStyle == LightEventType.FLASH || lightStyle == LightEventType.TRANSITION)
                                        {
                                            if ((EventType)pattern[patternIndex] == EventType.LEFT)
                                            {
                                                //Plugin.Log.Info($"[AutoLightMapper] Generated FADE (LEFT) event at time {notes[notes.FindIndex(n => n == note) + 1].time:F3}");
                                                lightEvents.Add(EBasicEventData.Create(notes[notes.FindIndex(n => n == note) + 1].time, (EventType)pattern[patternIndex], FadeEvent(lastLeftColor)));
                                            }
                                            else if ((EventType)pattern[patternIndex] == EventType.RIGHT)
                                            {
                                                //Plugin.Log.Info($"[AutoLightMapper] Generated FADE (RIGHT) event at time {notes[notes.FindIndex(n => n == note) + 1].time:F3}");
                                                lightEvents.Add(EBasicEventData.Create(notes[notes.FindIndex(n => n == note) + 1].time, (EventType)pattern[patternIndex], FadeEvent(lastRightColor)));
                                            }
                                            else if ((EventType)pattern[patternIndex] == EventType.BACK)
                                            {
                                                //Plugin.Log.Info($"[AutoLightMapper] Generated FADE (BACK) event at time {notes[notes.FindIndex(n => n == note) + 1].time:F3}");
                                                lightEvents.Add(EBasicEventData.Create(notes[notes.FindIndex(n => n == note) + 1].time, (EventType)pattern[patternIndex], FadeEvent(lastBackColor)));
                                            }
                                            else if ((EventType)pattern[patternIndex] == EventType.RING)
                                            {
                                                //Plugin.Log.Info($"[AutoLightMapper] Generated FADE (RING) event at time {notes[notes.FindIndex(n => n == note) + 1].time:F3}");
                                                lightEvents.Add(EBasicEventData.Create(notes[notes.FindIndex(n => n == note) + 1].time, (EventType)pattern[patternIndex], FadeEvent(lastRingColor)));
                                            }
                                            else if ((EventType)pattern[patternIndex] == EventType.CENTER)
                                            {
                                                //Plugin.Log.Info($"[AutoLightMapper] Generated FADE (CENTER) event at time {notes[notes.FindIndex(n => n == note) + 1].time:F3}");
                                                lightEvents.Add(EBasicEventData.Create(notes[notes.FindIndex(n => n == note) + 1].time, (EventType)pattern[patternIndex], FadeEvent(lastCenterColor)));
                                            }
                                        }
                                        else
                                        {
                                            //Plugin.Log.Info($"[AutoLightMapper] Generated OFF (pattern) event at time {notes[notes.FindIndex(n => n == note) + 1].time:F3}");
                                            lightEvents.Add(EBasicEventData.Create(notes[notes.FindIndex(n => n == note) + 1].time, (EventType)pattern[patternIndex], EventValue.OFF));
                                        }
                                    }
                                }
                            }
                        }
                        if (patternIndex < pattern.Count - 1)
                            patternIndex++;
                        else
                            patternIndex = 0;
                        patternCount++;
                        lastSpeed = time[0] - time[1];

                        // NEW: Laser rotation speed block.
                        float timeDifference = time[0] - time[1];
                        float x1 = 0.15f, x2 = 0.8f; // Interpolation bounds based on note timing.
                        float y1 = 9, y2 = 1;        // Maximum and minimum speed.
                        float calculatedSpeed;
                        if (time[1] == 0.0f)
                        {
                            calculatedSpeed = y2; // First iteration.
                        }
                        else if (timeDifference <= x1)
                        {
                            calculatedSpeed = y1; // Maximum speed.
                        }
                        else if (timeDifference >= x2)
                        {
                            calculatedSpeed = y2; // Minimum speed.
                        }
                        else
                        {
                            calculatedSpeed = y1 + ((y2 - y1) / (x2 - x1)) * (timeDifference - x1);
                        }
                        currentSpeed = Mathf.RoundToInt(Mathf.Clamp(calculatedSpeed, y2, y1));

                        // --- Long Gaps CHANGE: Move long gap events to the start of the gap (time[1]) ----------------------
                        if (timeDifference > 2.5f)// && UnityEngine.Random.Range(0, 2) == 0)
                        {
                            //Plugin.Log.Info($"[AutoLightMapper] Long gap detected ({timeDifference:F3}s). Adding slow, low-brightness rotating laser at time: {time[1]:F3} (start of gap).");

                            lightEvents.Add(EBasicEventData.Create(time[1], EventType.LEFT_SPEED, (EventValue)1));  // Very slow rotation at start of gap
                            lightEvents.Add(EBasicEventData.Create(time[1], EventType.RIGHT_SPEED, (EventValue)1)); // Very slow rotation at start of gap

                            EventValue fadeColor = useBlueFade ? EventValue.BLUE_FADE : EventValue.RED_FADE;
                            useBlueFade = !useBlueFade; // Flip it for next time

                            lightEvents.Add(EBasicEventData.Create(time[1], EventType.LEFT, fadeColor, 0.3f));  // Dim at start of gap
                            lightEvents.Add(EBasicEventData.Create(time[1], EventType.RIGHT, fadeColor, 0.3f)); // Dim at start of gap

                        }
                        // ------------------------------------------------------------------------------
                        if (needsLEFT && needsLEFTSPEED && pattern[patternIndex] == 2 && Math.Abs(currentSpeed - lastLeftSpeed) >= 2)
                        {
                            //Plugin.Log.Info($"[AutoLightMapper] Generated LEFT_SPEED event at time {time[0]:F3} with speed {currentSpeed}");
                            lightEvents.Add(EBasicEventData.Create(time[0], EventType.LEFT_SPEED, (EventValue)currentSpeed));
                            lastLeftSpeed = currentSpeed;
                        }
                        if (needsRIGHT && needsRIGHTSPEED && pattern[patternIndex] == 3 && Math.Abs(currentSpeed - lastRightSpeed) >= 2)
                        {
                            //Plugin.Log.Info($"[AutoLightMapper] Generated RIGHT_SPEED event at time {time[0]:F3} with speed {currentSpeed}");
                            lightEvents.Add(EBasicEventData.Create(time[0], EventType.RIGHT_SPEED, (EventValue)currentSpeed));
                            lastRightSpeed = currentSpeed;
                        }
                        //longGap = false;
                    }
                    for (int i = 3; i > 0; i--)
                    {
                        time[i] = time[i - 1];
                    }
                }
            }
            #endregion

            // Add original light events to the new list.
            foreach (EBasicEventData e in originalLightEvents)
            {
                EBasicEventData currentLight = EBasicEventData.Create(e.time, e.basicBeatmapEventType, e.value, e.floatValue);
                //Plugin.Log.Info($"[AutoLightMapper] Preserving original event: Time={e.time:F3}, Type={(EventType)e.basicBeatmapEventType}, Value={(EventValue)e.value}");
                lightEvents.Add(currentLight);
            }

            lightEvents = lightEvents.OrderBy(o => o.time).ToList();
            lightEvents = RemoveFused(lightEvents);
            lightEvents = lightEvents.OrderBy(o => o.time).ToList();
            /*
            List<EBasicEventData> lights = new List<EBasicEventData>();

            foreach (EBasicEventData e in lightEvent)
            {
                
                EBasicEventData customLightData = EBasicEventData.Create(e.time, e.basicBeatmapEventType, e.value, e.floatValue);
                lights.Add(customLightData);
                //Plugin.Log.Info($"[AutoLightMapper] Final light event: Time={e.time:F3}, Type={(EventType)e.basicBeatmapEventType}, Value={(EventValue)e.value}, FloatValue={e.floatValue}");
            }
            return lights;
            */
            return lightEvents;
        }
        //END CreateLight------------------------------------------------------------

        //clean up pass. detect when Multiple events of the same type that overlap but don’t add visual meaning and OFF events at the same time as an ON/FLASH/TRANSITION event.
        private static List<EBasicEventData> RemoveFused(List<EBasicEventData> events)
        {
            float closest = 0f;
            for (int i = 0; i < events.Count; i++)
            {
                EBasicEventData e = events[i];
                EBasicEventData MapEvent = events.Find(o => o.basicBeatmapEventType == e.basicBeatmapEventType && (Math.Abs(o.time - e.time) <= 0.02f) && o != e);
                if (MapEvent != null)
                {
                    EBasicEventData MapEvent2 = events.Find(o => o.basicBeatmapEventType == MapEvent.basicBeatmapEventType && (o.time - MapEvent.time >= -0.02 && o.time - MapEvent.time <= 0.02) && o != MapEvent);
                    if (MapEvent2 != null)
                    {
                        EBasicEventData temp = events.FindLast(o => o.time < e.time && e.time > closest && o.value != 0);
                        if (temp != null)
                        {
                            closest = temp.time;
                            if (MapEvent2.value == (int)EventValue.OFF)
                            {
                                events[events.FindIndex(o => o.time == MapEvent2.time && o.value == MapEvent2.value && o.basicBeatmapEventType == MapEvent2.basicBeatmapEventType)]
                                    .time = (float)(MapEvent2.time - ((MapEvent2.time - closest) / 2));
                            }
                            else
                            {
                                if (MapEvent.value == (int)EventValue.OFF || MapEvent.value == (int)EventValue.BLUE_TRANSITION || MapEvent.value == (int)EventValue.RED_TRANSITION)
                                {
                                    events[events.FindIndex(o => o.time == MapEvent.time && o.value == MapEvent.value && o.basicBeatmapEventType == MapEvent.basicBeatmapEventType)]
                                        .time = (float)(MapEvent.time - ((MapEvent.time - closest) / 2));
                                }
                                else
                                {
                                    events.RemoveAt(events.FindIndex(o => o.time == MapEvent.time && o.value == MapEvent.value && o.basicBeatmapEventType == MapEvent.basicBeatmapEventType));
                                }
                            }
                        }
                    }
                }
            }
            return events;
        }

        private static (EventValue color, float floatValue) FindColor(float first, float current, LightEventType type, bool random = false)
        {
            EventValue baseColor = EventValue.RED_FADE;
            for (int i = 0; i < ((current - first + Light.ColorOffset) / Light.ColorSwap); i++)
            {
                baseColor = Light.Inverse(baseColor);
            }
            if (first == current)
            {
                baseColor = EventValue.BLUE_FADE;
            }
            System.Random rnd = new System.Random();
            if (random)
            {
                int randomNumber = rnd.Next(2);
                baseColor = randomNumber == 0 ? EventValue.BLUE_FADE : EventValue.RED_FADE;
            }
            double chance = rnd.NextDouble();
            if (chance < 0.10)
            {
                baseColor = EventValue.TRANSITION;
            }
            EventValue finalColor = baseColor;
            switch (baseColor)
            {
                case EventValue.RED_FADE:
                case EventValue.RED_ON:
                case EventValue.RED_FLASH:
                case EventValue.RED_TRANSITION:
                    finalColor = GetColorForType(EventValue.RED_ON, type);
                    break;
                case EventValue.BLUE_FADE:
                case EventValue.BLUE_ON:
                case EventValue.BLUE_FLASH:
                case EventValue.BLUE_TRANSITION:
                    finalColor = GetColorForType(EventValue.BLUE_ON, type);
                    break;
                case EventValue.TRANSITION:
                    finalColor = GetColorForType(EventValue.ON, type);
                    break;
            }
            float floatValue = (float)Math.Round((chance * 0.5 + 0.5), 1);
            return (finalColor, floatValue);
        }

        private static EventValue GetColorForType(EventValue baseColor, LightEventType type)
        {
            switch (type)
            {
                case LightEventType.ON:
                    return baseColor;
                case LightEventType.FLASH:
                    return baseColor + 1;
                case LightEventType.FADE:
                    return baseColor + 2;
                case LightEventType.TRANSITION:
                    return baseColor + 3;
                default:
                    return baseColor;
            }
        }

        private static EventValue FadeEvent(EventValue currentColor)
        {
            switch (currentColor)
            {
                case EventValue.BLUE_ON:
                case EventValue.BLUE_FLASH:
                case EventValue.BLUE_FADE:
                case EventValue.BLUE_TRANSITION:
                    return EventValue.BLUE_FADE;
                case EventValue.RED_ON:
                case EventValue.RED_FLASH:
                case EventValue.RED_FADE:
                case EventValue.RED_TRANSITION:
                    return EventValue.RED_FADE;
                case EventValue.ON:
                case EventValue.FLASH:
                case EventValue.FADE:
                case EventValue.TRANSITION:
                    return EventValue.FADE;
                default:
                    return EventValue.OFF;
            }
        }

        private static void AddEventIfColorChanged(float time, EventType eventType, EventValue color, float floatValue, bool needsEventType, List<EBasicEventData> eventTempo, Dictionary<EventType, EventValue> lastEventColors)
        {
            if (needsEventType)
            {
                if (!lastEventColors.TryGetValue(eventType, out EventValue lastColor) || lastColor != color)
                {
                    Plugin.Log.Info($"[AutoLightMapper] Adding event for {eventType} at time {time:F3} with new color {color}");
                    eventTempo.Add(EBasicEventData.Create(time, eventType, color));
                    lastEventColors[eventType] = color;
                }
            }
        }
    }

    //---------------------------------------------------------------------------------------------------------------------------
    public static class Light
    {
        private static float colorOffset = 0.0f;
        private static float colorSwap = 4.0f;
        public static float ColorOffset { set => colorOffset = value > -100.0f ? value : 0.0f; get => colorOffset; }
        public static float ColorSwap { set => colorSwap = value > 0.0f ? value : 4.0f; get => colorSwap; }
        public static bool NerfStrobes { set; get; } = false; // suppresses strobes (was on!)

        public static EventValue Swap(EventValue x)
        {
            switch (x)
            {
                case EventValue.BLUE_FADE: return EventValue.BLUE_ON;
                case EventValue.RED_FADE: return EventValue.RED_ON;
                case EventValue.BLUE_ON: return EventValue.BLUE_FADE;
                case EventValue.RED_ON: return EventValue.RED_FADE;
                case EventValue.BLUE_FLASH: return EventValue.BLUE_FADE;
                case EventValue.RED_FLASH: return EventValue.RED_FADE;
                case EventValue.BLUE_TRANSITION: return EventValue.BLUE_FADE;
                case EventValue.RED_TRANSITION: return EventValue.RED_FADE;
                default: return EventValue.OFF;
            }
        }

        public static EventValue Inverse(EventValue eventValue)
        {
            if (eventValue > EventValue.BLUE_TRANSITION)
                return eventValue - 4;
            else
                return eventValue + 4;
        }

        public static void Shuffle<T>(this IList<T> list)
        {
            RandomNumberGenerator rng = RandomNumberGenerator.Create();
            int n = list.Count;
            while (n > 1)
            {
                byte[] box = new byte[1];
                do rng.GetBytes(box);
                while (!(box[0] < n * (Byte.MaxValue / n)));
                int k = (box[0] % n);
                n--;
                (list[n], list[k]) = (list[k], list[n]);
            }
        }


    }
}
