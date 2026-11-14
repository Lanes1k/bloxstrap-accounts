using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;

using Bloxstrap.Accounts;

namespace Bloxstrap.UI.Elements.Dialogs
{
    /// <summary>
    /// Interaction logic for AccountsDialog.xaml
    /// </summary>
    public partial class AccountsDialog
    {
        public AccountsDialog()
        {
            InitializeComponent();

            AccountList.ItemsSource = AccountManager.Accounts;

            Loaded += (_, _) => RefreshState();
            Closed += (_, _) => AccountManager.Accounts.CollectionChanged -= OnAccountsChanged;
            AccountManager.Accounts.CollectionChanged += OnAccountsChanged;
        }

        private void OnAccountsChanged(object? sender, EventArgs e) => RefreshState();

        private void RefreshState()
        {
            bool hasAccounts = AccountManager.Accounts.Count > 0;

            AccountListScroller.Visibility = hasAccounts ? Visibility.Visible : Visibility.Collapsed;
            EmptyState.Visibility = hasAccounts ? Visibility.Collapsed : Visibility.Visible;
        }

        private void OnAccountClicked(object sender, MouseButtonEventArgs e)
        {
            if (IsInsideButton(e.OriginalSource) || (sender as FrameworkElement)?.DataContext is not Account account)
                return;

            AccountManager.SetActive(account);
        }

        private void OnAddAccountClicked(object sender, RoutedEventArgs e)
        {
            new AddAccountDialog { Owner = this }.ShowDialog();
            RefreshState();
        }

        private void OnLaunchAccountClicked(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is not Account account)
                return;

            if (!AccountManager.Launch(account, out string? error))
            {
                if (!String.IsNullOrEmpty(error))
                    Frontend.ShowMessageBox(error, MessageBoxImage.Error);

                return;
            }

            RefreshState();
        }

        private void OnDeleteAccountClicked(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is not Account account)
                return;

            string name = String.IsNullOrEmpty(account.DisplayName) ? account.Username : account.DisplayName;

            var result = Frontend.ShowMessageBox(
                String.Format(Strings.Accounts_DeleteConfirm, name),
                MessageBoxImage.Warning,
                MessageBoxButton.YesNo
            );

            if (result != MessageBoxResult.Yes)
                return;

            AccountManager.Remove(account);
            RefreshState();
        }

        private void OnCloseClicked(object sender, RoutedEventArgs e) => Close();

        private static bool IsInsideButton(object? source)
        {
            if (source is not DependencyObject current)
                return false;

            while (current is not null)
            {
                if (current is System.Windows.Controls.Primitives.ButtonBase)
                    return true;

                if (current is not Visual and not Visual3D)
                    return false;

                current = VisualTreeHelper.GetParent(current);
            }

            return false;
        }
    }
}
