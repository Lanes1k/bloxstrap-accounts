using System.Windows;
using System.Windows.Input;

using Bloxstrap.Accounts;

namespace Bloxstrap.UI.Elements.Dialogs
{
    /// <summary>
    /// Interaction logic for AddAccountDialog.xaml
    /// </summary>
    public partial class AddAccountDialog
    {
        private CancellationTokenSource? _captureCancellation;

        public AddAccountDialog()
        {
            InitializeComponent();
            Closed += (_, _) => _captureCancellation?.Cancel();
        }

        private void OnCookieCardClicked(object sender, MouseButtonEventArgs e) => ShowCookiePanel();

        private void OnRobloxWindowCardClicked(object sender, MouseButtonEventArgs e) => _ = StartRobloxWindowAsync();

        private void ShowCookiePanel()
        {
            CookiePanel.Visibility = Visibility.Visible;
            RobloxWindowPanel.Visibility = Visibility.Collapsed;
            CookieTextBox.Focus();
        }

        private async Task StartRobloxWindowAsync()
        {
            if (AccountManager.IsRobloxRunning)
            {
                var result = Frontend.ShowMessageBox(
                    Strings.Accounts_CloseRobloxConfirm,
                    MessageBoxImage.Question,
                    MessageBoxButton.YesNo
                );

                if (result != MessageBoxResult.Yes)
                    return;

                AccountManager.CloseRoblox();
                await Task.Delay(500);
            }

            CookiePanel.Visibility = Visibility.Collapsed;
            RobloxWindowPanel.Visibility = Visibility.Visible;
            SetCardsEnabled(false);

            _captureCancellation = new CancellationTokenSource();

            var progress = new Progress<string>(status => RobloxWindowStatus.Text = status);

            try
            {
                var (account, error) = await AccountManager.CaptureFromRobloxWindowAsync(progress, _captureCancellation.Token);

                if (account is not null)
                {
                    Close();
                    return;
                }

                RobloxWindowPanel.Visibility = Visibility.Collapsed;
                SetCardsEnabled(true);

                if (!String.IsNullOrEmpty(error))
                    Frontend.ShowMessageBox(error, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("AddAccountDialog::StartRobloxWindowAsync", "Failed to capture an account from the Roblox window");
                App.Logger.WriteException("AddAccountDialog::StartRobloxWindowAsync", ex);

                RobloxWindowPanel.Visibility = Visibility.Collapsed;
                SetCardsEnabled(true);
                Frontend.ShowMessageBox(Strings.Accounts_LoginError, MessageBoxImage.Error);
            }
        }

        private async void OnAddCookieClicked(object sender, RoutedEventArgs e)
        {
            CookieErrorText.Visibility = Visibility.Collapsed;
            AddCookieButton.IsEnabled = false;

            try
            {
                var (account, error) = await AccountManager.AddFromCookieAsync(CookieTextBox.Text);

                if (account is not null)
                {
                    Close();
                    return;
                }

                CookieErrorText.Text = error ?? Strings.Accounts_AddError;
                CookieErrorText.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("AddAccountDialog::OnAddCookieClicked", "Failed to add an account from a cookie");
                App.Logger.WriteException("AddAccountDialog::OnAddCookieClicked", ex);

                CookieErrorText.Text = Strings.Accounts_AddError;
                CookieErrorText.Visibility = Visibility.Visible;
            }
            finally
            {
                AddCookieButton.IsEnabled = true;
            }
        }

        private void OnCookieTextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            CookieErrorText.Visibility = Visibility.Collapsed;
            CookiePlaceholder.Visibility = String.IsNullOrEmpty(CookieTextBox.Text) ? Visibility.Visible : Visibility.Collapsed;
        }

        private void OnCancelClicked(object sender, RoutedEventArgs e) => Close();

        private void OnCancelWindowClicked(object sender, RoutedEventArgs e)
        {
            _captureCancellation?.Cancel();
            RobloxWindowPanel.Visibility = Visibility.Collapsed;
            SetCardsEnabled(true);
        }

        private void SetCardsEnabled(bool enabled)
        {
            RobloxWindowCard.IsEnabled = enabled;
            CookieCard.IsEnabled = enabled;
            RobloxWindowCard.Opacity = enabled ? 1 : 0.5;
            CookieCard.Opacity = enabled ? 1 : 0.5;
        }
    }
}
