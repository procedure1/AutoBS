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
        private enum StrobeMode
        {
            Back = 0,            // EventType.BACK only
            FrontBoth = 1,       // EventType.LEFT + EventType.RIGHT together
            FrontAlternating = 2, // alternate LEFT / RIGHT each tick
            Center = 3
        }

        private static bool isTheFirstEnvironment = false; // decide if TheFirst is the environment

        //private static int seed = Utils.StableHash32(TransitionPatcher.SelectedPlayKey.ToString());

        //private static System.Random RepeatableRandom = new System.Random(seed);

        public static bool LightEventsAdded = false;
        public static void Start(EditableCBD eData)
        {
            LightEventsAdded = false;

            // Check the environment.
            string envName = TransitionPatcher.EnvironmentName != null ? TransitionPatcher.EnvironmentName : "DefaultEnvironment";

            // PROBLEM: if choose override same environment as default for the map, it was list as DefaultEnvironment!!!!!!!!!!!!!!!!!!!!!!!!! also, if the 2nd is overriden, it was listed as "TheSecondEnvironment" and if is defualt is listed as "The Second Environment"
            bool isSupportedEnvironment = IsV2Environment(envName);// || envName.Contains("Second");// || envName == "EDMEnvironment";

            isTheFirstEnvironment = isSupportedEnvironment && !TransitionPatcher.IsGen360 && (envName == "DefaultEnvironment" || envName.IndexOf("first", StringComparison.OrdinalIgnoreCase) >= 0);

            Plugin.LogDebug($"[AutoLightMapper] {envName} Environment isSupported: {isSupportedEnvironment}"); // if v3 environment is chosen, then there is no reason to produce lights since they are not supported. Never was able to produce GLS lights

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

            Plugin.LogDebug($"[AutoLightMapper] - this map needsBACK: {needsBACK}, needsRING: {needsRING}, needsLEFT: {needsLEFT}, needsRIGHT: {needsRIGHT}, needsCENTER: {needsCENTER}, needsLEFTSPEED: {needsLEFTSPEED}, needsRIGHTSPEED: {needsRIGHTSPEED}, needsRINGSPIN: {needsRINGSPIN}, needsRINGZOOM: {needsRINGZOOM}");

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
            Plugin.LogDebug($"[AutoLightMapper] - Existing Light Types: {existingLightTypes} count");

            //Standard maps: If 2 or more light types already exist → skip light generation
            if (existingLightTypes > 2 && //counts how many false values exist in bools. It effectively counts how many light events already exist in the map.
                TransitionPatcher.SelectedSerializedName != "Generated360Degree" &&
                TransitionPatcher.SelectedSerializedName != "360Degree" &&
                TransitionPatcher.SelectedSerializedName != "90Degree")
            {
                Plugin.LogDebug($"[AutoLightMapper] not used since there are 3 or more light events types programmed already for standard map.");
                LightEventsAdded = false;
                return;
            }

            int existing360LightTypes = threeSixtyTypes.Count(b => !b);

            //360: If all 7 core types are already present, it skips generation
            if (TransitionPatcher.SelectedSerializedName == "Generated360Degree" ||
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
                    Plugin.LogDebug($"[AutoLightMapper] not used since all seven 360 light event types are already programmed.");
                    LightEventsAdded = false;
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
                Plugin.LogDebug($"[AutoLightMapper] Converting v2 events into GLS events for environment '{envName}'");
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

            eData.BasicEventsChanged = false;

            int lightCounter = 0;
            foreach (EBasicEventData light in v2lights)
            {
                //Plugin.LogDebug($"[AutoLightMapper] Inserting event: Time={light.time:F3}, Type={(EventType)light.basicBeatmapEventType}, Value={(EventValue)light.value}, Brightness={light.floatValue:F2}");
                eData.BasicEvents.Add(light);
                lightCounter++;
            }

            if (lightCounter > 0) eData.BasicEventsChanged = true;

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

            System.Random repeatableRandom = TransitionPatcher.RepeatableRandom;

            if (notes == null || notes.Count == 0)
            {
                // No notes -> no generated events; return originals (or empty list if you prefer)
                return originalLightEvents?.ToList() ?? new List<EBasicEventData>();
            }

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
                Plugin.LogDebug($"[AutoLightMapper] Original special event: Type={s.Key}, Exists={s.Value}");
            }
            */
            Dictionary<EventType, EventValue> lastEventColors = new Dictionary<EventType, EventValue>();

            LightEventType lightStyle = (LightEventType)Config.Instance.LightStyle;

            float brightnessMultiplier = Config.Instance.BrightnessMultiplier;

            float frequencyMultiplier = Config.Instance.LightFrequencyMultiplier;

            // --- NEW: Determine allowed special events based on environment ---
            string environmentName = TransitionPatcher.EnvironmentName != null ? TransitionPatcher.EnvironmentName : "DefaultEnvironment";
            //Plugin.LogDebug($"[AutoLightMapper] Song name: {SetContent.SongName} --- Current environment name: {environmentName} -------------------");
            //Plugin.LogDebug($" -------------------");

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
                Plugin.LogDebug($"[AutoLightMapper] Environment '{environmentName}' does not support special events. No special events will be added.");
            }
            Plugin.LogDebug($"[AutoLightMapper] Allowed special event types: {string.Join(", ", allowedSpecialEventTypes)}");
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
                            float t = now - (now - last) / 2;
                            if (needsBACK)
                            {
                                //Plugin.LogDebug($"[AutoLightMapper] Generated OFF event (BACK) at time {t:F3}");
                                lightEvents.Add(EBasicEventData.Create(t, EventType.BACK, (int)EventValue.OFF));
                            }
                            if (needsRING)
                            {
                                //Plugin.LogDebug($"[AutoLightMapper] Generated OFF event (RING) at time {t:F3}");
                                lightEvents.Add(EBasicEventData.Create(t, EventType.RING, EventValue.OFF));
                            }
                            if (needsLEFT)
                            {
                                //Plugin.LogDebug($"[AutoLightMapper] Generated OFF event (LEFT) at time {t:F3}");
                                lightEvents.Add(EBasicEventData.Create(t, EventType.LEFT, EventValue.OFF));
                            }
                            if (needsRIGHT)
                            {
                                //Plugin.LogDebug($"[AutoLightMapper] Generated OFF event (RIGHT) at time {t:F3}");
                                lightEvents.Add(EBasicEventData.Create(t, EventType.RIGHT, EventValue.OFF));
                            }
                            if (needsCENTER)
                            {
                                //Plugin.LogDebug($"[AutoLightMapper] Generated OFF event (CENTER) at time {t:F3}");
                                lightEvents.Add(EBasicEventData.Create(t, EventType.CENTER, EventValue.OFF));
                            }
                            if (needsRINGSPIN)
                            {
                                //Plugin.LogDebug($"[AutoLightMapper] Generated OFF event (RING_SPIN) at time {t:F3}");
                                lightEvents.Add(EBasicEventData.Create(t, EventType.RING_SPIN, EventValue.OFF));
                            }
                            if (needsRINGZOOM)
                            {
                                //Plugin.LogDebug($"[AutoLightMapper] Generated OFF event (RING_ZOOM) at time {t:F3}");
                                lightEvents.Add(EBasicEventData.Create(t, EventType.RING_ZOOM, EventValue.OFF));
                            }
                            //Plugin.LogDebug($"[AutoLightMapper] Off events (group 1) added at time {t:F3}");
                        }
                        else
                        {
                            if (needsBACK)
                            {
                                //Plugin.LogDebug($"[AutoLightMapper] Generated OFF event (BACK) at time {now:F3}");
                                lightEvents.Add(EBasicEventData.Create(now, EventType.BACK, EventValue.OFF));
                            }
                            if (needsRING)
                            {
                                //Plugin.LogDebug($"[AutoLightMapper] Generated OFF event (RING) at time {now:F3}");
                                lightEvents.Add(EBasicEventData.Create(now, EventType.RING, EventValue.OFF));
                            }
                            if (needsLEFT)
                            {
                                //Plugin.LogDebug($"[AutoLightMapper] Generated OFF event (LEFT) at time {now:F3}");
                                lightEvents.Add(EBasicEventData.Create(now, EventType.LEFT, EventValue.OFF));
                            }
                            if (needsRIGHT)
                            {
                                //Plugin.LogDebug($"[AutoLightMapper] Generated OFF event (RIGHT) at time {now:F3}");
                                lightEvents.Add(EBasicEventData.Create(now, EventType.RIGHT, EventValue.OFF));
                            }
                            if (needsCENTER)
                            {
                                //Plugin.LogDebug($"[AutoLightMapper] Generated OFF event (CENTER) at time {now:F3}");
                                lightEvents.Add(EBasicEventData.Create(now, EventType.CENTER, EventValue.OFF));
                            }
                            if (needsRINGSPIN)
                            {
                                //Plugin.LogDebug($"[AutoLightMapper] Generated OFF event (RING_SPIN) at time {now:F3}");
                                lightEvents.Add(EBasicEventData.Create(now, EventType.RING_SPIN, EventValue.OFF));
                            }
                            if (needsRINGZOOM)
                            {
                                //Plugin.LogDebug($"[AutoLightMapper] Generated OFF event (RING_ZOOM) at time {now:F3}");
                                lightEvents.Add(EBasicEventData.Create(now, EventType.RING_ZOOM, EventValue.OFF));
                            }
                            //Plugin.LogDebug($"[AutoLightMapper] Off events (group 2) added at time {now:F3}");
                        }
                        doubleOn = false;
                    }

                    if ((now == time[1] || (now - time[1] <= 0.02 && time[1] != time[2])) && (time[1] != 0.0D && now != last) &&
                        !sliderTiming.Exists(e => e.time == now))
                    {
                        if (needsBACK)
                        {
                            (EventValue color, float floatValue) = FindColor(notes.First().time, time[0], lightStyle, repeatableRandom);
                            //Plugin.LogDebug($"[AutoLightMapper] Generated BACK event at time {now:F3} with color {color} and brightness {floatValue * brightnessMultiplier:F2}");
                            lightEvents.Add(EBasicEventData.Create(now, EventType.BACK, color, floatValue * brightnessMultiplier));
                        }
                        if (needsRING)
                        {
                            (EventValue color, float floatValue) = FindColor(notes.First().time, time[0], lightStyle, repeatableRandom);
                            //Plugin.LogDebug($"[AutoLightMapper] Generated RING event at time {now:F3} with color {color} and brightness {floatValue * brightnessMultiplier:F2}");
                            lightEvents.Add(EBasicEventData.Create(now, EventType.RING, color, floatValue * brightnessMultiplier));
                        }
                        if (needsLEFT || needsRIGHT)
                        {
                            (EventValue color, float floatValue) = FindColor(notes.First().time, time[0], lightStyle, repeatableRandom, false); // false so it will go back and forth between colors
                            if (needsLEFT)
                            {
                                //Plugin.LogDebug($"[AutoLightMapper] Generated LEFT event at time {now:F3} with color {color} and brightness {floatValue * brightnessMultiplier:F2}");
                                lightEvents.Add(EBasicEventData.Create(now, EventType.LEFT, color, floatValue * brightnessMultiplier));
                            }
                            if (needsRIGHT)
                            {
                                //Plugin.LogDebug($"[AutoLightMapper] Generated RIGHT event at time {now:F3} with color {color} and brightness {floatValue * brightnessMultiplier:F2}");
                                lightEvents.Add(EBasicEventData.Create(now, EventType.RIGHT, color, floatValue * brightnessMultiplier));
                            }
                        }
                        if (needsCENTER)
                        {
                            (EventValue color, float floatValue) = FindColor(notes.First().time, time[0], lightStyle, repeatableRandom);
                            //Plugin.LogDebug($"[AutoLightMapper] Generated CENTER event at time {now:F3} with color {color} and brightness {floatValue * brightnessMultiplier:F2}");
                            lightEvents.Add(EBasicEventData.Create(now, EventType.CENTER, color, floatValue * brightnessMultiplier));
                        }
                        if (needsRINGSPIN)
                        {
                            //Plugin.LogDebug($"[AutoLightMapper] Generated RING_SPIN event at time {now:F3}");
                            lightEvents.Add(EBasicEventData.Create(now, EventType.RING_SPIN, EventValue.OFF));
                        }
                        if (needsRINGZOOM)
                        {
                            (EventValue color, float floatValue) = FindColor(notes.First().time, time[0], lightStyle, repeatableRandom);
                            //Plugin.LogDebug($"[AutoLightMapper] Generated RING_ZOOM event at time {now:F3}");
                            lightEvents.Add(EBasicEventData.Create(now, EventType.RING_ZOOM, EventValue.OFF));
                        }
                        doubleOn = true;
                        last = now;
                        //Plugin.LogDebug($"[AutoLightMapper] Generated light event at time {now:F3}.");
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
                float lastTimeBack = 100;
                float lastTimeRing = 100;
                float lastTimeCenter = 100;
                float lastTimeLeft = 100;
                float lastTimeRight = 100;

                foreach (EBasicEventData x in lightEvents)
                {
                    if (x.eventType == EventType.BACK)
                    {
                        if (x.time - lastTimeBack <= 0.5)
                        {
                            x.eventValue = Light.Swap(x.eventValue);
                        }
                        lastTimeBack = x.time;
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

            //bool currentlyStrobing = false;

            float burstEndTime = -1f;

            List<(float start, float end, StrobeMode mode)> strobeWindows = new List<(float start, float end, StrobeMode mode)>();

            var sliderTimes = new HashSet<float>();
            if (sliders != null)
            {
                foreach (var s in sliders)
                    sliderTimes.Add(s.time);
            }

            float beatDuration = 60f / TransitionPatcher.bpm;
            float sixteenth = beatDuration / 4f; // 16th note grid in seconds

            bool enableStrobes = Config.Instance.EnableStrobes && ((needsLEFT && needsRIGHT) || (needsCENTER && !TransitionPatcher.IsGen360) || needsBACK);
            float mult = Config.Instance.StrobeMultiplier;

            int triggerEvery = 0;

            if (mult > 0f)
            {
                triggerEvery = Mathf.Clamp(Mathf.RoundToInt(40f / mult), 5, 400); // .25 = 160, .5 = 80, 1 = 40, 2 = 20, 4 = 10 etc
                enableStrobes = true;
            }
            else
                enableStrobes = false;

                const float closeNotesThreshold = 0.2f;

            const float burstMin = 0.2f;
            float burstMax = Config.Instance.StrobeMaxDuration;

            bool IsSuppressedByStrobe(float t, EventType type)
            {
                for (int i = 0; i < strobeWindows.Count; i++)
                {
                    var w = strobeWindows[i];
                    if (t < w.start || t > w.end) continue;

                    return w.mode switch
                    {
                        StrobeMode.Back => type == EventType.BACK,
                        StrobeMode.FrontBoth => type == EventType.LEFT || type == EventType.RIGHT,
                        StrobeMode.FrontAlternating => type == EventType.LEFT || type == EventType.RIGHT, // suppress both; ticks decide which fires
                        StrobeMode.Center => type == EventType.CENTER,
                        _ => false
                    };
                }

                return false;
            }


            StrobeMode ChooseStrobeMode(System.Random rng)
            {
                //return StrobeMode.Back;
                // Pick uniformly
                /*if (TransitionPatcher.IsGen360)
                {
                    StrobeMode mode = rng.Next(3) switch
                    {
                        0 => StrobeMode.Back,
                        1 => StrobeMode.FrontBoth,
                        _ => StrobeMode.FrontAlternating
                    };
                    return mode;
                }
                else*/
                {
                    StrobeMode mode = rng.Next(4) switch
                    {
                        0 => StrobeMode.Back,   
                        1 => StrobeMode.Center, //Center is invisible in 360
                        2 => StrobeMode.FrontBoth,
                        _ => StrobeMode.FrontAlternating
                    };
                    if (isTheFirstEnvironment && mode == StrobeMode.Back) //Back is very low key using TheFirst
                        mode = StrobeMode.Center;

                    return mode;
                }
            }


            // returns interval of light flickering in seconds
            float ChooseStrobeInterval(int index, List<ENoteData> notes, float beatDuration, System.Random rng)
            {

                // Average of last up to 4 note deltas (seconds)
                float sum = 0f;
                int n = 0;
                for (int k = index; k > 0 && n < 4; k--, n++)
                    sum += (notes[k].time - notes[k - 1].time);

                float avgDelta = (n > 0) ? (sum / n) : (beatDuration / 4f);

                // density: 0 = sparse, 1 = very dense
                float density = Mathf.InverseLerp(0.25f, 0.10f, avgDelta);

                // Fast-only musical candidates (seconds)
                float s32 = beatDuration / 8f;  // 1/32  (0.0625 at 120 BPM)
                float s24 = beatDuration / 6f;  // 1/24  (~0.0833 at 120 BPM)
                float s16 = beatDuration / 4f;  // 1/16  (0.125  at 120 BPM)  <-- slowest allowed here

                double r = rng.NextDouble();

                if (density >= 0.75f)
                {
                    // Very dense: mostly fastest
                    if (r < 0.70) return s32; // 70% chance
                    if (r < 0.95) return s24; // next 25% chance (0.70–0.95)
                    return s16;               // remaining 5% chance (0.95–1.00)
                }
                else if (density >= 0.40f)
                {
                    // Moderate: mostly s24, sometimes s16, rare s32
                    if (r < 0.15) return s32;
                    if (r < 0.80) return s24;
                    return s16;
                }
                else
                {
                    // Sparse: mostly s16; occasional fast burst
                    if (r < 0.05) return s32;   // rare
                    if (r < 0.20) return s24;   // occasional
                    return s16;                 // default
                }
                /*
                if (density >= 0.75f)
                    return s32;
                else if (density >= 0.40f)
                    return s24;
                else
                {
                    if (r < 0.25) return s24;
                    return s16;
                }
                */
            }

            float ChooseStrobeIntervalSLOW(int index, List<ENoteData> notes, float beatDuration, System.Random rng)
            {
                // Average of last up to 4 note deltas (seconds)
                float sum = 0f;
                int n = 0;
                for (int k = index; k > 0 && n < 4; k--, n++)
                    sum += (notes[k].time - notes[k - 1].time);

                float avgDelta = (n > 0) ? (sum / n) : (beatDuration / 4f);

                // density: 0 = sparse, 1 = very dense
                float density = Mathf.InverseLerp(0.25f, 0.10f, avgDelta);

                // Musical candidates (seconds)
                float s32 = beatDuration / 8f;  // 1/32
                float s24 = beatDuration / 6f;  // 1/24
                float s16 = beatDuration / 4f;  // 1/16 .125 for 120bpm
                float s12 = beatDuration / 3f;  // 1/12
                float s8 = beatDuration / 2f;  // 1/8   .250 for 120bpm
                float s6 = beatDuration / 1.5f; // 1/6 (quarter-triplet feel). Optional.
                //float s4 = beatDuration;       // 1/4

                double r = rng.NextDouble();

                // Very dense: mostly fast, occasionally slightly slower
                if (density >= 0.85f)
                {
                    if (r < 0.40) return s32;
                    if (r < 0.85) return s24;
                    return s16;
                }

                // Moderately dense: mid-fast, sometimes slower
                if (density >= 0.55f)
                {
                    if (r < 0.20) return s24;
                    if (r < 0.65) return s16;
                    if (r < 0.90) return s12;
                    return s8;
                }

                // Medium sparse: mostly 1/12–1/8, sometimes 1/4
                if (density >= 0.30f)
                {
                    if (r < 0.35) return s12;
                    if (r < 0.80) return s8;
                    // keep/remove the next line depending on whether you want 1/6
                    if (r < 0.92) return s6;
                    return s8;
                }

                // Very sparse: slow strobes (often 1/4)
                if (r < 0.65) return s8;
                //if (r < 0.90) return s8;
                return s12;
            }

            float strobeBrightnessMult = Config.Instance.StrobeBrightnessMultiplier * Config.Instance.BrightnessMultiplier;
            if (!TransitionPatcher.IsGen360) strobeBrightnessMult *= 1.5f;// needs to be brighter for standard environments at least for theFirst

            int strobeCount = 0;

            #region Main Loop
            // -------------------------
            // MAIN LOOP
            // -------------------------
            for (int index = 0; index < notes.Count; index++)
            {
                ENoteData note = notes[index];

                // Expire strobe state when its window ends.
                //if (note.time > burstEndTime)
                //    currentlyStrobing = false;

                // Do not start or process anything while inside an active burst window
                bool inBurst = note.time < burstEndTime;
                if (inBurst)
                {
                    // Advance the timing window so post-burst logic behaves normally.
                    for (int i = 3; i > 0; i--)
                        time[i] = time[i - 1];

                    time[0] = note.time;

                    // Keep your frequency gating consistent across the whole song.
                    lightEventMultiplierCounter += frequencyMultiplier;
                    if (lightEventMultiplierCounter >= 1.0f)
                        lightEventMultiplierCounter -= 1.0f;

                    // Also keep lastSpeed sane (used for pattern changes).
                    lastSpeed = time[0] - time[1];

                    continue; // still skip generating normal events during the burst
                }


                // Dense note detection (O(1))
                bool closeNotes = (index > 0) && ((notes[index].time - notes[index - 1].time) < closeNotesThreshold);

                // Slider time detection (O(1))
                bool isSlider = sliderTimes.Contains(note.time);

                #region Strobe Bursts
                // -------------------------
                // STROBE TRIGGER BLOCK
                // -------------------------
                if (enableStrobes && closeNotes)
                {
                    closeNoteCounter++;

                    if (closeNoteCounter % triggerEvery == 0)
                    {
                        StrobeMode mode = ChooseStrobeMode(repeatableRandom);

                        bool strobeModeAllowed(StrobeMode m) =>
                            ( m == StrobeMode.Back && needsBACK) || // won't get back unless is its not theFirst environment
                            ((m == StrobeMode.FrontBoth || m == StrobeMode.FrontAlternating) && needsLEFT && needsRIGHT) ||
                            ( m == StrobeMode.Center && needsCENTER); // won't get center unless not 360

                        // try a few re-rolls
                        for (int tries = 0; tries < 4 && !strobeModeAllowed(mode); tries++)
                            mode = ChooseStrobeMode(repeatableRandom);

                        // hard fallback (pick the first available group)
                        if (!strobeModeAllowed(mode))
                        {
                            if (needsLEFT && needsRIGHT) mode = StrobeMode.FrontAlternating;
                            else if (needsBACK && (TransitionPatcher.IsGen360 || (!TransitionPatcher.IsGen360 && !isTheFirstEnvironment))) mode = StrobeMode.Back;
                            else if (needsCENTER && !TransitionPatcher.IsGen360) mode = StrobeMode.Center;
                        }

                        if (strobeModeAllowed(mode))
                        {
                            float snappedTime = Mathf.Round(note.time / sixteenth) * sixteenth;

                            float burstStart = snappedTime;
                            if (index > 0)
                                burstStart = Mathf.Max(burstStart, notes[index - 1].time);

                            // bias 1.0 no bias.... 4.0 rare long bursts
                            float bias = 2.0f;

                            float u = (float)repeatableRandom.NextDouble();   // 0..1 uniform
                            float biased = Mathf.Pow(u, bias);                // still 0..1, but skewed low

                            float raw = burstMin + biased * (burstMax - burstMin);

                            // snap to nearest 1/16 note
                            float step = beatDuration / 4f;
                            float burstDuration = Mathf.Round(raw / step) * step;
                            burstDuration = Mathf.Clamp(burstDuration, burstMin, burstMax);


                            burstEndTime = burstStart + burstDuration;

                            //currentlyStrobing = true;

                            // Musical fallback: moderate (1/16 note at current BPM)
                            float strobeInterval = beatDuration / 4f;   // 0.125 at 120 BPM

                            if (index > 0)
                            {
                                strobeInterval = ChooseStrobeInterval(index, notes, beatDuration, repeatableRandom);
                            }

                            strobeCount++;

                        
                            strobeWindows.Add((burstStart, burstEndTime, mode));

                            Plugin.LogDebug(
                                $"[AutoLightMapper] Generated strobe burst {strobeCount}: start {burstStart:F3} dur {burstDuration:F3}, mode: {mode}, interval {strobeInterval:F3} (StrobeMultiplier: {mult})");

                            int strobeStep = 0;

                            EventValue tickColor = TransitionPatcher.RepeatableRandom.Next(2) == 0? EventValue.BLUE_FLASH: EventValue.RED_FLASH;

                            for (float strobeTime = burstStart; strobeTime < burstEndTime; strobeTime += strobeInterval, strobeStep++)
                            {
                                EventValue strobeColor =
                                        (strobeStep % 2 == 0)
                                            ? (repeatableRandom.Next(2) == 0 ? EventValue.FLASH : EventValue.ON)
                                            : EventValue.OFF;

                                switch (mode)
                                {
                                    case StrobeMode.Center:
                                        if (!TransitionPatcher.IsGen360)
                                            lightEvents.Add(EBasicEventData.Create(strobeTime, EventType.CENTER, strobeColor, strobeBrightnessMult));
                                        else
                                        {
                                            // Don’t use OFF for center in 360; alternate flash colors.
                                            // (If FLASH is too aggressive, use *_FADE instead.)
                                            bool tick = (strobeStep % 2 == 0);

                                            var v = tick ? tickColor : EventValue.FLASH;

                                            lightEvents.Add(EBasicEventData.Create(
                                                strobeTime,
                                                EventType.CENTER,
                                                v,
                                                strobeBrightnessMult));
                                        }
                                        break;

                                    case StrobeMode.Back:
                                        lightEvents.Add(EBasicEventData.Create(strobeTime, EventType.BACK, strobeColor, strobeBrightnessMult));
                                        break;

                                    case StrobeMode.FrontBoth:
                                        lightEvents.Add(EBasicEventData.Create(strobeTime, EventType.LEFT, strobeColor, strobeBrightnessMult));
                                        lightEvents.Add(EBasicEventData.Create(strobeTime, EventType.RIGHT, strobeColor, strobeBrightnessMult));
                                        break;

                                    case StrobeMode.FrontAlternating:
                                        if (strobeColor == EventValue.OFF)
                                        {
                                            lightEvents.Add(EBasicEventData.Create(strobeTime, EventType.LEFT, EventValue.OFF, strobeBrightnessMult));
                                            lightEvents.Add(EBasicEventData.Create(strobeTime, EventType.RIGHT, EventValue.OFF, strobeBrightnessMult));
                                        }
                                        else
                                        {
                                            int pulseIndex = strobeStep / 2;               // 0,1,2,3...
                                            bool leftPulse = (pulseIndex % 2 == 0);        // L, R, L, R...

                                            lightEvents.Add(EBasicEventData.Create(
                                                strobeTime,
                                                leftPulse ? EventType.LEFT : EventType.RIGHT,
                                                strobeColor,
                                                strobeBrightnessMult));
                                        }
                                        break;
                                }
                            }

                            // Hard end OFF
                            switch (mode)
                            {
                                case StrobeMode.Center:
                                    lightEvents.Add(EBasicEventData.Create(burstEndTime, EventType.CENTER, EventValue.OFF, strobeBrightnessMult)); // or FADE
                                    break;
                                case StrobeMode.Back:
                                    lightEvents.Add(EBasicEventData.Create(burstEndTime, EventType.BACK, EventValue.OFF, strobeBrightnessMult)); // or FADE
                                    break;

                                case StrobeMode.FrontBoth:
                                case StrobeMode.FrontAlternating:
                                    lightEvents.Add(EBasicEventData.Create(burstEndTime, EventType.LEFT, EventValue.OFF, strobeBrightnessMult)); // or FADE
                                    lightEvents.Add(EBasicEventData.Create(burstEndTime, EventType.RIGHT, EventValue.OFF, strobeBrightnessMult));// or FADE
                                    break;
                            }

                            // Ensure the legacy "mid-gap OFF insertion" does not cut through this burst.
                            // Treat the burst as having produced activity until burstEndTime.
                            doubleOn = false;

                            // Put `last` at (just before) the burst end so the next normal event doesn't see a huge gap.
                            // The tiny epsilon avoids `now == last` edge cases.
                            last = burstEndTime - 0.0001f;


                            continue;
                        }
                    }
                }


                #endregion


                // Determine whether we're currently inside a strobe window
                //bool inBurst = (note.time < burstEndTime);


                if ((closeNotes || isSlider) && (note.time - lastSpinTriggerTime >= 0.1))
                    {
                        if (needsRINGSPIN)
                        {
                            //Plugin.LogDebug($"[AutoLightMapper] Generated RING_SPIN event at time {note.time:F3} OFF");
                            lightEvents.Add(EBasicEventData.Create(note.time, EventType.RING_SPIN, EventValue.OFF));
                            lastSpinTriggerTime = note.time;
                        }
                    }

                    if ((closeNotes || isSlider) && (note.time - lastZoomTriggerTime >= 0.2))
                    {
                        if (needsRINGZOOM && (index % 2 == 0 || index % 3 == 0))
                        {
                            //Plugin.LogDebug($"[AutoLightMapper] Generated RING_ZOOM event at time {note.time:F3} OFF");
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
                        (EventValue specColor, float specBrightness) = FindColor(notes.First().time, note.time, lightStyle, repeatableRandom);
                        EventType specialEventType = missingSpecialEvents[specialEventIndex];  // Rotate through missing ones

                        //Plugin.LogDebug($"[AutoLightMapper] Generated SPECIAL event: Type={specialEventType}, Time={note.time:F3}, Value={specColor}, Brightness={specBrightness * brightnessMultiplier:F2}");

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
                            for (int i = index; i < notes.Count - 1; i++)
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
                            for (int i = index; i < notes.Count - 1; i++)
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
                            (EventValue color, float floatValue) = FindColor(notes.First().time, time[0], lightStyle, repeatableRandom);

                            //Plugin.LogDebug($"[AutoLightMapper] Generated SLIDER event ({et}) at time {time[0]:F3} with color {color} and brightness {floatValue * brightnessMultiplier:F2}");

                            if ((needsCENTER && et == EventType.CENTER) || (needsRING && et == EventType.RING) || (needsBACK && et == EventType.BACK))
                            {
                                if (!IsSuppressedByStrobe(time[0], et))
                                {
                                    //Plugin.LogDebug($"[AutoLightMapper] Generated SLIDER event ({et}) at time {time[0]:F3} with color {color} and brightness {floatValue * brightnessMultiplier:F2}");
                                    lightEvents.Add(EBasicEventData.Create(time[0], et, (color - 2), floatValue * brightnessMultiplier));
                                    lightEvents.Add(EBasicEventData.Create(time[0] + 0.125f, et, (color - 1), floatValue * brightnessMultiplier));
                                    lightEvents.Add(EBasicEventData.Create(time[0] + 0.25f, et, (color - 2), floatValue * brightnessMultiplier));
                                    lightEvents.Add(EBasicEventData.Create(time[0] + 0.375f, et, (color - 1), floatValue * brightnessMultiplier));
                                    lightEvents.Add(EBasicEventData.Create(time[0] + 0.5f, et, 0));
                                }
                            }
                            if ((needsRINGSPIN && et == EventType.RING_SPIN) || (needsRINGZOOM && et == EventType.RING_ZOOM))
                            {
                                //Plugin.LogDebug($"[AutoLightMapper] Generated SLIDER event (RING_SPIN or RING_ZOOM) OFF at time {time[0]:F3}");
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
                                    pattern.Shuffle(repeatableRandom);
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
                                (EventValue color, float floatValue) = FindColor(notes.First().time, time[0], lightStyle, repeatableRandom);
                                //Plugin.LogDebug($"[AutoLightMapper] Generated PATTERN event ({(EventType)pattern[patternIndex]}) at time {time[0]:F3} with color {color} and brightness {floatValue * brightnessMultiplier:F2}.");

                                if (!IsSuppressedByStrobe(time[0], (EventType)pattern[patternIndex]))
                                {
                                    //Plugin.LogDebug($"[AutoLightMapper] Generated PATTERN event ({(EventType)pattern[patternIndex]}) at time {time[0]:F3} with color {color} and brightness {floatValue * brightnessMultiplier:F2}.");
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
                                if (notes[index + 1].time == nextDouble)
                                {
                                    if (notes[index + 1].time - time[0] <= 2)
                                    {
                                        float value = (notes[index + 1].time - notes[index].time) / 2;
                                        if ((needsBACK && (EventType)pattern[patternIndex] == EventType.BACK) || (needsRING && (EventType)pattern[patternIndex] == EventType.RING) ||
                                            (needsLEFT && (EventType)pattern[patternIndex] == EventType.LEFT) || (needsRIGHT && (EventType)pattern[patternIndex] == EventType.RIGHT) ||
                                            (needsCENTER && (EventType)pattern[patternIndex] == EventType.CENTER))
                                        {
                                            if (!IsSuppressedByStrobe(notes[index].time, (EventType)pattern[patternIndex]))
                                            {
                                                if (lightStyle == LightEventType.FLASH || lightStyle == LightEventType.TRANSITION)
                                                {
                                                    if ((EventType)pattern[patternIndex] == EventType.LEFT)
                                                    {
                                                        //Plugin.LogDebug($"[AutoLightMapper] Generated FADE (LEFT) event at time {notes[index].time + value:F3}");
                                                        lightEvents.Add(EBasicEventData.Create(notes[index].time + value, (EventType)pattern[patternIndex], FadeEvent(lastLeftColor), brightnessMultiplier));
                                                    }
                                                    else if ((EventType)pattern[patternIndex] == EventType.RIGHT)
                                                    {
                                                        //Plugin.LogDebug($"[AutoLightMapper] Generated FADE (RIGHT) event at time {notes[index].time + value:F3}");
                                                        lightEvents.Add(EBasicEventData.Create(notes[index].time + value, (EventType)pattern[patternIndex], FadeEvent(lastRightColor), brightnessMultiplier));
                                                    }
                                                    else if ((EventType)pattern[patternIndex] == EventType.BACK)
                                                    {
                                                        //Plugin.LogDebug($"[AutoLightMapper] Generated FADE (BACK) event at time {notes[index].time + value:F3}");
                                                        lightEvents.Add(EBasicEventData.Create(notes[index].time + value, (EventType)pattern[patternIndex], FadeEvent(lastBackColor), brightnessMultiplier));
                                                    }
                                                    else if ((EventType)pattern[patternIndex] == EventType.RING)
                                                    {
                                                        //Plugin.LogDebug($"[AutoLightMapper] Generated FADE (RING) event at time {notes[index].time + value:F3}");
                                                        lightEvents.Add(EBasicEventData.Create(notes[index].time + value, (EventType)pattern[patternIndex], FadeEvent(lastRingColor), brightnessMultiplier));
                                                    }
                                                    else if ((EventType)pattern[patternIndex] == EventType.CENTER)
                                                    {
                                                        //Plugin.LogDebug($"[AutoLightMapper] Generated FADE (CENTER) event at time {notes[index].time + value:F3}");
                                                        lightEvents.Add(EBasicEventData.Create(notes[index].time + value, (EventType)pattern[patternIndex], FadeEvent(lastCenterColor), brightnessMultiplier));
                                                    }
                                                //Plugin.LogDebug($"[AutoLightMapper] Generated {(EventType)pattern[patternIndex]} event at time {notes[index].time + value:F3}");
                                            }
                                            else
                                                {
                                                    //Plugin.LogDebug($"[AutoLightMapper] Generated OFF (pattern) event at time {notes[index].time + value:F3}");
                                                    lightEvents.Add(EBasicEventData.Create(notes[index].time + value, (EventType)pattern[patternIndex], EventValue.OFF));
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
                                        if (!IsSuppressedByStrobe(notes[index + 1].time, (EventType)pattern[patternIndex]))
                                        {
                                            if (lightStyle == LightEventType.FLASH || lightStyle == LightEventType.TRANSITION)
                                            {
                                                if ((EventType)pattern[patternIndex] == EventType.LEFT)
                                                {
                                                    //Plugin.LogDebug($"[AutoLightMapper] Generated FADE (LEFT) event at time {notes[index + 1].time:F3}");
                                                    lightEvents.Add(EBasicEventData.Create(notes[index + 1].time, (EventType)pattern[patternIndex], FadeEvent(lastLeftColor), brightnessMultiplier));
                                                }
                                                else if ((EventType)pattern[patternIndex] == EventType.RIGHT)
                                                {
                                                    //Plugin.LogDebug($"[AutoLightMapper] Generated FADE (RIGHT) event at time {notes[index + 1].time:F3}");
                                                    lightEvents.Add(EBasicEventData.Create(notes[index + 1].time, (EventType)pattern[patternIndex], FadeEvent(lastRightColor), brightnessMultiplier));
                                                }
                                                else if ((EventType)pattern[patternIndex] == EventType.BACK)
                                                {
                                                    //Plugin.LogDebug($"[AutoLightMapper] Generated FADE (BACK) event at time {notes[index + 1].time:F3}");
                                                    lightEvents.Add(EBasicEventData.Create(notes[index + 1].time, (EventType)pattern[patternIndex], FadeEvent(lastBackColor), brightnessMultiplier));
                                                }
                                                else if ((EventType)pattern[patternIndex] == EventType.RING)
                                                {
                                                    //Plugin.LogDebug($"[AutoLightMapper] Generated FADE (RING) event at time {notes[index + 1].time:F3}");
                                                    lightEvents.Add(EBasicEventData.Create(notes[index + 1].time, (EventType)pattern[patternIndex], FadeEvent(lastRingColor), brightnessMultiplier));
                                                }
                                                else if ((EventType)pattern[patternIndex] == EventType.CENTER)
                                                {
                                                    //Plugin.LogDebug($"[AutoLightMapper] Generated FADE (CENTER) event at time {notes[index + 1].time:F3}");
                                                    if (repeatableRandom.Next(3) == 0)
                                                    {
                                                        lightEvents.Add(EBasicEventData.Create(notes[index + 1].time, EventType.CENTER, EventValue.OFF));
                                                        //Plugin.LogDebug($"[AutoLightMapper] Generated OFF (CENTER) event at time {notes[index + 1].time:F3}");
                                                    }
                                                    else
                                                        lightEvents.Add(EBasicEventData.Create(notes[index + 1].time, EventType.CENTER, FadeEvent(lastCenterColor), brightnessMultiplier));
                                                }
                                                //Plugin.LogDebug($"[AutoLightMapper] Generated {(EventType)pattern[patternIndex]} event at time {notes[index + 1].time:F3}");
                                        }
                                        else
                                            {
                                                //Plugin.LogDebug($"[AutoLightMapper] Generated OFF (pattern) event at time {notes[index + 1].time:F3}");
                                                lightEvents.Add(EBasicEventData.Create(notes[index + 1].time, (EventType)pattern[patternIndex], EventValue.OFF));
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
                                //Plugin.LogDebug($"[AutoLightMapper] Long gap detected ({timeDifference:F3}s). Adding slow, low-brightness rotating laser at time: {time[1]:F3} (start of gap).");

                                lightEvents.Add(EBasicEventData.Create(time[1], EventType.LEFT_SPEED, (EventValue)1));  // Very slow rotation at start of gap
                                lightEvents.Add(EBasicEventData.Create(time[1], EventType.RIGHT_SPEED, (EventValue)1)); // Very slow rotation at start of gap

                                EventValue fadeColor = useBlueFade ? EventValue.BLUE_FADE : EventValue.RED_FADE;
                                useBlueFade = !useBlueFade; // Flip it for next time

                                lightEvents.Add(EBasicEventData.Create(time[1], EventType.LEFT, fadeColor, brightnessMultiplier * 0.3f));  // Dim at start of gap
                                lightEvents.Add(EBasicEventData.Create(time[1], EventType.RIGHT, fadeColor, brightnessMultiplier * 0.3f)); // Dim at start of gap

                            }
                            // ------------------------------------------------------------------------------
                            if (needsLEFT && needsLEFTSPEED && pattern[patternIndex] == 2 && Math.Abs(currentSpeed - lastLeftSpeed) >= 2)
                            {
                                //Plugin.LogDebug($"[AutoLightMapper] Generated LEFT_SPEED event at time {time[0]:F3} with speed {currentSpeed}");
                                lightEvents.Add(EBasicEventData.Create(time[0], EventType.LEFT_SPEED, (EventValue)currentSpeed));
                                lastLeftSpeed = currentSpeed;
                            }
                            if (needsRIGHT && needsRIGHTSPEED && pattern[patternIndex] == 3 && Math.Abs(currentSpeed - lastRightSpeed) >= 2)
                            {
                                //Plugin.LogDebug($"[AutoLightMapper] Generated RIGHT_SPEED event at time {time[0]:F3} with speed {currentSpeed}");
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

            Plugin.LogDebug($"[AutoLightMapper] Generated {strobeCount} strobe bursts.");

            #endregion

            // Add original light events to the new list.
            foreach (EBasicEventData e in originalLightEvents)
            {
                EBasicEventData currentLight = EBasicEventData.Create(e.time, e.basicBeatmapEventType, e.value, e.floatValue);
                //Plugin.LogDebug($"[AutoLightMapper] Preserving original event: Time={e.time:F3}, Type={(EventType)e.basicBeatmapEventType}, Value={(EventValue)e.value}");
                lightEvents.Add(currentLight);
            }

            lightEvents = lightEvents.OrderBy(o => o.time).ToList();
            lightEvents = RemoveFused(lightEvents, strobeWindows);
            lightEvents = lightEvents.OrderBy(o => o.time).ToList();
            /*
            int survivors = lightEvents.Count(e =>
                e.time >= burstStart && e.time <= burstEndTime &&
                (useBackStrobe
                    ? (EventType)e.basicBeatmapEventType == EventType.BACK
                    : ((EventType)e.basicBeatmapEventType == EventType.LEFT || (EventType)e.basicBeatmapEventType == EventType.RIGHT)));

            Plugin.LogDebug($"[StrobeDbg] survivors in window={survivors}");
            */

            if (lightEvents.Count > 0)
            {
                LightEventsAdded = true;
                Plugin.LogDebug($"[AutoLightMapper] {lightEvents.Count} Light Events Added.");
            }

            return lightEvents;
        }

        //END CreateLight------------------------------------------------------------



            //clean up pass. detect when Multiple events of the same type that overlap but don’t add visual meaning and OFF events at the same time as an ON/FLASH/TRANSITION event.
        private static List<EBasicEventData> RemoveFused(
            List<EBasicEventData> events,
            List<(float start, float end, StrobeMode mode)> strobeWindows)
        {
            bool InStrobeWindow(float t, BasicBeatmapEventType basicType)
            {
                var type = (EventType)basicType;

                for (int i = 0; i < strobeWindows.Count; i++)
                {
                    var w = strobeWindows[i];
                    if (t < w.start || t > w.end)
                        continue;

                    switch (w.mode)
                    {
                        case StrobeMode.Back:
                            return type == EventType.BACK;

                        case StrobeMode.FrontBoth:
                        case StrobeMode.FrontAlternating:
                            return type == EventType.LEFT || type == EventType.RIGHT;
                        case StrobeMode.Center:
                            return type == EventType.CENTER;
                        default:
                            return false;
                    }
                }

                return false;
            }

            float closest = 0f;

            for (int i = 0; i < events.Count; i++)
            {
                EBasicEventData e = events[i];

                // Do not fuse/move/remove anything that occurs inside a strobe window
                if (InStrobeWindow(e.time, e.basicBeatmapEventType))
                    continue;

                EBasicEventData mapEvent = events.Find(o =>
                    o.basicBeatmapEventType == e.basicBeatmapEventType &&
                    Math.Abs(o.time - e.time) <= 0.02f &&
                    o != e);

                if (mapEvent == null)
                    continue;

                // NEW: also protect mapEvent if it is in a strobe window
                if (InStrobeWindow(mapEvent.time, mapEvent.basicBeatmapEventType))
                    continue;

                EBasicEventData mapEvent2 = events.Find(o =>
                    o.basicBeatmapEventType == mapEvent.basicBeatmapEventType &&
                    (o.time - mapEvent.time >= -0.02f && o.time - mapEvent.time <= 0.02f) &&
                    o != mapEvent);

                if (mapEvent2 == null)
                    continue;

                // NEW: also protect mapEvent2 if it is in a strobe window
                if (InStrobeWindow(mapEvent2.time, mapEvent2.basicBeatmapEventType))
                    continue;

                EBasicEventData temp = events.FindLast(o =>
                    o.time < e.time &&
                    e.time > closest &&
                    o.value != 0);

                if (temp == null)
                    continue;

                closest = temp.time;

                if (mapEvent2.value == (int)EventValue.OFF)
                {
                    int idx = events.FindIndex(o =>
                        o.time == mapEvent2.time &&
                        o.value == mapEvent2.value &&
                        o.basicBeatmapEventType == mapEvent2.basicBeatmapEventType);

                    if (idx >= 0)
                        events[idx].time = mapEvent2.time - ((mapEvent2.time - closest) / 2f);
                }
                else
                {
                    if (mapEvent.value == (int)EventValue.OFF ||
                        mapEvent.value == (int)EventValue.BLUE_TRANSITION ||
                        mapEvent.value == (int)EventValue.RED_TRANSITION)
                    {
                        int idx = events.FindIndex(o =>
                            o.time == mapEvent.time &&
                            o.value == mapEvent.value &&
                            o.basicBeatmapEventType == mapEvent.basicBeatmapEventType);

                        if (idx >= 0)
                            events[idx].time = mapEvent.time - ((mapEvent.time - closest) / 2f);
                    }
                    else
                    {
                        int idx = events.FindIndex(o =>
                            o.time == mapEvent.time &&
                            o.value == mapEvent.value &&
                            o.basicBeatmapEventType == mapEvent.basicBeatmapEventType);

                        if (idx >= 0)
                            events.RemoveAt(idx);
                    }
                }
            }

            return events;
        }


        // pick a base color and decide on a style (fade, flash, transition) based on position between first and current note
        private static (EventValue color, float floatValue) FindColor(float first, float current, LightEventType type, System.Random RepeatableRandom, bool random = true)
        {
            EventValue baseColor = EventValue.RED_FADE; // initial placeholder base color

            for (int i = 0; i < ((current - first + Light.ColorOffset) / Light.ColorSwap); i++)
            {
                baseColor = Light.Inverse(baseColor);
            }
            if (first == current) // first color is always blue
            {
                baseColor = EventValue.BLUE_FADE;
            }

            if (random)
            {
                int randomNumber = RepeatableRandom.Next(2);
                baseColor = randomNumber == 0 ? EventValue.BLUE_FADE : EventValue.RED_FADE;
            }
            double chance = RepeatableRandom.NextDouble();
            if (chance < 0.2)
            {
                baseColor = EventValue.ON;
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
                case EventValue.ON:
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
                    //Plugin.LogDebug($"[AutoLightMapper] Adding event for {eventType} at time {time:F3} with new color {color}");
                    eventTempo.Add(EBasicEventData.Create(time, eventType, color)); //, brightnessMultiplier
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

        public static void Shuffle<T>(this IList<T> list, System.Random rng)
        {
            for (int n = list.Count; n > 1; n--)
            {
                int k = rng.Next(n); // 0..n-1
                (list[n - 1], list[k]) = (list[k], list[n - 1]);
            }
        }
        

        /*
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
        */
    }
}
