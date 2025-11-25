using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoBS
{
    public class UNUSED_RotationBooster
    {
        // --- Tunables (can be moved to Config) ---
        public bool Enabled = true;
        public int WindowSize = 12;                 // how many recent steps to evaluate
        public int TargetAbsPerWindow = 210;         // desired |degrees| across WindowSize notes
        public int BoostLimitMin = 12;               // budget range (in 15° units)
        public int BoostLimitMax = 20;
        public int RestLenMin = 3;                  // how many eligible notes to idle
        public int RestLenMax = 6;
        public int InertiaMin = 10;                  // keep sign for at least this many eligible notes
        public int InertiaMax = 20;

        // --- State ---
        private Queue<int> lastAbs = new Queue<int>();
        private int windowAbsSum = 0;
        private int netCum = 0;          // total since start (± degrees)
        private int biasSign = +1;
        private int inertiaLeft = 0;
        private int boostBudget = 0;
        private int restLeft = 0;
        private Mode mode = Mode.Rest;
        private readonly Random rng;

        private enum Mode { Rest, Boost }

        public UNUSED_RotationBooster(int seed = 1337) // will be repeatable per map. can also use song hash for seed.
        {
            rng = new Random(seed);
        }

        public void ResetForNewMap()
        {
            lastAbs.Clear();
            windowAbsSum = 0;
            netCum = 0;
            biasSign = +1;
            inertiaLeft = 0;
            boostBudget = 0;
            restLeft = 0;
            mode = Mode.Rest;
        }

        public static bool IsDirectionless(NoteCutDirection d) =>
            d == NoteCutDirection.Up ||
            d == NoteCutDirection.Down ||
            d == NoteCutDirection.Any ||
            d == NoteCutDirection.None;

        // Call BEFORE Rotate() — you pass in the note + your current proposed rotationStep;
        // we may nudge rotationStep in-place.
        public void MaybeBoost(ENoteData note, ref int rotationStep, bool eligibleWindow = true)
        {
            if (!Enabled || !eligibleWindow) return;

            // only touch directionless notes
            if (!IsDirectionless(note.cutDirection))
                return;

            // (re)enter boost if idle and rotation density is low
            if (mode == Mode.Rest)
            {
                if (restLeft > 0)
                {
                    restLeft--;
                }
                else if (windowAbsSum < TargetAbsPerWindow)
                {
                    mode = Mode.Boost;
                    // budget is in degrees; we multiply a 15° unit by random span
                    boostBudget = rng.Next(BoostLimitMin, BoostLimitMax + 1) * 15;
                    inertiaLeft = rng.Next(InertiaMin, InertiaMax + 1);
                    biasSign = (netCum >= 0) ? +1 : -1; // keep drifting same way
                }
            }

            if (mode == Mode.Boost && boostBudget > 0)
            {
                int origStep = rotationStep;
                int baseMag = Math.Max(15, RoundTo15(Math.Abs(rotationStep)));
                int desired = biasSign * baseMag;

                // If your current step fights the bias, overwrite it; else ensure magnitude ≥ baseMag
                if (Math.Sign(rotationStep) != biasSign)
                    rotationStep = desired;
                else
                    rotationStep = Math.Sign(rotationStep) * baseMag;

                if (origStep != rotationStep)
                    Plugin.Log.Info($"[RotationBooster] Note time: {note.time} Old Rotation: {origStep} New Rotation: {rotationStep}");


                boostBudget -= Math.Abs(rotationStep);

                if (inertiaLeft > 0)
                {
                    inertiaLeft--;
                }
                else
                {
                    // small chance to flip when net near zero so we don't get stuck forever
                    if (Math.Abs(netCum) < 90 && rng.NextDouble() < 0.15)
                        biasSign = -biasSign;

                    inertiaLeft = rng.Next(InertiaMin, InertiaMax + 1);
                }

                if (boostBudget <= 0)
                {
                    mode = Mode.Rest;
                    restLeft = rng.Next(RestLenMin, RestLenMax + 1);
                }
            }
            else
                Plugin.Log.Info($"[RotationBooster] Boost OFF - Note time: {note.time} Unchanged Rotation: {rotationStep}");
        }

        // Call AFTER Rotate() — tell us what you actually applied
        public void TrackApplied(int appliedStep)
        {
            int a = Math.Abs(appliedStep);
            windowAbsSum += a;
            lastAbs.Enqueue(a);
            if (lastAbs.Count > WindowSize)
                windowAbsSum -= lastAbs.Dequeue();

            netCum += appliedStep;
        }

        private static int RoundTo15(int v)
        {
            // ensure magnitude is a multiple of 15°
            int m = (int)Math.Round(v / 15f);
            return Math.Max(1, m) * 15;
        }
    }
}
