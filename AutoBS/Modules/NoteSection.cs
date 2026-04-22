using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization.Formatters;
using System.Text;
using System.Threading.Tasks;
using UnityEngine.XR;
using static UnityEngine.GraphicsBuffer;

namespace AutoBS.Modules
{
    public class NoteSection
    {
        public List<NoteSwing> Swings { get; private set; }

        public NoteSection()// Parameterless constructor
        {
            this.Swings = new List<NoteSwing>();
        }
        public NoteSection(List<NoteSwing> swings) // Overloaded constructor to accept a list of NoteSwings
        {
            this.Swings = new List<NoteSwing>(swings); // will create new list not a reference or // = swings; will create a reference
        }
        public IEnumerator<NoteSwing> GetEnumerator()// so can use with Foreach loops
        {
            return Swings.GetEnumerator();
        }

        public List<NoteSwing> SwingsA => GetColorSwings(ColorType.ColorA);
        public List<NoteSwing> SwingsB => GetColorSwings(ColorType.ColorB);


        List<ENoteData> _baseNotes = new List<ENoteData>();
        private List<ENoteData> GetBaseNotes()
        {
            _baseNotes = new List<ENoteData>();

            foreach (var swing in Swings)
            {
                _baseNotes.AddRange(swing.GetNotes());
            }
            return _baseNotes;
        }

        private float beatsPerSecond = 0;
        private float secondaryMaxTimeDiff = 0;

        public List<NoteSwing> LowScoreSwings = new List<NoteSwing>(); // list of swings with a low score angle between current and next swing (not previous swing)

        public bool SectionIsEntireSong = false; // used to repair bad swings on entire song end of process.

        public int SwingCount => Swings.Count;

        public float StartTime => Swings.First().Time;
        public float EndTime => Swings.Last().Time;
        public int Index = 0;

        //public List<Tuple<NoteSwing, NoteSwing>> ContrarySwingPairs { get; private set; } = new List<Tuple<NoteSwing, NoteSwing>>();
        //public List<NoteSwing> NonContrarySwings { get; private set; } = new List<NoteSwing>();


        public void AddSwing(NoteSwing swing)
        {
            Swings.Add(swing);
        }
        public void AddSwings(IEnumerable<NoteSwing> swings)
        {
            Swings.AddRange(swings);
        }
        public void RemoveSwingSimple(NoteSwing swing)
        {
            Swings.Remove(swing);
        }
        public void RemoveSwing(NoteSwing swing, List<NoteSwing> swings)
        {
            SetAdjacentSwingsWhenRemovingSwing(swing, swings, false);
            Swings.Remove(swing);
        }

        public IEnumerable<NoteSwing> GetSwings()
        {
            return Swings;
        }
        public List<NoteSwing> GetColorSwings(ColorType colorType)
        {
            return Swings.Where(swing => swing.ColorType == colorType).ToList();
        }
        public void SortSwingsByTime()
        {
            Swings = Swings.OrderBy(swing => swing.FirstNote.time).ToList();
        }

        public float CalculateSectionNps()
        {
            return CalculateSectionNps(Swings);
        }
        public float CalculateSectionNps(List<NoteSwing> swings)
        {
            if (swings.Count == 0) return 0;
            if (swings.Count < 3) return Config.Instance.PreferredFinalNps; // if a section only has 1 swing then we don't want to do anything with it.

            float sectionLength = swings.Last().FirstNote.time - swings.First().FirstNote.time;
            int noteCount = swings.Sum(swing => swing.SwingLength);
            return noteCount / sectionLength;

        }
        public void SimplifySwingsOneByOne()
        {
            SortSwingsByTime();

            var swingsA = Swings.Where(s => s.ColorType == ColorType.ColorA).OrderBy(s => s.Time).ToList();
            var swingsB = Swings.Where(s => s.ColorType == ColorType.ColorB).OrderBy(s => s.Time).ToList();

            float preferredIntervalPerHand = 2f / AutoDifficultyReducer.PreferredNps;
            float preferredIntervalSoloHand = 1f / AutoDifficultyReducer.PreferredNps;

            List<NoteSwing> keptA = SimplifySingleColor(swingsA, swingsB, preferredIntervalPerHand, preferredIntervalSoloHand);
            List<NoteSwing> keptB = SimplifySingleColor(swingsB, swingsA, preferredIntervalPerHand, preferredIntervalSoloHand);

            List<NoteSwing> kept = new List<NoteSwing>(keptA.Count + keptB.Count);
            kept.AddRange(keptA);
            kept.AddRange(keptB);

            Swings = kept
                .Where(s => s != null)
                .Distinct()
                .OrderBy(s => s.Time)
                .ToList();

            SetAdjacentSwings(Swings);
        }
        private List<NoteSwing> SimplifySingleColor(
            List<NoteSwing> colorSwings,
            List<NoteSwing> oppositeColorSwings,
            float preferredIntervalPerHand,
            float preferredIntervalSoloHand)
        {
            List<NoteSwing> kept = new List<NoteSwing>();
            if (colorSwings == null || colorSwings.Count == 0)
                return kept;

            NoteSwing lastKept = colorSwings[0];
            kept.Add(lastKept);

            int index = 1;
            int oppositeIndex = 0;

            while (index < colorSwings.Count)
            {
                index = AdvanceIndexPastTime(colorSwings, index, lastKept.Time);
                oppositeIndex = AdvanceIndexPastTime(oppositeColorSwings, oppositeIndex, lastKept.Time - 0.001f);

                bool soloHandSection = IsSoloHandLocally(
                    lastKept.Time,
                    colorSwings,
                    index,
                    oppositeColorSwings,
                    oppositeIndex);

                float effectivePreferredInterval = soloHandSection ? preferredIntervalSoloHand : preferredIntervalPerHand;

                NoteSwing best = GetBestForwardCandidateSameColor(
                    colorSwings,
                    index,
                    lastKept,
                    kept,
                    effectivePreferredInterval);

                if (best == null)
                    break;

                kept.Add(best);
                lastKept = best;
                index = AdvanceIndexPastTime(colorSwings, index, best.Time);
                /*
                if ((lastKept.Time > 110 && lastKept.Time < 116) || (lastKept.Time > 122 && lastKept.Time < 127))
                {
                    Plugin.LogDebug(
                        $"[DiffReducer][{lastKept.ColorType}] LastKept t:{lastKept.Time:F3} dir:{lastKept.SwingDirection} len:{lastKept.SwingLength}");
                }
                */
            }
            /*
            while (index < colorSwings.Count)
            {
                index = AdvanceIndexPastTime(colorSwings, index, lastKept.Time);
                oppositeIndex = AdvanceIndexPastTime(oppositeColorSwings, oppositeIndex, lastKept.Time - 0.001f);

                bool soloHandSection = IsSoloHandLocally(
                    lastKept.Time,
                    colorSwings,
                    index,
                    oppositeColorSwings,
                    oppositeIndex);

                float effectivePreferredInterval = soloHandSection ? preferredIntervalSoloHand : preferredIntervalPerHand;

                NoteSwing best = GetBestForwardCandidateSameColor(
                    colorSwings,
                    index,
                    lastKept,
                    kept,
                    effectivePreferredInterval);

                if (best == null)
                    break;

                kept.Add(best);
                lastKept = best;
                index = AdvanceIndexPastTime(colorSwings, index, best.Time);

                if ((lastKept.Time > 110 && lastKept.Time < 116) || (lastKept.Time > 122 && lastKept.Time < 127))
                {
                    Plugin.LogDebug(
                        $"[DiffReducer][{lastKept.ColorType}] LastKept t:{lastKept.Time:F3} dir:{lastKept.SwingDirection} len:{lastKept.SwingLength}");
                }
            }
            */

            NoteSwing last = colorSwings[colorSwings.Count - 1];
            if (!kept.Contains(last))
                kept.Add(last);

            return kept;
        }
        private bool IsSoloHandLocally(
            float currentTime,
            List<NoteSwing> sameColorSwings,
            int sameStartIndex,
            List<NoteSwing> oppositeColorSwings,
            int oppositeStartIndex)
        {
            const float lookAheadWindow = 1.4f;

            int sameCount = 0;
            int sameEnd = Math.Min(sameStartIndex + 8, sameColorSwings.Count);
            for (int i = sameStartIndex; i < sameEnd; i++)
            {
                if (sameColorSwings[i].Time - currentTime <= lookAheadWindow)
                    sameCount++;
                else
                    break;
            }

            if (sameCount < 2)
                return false;

            int oppEnd = Math.Min(oppositeStartIndex + 8, oppositeColorSwings.Count);
            for (int i = oppositeStartIndex; i < oppEnd; i++)
            {
                float dt = oppositeColorSwings[i].Time - currentTime;
                if (dt < 0f)
                    continue;
                if (dt > lookAheadWindow)
                    break;

                return false;
            }

            return true;
        }
        /*
        private bool IsSoloHandLocally(float currentTime, List<NoteSwing> oppositeColorSwings, int startIndex)
        {
            // Small local window. Tune as needed.
            const float lookAheadWindow = 1.2f;

            int end = Math.Min(startIndex + 8, oppositeColorSwings.Count);

            for (int i = startIndex; i < end; i++)
            {
                float dt = oppositeColorSwings[i].Time - currentTime;

                if (dt < 0f)
                    continue;

                if (dt > lookAheadWindow)
                    break;

                return false; // opposite hand exists nearby
            }

            return true; // no opposite-hand swings nearby
        }
        */

        private NoteSwing GetBestForwardCandidateSameColor(
            List<NoteSwing> colorSwings,
            int startIndex,
            NoteSwing lastSameColor,
            List<NoteSwing> keptSoFar,
            float preferredInterval)
        {
            if (startIndex >= colorSwings.Count)
                return null;

            NoteSwing firstFuture = colorSwings[startIndex];

            float targetHandNps = 1f / preferredInterval;
            float burstAllowance = 0;// .35f; // how much a single moment may exceed the per - hand nps target -- turned this off for now.
            int DiffReducerBurstWindow = 3; //how many recent kept same - color intervals define the “recent average”
            int burstWindow = Math.Max(1, DiffReducerBurstWindow);

            float recentAverageHandNps = GetRecentAverageHandNps(keptSoFar, burstWindow);
            float firstGap = firstFuture.Time - lastSameColor.Time;

            if (firstGap > 0f)
            {
                float firstCandidateHandNps = 1f / firstGap;

                bool recentAverageIsAtOrBelowTarget =
                    recentAverageHandNps <= 0f || recentAverageHandNps <= targetHandNps;

                bool thisIsOnlyAMildSpike =
                    firstCandidateHandNps <= targetHandNps + burstAllowance;

                if (firstCandidateHandNps <= targetHandNps)
                    return firstFuture;

                if (recentAverageIsAtOrBelowTarget && thisIsOnlyAMildSpike)
                    return firstFuture;
            }

            NoteSwing best = null;
            float bestScore = float.MinValue;

            int end = Math.Min(startIndex + 12, colorSwings.Count);

            for (int i = startIndex; i < end; i++)
            {
                NoteSwing s = colorSwings[i];
                if (s.Time <= lastSameColor.Time)
                    continue;

                float gap = s.Time - lastSameColor.Time;
                float gapRatio = gap / preferredInterval;

                float score = ScoreCandidateSameColor(s, lastSameColor, preferredInterval);

                bool candidateIsDot = s.FirstNote != null && s.FirstNote.cutDirection == NoteCutDirection.Any;
                //bool lastIsDot = lastSameColor.FirstNote != null && lastSameColor.FirstNote.cutDirection == NoteCutDirection.Any;

                //if ((candidateIsDot || lastIsDot) && gapRatio < 0.85f)
                //{
                //    score -= (0.85f - gapRatio) * 140f;
                //}

                if (recentAverageHandNps > targetHandNps && gapRatio < 0.75f)
                {
                    score -= (0.75f - gapRatio) * 120f;
                }
                /*
                if ((s.Time > 110 && s.Time < 116) || (s.Time > 122 && s.Time < 127))
                {
                    Plugin.LogDebug(
                        $"[DiffReducer][Eval][{s.ColorType}] t:{s.Time:F3} gap:{gap:F3} pref:{preferredInterval:F3} ratio:{gapRatio:F2} angle:{lastSameColor.GetAngleBetweenSwings(s)} dot:{candidateIsDot} score:{score:F2}");
                }
                */
                if (score > bestScore)
                {
                    bestScore = score;
                    best = s;
                }
            }

            if (best == null)
                return firstFuture;

            float bestGap = best.Time - lastSameColor.Time;
            float firstFutureGap = firstFuture.Time - lastSameColor.Time;

            bool firstFutureIsVeryTight =
                firstFutureGap < preferredInterval * 0.75f &&
                recentAverageHandNps > targetHandNps;

            if (firstFutureIsVeryTight && best != firstFuture)
                return best;

            return best;
        }
        /*
        private NoteSwing GetBestForwardCandidateSameColor(
            List<NoteSwing> colorSwings,
            int startIndex,
            NoteSwing lastSameColor,
            List<NoteSwing> keptSoFar,
            float preferredInterval)
        {
            if (startIndex >= colorSwings.Count)
                return null;

            NoteSwing firstFuture = colorSwings[startIndex];

            // Real long-term target (per hand)
            float targetHandNps = 1f / preferredInterval;

            int DiffReducerBurstWindow = 3; //how many recent kept same - color intervals define the “recent average”

            // Short-term allowance only for brief spikes
            float burstAllowance = .35f; //how much a single moment may exceed the per - hand nps target
            int burstWindow = Math.Max(1, DiffReducerBurstWindow); 

            float recentAverageHandNps = GetRecentAverageHandNps(keptSoFar, burstWindow);
            float firstGap = firstFuture.Time - lastSameColor.Time;

            if (firstGap > 0f)
            {
                float firstCandidateHandNps = 1f / firstGap;

                bool recentAverageIsAtOrBelowTarget =
                    recentAverageHandNps <= 0f || recentAverageHandNps <= targetHandNps;

                bool thisIsOnlyAMildSpike =
                    firstCandidateHandNps <= targetHandNps + burstAllowance;

                if (recentAverageIsAtOrBelowTarget && thisIsOnlyAMildSpike)
                {
                    if ((firstFuture.Time > 110 && firstFuture.Time < 116) || (firstFuture.Time > 122 && firstFuture.Time < 127))
                    {
                        Plugin.LogDebug(
                            $"[DiffReducer][BurstAllowed][{firstFuture.ColorType}] t:{firstFuture.Time:F3} gap:{firstGap:F3} candNps:{firstCandidateHandNps:F2} targetHandNps:{targetHandNps:F2} recentAvg:{recentAverageHandNps:F2}");
                    }

                    return firstFuture;
                }

                // Allow brief local spike only if the recent average is still behaving.
                if (recentAverageIsAtOrBelowTarget && thisIsOnlyAMildSpike)
                    return firstFuture;

                // Also leave already-slow material alone.
                if (firstCandidateHandNps <= targetHandNps)
                    return firstFuture;
            }

            NoteSwing best = null;
            float bestScore = float.MinValue;

            int end = Math.Min(startIndex + 10, colorSwings.Count);

            for (int i = startIndex; i < end; i++)
            {
                NoteSwing s = colorSwings[i];
                if (s.Time <= lastSameColor.Time)
                    continue;

                float score = ScoreCandidateSameColor(s, lastSameColor, preferredInterval);

                if (score > bestScore)
                {
                    bestScore = score;
                    best = s;
                }
            }
            if ((lastSameColor.Time > 110 && lastSameColor.Time < 116) || (lastSameColor.Time > 122 && lastSameColor.Time < 127))
            {
                Plugin.LogDebug(
                    $"[DiffReducer][CandidateScan][{lastSameColor.ColorType}] last:{lastSameColor.Time:F3} firstFuture:{firstFuture?.Time:F3} preferredInt:{preferredInterval:F3} recentAvgNps:{recentAverageHandNps:F2}");
            }

            return best ?? firstFuture;
        }
        */
        private float GetRecentAverageHandNps(List<NoteSwing> keptSoFar, int intervalCount)
        {
            if (keptSoFar == null || keptSoFar.Count < 2)
                return 0f;

            int startIndex = Math.Max(1, keptSoFar.Count - intervalCount);
            float totalGap = 0f;
            int gapCount = 0;

            for (int i = startIndex; i < keptSoFar.Count; i++)
            {
                float gap = keptSoFar[i].Time - keptSoFar[i - 1].Time;
                if (gap > 0f)
                {
                    totalGap += gap;
                    gapCount++;
                }
            }

            if (gapCount == 0 || totalGap <= 0f)
                return 0f;

            float averageGap = totalGap / gapCount;
            return averageGap > 0f ? 1f / averageGap : 0f;
        }
        private int AdvanceIndexPastTime(List<NoteSwing> swings, int startIndex, float minTimeExclusive)
        {
            while (startIndex < swings.Count && swings[startIndex].Time <= minTimeExclusive)
                startIndex++;

            return startIndex;
        }
        private float AngleScore(int angle)
        {
            if (angle >= 160) return 60f;
            if (angle >= 135) return 45f;
            if (angle >= 112) return 28f;
            if (angle >= 90) return 10f;
            if (angle >= 67) return -18f;
            return -45f;
        }
        private float ScoreCandidateSameColor(
            NoteSwing candidate,
            NoteSwing lastSameColor,
            float preferredInterval)
        {
            float score = 0f;

            float gap = candidate.Time - lastSameColor.Time;
            score -= Math.Abs(gap - preferredInterval) * 30f;

            int angle = lastSameColor.GetAngleBetweenSwings(candidate);
            score += AngleScore(angle);

            if (gap > preferredInterval * 2.5f)
                score -= (gap - preferredInterval * 2.5f) * 25f;

            if (gap < preferredInterval * 0.40f)
                score -= (preferredInterval * 0.40f - gap) * 40f;

            if (candidate.SwingLength > 1)
                score += 12f;

            return score;
        }

        public void     RepairLowScoreSwings() // only removed 1 bad swing and added 2 90deg swings
        {
            // added this!!!!!!!!!!!!!!!!! so change how i created lowscoreswings previously!!!!!!!!!
            LowScoreSwings.Clear();

            foreach (var swing in Swings) // only need protection from deletion not direction alteration (except the 1st note of the song A & B)
            {
                //swing.Protected = false; 
                if (swing.AngleBetweenSwings <= 90 && !swing.FirstNoteOfSong) // will avoid adding the first a and b swings of the song since don't want to change those
                {
                    //if (swing == firstSwingA || swing == firstSwingB)
                    //    swing.Protected = true;

                    LowScoreSwings.Add(swing);

                    // string protection = "";

                    //if (swing.Protected) protection = "***** PROTECTED - ";

                    int prevAngle = swing.PrevSwing?.Angle ?? -1;
                    int nextAngle = swing.NextSwing?.Angle ?? -1;

                    string nextTime = swing.NextSwing?.Time.ToString() ?? "0";

                    var prevSwingDirection = swing.PrevSwing != null ? swing.PrevSwing.SwingDirection.ToString() : "None"; var nextSwingDirection = swing.NextSwing != null ? swing.NextSwing.SwingDirection.ToString() : "None";

                    Plugin.LogDebug(
                        $" ***** Low Score Swing {swing.ColorType} {LowScoreSwings.IndexOf(swing)} {swing.Time:F} {swing.SwingDirection} - prev: {prevSwingDirection} {prevAngle} -- next: {nextTime} {nextSwingDirection} {nextAngle} angleBetween: {swing.AngleBetweenSwings}");

                }
            }

            if (LowScoreSwings.Count > 0)
            {
                foreach (var swing in LowScoreSwings)
                {
                    //Plugin.LogDebug($"[DiffReducer]  ##### +++ will repair this swing:{i} {swing.Time:F} prevAngle: {swing.PrevDirection} - currAngle: {swing.SwingDirection} - nextAngle: {swing.NextDirection}"); // verified

                    // must update the actual Swings list not the lowScoreSwings list
                    int ind = Swings.IndexOf(swing);
                    var oldSwingDirection = Swings[ind].SwingDirection;
                    var oldAngleBetweenSwings = Swings[ind].AngleBetweenSwings;
                    var bestDirection = GetBestDirection(Swings[ind]); // will either return the same direction as original or better. not a different dir that is not better
                                                                       //bool goodSwingDirection = AdditionalSwingExtensions.GoodSwingDirection(Swings[ind], bestDirection);
                                                                       //swing.SetSwingDirection(bestDirection); // change the direction of the swing in lowScoreSwings

                    // Update the angles after modification based on Swings list
                    if (oldSwingDirection != bestDirection) //&& goodSwingDirection)
                    {
                        Swings[ind].SetSwingDirection(bestDirection);
                        SetAdjacentSwings(Swings[ind]);

                        int newAngleBetweenSwings = Swings[ind].AngleBetweenSwings;

                        string repaired = (newAngleBetweenSwings >= 90 && newAngleBetweenSwings > oldAngleBetweenSwings) ? "       " : "! NOT !";


                        int prevAngle = swing.PrevSwing?.Angle ?? -1;
                        int nextAngle = swing.NextSwing?.Angle ?? -1;

                        string nextTime = swing.NextSwing?.Time.ToString() ?? "0";

                        var prevSwingDirection = Swings[ind].PrevSwing != null ? Swings[ind].PrevSwing.SwingDirection.ToString() : "None"; var nextSwingDirection = Swings[ind].NextSwing != null ? Swings[ind].NextSwing.SwingDirection.ToString() : "None";
                        string oldSwingDir = repaired == "       " ? "(old: " + oldSwingDirection.ToString() + ")" : "";

                        //if (Swings[ind].ColorType == ColorType.ColorA)
                        //{
                        Plugin.LogDebug(
                            $" !!!!!!!!!!! Low Score Swing {repaired}Repaired: {Swings[ind].ColorType} {Swings[ind].Time:F} {Swings[ind].SwingDirection} {oldSwingDir} -- prev: {prevSwingDirection} {prevAngle} -- next: {nextTime} {nextSwingDirection} {nextAngle} angleBetween: {Swings[ind].AngleBetweenSwings}");
                        //}
                    }
                    else// if (!goodSwingDirection && oldSwingDirection != bestDirection)
                    {
                        var prevSwingDirection = Swings[ind].PrevSwing != null ? Swings[ind].PrevSwing.SwingDirection.ToString() : "None"; var nextSwingDirection = Swings[ind].NextSwing != null ? Swings[ind].NextSwing.SwingDirection.ToString() : "None";

                        Plugin.LogDebug(
                                $" !!!!!!!!!!! Low Score Swing NOT Repaired: {Swings[ind].ColorType} {Swings[ind].Time:F} {Swings[ind].SwingDirection} -- prev: {prevSwingDirection} -- next: {nextSwingDirection}. Suggested New Direction REJECTED!!: {bestDirection} lineLayer: {(int)Swings[ind].FirstNote.layer} lineIndex: {Swings[ind].FirstNote.line}");

                    }

                    /*
                    if (swing.ColorType == ColorType.ColorA)
                    {
                        int indA = SwingsA.IndexOf(swing);
                        float angle = SwingsA[indA].GetAngleBetweenSwings(SwingsA[indA + 1]);
                        SwingsA[indA].IsBadSwing = angle < 90;
                    }
                    else
                    {
                        int indB = SwingsB.IndexOf(swing);
                        float angle = SwingsB[indB].GetAngleBetweenSwings(SwingsB[indB + 1]);
                        SwingsB[indB].IsBadSwing = angle < 90;
                    }
                    */

                }
                /*
                foreach (var swing in Swings.Where(t => t.Time > 27 && t.Time < 28 && t.ColorType == ColorType.ColorA))
                {
                    Plugin.LogDebug(
                        $" ##### Test after repair {swing.Time:F} prevAngle: {swing.PrevDirection} - currAngle: {swing.SwingDirection} - nextAngle: {swing.NextDirection}");

                }*/
            }
            else
            {
                Plugin.LogDebug($"[DiffReducer]  ---------- Repairing Low Score Swings NOT NEEDED!!!!!!");
            }
        }

        
        //new version that calls good direction inside it
        private NoteCutDirection GetBestDirection(NoteSwing swing)
        {
            if (swing.SwingLength > 1)
            {
                int count = 0;
                foreach (var note in swing.GetNotes())
                {
                    if (note.cutDirection != NoteCutDirection.Any) count++;
                }

                if (count > 1) return swing.SwingDirection;
            }

            int prevAngle = swing.PrevSwing?.Angle ?? -1;
            int nextAngle = swing.NextSwing?.Angle ?? -1;

            if (prevAngle == -1 && nextAngle != -1)
            {
                return GetContraryDirection(nextAngle);
            }
            else if (prevAngle != -1 && nextAngle == -1)
            {
                return GetContraryDirection(prevAngle);
            }
            else if (prevAngle == -1 && nextAngle == -1)
            {
                return swing.SwingDirection; // No change if both are Any
            }

            int targetAngle = (prevAngle + nextAngle) / 2;

            if (Math.Abs(targetAngle - prevAngle) < 90 || Math.Abs(targetAngle - nextAngle) < 90)
            {
                targetAngle = (targetAngle + 180) % 360;
            }

            var directions = new Dictionary<int, NoteCutDirection>
            {
                { 0, NoteCutDirection.Up },
                { 45, NoteCutDirection.UpRight },
                { 90, NoteCutDirection.Right },
                { 135, NoteCutDirection.DownRight },
                { 180, NoteCutDirection.Down },
                { 225, NoteCutDirection.DownLeft },
                { 270, NoteCutDirection.Left },
                { 315, NoteCutDirection.UpLeft },
            };

            var sortedDirections = directions.OrderBy(d => Math.Abs(d.Key - targetAngle));

            Plugin.LogDebug($"[DiffReducer] --- GetBestDirection Start: Target Angle = {targetAngle} for Swing at Time: {swing.Time:F} ---");

            foreach (var dir in sortedDirections)
            {
                int angle = dir.Key;
                NoteCutDirection potentialDirection = dir.Value;

                Plugin.LogDebug($"[DiffReducer] ---- Checking direction {potentialDirection} with angle {angle} (Target Angle: {targetAngle})");

                if (AdditionalSwingExtensions.GoodSwingDirection(swing, potentialDirection))
                {
                    Plugin.LogDebug($"[DiffReducer] ----- Good direction found: {potentialDirection} (Angle: {angle}) for Swing at Time: {swing.Time:F}");
                    return potentialDirection;
                }
                else
                {
                    Plugin.LogDebug($"[DiffReducer] ----- Direction {potentialDirection} is not valid for Swing at Time: {swing.Time:F}");
                }
            }

            Plugin.LogDebug($"[DiffReducer] ---- No better direction found. Keeping original direction: {swing.SwingDirection}");

            // If none of the alternatives are valid, return the original direction
            return swing.SwingDirection;
        }

        private NoteCutDirection GetClosestDirection(int angle)
        {
            var directions = new Dictionary<int, NoteCutDirection>
        {
            { 0, NoteCutDirection.Up },
            { 45, NoteCutDirection.UpRight },
            { 90, NoteCutDirection.Right },
            { 135, NoteCutDirection.DownRight },
            { 180, NoteCutDirection.Down },
            { 225, NoteCutDirection.DownLeft },
            { 270, NoteCutDirection.Left },
            { 315, NoteCutDirection.UpLeft },
        };

            int closestAngle = directions.Keys.Aggregate((x, y) => Math.Abs(x - angle) < Math.Abs(y - angle) ? x : y);
            return directions[closestAngle];
        }
        private int GetClosestAngle(int angle)
        {
            var directions = new Dictionary<int, NoteCutDirection>
        {
            { 0, NoteCutDirection.Up },
            { 45, NoteCutDirection.UpRight },
            { 90, NoteCutDirection.Right },
            { 135, NoteCutDirection.DownRight },
            { 180, NoteCutDirection.Down },
            { 225, NoteCutDirection.DownLeft },
            { 270, NoteCutDirection.Left },
            { 315, NoteCutDirection.UpLeft },
        };

            int closestAngle = directions.Keys.Aggregate((x, y) => Math.Abs(x - angle) < Math.Abs(y - angle) ? x : y);
            return closestAngle;
        }

        private NoteCutDirection GetContraryDirection(int angle)
        {
            var directions = new Dictionary<int, NoteCutDirection>
        {
            { 0, NoteCutDirection.Up },
            { 45, NoteCutDirection.UpRight },
            { 90, NoteCutDirection.Right },
            { 135, NoteCutDirection.DownRight },
            { 180, NoteCutDirection.Down },
            { 225, NoteCutDirection.DownLeft },
            { 270, NoteCutDirection.Left },
            { 315, NoteCutDirection.UpLeft },
        };

            // Flip the angle by 180 degrees
            int contraryAngle = (angle + 180) % 360;

            // Find the closest direction to the contrary angle
            int closestAngle = directions.Keys.Aggregate((x, y) => Math.Abs(x - contraryAngle) < Math.Abs(y - contraryAngle) ? x : y);
            return directions[closestAngle];
        }
        
        private void SetAdjacentSwings(NoteSwing swing) // updates Swings list in most efficient manner
        {
            NoteSwing prevSwing = null;
            NoteSwing nextSwing = null;

            if (swing.ColorType == ColorType.ColorA)
            {
                int index = SwingsA.IndexOf(swing);
                if (index > 0) prevSwing = SwingsA[index - 1];
                if (index < SwingsA.Count - 1) nextSwing = SwingsA[index + 1];
            }
            else
            {
                int index = SwingsB.IndexOf(swing);
                if (index > 0) prevSwing = SwingsB[index - 1];
                if (index < SwingsB.Count - 1) nextSwing = SwingsB[index + 1];
            }

            int swingIndex = Swings.IndexOf(swing);

            if (prevSwing != null)
            {
                Swings[swingIndex].PrevSwing = prevSwing;
                int prevSwingIndex = Swings.IndexOf(prevSwing);
                Swings[prevSwingIndex].NextSwing = swing;
                Swings[prevSwingIndex].AngleBetweenSwings = Swings[prevSwingIndex].GetAngleBetweenSwings(swing);
            }
            if (nextSwing != null)
            {
                Swings[swingIndex].NextSwing = nextSwing;
                int nextSwingIndex = Swings.IndexOf(nextSwing);
                Swings[nextSwingIndex].PrevSwing = swing;
                Swings[swingIndex].AngleBetweenSwings = Swings[swingIndex].GetAngleBetweenSwings(nextSwing);
            }
        }

        private void SetAdjacentSwingsWhenRemovingSwing(NoteSwing swingToRemove, List<NoteSwing> swings, bool test) // updates non-swings list - has to sort into colorA and colorB
        {
            int swingsIndex = 0;

            List<NoteSwing> colorSwings = new List<NoteSwing>();

            if (swingToRemove.ColorType == ColorType.ColorA)
            {
                colorSwings = swings.Where(s => s.ColorType == ColorType.ColorA).ToList();
                swingsIndex = colorSwings.IndexOf(swingToRemove);
            }
            else
            {
                colorSwings = swings.Where(s => s.ColorType == ColorType.ColorB).ToList();
                swingsIndex = colorSwings.IndexOf(swingToRemove);

            }

            int prevIndex = swingsIndex - 1;
            int nextIndex = swingsIndex + 1;

            if (swingsIndex > 0 && swingsIndex < colorSwings.Count - 1)
            {
                colorSwings[prevIndex].NextSwing = colorSwings[nextIndex];
                colorSwings[prevIndex].AngleBetweenSwings = colorSwings[prevIndex].GetAngleBetweenSwings(colorSwings[nextIndex]);

                colorSwings[nextIndex].PrevSwing = colorSwings[prevIndex];

                if (!test)//ColorSwings[SwingsIndex].Time < 6 && ColorSwings[SwingsIndex].Time > 3)
                {
                    Plugin.LogDebug(
                        $" ------------ SetAdjacentSwingsWhenRemovingSwing: Will Remove: {colorSwings[swingsIndex].ColorType} {colorSwings[swingsIndex].Time:F} {colorSwings[swingsIndex].SwingDirection} -- prev: {colorSwings[prevIndex].Time:F} {colorSwings[prevIndex].SwingDirection} - next: {colorSwings[nextIndex].Time:F} {colorSwings[nextIndex].SwingDirection}");
                }
                else if (colorSwings[swingsIndex].Time > 6 && colorSwings[swingsIndex].Time < 7)//ColorSwings[SwingsIndex].Time < 6 && ColorSwings[SwingsIndex].Time > 3)
                {
                    Plugin.LogDebug(
                        $" ------------ SetAdjacentSwingsWhenRemovingSwing: TEST Will Remove: {colorSwings[swingsIndex].ColorType} {colorSwings[swingsIndex].Time:F} {colorSwings[swingsIndex].SwingDirection} -- prev: {colorSwings[prevIndex].Time:F} {colorSwings[prevIndex].SwingDirection} - next: {colorSwings[nextIndex].Time:F} {colorSwings[nextIndex].SwingDirection}");
                }
            }


        }

        public void SetAdjacentSwings(List<NoteSwing> swings) // this updates all swings in a list and can be used on test lists or Swings to update the real list
        {
            List<NoteSwing> swingsA = swings.Where(s => s.ColorType == ColorType.ColorA).ToList();
            List<NoteSwing> swingsB = swings.Where(s => s.ColorType == ColorType.ColorB).ToList();

            UpdateList(swingsA);
            UpdateList(swingsB);

            void UpdateList(List<NoteSwing> colorSwings)
            {
                foreach (var swing in colorSwings)
                {
                    NoteSwing prevSwing = null;
                    NoteSwing nextSwing = null;

                    int index = colorSwings.IndexOf(swing);
                    if (index > 0) prevSwing = colorSwings[index - 1];
                    if (index < colorSwings.Count - 1) nextSwing = colorSwings[index + 1];

                    int swingIndex = swings.IndexOf(swing);

                    if (prevSwing != null)
                    {
                        swings[swingIndex].PrevSwing = prevSwing;
                    }
                    if (nextSwing != null)
                    {
                        swings[swingIndex].NextSwing = nextSwing;
                        swings[swingIndex].AngleBetweenSwings = swings[swingIndex].GetAngleBetweenSwings(nextSwing); // sets the AngleBetweenSwings property
                    }
                }
            }
            /*
            foreach (var swing in swingsA)
            {
                var prevSwingDirection = swing.PrevSwing != null ? swing.PrevSwing.SwingDirection.ToString() : "None"; var nextSwingDirection = swing.NextSwing != null ? swing.NextSwing.SwingDirection.ToString() : "None";

                Plugin.LogDebug(
                    $" ------------ SetAdjacentSwings: {swing.ColorType} {swing.Time:F} {swing.SwingDirection} - prev: {prevSwingDirection} - next: {nextSwingDirection}");

            }
            */
        }
    }
}
