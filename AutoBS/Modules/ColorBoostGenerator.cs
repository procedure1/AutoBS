using System;
using System.Collections.Generic;
using System.Linq;
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
            int windowSize = 4;
            float ratioThreshold = 1.8f; //2.0 had 36 toggles, 1.5 had 90 on exp developing world
            float cooldownSeconds = 2.0f;
            float minIntervalSeconds = 0.08f;

            List<int> changeIdx = FindTempoChangeIndices(
                notes,
                windowSize,
                ratioThreshold,
                cooldownSeconds,
                minIntervalSeconds
            );

            if (changeIdx.Count == 0)
                return;

            // NEW: Make OFF last longer than ON
            // -------------------------------
            // These are in the same unit as notes[].time (beats if your map times are beats).
            // Tune to taste.
            float offHoldMin = 2.0f;  // minimum time to stay OFF before we allow next ON
            float offHoldMax = 10.0f;  // maximum time to stay OFF before we allow next ON

            // Optional: prevent ultra-short ON states (sometimes feels nicer).
            float onHoldMin = 0.5f;   // minimum time to stay ON before we allow turning OFF (set 0 to disable)

            int seed = 242;
            var rng = new System.Random(seed); // fixed seed for consistent results

            bool boostOn = false;               // start OFF so the first ON feels “special”
            float nextOnAllowedTime = float.NegativeInfinity;
            float nextOffAllowedTime = float.NegativeInfinity;

            for (int k = 0; k < changeIdx.Count; k++)
            {
                int idx = changeIdx[k];
                if (idx < 0 || idx >= notes.Count)
                    continue;

                float t = notes[idx].time;

                if (!boostOn)
                {
                    // OFF state: ignore change points until we've waited long enough.
                    if (t < nextOnAllowedTime)
                        continue;

                    eData.ColorBoostEvents.Add(EColorBoostEvent.Create(t, true));
                    boostOn = true;

                    Plugin.LogDebug($"[ColorBoostGeneratorModule] Event {t:F} ON");

                    // Once ON, optionally require a minimum ON duration before allowing OFF.
                    nextOffAllowedTime = t + onHoldMin;
                }
                else
                {
                    // ON state: we still turn OFF at a tempo change, but not before the minimum ON hold.
                    if (t < nextOffAllowedTime)
                        continue;

                    eData.ColorBoostEvents.Add(EColorBoostEvent.Create(t, false));
                    boostOn = false;

                    float dur = t - eData.ColorBoostEvents[eData.ColorBoostEvents.Count - 2].time;
                    Plugin.LogDebug($"[ColorBoostGeneratorModule] Event {t:F} OFF (Dur: {dur:F})");

                    // After turning OFF, enforce a "seemingly random" waiting period before we allow ON again.
                    float offHold = NextRange(rng, offHoldMin, offHoldMax);

                    // (Optional) add a little extra “randomness” that still tends to be longer than shorter:
                    // offHold = offHoldMin + (offHoldMax - offHoldMin) * (float)Math.Pow(rng.NextDouble(), 0.65);

                    nextOnAllowedTime = t + offHold;
                }
            }

            eData.ColorBoostEvents = eData.ColorBoostEvents.OrderBy(e => e.time).ToList();

            eData.ColorBoostEventsChanged = false;

            if (eData.ColorBoostEvents.Count > 0)
                eData.ColorBoostEventsChanged = true;

            Plugin.LogDebug("[ColorBoostGeneratorModule] Events count: " + eData.ColorBoostEvents.Count);
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



        // UNUSED ----------------------------------------------------------------

        public static void GenerateSimple(EditableCBD eData)
        {
            if (eData.MapAlreadyUsesEnvColorBoost)
                return;

            if (eData.ColorBoostEvents.Count > 0)
                return;

            if (eData.ColorNotes.Count == 0)
                return;

            eData.ColorNotes = eData.ColorNotes.OrderBy(n => n.time).ToList();

            const float eps = 0.0005f;

            int boostIteration = 0;
            bool boostOn = true;

            // Initialize with the first note time and count it immediately. This ensures the first unique timestamp increments boostIteration.
            // Without this, iteration would start at the *second* note time and all boost placements (24 / 29 / 33) would be shifted later.
            float lastTime = eData.ColorNotes[0].time;
            SetBoost(lastTime);

            for (int i = 1; i < eData.ColorNotes.Count; i++)
            {
                float t = eData.ColorNotes[i].time;

                // Same-time notes count once
                if (Math.Abs(t - lastTime) <= eps)
                    continue;

                lastTime = t;
                SetBoost(t);
            }

            void SetBoost(float time)
            {
                boostIteration++;

                if (boostIteration == 24 || boostIteration == 29)
                {
                    eData.ColorBoostEvents.Add(
                        EColorBoostEvent.Create(time, boostOn)
                    );
                    boostOn = !boostOn;
                }

                if (boostIteration == 33)
                    boostIteration = 0;
            }

            if (eData.ColorBoostEvents.Count > 1)
                eData.ColorBoostEvents =
                    eData.ColorBoostEvents.OrderBy(e => e.time).ToList();

            Plugin.LogDebug($"[ColorBoostGeneratorModule] Generated {eData.ColorBoostEvents.Count} color boost events. 1st Event: {eData.ColorBoostEvents[0].time:F} {eData.ColorBoostEvents[0].boostColorsAreOn} 2nd Event: {eData.ColorBoostEvents[1].time:F} {eData.ColorBoostEvents[1].boostColorsAreOn}");
        }
        private static void ExecuteWithTimedOff(EditableCBD eData)
        {
            if (eData.MapAlreadyUsesEnvColorBoost)
                return;

            if (eData.ColorBoostEvents.Count > 0)
                return;

            if (eData.ColorNotes.Count < 15)
                return;

            var notes = eData.ColorNotes = eData.ColorNotes.OrderBy(n => n.time).ToList();

            List<int> changeIndices =
                FindTempoChangeIndices(
                    notes,
                    windowSize: 4,
                    ratioThreshold: 2.0f,
                    minCooldownSeconds: 2.0f,
                    minIntervalSeconds: 0.08f
                );


            if (changeIndices.Count == 0)
                return;

            for (int i = 0; i < changeIndices.Count; i++)
            {
                int noteIndex = changeIndices[i];
                float t = notes[noteIndex].time;

                float boostDuration = ComputeBoostDuration(
                    notes,
                    noteIndex,
                    minDur: 0.8f,
                    maxDur: 2.5f
                );

                eData.ColorBoostEvents.Add(EColorBoostEvent.Create(t, true));
                eData.ColorBoostEvents.Add(EColorBoostEvent.Create(t + boostDuration, false));
            }

            eData.ColorBoostEvents = eData.ColorBoostEvents.OrderBy(e => e.time).ToList();

            Plugin.LogDebug($"[ColorBoostGeneratorModule] Tempo Generated {eData.ColorBoostEvents.Count} color boost events. 1st Event: {eData.ColorBoostEvents[0].time:F} {eData.ColorBoostEvents[0].boostColorsAreOn} 2nd Event: {eData.ColorBoostEvents[1].time:F} {eData.ColorBoostEvents[1].boostColorsAreOn}");
            foreach (var evt in eData.ColorBoostEvents)
            {
                Plugin.LogDebug($"    Event at {evt.time:F}, boostOn={evt.boostColorsAreOn}");
            }
        }

        private static float ComputeBoostDuration(
            List<ENoteData> notes,
            int noteIndex,
            float minDur,
            float maxDur)
        {
            // Average spacing of nearby notes
            float sum = 0f;
            int count = 0;

            int start = Math.Max(0, noteIndex - 2);
            int end = Math.Min(notes.Count - 2, noteIndex + 2);

            for (int i = start; i <= end; i++)
            {
                float dt = notes[i + 1].time - notes[i].time;
                if (dt > 0.001f)
                {
                    sum += dt;
                    count++;
                }
            }

            if (count == 0)
                return minDur;

            float avg = sum / count;

            // Map spacing → duration
            float dur = avg * 4.0f; // musical multiplier

            return Clamp(dur, minDur, maxDur);
        }

        private static float Clamp(float v, float min, float max)
        {
            if (v < min) return min;
            if (v > max) return max;
            return v;
        }
    }
}
