using System;
using System.IO;
using System.Runtime.CompilerServices;
using IPA.Config.Stores;
using IPA.Config.Stores.Attributes;
using IPA.Config.Stores.Converters;
using Microsoft.Identity.Client;
using UnityEngine;


[assembly: InternalsVisibleTo(GeneratedStore.AssemblyVisibilityTarget)]
namespace AutoBS
{

    internal class Config
    {
        public static Config Instance { get; set; }

        public virtual bool EnablePlugin { get; set; } = true;

        //Major Enablers
        public virtual bool Enable360fyer { get; set; } = true;

        public virtual bool EnableArcsGen360 { get; set; } = true;
        public virtual bool EnableArcsNonGen360 { get; set; } = true;
        public virtual bool EnableArcsStandard { get; set; } = true;

        public virtual bool EnableChainsGen360 { get; set; } = true;
        public virtual bool EnableChainsNonGen360 { get; set; } = true;
        public virtual bool EnableChainsStandard { get; set; } = true;


        public virtual bool EnableWallsGen360 { get; set; } = true;
        public virtual bool EnableWallsNonGen360 { get; set; } = true;
        public virtual bool EnableWallsStandard { get; set; } = false;


        public virtual bool EnableLightingGen360 { get; set; } = true;
        public virtual bool EnableLightingNonGen360 { get; set; } = true;
        public virtual bool EnableLightingStandard { get; set; } = true;

        // ROTATION ---------------------------------

        public virtual bool Wireless360 { get; set; } = true; //This assumes the user doesn't want rotation limits and it sets LimitRotations to 999 and BottleneckRotations to 999. only for 360 not 90.
        public virtual float LimitRotations360 { get; set; } = 330;//changed this to Degrees. Previously Default 28 where 24 is 360 degree circle. designed to avoid riping a cable

        // late means rotations only occur on an object if the object is later than the rotation event time. early, means the rotation applies to any object on or after the rotation event time. Will NOT override incoming 360 maps mode
        public virtual bool RotationModeLate { get; set; } = true;  // used by Arc Fix(), Apply Per Object Rotations(), Apply Wall Vision Blocking Fix()

        //public enum RotateMode { Early, Late }
        //[UseConverter(typeof(EnumConverter<RotateMode>))]
        //public virtual RotateMode RotationMode { get; set; } = RotateMode.Late;

        public virtual bool AddExtraRotation { get; set; } = true;//for periods of low rotation, will make sure rotations for direction-less notes move in same direction as last rotation so totalRotation will increase.
        public virtual float RotationGroupLimit { get; set; } = 10f;//If totalRotations are under this limit, will add more rotations - used by AddExtraRotation
        public virtual float RotationGroupSize { get; set; } = 12;//The number of rotations to remain inactive for adding rotations - used by AddExtraRotation

        public virtual float RotationSpeedMultiplier { get; set; } = 1.0f;//BW This is a multiplier for PreferredBarDuration which has a default of 1.84f causes to emit more (smaller, closer-together) delta rotation events
        public virtual float MinRotationSize { get; set; } = 15f;//disallows single rotations smaller than this
        public virtual float MaxRotationSize { get; set; } = 30f;//disallows single rotations larger than this
        public virtual float FOV { get; set; } = 80f; // rotations are always 15 degree increments so for example, 60-85 are the same (60/2 = 30 and 85/2 = 42.5 so no angle over 30 is allowed). 90/2 allows a 45 angle
        public virtual float TimeWindow { get; set; } = 0.35f; // FOV time window

        public virtual float VisionBlockingWallRemovalMult { get; set; } = 1f; // increase to remove more vision blocking walls 

        public virtual bool ReduceRotationForHighSpeedHighDensityMaps { get; set; } = true; // reduce rotation amounts for maps with high BPM and high note density
        public virtual float HighNJSThresholdForRotationReduction { get; set; } = 15f; // NJS above which rotation reduction can occur
        public virtual float HighNPSThresholdForRotationReduction { get; set; } = 5f; // notes per second above which rotation reduction can occur. both must be true! (NJS and NPS)
        public virtual int MassiveStreakNumberOfRotationsThreshold { get; set; } = 30; // how many same direction rotations to consider a "massive streak" that should be curtailed

        // ARCS

        public virtual bool ArcFixFull { get; set; } = true;//removes rotations between sliders head and tail
        public enum ArcRotationModeType { ForceZero, NetZero, NoRestriction }
        public virtual ArcRotationModeType ArcRotationMode { get; set; } = ArcRotationModeType.NetZero;

        public enum ArcSwingModeType { Curated180and135, Curated180and135and90, All180and135, All180and135and90 }
        public virtual ArcSwingModeType ArcSwingMode { get; set; } = ArcSwingModeType.Curated180and135and90;
        //public virtual bool ForceNaturalArcs { get; set; } = true; //False will allow more variety in arc connections between the head and tail notes. Some of these arcs will take a less natural path since they are 135 degree changes instead of 180 degree changes.
        public virtual float PreferredArcCountPerMin { get; set; } = 12.0f;//how many arcs per minute to aim for.
        public virtual float MinArcDuration { get; set; } = 0.9f;
        public virtual float MaxArcDuration { get; set; } = 2.5f;
        public virtual bool AllowArcHeadDotNotes { get; set; } = false;
        public virtual bool AllowArcTailDotNotes { get; set; } = false;
        

        // CHAINS

        public virtual float PreferredChainCountPerMin { get; set; } = 10.0f;//how many chains per minute to aim for.
        public virtual bool AlterNotes { get; set; } = false; // alter notes so that chains will be compatible - will move note position so tail can exist
        public virtual bool PauseDetection { get; set; } = true; // For no particular reason decided to use this algorith only (very similar to the other tempo change algorithm)
        public virtual bool ForceMoreChains { get; set; } = true;
        /// <summary>
        /// If true, do not create chains on notes that have a simultaneous pair
        /// where ColorA ends up to the right of ColorB (crossed-hands doubles).
        /// </summary>
        public bool DisallowChainsOnCrossedPairs { get; set; } = true;
        public virtual float ChainTimeBumper { get; set; } = 0.2f;//how much time between chain and other notes.
        public virtual bool EnableLongChains { get; set; } = true; // chains more like arcs that don't get slashed
        public virtual float LongChainMaxDuration { get; set; } = 0.425f; // max duration of long chains

        public virtual float LongChainChanceMultiplier { get; set; } = 10f; // multiplier to increase or decrease chance of long chains 10 is always allows. 0 is never allow.

        // BEAT SAGE

        public virtual bool EnableCleanBeatSage { get; set; } = true; // alter notes so that chains will be compatible - will move note position so tail can exist
        public virtual float MaxCrouchWallDuration { get; set; } = 0.75f; // max duration of crouch walls in Seconds
        public virtual float StrayNoteCleanerOffset { get; set; } = 6f; // how many seconds from primary note content to remove straggler notes that are off by themselves at the beginning or end of map
        public virtual float MaxStrayNotes { get; set; } = 5f; //how many stray notes allowed with a StrayNoteCleanerOffset time gap before removal

        // WALLS

        public virtual float StandardLevelWallMultiplier { get; set; } = .4f; // reduces all the wall multipliers in ResetWalls since non rotating maps end up with tons of walls
        
        //---

        public virtual bool AllowV2BoostedWalls { get; set; } = true; // allow walls with negative duration (boosted walls) to stay in generated maps
        public virtual bool EnableStandardWalls { get; set; } = true; 
        public virtual bool EnableBigWalls { get; set; } = true;
        public virtual float StandardWallsMultiplier { get; set; } = 100; // 100% is max and can't be increased unlike the other multipliers
        public virtual float StandardWallsMinDistance { get; set; } = 1; // default 1 so ends into lanes 0 and 3. at 0 will be lean walls into lanes 1 and 2
        public virtual bool AllowPlayerCrossingWalls { get; set; } = true; // true allows walls to cross in front of the player as long as they do not block the vision of an upcoming note. It will only delete or shorten the wall if it appears in the gap of time that would block the view of the next object. False will now stop allowing those walls. So no wall cross in front of the player. Will remove many walls though (1/3 maybe)


        //public virtual bool UseMappingExtensionsForWallsGenerator { get; set; } = true; // allows user to to use all the walls with ME if they prefer - doesn't work. must also somehow disable ME or will use it anyway.

        public virtual bool EnableDistantExtensionWalls { get; set; } = true; // -- i think i can delete this!!!!!!!!!!!!!!!!!!!!!!!
        public virtual float DistantExtensionWallsMultiplier { get; set; } = 2;
        
        public virtual bool EnableColumnWalls { get; set; } = true;
        public virtual float ColumnWallsMultiplier { get; set; } = 1; 
        public virtual float ColumnWallsMinDistance { get; set; } = 2; 
        
        public virtual bool EnableRowWalls { get; set; } = true;
        public virtual float RowWallsMultiplier { get; set; } = 1; 
        public virtual float RowWallsMinDistance { get; set; } = 3; 
        
        public virtual bool EnableTunnelWalls { get; set; } = true;
        public virtual float TunnelWallsMultiplier { get; set; } = 1; 
        public virtual float TunnelWallsMinDistance { get; set; } = 0; 
        
        public virtual bool EnableGridWalls { get; set; } = true;
        public virtual float GridWallsMultiplier { get; set; } = 1; 
        public virtual float GridWallsMinDistance { get; set; } = 2; 

        public virtual bool EnableWindowPaneWalls { get; set; } = true;
        public virtual float WindowPaneWallsMultiplier { get; set; } = 4; 
        public virtual float WindowPaneWallsMinDistance { get; set; } = 0; 

        public virtual bool EnableParticleWalls { get; set; } = true;
        public virtual bool EnableLargeParticleWalls { get; set; } = true;
        public virtual float ParticleWallsMultiplier { get; set; } = 4;
        public float ParticleWallsBatchSize { get; set; } = 20; // can get stuck on some songs if higher than this
        public virtual float ParticleWallsMinDistance { get; set; } = 0; 
        
        public virtual bool EnableFloorWalls { get; set; } = true;
        public virtual float FloorWallsMultiplier { get; set; } = 4;
        public float FloorWallsBatchSize { get; set; } = 20; // can get stuck on some songs if higher than this Halo Expl is worst so far
        public virtual float FloorWallsMinDistance { get; set; } = 0; 
        
        public virtual float MaxWaitTime { get; set; } = 6; // how many seconds to wait to remove problem walls before give up and let all remaining walls pass through (crossing vision etc)

        public virtual bool AllowCrouchWalls { get; set; } = true;
        public virtual bool AllowLeanWalls { get; set; } = true;
        public virtual float MinWallDuration { get; set; } = 0.001f;
        public virtual float MinDistanceBetweenNotesAndWalls { get; set; } = .2f;

        public virtual bool PatchV4ObstacleExtensionLayers { get; set; } = false; // v4 maps clamp obstacle layers from 0 to 4 which breaks v4 mapping extensions. disable if Mapping Extensions takes care of this.


        // LIGHTS

        public virtual bool BigLasers { get; set; } = true;
        public virtual bool BrightLights { get; set; } = true;
        public virtual bool BoostLighting { get; set; } = true;

        public virtual float BoostLightingMultiplier { get; set; } = 1;

        public virtual int BoostLightingRandomSeed { get; set; } = 242;

        public virtual bool EnableStrobes { get; set; } = true;
        public virtual float StrobeMultiplier { get; set; } = 1; // frequency of use. 0 is off

        public virtual float StrobeBrightnessMultiplier { get; set; } = 1f; // 1 is noticeably bright in 360 and will be automatically sets to 2x for standard environment since its so dimmer there.
        public virtual float StrobeMaxDuration { get; set; } = 5f; // seconds - this doesn't work for some reason. in logs this works, but when viewing the game, strobes usually seem much shorter than this. this can still help them last longer though.

        public virtual bool EnableLightAutoMapper { get; set; } = true;

        public virtual float LightFrequencyMultiplier { get; set; } = 1.0f;// Default is 1, adjust as needed use from 0 - 1 to reduce frequency
        public virtual float BrightnessMultiplier { get; set; } = 1.0f;//affect the floatValue property can increase or decrease

        public enum Style
        {
            ON = 1,        // Fast Strobe: on & off events
            FLASH = 2,     // Med Flash: flash & fade events
            FADE = 3,      // Med Fade: fade events
            TRANSITION = 4 // Slow Transition: transition & fade
        }
        public virtual Style LightStyle { get; set; } = Style.FADE;//not using off

        public enum Base
        {
            Standard,
            OneSaber,
            NoArrows,
            NinetyDegree,
            ThreeSixtyDegree
        }
        [UseConverter(typeof(EnumConverter<Base>))]
        public virtual Base BasedOn { get; set; } = Base.Standard;//BW Can be Standard,OneSaber,NoArrows,90Degree (but may keep old 90 rotation events. need to investigate)


       
        /// <summary>
        /// This is called whenever BSIPA reads the config from disk (including when file changes are detected).
        /// </summary>
        public virtual void OnReload()
        {
            // Do stuff after config is read from disk.
        }

        /// <summary>
        /// Call this to force BSIPA to update the config file. This is also called by BSIPA if it detects the file was modified.
        /// </summary>
        public virtual void Changed()
        {
            // Do stuff when the config is changed.
        }

        /// <summary>
        /// Call this to have BSIPA copy the values from <paramref name="other"/> into this config.
        /// </summary>
        public virtual void CopyFrom(Config other)
        {
            // This instance's members populated from other
        }


        // NJS FIXER

        public virtual bool EnableAutoNjsFixerGen360 { get; set; } = true;
        public virtual bool EnableAutoNjsFixerNonGen360 { get; set; } = false;
        public virtual bool EnableAutoNjsFixerStandard { get; set; } = false;

        public virtual bool EnabledInPractice { get; set; } = true; 
        public virtual float DesiredNJS { get; set; } = 10f; // if > 0 use this value
        public virtual float DesiredJD  { get; set; } = 30f; // if > 0 use this value
        public enum AutoNjsFixerModeType { PreserveTravelTime, SetNoteSpeed } // bpm is required for PreserveTravelTime mode. 
        public virtual AutoNjsFixerModeType AutoNjsFixerMode { get; set; } = AutoNjsFixerModeType.PreserveTravelTime;


        //------------------------

        public virtual bool EnableLiveVolumeControl { get; set; } = true;
        public virtual bool EnableLiveNjsJdControl { get; set; } = true;
        

        public enum LiveControlModeType
        {
            Off,
            ThumbstickLUpDown,
            ThumbstickLLeftRight,
            ThumbstickRUpDown,
            ThumbstickRLeftRight,
            ButtonsAB,
            ButtonsXY,
            ButtonsYB,
            ButtonsXA
        }
        public virtual LiveControlModeType LiveVolumeControl { get; set; } = LiveControlModeType.ThumbstickLUpDown;
        public virtual LiveControlModeType LiveNjsControl { get; set; } = LiveControlModeType.ThumbstickRUpDown;
        public virtual LiveControlModeType LiveJdControl { get; set; } = LiveControlModeType.ThumbstickRLeftRight;

        public virtual bool LiveVolumeAffectsSoundEffects { get; set; } = true;

        // Green Screen Passthrough portal

        public virtual bool EnableMixedRealityMenus { get; set; } = false;
        public virtual bool EnableMixedRealityStandard { get; set; } = false;
        public virtual bool EnableMixedReality360 { get; set; } = false;

        public virtual bool MixedRealityPortalShapeRound { get; set; } = true;
        public virtual float MixedRealityRoundDiameter { get; set; } = 3.2f;
        public virtual float MixedRealityRectWidth { get; set; } = 3.8f;
        public virtual float MixedRealityRectHeight { get; set; } = 3.1f;

        public virtual float MixedReality360Diameter { get; set; } = 10f; // set to 0 to turn off

        public virtual float MixedReality360CeilingHeight { get; set; } = 3.5f; // set to 0 to turn off

        public enum MixedRealityMenuModeType
        {
            Style1,
            Style2,
            Style3
        }
        public virtual MixedRealityMenuModeType MixedRealityMenuStyle { get; set; } = MixedRealityMenuModeType.Style1;

        public virtual float MixedRealityMenuZOffset { get; set; } = 0.3f;

        public virtual float MixedRealityGamePlayRoundZOffset { get; set; } = 3.0f;
        public virtual float MixedRealityGamePlayRectZOffset { get; set; } = 3.0f;

        public virtual float MixedRealityRoundYOffset { get; set; } = .2f; //meters - for centering passthroughj portal. use player height 1.8f
        public virtual float MixedRealityRectYOffset { get; set; } = 0f; //meters - for centering passthroughj portal. use player height 1.8f

        public virtual VirtualDesktopColor MixedRealityGreenScreenColor { get; set; } = new VirtualDesktopColor
        {
            r = 0,
            g = 255,
            b = 0
        };

        public virtual float MixedRealityColorMultiplier { get; set; } = 1f;
        //public virtual bool MixedRealityFullModeTest { get; set; } = false; // huge green screen behind everything. but it looks terrible. all objects get a green glow around them.
        /*
        public enum mRHeightMode
        {
            CenterAtCustomHeight,
            BottomAtFloorLevel,
        }
        public virtual mRHeightMode mRRoundHeightMode { get; set; } = mRHeightMode.CenterAtCustomHeight;
        public virtual mRHeightMode mRRectHeightMode { get; set; }  = mRHeightMode.CenterAtCustomHeight;
        */

        // Auto Difficulty Reducer DiffReducer

        public virtual bool EnableDiffReducer { get; set; } = false;
        public virtual bool EnableForAllMaps { get; set; } = false; //EnableOnlyIfSongAboveAveNps
        public virtual float PreferredFinalNps { get; set; } = 4f;


        // Virtual Camera and Real Camera Alignment Grid for Mixed Reality Setup 

        public virtual bool EnableCameraAlignmentGrid1 { get; set; } = false;

        public virtual SerializableVector3 CameraAlignmentGrid1OriginFeet { get; set; } = new SerializableVector3(0f, 0f, 0f);

        public virtual bool EnableCameraAlignmentGrid2 { get; set; } = false;

        public virtual SerializableVector3 CameraAlignmentGrid2OriginFeet { get; set; } = new SerializableVector3(0f, 0f, -3f);

        public virtual bool EnableCameraAlignmentVerticalMarkerSet1 { get; set; } = false;
        public virtual SerializableVector3 CameraAlignmentVerticalMarkerSet1OriginFeet { get; set; } = new SerializableVector3(-3f, 0f, 0f);

        public virtual bool EnableCameraAlignmentVerticalMarkerSet2 { get; set; } = false;
        public virtual SerializableVector3 CameraAlignmentVerticalMarkerSet2OriginFeet { get; set; } = new SerializableVector3(0, 0f, -3f);

        public virtual bool EnableCameraAlignmentVerticalMarkerSet3 { get; set; } = false;
        public virtual SerializableVector3 CameraAlignmentVerticalMarkerSet3OriginFeet { get; set; } = new SerializableVector3(3f, 0f, -3f);

        public virtual bool CameraAlignmentGridDisplayLines { get; set; } = true;

        public virtual float CameraAlignmentMarkerDiameterFeet { get; set; } = 0.02f;

        public virtual float CameraAlignmentGridUnitSizeFeet { get; set; } = 3f;

        // TEMP -----------------------------------
        //public float RotationOriginOffsetForRecording360 { get; set; } = 0f; // for recording 360 videos, can offset the rotation origin to the left or right. 0 is center, - is left, + is right. if plan to record multiple videos from various angles on the same song to edit into final video.

        //public virtual bool RemoveLeftWalls { get; set; } = false;
        public virtual bool RemoveMenuMusic { get; set; } = false;
        public virtual bool RemoveBadCutSound { get; set; } = false;



        // Output JASON files

        public virtual bool TurnOffJSONDatOutputAfterOneMapPlay { get; set; } = true; // to prevent constant JSON outputs. turned off in Plugin.cs
        public virtual bool OutputV2JsonToSongFolder_NoArcsNoChainsNoMapExtWalls { get; set; } = false; //will convert to v2 - doesn't output arcs or chains. arcs are supposed to be supported but always produce infinitely long arcs. Mappring extension walls don't work either for some reason so i filtered them out.
        public virtual bool OutputV3JsonToSongFolder { get; set; } = false; //will convert to v3 -- use ForceZero arc rotation mode or will have intersecting arcs/walls
        //add to v2 or v3! since its the info.dat file. add it to each "_difficultyBeatmaps" that needs it: "_customData": {"_requirements": ["Mapping Extensions"]},
        //at least for beat sage, need to remove in info.dat:  "_environmentNames": [ "DefaultEnvironment" ], AND remove "_environmentNameIdx": 0, from each difficultyBeatmap in order for 360 map to use 360 environment
        public virtual bool OutputV4JsonToSongFolder { get; set; } = false; // outputs perfect map compared to v2 or 3
        public virtual int OutputJsonSongSampleRate { get; set; } = 44100;
    }
    public class VirtualDesktopColor
    {
        public int r { get; set; } = 0;
        public int g { get; set; } = 255;
        public int b { get; set; } = 0;

        /// <summary>
        /// multiplier will brighten color only (not change hue). Can make HDR brightness levels. 
        /// For example unity (0,1,0) is full green green. but (0,2,0) is HDR green and will remove some shadows
        /// </summary>
        /// <param name="alpha"></param>
        /// <param name="multiplier"></param>
        /// <returns></returns>
        public Color ToUnityColor(float alpha = 1f, float multiplier = 1f) 
        {
            return new Color(
                Mathf.Clamp(r, 0, 255) / 255f * multiplier,
                Mathf.Clamp(g, 0, 255) / 255f * multiplier,
                Mathf.Clamp(b, 0, 255) / 255f * multiplier,
                alpha);
        }
    }
    [Serializable]
    public struct SerializableVector3
    {
        public float x;
        public float y;
        public float z;

        public SerializableVector3(float x, float y, float z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }

        public Vector3 ToMeters()
        {
            return new Vector3(x, y, z);
        }

        public Vector3 ToMetersFromFeet()
        {
            return new Vector3(x * 0.3048f, y * 0.3048f, z * 0.3048f);
        }

        public static SerializableVector3 FromVector3(Vector3 v)
        {
            return new SerializableVector3(v.x, v.y, v.z);
        }

        public static SerializableVector3 Zero => new SerializableVector3(0f, 0f, 0f);
    }
}
