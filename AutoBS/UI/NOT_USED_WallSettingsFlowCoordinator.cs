using HMUI;
using BeatSaberMarkupLanguage;
using BeatSaberMarkupLanguage.ViewControllers;
using UnityEngine;

namespace AutoBS.UI
{
    /*
    internal class WallSettingsFlowCoordinator : FlowCoordinator
    {
        public WallSettingsViewController wallSettingsViewController;

        protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
        {
            if (firstActivation)
            {
                // Set the title and enable the back button.
                SetTitle("Wall Settings", ViewController.AnimationType.In);
                showBackButton = true;

                // Create and load your WallSettingsViewController using BSML.
                wallSettingsViewController = BeatSaberUI.CreateViewController<WallSettingsViewController>();
                ProvideInitialViewControllers(wallSettingsViewController);
            }
        }

        protected override void BackButtonWasPressed(ViewController topViewController)
        {
            // When the back button is pressed, dismiss this coordinator.
            FlowCoordinator activeFlow = GameplaySetupView.GetDeepestFlowCoordinator(BeatSaberUI.MainFlowCoordinator);
            activeFlow.DismissFlowCoordinator(this, null, ViewController.AnimationDirection.Horizontal, false);
        }
    }
    */
}
