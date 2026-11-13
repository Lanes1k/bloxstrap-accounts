using System.Collections.ObjectModel;

namespace Bloxstrap.Accounts
{
    /// <summary>
    /// Stores the list of saved Roblox accounts and handles adding, capturing and switching accounts.
    /// </summary>
    public static class AccountManager
    {
        private const string LOG_IDENT = "AccountManager";
        private const string CookieName = ".ROBLOSECURITY";

        private static readonly LazyJsonManager<AccountList> Store = new("Accounts");

        public static ObservableCollection<Account> Accounts
        {
            get
            {
                AccountList list = Store.Prop;

                if (list.Accounts is null)
                    list.Accounts = new ObservableCollection<Account>();

                return list.Accounts;
            }
        }

        /// <summary>
        /// The account currently picked in the launch menu, used for the next Roblox launch.
        /// </summary>
        public static Account? ActiveAccount => Accounts.FirstOrDefault(x => x.UserId == Store.Prop.SelectedUserId);

        public static void SetActive(Account account)
        {
            Store.Prop.SelectedUserId = account.UserId;
            Save();

            foreach (var item in Accounts)
                item.NotifyActiveChanged();
        }

        /// <summary>
        /// Writes the active account's cookie into the Roblox client so it is used on the next launch.
        /// </summary>
        public static bool ApplyActiveAccount()
        {
            Account? account = ActiveAccount;

            if (account is null || String.IsNullOrEmpty(account.Cookie))
                return true;

            App.Logger.WriteLine(LOG_IDENT, $"Applying account cookie for {account.Username}");

            if (!RobloxCookieStore.WriteCookie(account.Cookie))
            {
                App.Logger.WriteLine(LOG_IDENT, "Failed to apply the active account cookie");
                return false;
            }

            account.LastUsedAt = DateTime.UtcNow;
            Save();

            return true;
        }

        /// <summary>
        /// Fired whenever the account list changes.
        /// </summary>
        public static event Action? Changed;

        public static void Load() => Store.Load(false);

        public static void Save()
        {
            Store.Save();
            Changed?.Invoke();
        }

        public static bool IsRobloxRunning => Process.GetProcessesByName(App.RobloxPlayerAppName).Any()
            || Process.GetProcessesByName("RobloxCrashHandler").Any();

        /// <summary>
        /// Validates a cookie against the Roblox API and saves the account it belongs to.
        /// If the account is already saved, its cookie and profile are refreshed.
        /// </summary>
        public static async Task<(Account? Account, string? Error)> AddFromCookieAsync(string rawCookie, CancellationToken token = default)
        {
            string cookie = NormalizeCookie(rawCookie);

            if (String.IsNullOrEmpty(cookie))
                return (null, "Вставь cookie вида .ROBLOSECURITY=...");

            if (cookie.Length < 50)
                return (null, "Cookie выглядит некорректно. Скопируй его целиком.");

            AuthenticatedUser? user;

            try
            {
                user = await GetAuthenticatedUserAsync(cookie, token);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT, "Failed to validate cookie");
                App.Logger.WriteException(LOG_IDENT, ex);
                return (null, "Не удалось связаться с Roblox. Проверь подключение и попробуй ещё раз.");
            }

            if (user is null || user.Id <= 0)
                return (null, "Cookie недействителен или истёк. Получи новый и попробуй снова.");

            string? avatarUrl = await GetAvatarUrlAsync(user.Id, token);

            Account account = Accounts.FirstOrDefault(x => x.UserId == user.Id) ?? new Account { UserId = user.Id };

            account.Username = user.Name;
            account.DisplayName = user.DisplayName;

            if (!String.IsNullOrEmpty(avatarUrl))
                account.AvatarUrl = avatarUrl;

            account.SetCookie(cookie);

            if (!Accounts.Contains(account))
                Accounts.Add(account);

            // Newly added or refreshed accounts become the one used for the next launch
            Store.Prop.SelectedUserId = account.UserId;

            Save();

            foreach (var item in Accounts)
                item.NotifyActiveChanged();

            return (account, null);
        }

        /// <summary>
        /// Switches the Roblox client to this account and starts another client instance.
        /// </summary>
        public static bool Launch(Account account, out string? error)
        {
            error = null;

            string cookie = account.Cookie;

            if (String.IsNullOrEmpty(cookie))
            {
                error = "Cookie этого аккаунта повреждён. Добавь аккаунт заново.";
                return false;
            }

            if (!RobloxCookieStore.WriteCookie(cookie))
            {
                error = "Не удалось записать cookie Roblox. Проверь права доступа к папке Roblox.";
                return false;
            }

            account.LastUsedAt = DateTime.UtcNow;
            Store.Prop.SelectedUserId = account.UserId;
            Save();

            foreach (var item in Accounts)
                item.NotifyActiveChanged();

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = Paths.Process,
                    Arguments = "-player"
                });
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT, "Failed to launch Roblox");
                App.Logger.WriteException(LOG_IDENT, ex);
                error = "Не удалось запустить Roblox.";
                return false;
            }

            return true;
        }

        /// <summary>
        /// Opens the Roblox client window so the user can sign in, then captures the cookie
        /// the client stores after a successful login.
        /// </summary>
        public static async Task<(Account? Account, string? Error)> CaptureFromRobloxWindowAsync(IProgress<string>? progress, CancellationToken token)
        {
            if (IsRobloxRunning)
                return (null, "Сначала закрой Roblox, чтобы войти в другой аккаунт.");

            // Keep the current session around in case the user cancels the login
            string? originalCookieList = RobloxCookieStore.ReadPlaintext();
            string originalCookie = RobloxCookieStore.ReadCookie() ?? String.Empty;

            // Start the client logged out so a fresh login can be detected
            RobloxCookieStore.Clear();

            progress?.Report("Запускаем Roblox…");

            if (!TryStartRobloxClient())
            {
                RestoreCookies(originalCookieList);
                return (null, "Не удалось запустить Roblox. Проверь, что он установлен.");
            }

            DateTime started = DateTime.UtcNow;
            bool seenRunning = false;

            try
            {
                while (!token.IsCancellationRequested)
                {
                    await Task.Delay(1500, token);

                    if (IsRobloxRunning)
                        seenRunning = true;

                    string? cookie = RobloxCookieStore.ReadCookie();

                    if (!String.IsNullOrEmpty(cookie) && cookie != originalCookie)
                    {
                        progress?.Report("Проверяем вход…");

                        var (account, _) = await AddFromCookieAsync(cookie, token);

                        if (account is not null)
                            return (account, null);
                    }

                    // The client was open and has since been closed without a successful login
                    if (seenRunning && !IsRobloxRunning && (DateTime.UtcNow - started).TotalSeconds > 10)
                        break;

                    if (DateTime.UtcNow - started > TimeSpan.FromMinutes(10))
                        break;
                }
            }
            catch (OperationCanceledException)
            {
            }

            if (token.IsCancellationRequested)
            {
                CloseRoblox();
                await Task.Delay(500);
            }

            RestoreCookies(originalCookieList);

            return (null, "Вход не выполнен. Попробуй ещё раз или добавь аккаунт через cookie.");
        }

        public static void Remove(Account account)
        {
            Accounts.Remove(account);

            if (Store.Prop.SelectedUserId == account.UserId)
                Store.Prop.SelectedUserId = Accounts.FirstOrDefault()?.UserId ?? 0;

            Save();

            foreach (var item in Accounts)
                item.NotifyActiveChanged();
        }

        public static void CloseRoblox()
        {
            foreach (string processName in new[] { App.RobloxPlayerAppName, "RobloxCrashHandler" })
            {
                foreach (var process in Process.GetProcessesByName(processName))
                {
                    try
                    {
                        process.Kill();
                        process.WaitForExit(3000);
                    }
                    catch (Exception ex)
                    {
                        App.Logger.WriteLine(LOG_IDENT, $"Failed to close {processName} ({process.Id})");
                        App.Logger.WriteException(LOG_IDENT, ex);
                    }
                }
            }
        }

        /// <summary>
        /// Strips the cookie out of whatever the user pasted: a bare value, an HTTP header,
        /// or a whole cookie line.
        /// </summary>
        public static string NormalizeCookie(string rawCookie)
        {
            if (String.IsNullOrWhiteSpace(rawCookie))
                return String.Empty;

            string value = rawCookie.Trim().Trim('"', '\'');

            Match line = Regex.Match(value, @"\t\.ROBLOSECURITY\t([^\r\n;]+)");
            if (line.Success)
                value = line.Groups[1].Value;

            const string prefix = CookieName + "=";

            if (value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                value = value[prefix.Length..];

            value = value.Split(';', '\r', '\n')[0].Trim().Trim('"', '\'');

            return value;
        }

        private static bool TryStartRobloxClient()
        {
            string versionGuid = App.PlayerState.Prop.VersionGuid;

            if (!String.IsNullOrEmpty(versionGuid))
            {
                string clientPath = Path.Combine(Paths.Versions, versionGuid, $"{App.RobloxPlayerAppName}.exe");

                if (File.Exists(clientPath))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = clientPath,
                        WorkingDirectory = Path.GetDirectoryName(clientPath)!
                    });

                    return true;
                }
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = Paths.Process,
                Arguments = "-player"
            });

            return true;
        }

        private static void RestoreCookies(string? plaintext)
        {
            if (!String.IsNullOrEmpty(plaintext))
                RobloxCookieStore.WritePlaintext(plaintext);
            else
                RobloxCookieStore.Clear();
        }

        private static async Task<AuthenticatedUser?> GetAuthenticatedUserAsync(string cookie, CancellationToken token)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "https://users.roblox.com/v1/users/authenticated");
            request.Headers.TryAddWithoutValidation("Cookie", $"{CookieName}={cookie}");
            request.Headers.TryAddWithoutValidation("Referer", "https://www.roblox.com/");

            using var response = await App.HttpClient.SendAsync(request, token);

            if (response.StatusCode == HttpStatusCode.Unauthorized)
                return null;

            response.EnsureSuccessStatusCode();

            string json = await response.Content.ReadAsStringAsync(token);

            return JsonSerializer.Deserialize<AuthenticatedUser>(json);
        }

        private static async Task<string?> GetAvatarUrlAsync(long userId, CancellationToken token)
        {
            try
            {
                var request = new ThumbnailRequest
                {
                    TargetId = (ulong)userId,
                    Type = "AvatarHeadShot",
                    Size = "150x150",
                    Format = "Png",
                    IsCircular = true
                };

                return await Thumbnails.GetThumbnailUrlAsync(request, token);
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT, $"Failed to fetch the avatar for user {userId}");
                App.Logger.WriteException(LOG_IDENT, ex);
                return null;
            }
        }

        private class AuthenticatedUser
        {
            [JsonPropertyName("id")]
            public long Id { get; set; }

            [JsonPropertyName("name")]
            public string Name { get; set; } = string.Empty;

            [JsonPropertyName("displayName")]
            public string DisplayName { get; set; } = string.Empty;
        }
    }
}