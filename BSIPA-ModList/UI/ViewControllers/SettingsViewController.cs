using CustomUI.Settings;
using IPA.Config;
using VRUI;

namespace BSIPA_ModList.UI
{
    internal static class SettingsViewController
    {
        private static SubMenu menu;
        private static BoolViewController autoUpdate;
        private static BoolViewController autoCheck;
        private static BoolViewController showEnableDisable;

        public static VRUIViewController Create()
        {
            menu = SettingsUI.CreateSubMenu("ModListSettings", false);

            autoCheck = menu.AddBool("Auto Update Check", "If enabled, automatically checks for updates on game start.");
            autoUpdate = menu.AddBool("Auto Update", "If enabled, automatically installs updates after checking for them.");
            showEnableDisable = menu.AddBool("Show Enable/Disable Button", "If enabled, BSIPA mods will have a button to enable or disable them.");

            autoCheck.applyImmediately = true;
            autoCheck.GetValue += () => IPA.Config.SelfConfig.Instance.Value.Updates.AutoCheckUpdates;
            autoCheck.SetValue += val =>
            {
                IPA.Config.SelfConfig.Instance.Value.Updates.AutoCheckUpdates = val;
                IPA.Config.SelfConfig.LoaderConfig.Store(IPA.Config.SelfConfig.Instance.Value);
            };

            autoUpdate.applyImmediately = true;
            autoUpdate.GetValue += () => IPA.Config.SelfConfig.Instance.Value.Updates.AutoUpdate;
            autoUpdate.SetValue += val =>
            {
                IPA.Config.SelfConfig.Instance.Value.Updates.AutoUpdate = val;
                IPA.Config.SelfConfig.LoaderConfig.Store(IPA.Config.SelfConfig.Instance.Value);
            };

            showEnableDisable.applyImmediately = true;
            showEnableDisable.GetValue += () => Plugin.config.Value.ShowEnableDisable;
            showEnableDisable.SetValue += val =>
            {
                Plugin.config.Value.ShowEnableDisable = val;
                Plugin.provider.Store(Plugin.config.Value);
            };

            autoCheck.Init();
            autoUpdate.Init();
            showEnableDisable.Init();

            return menu.viewController;
        }
    }
}
