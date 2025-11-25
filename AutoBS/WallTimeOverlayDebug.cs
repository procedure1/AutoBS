using AutoBS.Patches;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Remoting.Metadata.W3cXsd2001;
using TMPro;
using UnityEngine;

namespace AutoBS
{
    public class WallTimeOverlayDebug : MonoBehaviour
    {
        // -------- Shared / timing --------
        private AudioTimeSyncController _ats;
        private float _bpm = TransitionPatcher.bpm;

        // -------- Walls (event-driven preferred) --------
        private Component _bom;            // BeatmapObjectsManager/BeatmapObjectManager instance
        private Type _bomType;
        private Delegate _wallSpawnDelegate;
        private int _wallSpawnIndex = 0;

        // fallback scanner
        private bool _usingScannerFallback = false;
        private float _nextScan;
        private readonly HashSet<ObstacleController> _scannedWalls = new HashSet<ObstacleController>();

        // -------- Notes (didInitEvent-driven) --------
        //private readonly HashSet<NoteController> _hookedNotes = new HashSet<NoteController>();
        private readonly Dictionary<NoteController, int> _noteIds = new Dictionary<NoteController, int>();
        private readonly Dictionary<ObstacleController, int> _fallbackIds = new Dictionary<ObstacleController, int>(); 
        private int _fallbackCounter = 0;
        private int _noteCounter = 0;

        private void Start()
        {
            return;// DISABLED FOR RELEASE


            
            Debug.Log("[WallTimeOverlayDebug] Start()");
            _ats = FindATS();
            TryReadBpmFromATS();

            // 🔸 enable fallback scanner *immediately* so we don't miss walls
            EnableScannerFallback();

            // keep trying to hook the true spawn event in parallel
            StartCoroutine(HookWallSpawnEvent());
            StartCoroutine(HookNotesRoutine());
        }

        // The scanner Update stays the same EXCEPT we give each pooled OC its own ID while scanning
        private void Update()
        {
            if (!_usingScannerFallback) return;

            if (Time.unscaledTime < _nextScan) return;
            _nextScan = Time.unscaledTime + 0.25f;

            var obs = FindObjectsOfType<ObstacleController>();
            foreach (var oc in obs)
            {
                if (oc == null || _scannedWalls.Contains(oc)) continue;
                _scannedWalls.Add(oc);

                // assign a fallback ID for this *spawn* (handles pooling)
                _fallbackCounter++;
                _fallbackIds[oc] = _fallbackCounter;

                AttachWallLabel_Plain(oc, _fallbackIds[oc], fromFallback: true);

                // when the wall finishes, allow relabel on reuse
                System.Action<ObstacleController> cleanup = null;
                cleanup = _ =>
                {
                    _scannedWalls.Remove(oc);
                    _fallbackIds.Remove(oc);
                    oc.finishedMovementEvent -= cleanup;
                };
                oc.finishedMovementEvent += cleanup;
            }
        }


        private void OnDestroy()
        {
            if (_bom != null && _wallSpawnDelegate != null && _bomType != null)
            {
                var evt = _bomType.GetEvent("obstacleWasSpawnedEvent", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                          ?? _bomType.GetEvent("obstacleDidStartMovementEvent", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                evt?.RemoveEventHandler(_bom, _wallSpawnDelegate);
                _wallSpawnDelegate = null;
                Debug.Log("[WallTimeOverlayDebug] Unsubscribed wall spawn event");
            }
        }

        // ----------------------- Audio / BPM helpers -----------------------

        private AudioTimeSyncController FindATS()
        {
            var found = Resources.FindObjectsOfTypeAll<AudioTimeSyncController>().FirstOrDefault()
                        ?? FindObjectOfType<AudioTimeSyncController>();
            Debug.Log(found != null
                ? "[WallTimeOverlayDebug] Found AudioTimeSyncController"
                : "[WallTimeOverlayDebug] AudioTimeSyncController NOT found (will keep trying via other paths)");
            return found;
        }

        private void TryReadBpmFromATS()
        {
            if (_ats == null)
            {
                Debug.Log("[WallTimeOverlayDebug] ATS null; BPM stays default 120");
                return;
            }
            try
            {
                var bpmProp = typeof(AudioTimeSyncController).GetProperty("beatsPerMinute",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (bpmProp != null)
                {
                    var val = bpmProp.GetValue(_ats);
                    if (val is float f && f > 0f) _bpm = f;
                }
                else
                {
                    var bpmField = typeof(AudioTimeSyncController).GetField("_beatsPerMinute",
                        BindingFlags.Instance | BindingFlags.NonPublic);
                    if (bpmField?.GetValue(_ats) is float ff && ff > 0f) _bpm = ff;
                }
            }
            catch { /* keep default */ }

            Debug.Log($"[WallTimeOverlayDebug] BPM set to {_bpm:F3}");
        }

        // ----------------------- WALLS -----------------------

        // When we successfully hook the true spawn event, disable scanner
        private IEnumerator HookWallSpawnEvent()
        {
            Debug.Log("[WallTimeOverlayDebug] HookWallSpawnEvent() begin");

            // ... your existing manager find code ...

            if (_bom == null)
            {
                Debug.Log("[WallTimeOverlayDebug] Could not find manager (scanner stays enabled)");
                yield break; // keep fallback running
            }

            // ... pick event, create delegate ...

            // Pick an event name that exists
            var evt = _bomType.GetEvent("obstacleWasSpawnedEvent", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                      ?? _bomType.GetEvent("obstacleDidStartMovementEvent", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if (evt == null)
            {
                Debug.Log($"[WallTimeOverlayDebug] No obstacle spawn event on {_bomType.FullName} → keeping fallback scanner");
                yield break;
            }

            Debug.Log($"[WallTimeOverlayDebug] Using event {_bomType.Name}.{evt.Name}");

            // Create delegate for our handler
            var mi = GetType().GetMethod(nameof(OnWallSpawned_Reflected), BindingFlags.Instance | BindingFlags.NonPublic);
            if (mi == null)
            {
                Debug.Log("[WallTimeOverlayDebug] Handler method not found!? → keeping fallback scanner");
                yield break;
            }

            try
            {
                _wallSpawnDelegate = Delegate.CreateDelegate(evt.EventHandlerType, this, mi);
                evt.AddEventHandler(_bom, _wallSpawnDelegate);
                Debug.Log("[WallTimeOverlayDebug] Subscribed to spawn event OK");

                // 🔸 switch off fallback now that we have a solid hook
                _usingScannerFallback = false;
                _scannedWalls.Clear();
                _fallbackIds.Clear();
                Debug.Log("[WallTimeOverlayDebug] Fallback scanner disabled (using stable event ordering)");
            }
            catch (Exception ex)
            {
                Debug.Log("[WallTimeOverlayDebug] Failed to subscribe spawn event: " + ex);
                // leave fallback on
            }

        }

        private void LogWall(ObstacleController oc, int wallNumber, bool fromFallback)
        {
            try
            {
                var data = oc?.obstacleData;
                float beat = data?.time ?? 0f;
                float sec = (_bpm > 0f) ? (beat * 60f / _bpm) : 0f;

                if (data?.height != 1001 && data?.height != 1300 && (int)(data?.lineLayer) < 6 && data?.lineIndex >= -6 && data?.lineIndex <= 6)
                {
                    Debug.Log(
                        $"[WallTimeOverlayDebug] Wall #{wallNumber} " +
                        $"@ {sec:F3}s (beat≈{beat:F3}) " +
                        $"line={data?.lineIndex} layer={(int)(data?.lineLayer)} " +
                        $"width={data?.width} rot= {data?.rotation} height={data?.height} dur={data?.duration:F3}"
                        );
                        }
                }
            catch (Exception ex)
            {
                Debug.Log("[WallTimeOverlayDebug] LogWall error: " + ex);
            }
        }


        private void EnableScannerFallback()
        {
            if (_usingScannerFallback) return;
            _usingScannerFallback = true;
            Debug.Log("[WallTimeOverlayDebug] FALLBACK scanner enabled (will label walls via polling)");
        }

        // Event signature usually: Action<ObstacleController>
        private void OnWallSpawned_Reflected(object ocObj)
        {
            var oc = ocObj as ObstacleController;
            if (oc == null) return;

            _wallSpawnIndex++; // stable event ordering
            LogWall(oc, _wallSpawnIndex, fromFallback: false);

            AttachWallLabel_Plain(oc, _wallSpawnIndex, fromFallback: false);
        }

        // Label helper: add a tiny suffix so you can tell which path produced it
        private void AttachWallLabel_Plain(ObstacleController oc, int wallNumber, bool fromFallback)
        {
            // NEW: log even when we’re in scanner mode
            LogWall(oc, wallNumber, fromFallback);

            var go = new GameObject("WallTimeLabel");
            go.transform.SetParent(oc.transform, false);
            go.transform.localPosition = new Vector3(0f, 0.9f, -0.06f);

            var tmp = go.AddComponent<TMPro.TextMeshPro>();
            tmp.text = wallNumber.ToString(); // just the counter
            tmp.alignment = TMPro.TextAlignmentOptions.Center;
            tmp.fontSize = 4f;
            tmp.color = Color.white;
            tmp.outlineWidth = 0.25f;
            tmp.outlineColor = Color.black;

            var mr = tmp.GetComponent<MeshRenderer>(); if (mr != null) mr.sortingOrder = 5000;
            go.AddComponent<BillboardToHMD>();

            System.Action<ObstacleController> cleanup = null;
            cleanup = _ =>
            {
                if (go) Destroy(go);
                oc.finishedMovementEvent -= cleanup;
            };
            oc.finishedMovementEvent += cleanup;
        }



        // ----------------------- NOTES -----------------------

        private IEnumerator HookNotesRoutine()
        {
            Debug.Log("[WallTimeOverlayDebug] HookNotesRoutine() begin");
            while (true)
            {
                var notes = FindObjectsOfType<NoteController>();
                foreach (var nc in notes)
                {
                    if (nc == null) continue;

                    // attach exactly once per controller
                    var hook = nc.GetComponent<NoteLifecycleHook>();
                    if (hook == null)
                    {
                        //Debug.Log($"[WallTimeOverlayDebug] NoteHook+Add -> {nc.GetType().Name}");
                        hook = nc.gameObject.AddComponent<NoteLifecycleHook>();
                        hook.Init(this, nc);

                        // label immediately if it’s already spawned this wave
                        hook.TryLabelNow();
                    }
                }
                yield return new WaitForSeconds(0.25f);
            }
        }



        internal int AssignNoteId(NoteController nc)
        {
            _noteCounter++;
            _noteIds[nc] = _noteCounter;
            return _noteCounter;
        }

        internal void CreateNoteLabel(NoteController nc, int noteNumber)
        {
            float songTime = _ats != null ? _ats.songTime : 0f;
            var data = nc.noteData;

            Debug.Log(
                $"[WallTimeOverlayDebug] Note #{noteNumber} " +
                $"@ {data?.time:F3}s " +
                $"line={data?.lineIndex} layer={(int)(data?.noteLineLayer)} cutDir={data?.cutDirection} rot= {data?.rotation} {data?.colorType}"
            );

            var go = new GameObject("NoteTimeLabel");
            go.transform.SetParent(nc.transform, false);
            go.transform.localPosition = new Vector3(0f, 0.6f, -0.05f);

            var tmp = go.AddComponent<TextMeshPro>();
            tmp.text = $"{noteNumber}";
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontSize = 3f;
            tmp.color = Color.yellow;
            tmp.outlineWidth = 0.25f;
            tmp.outlineColor = Color.black;

            var mr = tmp.GetComponent<MeshRenderer>();
            if (mr != null) mr.sortingOrder = 5000;

            go.AddComponent<BillboardToHMD>();

            var hook = nc.GetComponent<NoteLifecycleHook>();
            if (hook != null) hook.SetLabel(go);
        }

        internal void OnNoteDespawn(NoteController nc)
        {
            _noteIds.Remove(nc);
        }
    }

    public class NoteLifecycleHook : MonoBehaviour,
    INoteControllerDidInitEvent,
    INoteControllerNoteWasCutEvent,
    INoteControllerNoteWasMissedEvent
    {
        private WallTimeOverlayDebug _overlay;
        private NoteController _nc;
        private GameObject _label;
        private bool _labeledThisSpawn; // guard per-spawn

        public void Init(WallTimeOverlayDebug overlay, NoteController nc)
        {
            _overlay = overlay;
            _nc = nc;

            // Subscribe ONCE and keep it for the lifetime of this controller
            _nc.didInitEvent.Add(this);
            _nc.noteWasCutEvent.Add(this);
            _nc.noteWasMissedEvent.Add(this);
        }

        public void TryLabelNow()
        {
            if (_overlay == null || _nc == null || _labeledThisSpawn) return;
            if (_nc.noteData != null) // already initialized this spawn
            {
                int id = _overlay.AssignNoteId(_nc);
                _overlay.CreateNoteLabel(_nc, id);
                _labeledThisSpawn = true;
                //Debug.Log("[WallTimeOverlayDebug] NoteImmediateLabel");
            }
        }

        public void SetLabel(GameObject go)
        {
            _label = go;
            _labeledThisSpawn = true;
        }

        // Fires once per actual spawn (even with pooling)
        public void HandleNoteControllerDidInit(NoteControllerBase _)
        {
            //Debug.Log("[WallTimeOverlayDebug] NoteInit");
            if (_overlay == null || _nc == null || _labeledThisSpawn) return;
            int id = _overlay.AssignNoteId(_nc);
            _overlay.CreateNoteLabel(_nc, id);
            _labeledThisSpawn = true;
        }

        public void HandleNoteControllerNoteWasCut(NoteController noteController, in NoteCutInfo noteCutInfo)
        {
            //Debug.Log("[WallTimeOverlayDebug] NoteCut");
            ClearForReuse();
        }

        public void HandleNoteControllerNoteWasMissed(NoteController noteController)
        {
            //Debug.Log("[WallTimeOverlayDebug] NoteMiss");
            ClearForReuse();
        }

        private void ClearForReuse()
        {
            if (_label) Destroy(_label);
            _label = null;
            _labeledThisSpawn = false; // allow relabel on next spawn
                                       // IMPORTANT: we DO NOT unsubscribe and DO NOT Destroy(this).
                                       // The controller will be reused; keeping the hook guarantees we get didInit next time.
        }

        private void OnDestroy()
        {
            // Only if the controller itself is being destroyed (scene exit, etc.)
            if (_nc != null)
            {
                _nc.didInitEvent.Remove(this);
                _nc.noteWasCutEvent.Remove(this);
                _nc.noteWasMissedEvent.Remove(this);
            }
        }
    }



    public class BillboardToHMD : MonoBehaviour
    {
        private Transform _cam;
        void LateUpdate()
        {
            if (_cam == null)
            {
                var cam = Camera.main;
                if (cam == null) return;
                _cam = cam.transform;
            }
            var toCam = transform.position - _cam.position;
            if (toCam.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(toCam);
        }
    }
}
