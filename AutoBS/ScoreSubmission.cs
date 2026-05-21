using AutoBS.Patches;
using BeatmapSaveDataVersion3;
using HarmonyLib;
using HMUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;

namespace AutoBS
{
    internal class ScoreSubmission
    {
        #region Prefix & Postfix - BS-Utils Patch score submission bug
        // Patch BS_Utils score submission disabled banner to show only one line instead of multiple lines and to fix problem of 1st run not showing disabled mod reason
        [HarmonyPatch(typeof(ResultsViewController), "SetDataToUI")]
        [HarmonyAfter("com.kyle1413.BeatSaber.BS-Utils")] // run after BS_Utils
        static class ResultsViewController_SetDataToUI_Fix
        {
            const string LabelName = "AutoBS_NoSubmitLabel";

            static void DestroyAutoBSLabel(GameObject banner)
            {
                if (!banner) return;

                var t = banner.transform.Find(LabelName);
                if (t)
                    UnityEngine.Object.Destroy(t.gameObject);
            }

            static void DestroyAutoBSLabels(GameObject clearedBanner, GameObject failedBanner)
            {
                DestroyAutoBSLabel(clearedBanner);
                DestroyAutoBSLabel(failedBanner);
            }

            static void RemoveBSUtilsScoreSubmissionText(GameObject clearedBanner, GameObject failedBanner)
            {
                RemoveBSUtilsScoreSubmissionTextFromBanner(clearedBanner, false);
                RemoveBSUtilsScoreSubmissionTextFromBanner(failedBanner, true);
            }

            static void RemoveBSUtilsScoreSubmissionTextFromBanner(GameObject bannerGo, bool isFailedBanner)
            {
                if (!bannerGo) return;

                var tmp = bannerGo.GetComponentInChildren<CurvedTextMeshPro>(true);
                if (!tmp) return;

                string text = tmp.text ?? "";

                const string marker = "Score Submission Disabled by:";
                int markerIndex = text.IndexOf(marker, StringComparison.OrdinalIgnoreCase);

                if (markerIndex < 0)
                    return;

                int appendStart = text.LastIndexOf("  \r\n", markerIndex, StringComparison.Ordinal);

                if (appendStart < 0)
                    appendStart = text.LastIndexOf("\r\n", markerIndex, StringComparison.Ordinal);
                if (appendStart < 0)
                    appendStart = text.LastIndexOf("\n", markerIndex, StringComparison.Ordinal);
                if (appendStart < 0)
                    appendStart = markerIndex;

                tmp.text = text.Substring(0, appendStart).TrimEnd();
                tmp.enableWordWrapping = true;

                if (isFailedBanner)
                    tmp.color = Color.red;

                var bg = bannerGo.transform.Find("BG");
                if (bg)
                    bg.gameObject.SetActive(true);
            }

            static void Postfix(ref GameObject ____clearedBannerGo, ref GameObject ____failedBannerGo, ref TextMeshProUGUI ____rankText)
            {
                bool enabled =
                    Config.Instance.EnablePlugin &&
                    Utils.IsEnabledForGeneralFeatures();

                if (!enabled)
                {
                    DestroyAutoBSLabels(____clearedBannerGo, ____failedBannerGo);
                    RemoveBSUtilsScoreSubmissionText(____clearedBannerGo, ____failedBannerGo);
                    return;
                }

                RemoveBSUtilsScoreSubmissionText(____clearedBannerGo, ____failedBannerGo);

                // Debug: confirm gate state at render time
                Plugin.Log.Info($"[ScoreGate] Map Results: IsDisabled={ScoreGate.IsDisabledThisRun} Reason='{ScoreGate.ReasonThisRun}'");

                var host = ____clearedBannerGo.activeInHierarchy ? ____clearedBannerGo : ____failedBannerGo;
                var label = host.transform.Find(LabelName)?.GetComponent<TextMeshProUGUI>();
                if (!ScoreGate.IsDisabledThisRun)
                {
                    DestroyAutoBSLabels(____clearedBannerGo, ____failedBannerGo);
                    return;
                }

                // Ensure a label exists (create once, reuse forever).
                if (!label)
                {
                    var go = new GameObject(LabelName);
                    go.transform.SetParent(host.transform, false);

                    label = go.AddComponent<TextMeshProUGUI>();
                    label.raycastTarget = false;
                    label.textWrappingMode = TextWrappingModes.NoWrap; //label.enableWordWrapping = false;
                    label.alignment = TextAlignmentOptions.Center;
                    label.fontSize = 3.5f;

                    // Place it just under the banner text (or under the rank text as a fallback).
                    var rt = (RectTransform)label.transform;
                    rt.anchorMin = new Vector2(0.5f, 0f);
                    rt.anchorMax = new Vector2(0.5f, 0f);
                    rt.pivot = new Vector2(0.5f, 0f);
                    rt.anchoredPosition = new Vector2(0f, 25f); //35 TEST

                    // If the banner hierarchy is odd on first run, fall back to rankText’s parent
                    if (!label.isActiveAndEnabled && ____rankText && ____rankText.transform is RectTransform rankRT)
                    {
                        rt.SetParent(rankRT.parent, false);
                        rt.anchoredPosition = new Vector2(0f, -30f);
                    }

                    Plugin.LogDebug("[ScoreGate] Created results label");
                }

                // Write a single, clean line every time (no stacking).
                var reason = ScoreGate.ReasonThisRun;
                label.text = string.IsNullOrEmpty(reason)
                    ? "\n<size=200%><color=#ff0000ff>Score submission disabled</color></size>"
                    : $"\n<size=200%><color=#ff0000ff>Score submission disabled:  {reason}</color></size>";
                label.gameObject.SetActive(true);
            }
        }
        #endregion

        #region ScoreGate - Enable/Disable Scoring
        //Clear your flag when you return to menu so it doesn’t carry over
        [HarmonyPatch(typeof(MainFlowCoordinator), "DidActivate")]
        static class ScoreGate_ClearOnMenu
        {
            static void Postfix()
            {
                if (!Config.Instance.EnablePlugin) return;
                if (!Utils.IsEnabledForGeneralFeatures()) return;

                Plugin.LogDebug("[ScoreGate] Clearing on MainFlowCoordinator.DidActivate");
                ScoreGate.Clear();
            }

        }

        public static class ScoreGate
        {
            public static bool IsDisabledThisRun { get; private set; }
            public static string ReasonThisRun { get; private set; } = "";

            public static void Disable(string reason)
            {
                if (string.IsNullOrWhiteSpace(reason))
                    return;

                IsDisabledThisRun = true;
                ReasonThisRun = reason;

                BS_Utils.Gameplay.ScoreSubmission.DisableSubmission(reason);

                Plugin.Log.Info($"[ScoreGate] Score submission disabled using BS_Utils. Reason: '{reason}'");
            }
            // Adds reason for live njs jd events during gameplay after the pipeline has run and already used ScoreGate. this adds a reason at the end of any other reasons and will say "AutoBS-Live NJS" if its the only reason.
            public static void AddReason(string reason)
            {
                if (string.IsNullOrWhiteSpace(reason))
                    return;

                string normalizedReason = reason.StartsWith("AutoBS—")
                    ? reason
                    : "AutoBS—" + reason;

                if (string.IsNullOrWhiteSpace(ReasonThisRun))
                {
                    Disable(normalizedReason);
                    return;
                }

                if (ReasonThisRun.Contains(normalizedReason))
                    return;

                Disable($"{ReasonThisRun}, {normalizedReason}");
            }
            public static void Clear()
            {
                IsDisabledThisRun = false;
                ReasonThisRun = "";
            }
        }
        #endregion
        public static string DetermineScoreSubmissionDisabledReason(EditableCBD eData)
        {
            string str = "";
            /*
            if (TransitionPatcher.SelectedSerializedName == GameModeHelper.GENERATED_360DEGREE_MODE)
            {
                if (Config.Instance.BasedOn != Config.Base.Standard)
                {
                    str = "Base Map Not Standard";
                }
                if (Config.Instance.RotationSpeedMultiplier < 0.3f)
                {
                    str += (str != "" ? " | " : "") + "Rotation Mult Low";
                }
                if (!Config.Instance.Wireless360 && Config.Instance.LimitRotations360 < 90)
                {
                    str += (str != "" ? " | " : "") + "Rotations Limited";
                }
            }
            */
            if (TransitionPatcher.SelectedSerializedName == GameModeHelper.GENERATED_360DEGREE_MODE) // Since 360 settings can be altered, there is no standard for 360 generated maps and so can't score it.
            {
                str = "AutoBS—360fyer";
                return str;
            }

            if (Config.Instance.EnableDiffReducer && TransitionPatcher.DifficultyReducerRemovedNotes)
            {
                str += (str != "" ? " | " : "") + "Auto Diff Reducer";
            }

            if (Mathf.Abs(TransitionPatcher.OriginalNoteJumpMovementSpeed - TransitionPatcher.FinalNoteJumpMovementSpeed) > 0.0001f)
            {
                str += (str != "" ? " | " : "") + "Auto NJS Fixer";
            }

            if (eData.ArcsChanged)
            {
                str += (str != "" ? " | " : "") + "Arcitect Arcs";
            }

            if (eData.ChainsChanged)
            {
                str += (str != "" ? " | " : "") + "Arcitect Chains";
            }

            if (Utils.IsEnabledWalls() && eData.ObstaclesChanged) // since even if auto walls is off, arcitect can alter walls and so we don't want to show a reason for auto walls when its disabled. arcs or chains will disable and worse case my stringent test will find it.
            {
                str += (str != "" ? " | " : "") + "Auto Walls";
            }

            if (Config.Instance.EnableCleanBeatSage && (TransitionPatcher.IsBeatSageMap) && BeatSageCleanUp.DisableScoreSubmission)
            {
                str += (str != "" ? " | " : "") + "Beat Sage Cleaner";
            }

            if (str != "")
                str = "AutoBS—" + str; // prefix once

            return str;
        }
        public static class ScoreStringentBeatmapComparison
        {
            public static bool TryGetScoreDisableReason(
                IReadonlyBeatmapData original,
                IReadonlyBeatmapData final,
                out string reason)
            {
                if (!SameNotes(original, final))
                {
                    reason = "NoteData changed";
                    return true;
                }

                if (!SameObstacles(original, final))
                {
                    reason = "ObstacleData changed";
                    return true;
                }

                if (!SameSliders(original, final, SliderData.Type.Normal))
                {
                    reason = "SliderData Arcs changed";
                    return true;
                }

                if (!SameSliders(original, final, SliderData.Type.Burst))
                {
                    reason = "SliderData Chains changed";
                    return true;
                }

                if (!SameWaypoints(original, final))
                {
                    reason = "WaypointData changed";
                    return true;
                }

                if (!SameRotations(original, final))
                {
                    reason = "Rotation events changed";
                    return true;
                }

                if (!SameBpmEvents(original, final))
                {
                    reason = "BPM events changed";
                    return true;
                }

                if (!SameNjsEvents(original, final))
                {
                    reason = "NJS events changed";
                    return true;
                }
                if (!SameOtherBeatmapObjects(original, final))
                {
                    reason = "Other beatmap objects changed";
                    return true;
                }

                reason = "";
                return false;
            }

            private static bool SameNotes(IReadonlyBeatmapData a, IReadonlyBeatmapData b)
            {
                var x = a.allBeatmapDataItems.OfType<NoteData>()
                    .OrderBy(n => n.time)
                    .ThenBy(n => n.gameplayType)
                    .ThenBy(n => n.colorType)
                    .ThenBy(n => n.lineIndex)
                    .ThenBy(n => n.noteLineLayer)
                    .ThenBy(n => n.cutDirection)
                    .ToList();

                var y = b.allBeatmapDataItems.OfType<NoteData>()
                    .OrderBy(n => n.time)
                    .ThenBy(n => n.gameplayType)
                    .ThenBy(n => n.colorType)
                    .ThenBy(n => n.lineIndex)
                    .ThenBy(n => n.noteLineLayer)
                    .ThenBy(n => n.cutDirection)
                    .ToList();

                if (x.Count != y.Count) return false;

                for (int i = 0; i < x.Count; i++)
                {
                    if (!Same(x[i].time, y[i].time)) return false;
                    if (x[i].gameplayType != y[i].gameplayType) return false;
                    if (x[i].scoringType != y[i].scoringType) return false;
                    if (x[i].colorType != y[i].colorType) return false;
                    if (x[i].lineIndex != y[i].lineIndex) return false;
                    if (x[i].noteLineLayer != y[i].noteLineLayer) return false;
                    if (x[i].cutDirection != y[i].cutDirection) return false;
                    if (x[i].rotation != y[i].rotation) return false;
                }

                return true;
            }

            private static bool SameObstacles(IReadonlyBeatmapData a, IReadonlyBeatmapData b)
            {
                var x = a.allBeatmapDataItems.OfType<ObstacleData>()
                    .OrderBy(o => o.time)
                    .ThenBy(o => o.lineIndex)
                    .ThenBy(o => o.lineLayer)
                    .ThenBy(o => o.duration)
                    .ThenBy(o => o.width)
                    .ThenBy(o => o.height)
                    .ThenBy(o => o.rotation)
                    .ToList();

                var y = b.allBeatmapDataItems.OfType<ObstacleData>()
                    .OrderBy(o => o.time)
                    .ThenBy(o => o.lineIndex)
                    .ThenBy(o => o.lineLayer)
                    .ThenBy(o => o.duration)
                    .ThenBy(o => o.width)
                    .ThenBy(o => o.height)
                    .ThenBy(o => o.rotation)
                    .ToList();

                if (x.Count != y.Count)
                {
                    Plugin.LogDebug($"[ScoreCompare][Obstacle] Count mismatch: original {x.Count} obstacles vs final {y.Count} obstacles");
                    return false;
                }

                for (int i = 0; i < x.Count; i++)
                {
                    if (!Same(x[i].time, y[i].time))
                    {
                        LogObstacleMismatch(i, x[i], y[i], "time");
                        return false;
                    }

                    if (x[i].lineIndex != y[i].lineIndex)
                    {
                        LogObstacleMismatch(i, x[i], y[i], "lineIndex");
                        return false;
                    }

                    if (x[i].lineLayer != y[i].lineLayer)
                    {
                        LogObstacleMismatch(i, x[i], y[i], "lineLayer");
                        return false;
                    }

                    if (!Same(x[i].duration, y[i].duration))
                    {
                        LogObstacleMismatch(i, x[i], y[i], "duration");
                        return false;
                    }

                    if (x[i].width != y[i].width)
                    {
                        LogObstacleMismatch(i, x[i], y[i], "width");
                        return false;
                    }

                    if (x[i].height != y[i].height)
                    {
                        LogObstacleMismatch(i, x[i], y[i], "height");
                        return false;
                    }

                    if (x[i].rotation != y[i].rotation)
                    {
                        LogObstacleMismatch(i, x[i], y[i], "rotation");
                        return false;
                    }
                }

                return true;
            }
            private static void LogObstacleMismatch(int index, ObstacleData original, ObstacleData final, string field)
            {
                Plugin.LogDebug(
                    $"[ScoreCompare][Obstacle] {field} mismatch at index {index}: " +
                    $"original t={original.time:F4} line={original.lineIndex} layer={original.lineLayer} dur={original.duration:F4} w={original.width} h={original.height} rot={original.rotation}; " +
                    $"final t={final.time:F4} line={final.lineIndex} layer={final.lineLayer} dur={final.duration:F4} w={final.width} h={final.height} rot={final.rotation}");
            }
            private static bool SameSliders(IReadonlyBeatmapData a, IReadonlyBeatmapData b, SliderData.Type sliderType)
            {
                var x = a.allBeatmapDataItems.OfType<SliderData>()
                    .Where(s => s.sliderType == sliderType)
                    .OrderBy(s => s.time)
                    .ThenBy(s => s.colorType)
                    .ThenBy(s => s.headLineIndex)
                    .ThenBy(s => s.headLineLayer)
                    .ThenBy(s => s.tailTime)
                    .ThenBy(s => s.tailLineIndex)
                    .ThenBy(s => s.tailLineLayer)
                    .ToList();

                var y = b.allBeatmapDataItems.OfType<SliderData>()
                    .Where(s => s.sliderType == sliderType)
                    .OrderBy(s => s.time)
                    .ThenBy(s => s.colorType)
                    .ThenBy(s => s.headLineIndex)
                    .ThenBy(s => s.headLineLayer)
                    .ThenBy(s => s.tailTime)
                    .ThenBy(s => s.tailLineIndex)
                    .ThenBy(s => s.tailLineLayer)
                    .ToList();

                if (x.Count != y.Count) return false;

                for (int i = 0; i < x.Count; i++)
                {
                    if (!Same(x[i].time, y[i].time)) return false;
                    if (x[i].hasHeadNote != y[i].hasHeadNote) return false;
                    if (x[i].colorType != y[i].colorType) return false;
                    if (x[i].headLineIndex != y[i].headLineIndex) return false;
                    if (x[i].headLineLayer != y[i].headLineLayer) return false;
                    if (x[i].headCutDirection != y[i].headCutDirection) return false;
                    if (!Same(x[i].tailTime, y[i].tailTime)) return false;
                    if (x[i].hasTailNote != y[i].hasTailNote) return false;
                    if (x[i].tailLineIndex != y[i].tailLineIndex) return false;
                    if (x[i].tailLineLayer != y[i].tailLineLayer) return false;
                    if (x[i].tailCutDirection != y[i].tailCutDirection) return false;
                    if (x[i].midAnchorMode != y[i].midAnchorMode) return false;
                    if (x[i].sliceCount != y[i].sliceCount) return false;
                    if (!Same(x[i].squishAmount, y[i].squishAmount)) return false;
                }

                return true;
            }

            private static bool SameWaypoints(IReadonlyBeatmapData a, IReadonlyBeatmapData b)
            {
                var x = a.allBeatmapDataItems.OfType<WaypointData>()
                    .OrderBy(w => w.time)
                    .ThenBy(w => w.lineIndex)
                    .ThenBy(w => w.lineLayer)
                    .ThenBy(w => w.offsetDirection)
                    .ToList();

                var y = b.allBeatmapDataItems.OfType<WaypointData>()
                    .OrderBy(w => w.time)
                    .ThenBy(w => w.lineIndex)
                    .ThenBy(w => w.lineLayer)
                    .ThenBy(w => w.offsetDirection)
                    .ToList();

                if (x.Count != y.Count) return false;

                for (int i = 0; i < x.Count; i++)
                {
                    if (!Same(x[i].time, y[i].time)) return false;
                    if (x[i].lineIndex != y[i].lineIndex) return false;
                    if (x[i].lineLayer != y[i].lineLayer) return false;
                    if (x[i].offsetDirection != y[i].offsetDirection) return false;
                }

                return true;
            }

            private static bool SameRotations(IReadonlyBeatmapData a, IReadonlyBeatmapData b)
            {
                var x = a.allBeatmapDataItems.OfType<BasicBeatmapEventData>()
                    .Where(e => e.basicBeatmapEventType == BasicBeatmapEventType.Event14 ||
                                e.basicBeatmapEventType == BasicBeatmapEventType.Event15)
                    .OrderBy(e => e.time)
                    .ThenBy(e => e.basicBeatmapEventType)
                    .ThenBy(e => e.value)
                    .ToList();

                var y = b.allBeatmapDataItems.OfType<BasicBeatmapEventData>()
                    .Where(e => e.basicBeatmapEventType == BasicBeatmapEventType.Event14 ||
                                e.basicBeatmapEventType == BasicBeatmapEventType.Event15)
                    .OrderBy(e => e.time)
                    .ThenBy(e => e.basicBeatmapEventType)
                    .ThenBy(e => e.value)
                    .ToList();

                if (x.Count != y.Count) return false;

                for (int i = 0; i < x.Count; i++)
                {
                    if (!Same(x[i].time, y[i].time)) return false;
                    if (x[i].basicBeatmapEventType != y[i].basicBeatmapEventType) return false;
                    if (x[i].value != y[i].value) return false;
                }

                return true;
            }

            private static bool SameBpmEvents(IReadonlyBeatmapData a, IReadonlyBeatmapData b)
            {
                var x = a.allBeatmapDataItems.OfType<BpmChangeEventData>()
                    .OrderBy(e => e.beat)
                    .ThenBy(e => e.bpm)
                    .ToList();

                var y = b.allBeatmapDataItems.OfType<BpmChangeEventData>()
                    .OrderBy(e => e.beat)
                    .ThenBy(e => e.bpm)
                    .ToList();

                if (x.Count != y.Count) return false;

                for (int i = 0; i < x.Count; i++)
                {
                    if (!Same(x[i].beat, y[i].beat)) return false;
                    if (!Same(x[i].bpm, y[i].bpm)) return false;
                }

                return true;
            }

            private static bool SameNjsEvents(IReadonlyBeatmapData a, IReadonlyBeatmapData b)
            {
                var x = a.allBeatmapDataItems.OfType<NoteJumpSpeedEventData>()
                    .OrderBy(e => e.time)
                    .ThenBy(e => e.relativeNoteJumpSpeed)
                    .ToList();

                var y = b.allBeatmapDataItems.OfType<NoteJumpSpeedEventData>()
                    .OrderBy(e => e.time)
                    .ThenBy(e => e.relativeNoteJumpSpeed)
                    .ToList();

                if (x.Count != y.Count) return false;

                for (int i = 0; i < x.Count; i++)
                {
                    if (!Same(x[i].time, y[i].time)) return false;
                    if (!Same(x[i].relativeNoteJumpSpeed, y[i].relativeNoteJumpSpeed)) return false;
                }

                return true;
            }
            private static bool SameOtherBeatmapObjects(IReadonlyBeatmapData a, IReadonlyBeatmapData b)
            {
                var x = a.allBeatmapDataItems.OfType<BeatmapObjectData>()
                    .Where(o => !(o is NoteData)
                             && !(o is ObstacleData)
                             && !(o is SliderData))
                    .OrderBy(o => o.GetType().FullName)
                    .ThenBy(o => o.time)
                    .ToList();

                var y = b.allBeatmapDataItems.OfType<BeatmapObjectData>()
                    .Where(o => !(o is NoteData)
                             && !(o is ObstacleData)
                             && !(o is SliderData))
                    .OrderBy(o => o.GetType().FullName)
                    .ThenBy(o => o.time)
                    .ToList();

                if (x.Count != y.Count) return false;

                for (int i = 0; i < x.Count; i++)
                {
                    if (x[i].GetType() != y[i].GetType()) return false;
                    if (!Same(x[i].time, y[i].time)) return false;
                }

                return true;
            }

            private static bool Same(float a, float b)
            {
                return Mathf.Abs(a - b) <= 0.0001f;
            }
        }
    }
}
