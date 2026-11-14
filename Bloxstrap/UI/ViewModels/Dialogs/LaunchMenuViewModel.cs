using System.Windows;
using System.Windows.Input;

using Bloxstrap.Accounts;
using Bloxstrap.UI.Elements.About;

using CommunityToolkit.Mvvm.Input;

namespace Bloxstrap.UI.ViewModels.Installer
{
    public class LaunchMenuViewModel : NotifyPropertyChangedViewModel
    {
        public string Version => string.Format(Strings.Menu_About_Version, App.Version);

        /// <summary>
        /// Shown under the account menu entry so the picked account is visible at a glance.
        /// </summary>
        public string ActiveAccountCaption
        {
            get
            {
                Account? account = AccountManager.ActiveAccount;

                if (account is null)
                    return Strings.Accounts_CurrentLogin;

                return String.IsNullOrEmpty(account.DisplayName)
                    ? account.Username
                    : $"{account.DisplayName} (@{account.Username})";
            }
        }

        public ICommand LaunchSettingsCommand => new RelayCommand(LaunchSettings);

        public ICommand LaunchRobloxCommand => new RelayCommand(LaunchRoblox);

        public ICommand LaunchRobloxStudioCommand => new RelayCommand(LaunchRobloxStudio);

        public ICommand LaunchAboutCommand => new RelayCommand(LaunchAbout);

        public event EventHandler<NextAction>? CloseWindowRequest;

        public void Refresh()
        {
            OnPropertyChanged(nameof(ActiveAccountCaption));
        }

        private void LaunchSettings() => CloseWindowRequest?.Invoke(this, NextAction.LaunchSettings);

        private void LaunchRoblox() => CloseWindowRequest?.Invoke(this, NextAction.LaunchRoblox);

        private void LaunchRobloxStudio() => CloseWindowRequest?.Invoke(this, NextAction.LaunchRobloxStudio);

        private void LaunchAbout() => new MainWindow().ShowDialog();
    }
}
