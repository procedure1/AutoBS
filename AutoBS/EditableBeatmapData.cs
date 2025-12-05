using BeatmapSaveDataVersion3;
using CustomJSONData;
using CustomJSONData.CustomBeatmap;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static BloomPrePassRenderDataSO;
using static NoteData;
using static UnityEngine.UI.Image;

namespace AutoBS
{
    using AutoBS.Patches;
    using BeatmapSaveDataVersion4;
    using CustomJSONData.CustomBeatmap;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.InteropServices.WindowsRuntime;
    using System.Text.Json.Serialization;
    using UnityEngine.Profiling;

    // Define mutable equivalents for Custom* types
    public class ENoteData
    {
        public float time { get; set; }
        public float beat { get; set; }
        public ColorType colorType { get; set; }
        public GameplayType gameplayType { get; set; }// = GameplayType.Normal; // Default to normal gameplay type
        public ScoringType scoringType { get; set; }
        public int line { get; set; }
        public int layer { get; set; } // Now int, not NoteLineLayer
        public NoteCutDirection cutDirection { get; set; }
        public int rotation { get; set; } = 0; //accumulated rotation

        public ESliderData headNoteArc { get; set; } = null; // For arc sliders, helps keep track of the head note
        public ESliderData tailNoteArc { get; set; } = null;
        public ESliderData headNoteChain { get; set; } = null;

        public CustomData customData { get; set; } = new CustomData();

        public ENoteData() { } // Default constructor needed for Create method

        public ENoteData(CustomNoteData original)
        {
            time = original.time;
            gameplayType = original.gameplayType;
            scoringType = original.scoringType;
            line = original.lineIndex;
            layer = (int)original.noteLineLayer; // Cast to int
            cutDirection = original.cutDirection;
            colorType = (ColorType)original.colorType;
            rotation = original.rotation;
            customData = original.customData ?? new CustomData();
        }
        public ENoteData(NoteData original)
        {
            time = original.time;
            gameplayType = original.gameplayType;
            scoringType = original.scoringType;
            line = original.lineIndex;
            layer = (int)original.noteLineLayer; // Cast to int
            cutDirection = original.cutDirection;
            colorType = (ColorType)original.colorType;
            rotation = original.rotation;
        }
        /// <summary>
        /// Creates a new ENoteData object. Rotation is accumulated.
        /// </summary>
        /// <param name="time"></param>
        /// <param name="colorType"></param>
        /// <param name="line"></param>
        /// <param name="layer"></param>
        /// <param name="cutDirection"></param>
        /// <param name="rotation"></param>
        /// <param name="ArcHeadNote"></param>
        /// <param name="ArcTailNote"></param>
        /// <param name="ChainHeadNote"></param>
        /// <param name="gameplayType"></param>
        /// <returns></returns>
        public static ENoteData Create(
            float time,
            ColorType colorType,
            int line,
            int layer,
            NoteCutDirection cutDirection,
            int rotation = 0,
            ESliderData ArcHeadNote = null,
            ESliderData ArcTailNote = null,
            ESliderData ChainHeadNote = null,
            GameplayType gameplayType = GameplayType.Normal,
            ScoringType scoringType = ScoringType.Normal
        )
        {
            // If you want 'beat' to be in beats and 'time' to be in seconds, use bpm conversion.
            // If both are in beats, just set both to 'time'.
            float beatValue = time * (TransitionPatcher.bpm / 60f);

            return new ENoteData
            {
                time = time,
                beat = beatValue,
                colorType = colorType,
                line = line,
                layer = layer,
                cutDirection = cutDirection,
                rotation = rotation,
                headNoteArc = ArcHeadNote,
                tailNoteArc = ArcTailNote,
                headNoteChain = ChainHeadNote,
                gameplayType = gameplayType,
                scoringType = scoringType,
                customData = new CustomData()
            };
        }
        // In ENoteData.cs
        public CustomNoteData ToCustomNoteData(Version version)
        {

            return new CustomNoteData(
                time: this.time,
                beat: this.beat,
                rotation: this.rotation,
                lineIndex: this.line,
                noteLineLayer: (NoteLineLayer)this.layer,
                beforeJumpNoteLineLayer: (NoteLineLayer)this.layer, // set to 0 or a valid layer if you don't track this
                gameplayType: this.gameplayType,
                scoringType: this.scoringType,
                colorType: this.colorType,
                cutDirection: this.cutDirection,
                timeToNextColorNote: 0f, // Set as needed, or add to ENoteData
                timeToPrevColorNote: 0f, // Set as needed, or add to ENoteData
                flipLineIndex: this.line, // Set as needed, or add to ENoteData
                flipYSide: 0f, // Set as needed, or add to ENoteData
                cutDirectionAngleOffset: 0f, // Set as needed, or add to ENoteData
                cutSfxVolumeMultiplier: 1f, // Set as needed, or add to ENoteData
                customData: this.customData ?? new CustomData(),
                version: version
            );
        }
        public NoteData ToNoteData()
        {
            bool isBomb = this.gameplayType == NoteData.GameplayType.Bomb;

            if (isBomb)
                return NoteData.CreateBombNoteData(this.time, this.beat, this.rotation, this.line, (NoteLineLayer)this.layer);

            bool isChainHead = this.headNoteChain != null;
            bool isArcHead = this.headNoteArc != null;
            bool isArcTail = this.tailNoteArc != null;

            NoteData note = NoteData.CreateBasicNoteData(this.time, this.beat, this.rotation, this.line, (NoteLineLayer)this.layer, this.colorType, this.cutDirection);

            // scoring! these are noteData vanilla methods
            if (isArcHead)
                note.MarkAsSliderHead();
            if (isArcTail)
                note.MarkAsSliderTail();
            if (isChainHead)
                note.ChangeToBurstSliderHead();

            return note;
        }

    }

    public class EObstacleData
    {
        public float time { get; set; }
        public float beat { get; set; }
        public float duration { get; set; }
        public float endTime { get; set; } // added this avoid constant calculation of endTime from time + duration
        public float endBeat { get; set; }
        public int line { get; set; }
        public int layer { get; set; }
        public int width { get; set; }
        public int height { get; set; }
        public int rotation { get; set; } = 0;

        public CustomData customData { get; set; } = new CustomData();
        public void UpdateDuration(float dur)
        {
            duration = dur;
            endTime = time + duration;
            endBeat = (time + duration) * TransitionPatcher.bpm / 60f;
        }
        public EObstacleData() { } // Default constructor needed for Create method
        public EObstacleData(CustomObstacleData original)
        {
            time = original.time;
            beat = original.beat;
            duration = original.duration;
            endBeat = original.endBeat;
            line = original.lineIndex;
            layer = (int)original.lineLayer;
            width = original.width;
            height = original.height;
            rotation = original.rotation;
            customData = original.customData ?? new CustomData();
        }
        public EObstacleData(ObstacleData original)
        {
            time = original.time;
            beat = original.beat;
            duration = original.duration;
            endBeat = original.endBeat;
            line = original.lineIndex;
            layer = (int)original.lineLayer;
            width = original.width;
            height = original.height;
            rotation = original.rotation;
        }
        /// <summary>
        /// Creates a new EObstacleData object. Rotation is accumulated.
        /// </summary>
        /// <param name="time"></param>
        /// <param name="line"></param>
        /// <param name="layer"></param>
        /// <param name="duration"></param>
        /// <param name="width"></param>
        /// <param name="height"></param>
        /// <param name="rotation"></param>
        /// <param name="customData"></param>
        /// <returns></returns>
        public static EObstacleData Create(
            float time,
            int line,
            int layer,
            float duration,
            int width,
            int height,
            int rotation = 0,
            CustomData customData = null)
        {
            float beatValue = time * TransitionPatcher.bpm / 60f;
            float endBeatValue = (time + duration) * TransitionPatcher.bpm / 60f;

            return new EObstacleData
            {
                time = time,
                beat = beatValue,
                duration = duration,
                endTime = time + duration,
                endBeat = endBeatValue,
                line = line,
                layer = layer,
                width = width,
                height = height,
                rotation = rotation,
                customData = new CustomData(),
            };
        }

        public CustomObstacleData ToCustomObstacleData(Version version)
        {
            return new CustomObstacleData(
                time: this.time,
                startBeat: this.beat,     // or actual beat if you track it
                duration: this.duration,
                endBeat: this.endBeat, // or however you calculate it
                rotation: this.rotation,              // use actual if you track rotation for obstacles
                lineIndex: this.line,
                lineLayer: (NoteLineLayer)this.layer,
                width: this.width,
                height: this.height,
                customData: this.customData ?? new CustomData(),
                version: version
            );
        }
        public ObstacleData ToObstacleData()
        {
            return new ObstacleData(
                time: this.time,
                startBeat: this.beat,     // or actual beat if you track it
                duration: this.duration,
                endBeat: this.endBeat, // or however you calculate it
                rotation: this.rotation,              // use actual if you track rotation for obstacles
                lineIndex: this.line,
                lineLayer: (NoteLineLayer)this.layer,
                width: this.width,
                height: this.height
            );
        }
    }

    public enum ESliderType { Arc, Chain }

    public class ESliderData
    {
        public ESliderType sliderType { get; set; }
        public ColorType colorType { get; set; }
        public float time { get; set; }
        public int line { get; set; } // headLineIndex -> line
        public int layer { get; set; } // headLineLayer -> layer

        public int headBeforeJumpLineLayer { get; set; }
        public NoteCutDirection cutDirection { get; set; } // headCutDirection -> cutDirection
        public float tailTime { get; set; }
        public int tailLine { get; set; }
        public int tailLayer { get; set; }

        public int tailBeforeJumpLineLayer { get; set; }
        public NoteCutDirection tailCutDirection { get; set; }

        public SliderMidAnchorMode sliderMidAnchorMode { get; set; } = SliderMidAnchorMode.Straight;
        public float headControlPointLengthMultiplier { get; set; } = 1f;
        public float tailControlPointLengthMultiplier { get; set; } = 1f;
        public int rotation { get; set; } = 0;
        public int tailRotation { get; set; } = 0;

        public bool hasHeadNote = true;
        public bool hasTailNote = true;

        public ENoteData headNote { get; set; } = null; // help keep track of the notes that are part of the slider
        public ENoteData tailNote { get; set; } = null;

        public int sliceCount { get; set; } = 0;
        public float squishAmount { get; set; } = 1f;

        public CustomData customData { get; set; } = new CustomData();

        public ESliderData() { } // need this for CreateArc and Chain methods
        public ESliderData(CustomSliderData original)
        {
            sliderType = original.sliderType == SliderData.Type.Normal ? ESliderType.Arc : ESliderType.Chain;
            colorType = (ColorType)original.colorType;
            time = original.time;
            line = original.headLineIndex;
            layer = (int)original.headLineLayer;
            headBeforeJumpLineLayer = (int)original.headLineLayer;
            cutDirection = original.headCutDirection;
            tailTime = original.tailTime;
            tailLine = original.tailLineIndex;
            tailLayer = (int)original.tailLineLayer;
            tailBeforeJumpLineLayer = (int)original.tailLineLayer;
            tailCutDirection = original.tailCutDirection;
            sliderMidAnchorMode = original.midAnchorMode;
            headControlPointLengthMultiplier = original.headControlPointLengthMultiplier;
            tailControlPointLengthMultiplier = original.tailControlPointLengthMultiplier;
            rotation = 0; // original.rotation; // since all eData events have rotation events in ERotationEventData and not inline which is added later.
            tailRotation = original.rotation; // same as head
            hasHeadNote = original.hasHeadNote;
            hasTailNote = original.hasTailNote;
            sliceCount = original.sliceCount;
            squishAmount = original.squishAmount;
            customData = original.customData ?? new CustomData();
        }
        public ESliderData(SliderData original)
        {
            sliderType = original.sliderType == SliderData.Type.Normal ? ESliderType.Arc : ESliderType.Chain;
            colorType = (ColorType)original.colorType;
            time = original.time;
            line = original.headLineIndex;
            layer = (int)original.headLineLayer;
            headBeforeJumpLineLayer = (int)original.headLineLayer;
            cutDirection = original.headCutDirection;
            rotation = 0; // since all eData events have rotation events in ERotationEventData and not inline which is added later.
            tailTime = original.tailTime;
            tailLine = original.tailLineIndex;
            tailLayer = (int)original.tailLineLayer;
            tailBeforeJumpLineLayer = (int)original.tailLineLayer;
            tailCutDirection = original.tailCutDirection;
            sliderMidAnchorMode = original.midAnchorMode;
            headControlPointLengthMultiplier = original.headControlPointLengthMultiplier;
            tailControlPointLengthMultiplier = original.tailControlPointLengthMultiplier;
            rotation = original.rotation;
            tailRotation = original.tailRotation;
            hasHeadNote = original.hasHeadNote;
            hasTailNote = original.hasTailNote;
            sliceCount = original.sliceCount;
            squishAmount = original.squishAmount;
        }

        /// <summary>
        /// Creates a normal arc slider data object. Rotation is accumulated.
        /// </summary>
        /// <param name="colorType"></param>
        /// <param name="time"></param>
        /// <param name="line"></param>
        /// <param name="layer"></param>
        /// <param name="cutDirection"></param>
        /// <param name="tailTime"></param>
        /// <param name="tailLine"></param>
        /// <param name="tailLayer"></param>
        /// <param name="tailCutDirection"></param>
        /// <param name="HeadControlPointLengthMultiplier"></param>
        /// <param name="TailControlPointLengthMultiplier"></param>
        /// <returns></returns>
        public static ESliderData CreateArc(
            ColorType colorType,
            float time,
            int line,
            int layer,
            NoteCutDirection cutDirection,
            float tailTime,
            int tailLine,
            int tailLayer,
            NoteCutDirection tailCutDirection,
            float rotation = 0,
            SliderMidAnchorMode sliderMidAnchorMode = SliderMidAnchorMode.Straight,
            float headControlPointLengthMultiplier = 1,
            float tailControlPointLengthMultiplier = 1,
            ENoteData headNote = null,
            ENoteData tailNote = null,
            bool hasHeadNote = true,
            bool hasTailNote = true
            )
        {

            return new ESliderData
            {
                sliderType = ESliderType.Arc,
                colorType = colorType,
                time = time,
                line = line,
                layer = layer,
                headBeforeJumpLineLayer = layer,
                cutDirection = cutDirection,
                headControlPointLengthMultiplier = headControlPointLengthMultiplier,
                rotation = 0, // since all eData events have rotation events in ERotationEventData and not inline which is added later.
                tailTime = tailTime,
                tailLine = tailLine,
                tailLayer = tailLayer,
                tailBeforeJumpLineLayer = tailLayer,
                tailCutDirection = tailCutDirection,
                tailControlPointLengthMultiplier = tailControlPointLengthMultiplier,
                sliderMidAnchorMode = sliderMidAnchorMode,
                sliceCount = 0,
                squishAmount = 1f,
                hasHeadNote = true,
                hasTailNote = true,
                headNote = headNote,
                tailNote = tailNote,
            };
        }
        /// <summary>
        /// Creates a chain slider data object. Rotation is accumulated.
        /// </summary>
        /// <param name="colorType"></param>
        /// <param name="time"></param>
        /// <param name="line"></param>
        /// <param name="layer"></param>
        /// <param name="cutDirection"></param>
        /// <param name="tailTime"></param>
        /// <param name="tailLine"></param>
        /// <param name="tailLayer"></param>
        /// <param name="sliceCount"></param>
        /// <param name="squishAmount"></param>
        /// <returns></returns>
        public static ESliderData CreateChain(
            ColorType colorType,
            float time,
            int line,
            int layer,
            NoteCutDirection cutDirection,
            float tailTime,
            int tailLine,
            int tailLayer,
            int sliceCount,
            //float rotation = 0,
            float squishAmount = 1,
            ENoteData headNote = null)
        {
            return new ESliderData
            {
                sliderType = ESliderType.Chain,
                colorType = colorType,
                hasHeadNote = true,
                hasTailNote = false,
                time = time,
                line = line,
                layer = layer,
                headControlPointLengthMultiplier = 1f,
                cutDirection = cutDirection,
                rotation = 0, // since all eData events have rotation events in ERotationEventData and not inline which is added later.
                tailTime = tailTime,
                tailLine = tailLine,
                tailLayer = tailLayer,
                tailControlPointLengthMultiplier = 1f,
                tailCutDirection = cutDirection,
                sliderMidAnchorMode = SliderMidAnchorMode.Straight, // Or Burst, if you have a special enum for chains
                sliceCount = sliceCount,
                squishAmount = squishAmount,
                headNote = headNote,
                customData = headNote?.customData ?? new CustomData(), // gives the headnotes custom data to the chain hopefully so links will have same colors as head note for example
            };
        }

        public CustomSliderData ToCustomSliderData(Version version)
        {
            float beatValue = time * (TransitionPatcher.bpm / 60f);

            return new CustomSliderData(
                sliderType: this.sliderType == ESliderType.Arc ? SliderData.Type.Normal : SliderData.Type.Burst,
                colorType: this.colorType,
                hasHeadNote: this.hasHeadNote,
                headTime: this.time,
                headBeat: beatValue, // or provide actual beat if tracked separately
                rotation: this.rotation,
                headLineIndex: this.line,
                headLineLayer: (NoteLineLayer)this.layer,
                headBeforeJumpLineLayer: (NoteLineLayer)this.layer,
                headControlPointLengthMultiplier: this.headControlPointLengthMultiplier,
                headCutDirection: this.cutDirection,
                headCutDirectionAngleOffset: 0f, // use actual if you track it
                hasTailNote: this.hasTailNote,
                tailTime: this.tailTime,
                tailRotation: this.rotation, // same as head!!!!!!!!!!!!!!!!
                tailLineIndex: this.tailLine,
                tailLineLayer: (NoteLineLayer)this.tailLayer,
                tailBeforeJumpLineLayer: (NoteLineLayer)this.tailLayer,
                tailControlPointLengthMultiplier: this.tailControlPointLengthMultiplier,
                tailCutDirection: this.tailCutDirection,
                tailCutDirectionAngleOffset: 0f, // use actual if you track it
                midAnchorMode: this.sliderMidAnchorMode,
                sliceCount: this.sliceCount,
                squishAmount: this.squishAmount,
                customData: this.customData ?? new CustomData(),
                version: version
            );
        }
        
        public SliderData ToSliderData()
        {
            float beatValue = time * (TransitionPatcher.bpm / 60f);

            return new SliderData(
                sliderType: this.sliderType == ESliderType.Arc ? SliderData.Type.Normal : SliderData.Type.Burst,
                colorType: this.colorType,
                hasHeadNote: this.hasHeadNote,
                headTime: this.time,
                headBeat: beatValue, // or provide actual beat if tracked separately
                headRotation: this.rotation,
                headLineIndex: this.line,
                headLineLayer: (NoteLineLayer)this.layer,
                headBeforeJumpLineLayer: (NoteLineLayer)this.layer,
                headControlPointLengthMultiplier: this.headControlPointLengthMultiplier,
                headCutDirection: this.cutDirection,
                headCutDirectionAngleOffset: 0f, // use actual if you track it
                hasTailNote: this.hasTailNote,
                tailTime: this.tailTime,
                tailRotation: this.rotation, // same as head!!!!!!!!!!!!!!!!
                tailLineIndex: this.tailLine,
                tailLineLayer: (NoteLineLayer)this.tailLayer,
                tailBeforeJumpLineLayer: (NoteLineLayer)this.tailLayer,
                tailControlPointLengthMultiplier: this.tailControlPointLengthMultiplier,
                tailCutDirection: this.tailCutDirection,
                tailCutDirectionAngleOffset: 0f, // use actual if you track it
                midAnchorMode: this.sliderMidAnchorMode,
                sliceCount: this.sliceCount,
                squishAmount: this.squishAmount
            );
        }

        /// <summary>
        /// For Built-in v4 maps with arcs/chains, fixes the head notes to have correct scoring type and links the sliders to the head and tail notes also needed to figure out scoring later
        /// </summary>
        /// <param name="eData"></param>
        public static void FixArcChainNoteScoring(EditableCBD eData)
        {
            if ((eData?.Chains == null || eData.Chains.Count == 0) && (eData?.Arcs == null || eData.Arcs.Count == 0)) return;
            if (eData.ColorNotes == null || eData.ColorNotes.Count == 0) return;

            const float TOL = 0.0005f; // time match tolerance

            foreach (var chain in eData.Chains)
            {
                // Find the head note of this chain (same color, line, layer, and close in time)
                var headNote = eData.ColorNotes.FirstOrDefault(n =>
                    n.colorType == chain.colorType &&
                    n.line == chain.line &&
                    n.layer == chain.layer &&
                    Math.Abs(n.time - chain.time) <= TOL);

                if (headNote != null)
                {
                    headNote.headNoteChain = chain;
                    headNote.scoringType = ScoringType.ChainHead;
                    chain.headNote = headNote;

                    // Basic consistency
                    chain.hasHeadNote = true;
                    chain.hasTailNote = false;
                    //Plugin.Log.Info($"[FixArcChainNoteScoring][Chain] Fixed chain head note scoring @ {headNote.time:F3}s line={headNote.line} layer={headNote.layer}");
                }
            }

            foreach (var arc in eData.Arcs)
            {
                // ----- HEAD -----
                var headNote = eData.ColorNotes.FirstOrDefault(n =>
                    n.colorType == arc.colorType &&
                    n.line == arc.line &&
                    n.layer == arc.layer &&
                    Math.Abs(n.time - arc.time) <= TOL);

                if (headNote != null)
                {
                    headNote.headNoteArc = arc;
                    headNote.scoringType = ScoringType.ArcHead;
                    arc.headNote = headNote;

                    arc.hasHeadNote = true;
                    //Plugin.Log.Info($"[FixArcChainNoteScoring][Arc] Fixed arc head note scoring @ {headNote.time:F3}s line={headNote.line} layer={headNote.layer}");
                }

                // ----- TAIL -----
                var tailNote = eData.ColorNotes.FirstOrDefault(n =>
                    n.colorType == arc.colorType &&
                    n.line == arc.tailLine &&
                    n.layer == arc.tailLayer &&
                    Math.Abs(n.time - arc.tailTime) <= TOL);

                if (tailNote != null)
                {
                    tailNote.tailNoteArc = arc;
                    tailNote.scoringType = ScoringType.ArcTail;
                    arc.tailNote = tailNote;

                    arc.hasTailNote = true;
                    //Plugin.Log.Info($"[FixArcChainNoteScoring][ArcFix] Fixed arc tail note @ {tailNote.time:F3}s line={tailNote.line} layer={tailNote.layer}");
                }
            }

        }
    }

    public class ERotationEventData
    {
        public float time { get; set; }
        public int rotation { get; set; }
        public int accumRotation { get; set; }
        public CustomData customData { get; set; } = new CustomData();

        private static int lastAccumRotation = 0; // tracks across Create calls

        public static List<ERotationEventData> RecalculateAccumulatedRotations(List<ERotationEventData> rotationEvents)
        {
            int prevRot = 0;
            foreach (var rot in rotationEvents)
            {
                rot.accumRotation = prevRot + rot.rotation;
                prevRot = rot.accumRotation;
            }
            return rotationEvents;
        }

        public ERotationEventData() { } // Default constructor needed for Create method
        public ERotationEventData(float _time, int _rotation, int _accumRotation = 0, CustomData _customData = null)
        {
            time = _time;
            rotation = _rotation;
            accumRotation = _accumRotation;
            customData = _customData ?? new CustomData();
        }
        /// <summary>
        /// Create new rotation event.
        /// </summary>
        /// <param name="time"></param>
        /// <param name="rotation"></param>
        /// <param name="accumRotation">
        /// <param name="customData"></param>
        /// <returns></returns>
        public static ERotationEventData Create(
            float time,
            int rotation,
            int accumRotation = 0,
            CustomData customData = null)
        {
            return new ERotationEventData
            {
                time = time,
                rotation = rotation,
                accumRotation = accumRotation,
                customData = customData ?? new CustomData()
            };
        }
        /// <summary>
        /// Creates a new ERotationEventData object and will calculate accumulated rotation based on last created event.
        /// </summary>
        /// <param name="time"></param>
        /// <param name="rotation"></param>
        /// <param name="customData"></param>
        /// <returns></returns>
        public static ERotationEventData CreateInOrder(float time, int rotation, CustomData customData = null)
        {
            var evt = new ERotationEventData
            {
                time = time,
                rotation = rotation,
                accumRotation = lastAccumRotation + rotation,
                customData = customData ?? new CustomData()
            };
            lastAccumRotation = evt.accumRotation; // update tracker
            return evt;
        }
    }
    public enum EventType
    {
        BACK = BasicBeatmapEventType.Event0,           // top laser bars in 360 or static lasers (often in an "X" pattern at the back)
        RING = BasicBeatmapEventType.Event1,           // far back bars or lights attached to the inner side of the outer rings
        LEFT = BasicBeatmapEventType.Event2,           // rotating laser (points to the right)
        RIGHT = BasicBeatmapEventType.Event3,          // rotating laser (points to the left)
        CENTER = BasicBeatmapEventType.Event4,         // side light
        BOOST = BasicBeatmapEventType.Event5,          // boost event (legacy)
        SPECIAL_6 = BasicBeatmapEventType.Event6,      // Special: For Skrillex extra lasers
        SPECIAL_7 = BasicBeatmapEventType.Event7,      // Special: For Skrillex extra lasers
        RING_SPIN = BasicBeatmapEventType.Event8,      // Turns the rings (spins them)
        RING_ZOOM = BasicBeatmapEventType.Event9,      // Moves the rings (zoom)
        SPECIAL_10 = BasicBeatmapEventType.Event10,     // Special: For Billie Eilish water channel lights/extra left/right
        SPECIAL_11 = BasicBeatmapEventType.Event11,     // Special: For Billie Eilish extra left/right lasers
        LEFT_SPEED = BasicBeatmapEventType.Event12,    // Laser rotation speed for left laser
        RIGHT_SPEED = BasicBeatmapEventType.Event13,   // Laser rotation speed for right laser
        // Note: Event14 and Event15 are legacy rotation events.
        SPECIAL_16 = BasicBeatmapEventType.Event16,     // Special: For Lady Gaga additional tower lights, etc.
        SPECIAL_17 = BasicBeatmapEventType.Event17,     // Special: For Lady Gaga
        SPECIAL_18 = BasicBeatmapEventType.Event18,     // Special: For Lady Gaga
        SPECIAL_19 = BasicBeatmapEventType.Event19      // Special: For Lady Gaga
    }

    public enum EventValue
    {
        OFF = 0, //!!! RING_SPIN and RING_ZOOM use only this event to start an event. Causes rotation or zoom to last for a couple of seconds and will reverse direction on the next OFF event basically
        BLUE_ON = 1,   // Changes the lights to blue, and turns the lights on.
        BLUE_FLASH = 2,// Changes the lights to blue, and flashes brightly before returning to normal.
        BLUE_FADE = 3, // Changes the lights to blue, and flashes brightly before fading to black.
        BLUE_TRANSITION = 4, //Changes the lights to blue by fading from the current state.
        RED_ON = 5,
        RED_FLASH = 6,
        RED_FADE = 7,
        RED_TRANSITION = 8,
        ON = 9, //white
        FLASH = 10,
        FADE = 11,
        TRANSITION = 12
    }

    public enum LightEventType
    {
        ON = 1,
        FLASH = 2,
        FADE = 3,
        TRANSITION = 4
    }
    public class EBasicEventData
    {
        public float time { get; set; }
        public EventType eventType { get; set; }
        public BasicBeatmapEventType basicBeatmapEventType { get; set; }
        public EventValue eventValue { get; set; }
        public int value { get; set; } = 0; // Default to 0
        public float floatValue { get; set; } = 1;

        public CustomData customData { get; set; } = new CustomData();

        public EBasicEventData() { } // Default constructor needed for Create method
        public EBasicEventData(CustomBasicBeatmapEventData original)
        {
            time = original.time;
            basicBeatmapEventType = original.basicBeatmapEventType;
            value = original.value;
            floatValue = original.floatValue;
            customData = original.customData ?? new CustomData();
        }
        public EBasicEventData(BasicBeatmapEventData original)
        {
            time = original.time;
            basicBeatmapEventType = original.basicBeatmapEventType;
            value = original.value;
            floatValue = original.floatValue;
        }
        // 1) Beat Saber–typed factory (the “real” one)
        public static EBasicEventData Create(
            float time,
            BasicBeatmapEventType basicBeatmapEventType,
            int value,
            float floatValue = 1f,
            CustomData customData = null)
        {
            return new EBasicEventData
            {
                time = time,
                basicBeatmapEventType = basicBeatmapEventType,
                value = value,
                floatValue = floatValue,
                customData = customData ?? new CustomData()
            };
        }

        // 2) Readable-enum overload that forwards to #1
        public static EBasicEventData Create(
            float time,
            EventType eventType,
            EventValue eventValue,
            float floatValue = 1f,
            CustomData customData = null)
        {
            return Create(
                time,
                (BasicBeatmapEventType)eventType,
                (int)eventValue,
                floatValue,
                customData
            );
        }
        public CustomBasicBeatmapEventData ToCustomBasicBeatmapEventData(Version version)
        {
            return new CustomBasicBeatmapEventData(
                time: this.time,
                basicBeatmapEventType: this.basicBeatmapEventType, //(BasicBeatmapEventType)this.eventType,
                value: this.value,
                floatValue: this.floatValue,
                customData: this.customData ?? new CustomData(),
                version: version
            );
        }
        public BasicBeatmapEventData ToBasicBeatmapEventData()
        {
            return new BasicBeatmapEventData(
                time: this.time,
                basicBeatmapEventType: this.basicBeatmapEventType, //(BasicBeatmapEventType)this.eventType,
                value: this.value,
                floatValue: this.floatValue
            );
        }

    }

    public class EColorBoostEvent
    {
        public float time { get; set; }
        public bool boostColorsAreOn { get; set; }
        public CustomData customData { get; set; } = new CustomData();

        public EColorBoostEvent() { } // Default constructor needed for Create method
        public EColorBoostEvent(CustomColorBoostBeatmapEventData original)
        {
            time = original.time;
            boostColorsAreOn = original.boostColorsAreOn;
            customData = original.customData ?? new CustomData();
        }
        public EColorBoostEvent(ColorBoostBeatmapEventData original)
        {
            time = original.time;
            boostColorsAreOn = original.boostColorsAreOn;
        }
        public static EColorBoostEvent Create(
            float time,
            bool boostOn,
            CustomData customData = null)
        {
            return new EColorBoostEvent
            {
                time = time,
                boostColorsAreOn = boostOn,
                customData = customData ?? new CustomData()
            };
        }
        public CustomColorBoostBeatmapEventData ToCustomColorBoostBeatmapEventData(Version version)
        {
            return new CustomColorBoostBeatmapEventData(
                time: this.time,
                boostColorsAreOn: this.boostColorsAreOn,
                customData: this.customData ?? new CustomData(),
                version: version
            );
        }
        public ColorBoostBeatmapEventData ToColorBoostBeatmapEventData()
        {
            return new ColorBoostBeatmapEventData(
                time: this.time,
                boostColorsAreOn: this.boostColorsAreOn
            );
        }

    }

    public class ECustomEventData
    {
        public float time { get; set; }
        public string eventType { get; set; }
        public CustomData customData { get; set; }

        public ECustomEventData(CustomEventData original)
        {
            time = original.time;
            eventType = original.eventType;
            customData = original.customData ?? new CustomData();
        }
        public CustomEventData ToCustomEventData(Version version)
        {
            return new CustomEventData(
                time: this.time,
                type: this.eventType,
                data: this.customData ?? new CustomData(),
                version: version
            );
        }


    }


    public class EditableCBD
    {
        public CustomBeatmapData OriginalCBData { get; }
        public BeatmapData OriginalBData { get; }
        public List<ENoteData> ColorNotes { get; set; }
        public List<ENoteData> BombNotes { get; }
        public List<EObstacleData> Obstacles { get; set; }
        public List<ESliderData> Arcs { get; set; }
        public List<ESliderData> Chains { get; set; }
        public List<ERotationEventData> RotationEvents { get; set; }

        //public List<ERotationEventData> RotationEventsMatchEarlyPerObject { get; set; } // this is created at the last step after assigning per object and is used to output rotation events for v2/3 JSON dat files. but this list casues more wall and arc rotaiton problems for json. best to use original rotation events
        public List<EBasicEventData> BasicEvents { get; }
        public List<EColorBoostEvent> ColorBoostEvents { get; set; }
        public List <ECustomEventData> CustomEvents { get; set; }
        public CustomData BeatmapCustomData { get; }
        public CustomData LevelCustomData { get; }
        public CustomData CustomData { get; }
        public Version Version { get; }

        // Creates an EditableCBD from a standard BeatmapData object!!!!!!!!!!!!!! v4 maps will come here too since CustomBeatmapData is not compatible with v4 maps
        public EditableCBD(BeatmapData original, Version version)
        {
            OriginalBData = original;
            BeatmapCustomData = new CustomData();
            LevelCustomData = new CustomData();
            CustomData = new CustomData();
            Version = version ?? new Version();

            ColorNotes = original.allBeatmapDataItems
                .OfType<NoteData>()
                .Where(n => n.cutDirection != NoteCutDirection.None)
                .OrderBy(n => n.time)
                .Select(n => new ENoteData(n))   // ← no overrides
                .ToList();

            BombNotes = original.allBeatmapDataItems
                .OfType<NoteData>()
                .Where(n => n.cutDirection == NoteCutDirection.None)
                .OrderBy(n => n.time)
                .Select(n => new ENoteData(n))   // ← no overrides either
                .ToList();

            /* Testing if above version helps v4 maps with chains have correct head size
            // Notes
            ColorNotes = original.allBeatmapDataItems
             .OfType<NoteData>()
             .Where(n => n.cutDirection != NoteCutDirection.None)
             .OrderBy(n => n.time)
             .Select(n => new ENoteData(n)
             {
                 gameplayType = NoteData.GameplayType.Normal,
                 scoringType  = NoteData.ScoringType.Normal
             })
             .ToList();

            BombNotes = original.allBeatmapDataItems
                .OfType<NoteData>()
                .Where(n => n.cutDirection == NoteCutDirection.None)
                .OrderBy(n => n.time)
                .Select(n => new ENoteData(n)
                {
                    gameplayType = NoteData.GameplayType.Bomb,
                    scoringType  = NoteData.ScoringType.NoScore,
                    colorType    = ColorType.None,
                })
                .ToList();
            */
            Obstacles = original.allBeatmapDataItems
                .OfType<ObstacleData>()
                .OrderBy(o => o.time)
                .Select(o => new EObstacleData(o))
                .ToList();

            // Sliders
            Arcs = original.allBeatmapDataItems
                .OfType<SliderData>()
                .Where(s => s.sliderType == SliderData.Type.Normal)
                .OrderBy(s => s.time)
                .Select(s => new ESliderData(s))
                .ToList();

            Chains = original.allBeatmapDataItems
                .OfType<SliderData>()
                .Where(s => s.sliderType == SliderData.Type.Burst)
                .OrderBy(s => s.time)
                .Select(s => new ESliderData(s))
                .ToList();

            // Rotation Events (for v3/v2, you may want to adjust logic for rotation, but basic fallback below)
            RotationEvents = original.allBeatmapDataItems
                .OfType<RotationEventData>()
                .Select(e => new ERotationEventData(e.beat, (int)e.rotation, 0, new CustomData()))
                .OrderBy(e => e.time)
                .ToList();

            // Fallback: synthesize from inline 'r' in the v4 beatmap 360 JSON (early events)
            if (RotationEvents.Count == 0) 
                RotationEvents = BuildInlineRotations(ColorNotes, BombNotes, Obstacles, Arcs, Chains);

            // Basic events
            BasicEvents = original.allBeatmapDataItems
                .OfType<BasicBeatmapEventData>()
                .Where(e => e.basicBeatmapEventType != BasicBeatmapEventType.Event14 && e.basicBeatmapEventType != BasicBeatmapEventType.Event15 && e.basicBeatmapEventType != BasicBeatmapEventType.Event5)
                .OrderBy(e => e.time)
                .Select(e => new EBasicEventData(e))
                .ToList();

            // Color Boost Events
            ColorBoostEvents = original.allBeatmapDataItems
                .OfType<ColorBoostBeatmapEventData>()
                .Select(e => new EColorBoostEvent(e))
                .OrderBy(e => e.time)
                .ToList();

            CustomEvents = original.allBeatmapDataItems.
                OfType<CustomEventData>()
                .Select(e => new ECustomEventData(e))
                .OrderBy(e => e.time)
                .ToList();

            LinkArcEndpointsToNotes();
        }

        // Creates an EditableCBD from a CustomBeatmapData object!!!!!!!!!!!!!!
        public EditableCBD(CustomBeatmapData original)
        {
            OriginalCBData = original; // keep a copy of the original data for the final conversion back to CustomBeatmapData since waypoints, etc are not needed to be used or altered in EditableCBD
            BeatmapCustomData = original.beatmapCustomData ?? new CustomData();
            LevelCustomData = original.levelCustomData ?? new CustomData();
            CustomData = original.customData ?? new CustomData();
            Version = original.version;

            ColorNotes = original.beatmapObjectDatas
                .OfType<CustomNoteData>()
                .Where(n => n.cutDirection != NoteCutDirection.None)
                .OrderBy(n => n.time)
                .Select(n => new ENoteData(n))
                .ToList();

            BombNotes = original.beatmapObjectDatas
                .OfType<CustomNoteData>()
                .Where(n => n.cutDirection == NoteCutDirection.None)
                .OrderBy(n => n.time)
                .Select(n => new ENoteData(n))
                .ToList();

            Obstacles = original.beatmapObjectDatas
                .OfType<CustomObstacleData>()
                .OrderBy(o => o.time)
                .Select(o => new EObstacleData(o))
                .ToList();

            Arcs = original.beatmapObjectDatas
                .OfType<CustomSliderData>()
                .Where(s => s.sliderType == CustomSliderData.Type.Normal)
                .OrderBy(s => s.time)
                .Select(s => new ESliderData(s))
                .ToList();

            Chains = original.beatmapObjectDatas
                .OfType<CustomSliderData>()
                .Where(s => s.sliderType == CustomSliderData.Type.Burst)
                .OrderBy(s => s.time)
                .Select(s => new ESliderData(s))
                .ToList();

            // RotationEvents
            if (Version.Major == 3)// CustomBeatmapData is not compatible with v4
            {
                RotationEvents = original.beatmapEventDatas
                    .OfType<RotationEventData>()
                    .Select(e => new ERotationEventData(e.beat, (int)e.rotation, 0, new CustomData()))
                    .OrderBy(e => e.time)
                    .ToList();
            }
            else if (Version.Major == 2)
            {
                RotationEvents = original.beatmapEventDatas
                    .OfType<CustomBasicBeatmapEventData>()
                    .Where(e => e.basicBeatmapEventType == BasicBeatmapEventType.Event14 || e.basicBeatmapEventType == BasicBeatmapEventType.Event15)
                    .Select(e => new ERotationEventData(e.time, Generator.SpawnRotationValueToDegrees(e.value), 0, e.customData))
                    .OrderBy(e => e.time)
                    .ToList();
            }
            else
            {
                RotationEvents = new List<ERotationEventData>();
            }

            // Fallback: synthesize from inline 'r' in the v4 beatmap 360 JSON (early events)
            if (Version.Major == 4 && RotationEvents.Count == 0) //
                RotationEvents = BuildInlineRotations(ColorNotes, BombNotes, Obstacles, Arcs, Chains);

            // Basic lighting events (exclude rotation & color boost)
            BasicEvents = original.beatmapEventDatas
                .OfType<CustomBasicBeatmapEventData>()
                .Where(e => e.basicBeatmapEventType != BasicBeatmapEventType.Event14 && e.basicBeatmapEventType != BasicBeatmapEventType.Event15 && e.basicBeatmapEventType != BasicBeatmapEventType.Event5)
                .OrderBy(e => e.time)
                .Select(e => new EBasicEventData(e))
                .ToList();

            // Color Boost Events
            if (Version.Major == 3)
            {
                ColorBoostEvents = original.beatmapEventDatas
                    .OfType<CustomColorBoostBeatmapEventData>()
                    .Select(e => new EColorBoostEvent(e))
                    .OrderBy(e => e.time)
                    .ToList();
            }
            else if (Version.Major == 2)
            {
                ColorBoostEvents = original.beatmapEventDatas
                    .OfType<CustomBasicBeatmapEventData>()
                    .Where(e => e.basicBeatmapEventType == BasicBeatmapEventType.Event5)
                    .Select(e => EColorBoostEvent.Create(e.time, e.value == 1))
                    .OrderBy(e => e.time)
                    .ToList();
            }
            else
            {
                ColorBoostEvents = new List<EColorBoostEvent>();
            }

            CustomEvents = original.customEventDatas
                .Select(e => new ECustomEventData(e))
                .OrderBy(e => e.time)
                .ToList();

            LinkArcEndpointsToNotes();

            /*
            foreach (var note in ColorNotes)
            {
                if (note.time > 23f && note.time < 27)
                    Plugin.Log.Info($"[EditableCBD] Note @{note.time:F2}s, line {note.line}, layer {note.layer}, cutDirection {note.cutDirection}, rotation {note.rotation}");

            }
            Plugin.Log.Info($"[ConvertEditableCBD] bombs Count: {BombNotes.Count}");
            foreach (var bomb in BombNotes)
            {
                if (bomb.time > 23f && bomb.time < 27)
                    Plugin.Log.Info($"[EditableCBD] bomb @{bomb.time:F2}s, line {bomb.line}, layer {bomb.layer}, cutDirection {bomb.cutDirection}, rotation {bomb.rotation}");

            }
            */
        }


        /// <summary>
        /// Links arc with a head note and tail note based on matching time, color, line, and layer. This is needed to add per object rotations to notes linked to arcs on maps that have arcs already
        /// </summary>
        private void LinkArcEndpointsToNotes()
        {
            if (Arcs == null || Arcs.Count == 0) return;
            if (ColorNotes == null || ColorNotes.Count == 0) return;

            const float TOL = 0.0005f;

            // ColorNotes are already ordered by time in your constructor
            var notes = ColorNotes;
            int noteIdx = 0;

            foreach (var arc in Arcs)
            {
                ENoteData head = null;
                ENoteData tail = null;

                float headTime = arc.time;
                float tailTime = arc.tailTime;

                // Move noteIdx up to just before head time (small lookbehind)
                while (noteIdx < notes.Count && notes[noteIdx].time < headTime - 0.1f)
                    noteIdx++;

                // ---- HEAD ----
                for (int i = noteIdx; i < notes.Count; i++)
                {
                    var n = notes[i];
                    if (n.time > headTime + 0.1f) break; // too far in future

                    if (Math.Abs(n.time - headTime) <= TOL
                        && n.colorType == arc.colorType
                        && n.line == arc.line
                        && n.layer == arc.layer)
                    {
                        head = n;
                        break;
                    }
                }

                // ---- TAIL ----
                if (head != null)
                {
                    for (int i = noteIdx; i < notes.Count; i++)
                    {
                        var n = notes[i];
                        if (n.time > tailTime + 0.1f) break;

                        if (Math.Abs(n.time - tailTime) <= TOL
                            && n.colorType == arc.colorType
                            && n.line == arc.tailLine
                            && n.layer == arc.tailLayer)
                        {
                            tail = n;
                            break;
                        }
                    }
                }

                arc.headNote = head;
                arc.tailNote = tail;

                //built-in maps may have arcs without tail notes!
                //Plugin.Log.Info($"[LinkArcEndpointsToNotes] Arc @{arc.time:F3}s linked head note: {(head != null ? head.time.ToString("F3") : "null")}, tail note: {(tail != null ? tail.time.ToString("F3") : "null")}");
            }
        }



        /// <summary>
        /// Builds rotation events from inline 'r' rotation values in notes and obstacles for v4 built-in or possibly v4 custom maps. These are "early" rotation events.
        /// </summary>
        /// <param name="colorNotes"></param>
        /// <param name="obstacles"></param>
        /// <returns></returns>
        private static List<ERotationEventData> BuildInlineRotations(List<ENoteData> colorNotes, List<ENoteData> bombs, List<EObstacleData> obstacles, List<ESliderData> arcs, List<ESliderData> chains)
        {
            // fast early-exit: if no inline rotations anywhere, bail out
            bool hasInlineRotations =
                colorNotes.Any(n => n.rotation != 0);

            if (!hasInlineRotations)
                return new List<ERotationEventData>();

            var notesAndBombs = new List<ENoteData>();
            notesAndBombs.AddRange(colorNotes); notesAndBombs.AddRange(bombs);
            notesAndBombs = notesAndBombs.OrderBy(n => n.time).ToList();

            var rotations = new List<ERotationEventData>(colorNotes.Count);// + obstacles.Count);

            int rotation  = 0;
            int accumRot  = 0;
            int prevAccum = 0;

            // From notes
            foreach (var n in notesAndBombs)
            {
                //Plugin.Log.Info($"[EditableCBD][BuildInlineRotations] Note time:{n.time:F} inline rot: {n.rotation}");  
                accumRot = n.rotation; //n.rotation is inline accumulated rotation as hard coded in v4 beatmap json 'r' field
                rotation = n.rotation - prevAccum;
                if (n.rotation != prevAccum) // good usually but misses first note if has rotation = 0. seems to work fine though
                {
                    rotations.Add(new ERotationEventData(n.time, rotation, accumRot));
                }
                prevAccum = n.rotation;
                n.rotation = 0; // reset inline rotation after extracting since moving rotations to ERotationEventData and will later convert back to inline rotations. a reference to ColorNotes and BombNotes so this updates them outside too.
            }
            //prevRotation = 0f;
            // From obstacles
            foreach (var o in obstacles) // decided that even though obstcles have inline rotation, just better to stick to all important notes
            {
                //if (o.rotation != prevRotation)
                //    rotations.Add(new ERotationEventData(o.time, (int)o.rotation));
                //prevRotation = o.rotation;
                o.rotation = 0;
            }
            foreach (var a in arcs) // haven't thought this through, but for consistency, reset arc inline rotations too and hope that notes are all that matters
            {
                a.rotation = 0; a.tailRotation = 0;
            }
            foreach (var c in chains)
            {
                c.rotation = 0; c.tailRotation = 0;
            }

            //rotations = ERotationEventData.RecalculateAccumulatedRotations(rotations);
            /*
            foreach (var rot in rotations)
            {
                Plugin.Log.Info($"[EditableCBD][BuildInlineRotations] rotation time:{rot.time:F} rot: {rot.rotation} accumRotation {rot.accumRotation}");
            }
            */
            return rotations;// returns reference so  //MergeAndDedupeRotations(rotations);
        }
        /*
        /// <summary>
        /// Merge, dedupe by beat with list created from notes and obstacles,
        /// then collapse consecutive duplicates with same rotation.
        /// </summary>
        private static List<ERotationEventData> MergeAndDedupeRotations(List<ERotationEventData> src)
        {
            const float EPS = 1e-4f;

            // 1) sort by time (then stable by input order)
            var list = src.OrderBy(e => e.time).ToList();

            // 2) dedupe by beat (prefer last seen in same-beat cluster)
            var byBeat = new List<ERotationEventData>();
            foreach (var e in list)
            {
                if (byBeat.Count == 0) { byBeat.Add(e); continue; }
                var last = byBeat[^1];
                if (Math.Abs(e.time - last.time) <= EPS)
                {
                    // same beat: replace the last with the newer one
                    byBeat[^1] = e;
                }
                else
                {
                    byBeat.Add(e);
                }
            }

            // 3) remove consecutive duplicates (same rotation value)
            var compressed = new List<ERotationEventData>(byBeat.Count);
            foreach (var e in byBeat)
            {
                if (compressed.Count == 0 || compressed[^1].rotation != e.rotation)
                    compressed.Add(e);
            }

            compressed = ERotationEventData.RecalculateAccumulatedRotations(compressed);

            return compressed;
        }
        */
        public static (bool NoodleProblemNotes, bool NoodleProblemObstacles) TestForNoodleCustomData(EditableCBD eData)
        {
            bool NoodleProblemNotes = false;
            bool NoodleProblemObstacles = false;

            if (TransitionPatcher.RequiresNoodle) // this happens long after TransitionPatcher.BeatmapPatcher
            {
                Plugin.Log.Info("Checking for Noodle Problem Attributes...");
                // Define the set of gameplay-affecting attributes
                HashSet<string> problemAttributes = new HashSet<string>
                {
                    "_position", "_definitePosition", "_rotation", "_localRotation", "_scale", "_track", "_animation", "coordinates"
                };

                // Counters for each type
                int noteProblemCount = 0;
                int obstacleProblemCount = 0;

                foreach (var note in eData.ColorNotes)
                {
                    if (note.customData?.Keys.Any(key => problemAttributes.Contains(key)) == true)
                    {
                        noteProblemCount++;
                        if (noteProblemCount > 10)
                        {
                            NoodleProblemNotes = true;
                            break; // Stop counting notes if >10
                        }
                    }
                }
                foreach (var wall in WallGenerator._originalWalls)
                {
                    if (wall.customData?.Keys.Any(key => problemAttributes.Contains(key)) == true)
                    {
                        obstacleProblemCount++;
                        if (obstacleProblemCount > 10)
                        {
                            NoodleProblemObstacles = true;
                            break; // Stop counting obstacles if >10
                        }
                    }
                }

                if (NoodleProblemNotes || NoodleProblemObstacles)
                {
                    Plugin.Log.Info($"TestForNoodleCustomData: Contains Noodle Problem Attributes: eNotes: {noteProblemCount} eObs: {obstacleProblemCount}! (This stops counting at 11.) (out of eNotes: {eData.ColorNotes.Count} and eObs: {eData.Obstacles.Count})!");
                }
                else
                {
                    Plugin.Log.Info($"TestForNoodleCustomData: No Noodle Problem Attributes Found (out of eNotes: {eData.ColorNotes.Count} and eObs: {eData.Obstacles.Count})!");
                }

            }

            return (NoodleProblemNotes, NoodleProblemObstacles);
        }
    }



    /// <summary>
    /// Creates a new CustomBeatmapData and inserts
    /// everything from your Editable Custom Data lists (plus any kept CustomEvents)
    /// in time-sorted order. Should preserve all per item customData.
    /// </summary>
    public static class ConvertEditableCBD
    {
        public static CustomBeatmapData Convert(EditableCBD eData)
        {
            /*
            // --------------------------------------------------------------------
            // Emit v2/3-style rotation basic events (14 / 15) for JSON output only. Not needed in-game since per object rotation is set. v3 still has legacy 14/15 rotation events inside of basic data so it works for v3 JSON output
            // --------------------------------------------------------------------
            if ((Config.Instance.OutputV2JsonToSongFolderNoArcsNoChainsNoMappingExtensionWalls || 
                 Config.Instance.OutputV3JsonToSongFolder) &&
                eData.RotationEvents != null &&
                eData.RotationEvents.Count > 0)
            {
                
                int v2orv3 = 0;
                if (Config.Instance.OutputV2JsonToSongFolderNoArcsNoChainsNoMappingExtensionWalls)
                    v2orv3 = 2;
                if (Config.Instance.OutputV3JsonToSongFolder)
                    v2orv3 = 3;
                //Plugin.Log.Info($"[ConvertEditableCBD] Injecting rotation events for JSON File Output v{v2orv3} ...");

                int GetValue(int delta)
                {
                    if (v2orv3 == 2)
                        return Generator.SpawnRotationDegreesToValue(delta);
                    else // v3
                        return delta;
                }

                // Choose early vs late event type based on your RotationModeLate flag -- this chooses LATE but it doesn't work for final output. must be EARLY!!!
                //var rotEventType = Config.Instance.RotationModeLate
                //   ? BasicBeatmapEventType.Event15  // late
                //   : BasicBeatmapEventType.Event14; // early

                //eData.RotationEvents = ERotationEventData.RecalculateAccumulatedRotations(eData.RotationEvents);

                foreach (var rot in eData.RotationEvents) //delta and accumRotation are setup from within ApplyPerObjectRotations
                {
                    int delta = rot.rotation;
                    if (delta == 0)
                        continue; // skip zero-rotation events
                    // Map your ERotationEventData -> EBasicEventData
                    // You can decide whether you want delta or accumulated in value.
                    // For debugging it's usually nice to see the *accumulated* lane:
                    //int value = GetValue(delta);

                    // This doesn't work in-game and so has no effect in-game! this is just for outputting JSON .dat files.
                    var ev = EBasicEventData.Create(
                        time: rot.time,
                        basicBeatmapEventType: BasicBeatmapEventType.Event14, // rotEventType, ********* THIS MUST BE EARLY (14) TO WORK PROPERLY for output JSON maps using CustomBeatmapDataConverter.ToJsonStringFile
                        value: GetValue(delta)
                    );

                    eData.BasicEvents.Add(ev);
                    //Plugin.Log.Debug($"[ConvertEditableCBD] Injected rotation event @{ev.time:F2}s type:{ev.basicBeatmapEventType} value:{ev.value} -- FROM: rot.Rotation: {delta} rot.accum: {rot.accumRotation}");
                }
                
                //Plugin.Log.Info($"[ConvertEditableCBD] Injected for JSON File Output - {eData.RotationEvents.Count} rotation events as {(Config.Instance.RotationModeLate ? "late (15)" : "early (14)")} basic events.");
            }
            */
            var newData = new CustomBeatmapData(
                numberOfLines: 4, // Standard; parameterize if you support others
                beatmapCustomData: eData.BeatmapCustomData ?? new CustomData(),
                levelCustomData: eData.LevelCustomData ?? new CustomData(),
                customData: new CustomData(), // container-level custom data
                version: eData.Version
            );

            var allItems = new List<object>(capacity: 4096);

            // --- Your edited objects ---
            allItems.AddRange(eData.ColorNotes.Select(n => (object)n.ToCustomNoteData(eData.Version)));
            allItems.AddRange(eData.BombNotes.Select(n => (object)n.ToCustomNoteData(eData.Version)));
            allItems.AddRange(eData.Obstacles.Select(o => (object)o.ToCustomObstacleData(eData.Version)));
            allItems.AddRange(eData.Arcs.Select(a => (object)a.ToCustomSliderData(eData.Version)));
            allItems.AddRange(eData.Chains.Select(c => (object)c.ToCustomSliderData(eData.Version)));

            // --- Your rebuilt vanilla-style events (custom-capable) ---
            allItems.AddRange(eData.BasicEvents.Select(ev => (object)ev.ToCustomBasicBeatmapEventData(eData.Version)));
            allItems.AddRange(eData.ColorBoostEvents.Select(ev => (object)ev.ToCustomColorBoostBeatmapEventData(eData.Version)));

            // --- Custom events: choose ONE source ---
            if ((eData.CustomEvents?.Count ?? 0) > 0)
            {
                allItems.AddRange(eData.CustomEvents.Select(cev => (object)cev.ToCustomEventData(eData.Version)));
            }
            else if (eData.OriginalCBData?.customEventDatas is { } origCevs && origCevs.Count > 0)
            {
                // Pass-through original custom events (already have non-null customData by design)
                allItems.AddRange(origCevs);
            }

            // --- Pass-through everything else ONCE (skip types you rebuilt) ---
            if (eData.OriginalCBData != null)
            {
                foreach (var item in eData.OriginalCBData.allBeatmapDataItems)
                {
                    // Skip objects you rebuilt
                    if (item is CustomNoteData
                     || item is CustomObstacleData
                     || item is CustomSliderData)
                        continue;

                    // Skip the event classes you rebuilt to avoid duplicates
                    if (item is CustomBasicBeatmapEventData
                     || item is CustomColorBoostBeatmapEventData
                     || item is CustomEventData) // handled above (either edited or passed-through from customEventDatas)
                        continue;

                    // Keep: rotations, BPM changes, NJS changes, etc.
                    allItems.Add(item);
                }
            }
            else if (eData.OriginalBData != null)
            {
                foreach (var item in eData.OriginalBData.allBeatmapDataItems)
                {
                    if (item is NoteData || item is ObstacleData || item is SliderData)// || item is BurstSliderData)
                        continue;

                    // Keep vanilla events when CustomBeatmapData wasn’t available
                    allItems.Add(item);
                }
            }

            // --- Sort once by time ---
            var sorted = allItems.OrderBy(item =>
            {
                switch (item)
                {
                    case BeatmapObjectData o: return o.time;
                    case BeatmapEventData e: return e.time;
                    case CustomEventData c: return c.time;
                    default: return float.MaxValue;
                }
            });

            // --- Insert into the container using InOrder helpers ---
            foreach (var o in sorted)
            {
                switch (o)
                {
                    case BeatmapObjectData obj:
                        newData.AddBeatmapObjectDataInOrder(obj);
                        break;
                    case BeatmapEventData evt:
                        newData.InsertBeatmapEventDataInOrder(evt);
                        break;
                    case CustomEventData cev:
                        newData.InsertCustomEventDataInOrder(cev);
                        break;
                }
            }

            newData.ProcessAndSortBeatmapData();

            Plugin.Log.Info($"[ConvertEditableCBD] -> CustomBeatmapData: " +
                                     $"{newData.beatmapObjectDatas.Count} objects, " +
                                     $"{newData.beatmapEventDatas.Count} events, " +
                                     $"{newData.customEventDatas.Count} customEvents");


            return newData;
        }

        /// <summary>
        /// Must convert built-in OST maps to standard beatmapData or for some reason heck will crash beat saber since it finds cast exception for customBeatmapData
        /// </summary>
        /// <param name="eData"></param>
        /// <returns></returns>
        public static BeatmapData ConvertVanilla (EditableCBD eData) 
        {
            ESliderData.FixArcChainNoteScoring(eData);
            // numberOfLines: 4 for Standard; adjust if you support others
            var newData = new BeatmapData(4);

            // 1) Objects (vanilla types)
            foreach (var n in eData.ColorNotes) newData.AddBeatmapObjectDataInOrder(n.ToNoteData());
            foreach (var n in eData.BombNotes) newData.AddBeatmapObjectDataInOrder(n.ToNoteData());
            foreach (var o in eData.Obstacles) newData.AddBeatmapObjectDataInOrder(o.ToObstacleData());
            foreach (var a in eData.Arcs) newData.AddBeatmapObjectDataInOrder(a.ToSliderData());
            foreach (var c in eData.Chains) newData.AddBeatmapObjectDataInOrder(c.ToSliderData());

            foreach (var e in eData.BasicEvents) newData.InsertBeatmapEventDataInOrder(e.ToBasicBeatmapEventData());
            foreach (var b in eData.ColorBoostEvents) newData.InsertBeatmapEventDataInOrder(b.ToColorBoostBeatmapEventData());

            // --- Pass-through ALL other vanilla events you didn’t touch --- should send NJS events etc
            if (eData.OriginalBData != null)
            {
                foreach (var item in eData.OriginalBData.allBeatmapDataItems)
                {
                    // Skip objects – you already recreated those
                    if (item is NoteData || item is ObstacleData || item is SliderData || item is BurstSliderData)
                        continue;

                    // Skip the event types you explicitly rebuilt above to avoid duplicates
                    if (item is BasicBeatmapEventData || item is ColorBoostBeatmapEventData)
                        continue;

                    // Keep everything else: rotations, BPM changes, NJS events, etc.
                    if (item is BeatmapEventData evt)
                        newData.InsertBeatmapEventDataInOrder(evt);
                }
            }

            newData.ProcessAndSortBeatmapData();

            return newData;
        }

        /// <summary>
        /// Updates all ENoteData, EObstacleData and ESliderData rotations in-place,
        /// using eData.RotationEvents and the headNote/tailNote links on each slider.
        /// </summary>
        public static void ApplyPerObjectRotations(EditableCBD eData)
        {
            Plugin.Log.Info("[RotationApplier] ---------- Starting per-object rotation application");

            if (eData.RotationEvents.Count == 0 || eData.ColorNotes.Count == 0)
                return;

            // 1) Sort your raw rotation events by time
            eData.RotationEvents = eData.RotationEvents
                .OrderBy(r => r.time)
                .ToList();

            bool rotationModeLate = Config.Instance.RotationModeLate;

            // 2) Build a cumulative list: at each event time, what’s the running total?
            var accumulated = new List<(float time, int total)>();
            int runningTotal = 0;
            foreach (var evt in eData.RotationEvents)
            {
                runningTotal += evt.rotation;
                accumulated.Add((evt.time, runningTotal));
                //if (evt.time < 20) Plugin.Log.Info($"[RotationApplier] Δ@{evt.time:F2}s = {evt.rotation}, cum → {runningTotal}");
            }

            //the last cumulative rotation at or before t - equivalent to v2 "early" rotation events
            const float EPS = 0.0005f; // same spirit as your TOL

            int GetAccumRotationAt(float t) // bianary search version
            {
                int lo = 0, hi = accumulated.Count - 1, ans = -1;
                while (lo <= hi)
                {
                    int mid = (lo + hi) >> 1;
                    float mt = accumulated[mid].time;

                    bool ok = rotationModeLate ? (t > mt + EPS) : (t >= mt - EPS);
                    if (ok) { ans = mid; lo = mid + 1; } else { hi = mid - 1; }
                }
                return (ans >= 0) ? accumulated[ans].total : 0;
            }


            // Apply to all notes (color + bombs)
            foreach (var note in eData.ColorNotes)
            {
                note.rotation = GetAccumRotationAt(note.time);
                //if (note.time < 30)
                //    Plugin.Log.Info($"[RotationApplier] Note @{note.time:F2}s → rot: {note.rotation} line: {note.line} layer: {note.layer} color: {note.colorType}");
            }

            foreach (var bomb in eData.BombNotes)
            {
                bomb.rotation = GetAccumRotationAt(bomb.time);
                //if (bomb.time < 30)
                //    Plugin.Log.Info($"[RotationApplier] Note @{bomb.time:F2}s → rot: {bomb.rotation}");
            }
            
            //var arcTailAccumRotationOverride = new List<ERotationEventData>(); // list of accumRotations changed for arc tails // didn't help as far as i could see to create arcTailAccumRotationOverride() and actually caused problems for old version of vision blocking fix

            foreach (var arc in eData.Arcs)
            {
                int desiredAccum = arc.headNote != null
                    ? arc.headNote.rotation
                    : GetAccumRotationAt(arc.time);
                //int tailRot = arc.tailNote != null
                //    ? arc.tailNote.rotation
                //    : GetRotationAt(arc.tailTime);

                //tailRot = headRot; // if tail rotation matches head rotation, use head rotation

                arc.rotation     = desiredAccum;
                arc.tailRotation = desiredAccum;// tailRot;

                //bool tailRotationCorrected = false;
                
                /*
                int tailAccumBefore = (arc.tailNote != null)
                    ? arc.tailNote.rotation
                    : GetAccumRotationAt(arc.tailTime - EPS);
                
                if (tailAccumBefore != desiredAccum)
                {
                    //tailRotationCorrected = true;

                    int deltaRot = desiredAccum - tailAccumBefore;

                    if (deltaRot != 0)
                    {
                        // If another override exists at the exact same time, merge to avoid duplicates
                        var existing = arcTailAccumRotationOverride
                            .FirstOrDefault(e => Math.Abs(e.time - arc.tailTime) < EPS);

                        if (existing != null)
                        {
                            existing.rotation += deltaRot;       // keep direction sum
                            existing.accumRotation = desiredAccum;   // latest target (for logging)
                        }
                        else
                        {
                            // rotation = delta (direction), accumRotation = target accum (optional but handy)
                            arcTailAccumRotationOverride.Add(
                                new ERotationEventData(arc.tailTime, deltaRot, desiredAccum)
                            );
                        }

                        //Plugin.Log.Info($"[RotationApplier] Arc @{arc.time:F2}s tail {arc.tailTime:F2}: " +$"pre={tailAccumBefore}, delta={deltaRot:+#;-#;0}, target={desiredAccum}");
                    }
                }
                */

                //if ((arc.headNote != null && desiredAccum != arc.headNote.rotation) || (arc.tailNote != null && desiredAccum != arc.tailNote.rotation))
                //    Plugin.Log.Warn($"[RotationApplier] Arc @{arc.time:F2}s tail {arc.tailTime:F2}: headAccum={desiredAccum}, headAccumBefore={arc.headNote.rotation} tailAccumBefore={arc.tailNote.rotation} -- Tail Rotation Corrected!");

                if (arc.headNote != null) arc.headNote.rotation = desiredAccum;
                if (arc.tailNote != null) arc.tailNote.rotation = desiredAccum;// tailRot;
                //if (arc.tailNote != null)
                //    Plugin.Log.Info($"[RotationApplier] FINAL Arc @{arc.time:F2}s tailTime: {arc.tailTime} → arc rot: {arc.rotation} headNote rot: {arc.headNote.rotation} tail rot: {arc.tailRotation} tailNote rot: {arc.tailNote.rotation} -- color: {arc.colorType}");
            }

            // Apply to chains: we have headNote link; tail uses time
            foreach (var chain in eData.Chains)
            {
                int headRot = chain.headNote != null
                    ? chain.headNote.rotation
                    : GetAccumRotationAt(chain.time);
                //int tailRot = GetAccumRotationAt(chain.tailTime);

                // maybe its ok to allow rotation during chain but logs show it may never happen anyway probably because the head to tail time period is too short
                //if (tailRotationMatchesHeadRotation) tailRot = headRot; // if tail rotation matches head rotation, use head rotation

                chain.rotation = headRot;
                chain.tailRotation = headRot;// tailRot; // this is giving different rotations to tails! seems to work fine anyway

                //if (chain.time < 20) Plugin.Log.Info($"[RotationApplier] Chain @{chain.time:F2}s → head rot: {headRot}, tail rot: {tailRot}");
            }

            //ApplyArcTailAccumOverridesToRotationEvents(eData.RotationEvents, arcTailAccumRotationOverride);

            // 4) Apply to all obstacles
            (bool noodleProblemNotes, bool noodleProblemObstacles) = EditableCBD.TestForNoodleCustomData(eData);

            // After you've finished adjusting notes/arcs/chains
            var noteAndBombKeyframes = eData.ColorNotes
                .OrderBy(n => n.time)
                .Select(n => (time: n.time, rot: n.rotation))
                .ToList();

            var bombFrames = eData.BombNotes
                .OrderBy(b => b.time)
                .Select(b => (time: b.time, rot: b.rotation));

            noteAndBombKeyframes = noteAndBombKeyframes
                .Concat(bombFrames)
                .GroupBy(x => x.time)
                .Select(g => (time: g.Key, rot: g.First().rot)) // or some merge logic
                .OrderBy(x => x.time)
                .ToList();

            // Build a normalized rotation event list from *final* note/bomb rotations. this is a set of Early Events that should work well with the convert method to create a JSON .dat file later

            //if (Config.Instance.OutputV2JsonToSongFolderNoArcsNoChainsNoMappingExtensionWalls || Config.Instance.OutputV3JsonToSongFolder)
            //    RebuildRotationEventsFromKeyframes();

            /*
            void RebuildRotationEventsFromKeyframes()
            {
                var rebuilt = new List<ERotationEventData>();

                int prevAccum = 0;
                foreach (var k in noteAndBombKeyframes)
                {
                    int delta = k.rot - prevAccum;
                    if (delta == 0)
                        continue; // skip 0-delta events

                    // accumRotation here is simply k.rot (the new cumulative value)
                    rebuilt.Add(new ERotationEventData(k.time, delta, k.rot));
                    prevAccum = k.rot;
                }

                //eData.RotationEventsMatchEarlyPerObject = rebuilt;

                // Debug (optional):
                // foreach (var ev in rebuilt.TakeWhile(ev => ev.time < 20f))
                //     Plugin.Log.Info($"[RotationApplier] Rebuilt rotEvt @ {ev.time:F3}s: Δ={ev.rotation}, accum={ev.accumRotation}");
            }
            */
            const float TOL = 0.0005f;

            int GetLogicalRotationFromNotes(float t)
            {
                if (noteAndBombKeyframes.Count == 0)
                    return 0;

                // Before first note
                if (t < noteAndBombKeyframes[0].time - TOL)
                    return noteAndBombKeyframes[0].rot;

                // After last note
                if (t > noteAndBombKeyframes[^1].time + TOL)
                    return noteAndBombKeyframes[^1].rot;

                // Binary search: last keyframe with time <= t
                int lo = 0, hi = noteAndBombKeyframes.Count - 1, idx = 0;
                while (lo <= hi)
                {
                    int mid = (lo + hi) >> 1;
                    if (noteAndBombKeyframes[mid].time <= t + TOL)
                    {
                        idx = mid;
                        lo = mid + 1;
                    }
                    else hi = mid - 1;
                }

                var k0 = noteAndBombKeyframes[idx];

                // If there's a next keyframe and times are close, and you want to be fancy:
                if (idx < noteAndBombKeyframes.Count - 1)
                {
                    var k1 = noteAndBombKeyframes[idx + 1];

                    // If both rotations are same, treat whole span as flat
                    if (k0.rot == k1.rot)
                        return k0.rot;

                    // If you ever want interpolation between different angles, you could add it here.
                    // For now, just stick with k0.rot to keep behavior simple.
                }

                return k0.rot;
            }

            //if (!noodleProblemObstacles) // i removed this for Ride Remix which has no noodle walls (in original walls) and thus has no rotations
            {

                foreach (var obs in eData.Obstacles)
                {
                    obs.rotation = GetLogicalRotationFromNotes(obs.time); //suggested to use this instead of rotationEvents
                }

                eData.Obstacles = ApplyWallVisionBlockingFix(eData); // will alter eData by reference
            }

            //if (Config.Instance.OutputV2JsonToSongFolderNoArcsNoChainsNoMappingExtensionWalls || Config.Instance.OutputV3JsonToSongFolder)
            //    eData.RotationEventsMatchEarlyPerObject = BuildEarlyRotationEventsFromPerObjectRotations(eData);

            //else
            //{
            //    Plugin.Log.Warn("[RotationApplier] Skipping Wall Rotation Application and Wall Vision Blocking Fix due to Noodle customData on obstacles!");
            //}

            //Plugin.Log.Info("[RotationApplier] Finished per-object rotation application");
        }

        // Not used currently. i made this list to prevent arcs and walls from have rotation problems in JSON output files. but it turns out the the origianl rotation events list is best.
        /// <summary>
        /// Builds a new rotation events list based on the actual per-object rotations.
        /// This list accurately represents "early" rotation events for JSON export,
        /// accounting for arc tail corrections and wall rotations from notes.
        /// </summary>
        public static List<ERotationEventData> BuildEarlyRotationEventsFromPerObjectRotations(EditableCBD eData)
        {
            const float EPS = 0.0005f;

            // Gather all objects with their times and rotations
            var allObjects = new List<(float time, int rotation)>();

            foreach (var note in eData.ColorNotes)
                allObjects.Add((note.time, note.rotation));
            foreach (var bomb in eData.BombNotes)
                allObjects.Add((bomb.time, bomb.rotation));
            foreach (var obs in eData.Obstacles)
                allObjects.Add((obs.time, obs.rotation));
            foreach (var arc in eData.Arcs)
            {
                allObjects.Add((arc.time, arc.rotation));
                // Include tail time with head rotation (since we forced tail = head)
                allObjects.Add((arc.tailTime, arc.rotation));
            }
            foreach (var chain in eData.Chains)
            {
                allObjects.Add((chain.time, chain.rotation));
                allObjects.Add((chain.tailTime, chain.rotation));
            }

            // Sort by time
            allObjects = allObjects.OrderBy(o => o.time).ToList();

            if (allObjects.Count == 0)
                return new List<ERotationEventData>();

            // Build rotation events: emit an event whenever the accumulated rotation changes
            var rotationEvents = new List<ERotationEventData>();
            int prevAccumRotation = 0;
            float prevTime = float.MinValue;

            foreach (var (time, rotation) in allObjects)
            {
                // Skip if same time as previous (within epsilon) - use the first object at each time
                if (Math.Abs(time - prevTime) < EPS)
                    continue;

                // If this object has a different accumulated rotation, emit an event
                if (rotation != prevAccumRotation)
                {
                    int deltaRotation = rotation - prevAccumRotation;
                    rotationEvents.Add(ERotationEventData.Create(time, deltaRotation, rotation, new CustomData()));
                    prevAccumRotation = rotation;
                }

                prevTime = time;
            }

            Plugin.Log.Info($"[BuildEarlyRotationEvents] Built {rotationEvents.Count} early rotation events from per-object rotations");

            // Ensure arc tails match heads by adding corrective events
            rotationEvents = EnsureArcTailsMatchHeads(rotationEvents, eData.Arcs);

            return rotationEvents;
        }

        /// <summary>
        /// Ensures that at each arc tail time, the accumulated rotation matches the head rotation.
        /// If rotations occurred between head and tail, adds a corrective event at the tail time.
        /// Call this AFTER building rotation events from per-object rotations.
        /// </summary>
        public static List<ERotationEventData> EnsureArcTailsMatchHeads(
            List<ERotationEventData> rotationEvents,
            List<ESliderData> arcs)
        {
            const float EPS = 0.0005f;

            if (arcs == null || arcs.Count == 0)
                return rotationEvents;

            // Sort rotation events by time
            var events = rotationEvents.OrderBy(e => e.time).ToList();

            // Build accumulated rotation lookup
            var accumulated = new List<(float time, int total)>();
            int runningTotal = 0;
            foreach (var evt in events)
            {
                runningTotal += evt.rotation;
                accumulated.Add((evt.time, runningTotal));
            }

            // Get accumulated rotation at time t (early style - at or before t)
            int GetAccumAt(float t)
            {
                if (accumulated.Count == 0) return 0;

                int lo = 0, hi = accumulated.Count - 1, ans = -1;
                while (lo <= hi)
                {
                    int mid = (lo + hi) >> 1;
                    if (accumulated[mid].time <= t + EPS)
                    {
                        ans = mid;
                        lo = mid + 1;
                    }
                    else hi = mid - 1;
                }
                return ans >= 0 ? accumulated[ans].total : 0;
            }

            var corrections = new List<ERotationEventData>();

            foreach (var arc in arcs)
            {
                int headRotation = arc.rotation; // The desired accumulated rotation at head (and tail)
                int tailAccumBefore = GetAccumAt(arc.tailTime);

                if (tailAccumBefore != headRotation)
                {
                    int deltaCorrection = headRotation - tailAccumBefore;

                    // Check if a correction already exists at this tail time
                    var existingCorrection = corrections.FirstOrDefault(c => Math.Abs(c.time - arc.tailTime) < EPS);
                    if (existingCorrection != null)
                    {
                        // Merge: update the delta (use the latest target rotation)
                        existingCorrection.rotation += deltaCorrection;
                        existingCorrection.accumRotation = headRotation;
                    }
                    else
                    {
                        corrections.Add(ERotationEventData.Create(
                            arc.tailTime,
                            deltaCorrection,
                            headRotation,
                            new CustomData()
                        ));
                    }

                    Plugin.Log.Info($"[ArcTailCorrection] Arc @{arc.time:F2}s tail @{arc.tailTime:F2}s: " +
                        $"tailAccumBefore={tailAccumBefore}, headRot={headRotation}, delta={deltaCorrection}");
                }
            }

            if (corrections.Count > 0)
            {
                Plugin.Log.Info($"[ArcTailCorrection] Added {corrections.Count} corrective rotation events for arc tails");

                // Merge corrections into events
                events.AddRange(corrections);
                events = events.OrderBy(e => e.time).ToList();

                // Recalculate accumulated rotations
                events = ERotationEventData.RecalculateAccumulatedRotations(events);
            }

            return events;
        }

        /// <summary>
        /// When arc tail rotation overrides exist where I had to force the tail to match the head, apply them to the rotation events list
        /// </summary>
        /// <param name="rotations"></param>
        /// <param name="arcTailAccumRotationOverride"></param>
        /// <returns></returns>
        static List<(float time, int total)> ApplyArcTailAccumOverridesToRotationEvents(
            List<ERotationEventData> rotations,
            List<ERotationEventData> arcTailAccumRotationOverride)
        {
            const float EPS = 0.0005f;

            if (rotations == null || rotations.Count == 0)
                return new List<(float time, int total)>();

            // Apply overrides only if we have any; otherwise just fall through
            if (arcTailAccumRotationOverride != null && arcTailAccumRotationOverride.Count > 0)
            {
                foreach (var ov in arcTailAccumRotationOverride)
                {
                    if (Math.Abs(ov.rotation) < float.Epsilon)
                        continue; // nothing to do

                    var existing = rotations.FirstOrDefault(r => Math.Abs(r.time - ov.time) < EPS);

                    if (existing != null)
                    {
                        existing.rotation += ov.rotation;
                    }
                    else
                    {
                        rotations.Add(ERotationEventData.Create(ov.time, ov.rotation, 0, new CustomData()));
                    }
                }

                // Sort if we inserted new events
                rotations = rotations.OrderBy(r => r.time).ToList();

                // Recalc accumRotation for the entire list
                rotations = ERotationEventData.RecalculateAccumulatedRotations(rotations);
            }

            // Build accumulated from the (possibly updated) rotations
            var accumulated = new List<(float time, int total)>();
            int runningTotal = 0;
            foreach (var evt in rotations)
            {
                runningTotal += evt.rotation;
                accumulated.Add((evt.time, runningTotal));
            }

            return accumulated;
        } 




        // WallRemovalForRotations() new version
        // works but as RotationSpeedMultiplier increases this doesn't account for it.
        static List<EObstacleData> ApplyWallVisionBlockingFixOLD(EditableCBD eData)  // WallRemovalForRotations() new version works except for NetZero and NoRestrictions in ArcFix
        {
            var rotationEvents = eData.RotationEvents;
            var obstacles = eData.Obstacles;
            bool rotationEventsSubsetUsed = false;

            float njs = TransitionPatcher.FinalNoteJumpMovementSpeed;
            float jd = TransitionPatcher.FinalJumpDistance;

            if (njs <= 0 || jd <= 0)
            {
                Plugin.Log.Error("[WallBlockingFix] NoteJumpMovementSpeed or JumpDistance is not set, skipping wall blocking fix.");
                return obstacles;
            }

            float wallTravelTime = jd / njs;

            Plugin.Log.Info($"[WallBlockingFix] Total Obstacles: {obstacles.Count} NJD={jd:F2}, NJS={njs:F2}, wallTravelTime={wallTravelTime:F2}");

            var rotations = rotationEvents.OrderBy(evt => evt.time).ToList(); //checked accumulted is accurate here

            int GetAccumRotationAt(float t)
            {
                // faster binary search
                int lo = 0, hi = rotations.Count - 1, ans = -1;
                while (lo <= hi)
                {
                    int mid = (lo + hi) >> 1;
                    if (rotations[mid].time <= t)
                    {
                        ans = mid;
                        lo = mid + 1;
                    }
                    else hi = mid - 1;
                }
                return ans >= 0 ? rotations[ans].accumRotation : 0;
            }

            int RotationForSegmentStart(EObstacleData obs, float segStart)
                => rotationEventsSubsetUsed ? obs.rotation : GetAccumRotationAt(segStart);

            // Returns true if a rotation (direction) would block this wall (by line).
            //bool WouldBlock(int lineIndex, int direction) =>
            //    (lineIndex < 2 && direction < 0) || (lineIndex > 1 && direction > 0);
            bool WouldBlock(int lineIndex, int direction, float obsTime, float rotTime)
            {
                //if (obsTime > 0 && obsTime < 16)  Plugin.Log.Info($"[WallBlockingFix] --- WouldBlock check: obs time: {obsTime} lineIndex={lineIndex}, direction={direction} at time: {rotTime}");

                if (lineIndex < 2 && direction < 0)
                {
                    //if (obsTime > 0 && obsTime < 16)  Plugin.Log.Info($"[WallBlockingFix] ----- Blocked (left wall with left turn)");
                    return true;
                }
                if (lineIndex > 1 && direction > 0)
                {
                    //if (obsTime > 0 && obsTime < 16) Plugin.Log.Info($"[WallBlockingFix] ----- Blocked (right wall with right turn)");
                    return true;
                }
                //if (obsTime > 0 && obsTime < 16)  Plugin.Log.Info($"[WallBlockingFix] ----- Not blocked");
                return false;
            }

            var kept = new List<EObstacleData>();
            int obsIdx = 0;
            bool boostedWalls = Config.Instance.AllowV2BoostedWalls;

            foreach (var obs in obstacles)
            {
                if (WallGenerator._originalWalls.Contains(obs) && WallGenerator.IsCustomNoodleWall(obs))
                    continue;

                float obsTime = obs.time;
                int line = obs.line;
                int layer = obs.layer;
                float dur = obs.duration;

                if (line < 0) line = Math.Abs(obs.line + obs.width); // right edge of left wall

                // If high wall: keep as-is, skip splitting/removal
                bool canSkip = false;
                if (layer > 11000) canSkip = true; // high walls can skip
                else if (layer > 10 && layer < 1000) canSkip = true; //floor walls can skip

                int absLine = Math.Abs(line);
                // If short distant wall: keep as-is, skip splitting/removal, but really long distant walls can esp block vision

                //BW disabled this to test
                //if (absLine > 16000 && dur < .1f) canSkip = true; // far away and short
                //else if (absLine > 15 && absLine < 1000 && dur < .1f) canSkip = true; // far away and short

                //don't use line here must use obs.line
                if (layer == 2 && obs.line == 0 && obs.width > 2) canSkip = true; //crouch wall so rotation does not matter

                if (dur < 0 && boostedWalls) canSkip = true; // boosted walls on v2 maps use negative duration to make super fast walls. these will get removed if not skipped

                if (canSkip)
                {
                    kept.Add(obs);
                    //if (obsTime > 0 && obsTime < 16)
                    //    Plugin.Log.Info($"[WallBlockingFix]   (SKIP) Keeping wall as-is: time={obsTime:F2}, dur={obs.duration:F2}, rot={obs.rotation} line: {obs.line} layer: {obs.layer} width: {obs.width} height: {obs.height}");
                    obsIdx++;
                    continue; // skip the splitting/removal logic for this wall
                }
                //*** OLD
                float visibleStart = obsTime;// + .01f; // the smaller this is, the closer to the exact start of the wall. adding a little bit may make a tiny wall but will create small walls that block vision. minwallduration is .001
                float visibleEnd = obs.endTime + wallTravelTime/3f; // best /10 allows vision blocking walls. /4 still has questionable walls, /2 cuts too many i think
                //if (obsTime > 0 && obsTime < 16) Plugin.Log.Info($"[WallBlockingFix] Obstacle {obsIdx}: start={obsTime:F2}, dur={obs.duration:F2}, rot={obs.rotation}, visibleStart= {visibleStart} visibleEnd={visibleEnd:F2}, line={obs.line}, layer={obs.layer}, width={obs.width}, height={obs.height}");

                // Find all blocking rotations within the visible window
                var blockEvents = rotations //newRotationEvents
                    .Where(dt => dt.time >= visibleStart && dt.time <= visibleEnd && WouldBlock(obs.line, dt.rotation, obsTime, dt.time))
                    .Select(dt => dt.time)
                    .OrderBy(t => t)
                    .ToList();

                //if (blockEvents.Count > 0 && obsTime > 0 && obsTime < 16) Plugin.Log.Info($"[WallBlockingFix] ------ has {blockEvents.Count} blocking rotations at times: {string.Join(", ", blockEvents.Select(t => t.ToString("F2")))}");

                float segStart = obsTime;
                float segEnd = obs.endTime;
                int segmentCount = 0;

                // Go through each blocking event and split at the *time when the block reaches the player*
                foreach (var blockTime in blockEvents)
                {
                    // The time when the block rotation hits the player
                    float cutTime = blockTime - wallTravelTime/3f; // or /2 works too. doesn't work with just blockTime
                    //***
                    if (cutTime > segStart)
                    {
                        segmentCount++;
                        float duration = Math.Min(segEnd, cutTime) - segStart;
                        if (duration >= 0.001f) // ignore tiny negative/zero segments
                        {
                            var segment = EObstacleData.Create(
                                segStart,
                                obs.line,
                                obs.layer,
                                duration,
                                obs.width,
                                obs.height,
                                RotationForSegmentStart(obs, segStart) // ← use wall’s own rotation in pass 2 when a subset of rotation events is used since without a full set the GetAccumRotationAt() would be wrong
                            );
                            kept.Add(segment);
                            //if (obsTime > 0 && obsTime < 16) Plugin.Log.Info($"[WallBlockingFix]   Segment {segmentCount}: time={segment.time:F2}, dur={segment.duration:F2}, rot={segment.rotation} line: {segment.line} layer: {segment.layer} width: {segment.width} height: {segment.height}");
                        }
                    }
                    // Now skip the rest of the wall (delete the rest after the block)
                    segStart = Math.Max(segStart, Math.Min(segEnd, cutTime));
                    segEnd = segStart; // This ensures the next 'if (segEnd > segStart)' does not create another segment
                    break; // Only keep the part before the first block, then stop
                }

                // Add final segment if any left
                if (segEnd > segStart)
                {
                    float duration = segEnd - segStart;
                    if (duration >= 0.001f)
                    {
                        segmentCount++;
                        var segment = EObstacleData.Create(
                            segStart,
                            obs.line,
                            obs.layer,
                            duration,
                            obs.width,
                            obs.height,
                            RotationForSegmentStart(obs, segStart) // ← use wall’s own rotation in pass 2 when a subset of rotation events is used since without a full set the GetAccumRotationAt() would be wrong
                        );
                        kept.Add(segment);
                        //if (obsTime > 0 && obsTime < 16) Plugin.Log.Info($"[WallBlockingFix]   Segment {segmentCount}: time={segment.time:F2}, dur={segment.duration:F2}, rot={segment.rotation} line: {segment.line} layer: {segment.layer} width: {segment.width} height: {segment.height} -----------");
                    }
                }

                //if (segmentCount > 0 && obsTime > 0 && obsTime < 16) Plugin.Log.Info($"[WallBlockingFix] -------- Kept {segmentCount} part(s) of the original wall.");
                //else if (segmentCount == 0 && obsTime > 0 && obsTime < 16) Plugin.Log.Info($"[WallBlockingFix] -------- No parts of the original wall were kept, it was fully blocked by rotations.");
                obsIdx++;
            }
            Plugin.Log.Info($"[WallBlockingFix] Total output obstacles: {kept.Count} kept out of {obstacles.Count}");
            obstacles = kept;

            return obstacles;
        }

        static List<EObstacleData> ApplyWallVisionBlockingFixOLDBAD(EditableCBD eData)  // NOTES PENETRATING WALLS with/without override. OLD version but using gaze points for per object rotation information instead of rotationEvents
        {
            //var rotationEvents = eData.RotationEvents;
            var obstacles = eData.Obstacles;
            bool rotationEventsSubsetUsed = false;

            float njs = TransitionPatcher.FinalNoteJumpMovementSpeed;
            float jd = TransitionPatcher.FinalJumpDistance;

            if (njs <= 0 || jd <= 0)
            {
                Plugin.Log.Error("[WallBlockingFix] NoteJumpMovementSpeed or JumpDistance is not set, skipping wall blocking fix.");
                return obstacles;
            }

            float wallTravelTime = jd / njs;

            Plugin.Log.Info($"[WallBlockingFix] Total Obstacles: {obstacles.Count} NJD={jd:F2}, NJS={njs:F2}, wallTravelTime={wallTravelTime:F2}");

            //var rotations = rotationEvents.OrderBy(evt => evt.time).ToList(); //checked accumulted is accurate here

            var gazePoints = new List<ENoteData>();

            // Always include real color notes
            if (eData.ColorNotes != null)
                gazePoints.AddRange(eData.ColorNotes);

            if (eData.BombNotes != null)
                gazePoints.AddRange(eData.BombNotes);

            // turn chain tails into notes for gaze point checking
            if (eData.Chains != null)
            {
                foreach (var chain in eData.Chains)
                {
                    gazePoints.Add(ENoteData.Create(chain.tailTime, ColorType.None, chain.tailLine, chain.tailLayer, NoteCutDirection.Any, chain.tailRotation)); //chain.tailRotation is different than heads in some cases even on short chains.
                    //Plugin.Log.Info($"[WallBlockingFix] Added chain tail as gaze point. HEAD - time={chain.time:F2}, line={chain.line}, layer={chain.layer}, rot={chain.rotation} TAIL - time={chain.tailTime:F2}, line={chain.tailLine}, layer={chain.tailLayer}, rot={chain.rotation}");
                }
            }

            Plugin.Log.Info($"[WallBlockingFix] Gaze Points: ColorNotes={eData.ColorNotes?.Count ?? 0}, BombNotes={eData.BombNotes?.Count ?? 0}");//, ChainTails={chainTails?.Count ?? 0}");

            // (OPTIONAL) Include bombs if you also want to prevent walls from hiding bombs
            // gazePoints.AddRange(eData.BombNotes);

            // Sort them by time to speed up lookup
            gazePoints = gazePoints.OrderBy(n => n.time).ToList();

            int GetAccumRotationAt(float t)
            {
                // faster binary search
                int lo = 0, hi = gazePoints.Count - 1, ans = -1;
                while (lo <= hi)
                {
                    int mid = (lo + hi) >> 1;
                    if (gazePoints[mid].time <= t)
                    {
                        ans = mid;
                        lo = mid + 1;
                    }
                    else hi = mid - 1;
                }
                return ans >= 0 ? gazePoints[ans].rotation : 0;
            }

            int RotationForSegmentStart(EObstacleData obs, float segStart)
                => rotationEventsSubsetUsed ? obs.rotation : GetAccumRotationAt(segStart);

            // Returns true if a rotation (direction) would block this wall (by line).
            //bool WouldBlock(int lineIndex, int direction) =>
            //    (lineIndex < 2 && direction < 0) || (lineIndex > 1 && direction > 0);
            bool WouldBlock(int lineIndex, int direction, float obsTime, float rotTime)
            {
                //if (obsTime > 0 && obsTime < 16)  Plugin.Log.Info($"[WallBlockingFix] --- WouldBlock check: obs time: {obsTime} lineIndex={lineIndex}, direction={direction} at time: {rotTime}");

                if (lineIndex < 2 && direction < 0)
                {
                    //if (obsTime > 0 && obsTime < 16)  Plugin.Log.Info($"[WallBlockingFix] ----- Blocked (left wall with left turn)");
                    return true;
                }
                if (lineIndex > 1 && direction > 0)
                {
                    //if (obsTime > 0 && obsTime < 16) Plugin.Log.Info($"[WallBlockingFix] ----- Blocked (right wall with right turn)");
                    return true;
                }
                //if (obsTime > 0 && obsTime < 16)  Plugin.Log.Info($"[WallBlockingFix] ----- Not blocked");
                return false;
            }

            var kept = new List<EObstacleData>();
            int obsIdx = 0;
            bool boostedWalls = Config.Instance.AllowV2BoostedWalls;

            foreach (var obs in obstacles)
            {
                if (WallGenerator._originalWalls.Contains(obs) && WallGenerator.IsCustomNoodleWall(obs))
                    continue;

                float obsTime = obs.time;
                int line = obs.line;
                int layer = obs.layer;
                float dur = obs.duration;

                if (line < 0) line = Math.Abs(obs.line + obs.width); // right edge of left wall

                // If high wall: keep as-is, skip splitting/removal
                bool canSkip = false;
                if (layer > 11000) canSkip = true; // high walls can skip
                else if (layer > 10 && layer < 1000) canSkip = true; //floor walls can skip

                int absLine = Math.Abs(line);
                // If short distant wall: keep as-is, skip splitting/removal, but really long distant walls can esp block vision

                //BW disabled this to test
                //if (absLine > 16000 && dur < .1f) canSkip = true; // far away and short
                //else if (absLine > 15 && absLine < 1000 && dur < .1f) canSkip = true; // far away and short

                //don't use line here must use obs.line
                if (layer == 2 && obs.line == 0 && obs.width > 2) canSkip = true; //crouch wall so rotation does not matter

                if (dur < 0 && boostedWalls) canSkip = true; // boosted walls on v2 maps use negative duration to make super fast walls. these will get removed if not skipped

                if (canSkip)
                {
                    kept.Add(obs);
                    //if (obsTime > 0 && obsTime < 16)
                    //    Plugin.Log.Info($"[WallBlockingFix]   (SKIP) Keeping wall as-is: time={obsTime:F2}, dur={obs.duration:F2}, rot={obs.rotation} line: {obs.line} layer: {obs.layer} width: {obs.width} height: {obs.height}");
                    obsIdx++;
                    continue; // skip the splitting/removal logic for this wall
                }
                //*** OLD
                float visibleStart = obsTime;// + .01f; // the smaller this is, the closer to the exact start of the wall. adding a little bit may make a tiny wall but will create small walls that block vision. minwallduration is .001
                float visibleEnd = obs.endTime + wallTravelTime / 3f; // best /10 allows vision blocking walls. /4 still has questionable walls, /2 cuts too many i think
                //if (obsTime > 0 && obsTime < 16) Plugin.Log.Info($"[WallBlockingFix] Obstacle {obsIdx}: start={obsTime:F2}, dur={obs.duration:F2}, rot={obs.rotation}, visibleStart= {visibleStart} visibleEnd={visibleEnd:F2}, line={obs.line}, layer={obs.layer}, width={obs.width}, height={obs.height}");

                // Find all blocking rotations within the visible window
                var blockEvents = gazePoints //newRotationEvents
                    .Where(dt => dt.time >= visibleStart && dt.time <= visibleEnd && WouldBlock(obs.line, dt.rotation, obsTime, dt.time))
                    .Select(dt => dt.time)
                    .OrderBy(t => t)
                    .ToList();

                //if (blockEvents.Count > 0 && obsTime > 0 && obsTime < 16) Plugin.Log.Info($"[WallBlockingFix] ------ has {blockEvents.Count} blocking rotations at times: {string.Join(", ", blockEvents.Select(t => t.ToString("F2")))}");

                float segStart = obsTime;
                float segEnd = obs.endTime;
                int segmentCount = 0;

                // Go through each blocking event and split at the *time when the block reaches the player*
                foreach (var blockTime in blockEvents)
                {
                    // The time when the block rotation hits the player
                    float cutTime = blockTime - wallTravelTime / 3f; // or /2 works too. doesn't work with just blockTime
                    //***
                    if (cutTime > segStart)
                    {
                        segmentCount++;
                        float duration = Math.Min(segEnd, cutTime) - segStart;
                        if (duration >= 0.001f) // ignore tiny negative/zero segments
                        {
                            var segment = EObstacleData.Create(
                                segStart,
                                obs.line,
                                obs.layer,
                                duration,
                                obs.width,
                                obs.height,
                                RotationForSegmentStart(obs, segStart) // ← use wall’s own rotation in pass 2 when a subset of rotation events is used since without a full set the GetAccumRotationAt() would be wrong
                            );
                            kept.Add(segment);
                            //if (obsTime > 0 && obsTime < 16) Plugin.Log.Info($"[WallBlockingFix]   Segment {segmentCount}: time={segment.time:F2}, dur={segment.duration:F2}, rot={segment.rotation} line: {segment.line} layer: {segment.layer} width: {segment.width} height: {segment.height}");
                        }
                    }
                    // Now skip the rest of the wall (delete the rest after the block)
                    segStart = Math.Max(segStart, Math.Min(segEnd, cutTime));
                    segEnd = segStart; // This ensures the next 'if (segEnd > segStart)' does not create another segment
                    break; // Only keep the part before the first block, then stop
                }

                // Add final segment if any left
                if (segEnd > segStart)
                {
                    float duration = segEnd - segStart;
                    if (duration >= 0.001f)
                    {
                        segmentCount++;
                        var segment = EObstacleData.Create(
                            segStart,
                            obs.line,
                            obs.layer,
                            duration,
                            obs.width,
                            obs.height,
                            RotationForSegmentStart(obs, segStart) // ← use wall’s own rotation in pass 2 when a subset of rotation events is used since without a full set the GetAccumRotationAt() would be wrong
                        );
                        kept.Add(segment);
                        //if (obsTime > 0 && obsTime < 16) Plugin.Log.Info($"[WallBlockingFix]   Segment {segmentCount}: time={segment.time:F2}, dur={segment.duration:F2}, rot={segment.rotation} line: {segment.line} layer: {segment.layer} width: {segment.width} height: {segment.height} -----------");
                    }
                }

                //if (segmentCount > 0 && obsTime > 0 && obsTime < 16) Plugin.Log.Info($"[WallBlockingFix] -------- Kept {segmentCount} part(s) of the original wall.");
                //else if (segmentCount == 0 && obsTime > 0 && obsTime < 16) Plugin.Log.Info($"[WallBlockingFix] -------- No parts of the original wall were kept, it was fully blocked by rotations.");
                obsIdx++;
            }
            Plugin.Log.Info($"[WallBlockingFix] Total output obstacles: {kept.Count} kept out of {obstacles.Count}");
            obstacles = kept;

            return obstacles;
        }


        static List<EObstacleData> ApplyWallVisionBlockingFixNew(EditableCBD eData) // works mostly for NetZero and NoRestrictions but removing too many walls
        {
            float njs = TransitionPatcher.FinalNoteJumpMovementSpeed;
            float jd = TransitionPatcher.FinalJumpDistance;

            if (njs <= 0 || jd <= 0)
            {
                Plugin.Log.Error("[WallBlockingFix] NoteJumpMovementSpeed or JumpDistance is not set, skipping wall blocking fix.");
                return eData.Obstacles;
            }

            float wallTravelTime = jd / njs;

            var obstacles = eData.Obstacles ?? new List<EObstacleData>();

            var gazePoints = new List<ENoteData>();

            // Always include real color notes
            if (eData.ColorNotes != null)
                gazePoints.AddRange(eData.ColorNotes);

            if (eData.BombNotes != null)
                gazePoints.AddRange(eData.BombNotes);

            // turn chain tails into notes for gaze point checking
            if (eData.Chains != null)
            {
                foreach (var chain in eData.Chains)
                {
                    gazePoints.Add(ENoteData.Create(chain.tailTime, ColorType.None, chain.tailLine, chain.tailLayer, NoteCutDirection.Any, chain.tailRotation)); //chain.tailRotation is different than heads in some cases even on short chains.
                    //Plugin.Log.Info($"[WallBlockingFix] Added chain tail as gaze point. HEAD - time={chain.time:F2}, line={chain.line}, layer={chain.layer}, rot={chain.rotation} TAIL - time={chain.tailTime:F2}, line={chain.tailLine}, layer={chain.tailLayer}, rot={chain.rotation}");
                }
            }

            Plugin.Log.Info($"[WallBlockingFix] Gaze Points: ColorNotes={eData.ColorNotes?.Count ?? 0}, BombNotes={eData.BombNotes?.Count ?? 0}");//, ChainTails={chainTails?.Count ?? 0}");

            // (OPTIONAL) Include bombs if you also want to prevent walls from hiding bombs
            // gazePoints.AddRange(eData.BombNotes);

            // Sort them by time to speed up lookup
            gazePoints = gazePoints.OrderBy(n => n.time).ToList();

            Plugin.Log.Info($"[WallBlockingFix] Total Obstacles: {obstacles.Count} NJD={jd:F2}, NJS={njs:F2}, wallTravelTime={wallTravelTime:F2}");

            // Debug window
            const float DBG_START = 0;
            const float DBG_END = 30f;

            /*
            // Log notes in debug window
            foreach (var n in gazePoints)
            {
                if (n.time >= DBG_START && n.time <= DBG_END)
                {
                    Plugin.Log.Info(
                        $"[WallFixDBG][NOTE] t={n.time:F3}, line={n.line}, layer={n.layer}, " +
                        $"rot={n.rotation}, color: {n.colorType}");
                }
            }
            */
            bool IsRightSide(int line) => line > 1; // 2,3
            bool IsLeftSide(int line) => line < 2; // 0,1

            var kept = new List<EObstacleData>();
            bool boostedWalls = Config.Instance.AllowV2BoostedWalls;

            const float MAX_EXTENSION = 2f;// 3f;

            foreach (var obs in obstacles)
            {
                float obsTime = obs.time;
                int line = obs.line;
                int layer = obs.layer;
                float dur = obs.duration;

                bool debug = false;// obsTime >= DBG_START && obsTime <= DBG_END;

                // Skip noodle originals, as before
                if (WallGenerator._originalWalls.Contains(obs) && WallGenerator.IsCustomNoodleWall(obs))
                    continue;

                // Side classification for occlusion logic
                bool wallRight = IsRightSide(line);
                bool wallLeft = IsLeftSide(line);

                // ---------- skip rules (same as your original) ----------
                bool canSkip = false;
                if (layer > 11000) canSkip = true; // high walls
                else if (layer > 10 && layer < 1000) canSkip = true; // floor walls

                int absLine = Math.Abs(line);
                // optional far-wall rules you had commented out
                //if (absLine > 16000 && dur < .1f) canSkip = true;
                //else if (absLine > 15 && absLine < 1000 && dur < .1f) canSkip = true;

                if (layer == 2 && obs.line == 0 && obs.width > 2) canSkip = true; // crouch wall

                if (dur < 0 && boostedWalls) canSkip = true; // boosted v2 walls

                // Visible window
                float visibleStart = obsTime - .05f;// this extra .002 catches window panes from crossing
                float maxVisibleEnd = obs.endTime + wallTravelTime * MAX_EXTENSION; // for long arcs, can see a arcline pass through a wall even though the tail note is way later than the end of the wall.

                if (debug)
                {
                    Plugin.Log.Info(
                        $"[WallFixDBG][WALL] Wall t={obs.time:F3}→{obs.endTime:F3}, " +
                        $"line={obs.line}, layer={obs.layer}, dur={obs.duration:F3}, w={obs.width}, h={obs.height}, " +
                        $"visible=[{visibleStart:F3}, worstCase {maxVisibleEnd:F3}], rot={obs.rotation}");
                }

                if (canSkip)
                {
                    if (debug)
                    {
                        Plugin.Log.Info("[WallFixDBG][WALL]   -> SKIP (high/floor/crouch/boosted)");
                    }
                    kept.Add(obs);
                    continue;
                }

                // ---------- Find earliest note that this wall will block ----------
                float? blockTime = null;
                ENoteData blockNote = null;

                
                float extensionForDelta = MAX_EXTENSION;

                float visionBlockingWallRemovalMult = Config.Instance.VisionBlockingWallRemovalMult;

                int wallRot = obs.rotation;
                int absDelta = 0;

                foreach (var n in gazePoints)
                {
                    if (n.time < visibleStart) continue;
                    if (n.time > maxVisibleEnd) break;

                    // Must be on same side as wall to be blockable
                    //bool noteRight = IsRightSide(n.line);
                    //bool noteLeft = IsLeftSide(n.line);

                    //if (wallRight && !noteRight) continue;
                    //if (wallLeft && !noteLeft) continue;

                    int noteRot = n.rotation;
                    int delta = noteRot - wallRot;
                    absDelta = Math.Abs(delta);

                    // Must be turning TOWARD the wall side
                    bool blocks = false;
                    if (wallRight && delta > 0) blocks = true;   // rightward turn
                    if (wallLeft && delta < 0) blocks = true;   // leftward turn

                    if (!blocks) continue;
                    if (absDelta < 15) continue;

                    // Per-angle extension:
                    //  |delta| = 15  → 1.5
                    //  |delta| = 30  → 3.0
                    if (absDelta == 15) extensionForDelta = MAX_EXTENSION/2f;
                    else extensionForDelta = MAX_EXTENSION;


                    float visibleEnd = obs.endTime + wallTravelTime * extensionForDelta;

                    // If this note is too far in the future for its angle, skip it
                    if (n.time > visibleEnd) continue;

                    // This is the first note (in time) that actually blocks this wall
                    blockTime = n.time;
                    blockNote = n;
                    break;
                }

                if (debug)
                {
                    if (blockTime.HasValue)
                    {
                        Plugin.Log.Info(
                            $"[WallFixDBG][BLOCK] wallRot={wallRot}, blockTime={blockTime.Value:F3}, " +
                            $"blockNote t={blockNote.time:F3}, line={blockNote.line}, layer={blockNote.layer}, rot={blockNote.rotation} - extensionForDelta={extensionForDelta}");
                    }
                    else
                    {
                        Plugin.Log.Info("[WallFixDBG][BLOCK] NO BLOCKING NOTE for this wall");
                    }
                }

                // ---------- Apply block result ----------
                if (!blockTime.HasValue)
                {
                    // No note on that side whose rotation is same/further toward that side
                    kept.Add(obs);
                    continue;
                }

                // If angle is bigger, cut earlier (more lead time)
                float multiplier = (absDelta > 15) ? visionBlockingWallRemovalMult : visionBlockingWallRemovalMult / 2f;//.8 & .4 were letting some through
                float leadTime = wallTravelTime * multiplier;

                float cutTime = blockTime.Value - leadTime; // Convert note time into wall cut time (~when the note is at player)

                if (cutTime <= obs.time + 0.001f)
                {
                    // Entire wall happens while player is already looking along that lane -> drop it
                    if (debug)
                    {
                        Plugin.Log.Info(
                            $"[WallFixDBG][REMOVE] FULL remove wall t={obs.time:F3} " +
                            $"due to note t={blockNote.time:F3}, wallRot={wallRot}, noteRot={blockNote.rotation}, absDelta={absDelta}");
                    }
                    continue;
                }

                float newDur = Math.Min(obs.endTime, cutTime) - obs.time;

                if (newDur >= 0.001f)
                {
                    var segment = EObstacleData.Create(
                        obs.time,
                        obs.line,
                        obs.layer,
                        newDur,
                        obs.width,
                        obs.height,
                        obs.rotation  // keep original wall lane
                    );
                    kept.Add(segment);

                    if (debug)
                    {
                        Plugin.Log.Info(
                            $"[WallFixDBG][TRUNCATE] wall {obs.time:F3}→{obs.endTime:F3} " +
                            $"cut to {obs.time:F3}→{(obs.time + newDur):F3} " +
                            $"by note t={blockNote.time:F3}, wallRot={wallRot}, noteRot={blockNote.rotation}, cutTime={cutTime:F3}");
                    }
                }
            }

            Plugin.Log.Info($"[WallBlockingFix] Total output obstacles: {kept.Count} kept out of {obstacles.Count}");
            eData.Obstacles = kept;
            return kept;
        }


        static List<EObstacleData> ApplyWallVisionBlockingFix(EditableCBD eData) // COMBO Method seems to allow more walls on both sides during turns 
        {
            var obstacles = eData.Obstacles ?? new List<EObstacleData>();
            var rotationEvents = eData.RotationEvents ?? new List<ERotationEventData>();
            
            var arcs = eData.Arcs ?? new List<ESliderData>();
            bool hasArcs = arcs != null || arcs.Count > 0;

            var longChains = (eData.Chains ?? new List<ESliderData>())
                .Where(c => c.tailTime - c.time > 0.05f)
                .ToList();

            float njs = TransitionPatcher.FinalNoteJumpMovementSpeed;
            float jd = TransitionPatcher.FinalJumpDistance;

            if (njs <= 0 || jd <= 0)
            {
                Plugin.Log.Error("[WallBlockingFix] NoteJumpMovementSpeed or JumpDistance is not set, skipping wall blocking fix.");
                return obstacles;
            }

            float wallTravelTime = jd / njs;
            Plugin.Log.Info($"[WallBlockingFix] Total Obstacles: {obstacles.Count} NJD={jd:F2}, NJS={njs:F2}, wallTravelTime={wallTravelTime:F2}");

            // --- Common rotation helpers related to rotationEvents (from style1) ------------------------

            var rotations = rotationEvents.OrderBy(evt => evt.time).ToList();
            bool rotationEventsSubsetUsed = false; // if you ever add the "subset" optimization again

            int GetAccumRotationAt(float t)
            {
                int lo = 0, hi = rotations.Count - 1, ans = -1;
                while (lo <= hi)
                {
                    int mid = (lo + hi) >> 1;
                    if (rotations[mid].time <= t)
                    {
                        ans = mid;
                        lo = mid + 1;
                    }
                    else hi = mid - 1;
                }
                return ans >= 0 ? rotations[ans].accumRotation : 0;
            }

            int RotationForSegmentStart(EObstacleData obs, float segStart)
                => rotationEventsSubsetUsed ? obs.rotation : GetAccumRotationAt(segStart);

            bool WouldBlock(int lineIndex, int direction)
            {
                if (lineIndex < 2 && direction < 0) return true; // left wall, left turn
                if (lineIndex > 1 && direction > 0) return true; // right wall, right turn
                return false;
            }

            // --- Gaze points related to per object Note rotation (from style2) ------------------------------------

            var gazePoints = new List<ENoteData>();

            if (eData.ColorNotes != null)
                gazePoints.AddRange(eData.ColorNotes);
            if (eData.BombNotes != null)
                gazePoints.AddRange(eData.BombNotes);

            if (eData.Chains != null)
            {
                foreach (var chain in eData.Chains)
                {
                    gazePoints.Add(ENoteData.Create(
                        chain.tailTime,
                        ColorType.None,
                        chain.tailLine,
                        chain.tailLayer,
                        NoteCutDirection.Any,
                        chain.tailRotation));
                }
            }

            gazePoints = gazePoints.OrderBy(n => n.time).ToList();
            Plugin.Log.Info($"[WallBlockingFix] Gaze Points: ColorNotes={eData.ColorNotes?.Count ?? 0}, BombNotes={eData.BombNotes?.Count ?? 0}");

            // --- Arc overlap helper ----------------------------------------

            bool IsObstacleInArcChainWindow(EObstacleData obs)
            {
                //if (arcs == null || arcs.Count == 0) return false; // already checked

                // Simple time-interval overlap. You can tweak with tolerance if needed.
                float oStart = obs.time;
                float oEnd = obs.endTime;

                foreach (var arc in arcs)
                {
                    float aStart = arc.time;
                    float aEnd = arc.tailTime;

                    if (oEnd >= aStart && oStart <= aEnd)
                        return true;
                }
                foreach (var chain in longChains) // NEW TEST!
                {
                    float cStart = chain.time;
                    float cEnd = chain.tailTime;
                    if (oEnd >= cStart && oStart <= cEnd)
                        return true;
                }
                return false;
            }

            // --- Side helpers for NEW logic --------------------------------

            bool IsRightSide(int line) => line > 1; // 2,3
            bool IsLeftSide(int line) => line < 2; // 0,1

            bool boostedWalls = Config.Instance.AllowV2BoostedWalls;
            const float MAX_EXTENSION = 2f;

            // --- Per-wall Syle1 logic (as a helper) --------------------------

            List<EObstacleData> ApplyStyle1ForWall(EObstacleData obs)
            {
                var result = new List<EObstacleData>();

                float obsTime = obs.time;

                float mult = Config.Instance.VisionBlockingWallRemovalMult;

                // Map so OLD feels identical at mult=0.9
                float normalized = mult / 0.9f;

                float lead = (wallTravelTime / 3f) * normalized;


                // These are the same as in your OLD method
                float visibleStart = obsTime;
                float visibleEnd = obs.endTime + lead;

                var blockEvents = rotations
                    .Where(dt =>
                        dt.time >= visibleStart &&
                        dt.time <= visibleEnd &&
                        WouldBlock(obs.line, dt.rotation))
                    .Select(dt => dt.time)
                    .OrderBy(t => t)
                    .ToList();

                float segStart = obs.time;
                float segEnd = obs.endTime;
                int segmentCount = 0;

                foreach (var blockTime in blockEvents)
                {
                    float cutTime = blockTime - lead;
                    if (cutTime > segStart)
                    {
                        segmentCount++;
                        float duration = Math.Min(segEnd, cutTime) - segStart;
                        if (duration >= 0.001f)
                        {
                            var segment = EObstacleData.Create(
                                segStart,
                                obs.line,
                                obs.layer,
                                duration,
                                obs.width,
                                obs.height,
                                RotationForSegmentStart(obs, segStart)
                            );
                            result.Add(segment);
                        }
                    }

                    // Delete rest after the first block
                    segStart = Math.Max(segStart, Math.Min(segEnd, cutTime));
                    segEnd = segStart;
                    break;
                }

                if (segEnd > segStart)
                {
                    float duration = segEnd - segStart;
                    if (duration >= 0.001f)
                    {
                        segmentCount++;
                        var segment = EObstacleData.Create(
                            segStart,
                            obs.line,
                            obs.layer,
                            duration,
                            obs.width,
                            obs.height,
                            RotationForSegmentStart(obs, segStart)
                        );
                        result.Add(segment);
                    }
                }

                return result;
            }

            // --- Per-wall Style2 logic (as a helper) during arcs --------------------------

            List<EObstacleData> ApplyStyle2ForWall(EObstacleData obs)
            {
                var result = new List<EObstacleData>();

                float obsTime = obs.time;
                float dur = obs.duration;

                float visibleStart = obsTime - .05f;
                float maxVisibleEnd = obs.endTime + wallTravelTime * MAX_EXTENSION;

                bool wallRight = IsRightSide(obs.line);
                bool wallLeft = IsLeftSide(obs.line);

                float? blockTime = null;
                ENoteData blockNote = null;

                float extensionForDelta = MAX_EXTENSION;
                float visionBlockingWallRemovalMult = Config.Instance.VisionBlockingWallRemovalMult;

                int wallRot = obs.rotation;
                int absDelta = 0;

                foreach (var n in gazePoints)
                {
                    if (n.time < visibleStart) continue;
                    if (n.time > maxVisibleEnd) break;

                    int noteRot = n.rotation;
                    int delta = noteRot - wallRot;
                    absDelta = Math.Abs(delta);

                    bool blocks = false;
                    if (wallRight && delta > 0) blocks = true;
                    if (wallLeft && delta < 0) blocks = true;
                    if (!blocks) continue;
                    if (absDelta < 15) continue;

                    if (absDelta == 15) extensionForDelta = MAX_EXTENSION / 2f;
                    else extensionForDelta = MAX_EXTENSION;

                    float visibleEnd = obs.endTime + wallTravelTime * extensionForDelta;
                    if (n.time > visibleEnd) continue;

                    blockTime = n.time;
                    blockNote = n;
                    break;
                }

                if (!blockTime.HasValue)
                {
                    result.Add(obs); // keep as-is
                    return result;
                }

                float multiplier = (absDelta > 15)
                    ? visionBlockingWallRemovalMult
                    : visionBlockingWallRemovalMult / 2f;

                float leadTime = wallTravelTime * multiplier;
                float cutTime = blockTime.Value - leadTime;

                if (cutTime <= obs.time + 0.001f)
                {
                    // Entire wall removed
                    return result;
                }

                float newDur = Math.Min(obs.endTime, cutTime) - obs.time;
                if (newDur >= 0.001f)
                {
                    var segment = EObstacleData.Create(
                        obs.time,
                        obs.line,
                        obs.layer,
                        newDur,
                        obs.width,
                        obs.height,
                        obs.rotation
                    );
                    result.Add(segment);
                }

                return result;
            }

            // --- Main loop, hybrid dispatch --------------------------------

            var kept = new List<EObstacleData>();
            var arcMode = Config.Instance.ArcRotationMode;

            foreach (var obs in obstacles)
            {
                // Skip noodle custom walls
                if (WallGenerator._originalWalls.Contains(obs) && WallGenerator.IsCustomNoodleWall(obs))
                    continue;

                float obsTime = obs.time;
                int layer = obs.layer;
                float dur = obs.duration;

                bool canSkip = false;

                if (layer > 11000) canSkip = true;         // high walls
                else if (layer > 10 && layer < 1000) canSkip = true; // floor walls

                if (layer == 2 && obs.line == 0 && obs.width > 2) canSkip = true; // crouch wall
                if (dur < 0 && boostedWalls) canSkip = true;                       // boosted v2 walls

                if (canSkip)
                {
                    kept.Add(obs);
                    continue;
                }

                // Decide which logic to use for this wall
                bool useNew = hasArcs &&
                    arcMode != Config.ArcRotationModeType.ForceZero &&
                    IsObstacleInArcChainWindow(obs);

                List<EObstacleData> pieces =
                    useNew ? ApplyStyle2ForWall(obs)
                           : ApplyStyle1ForWall(obs);

                kept.AddRange(pieces);
            }

            Plugin.Log.Info($"[WallBlockingFix] Total output obstacles: {kept.Count} kept out of {obstacles.Count}");
            eData.Obstacles = kept;
            return kept;
        }

        static List<EObstacleData> ApplyWallVisionBlockingFixComboV2BAD(EditableCBD eData) // COMBO Method v2 no rotationEvents used, only note-based orientation but crossing vision
        {
            var obstacles = eData.Obstacles ?? new List<EObstacleData>();
            var arcs = eData.Arcs ?? new List<ESliderData>();

            float njs = TransitionPatcher.FinalNoteJumpMovementSpeed;
            float jd = TransitionPatcher.FinalJumpDistance;
            if (njs <= 0 || jd <= 0) return obstacles;

            float wallTravelTime = jd / njs;

            //----------------------------------------------------------------------
            // 1. BUILD NOTE-BASED ORIENTATION CURVE
            //----------------------------------------------------------------------

            List<(float time, int rot)> frames = new List<(float time, int rot)>();

            void AddFrame(float t, int r)
            {
                frames.Add((t, r));
            }

            // Color notes
            foreach (var n in eData.ColorNotes)
                AddFrame(n.time, n.rotation);

            // Bombs
            foreach (var b in eData.BombNotes)
                AddFrame(b.time, b.rotation);

            // Chains
            foreach (var c in eData.Chains)
            {
                if ((c.tailTime - c.time) > 0.05f) // must be a long chain
                    AddFrame(c.tailTime, c.rotation); // use head rotation for tail
            }

            // Sort & collapse duplicates
            frames = frames
                .OrderBy(f => f.time)
                .GroupBy(f => f.time)
                .Select(g => (g.Key, g.First().rot))
                .ToList();

            var gazePoints = frames
                .Select(f => new { time = f.time, rot = f.rot })
                .ToList();

            //----------------------------------------------------------------------
            // 2. ORIENTATION LOOKUP FROM NOTE FRAMES
            //----------------------------------------------------------------------

            const float TOL = 0.0005f;

            int GetOrientationAt(float t)
            {
                if (frames.Count == 0) return 0;

                // Before first
                if (t <= frames[0].time + TOL)
                    return frames[0].rot;

                // After last
                if (t >= frames[^1].time - TOL)
                    return frames[^1].rot;

                // Binary search: last frame ≤ t
                int lo = 0, hi = frames.Count - 1, idx = 0;
                while (lo <= hi)
                {
                    int mid = (lo + hi) >> 1;
                    if (frames[mid].time <= t + TOL)
                    {
                        idx = mid;
                        lo = mid + 1;
                    }
                    else hi = mid - 1;
                }

                // If next frame has same rotation, treat interval as flat
                if (idx < frames.Count - 1 && frames[idx].rot == frames[idx + 1].rot)
                    return frames[idx].rot;

                return frames[idx].rot;
            }

            //----------------------------------------------------------------------
            // 4. ARC OVERLAP CHECK
            //----------------------------------------------------------------------

            bool IsObstacleInArcWindow(EObstacleData obs)
            {
                float oStart = obs.time;
                float oEnd = obs.endTime;

                foreach (var arc in arcs)
                {
                    float aStart = arc.time;
                    float aEnd = arc.tailTime;

                    if (oEnd >= aStart && oStart <= aEnd)
                        return true;
                }
                return false;
            }

            //----------------------------------------------------------------------
            // 5. HELPERS
            //----------------------------------------------------------------------

            bool IsRightSide(int line) => line > 1;
            bool IsLeftSide(int line) => line < 2;

            bool WouldBlock(int wallLine, int deltaRot)
            {
                if (wallLine < 2 && deltaRot < 0) return true; // left wall blocks on CCW
                if (wallLine > 1 && deltaRot > 0) return true; // right wall blocks on CW
                return false;
            }

            //----------------------------------------------------------------------
            // 6. OLD LOGIC (rewritten for note-based orientation) // allows more wall on both sides of a turn but doesn't work well during arcs
            //----------------------------------------------------------------------

            List<EObstacleData> ApplyStyle1ForWall(EObstacleData obs) 
            {
                var result = new List<EObstacleData>();

                float obsStart = obs.time;
                float obsEnd = obs.endTime;

                float mult = Config.Instance.VisionBlockingWallRemovalMult;
                float normalized = mult / 0.9f;
                float lead = (wallTravelTime / 3f) * normalized;

                // Scan gaze points for rotation conflicts
                var blockTimes =
                    gazePoints
                    .Where(g =>
                        g.time >= obsStart &&
                        g.time <= obsEnd + lead &&
                        WouldBlock(obs.line, g.rot - obs.rotation))
                    .Select(g => g.time)
                    .ToList();

                float segStart = obsStart;
                float segEnd = obsEnd;

                if (blockTimes.Count > 0)
                {
                    float blockTime = blockTimes[0];
                    float cutTime = blockTime - lead;

                    if (cutTime > segStart)
                    {
                        float duration = Math.Min(segEnd, cutTime) - segStart;
                        if (duration >= 0.001f)
                        {
                            result.Add(EObstacleData.Create(
                                segStart, obs.line, obs.layer, duration,
                                obs.width, obs.height, obs.rotation
                            ));
                        }
                    }
                    return result; // rest removed
                }

                // No block → keep whole
                result.Add(obs);
                return result;
            }

            //----------------------------------------------------------------------
            // 7. NEW LOGIC (same as before but uses orientation from notes) // works well during arcs but removes more walls
            //----------------------------------------------------------------------

            List<EObstacleData> ApplyStyle2ForWall(EObstacleData obs)
            {
                var result = new List<EObstacleData>();

                float obsStart = obs.time;
                float obsEnd = obs.endTime;

                float visibleStart = obsStart - 0.05f;
                float maxVisibleEnd = obsEnd + wallTravelTime * 2f;

                bool wallRight = IsRightSide(obs.line);
                bool wallLeft = IsLeftSide(obs.line);

                float? blockTime = null;
                int absDelta = 0;

                foreach (var g in gazePoints)
                {
                    if (g.time < visibleStart) continue;
                    if (g.time > maxVisibleEnd) break;

                    int delta = g.rot - obs.rotation;
                    absDelta = Math.Abs(delta);

                    bool blocks =
                        (wallRight && delta > 0) ||
                        (wallLeft && delta < 0);

                    if (!blocks || absDelta < 15) continue;

                    float visibleEnd = obsEnd + wallTravelTime * (absDelta == 15 ? 1f : 2f);
                    if (g.time > visibleEnd) continue;

                    blockTime = g.time;
                    break;
                }

                if (!blockTime.HasValue)
                {
                    result.Add(obs);
                    return result;
                }

                float mult = Config.Instance.VisionBlockingWallRemovalMult;
                if (absDelta == 15) mult /= 2f;

                float cut = blockTime.Value - wallTravelTime * mult;

                if (cut <= obsStart + 0.001f)
                    return result; // whole removed

                float dur = Math.Min(obsEnd, cut) - obsStart;
                if (dur >= 0.001f)
                    result.Add(EObstacleData.Create(
                        obsStart, obs.line, obs.layer, dur,
                        obs.width, obs.height, obs.rotation
                    ));

                return result;
            }

            //----------------------------------------------------------------------
            // 8. MAIN LOOP
            //----------------------------------------------------------------------

            var kept = new List<EObstacleData>();
            bool boostedWalls = Config.Instance.AllowV2BoostedWalls;
            var arcMode = Config.Instance.ArcRotationMode;

            foreach (var obs in obstacles)
            {
                // Skip noodle custom walls
                if (WallGenerator._originalWalls.Contains(obs) &&
                    WallGenerator.IsCustomNoodleWall(obs))
                {
                    continue;
                }

                bool skip =
                    (obs.layer > 11000) ||
                    (obs.layer > 10 && obs.layer < 1000) ||
                    (obs.layer == 2 && obs.line == 0 && obs.width > 2) ||
                    (obs.duration < 0 && boostedWalls);

                if (skip)
                {
                    kept.Add(obs);
                    continue;
                }

                bool useNew =
                    arcMode != Config.ArcRotationModeType.ForceZero &&
                    IsObstacleInArcWindow(obs);

                var pieces = useNew
                    ? ApplyStyle2ForWall(obs)
                    : ApplyStyle1ForWall(obs);

                kept.AddRange(pieces);
            }

            eData.Obstacles = kept;
            return kept;
        }

        public static void PerObjectRotationLog(CustomBeatmapData customBeatmapData, EditableCBD eData)
        {
            var combinedList = new List<(float time, string type, object item)>();

            // Gather rotation events from eData
            if (eData?.RotationEvents != null)
            {
                Plugin.Log.Info($" Total Rotation Events:  {eData.RotationEvents.Count} -----------------------------------------------------------");
                foreach (var rotEvent in eData.RotationEvents)
                {
                    // rotEvent is (float time, int rotation)
                    combinedList.Add((rotEvent.time, "rot", rotEvent));
                }
            }

            // Gather notes
            foreach (var note in customBeatmapData.allBeatmapDataItems
                                               .OfType<CustomNoteData>()
                                               .Where(n => n.time < 16 && n.time > 0))
            {
                combinedList.Add((note.time, "note", note));
            }

            // Gather arcs
            foreach (var arc in customBeatmapData.allBeatmapDataItems
                                                 .OfType<CustomSliderData>()
                                                 .Where(e => e.sliderType == SliderData.Type.Normal))
            {
                combinedList.Add((arc.time, "arc", arc));
            }
            foreach (var chain in customBeatmapData.allBeatmapDataItems
                                                 .OfType<CustomSliderData>()
                                                 .Where(e => e.sliderType == SliderData.Type.Burst))
            {
                combinedList.Add((chain.time, "arc", chain));
            }


            foreach (var wall in customBeatmapData.allBeatmapDataItems
                                                 .OfType<CustomObstacleData>()
                                                 .Where(o => o.time < 16 && o.time > 0))
            {
                combinedList.Add((wall.time, "wall", wall));
            }


            // Order all by time (and by type for deterministic output if times are equal)
            var ordered = combinedList
                .OrderBy(x => x.time)
                .ThenBy(x => x.type)
                .ToList();

            float prevRotation = float.NaN;
            float runningTotalRotation = 0;

            foreach (var entry in ordered)
            {
                //if (entry.time > 74) continue;

                switch (entry.type)
                {
                    case "rot":
                        {
                            // Use your class type
                            var rot = (ERotationEventData)entry.item;
                            int deltaRot = rot.rotation;
                            runningTotalRotation += deltaRot;
                            Plugin.Log.Info($" - RotEvent - Time: {rot.time:F2}  Tot Rot: {(int)runningTotalRotation} ---------------------");//  (Δ Rotation: {(int)deltaRot}) -------------");
                            prevRotation = runningTotalRotation;
                            break;
                        }
                    case "note":
                        {
                            var note = (CustomNoteData)entry.item;
                            float totalRot = note.rotation;
                            if (float.IsNaN(prevRotation) || totalRot != prevRotation)
                            {
                                float deltaRot = float.IsNaN(prevRotation) ? 0 : totalRot - prevRotation;
                                Plugin.Log.Info($"   Note     - Time: {note.time:F2}  Tot Rot: {(int)totalRot} color: {note.colorType} Line: {note.lineIndex}  Layer: {(int)note.noteLineLayer}");//  (Δ Rotation: {(int)deltaRot})");
                                prevRotation = totalRot;
                            }
                            break;
                        }
                    case "arc":
                        {
                            var arc = (CustomSliderData)entry.item;
                            Plugin.Log.Info($"   Arc      - Time: {arc.time:F2}  Tot Rot: {arc.rotation} Line: {arc.headLineIndex}  Layer: {(int)arc.headLineLayer} --- Tail time: {arc.tailTime:F2}");//  Tot Rot: {arc.tailRotation}");
                            break;
                        }
                    case "chain":
                        {
                            var chain = (CustomSliderData)entry.item;
                            Plugin.Log.Info($"   Chain    - Time: {chain.time:F2}  Tot Rot: {chain.rotation} Line: {chain.headLineIndex}  Layer: {(int)chain.headLineLayer} --- Tail time: {chain.tailTime:F2}");//  Tot Rot: {arc.tailRotation}");
                            break;
                        }
                    case "wall":
                        {
                            var wall = (CustomObstacleData)entry.item;
                            Plugin.Log.Info($"   Wall     - Time: {wall.time:F2}  Tot Rot: {wall.rotation} Dur: {wall.duration:F2}  Line: {wall.lineIndex}  Layer: {(int)wall.lineLayer} Width: {wall.width} Height: {wall.height}"); //always logs 0!!!!!!!!!
                            break;
                        }
                }
            }
        }


        // decided to do this before convert to CustomBeatmapData
        /*
        public static CustomBeatmapData ApplyPerObjectRotations(
                CustomBeatmapData originalData,
                List<ERotationEventData> finalRotations)
        {
            // Defensive: ensure rotations are sorted
            var sortedRotations = finalRotations.OrderBy(r => r.time).ToList();

            // If no rotations, default everything to 0
            if (sortedRotations.Count == 0)
                return originalData;

            var accumulatedRotations = new List<(float time, int totalRotation)>();
            int runningRotation = 0;
            foreach (var rot in sortedRotations)
            {
                runningRotation += rot.rotation; // accumulate
                accumulatedRotations.Add((rot.time, runningRotation));
            }


            var newData = new CustomBeatmapData(originalData.numberOfLines, originalData.beatmapCustomData, originalData.levelCustomData, originalData.customData, originalData.version);

            foreach (var item in originalData.allBeatmapDataItems)
            {
                float itemTime = item.time;
                int rotation = 0;

                // Find the latest accumulated rotation <= itemTime
                for (int i = accumulatedRotations.Count - 1; i >= 0; i--)
                {
                    if (itemTime >= accumulatedRotations[i].time)
                    {
                        rotation = accumulatedRotations[i].totalRotation;
                        break;
                    }
                }

                if (item is CustomSliderData slider)
                {
                    CustomSliderData newSlider;
                    if (slider.sliderType == CustomSliderData.Type.Normal)
                    {
                        newSlider = (CustomSliderData)CustomSliderData.CreateCustomSliderData(
                            slider.colorType,
                            slider.time,
                            slider.beat,
                            rotation, // use new rotation
                            slider.headLineIndex,
                            slider.headLineLayer,
                            slider.headBeforeJumpLineLayer,
                            slider.headControlPointLengthMultiplier,
                            slider.headCutDirection,
                            slider.tailTime,
                            rotation, // head and tail must be on same rotation lane
                            slider.tailLineIndex,
                            slider.tailLineLayer,
                            slider.tailBeforeJumpLineLayer,
                            slider.tailControlPointLengthMultiplier,
                            slider.tailCutDirection,
                            slider.midAnchorMode,
                            slider.customData,
                            slider.version
                        );
                    }
                    else
                    {
                        newSlider = (CustomSliderData)CustomSliderData.CreateCustomBurstSliderData(
                            slider.colorType,
                            slider.time,
                            slider.beat,
                            rotation, // use new rotation
                            slider.headLineIndex,
                            slider.headLineLayer,
                            slider.headBeforeJumpLineLayer,
                            slider.headCutDirection,
                            slider.tailTime,
                            rotation,
                            slider.tailLineIndex,
                            slider.tailLineLayer,
                            slider.tailBeforeJumpLineLayer,
                            slider.sliceCount,
                            slider.squishAmount,
                            slider.customData,
                            slider.version
                        );
                    }

                    newData.AddBeatmapObjectDataInOrder(newSlider);
                }

                // Branch by type (your data model may vary, pseudocode here)
                else if (item is CustomNoteData note)
                {

                    // --- Arc tail/Note match logic ---
                    int? arcTailRotation = null;
                    foreach (var slide in originalData.allBeatmapDataItems.OfType<CustomSliderData>())
                    {
                        if (Math.Abs(slide.tailTime - note.time) < 0.001f)
                        {
                            arcTailRotation = slide.tailRotation; // Prefer the first match
                            break;
                        }
                    }
                    // If this note matches an arc tail, override the rotation:
                    if (arcTailRotation.HasValue)
                        rotation = arcTailRotation.Value;


                    // Determine bomb or normal note
                    CustomNoteData newNote;
                    if (note.gameplayType == GameplayType.Bomb)
                    {
                        newNote = CustomNoteData.CreateCustomBombNoteData(
                            note.time, note.beat, rotation, note.lineIndex, note.noteLineLayer, note.customData, note.version);
                    }
                    else
                    {
                        newNote = CustomNoteData.CreateCustomBasicNoteData(
                            note.time, note.beat, rotation, note.lineIndex, note.noteLineLayer, note.colorType, note.cutDirection, note.customData, note.version);
                    }
                    newData.AddBeatmapObjectDataInOrder(newNote);
                }
                else if (item is CustomObstacleData obs)
                {
                    var newObs = new CustomObstacleData(
                        obs.time, obs.beat, obs.endBeat, rotation,
                        obs.lineIndex, obs.lineLayer, obs.duration, obs.width, obs.height,
                        obs.customData, obs.version);
                    newData.AddBeatmapObjectDataInOrder(newObs);
                }

                else if (item is BasicBeatmapEventData evt)
                {
                    // Only keep lighting events; you can fine-tune this filter if needed
                    if (evt.basicBeatmapEventType != BasicBeatmapEventType.Event14 &&
                        evt.basicBeatmapEventType != BasicBeatmapEventType.Event15)
                    {
                        newData.InsertBeatmapEventDataInOrder(evt);
                    }

                    // Omit rotation events on purpose
                }
                
                //else
                //{
                //    // Keep other data types unchanged, if needed
                //    newData.AddBeatmapObjectData(item);
                //}
                
            }

            return newData;
        }

        */
    }


}