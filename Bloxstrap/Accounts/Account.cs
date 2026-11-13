using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Security.Cryptography;

namespace Bloxstrap.Accounts
{
    public class Account : INotifyPropertyChanged
    {
        private string _username = string.Empty;
        private string _displayName = string.Empty;
        private string _avatarUrl = string.Empty;
        private DateTime _lastUsedAt;

        public long UserId { get; set; }

        public string Username
        {
            get => _username;
            set
            {
                if (_username == value)
                    return;

                _username = value;
                OnPropertyChanged(nameof(Username));
                OnPropertyChanged(nameof(Tag));
            }
        }

        public string DisplayName
        {
            get => _displayName;
            set
            {
                if (_displayName == value)
                    return;

                _displayName = value;
                OnPropertyChanged(nameof(DisplayName));
                OnPropertyChanged(nameof(Initial));
                OnPropertyChanged(nameof(HasDisplayName));
            }
        }

        public string AvatarUrl
        {
            get => _avatarUrl;
            set
            {
                if (_avatarUrl == value)
                    return;

                _avatarUrl = value;
                OnPropertyChanged(nameof(AvatarUrl));
                OnPropertyChanged(nameof(HasAvatar));
            }
        }

        /// <summary>
        /// The account's .ROBLOSECURITY cookie, protected with DPAPI for the current Windows user.
        /// </summary>
        public string ProtectedCookie { get; set; } = string.Empty;

        public DateTime AddedAt { get; set; } = DateTime.UtcNow;

        public DateTime LastUsedAt
        {
            get => _lastUsedAt;
            set
            {
                if (_lastUsedAt == value)
                    return;

                _lastUsedAt = value;
                OnPropertyChanged(nameof(LastUsedAt));
            }
        }

        [JsonIgnore]
        public string Tag => $"@{Username}";

        [JsonIgnore]
        public bool HasAvatar => !String.IsNullOrEmpty(AvatarUrl);

        [JsonIgnore]
        public bool HasDisplayName => !String.IsNullOrEmpty(DisplayName);

        [JsonIgnore]
        public string Initial
        {
            get
            {
                string name = String.IsNullOrEmpty(DisplayName) ? Username : DisplayName;
                return String.IsNullOrEmpty(name) ? "?" : name[..1].ToUpperInvariant();
            }
        }

        [JsonIgnore]
        public bool HasCookie => !String.IsNullOrEmpty(ProtectedCookie);

        /// <summary>
        /// True for the account currently selected in the account menu.
        /// </summary>
        [JsonIgnore]
        public bool IsActive => AccountManager.ActiveAccount?.UserId == UserId;

        /// <summary>
        /// Decrypted .ROBLOSECURITY cookie. Use <see cref="SetCookie"/> to store a new one.
        /// </summary>
        [JsonIgnore]
        public string Cookie => Unprotect(ProtectedCookie);

        public void SetCookie(string cookie) => ProtectedCookie = Protect(cookie);

        internal static string Protect(string value)
        {
            byte[] encrypted = ProtectedData.Protect(
                Encoding.UTF8.GetBytes(value),
                null,
                DataProtectionScope.CurrentUser
            );

            return Convert.ToBase64String(encrypted);
        }

        internal static string Unprotect(string value)
        {
            if (String.IsNullOrWhiteSpace(value))
                return string.Empty;

            try
            {
                byte[] decrypted = ProtectedData.Unprotect(
                    Convert.FromBase64String(value),
                    null,
                    DataProtectionScope.CurrentUser
                );

                return Encoding.UTF8.GetString(decrypted);
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("Account::Unprotect", "Failed to decrypt a stored account cookie");
                App.Logger.WriteException("Account::Unprotect", ex);
                return String.Empty;
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        internal void NotifyActiveChanged() => OnPropertyChanged(nameof(IsActive));

        private void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public class AccountList
    {
        public ObservableCollection<Account> Accounts { get; set; } = new();

        /// <summary>
        /// The account the user last picked in the launch menu.
        /// </summary>
        public long SelectedUserId { get; set; }
    }
}