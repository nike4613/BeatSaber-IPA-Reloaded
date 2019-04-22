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

        public static VRUIViewController Create()
        {
            menu = SettingsUI.CreateSubMenu("ModListSettings", false);

            autoCheck = menu.AddBool("Auto Update Check", "If enabled, automatically checks for updates on game start.");
            autoUpdate = menu.AddBool("Auto Update", "If enabled, automatically installs updates after checking for them.");

            autoCheck.applyImmediately = true;
            autoCheck.GetValue += () => SelfConfig.SelfConfigRef.Value.Updates.AutoCheckUpdates;
            autoCheck.SetValue += val =>
            {
                SelfConfig.SelfConfigRef.Value.Updates.AutoCheckUpdates = val;
                SelfConfig.LoaderConfig.Store(SelfConfig.SelfConfigRef.Value);
            };

            autoUpdate.applyImmediately = true;
            autoUpdate.GetValue += () => SelfConfig.SelfConfigRef.Value.Updates.AutoUpdate;
            autoUpdate.SetValue += val =>
            {
                SelfConfig.SelfConfigRef.Value.Updates.AutoUpdate = val;
                SelfConfig.LoaderConfig.Store(SelfConfig.SelfConfigRef.Value);
            };

            return menu.viewController;
        }
    }
}
