using AutoBS.Patches;
using HarmonyLib;
using IPA.Loader;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace AutoBS
{
    // ChatGPT conversation "Force Activate Extensions" - Since 360fyer cannot send the requirements such as mapping extensions to the 360fyer version, this will force activate it if the orginal standard level has mapping extensions
    // for chroma/noodle - Both mods gate activation in their FeaturesModule.Condition(...). That means you can flip them on reliably by Harmony-postfixing those Condition methods
    internal static class ForceActivatePatches
    {
        public static void Install(Harmony h)
        {
            //if (!Config.Instance.EnablePlugin || !Config.Instance.Enable360fyer) return; // can't do this or will not run when needed later
            PatchChromaCondition(h);
            PatchNoodleCondition(h);
        }

        static void PatchChromaCondition(Harmony h)
        {
            var t = AccessTools.TypeByName("Chroma.Modules.FeaturesModule");
            if (t == null) { Plugin.Log.Warn("[ForceActivate] Chroma FeaturesModule not found."); return; }

            // Chroma has 2 Condition variants across versions:
            //   private bool Condition(Capabilities capabilities)
            //   private bool Condition(IDifficultyBeatmap db, Capabilities capabilities)   // PRE_V1_37_1
            var targets = t.GetMethods(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                           .Where(m => m.Name == "Condition")
                           .ToArray();

            if (targets.Length == 0)
            {
                Plugin.Log.Warn("[ForceActivate] Chroma Condition() not found.");
                return;
            }

            foreach (var mi in targets)
            {
                try
                {
                    h.Patch(mi, postfix: new HarmonyMethod(typeof(ForceActivatePatches), nameof(ChromaConditionPostfix)));
                    //Plugin.Log.Info($"[ForceActivate] Patched Chroma Condition: {mi}");
                }
                catch (Exception ex)
                {
                    Plugin.Log.Warn($"[ForceActivate] Failed to patch Chroma Condition {mi}: {ex}");
                }
            }
        }

        static void PatchNoodleCondition(Harmony h)
        {
            var t = AccessTools.TypeByName("NoodleExtensions.FeaturesModule");
            if (t == null) { Plugin.Log.Warn("[ForceActivate] Noodle FeaturesModule not found."); return; }

            // Noodle: private static bool Condition(Capabilities capabilities)
            var mi = t.GetMethods(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
                      .FirstOrDefault(m => m.Name == "Condition");
            if (mi == null)
            {
                Plugin.Log.Warn("[ForceActivate] Noodle Condition() not found.");
                return;
            }

            try
            {
                h.Patch(mi, postfix: new HarmonyMethod(typeof(ForceActivatePatches), nameof(NoodleConditionPostfix)));
                //Plugin.Log.Info($"[ForceActivate] Patched Noodle Condition: {mi}");
            }
            catch (Exception ex)
            {
                Plugin.Log.Warn($"[ForceActivate] Failed to patch Noodle Condition: {ex}");
            }
        }

        // Postfixes only need __result; we don't need exact parameter types.
        static void ChromaConditionPostfix(ref bool __result)
        {
            // Only OR-true when you actually injected Chroma content
            if (!__result && TransitionPatcher.RequiresChroma)
            {
                __result = true;
                Plugin.Log.Info("[ForceActivate] Forcing Chroma Active (detected Chroma content in generated data).");
            }
        }

        static void NoodleConditionPostfix(ref bool __result)
        {
            if (!__result && TransitionPatcher.RequiresNoodle)
            {
                __result = true;
                Plugin.Log.Info("[ForceActivate] Forcing Noodle Extensions Active (detected NE content in generated data).");
            }
        }

        // Mapping Extension has its own built-in method to force activate it for a song
        public static void MappingExtensionsForceActivate()
        {
            if (!Config.Instance.EnablePlugin || !Utils.IsEnabledForGeneralFeatures())
                return;

            bool alreadyUsing = TransitionPatcher.MapAlreadyUsesMappingExtensions;

            //Plugin.Log.Warn($"Mapping Extensions test - IsEnabledExtensionWalls: {Utils.IsEnabledExtensionWalls()}, mapAlreadyUsesMappingExtensions: {mapAlreadyUsesMappingExtensions}");

            if ((Utils.IsEnabledExtensionWalls() && !alreadyUsing) || alreadyUsing)
            {
                // Get all loaded assemblies
                var assemblies = AppDomain.CurrentDomain.GetAssemblies();

                // Find the 'MappingExtensions' assembly by name
                var mappingExtensionsAssembly = assemblies.FirstOrDefault(a => a.GetName().Name == "MappingExtensions");

                if (mappingExtensionsAssembly == null)
                {
                    //Plugin.Log.Warn("MappingExtensions assembly not found.");
                    return;
                }

                // Get the 'Plugin' class type
                var pluginType = mappingExtensionsAssembly.GetType("MappingExtensions.Plugin");

                if (pluginType == null)
                {
                    //Plugin.Log.Warn("Plugin class not found in MappingExtensions assembly.");
                    return;
                }

                // Find the 'ForceActivateForSong' method
                var forceActivateForSongMethod = pluginType.GetMethod("ForceActivateForSong", BindingFlags.Public | BindingFlags.Static);

                if (forceActivateForSongMethod == null)
                {
                    //Plugin.Log.Warn("ForceActivateForSong method not found in Plugin class.");
                    return;
                }

                // Invoke the 'ForceActivateForSong' method
                forceActivateForSongMethod.Invoke(null, null);

                Plugin.Log.Info("[MappingExtensionsForceActivate] Mapping Extensions - ForceActivateForSong method invoked successfully.");
                //SongCore.Collections.RegisterCapability("Mapping Extensions");
            }

        }
    }
}
