using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CustomJSONData.CustomBeatmap;

namespace AutoBS.Modules
{
    internal class AutoDifficultyReducer
    {
        public static int OriginalNoteCount = 0;
        public static float InitialNps = 0;
        public static float PreferredNps = 0;
        public static float EstimatedFinalNps = 0;
        public static float SongLength = 0;

        public static List<NoteSwing> AllSwings;
        //public static List<NoteSection> HighSpsSections;
        //public static NoteSection LowSpsSection;
        public static List<NoteData> Bombs;

        // if lose both ends always remove whole arc
        public static bool IfDeleteHeadDeleteArc = true; // RemoveArcHead = true → remove whole arc , false → set arc.hasHeadNote = false
        public static bool IfDeleteTailDeleteArc = true; // RemoveArcTail = true → remove whole arc, false → set arc.hasTailNote = false
        public static bool RemoveArcIfDeleteHeadAndTail = true;

        // Use the actual slider type in your project if different.
        public static HashSet<object> ArcsToRemove = new HashSet<object>();

        //public static int SectionSizeWarning = 0;

        public static void InitialCalculations(EditableCBD eData, float bpm, float initialNps)
        {
            AllSwings = new List<NoteSwing>();
            //HighSpsSections = new List<NoteSection>();
            //LowSpsSection = new NoteSection();
            Bombs = new List<NoteData>();
            EstimatedFinalNps = 0;

            ArcsToRemove = new HashSet<object>();

            InitialNps = initialNps;

            var notes = eData.ColorNotes;
            OriginalNoteCount = notes.Count;
            PreferredNps = Config.Instance.PreferredFinalNps;
            SongLength = notes.Last().time;

            var colorANotes = notes.Where(note => note.colorType == ColorType.ColorA).ToList();
            var colorBNotes = notes.Where(note => note.colorType == ColorType.ColorB).ToList();

            List<NoteSwing> colorASwings = CreateSwings(colorANotes, ColorType.ColorA, bpm);
            List<NoteSwing> colorBSwings = CreateSwings(colorBNotes, ColorType.ColorB, bpm);

            AllSwings.AddRange(colorASwings);
            AllSwings.AddRange(colorBSwings);
            AllSwings = AllSwings
                .Where(swing => swing != null && swing.FirstNote != null)
                .OrderBy(swing => swing.FirstNote.time)
                .ToList();

            // crude estimate only; real simplification happens later
            //EstimatedFinalNps = Math.Min(InitialNps, PreferredNps);
            //return (InitialNps, EstimatedFinalNps);
        }

        public static List<ENoteData> SimplifyBeatmap(EditableCBD eData)
        {
            //Plugin.LogDebug($"[AutoDifficultyReducer] --- Simplify One by One ---");

            NoteSection allSwingsSection = new NoteSection();

            foreach (var swing in AllSwings)
            {
                allSwingsSection.AddSwing(swing);
            }

            int originalSwingCount = allSwingsSection.SwingCount;

            Plugin.Log.Info($"[AutoDifficultyReducer] Started: Initial NPS: {InitialNps:F}, Preferred NPS: {PreferredNps:F}, Original Note Count: {OriginalNoteCount}, Original Swing Count: {originalSwingCount}");

            allSwingsSection.SimplifySwingsOneByOne();


            List<ENoteData> newNotes = new List<ENoteData>();
            //newNotes1.AddRange(allSwingsSection.GetSwings().SelectMany(swing => swing.GetNotes()));
            newNotes.AddRange(
                (allSwingsSection?.Swings ?? Enumerable.Empty<NoteSwing>())
                    .Where(swing => swing != null)
                    .SelectMany(swing => swing.GetNotes() ?? Enumerable.Empty<ENoteData>())
            );

            if (OriginalNoteCount != newNotes.Count)
            {
                Plugin.Log.Info($"[AutoDifficultyReducer] Finished: Notes Removed: {OriginalNoteCount - newNotes.Count}, Final Note Count: {newNotes.Count}, Swings Removed: {originalSwingCount - allSwingsSection.SwingCount}, Final Swing Count: {allSwingsSection.SwingCount}");
            }
            else
            {
                Plugin.Log.Info($"[AutoDifficultyReducer] Finished. No notes removed.");
            }
            //loat finalNps = CalculateSongNps(newNotes.Count);
            return newNotes;// (InitialNps, finalNps, newNotes1);
        }

        /*
        public static float CalculateSongNps(int noteCount)//List<BeatmapObjectData> map)
        {
            if (OriginalNoteCount != noteCount)
            {
                Plugin.LogDebug($"[AutoDifficultyReducer] CalculateSongNps(): original noteCount: {OriginalNoteCount} current noteCount: {noteCount} songLength: {SongLength:F} originalNps: {InitialNps:F} currentNps: {noteCount / SongLength:F}");
            }
            else
            {
                Plugin.LogDebug($"[AutoDifficultyReducer] -- CalculateSongNps(): noteCount: {noteCount} songLength: {SongLength:F} currentNps: {noteCount / SongLength}");
            }

            return noteCount / SongLength;
        }
        */
        private static List<NoteSwing> CreateSwings(List<ENoteData> notes, ColorType colorType, float bpm)
        {
            //Plugin.LogDebug($"[AutoDifficultyReducer] CalculateSwings() for {colorType} -----------");
            if (notes.Count == 0)
            {
                Plugin.LogDebug($"[AutoDifficultyReducer] -------- Total Swings: {colorType} 0 -----------");
                return new List<NoteSwing>();
            }

            List<NoteSwing> swings = new List<NoteSwing>();
            NoteSwing swing = new NoteSwing();
            float maxSwingTimeDiff = Math.Min(1f, bpm / 60f / 2f); // bps divided by 2 the original version seemed to catch too many items into a swing esp on nullctrl expl (200bpm) with lots of any dir notes. You can do it - 100bpm works well

            if (notes.Count > 0)
            {
                swing.AddNote(notes.First());
                //Plugin.LogDebug($"[AutoDifficultyReducer]  -- Evaluate Note: time={notes.First().time:F}, cutDirection={notes.First().cutDirection}, lineLayer={(int)notes.First().noteLineLayer}, lineIndex={notes.First().lineIndex}");

                if (notes.Count > 1)
                {
                    for (int i = 1; i < notes.Count; i++)
                    {
                        var note = notes[i];
                        float timeDiff = note.time - swing.FirstNote.time;
                        //Plugin.LogDebug($"[AutoDifficultyReducer]  -- Evaluate Note: time={nextNote.time:F}, cutDirection={nextNote.cutDirection}, lineLayer={(int)nextNote.noteLineLayer}, lineIndex={nextNote.lineIndex}");

                        if (timeDiff <= maxSwingTimeDiff && !note.IsContraryDirectionWithinSwing(swing.FirstNote))
                        {
                            swing.AddNote(note);
                        }
                        else
                        {
                            swings.Add(swing);
                            /*
                            Plugin.LogDebug($"[AutoDifficultyReducer]  ---- Swing {colorType} {swings.Count} {swing.SwingDirection}");
                            foreach (var swingNote in swing.GetNotes())
                            {
                                Plugin.LogDebug(
                                    $" ------ Note: {swingNote.time:F}, {swingNote.cutDirection}, Layer: {(int)swingNote.noteLineLayer}, Index: {swingNote.lineIndex}");
                            }
                            */
                            swing = new NoteSwing();
                            swing.AddNote(note);
                        }
                    }
                }
            }

            if (swing.GetNotes().Any())
            {
                swings.Add(swing);
                /*
                Plugin.LogDebug($"[AutoDifficultyReducer]  ---- Swing {colorType} {swings.Count} {swing.SwingDirection}");
                foreach (var swingNote in swing.GetNotes())
                {
                    Plugin.LogDebug(
                        $" ------ Note: {swingNote.time:F}, {swingNote.cutDirection}, Layer: {(int)swingNote.noteLineLayer}, Index: {swingNote.lineIndex}");
                }
                */
            }

            //TODO change this to SetAdjacentSwings etc - (this is not part of DiffReducer5)
            int countBad = 0;
            int count90 = 0;

            for (int i = 0; i < swings.Count; i++)
            {
                var currentSwing = swings[i];
                NoteCutDirection prevSwingDir = NoteCutDirection.Any; //v11 .None
                if (i > 0)
                {
                    currentSwing.PrevSwing = swings[i - 1];
                    prevSwingDir = swings[i - 1].SwingDirection;
                }
                NoteCutDirection nextSwingDir = NoteCutDirection.Any; //v11 .None
                if (i < swings.Count - 1)
                {
                    currentSwing.NextSwing = swings[i + 1];
                    nextSwingDir = swings[i + 1].SwingDirection;
                }


                currentSwing.AngleBetweenSwings = 180; //v11 didn't set this value at all
                if (currentSwing.NextSwing != null)
                {
                    currentSwing.AngleBetweenSwings = currentSwing.GetAngleBetweenSwings(currentSwing.NextSwing);
                }
                if (currentSwing.AngleBetweenSwings < 90) countBad++;
                if (currentSwing.AngleBetweenSwings == 90) count90++;

                //Plugin.LogDebug($"[AutoDifficultyReducer] ---- Calc Swing: {colorType} {currentSwing.Time} {currentSwing.SwingDirection} prev: {prevSwingDir} next: {nextSwingDir} AngleBetween: {currentSwing.AngleBetweenSwings} BadCount: {countBad},  NinetyCount: {count90}");
            }

            swings[0].Protected = true; // protect first swing of song in each color so (so changes to song don't appear jarring to player)
            swings[0].FirstNoteOfSong = true; //protect first note of entire song in each color



            Plugin.LogDebug($"[AutoDifficultyReducer] -------- Total Swings: {colorType} {swings.Count} Original Bad Swings: {countBad}, Original 90Deg: {count90} -----------");


            return swings;
        }
        public static void ApplyArcAndChainEndpointChanges(EditableCBD eData, List<ENoteData> keptNotes)
        {
            if (eData == null || keptNotes == null)
                return;

            HashSet<ENoteData> keptSet = new HashSet<ENoteData>(keptNotes);

            List<ENoteData> originalNotes = (AllSwings ?? new List<NoteSwing>())
                .Where(s => s != null)
                .SelectMany(s => s.GetNotes() ?? Enumerable.Empty<ENoteData>())
                .Where(n => n != null)
                .Distinct()
                .ToList();

            HashSet<ESliderData> arcsToRemove = new HashSet<ESliderData>();
            HashSet<ESliderData> arcsLoseHead = new HashSet<ESliderData>();
            HashSet<ESliderData> arcsLoseTail = new HashSet<ESliderData>();

            HashSet<ESliderData> chainsToRemove = new HashSet<ESliderData>();

            foreach (ENoteData note in originalNotes)
            {
                if (keptSet.Contains(note))
                    continue;

                // Arc head removed
                if (note.headNoteArc != null)
                {
                    if (IfDeleteHeadDeleteArc)
                        arcsToRemove.Add(note.headNoteArc);
                    else
                        arcsLoseHead.Add(note.headNoteArc);
                }

                // Arc tail removed
                if (note.tailNoteArc != null)
                {
                    if (IfDeleteTailDeleteArc)
                        arcsToRemove.Add(note.tailNoteArc);
                    else
                        arcsLoseTail.Add(note.tailNoteArc);
                }

                // Chain head removed -> always remove chain
                if (note.headNoteChain != null)
                {
                    chainsToRemove.Add(note.headNoteChain);
                }
            }

            // If an arc loses both ends, always remove it
            foreach (ESliderData arc in arcsLoseHead)
            {
                if (arcsLoseTail.Contains(arc))
                    arcsToRemove.Add(arc);
            }

            int originalArcCount = eData.Arcs?.Count ?? 0;
            int originalChainCount = eData.Chains?.Count ?? 0;
            int endpointModifiedCount = 0;

            if (eData.Arcs != null)
            {
                foreach (ESliderData arc in eData.Arcs)
                {
                    if (arc == null || arcsToRemove.Contains(arc))
                        continue;

                    bool changed = false;

                    if (arcsLoseHead.Contains(arc) && arc.hasHeadNote)
                    {
                        arc.hasHeadNote = false;
                        changed = true;
                    }

                    if (arcsLoseTail.Contains(arc) && arc.hasTailNote)
                    {
                        arc.hasTailNote = false;
                        changed = true;
                    }

                    if (changed)
                        endpointModifiedCount++;
                }

                eData.Arcs = eData.Arcs
                    .Where(arc => arc != null && !arcsToRemove.Contains(arc))
                    .ToList();
            }

            if (eData.Chains != null)
            {
                eData.Chains = eData.Chains
                    .Where(chain => chain != null && !chainsToRemove.Contains(chain))
                    .ToList();
            }

            if ((eData.Arcs?.Count ?? 0) != originalArcCount || endpointModifiedCount > 0)
            {
                eData.ArcsChanged = true;
                eData.ArcsChangedByDifficultyReducer = true;
            }

            if ((eData.Chains?.Count ?? 0) != originalChainCount)
            {
                eData.ChainsChanged = true;
                eData.ChainsChangedByDifficultyReducer = true;
            }

            Plugin.LogDebug(
                $"[AutoDifficultyReducer] Arc/Chain cleanup: removedArcs {originalArcCount - (eData.Arcs?.Count ?? 0)}, modifiedArcs {endpointModifiedCount}, removedChains {originalChainCount - (eData.Chains?.Count ?? 0)}");

        }
    }
}
