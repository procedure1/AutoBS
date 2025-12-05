using HarmonyLib;
using IPA;
using IPA.Config.Stores;
using AutoBS.UI;//BW UI
using IPALogger = IPA.Logging.Logger;
using IPAConfig = IPA.Config.Config;
using Zenject;
using SiraUtil.Zenject;//needed to get Zenjector for installer
using JetBrains.Annotations;
using System.Linq;
using UnityEngine;
using System;
using System.Collections.Generic;
using AutoBS.Patches;
using System.Reflection;
using BeatSaberMarkupLanguage.GameplaySetup;
using UnityEngine.SceneManagement;
using SongCore;

namespace AutoBS
{
    [Plugin(RuntimeOptions.SingleStartInit)]
    public class Plugin
    {
        internal static Plugin Instance { get; private set; }
        internal static IPALogger Log { get; private set; }

        public static BeatmapLevelsModel BeatmapLevelsModel { get; private set; }

        [Init]
        public void Init(IPALogger logger, IPAConfig conf, Zenjector zenjector)
        {
            Instance = this;
            Log = logger;
            Config.Instance = conf.Generated<Config>();
            Log.Info("AutoBS initialized.");

            // Installers
            zenjector.Install<AppInstaller>(Location.App);
            zenjector.Install<MenuInstaller>(Location.Menu);
        }

        [OnStart]
        public void OnApplicationStart()
        {
            var harmony = new Harmony("com.bradwallace.AutoBS");
            
            ForceActivatePatches.Install(harmony); // forcing Chroma/Noodle activation 

            harmony.PatchAll();


            
            //Disabled 
            //Plugin.Log?.Info("[WallTimeOverlayDebug] OnApplicationStart()");
            //SceneManager.activeSceneChanged += OnActiveSceneChanged;
        }

        // Only for adding text labels to walls/notes for debugging
        private GameObject _runner;

        private void OnActiveSceneChanged(Scene prev, Scene next)
        {
            Debug.Log("[WallTimeOverlayDebug] activeSceneChanged → " + next.name);

            if (next.name.Contains("GameCore"))
            {
                if (_runner == null)
                {
                    _runner = new GameObject("WallTimeOverlayDebug_Runner");
                    UnityEngine.Object.DontDestroyOnLoad(_runner);

                    // Use the new debug overlay class
                    _runner.AddComponent<WallTimeOverlayDebug>();

                    Debug.Log("[WallTimeOverlayDebug] Runner created in GameCore");
                }
            }
            else
            {
                if (_runner != null)
                {
                    UnityEngine.Object.Destroy(_runner);
                    _runner = null;
                    Debug.Log("[WallTimeOverlayDebug] Runner destroyed (left GameCore)");
                }
            }
        }


        [OnExit]
        public void OnApplicationQuit() 
        {
            Config.Instance.TurnOffJSONDatOutputAfterOneMapPlay = true;
            Config.Instance.OutputV2JsonToSongFolderNoArcsNoChainsNoMappingExtensionWalls = false;
            Config.Instance.OutputV3JsonToSongFolder = false;
            Config.Instance.OutputV4JsonToSongFolder = false;
            Config.Instance.Changed();
        }
    }
    //Zenject installer. Need this to access and control ParametricBoxController since I couldn't find another way to access that method. If could figure out how to patch a level of beatsaber i might be able to find it another way
    //I access this in LevelUpdatePatcher.cs so that the level has already started and gameObject that uses this has spawned. I cannot access it here since the gameObject hasn't spawned.
    // Runs globally, for game mode setup and controller binding
    public class AppInstaller : Installer
    {
        public override void InstallBindings()
        {
            Container.Bind<ParametricBoxController>().FromComponentInHierarchy().AsTransient();

            //Container.BindInterfacesAndSelfTo<BeatmapDataLoaderInjector>().AsSingle();

            RegisterGameModes();
        }

        private void RegisterGameModes()
        {
            var GameMode360 = GetCustomGameMode("GEN360", "Generated 360 mode", "Generated360Degree", "Generated360Degree");
            var GameMode90 = GetCustomGameMode("GEN90", "Generated 90 mode", "Generated90Degree", "Generated90Degree");
        }

        private BeatmapCharacteristicSO GetCustomGameMode(string characteristicName, string hintText, string serializedName, string compoundIdPartName, bool requires360Movement = true, bool containsRotationEvents = true, int sortingOrder = 99)
        {
            var customGameMode = Collections.customCharacteristics.FirstOrDefault(x => x.serializedName == serializedName);
            if (customGameMode != null)
                return customGameMode;

            var tex = new Texture2D(50, 50);
            var icon = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            return Collections.RegisterCustomCharacteristic(icon, characteristicName, hintText, serializedName, compoundIdPartName, requires360Movement, containsRotationEvents, sortingOrder);
        }
    }
    // Runs only in MainMenu for UI tab registration
    public class MenuInstaller : Installer
    {
        public override void InstallBindings()
        {
            Container.BindInterfacesAndSelfTo<GameplaySetupTabRegister>().AsSingle();
        }
    }

    // Handles showing your tab in the GameplaySetup menu
    public class GameplaySetupTabRegister : IInitializable, IDisposable
    {
        private const string TabName = "AutoBS";
        private const string ResourcePath = "AutoBS.UI.GameplaySetupView.bsml";
        private readonly GameplaySetupView _view;

        public GameplaySetupTabRegister()
        {
            _view = new GameplaySetupView();
        }

        public void Initialize()
        {
            GameplaySetup.Instance.AddTab(TabName, ResourcePath, _view);
        }

        public void Dispose()
        {
            //v1.39
            //GameplaySetup.Instance.RemoveTab(TabName);
        }
    }

}
