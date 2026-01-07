using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization.Formatters;
using System.Text;
using System.Threading.Tasks;

namespace AutoBS
{
    internal static class ColorBoostGenerator
    {
        public static void Generate(EditableCBD eData) // this version toggles true on tempo changes and next tempo change turns it false
        {
            if (eData.MapAlreadyUsesEnvColorBoost)
                return;

            if (eData.ColorBoostEvents.Count > 0)
                return;

            if (eData.ColorNotes.Count < 3)
                return;

            // Ensure sorted
            List<ENoteData> notes = eData.ColorNotes.OrderBy(n => n.time).ToList();


            // Detector parameters (tune these)
            int windowSize = 4; //the number of adjacent note intervals used on each side of a point to estimate local tempo when detecting a tempo change. For each candidate position, you compare: 1the median of the previous 4 note - to - note intervals, and 2 the median of the next 4 note - to - note intervals.
            float ratioThresholdBase = 1.8f; //2.0 had 36 toggles, 1.5 had 90 on exp developing world
            float cooldownSecondsBase = 2.0f;
            float minIntervalSeconds = 0.08f;
            float offHoldMinBase = 2.0f;  // minimum time to stay OFF before we allow next ON
            float offHoldMaxBase = 10.0f;  // maximum time to stay OFF before we allow next ON
            // Optional: prevent ultra-short ON states (sometimes feels nicer).
            float onHoldMinBase = 0.5f;   // minimum time to stay ON before we allow turning OFF (set 0 to disable)
            float onHoldMaxBase = 8.0f; // seconds max ON duration


            // Multiplier User Knob ---------------------------------------------
            var mult = Config.Instance.BoostLightingMultiplier;
            
            
            
            
            // --- Scaled values ---
            // 1) Make detection easier as m increases:
            // Lower threshold => more detections
            float ratioThreshold = ratioThresholdBase * InvPow(mult, 0.35f);
            // Clamp so it doesn't get ridiculous
            ratioThreshold = Clamp(ratioThreshold, 1.25f, 3.0f);

            // 2) Allow more change points as m increases:
            float cooldownSeconds = cooldownSecondsBase * InvPow(mult, 0.50f);
            cooldownSeconds = Clamp(cooldownSeconds, 0.35f, 6.0f);

            // 3) Make OFF hold shorter as m increases (big driver of event density):
            float offHoldMin = offHoldMinBase * InvPow(mult, 0.85f);
            float offHoldMax = offHoldMaxBase * InvPow(mult, 0.85f);

            // Keep ordering sane and prevent too-low holds
            offHoldMin = Clamp(offHoldMin, 0.25f, 30.0f);
            offHoldMax = Clamp(offHoldMax, offHoldMin + 0.1f, 60.0f);

            // 4) Keep ON hold mostly stable (or slightly increase at high m to avoid chatter)
            float onHoldMin = onHoldMinBase * (float)System.Math.Pow(mult, 0.10f);
            onHoldMin = Clamp(onHoldMin, 0.0f, 2.0f);

            // 5) Make ON hold shorter as mult decreases
            float onHoldMax = onHoldMaxBase * (float)System.Math.Pow(mult, 0.5f);
            onHoldMax = Clamp(onHoldMax, 3.0f, 30.0f);

            int seed = Config.Instance.BoostLightingRandomSeed;
            var rng = new System.Random(seed);

            // Initial “OFF hold” before first ON
            float nextOnAllowedTime = notes[0].time + NextRange(rng, offHoldMin, offHoldMax);
            float nextOffAllowedTime = float.NegativeInfinity;

            float onStartTime = float.NegativeInfinity;

            float firstNoteTime = notes[0].time;
            nextOnAllowedTime = firstNoteTime + NextRange(rng, offHoldMin, offHoldMax);


            List<int> changeIdx = FindTempoChangeIndices(
                notes,
                windowSize,
                ratioThreshold,
                cooldownSeconds,
                minIntervalSeconds
            );

            if (changeIdx.Count == 0)
                return;

            bool boostOn = false;


            for (int k = 0; k < changeIdx.Count; k++)
            {
                int idx = changeIdx[k];
                if (idx < 0 || idx >= notes.Count)
                    continue;

                float t = notes[idx].time;

                if (!boostOn)
                {
                    if (t < nextOnAllowedTime)
                        continue;

                    eData.ColorBoostEvents.Add(EColorBoostEvent.Create(t, true));
                    boostOn = true;
                    onStartTime = t;

                    float dur = t;
                    if (eData.ColorBoostEvents.Count > 1)
                        dur = t - eData.ColorBoostEvents[eData.ColorBoostEvents.Count - 2].time;
                    //Plugin.LogDebug($"[ColorBoostGeneratorModule] Event {t:F2} ON (OFF Dur: {dur:F2})");

                    nextOffAllowedTime = t + onHoldMin;
                }
                else
                {
                    // Enforce max ON duration by injecting OFF at the first note at/after onStartTime + onHoldMax
                    float hardOffTimeTarget = onStartTime + onHoldMax;

                    if (t >= hardOffTimeTarget)
                    {
                        float offT = FindFirstNoteTimeAtOrAfter(notes, hardOffTimeTarget);

                        // Prevent out-of-order injection relative to last event
                        float lastEventTime = eData.ColorBoostEvents[eData.ColorBoostEvents.Count - 1].time;
                        if (offT <= lastEventTime)
                            offT = t; // fallback

                        eData.ColorBoostEvents.Add(EColorBoostEvent.Create(offT, false));
                        boostOn = false;

                        float dur = offT - lastEventTime;
                        //Plugin.LogDebug($"[ColorBoostGeneratorModule] Event {offT:F2} OFF (ON Dur: {dur:F2}) [CAP {onHoldMax:F2}s]");

                        float offHold = NextRange(rng, offHoldMin, offHoldMax);
                        nextOnAllowedTime = offT + offHold;

                        // We just forced OFF; continue scanning later change points
                        continue;
                    }

                    // Normal OFF on a tempo change, but not before min ON hold
                    if (t < nextOffAllowedTime)
                        continue;

                    eData.ColorBoostEvents.Add(EColorBoostEvent.Create(t, false));
                    boostOn = false;

                    float dur2 = t - eData.ColorBoostEvents[eData.ColorBoostEvents.Count - 2].time;
                    //Plugin.LogDebug($"[ColorBoostGeneratorModule] Event {t:F2} OFF (ON Dur: {dur2:F2})");

                    float offHold2 = NextRange(rng, offHoldMin, offHoldMax);
                    nextOnAllowedTime = t + offHold2;
                }
            }


            eData.ColorBoostEvents = eData.ColorBoostEvents.OrderBy(e => e.time).ToList();

            eData.ColorBoostEventsChanged = false;

            if (eData.ColorBoostEvents.Count > 0)
                eData.ColorBoostEventsChanged = true;

            Plugin.LogDebug($"[ColorBoostGeneratorModule] Events count: {eData.ColorBoostEvents.Count} BoostLightingMultiplier={mult:0.00} ratioThreshold={ratioThreshold:0.00}, cooldownSeconds={cooldownSeconds:0.00}, offHoldMin={offHoldMin:0.00}, offHoldMax={offHoldMax:0.00}, onHoldMin={onHoldMin:0.00}, onHoldMax={onHoldMax:0.00}");
        }

        private static float FindFirstNoteTimeAtOrAfter(List<ENoteData> notes, float time)
        {
            int lo = 0, hi = notes.Count - 1;
            while (lo < hi)
            {
                int mid = (lo + hi) / 2;
                if (notes[mid].time < time) lo = mid + 1;
                else hi = mid;
            }

            // If all notes are before "time", return time (end-of-map edge case)
            return notes[lo].time < time ? time : notes[lo].time;
        }



        internal static float NextRange(System.Random rng, float min, float max)
        {
            if (max <= min) return min;
            return min + (float)rng.NextDouble() * (max - min);
        }

        internal static List<int> FindTempoChangeIndices(
            List<ENoteData> notesSorted,
            int windowSize,
            float ratioThreshold,
            float minCooldownSeconds,
            float minIntervalSeconds)
        {
            List<int> indices = new List<int>();
            if (notesSorted.Count < windowSize * 2 + 2)
                return indices;

            float lastEmitTime = -999f;

            float[] intervals = new float[notesSorted.Count - 1];
            for (int i = 0; i < notesSorted.Count - 1; i++)
                intervals[i] = notesSorted[i + 1].time - notesSorted[i].time;

            for (int center = windowSize; center < intervals.Length - windowSize; center++)
            {
                float prevMed = Median(intervals, center - windowSize, windowSize);
                float nextMed = Median(intervals, center, windowSize);

                if (prevMed < minIntervalSeconds || nextMed < minIntervalSeconds)
                    continue;

                float ratio = prevMed > nextMed ? (prevMed / nextMed) : (nextMed / prevMed);

                if (ratio >= ratioThreshold)
                {
                    float t = notesSorted[center].time;
                    if (t - lastEmitTime >= minCooldownSeconds)
                    {
                        indices.Add(center);
                        lastEmitTime = t;
                    }
                }
            }

            return indices;
        }

        private static float Median(float[] arr, int start, int count)
        {
            // small window => simple copy+sort is fine
            float[] tmp = new float[count];
            for (int i = 0; i < count; i++)
                tmp[i] = arr[start + i];

            Array.Sort(tmp);

            int mid = count / 2;
            if ((count & 1) == 1)
                return tmp[mid];

            return (tmp[mid - 1] + tmp[mid]) * 0.5f;
        }

        // for user controlled multiplier
        private static float Clamp(float v, float lo, float hi) => v < lo ? lo : (v > hi ? hi : v);
        // for user controlled multiplier
        private static float Lerp(float a, float b, float t) => a + (b - a) * t;

        // for user controlled multiplier
        // m=1 => scale=1
        // m>1 => scale decreases (more events) when used as divisor
        private static float InvPow(float m, float exp)
        {
            // exp > 0: m=2 => ~0.707 (for exp=0.5), m=4 => 0.5
            return (float)System.Math.Pow(m, -exp);
        }
    }
}
