using System.Security.Cryptography;

namespace Bloxstrap.Accounts
{
    /// <summary>
    /// Reads and writes the Roblox client's cookie store
    /// (%LOCALAPPDATA%\Roblox\LocalStorage\RobloxCookies.dat).
    /// On Windows the file is a small JSON envelope around a DPAPI-protected,
    /// Netscape-formatted cookie list.
    /// </summary>
    internal static class RobloxCookieStore
    {
        private const string LOG_IDENT = "RobloxCookieStore";
        private const string CookieName = ".ROBLOSECURITY";

        private static readonly Regex AuthCookieRegex = new(@"\t\.ROBLOSECURITY\t([^\r\n;]+)", RegexOptions.Compiled);

        public static string CookiesPath => Path.Combine(Paths.LocalAppData, "Roblox", "LocalStorage", "RobloxCookies.dat");

        private class CookiesFile
        {
            [JsonPropertyName("CookiesVersion")]
            public string Version { get; set; } = "1";

            [JsonPropertyName("CookiesData")]
            public string Data { get; set; } = string.Empty;
        }

        /// <summary>
        /// Returns the current .ROBLOSECURITY cookie stored by the Roblox client, or null if there isn't one.
        /// </summary>
        public static string? ReadCookie()
        {
            string? plaintext = ReadPlaintext();

            if (plaintext is null)
                return null;

            Match match = AuthCookieRegex.Match(plaintext);

            return match.Success ? match.Groups[1].Value.Trim() : null;
        }

        /// <summary>
        /// Returns the decrypted cookie list (Netscape format) stored by the Roblox client.
        /// </summary>
        public static string? ReadPlaintext()
        {
            try
            {
                if (!File.Exists(CookiesPath))
                    return null;

                string contents = File.ReadAllText(CookiesPath);

                string payload = contents.TrimStart().StartsWith('{')
                    ? JsonSerializer.Deserialize<CookiesFile>(contents)?.Data ?? string.Empty
                    : contents;

                if (String.IsNullOrWhiteSpace(payload))
                    return null;

                byte[] decrypted = ProtectedData.Unprotect(
                    Convert.FromBase64String(payload),
                    null,
                    DataProtectionScope.CurrentUser
                );

                return Encoding.UTF8.GetString(decrypted);
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT, "Failed to read the Roblox cookie file");
                App.Logger.WriteException(LOG_IDENT, ex);
                return null;
            }
        }

        /// <summary>
        /// Writes the given .ROBLOSECURITY cookie into the Roblox client's cookie store,
        /// keeping any other cookies that are already there.
        /// </summary>
        public static bool WriteCookie(string cookie)
        {
            try
            {
                string plaintext = ReadPlaintext() ?? CreateDefaultCookieList();

                var lines = plaintext
                    .Split('\n')
                    .Where(line => !line.Contains($"\t{CookieName}\t", StringComparison.Ordinal))
                    .Where(line => !String.IsNullOrWhiteSpace(line))
                    .ToList();

                lines.Add($"#HttpOnly_.roblox.com\tTRUE\t/\tTRUE\t1817995500\t{CookieName}\t{cookie}");

                byte[] encrypted = ProtectedData.Protect(
                    Encoding.UTF8.GetBytes(String.Join("\n", lines)),
                    null,
                    DataProtectionScope.CurrentUser
                );

                var file = new CookiesFile { Data = Convert.ToBase64String(encrypted) };

                Directory.CreateDirectory(Path.GetDirectoryName(CookiesPath)!);
                File.WriteAllText(CookiesPath, JsonSerializer.Serialize(file));

                return true;
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT, "Failed to write the Roblox cookie file");
                App.Logger.WriteException(LOG_IDENT, ex);
                return false;
            }
        }

        /// <summary>
        /// Writes a decrypted cookie list (Netscape format) back to the Roblox cookie store as-is.
        /// </summary>
        public static bool WritePlaintext(string plaintext)
        {
            try
            {
                byte[] encrypted = ProtectedData.Protect(
                    Encoding.UTF8.GetBytes(plaintext),
                    null,
                    DataProtectionScope.CurrentUser
                );

                var file = new CookiesFile { Data = Convert.ToBase64String(encrypted) };

                Directory.CreateDirectory(Path.GetDirectoryName(CookiesPath)!);
                File.WriteAllText(CookiesPath, JsonSerializer.Serialize(file));

                return true;
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT, "Failed to restore the Roblox cookie file");
                App.Logger.WriteException(LOG_IDENT, ex);
                return false;
            }
        }

        /// <summary>
        /// Removes the cookie store, so the Roblox client opens logged out.
        /// </summary>
        public static void Clear()
        {
            try
            {
                if (File.Exists(CookiesPath))
                    File.Delete(CookiesPath);
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT, "Failed to remove the Roblox cookie file");
                App.Logger.WriteException(LOG_IDENT, ex);
            }
        }

        private static string CreateDefaultCookieList()
        {
            string guestData = Convert.ToBase64String(Guid.NewGuid().ToByteArray());
            string trackerData = Convert.ToBase64String(Guid.NewGuid().ToByteArray());

            return $"#HttpOnly_.roblox.com\tTRUE\t/\tFALSE\t0\tGuestData\t{guestData}\n" +
                   $"#HttpOnly_.roblox.com\tTRUE\t/\tFALSE\t0\tRBXEventTrackerV2\t{trackerData}";
        }
    }
}