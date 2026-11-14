namespace Bloxstrap.MultiInstance
{
    /// <summary>
    /// Lets several Roblox clients run at the same time.
    ///
    /// The client (0.74x) guards itself with the named mutex ROBLOX_singletonMutex and the
    /// named event ROBLOX_singletonEvent. A new client gives up when those names already
    /// exist, and since 0.746 a launch can also be handed to a background tray process.
    ///
    /// Before every launch we therefore close the singleton handles inside the running
    /// clients (which removes the names from the kernel) and end background tray processes,
    /// so the next client starts as if it were the only one. Already running clients keep
    /// their session.
    /// </summary>
    internal static class MultiInstanceManager
    {
        private const string LOG_IDENT = "MultiInstanceManager";

        public const string SingletonMutexName = "ROBLOX_singletonMutex";
        public const string SingletonEventName = "ROBLOX_singletonEvent";

        private static readonly string[] SingletonNames = { SingletonEventName, SingletonMutexName };

        /// <summary>Time a freshly started client gets to create its window before we judge it.</summary>
        private static readonly TimeSpan StartingGrace = TimeSpan.FromSeconds(4);

        /// <summary>A client without a window this old is a background tray process.</summary>
        private static readonly TimeSpan TrayAge = TimeSpan.FromSeconds(10);

        /// <summary>
        /// A deeplink launch, as opposed to starting the client without arguments, which makes an
        /// already running client "resume" instead of a new one starting.
        /// </summary>
        public static string BuildHomeLaunchUrl(string? authenticationTicket = null)
        {
            long launchTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            int browserTrackerId = Random.Shared.Next(10_000_000, 1_500_000_000);

            string url = "roblox-player:1" +
                         "+launchmode:app";

            if (!String.IsNullOrEmpty(authenticationTicket))
                url += $"+gameinfo:{authenticationTicket}";

            return url +
                   $"+launchtime:{launchTime}" +
                   $"+browsertrackerid:{browserTrackerId}" +
                   "+robloxLocale:en_us" +
                   "+gameLocale:en_us" +
                   "+channel:" +
                   "+LaunchExp:InApp";
        }

        /// <summary>
        /// Clears the way for another Roblox client. Call right before starting the executable.
        /// </summary>
        public static void PrepareForLaunch()
        {
            try
            {
                List<Process> clients = CollectClients();

                if (clients.Any(x => !HasWindow(x) && Age(x) < StartingGrace))
                {
                    Thread.Sleep(StartingGrace);
                    clients = CollectClients();
                }

                var runningClients = new List<int>();

                foreach (Process process in clients)
                {
                    if (!HasWindow(process) && Age(process) >= TrayAge)
                    {
                        try
                        {
                            App.Logger.WriteLine(LOG_IDENT, $"Closing Roblox tray process {process.Id}");
                            process.Kill();
                            process.WaitForExit(3000);
                        }
                        catch (Exception ex)
                        {
                            App.Logger.WriteLine(LOG_IDENT, $"Failed to close the tray process {process.Id}");
                            App.Logger.WriteException(LOG_IDENT, ex);
                        }

                        continue;
                    }

                    runningClients.Add(process.Id);
                }

                if (runningClients.Count > 0)
                    NamedObjectCloser.Close(runningClients, SingletonNames);
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT, "Failed to prepare for a multi-instance launch");
                App.Logger.WriteException(LOG_IDENT, ex);
            }
        }

        private static List<Process> CollectClients() => Process.GetProcessesByName(App.RobloxPlayerAppName).ToList();

        private static bool HasWindow(Process process)
        {
            try
            {
                return !process.HasExited && process.MainWindowHandle != IntPtr.Zero;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static TimeSpan Age(Process process)
        {
            try
            {
                return DateTime.Now - process.StartTime;
            }
            catch (Exception)
            {
                return TimeSpan.MaxValue;
            }
        }
    }
}