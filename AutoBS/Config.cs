using System;
using System.IO;
using System.Runtime.CompilerServices;
using IPA.Config.Stores;
using IPA.Config.Stores.Attributes;
using IPA.Config.Stores.Converters;


[assembly: InternalsVisibleTo(GeneratedStore.AssemblyVisibilityTarget)]
namespace AutoBS
{

    internal class Config
    {
        //Major Enablers


        public static Config Instance { get; set; }

        public virtual bool EnablePlugin { get; set; } = true;


        public virtual bool Enable360fyer { get; set; } = true;


        public virtual bool EnableArcsGen360 { get; set; } = true;
        public virtual bool EnableArcsNonGen360 { get; set; } = false;
        public virtual bool EnableArcsStandard { get; set; } = false;

        public virtual bool EnableChainsGen360 { get; set; } = true;
        public virtual bool EnableChainsNonGen360 { get; set; } = false;
        public virtual bool EnableChainsStandard { get; set; } = false;


        public virtual bool EnableWallsGen360 { get; set; } = true;
        public virtual bool EnableWallsNonGen360 { get; set; } = true;
        public virtual bool EnableWallsStandard { get; set; } = false;


        public virtual bool EnableLightingGen360 { get; set; } = true;
        public virtual bool EnableLightingNonGen360 { get; set; } = true;
        public virtual bool EnableLightingStandard { get; set; } = false;


       // public virtual bool EnableFeaturesForNonGen360Maps { get; set; } = true; // non-generated 360/90 maps can use 360fyer features like walls and lights
       // public virtual bool EnableFeaturesForStandardMaps { get; set; } = false; // standard, OneSaber, etc maps can use 360fyer features like walls and lights



        //public virtual float VolumeAdjuster { get; set; } = 0; // db - 10db should double the volume


        // 360
        public virtual bool Enable360ForStandardMaps { get; set; } = false; // add rotations to an actual standard map!
        public virtual bool EnableEnvironmentAutoBS { get; set; } = false; // add rotated geometry to standard environments!!


        public virtual bool Wireless360 { get; set; } = true; //BW This assumes the user doesn't want rotation limits and it sets LimitRotations to 999 and BottleneckRotations to 999. only for 360 not 90.
        public virtual float LimitRotations360 { get; set; } = 360;//BW changed this to Degrees. Previously Default 28 where 24 is 360 degree circle. designed to avoid riping a cable

        // 90

        public virtual float LimitRotations90 { get; set; } = 90;//BW changed this to Degrees
        public virtual bool ShowGenerated90 { get; set; } = false;

        // ROTATION
        public virtual bool RotationModeLate { get; set; } = true; // late means rotations only occur on an object if the object is later than the rotation event time. early, means the rotation applies to any object on or after the rotation event time
        
        //public virtual bool AddExtraRotationV2 { get; set; } = true;
        public virtual bool AddExtraRotation { get; set; } = true;//for periods of low rotation, will make sure rotations for direction-less notes move in same direction as last rotation so totalRotation will increase.
        public virtual float RotationGroupLimit { get; set; } = 10f;//If totalRotations are under this limit, will add more rotations
        public virtual float RotationGroupSize { get; set; } = 12;//The number of rotations to remain inactive for adding rotations

        public virtual int MaxSameDirectionStreak { get; set; } = 20; // prevent too many rotations in same direction in a row

        //BW Not needed since rotates outside of the 15 degree pasages
        //public virtual float RotationAngleMultiplier { get; set; } = 1.0f;//BW added this to lessen/increase rotation angle amount
        public virtual float RotationSpeedMultiplier { get; set; } = 1.0f;//BW This is a multiplier for PreferredBarDuration which has a default of 1.84f causes to emit more (smaller, closer-together) delta rotation events
        public virtual float MinRotationSize { get; set; } = 15f;//disallows single rotations smaller than this
        public virtual float MaxRotationSize { get; set; } = 30f;//disallows single rotations larger than this
        public virtual float FOV { get; set; } = 80f;
        public virtual float TimeWindow { get; set; } = 0.35f;

        public virtual float VisionBlockingWallRemovalMult { get; set; } = 1f; // increase to remove more vision blocking walls 

        public virtual bool ReduceRotationForHighSpeedHighDensityMaps { get; set; } = true; // reduce rotation amounts for maps with high BPM and high note density
        public virtual float HighSpeedThreshold { get; set; } = 15f; // BPM above which rotation reduction can occur
        public virtual float HighDensityThreshold { get; set; } = 5f; // notes per second above which rotation reduction can occur

        // ARCS

        //public virtual bool EnableArcs { get; set; } = true;

        //public virtual bool ArcFix { get; set; } = true;//remove rotation during sliders unless the head and tail rotation ends up the same. results is partial mismatch of tail
        public virtual bool ArcFixFull { get; set; } = true;//removes all rotations during sliders

        public enum ArcRotationModeType { ForceZero, NetZero, NoRestriction }
        public virtual ArcRotationModeType ArcRotationMode { get; set; } = ArcRotationModeType.NetZero;
        public virtual bool ForceNaturalArcs { get; set; } = true; //False will allow more variety in arc connections between the head and tail notes. Some of these arcs will take a less natural path since they are 135 degree changes instead of 180 degree changes.

        //public virtual bool ForceZeroArcRotations { get; set; } = false; //strict: no deviation inside covered segments of arcs. no rotations for any events during arcs!
        //public virtual bool ForceNetZeroPerArcRotations { get; set; } = false; //force arcs to net zero rotation over the course of the arc. may allow some rotations as long as net zero during arc.
        public virtual bool EnableArcsForStandardMaps { get; set; } = false;
        public virtual float PreferredArcCountPerMin { get; set; } = 12.0f;//how many arcs per minute to aim for.
        // public virtual float ArcMultiplier { get; set; } = 1.0f;//.1 is 10th of time and 1 is always add arc
        public virtual float ControlPointLength { get; set; } = 1.0f;
        public virtual float MinArcDuration { get; set; } = 0.9f;
        public virtual float MaxArcDuration { get; set; } = 2.5f;

        // CHAINS

        //public virtual bool EnableChains { get; set; } = true;

        public virtual bool EnableChainsForStandardMaps { get; set; } = false;

        public virtual float PreferredChainCountPerMin { get; set; } = 10.0f;//how many chains per minute to aim for.
        public virtual bool AlterNotes { get; set; } = false; // alter notes so that chains will be compatible - will move note position so tail can exist
        public virtual bool PauseDetection { get; set; } = true; // For no particular reason decided to use this algorith only (very similar to the other tempo change algorithm)
        public virtual bool ForceMoreChains { get; set; } = true;
        public virtual float ChainTimeBumper { get; set; } = 0.2f;//how much time between chain and other notes.
        public virtual bool EnableLongChains { get; set; } = true; // chains more like arcs that don't get slashed
        public virtual float LongChainMaxDuration { get; set; } = 0.425f; // max duration of long chains

        // BEAT SAGE

        public virtual bool EnableCleanBeatSage { get; set; } = false; // alter notes so that chains will be compatible - will move note position so tail can exist
        public virtual float MaxCrouchWallDuration { get; set; } = 0.75f; // max duration of crouch walls in Seconds
        public virtual float StrayNoteCleanerOffset { get; set; } = 6f; // how many seconds from primary note content to remove straggler notes that are off by themselves at the beginning or end of map

        //public virtual bool EnableNJS { get; set; } = false;
        //public virtual float NJS { get; set; } = 15f;
        //public virtual float NJO { get; set; } = 0f;

        // WALLS

        //public virtual bool EnableWallGenerator { get; set; } = true;

        //public virtual bool EnableWallGeneratorForStandardMaps { get; set; } = false;

        public virtual bool AllowV2BoostedWalls { get; set; } = true; // allow walls with negative duration (boosted walls) to stay in generated maps
        public virtual bool EnableStandardWalls { get; set; } = true; 
        public virtual bool EnableBigWalls { get; set; } = true;
        public virtual float StandardWallsMultiplier { get; set; } = 100; // 100% is max and can't be increased unlike the other multipliers
        public virtual float StandardWallsMinDistance { get; set; } = 0; // default 0 since comes into lanes 0 and 3
        public virtual bool EnableMappingExtensionsWallsGenerator { get; set; } = true; // not set by user currently. set by whether mapping extensions mod is installed. // turns on/off all ext walls - BW add new decorative extension mapping walls. must install extension mapping to work
        
        public virtual bool EnableDistantExtensionWalls { get; set; } = true; // regular extension mapping walls
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

        //public virtual float WindowPaneWallsSize { get; set; } = 2;
        //public virtual float WindowPaneWallsHeight { get; set; } = 3;
        public virtual float WindowPaneWallsMultiplier { get; set; } = 1; 
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

        public virtual bool AllowCrouchWalls { get; set; } = true;//BW added this
        public virtual bool AllowLeanWalls { get; set; } = true;//BW added this
        public virtual float MinWallDuration { get; set; } = 0.001f;
        public virtual float MinDistanceBetweenNotesAndWalls { get; set; } = .2f;

       

        // LIGHTS

        public virtual bool BigLasers { get; set; } = true;
        public virtual bool BrightLights { get; set; } = true;
        public virtual bool BoostLighting { get; set; } = true;

        public virtual bool EnableLightEnhanceForStandardMaps { get; set; } = false;

        public virtual bool EnableLightAutoMapper { get; set; } = true;

        public virtual bool EnableLightAutoMapperForStandardMaps { get; set; } = false;
        public virtual float LightFrequencyMultiplier { get; set; } = 1.0f;// Default is 1, adjust as needed use from 0 - 1 to reduce frequency
        public virtual float BrightnessMultiplier { get; set; } = 1.0f;//affect the floatValue property can increase or decrease

        public enum Style
        {
            ON = 1,        // Fast Strobe: on & off events
            FLASH = 2,     // Med Flash: flash & fade events
            FADE = 3,      // Med Fade: fade events
            TRANSITION = 4 // Slow Transition: transition & fade
        }
        public virtual Style LightStyle { get; set; } = Style.FLASH;//not using off

        // ONE SABER

        //public virtual bool OnlyOneSaber { get; set; } = false;
        //public virtual bool LeftHandedOneSaber { get; set; } = false;

        //public virtual bool OnlyOneSaberForStandardMaps { get; set; } = false;

        // BASE MAP

        //BW added this baseded on NoteLimiter UI. enums cannot use a digit so had to change 90Degree to NinetyDegree and 360Degree to ThreeSixtyDegree
        //public virtual string TextColor { get; set; } = "#555555";//BW sets the color of the LimitRotations360 menu text. Dims it if deactivated by Wireless360;

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
        //public virtual bool EnableAutoNjsFixer { get; set; } = true;
        public virtual bool EnableAutoNjsFixerGen360 { get; set; } = true;
        public virtual bool EnableAutoNjsFixerNonGen360 { get; set; } = false;
        public virtual bool EnableAutoNjsFixerStandard { get; set; } = false;

        public virtual bool EnabledInPractice { get; set; } = false; // if > 0, forces NJS to this value
        public virtual float DesiredNJS { get; set; } = 10f;
        public virtual float DesiredJD  { get; set; } = 30f;
        public enum AutoNjsFixerModeType { MaintainNoteSpeed, ForceNJS } // bpm is required for maintain speed mode
        public virtual AutoNjsFixerModeType AutoNjsFixerMode { get; set; } = AutoNjsFixerModeType.MaintainNoteSpeed;

        //------------------------
        //v2 not working well. rotation event have wall penetrations. the mapping extension walls don't work and arcs do not work even though they are supposed to be supported. i can't find a single custom map v2 with arcs and none in the built in system for v1.34 either
        public virtual bool TurnOffJSONDatOutputAfterOneMapPlay { get; set; } = true; // to prevent constant JSON outputs
        public virtual bool OutputV2JsonToSongFolderNoArcsNoChainsNoMappingExtensionWalls { get; set; } = false; //will convert to v2 - doesn't output arcs or chains. arcs are supposed to be supported but always produce infinitely long arcs. Mappring extension walls don't work either for some reason so i filtered them out.
        public virtual bool OutputV3JsonToSongFolder { get; set; } = false; //will convert to v3
        //add to v2 or v3! since its the info.dat file. add it to each "_difficultyBeatmaps" that needs it: "_customData": {"_requirements": ["Mapping Extensions"]},
        //at least for beat sage, need to remove in info.dat:  "_environmentNames": [ "DefaultEnvironment" ], AND remove "_environmentNameIdx": 0, from each difficultyBeatmap in order for 360 map to use 360 environment
        public virtual bool OutputV4JsonToSongFolder { get; set; } = false;
        public virtual int OutputV4JsonSongFrequency { get; set; } = 44100;
    }
}
