using BeatmapSaveDataVersion3;
using CustomJSONData;
using CustomJSONData.CustomBeatmap;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static AutoBS.RotationV3Registry;
using static BeatmapDataStats;
using static BloomPrePassRenderDataSO;
using static NoteData;
using static UnityEngine.UI.Image;

namespace AutoBS
{
    using AutoBS.Patches;
    using AutoBS.UI;
    using BeatmapSaveDataVersion4;
    using CustomJSONData.CustomBeatmap;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.InteropServices.WindowsRuntime;
    using System.Text.Json.Serialization;
    using UnityEngine.Assertions.Must;
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
        public int beforeJumpLayer { get; set; } // Github Issue #2 Walls gone when using autolights (and last notes of song have no animation bounce)

        public NoteCutDirection cutDirection { get; set; }
        public int rotation { get; set; } = 0; //accumulated rotation as held by customNoteData

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
            beforeJumpLayer = (int)original.beforeJumpNoteLineLayer;
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
            beforeJumpLayer = (int)original.beforeJumpNoteLineLayer;
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
                beforeJumpLayer = 0, // animation seems always to come from 0 layer
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
        public CustomNoteData ToCustomNoteData(Version version)
        {

            return new CustomNoteData(
                time: this.time,
                beat: this.beat,
                rotation: this.rotation,
                lineIndex: this.line,
                noteLineLayer: (NoteLineLayer)this.layer,
                beforeJumpNoteLineLayer: (NoteLineLayer)this.beforeJumpLayer,
                gameplayType: this.gameplayType,
                scoringType: this.scoringType,
                colorType: this.colorType,
                cutDirection: this.cutDirection,
                timeToNextColorNote: 0f,
                timeToPrevColorNote: 0f,
                flipLineIndex: this.line,
                flipYSide: 0f,
                cutDirectionAngleOffset: 0f,
                cutSfxVolumeMultiplier: 1f, 
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
                startBeat: this.beat,    
                duration: this.duration,
                endBeat: this.endBeat, 
                rotation: this.rotation, 
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
                startBeat: this.beat,    
                duration: this.duration,
                endBeat: this.endBeat, 
                rotation: this.rotation,        
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

        public float headCutDirectionAngleOffset { get; set; } = 0f;
        public float tailCutDirectionAngleOffset { get; set; } = 0f;

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
            headBeforeJumpLineLayer = (int)original.headBeforeJumpLineLayer;
            cutDirection = original.headCutDirection;
            tailTime = original.tailTime;
            tailLine = original.tailLineIndex;
            tailLayer = (int)original.tailLineLayer;
            tailBeforeJumpLineLayer = (int)original.tailBeforeJumpLineLayer;
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
                hasHeadNote = hasHeadNote,
                hasTailNote = hasTailNote,
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
                headBeforeJumpLineLayer: (NoteLineLayer)this.headBeforeJumpLineLayer,
                headControlPointLengthMultiplier: this.headControlPointLengthMultiplier,
                headCutDirection: this.cutDirection,
                headCutDirectionAngleOffset: headCutDirectionAngleOffset,
                hasTailNote: this.hasTailNote,
                tailTime: this.tailTime,
                tailRotation: this.rotation, // same as head!!!!!!!!!!!!!!!!
                tailLineIndex: this.tailLine,
                tailLineLayer: (NoteLineLayer)this.tailLayer,
                tailBeforeJumpLineLayer: (NoteLineLayer)this.tailBeforeJumpLineLayer,
                tailControlPointLengthMultiplier: this.tailControlPointLengthMultiplier,
                tailCutDirection: this.tailCutDirection,
                tailCutDirectionAngleOffset: tailCutDirectionAngleOffset,
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
                headBeat: beatValue,
                headRotation: this.rotation,
                headLineIndex: this.line,
                headLineLayer: (NoteLineLayer)this.layer,
                headBeforeJumpLineLayer: (NoteLineLayer)this.layer,
                headControlPointLengthMultiplier: this.headControlPointLengthMultiplier,
                headCutDirection: this.cutDirection,
                headCutDirectionAngleOffset: headCutDirectionAngleOffset,
                hasTailNote: this.hasTailNote,
                tailTime: this.tailTime,
                tailRotation: this.rotation, // same as head!!!!!!!!!!!!!!!!
                tailLineIndex: this.tailLine,
                tailLineLayer: (NoteLineLayer)this.tailLayer,
                tailBeforeJumpLineLayer: (NoteLineLayer)this.tailLayer,
                tailControlPointLengthMultiplier: this.tailControlPointLengthMultiplier,
                tailCutDirection: this.tailCutDirection,
                tailCutDirectionAngleOffset: tailCutDirectionAngleOffset,
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
            if (rotationEvents == null || rotationEvents.Count == 0)
                return rotationEvents ?? new List<ERotationEventData>();

            rotationEvents = rotationEvents
                .Where(r => r.rotation != 0)     // enforce “no zero rotations”
                .OrderBy(r => r.time)
                .ToList();

            int accum = 0;
            foreach (var rot in rotationEvents)
            {
                accum += rot.rotation;
                rot.accumRotation = accum;
            }
            return rotationEvents;
        }


        public ERotationEventData() { } // Default constructor needed for Create method

        public ERotationEventData(RotationEventData original)
        {
            time = original.beat;
            rotation = (int)original.rotation;
            accumRotation = 0;
            customData = new CustomData(); // v3 rotation events NEVER contain customData
        }

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
                basicBeatmapEventType: this.basicBeatmapEventType, 
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
                basicBeatmapEventType: this.basicBeatmapEventType, 
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

            // Preserve customData when available
            if (original is CustomColorBoostBeatmapEventData cb)
                customData = cb.customData ?? new CustomData();
            else
                customData = new CustomData();
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

        public bool MapWasAltered { get; set; }

        public bool RotationEventsChanged { get; set; } = false; // this will cause Color Notes, Bomb Notes, Arcs, and Chains to change since will add per object rotations
        public bool ColorNotesChanged { get; set; } = false;
        public bool BombNotesChanged { get; set; } = false;
        public bool ObstaclesChanged { get; set; } = false;
        public bool ArcsChanged { get; set; } = false;
        public bool ChainsChanged { get; set; } = false;

        public bool BasicEventsChanged { get; set; } = false;
        public bool ColorBoostEventsChanged { get; set; } = false;
        public bool CustomEventsChanged { get; set; } = false;

        public bool RotationModeLate { get; set; } = false; // false for Gen360 but can be set to true when beatmapData is converted to eData and has late events in basicData


        public List<ENoteData> ColorNotes { get; set; }
        public List<ENoteData> BombNotes { get; set; }
        public List<EObstacleData> Obstacles { get; set; }
        public List<ESliderData> Arcs { get; set; }
        public List<ESliderData> Chains { get; set; }
        public List<ERotationEventData> RotationEvents { get; set; }

        //public List<ERotationEventData> RotationEventsMatchEarlyPerObject { get; set; } // this is created at the last step after assigning per object and is used to output rotation events for v2/3 JSON dat files. but this list casues more wall and arc rotaiton problems for json. best to use original rotation events
        public List<EBasicEventData> BasicEvents { get; set; }
        public List<EColorBoostEvent> ColorBoostEvents { get; set; }
        public List <ECustomEventData> CustomEvents { get; set; }
        public CustomData BeatmapCustomData { get; }
        public CustomData LevelCustomData { get; }
        public CustomData CustomData { get; }
        public Version Version { get; }
        public bool MapAlreadyUsesEnvColorBoost { get; set; } = false;
        public bool MapAlreadyUsesArcs { get; set; } = false;
        public bool MapAlreadyUsesChains { get; set; } = false;
        public bool MapAlreadyUsesRotations { get; set; } = false;
        public int OriginalObstacleCount { get; set; } = 0;
        public List<(float time, int rotationSteps)> WallCutMoments { get; set; }
            = new List<(float time, int rotationSteps)>(); // Rotation cut moments for wall gap logic, filled by RotationGenerator.

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

            Obstacles = original.allBeatmapDataItems
                .OfType<ObstacleData>()
                .OrderBy(o => o.time)
                .Select(o => new EObstacleData(o))
                .ToList();

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

            RotationEvents = new List<ERotationEventData>();

            //no v2 or v3 files will come to this method at all. but someone could program rotations into basic events. v4.0.0 has SpawnRotations but was removed in v4.1.0 so very little change of those existing in any map

            var basicRotEvents = original.allBeatmapDataItems
                .OfType<BasicBeatmapEventData>()
                .Where(e =>
                    e.basicBeatmapEventType == BasicBeatmapEventType.Event14 || // early
                    e.basicBeatmapEventType == BasicBeatmapEventType.Event15    // late
                )
                .OrderBy(e => e.time)
                .ToList();

            if (basicRotEvents.Count > 0)
            {
                int earlyCount = basicRotEvents.Count(e => e.basicBeatmapEventType == BasicBeatmapEventType.Event14);
                int lateCount  = basicRotEvents.Count(e => e.basicBeatmapEventType == BasicBeatmapEventType.Event15);

                // Majority vote; tie-breaker: prefer late (or early—your choice)
                RotationModeLate = lateCount >= earlyCount;

                int accum = 0;

                foreach (var e in basicRotEvents)
                {
                    // v2: value is a step that needs scaling (e.g., *15)
                    // v3: value is already degrees; if you’re not sure you can treat 2+3 the same using your helper
                    int deltaDegrees = RotationGenerator.SpawnRotationValueToDegrees(e.value);

                    accum += deltaDegrees;

                    RotationEvents.Add(new ERotationEventData(
                        e.time,        // time in beats
                        deltaDegrees,  // delta rotation
                        accum          // accumRotation
                                       // customData omitted, ctor defaults to new CustomData()
                    ));
                }

                Plugin.LogDebug($"[EditableCBD] - RotationImport BasicEvents- Loaded {RotationEvents.Count} rotation events. early={earlyCount}, late={lateCount} => using {(RotationModeLate ? "LATE" : "EARLY")}");
            }
            
            if (RotationEvents.Count == 0)
                RotationEvents = BuildInlineRotations(ColorNotes, BombNotes, Obstacles, Arcs, Chains);

            BasicEvents = original.allBeatmapDataItems
                .OfType<BasicBeatmapEventData>()
                .Where(e => e.basicBeatmapEventType != BasicBeatmapEventType.Event14 && e.basicBeatmapEventType != BasicBeatmapEventType.Event15 && e.basicBeatmapEventType != BasicBeatmapEventType.Event5)
                .OrderBy(e => e.time)
                .Select(e => new EBasicEventData(e))
                .ToList();

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

            //v1.42 not sure why but getting a color boost event false at time 0 when doesn't exist in the map. so check if more than 1 boost event to set the flag.
            MapAlreadyUsesEnvColorBoost = ColorBoostEvents?.Count > 1;
            MapAlreadyUsesArcs = Arcs?.Count > 0;
            MapAlreadyUsesChains = Chains?.Count > 0;
            MapAlreadyUsesRotations = RotationEvents?.Count > 1;

            OriginalObstacleCount = Obstacles.Count;

            Plugin.LogDebug($"[EditableCBD] MapAlreadyUsesEnvColorBoost: {MapAlreadyUsesEnvColorBoost}, MapAlreadyUsesArcs: {MapAlreadyUsesArcs}, MapAlreadyUsesChains: {MapAlreadyUsesChains}, MapAlreadyUsesRotations: {MapAlreadyUsesRotations}");

            LinkArcEndpointsToNotes();
        }

        // Creates an EditableCBD from a CustomBeatmapData object!!!!!!!!!!!!!!
        public EditableCBD(CustomBeatmapData original)
        {
            OriginalCBData = original; // keep a copy of the original data for the final conversion back to CustomBeatmapData since waypoints, etc are not needed to be used or altered in EditableCBD
            
            BeatmapCustomData = original.beatmapCustomData ?? new CustomData();
            LevelCustomData = original.levelCustomData ?? new CustomData();
            CustomData = original.customData ?? new CustomData();
            Version = TransitionPatcher.SelectedBeatmapVersion;

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

            RotationEvents = new List<ERotationEventData>();

            // RotationEvents
            if (Version.Major == 3)
            {
                // Try to look up v3 save-data rotations
                if (RotationV3Registry.RotationEventsByKey.TryGetValue(TransitionPatcher.SelectedPlayKey, out var v3RotList)
                    && v3RotList != null && v3RotList.Count > 0)
                {
                    var sorted = v3RotList.OrderBy(re => re.beat).ToList();
                    float bps = TransitionPatcher.bpm / 60f;

                    var v3List = sorted
                        .Select(re => new ERotationEventData(
                            re.beat / bps,
                            (int)re.rotation,
                            0
                        ))
                        .Where(r => r.rotation != 0)
                        .OrderBy(r => r.time)
                        .ToList();

                    if (v3List.Count > 0)
                    {
                        RotationEvents = ERotationEventData.RecalculateAccumulatedRotations(v3List);

                        // majority vote using execution
                        int earlyCount = v3RotList.Count(r => r.execution == 0);
                        int lateCount = v3RotList.Count(r => r.execution == 1);
                        RotationModeLate = lateCount >= earlyCount;

                        Plugin.LogDebug($"[EditableCBD] - RotationImport v3 - Loaded {RotationEvents.Count} rotation events. early={earlyCount}, late={lateCount} => using {(RotationModeLate ? "LATE" : "EARLY")}");
                    }
                    else
                        Plugin.LogDebug($"[EditableCBD] - RotationImport v3 - Loaded 0 rotation events.");
                }
                
            }
            else if (Version.Major == 2) // v2 custom maps
            {
                var v2List = original.beatmapEventDatas
                    .OfType<CustomBasicBeatmapEventData>()
                    .Where(e => e.basicBeatmapEventType == BasicBeatmapEventType.Event14 || e.basicBeatmapEventType == BasicBeatmapEventType.Event15)
                    .Select(e => new ERotationEventData(
                        e.time,
                        RotationGenerator.SpawnRotationValueToDegrees(e.value),
                        0,
                        e.customData ?? new CustomData()
                    ))
                    .Where(r => r.rotation != 0)
                    .OrderBy(r => r.time)
                    .ToList();

                if (v2List.Count > 0)
                {
                    RotationEvents = ERotationEventData.RecalculateAccumulatedRotations(v2List);

                    int earlyCount = original.beatmapEventDatas.OfType<CustomBasicBeatmapEventData>()
                        .Count(e => e.basicBeatmapEventType == BasicBeatmapEventType.Event14);
                    int lateCount = original.beatmapEventDatas.OfType<CustomBasicBeatmapEventData>()
                        .Count(e => e.basicBeatmapEventType == BasicBeatmapEventType.Event15);

                    // Majority; tie-breaker prefer late (consistent with prior suggestion)
                    RotationModeLate = lateCount >= earlyCount;

                    Plugin.LogDebug($"[EditableCBD] - RotationImport v2 - Loaded {RotationEvents.Count} rotation events. early={earlyCount}, late={lateCount} => using {(RotationModeLate ? "LATE" : "EARLY")}");
                }
                else
                    Plugin.LogDebug($"[EditableCBD] - RotationImport v2 - Loaded 0 rotation events.");
            }
            else if (Version.Major == 4)
            {
                if (RotationEvents.Count == 0)
                    RotationEvents = BuildInlineRotations(ColorNotes, BombNotes, Obstacles, Arcs, Chains); // Fallback: synthesize from inline 'r' in the v4 beatmap 360 JSON (early events)
            }

            // Basic lighting events (exclude rotation & color boost)
            BasicEvents = original.beatmapEventDatas
                .OfType<CustomBasicBeatmapEventData>()
                .Where(e => e.basicBeatmapEventType != BasicBeatmapEventType.Event14 && e.basicBeatmapEventType != BasicBeatmapEventType.Event15 && e.basicBeatmapEventType != BasicBeatmapEventType.Event5)
                .OrderBy(e => e.time)
                .Select(e => new EBasicEventData(e))
                .ToList();

            // Color Boost Events (robust across v2/v3 loaders) possibly because the runtime moves v2 into this already
            ColorBoostEvents =
                original.allBeatmapDataItems
                    .OfType<ColorBoostBeatmapEventData>()
                    .Select(e =>
                    {
                        // preserve customData when present
                        var cd = (e as CustomColorBoostBeatmapEventData)?.customData;
                        return EColorBoostEvent.Create(e.time, e.boostColorsAreOn, cd);
                    })
                    .OrderBy(e => e.time)
                    .ToList();

            // Fallback: legacy v2 boost encoded as Basic Event5 -- i don't think this gets used...
            if (ColorBoostEvents.Count == 0 && Version.Major == 2)
            {
                ColorBoostEvents = original.beatmapEventDatas
                    .OfType<BasicBeatmapEventData>()
                    .Where(e => e.basicBeatmapEventType == BasicBeatmapEventType.Event5)
                    .Select(e =>
                    {
                        var cd = (e as CustomBasicBeatmapEventData)?.customData;
                        return EColorBoostEvent.Create(e.time, e.value == 1, cd);
                    })
                    .OrderBy(e => e.time)
                    .ToList();
            }

            //v1.42 not sure why but getting a color boost event false at time 0 when doesn't exist in the map. so check if more than 1 boost event to set the flag.
            MapAlreadyUsesEnvColorBoost = ColorBoostEvents?.Count > 1;
            MapAlreadyUsesArcs = Arcs?.Count > 0;
            MapAlreadyUsesChains = Chains?.Count > 0;
            MapAlreadyUsesRotations = RotationEvents?.Count > 1;


            OriginalObstacleCount = Obstacles.Count;

            Plugin.LogDebug($"[EditableCBD] MapAlreadyUsesEnvColorBoost: {MapAlreadyUsesEnvColorBoost}, MapAlreadyUsesArcs: {MapAlreadyUsesArcs}, MapAlreadyUsesChains: {MapAlreadyUsesChains}, MapAlreadyUsesRotations: {MapAlreadyUsesRotations}");

            CustomEvents = original.customEventDatas
                    .Select(e => new ECustomEventData(e))
                    .OrderBy(e => e.time)
                    .ToList();

            LinkArcEndpointsToNotes();
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

            Plugin.LogDebug($"[BuildInlineRotations] {rotations.Count} rotationEvents created from per object rotations.");

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
                Plugin.LogDebug("[TestForNoodleCustomData] Checking for Noodle Problem Attributes...");
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
                foreach (var wall in WallGenerator.originalWalls)
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
                    Plugin.LogDebug($"[TestForNoodleCustomData] Contains Noodle Problem Attributes: eNotes: {noteProblemCount} eObs: {obstacleProblemCount}! (This stops counting at 11.) (out of eNotes: {eData.ColorNotes.Count} and eObs: {eData.Obstacles.Count})!");
                }
                else
                {
                    Plugin.LogDebug($"[TestForNoodleCustomData] No Noodle Problem Attributes Found (out of eNotes: {eData.ColorNotes.Count} and eObs: {eData.Obstacles.Count})!");
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
            var newData = new CustomBeatmapData(
                numberOfLines: 4, // Standard; parameterize if you support others
                beatmapCustomData: eData.BeatmapCustomData ?? new CustomData(),
                levelCustomData: eData.LevelCustomData ?? new CustomData(),
                customData: new CustomData(), // container-level custom data
                version: eData.Version
            );

            var allItems = new List<object>(capacity: 4096);

            // --- Your edited objects ---
            if (!eData.ColorNotesChanged)
            {
                allItems.AddRange(GetOriginalColorNotes(eData));
                Plugin.LogDebug($"[ConvertEditableCBD] Color Notes NOT ALTERED. Using original (if they exist).");
            }
            else
                allItems.AddRange(eData.ColorNotes.Select(n => (object)n.ToCustomNoteData(eData.Version)));

            if (!eData.BombNotesChanged)
            {
                allItems.AddRange(GetOriginalBombNotes(eData));
                Plugin.LogDebug($"[ConvertEditableCBD] Bomb Notes NOT ALTERED. Using original (if they exist).");
            }
            else
                allItems.AddRange(eData.BombNotes.Select(n => (object)n.ToCustomNoteData(eData.Version)));

            // Obstacles
            if (!eData.ObstaclesChanged)
            { 
                allItems.AddRange(GetOriginalObstacles(eData));
                Plugin.LogDebug($"[ConvertEditableCBD] Obstacles NOT ALTERED. Using original (if they exist).");
            }
            else
                allItems.AddRange(eData.Obstacles.Select(o => (object)o.ToCustomObstacleData(eData.Version)));

            // Arcs
            if (!eData.ArcsChanged)
            { 
                allItems.AddRange(GetOriginalArcs(eData));
                Plugin.LogDebug($"[ConvertEditableCBD] Arcs NOT ALTERED. Using original (if they exist).");
            }
            else
                allItems.AddRange(eData.Arcs.Select(a => (object)a.ToCustomSliderData(eData.Version)));

            // Chains
            if (!eData.ChainsChanged)
            { 
                allItems.AddRange(GetOriginalChains(eData));
                Plugin.LogDebug($"[ConvertEditableCBD] Chains NOT ALTERED. Using original (if they exist).");
            }
            else
                allItems.AddRange(eData.Chains.Select(c => (object)c.ToCustomSliderData(eData.Version)));

            // Basic events
            if (!eData.BasicEventsChanged)
            { 
                allItems.AddRange(GetOriginalBasicEvents(eData));
                Plugin.LogDebug($"[ConvertEditableCBD] Basic Events NOT ALTERED. Using original (if they exist).");
            }
            else
                allItems.AddRange(eData.BasicEvents.Select(ev => (object)ev.ToCustomBasicBeatmapEventData(eData.Version)));

            // Color boost events
            if (!eData.ColorBoostEventsChanged)
            { 
                allItems.AddRange(GetOriginalColorBoostEvents(eData));
                Plugin.LogDebug($"[ConvertEditableCBD] Color Boosts NOT ALTERED. Using original (if they exist).");
            }
            else
                allItems.AddRange(eData.ColorBoostEvents.Select(ev => (object)ev.ToCustomColorBoostBeatmapEventData(eData.Version)));

            // --- Custom events: choose ONE source ---
            if (eData.CustomEventsChanged && (eData.CustomEvents?.Count ?? 0) > 0)
            {
                allItems.AddRange(eData.CustomEvents.Select(cev => (object)cev.ToCustomEventData(eData.Version)));
            }
            else
            {
                allItems.AddRange(GetOriginalCustomEvents(eData));
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

            Plugin.LogDebug($"[ConvertEditableCBD] -> CustomBeatmapData: " +
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
            if (eData.OriginalBData == null)
                throw new InvalidOperationException("ConvertVanilla requires OriginalBData.");

            ESliderData.FixArcChainNoteScoring(eData);
            // numberOfLines: 4 for Standard; adjust if you support others
            var newData = new BeatmapData(4);

            var allItems = new List<object>(capacity: 4096);

            // -------------------------
            // 1) Objects (Notes/Bombs/Obstacles/Sliders)
            // -------------------------

            // Color notes
            if (!eData.ColorNotesChanged)
            { 
                allItems.AddRange(GetOriginalColorNotes(eData)); // returns NoteData when OriginalBData exists
                Plugin.LogDebug($"[ConvertEditableCBD] Color Notes NOT ALTERED. Using original (if they exist).");
            }
            else
                allItems.AddRange(eData.ColorNotes.Select(n => (object)n.ToNoteData()));

            // Bomb notes
            if (!eData.BombNotesChanged)
            { 
                allItems.AddRange(GetOriginalBombNotes(eData));
                Plugin.LogDebug($"[ConvertEditableCBD] Bomb Notes NOT ALTERED. Using original (if they exist).");
            }
            else
                allItems.AddRange(eData.BombNotes.Select(n => (object)n.ToNoteData()));

            // Obstacles
            if (!eData.ObstaclesChanged)
            { 
                allItems.AddRange(GetOriginalObstacles(eData));
                Plugin.LogDebug($"[ConvertEditableCBD] Obstacles NOT ALTERED. Using original (if they exist).");
            }
            else
                allItems.AddRange(eData.Obstacles.Select(o => (object)o.ToObstacleData()));

            // Arcs (normal sliders)
            if (!eData.ArcsChanged)
            { 
                allItems.AddRange(GetOriginalArcs(eData));
                Plugin.LogDebug($"[ConvertEditableCBD] Arcs NOT ALTERED. Using original (if they exist).");
            }
            else
                allItems.AddRange(eData.Arcs.Select(a => (object)a.ToSliderData()));

            // Chains (burst sliders)
            if (!eData.ChainsChanged)
            { 
                allItems.AddRange(GetOriginalChains(eData));
                Plugin.LogDebug($"[ConvertEditableCBD] Chains NOT ALTERED. Using original (if they exist).");
            }
            else
                allItems.AddRange(eData.Chains.Select(c => (object)c.ToSliderData()));

            // -------------------------
            // 2) Events (Basic + ColorBoost)
            // -------------------------

            if (!eData.BasicEventsChanged)
            { 
                allItems.AddRange(GetOriginalBasicEvents(eData));
                Plugin.LogDebug($"[ConvertEditableCBD] Basic Events NOT ALTERED. Using original (if they exist).");
            }
            else
                allItems.AddRange(eData.BasicEvents.Select(ev => (object)ev.ToBasicBeatmapEventData()));

            if (!eData.ColorBoostEventsChanged)
            { 
                allItems.AddRange(GetOriginalColorBoostEvents(eData));
                Plugin.LogDebug($"[ConvertEditableCBD] Color Boosts NOT ALTERED. Using original (if they exist).");
            }
            else
                allItems.AddRange(eData.ColorBoostEvents.Select(ev => (object)ev.ToColorBoostBeatmapEventData()));


            // -------------------------
            // 3) Pass-through everything else (vanilla-only)
            foreach (var item in eData.OriginalBData.allBeatmapDataItems)
            {
                if (item is NoteData
                 || item is ObstacleData
                 || item is SliderData
                 || item is BasicBeatmapEventData
                 || item is ColorBoostBeatmapEventData)
                    continue;

                if (item is BeatmapEventData evt)
                    allItems.Add(evt);
            }

            foreach (var item in allItems.OrderBy(i =>
                i is BeatmapObjectData o ? o.time :
                i is BeatmapEventData e ? e.time :
                float.MaxValue))
            {
                if (item is BeatmapObjectData o)
                    newData.AddBeatmapObjectDataInOrder(o);
                else if (item is BeatmapEventData e)
                    newData.InsertBeatmapEventDataInOrder(e);
            }

            newData.ProcessAndSortBeatmapData();
            return newData;
        }

        // used to load original data when no changes made to that type
        private static IEnumerable<object> GetOriginalColorNotes(EditableCBD eData)
        {
            if (eData.OriginalCBData != null)
                return eData.OriginalCBData.beatmapObjectDatas
                    .OfType<CustomNoteData>()
                    .Where(n => n.cutDirection != NoteCutDirection.None)
                    .Cast<object>();

            if (eData.OriginalBData != null)
                return eData.OriginalBData.allBeatmapDataItems
                    .OfType<NoteData>()
                    .Where(n => n.cutDirection != NoteCutDirection.None)
                    .Cast<object>();

            return Enumerable.Empty<object>();
        }

        private static IEnumerable<object> GetOriginalBombNotes(EditableCBD eData)
        {
            if (eData.OriginalCBData != null)
                return eData.OriginalCBData.beatmapObjectDatas
                    .OfType<CustomNoteData>()
                    .Where(n => n.cutDirection == NoteCutDirection.None)
                    .Cast<object>();

            if (eData.OriginalBData != null)
                return eData.OriginalBData.allBeatmapDataItems
                    .OfType<NoteData>()
                    .Where(n => n.cutDirection == NoteCutDirection.None)
                    .Cast<object>();

            return Enumerable.Empty<object>();
        }

        private static IEnumerable<object> GetOriginalObstacles(EditableCBD eData)
        {
            if (eData.OriginalCBData != null)
                return eData.OriginalCBData.beatmapObjectDatas
                    .OfType<CustomObstacleData>()
                    .Cast<object>();

            if (eData.OriginalBData != null)
                return eData.OriginalBData.allBeatmapDataItems
                    .OfType<ObstacleData>()
                    .Cast<object>();

            return Enumerable.Empty<object>();
        }


        private static IEnumerable<object> GetOriginalArcs(EditableCBD eData)
        {
            if (eData.OriginalCBData != null)
                return eData.OriginalCBData.beatmapObjectDatas
                    .OfType<CustomSliderData>()
                    .Where(s => s.sliderType == SliderData.Type.Normal)
                    .Cast<object>();

            if (eData.OriginalBData != null)
                return eData.OriginalBData.allBeatmapDataItems
                    .OfType<SliderData>()
                    .Where(s => s.sliderType == SliderData.Type.Normal)
                    .Cast<object>();

            return Enumerable.Empty<object>();
        }

        private static IEnumerable<object> GetOriginalChains(EditableCBD eData)
        {
            if (eData.OriginalCBData != null)
                return eData.OriginalCBData.beatmapObjectDatas
                    .OfType<CustomSliderData>()
                    .Where(s => s.sliderType == SliderData.Type.Burst)
                    .Cast<object>();

            if (eData.OriginalBData != null)
                return eData.OriginalBData.allBeatmapDataItems
                    .OfType<SliderData>()
                    .Where(s => s.sliderType == SliderData.Type.Burst)
                    .Cast<object>();

            return Enumerable.Empty<object>();
        }

        private static IEnumerable<object> GetOriginalBasicEvents(EditableCBD eData)
        {
            if (eData.OriginalCBData != null)
                return eData.OriginalCBData.allBeatmapDataItems
                    .OfType<CustomBasicBeatmapEventData>()
                    .Cast<object>();

            if (eData.OriginalBData != null)
                return eData.OriginalBData.allBeatmapDataItems
                    .OfType<BasicBeatmapEventData>()
                    .Cast<object>();

            return Enumerable.Empty<object>();
        }

        private static IEnumerable<object> GetOriginalColorBoostEvents(EditableCBD eData)
        {
            if (eData.OriginalCBData != null)
                return eData.OriginalCBData.allBeatmapDataItems
                    .OfType<ColorBoostBeatmapEventData>() // includes custom variant
                    .Cast<object>();

            if (eData.OriginalBData != null)
                return eData.OriginalBData.allBeatmapDataItems
                    .OfType<ColorBoostBeatmapEventData>()
                    .Cast<object>();

            return Enumerable.Empty<object>();
        }


        private static IEnumerable<object> GetOriginalCustomEvents(EditableCBD eData)
        {
            if (eData.OriginalCBData?.customEventDatas is { } orig && orig.Count > 0)
                return orig.Cast<object>();

            return Enumerable.Empty<object>();
        }


        /// <summary>
        /// Updates all ENoteData, EObstacleData and ESliderData rotations in-place,
        /// using eData.RotationEvents and the headNote/tailNote links on each slider.
        /// </summary>
        public static void ApplyPerObjectRotations(EditableCBD eData)
        {
            bool rotationModeLate = eData.RotationModeLate;

            Plugin.LogDebug($"[ApplyPerObjectRotations] ---------- Starting per-object rotation application (using RotationModeLate: {rotationModeLate})");

            if (eData.RotationEvents.Count == 0)
                return;

            // 1) Sort your raw rotation events by time
            eData.RotationEvents = eData.RotationEvents
                    .OrderBy(r => r.time)
                    .ToList();

            // 2) Build a cumulative list: at each event time, what’s the running total?
            var accumulated = new List<(float time, int total)>();

            foreach (var evt in eData.RotationEvents)
            {
                accumulated.Add((evt.time, evt.accumRotation)); // i checked this is accurate
                //if (evt.time > 55 && evt.time < 65) Plugin.Log.Info($"[RotationApplier] Rotation: {evt.time:F} Rot: {evt.rotation} Accum: {evt.accumRotation}");
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
            int rotatedNoteCount = 0;
            foreach (var note in eData.ColorNotes)
            {
                note.rotation = GetAccumRotationAt(note.time);
                rotatedNoteCount++;
                //if (note.time < 30)
                //    Plugin.Log.Info($"[RotationApplier] Note @{note.time:F2}s → rot: {note.rotation} line: {note.line} layer: {note.layer} color: {note.colorType}");
            }
            Plugin.LogDebug($"[ApplyPerObjectRotations] Notes given per object rotation: {rotatedNoteCount}");

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

                arc.rotation     = desiredAccum;
                arc.tailRotation = desiredAccum;// tailRot;

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

                chain.rotation = headRot;
                chain.tailRotation = headRot;// tailRot; // this is giving different rotations to tails! seems to work fine anyway

                //if (chain.time < 20) Plugin.Log.Info($"[RotationApplier] Chain @{chain.time:F2}s → head rot: {headRot}, tail rot: {tailRot}");
            }

            //ApplyArcTailAccumOverridesToRotationEvents(eData.RotationEvents, arcTailAccumRotationOverride);

            // 4) Apply to all obstacles
            (bool noodleProblemNotes, bool noodleProblemObstacles) = EditableCBD.TestForNoodleCustomData(eData);

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
                }

                return k0.rot;
            }

            //if (!noodleProblemObstacles) // i removed this for Ride Remix which has no noodle walls (in original walls) and thus has no rotations
            {

                foreach (var obs in eData.Obstacles)
                {
                    //obs.rotation = GetLogicalRotationFromNotes(obs.time); //suggested to use this instead of rotationEvents
                    obs.rotation = GetAccumRotationAt(obs.time); // test this version instead of logical from above
                }

            }
        }

        // Not used. i made this list to prevent arcs and walls from having rotation problems in JSON output files. but it turns out the the origianl rotation events list is best.
        /*
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
        */
        /*
        /// <summary>
        /// UNUSED When arc tail rotation overrides exist where I had to force the tail to match the head, apply them to the rotation events list. strangely this didn't help.
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
        */



        // This is the new WallRemovalForRotations() 
        public static void ApplyWallVisionBlockingFix(EditableCBD eData) // COMBO Method seems to allow more walls on both sides during turns 
        {
            float inPointLog  = 207f; // seconds for log only
            float outPointLog = 207.1f;

            bool rotationModeLate = eData.RotationModeLate;

            //if (WallGenerator._originalWallCount == eData.Obstacles.Count) // started to add this since an untouched map, should keep its obstacles but doesn't work. v3 not working and not v4 since probably is translating everyting from per object into rotation events and then back again and causes wall crossing again
            //    return eData.Obstacles;

            if (eData.Obstacles.Count == 0 || eData.RotationEvents.Count == 0)
                return;

            var obstacles = eData.Obstacles ?? new List<EObstacleData>();
            var rotationEvents = eData.RotationEvents ?? new List<ERotationEventData>();
            
            var arcs = eData.Arcs ?? new List<ESliderData>();
            bool hasArcs = arcs.Count > 0;

            var longChains = (eData.Chains ?? new List<ESliderData>())
                .Where(c => c.tailTime - c.time > 0.05f)
                .ToList();

            float njs = TransitionPatcher.FinalNoteJumpMovementSpeed;
            float jd = TransitionPatcher.FinalJumpDistance;

            if (njs <= 0 || jd <= 0)
            {
                Plugin.LogDebug("[ApplyWallVisionBlockingFix] NoteJumpMovementSpeed or JumpDistance is not set, skipping wall blocking fix.");
                return;
            }

            float wallTravelTime = jd / njs;
            Plugin.LogDebug($"[ApplyWallVisionBlockingFix] Total Obstacles: {obstacles.Count} Total Rotations: {eData.RotationEvents.Count} - NJD={jd:F2}, NJS={njs:F2}, wallTravelTime={wallTravelTime:F2}");

            // --- Common rotation helpers related to rotationEvents (from style1) ------------------------

            var rotations = rotationEvents.OrderBy(evt => evt.time).ToList();

            bool rotationEventsSubsetUsed = false; // if you ever add the "subset" optimization again

            //old
            /*
            int GetAccumRotationAt(float t)
            {
                int lo = 0, hi = rotations.Count - 1, ans = -1;
                while (lo <= hi)
                {
                    int mid = (lo + hi) >> 1;
                    if (rotations[mid].time <= t) // was < 
                    {
                        ans = mid;
                        lo = mid + 1;
                    }
                    else hi = mid - 1;
                }
                return ans >= 0 ? rotations[ans].accumRotation : 0;
            }
            */
            int GetAccumRotationAt(float t) //recognizes early/late 1 of 2
            {
                int lo = 0, hi = rotations.Count - 1, ans = -1;
                while (lo <= hi)
                {
                    int mid = (lo + hi) >> 1;
                    float mt = rotations[mid].time;

                    bool ok = rotationModeLate ? (mt < t) : (mt <= t);
                    if (ok) { ans = mid; lo = mid + 1; }
                    else { hi = mid - 1; }
                }
                return ans >= 0 ? rotations[ans].accumRotation : 0;
            }


            int RotationForSegmentStart(EObstacleData obs, float segStart)
                => rotationEventsSubsetUsed ? obs.rotation : GetAccumRotationAt(segStart);

            static bool IsPrecision(int v) => Math.Abs(v) >= 1000;

            static float DecodePrecision(int line)
            {
                // Standard: 0..3 are lanes
                if (!IsPrecision(line)) return line;

                // Precision: 1000->0, 2000->1, etc. => /1000 - 1
                return (line / 1000f) - 1f;
            }

            // new version to account for ME
            static bool WouldBlock(EObstacleData obs, int deltaRot)
            {
                float leftEdge = DecodePrecision(obs.line);
                float width    = DecodePrecision(obs.width);
                float rightEdge = leftEdge + width;

                bool leftSide  = rightEdge <=  2;
                bool rightSide = leftEdge  >   2;

                if (deltaRot < 0) return leftSide;   // left turn blocks if wall touches left half
                if (deltaRot > 0) return rightSide;  // right turn blocks if wall touches right half
                return false;
            }
            /*
            bool WouldBlockOld(EObstacleData obs, int direction)
            {
                int lineIndex = obs.line;

                if (lineIndex < 2 && direction < 0) return true; // left wall, left turn
                if (lineIndex > 1 && direction > 0) return true; // right wall, right turn
                return false;
            }
            */
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
            Plugin.LogDebug($"[ApplyWallVisionBlockingFix] Gaze Points: ColorNotes={eData.ColorNotes?.Count ?? 0}, BombNotes={eData.BombNotes?.Count ?? 0}, ChainTails={eData.Chains?.Count ?? 0}");

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

                /* old
                var blockEvents = rotations
                    .Where(dt =>
                        dt.time >= visibleStart && // was > KEEP THIS
                        dt.time <= visibleEnd &&
                        WouldBlock(obs, dt.rotation))
                    .Select(dt => dt.time)
                    .OrderBy(t => t)
                    .ToList();
                */
                // new respects early late 2 of 2
                var blockEvents = rotations
                    .Where(dt =>
                        InBlockWindow(dt.time, visibleStart, visibleEnd, rotationModeLate) &&
                        WouldBlock(obs, dt.rotation))
                    .Select(dt => dt.time)
                    .OrderBy(t => t)
                    .ToList();

                const float EPS = 0.0005f;

                bool InBlockWindow(float evtTime, float start, float end, bool late)
                {
                    // start boundary differs:
                    //  - early: event at start counts
                    //  - late : event at start does NOT count
                    bool afterStart = late ? (evtTime > start + EPS) : (evtTime >= start - EPS);

                    // end boundary: inclusive is fine for both (use EPS for float noise)
                    bool beforeEnd = evtTime <= end + EPS;

                    return afterStart && beforeEnd;
                }
                // -----------------------------

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

            // --- Style2 Side helpers  -------------------------------- for walls near arcs not in ForceZero mode

            //old version before ME detection
            //bool IsRightSide(int line) => line > 1; // 2,3
            //bool IsLeftSide(int line) => line < 2; // 0,1

            bool boostedWalls = Config.Instance.AllowV2BoostedWalls;
            const float MAX_EXTENSION = 2f;

            // --- Per-wall Style2 logic (as a helper) during arcs --------------------------

            List<EObstacleData> ApplyStyle2ForWall(EObstacleData obs)
            {
                var result = new List<EObstacleData>();

                float obsTime = obs.time;
                float dur = obs.duration;

                // New to fix ME walls
                float visibleStart = obsTime - .05f;
                float maxVisibleEnd = obs.endTime + wallTravelTime * MAX_EXTENSION;

                float leftEdge  = DecodePrecision(obs.line);
                float width     = DecodePrecision(obs.width);
                float rightEdge = leftEdge + width;

                bool leftSide = rightEdge <= 2;
                bool rightSide = leftEdge >  2;
                //------------------------------------------
                // old version
                //bool wallRight = IsRightSide(obs.line);
                //bool wallLeft = IsLeftSide(obs.line);

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
                    if (rightSide && delta > 0) blocks = true;
                    if (leftSide && delta < 0) blocks = true;
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

            int obsCount = 1;
            foreach (var obs in obstacles)
            {
                if (obs.time > inPointLog && obs.time < outPointLog)
                    Plugin.LogDebug($"[ApplyWallVisionBlockingFix] {obsCount} Obs: {obs.time:F} Dur: {obs.duration:F} line: {obs.line} layer: {obs.layer} width: {obs.width} height: {obs.height}");
                // Skip original walls that are noodle custom walls
                if (WallGenerator.originalWalls.Contains(obs) && WallGenerator.IsCustomNoodleWall(obs))
                {
                    //Plugin.LogDebug($"[ApplyWallVisionBlockingFix] Skipping Noodle Wall: {obs.time:F}");
                    continue;
                }

                float obsTime = obs.time;
                int layer = obs.layer;
                float dur = obs.duration;

                bool canSkip = false;

                if (WallGenerator.ExtensionMappingWallsGenerated) // otherwise if ME is not used, then don't skip higher walls
                {
                    if (layer > 11000) canSkip = true;         // high walls
                    else if (layer > 10 && layer < 1000) canSkip = true; // floor walls
                }

                if (layer == 2 && obs.line == 0 && obs.width > 2) canSkip = true; // crouch wall
                if (dur < 0 && boostedWalls) canSkip = true;                       // boosted v2 walls

                if (canSkip)
                {
                    kept.Add(obs);
                    continue;
                }

                // Decide which logic to use for this wall
                bool useStyle2 = hasArcs &&
                    arcMode != Config.ArcRotationModeType.ForceZero &&
                    IsObstacleInArcChainWindow(obs);

                List<EObstacleData> pieces =
                    useStyle2 ? ApplyStyle2ForWall(obs)
                           : ApplyStyle1ForWall(obs);

                kept.AddRange(pieces);

                if (obs.time > inPointLog && obs.time < outPointLog)
                {
                    if (pieces.Count > 0)
                    {
                        foreach (EObstacleData piece in pieces)
                        {
                            Plugin.LogDebug($"[ApplyWallVisionBlockingFix] -- Kept Wall Piece: {piece.time:F} Dur: {piece.duration:F}");
                        }
                    }
                    else
                        Plugin.LogDebug($"[ApplyWallVisionBlockingFix] -- Discarded Wall!");
                }

                    obsCount++;
            }

            Plugin.LogDebug($"[ApplyWallVisionBlockingFix] Total output obstacles: {kept.Count} kept out of {obstacles.Count}");
            eData.Obstacles = kept;
        }

        public static void PerObjectRotationLog(CustomBeatmapData customBeatmapData, EditableCBD eData, float inPointLog, float outPointLog)
        {
            var combinedList = new List<(float time, string type, object item)>();

            // Rotation events from eData
            var rotations = (eData?.RotationEvents ?? new List<ERotationEventData>())
                .OrderBy(r => r.time)
                .ToList();

            Plugin.LogDebug($"[PerObjectRotationLog] Total Rotation Events: {rotations.Count}");

            foreach (var rotEvent in rotations)
                combinedList.Add((rotEvent.time, "rot", rotEvent));

            // Notes (same window!)
            foreach (var note in customBeatmapData.allBeatmapDataItems
                .OfType<CustomNoteData>()
                .Where(n => n.time >= inPointLog && n.time <= outPointLog))
            {
                combinedList.Add((note.time, "note", note));
            }

            // Arcs (same window!)
            foreach (var arc in customBeatmapData.allBeatmapDataItems
                .OfType<CustomSliderData>()
                .Where(s => s.sliderType == SliderData.Type.Normal)
                .Where(s => s.time <= outPointLog && s.tailTime >= inPointLog)) // overlaps window
            {
                combinedList.Add((arc.time, "arc", arc));
            }

            // Chains (same window!)
            foreach (var chain in customBeatmapData.allBeatmapDataItems
                .OfType<CustomSliderData>()
                .Where(s => s.sliderType == SliderData.Type.Burst)
                .Where(s => s.time <= outPointLog && s.tailTime >= inPointLog)) // overlaps window
            {
                combinedList.Add((chain.time, "chain", chain));
            }

            // Walls (same window!)
            foreach (var wall in customBeatmapData.allBeatmapDataItems
                .OfType<CustomObstacleData>()
                .Where(o => o.time >= inPointLog && o.time <= outPointLog))
            {
                combinedList.Add((wall.time, "wall", wall));
            }

            // Helper: accumulated rotation at time t (prefer your stored accumRotation if you have it)
            /*
            int GetAccumRotationAt(float t)
            {
                int lo = 0, hi = rotations.Count - 1, ans = -1;
                while (lo <= hi)
                {
                    int mid = (lo + hi) >> 1;
                    if (rotations[mid].time < t) { ans = mid; lo = mid + 1; } // was <= t
                    else { hi = mid - 1; }
                }
                return ans >= 0 ? rotations[ans].accumRotation : 0;
            }
            */
            var ordered = combinedList
                .OrderBy(x => x.time)
                .ThenBy(x => x.type)
                .ToList();

            foreach (var entry in ordered)
            {
                // print ONLY the window
                if (entry.time < inPointLog || entry.time > outPointLog) continue;

                switch (entry.type)
                {
                    case "rot":
                        {
                            var rot = (ERotationEventData)entry.item;
                            Plugin.LogDebug($"-- RotEvent - Time: {rot.time}  rot:      {rot.accumRotation} deltaRot: {rot.rotation} ----------------------- ");
                            break;
                        }
                    case "note":
                        {
                            var note = (CustomNoteData)entry.item;
                            Plugin.LogDebug($"   Note     - Time: {note.time}  note.rot: {note.rotation} c:{note.colorType} X:{note.lineIndex} Y:{(int)note.noteLineLayer} cutDir: {note.cutDirection}");
                            break;
                        }
                    case "arc":
                        {
                            var arc = (CustomSliderData)entry.item;
                            Plugin.LogDebug($"   Arc      - Time: {arc.time}  arc.rot: {arc.rotation} Head X:{arc.headLineIndex} Y:{(int)arc.headLineLayer} Tail:{arc.tailTime:F2} ");
                            break;
                        }
                    case "chain":
                        {
                            var chain = (CustomSliderData)entry.item;
                            Plugin.LogDebug($"   Chain    - Time: {chain.time}  chain.rot:{chain.rotation} Head X:{chain.headLineIndex} Y:{(int)chain.headLineLayer} Tail:{chain.tailTime:F2} ");
                            break;
                        }
                    case "wall":
                        {
                            var wall = (CustomObstacleData)entry.item;
                            Plugin.LogDebug($"   Wall     - Time: {wall.time}  wall.rot: {wall.rotation} dur:{wall.duration:F2}  X:{wall.lineIndex} Y:{(int)wall.lineLayer} W:{wall.width} H:{wall.height} ");
                            break;
                        }
                }
            }
        }


    }


}