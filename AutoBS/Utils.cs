using AutoBS.Patches;
using AutoBS.UI;
using BeatmapSaveDataVersion2_6_0AndEarlier;
using BeatmapSaveDataVersion3;
using BGLib.UnityExtension;
using BS_Utils.Gameplay;
using CustomJSONData.CustomBeatmap;
using IPA.Config.Stores.Converters;
using IPA.Loader;
using JetBrains.Annotations;
using Newtonsoft.Json.Linq;
using SongCore;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net.Http.Headers;
using System.Reflection;
using System.Threading;
using UnityEngine;
using static BeatSaberMarkupLanguage.Components.KEYBOARD;

namespace AutoBS
{
    internal class Utils
    {
        public static bool IsEnabledForGeneralFeatures()
        {
            if (TransitionPatcher.SelectedSerializedName == GameModeHelper.GENERATED_360DEGREE_MODE || IsEnabledArcs() || IsEnabledChains() || IsEnabledWalls() || IsEnabledLighting() || IsEnabledAutoNjsFixer() || Config.Instance.EnableCleanBeatSage)
            {
                Plugin.LogDebug("[Utils] General features enabled.");
                return true;
            }
            else
            {
                Plugin.LogDebug("[Utils] General features disabled.");
                return false;
            }
        }
        public static bool IsEnabledRotations()
        {
            //if (HarmonyPatches.RequiresNoodle) return false;

            if (TransitionPatcher.SelectedSerializedName == GameModeHelper.GENERATED_360DEGREE_MODE)
            {
                //Plugin.Log.Info("Rotations enabled");
                return true;
            }
            else
            {
                return false;
            }
        }
        public static bool IsEnabledFOV(bool wallsAdded)
        {
            if (TransitionPatcher.SelectedSerializedName == GameModeHelper.GENERATED_360DEGREE_MODE ||
                (Config.Instance.EnableWallsNonGen360 &&
                ((TransitionPatcher.SelectedSerializedName == "360Degree" || TransitionPatcher.SelectedSerializedName == "90Degree") && wallsAdded))) // use this for nonGen360 maps with wall gen since old 360fyer generated maps have wild rotations that cause walls to reverse through the frame. this will not help some walls blocking player vision that are built into 360fyer old generated output
            {
                //Plugin.Log.Info("FOV enabled");
                return true;
            }
            else
            {
                return false;
            }
        }

        public static bool IsEnabledArcs()
        {
            string characteristic = TransitionPatcher.SelectedSerializedName;

            if ((!Config.Instance.EnableArcsGen360    && characteristic  == "Generated360Degree") ||
                (!Config.Instance.EnableArcsNonGen360 && (characteristic == "360Degree" || characteristic == "90Degree")) ||
                (!Config.Instance.EnableArcsStandard  && characteristic  != "Generated360Degree" &&
                  characteristic != "360Degree" && characteristic != "90Degree"))
            {
                return false;
            }
            //Plugin.Log.Info($"Arcs enabled.");
            return true;
        }

        public static bool IsEnabledChains()
        {
            string characteristic = TransitionPatcher.SelectedSerializedName;

            if ((!Config.Instance.EnableChainsGen360    && characteristic  == "Generated360Degree") ||
                (!Config.Instance.EnableChainsNonGen360 && (characteristic == "360Degree" || characteristic == "90Degree")) ||
                (!Config.Instance.EnableChainsStandard  && characteristic  != "Generated360Degree" &&
                  characteristic  != "360Degree" && characteristic != "90Degree"))
            {
                return false;
            }
            //Plugin.Log.Info($"Chains enabled.");
            return true;
        }

        public static bool IsEnabledWalls()
        {
            string characteristic = TransitionPatcher.SelectedSerializedName;
            //Plugin.Log.Info($"IsEnabledWalls Characteristic: {characteristic}");

            if ((!Config.Instance.EnableWallsGen360    && characteristic  == "Generated360Degree") ||
                (!Config.Instance.EnableWallsNonGen360 && (characteristic == "360Degree" || characteristic == "90Degree")) ||
                (!Config.Instance.EnableWallsStandard  && characteristic  != "Generated360Degree" &&
                  characteristic != "360Degree" && characteristic != "90Degree"))
            {
                return false;
            }
            //Plugin.Log.Info($"Walls enabled.");
            return true;
        }
        public static bool IsEnabledExtensionWalls()
        {
            if (!IsEnabledWalls()) return false;

            if (GameplaySetupView.IsMappingExtensionsInstalled) return false; //fix!!!!!!!!!

            return true;
        }

        public static bool IsEnabledLighting()
        {
            string characteristic = TransitionPatcher.SelectedSerializedName;

            if ((!Config.Instance.EnableLightingGen360    && characteristic  == "Generated360Degree") ||
                (!Config.Instance.EnableLightingNonGen360 && (characteristic == "360Degree" || characteristic == "90Degree")) ||
                (!Config.Instance.EnableLightingStandard  && characteristic  != "Generated360Degree" &&
                  characteristic != "360Degree" && characteristic != "90Degree"))
            {
                return false;
            }
            //Plugin.Log.Info($"Lighting enabled for standard:{Config.Instance.EnableLightingStandard} and charac: {characteristic}");
            //Plugin.Log.Info($"Lighting enabled.");
            return true;
        }
        public static bool IsEnabledAutoNjsFixer()
        {
            if (TransitionPatcher.AutoNJSDisabledByConflictingMod) return false;

            //Will need to check for various types of lighting despite this function (such as lightmapper, etc)

            string characteristic = TransitionPatcher.SelectedSerializedName;

            if ((!Config.Instance.EnableAutoNjsFixerGen360 && characteristic == "Generated360Degree") ||
                (!Config.Instance.EnableAutoNjsFixerNonGen360 && (characteristic == "360Degree" || characteristic == "90Degree")) ||
                (!Config.Instance.EnableAutoNjsFixerStandard && characteristic != "Generated360Degree" &&
                  characteristic != "360Degree" && characteristic != "90Degree"))
            {
                Plugin.LogDebug($"[IsEnabledAutoNjsFixer][AutoNjsFixer] disabled for charac: {characteristic}");
                return false;
            }
            //Plugin.Log.Info($"Lighting enabled for standard:{Config.Instance.EnableLightingStandard} and charac: {characteristic}");
            //Plugin.Log.Info($"Lighting enabled.");
            return true;
        }
        // look for other mods being enabled like JDFixer and LevelTweaks and NJSFixer
        public static bool IsModEnabled(string modName)
        {
            return PluginManager.EnabledPlugins.Any(p =>
                p.Name.Equals(modName, StringComparison.OrdinalIgnoreCase));
        }

    }
}
