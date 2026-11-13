using System.Runtime.InteropServices;

namespace Bloxstrap.MultiInstance
{
    /// <summary>
    /// Closes handles to named kernel objects inside other processes of the current user.
    /// When the last handle to a named object is closed, Windows removes the name, so the
    /// next process that creates an object with that name gets a brand new one.
    /// </summary>
    internal static class NamedObjectCloser
    {
        private const string LOG_IDENT = "NamedObjectCloser";

        private const int SystemExtendedHandleInformation = 64;
        private const int ObjectNameInformation = 1;
        private const int ObjectTypeInformation = 2;

        private const int StatusInfoLengthMismatch = unchecked((int)0xC0000004);
        private const int StatusBufferOverflow = unchecked((int)0x80000005);
        private const int StatusBufferTooSmall = unchecked((int)0xC0000023);

        private const uint ProcessDuplicateHandle = 0x0040;
        private const uint DuplicateCloseSource = 0x00000001;
        private const uint DuplicateSameAccess = 0x00000002;

        private const int MaxSnapshotBytes = 512 * 1024 * 1024;

        private static readonly int EntrySize = Marshal.SizeOf<HandleEntry>();
        private static readonly int EntriesOffset = IntPtr.Size * 2;

        /// <summary>
        /// Closes every handle in the given processes whose object is an Event or Mutex named
        /// exactly one of <paramref name="objectNames"/>. Returns the number of closed handles.
        /// </summary>
        public static int Close(IReadOnlyCollection<int> processIds, IReadOnlyCollection<string> objectNames)
        {
            if (processIds.Count == 0 || objectNames.Count == 0)
                return 0;

            var wanted = new HashSet<string>(objectNames, StringComparer.OrdinalIgnoreCase);
            var targets = new HashSet<int>(processIds);
            Dictionary<int, List<IntPtr>> handlesByProcess = SnapshotHandles(targets);

            int closed = 0;

            foreach (int pid in targets)
            {
                IntPtr process = OpenProcess(ProcessDuplicateHandle, false, pid);

                if (process == IntPtr.Zero)
                    continue;

                try
                {
                    if (!handlesByProcess.TryGetValue(pid, out List<IntPtr>? handles))
                        continue;

                    foreach (IntPtr handle in handles)
                    {
                        if (CloseIfNamed(process, handle, wanted))
                            closed++;
                    }
                }
                finally
                {
                    CloseHandle(process);
                }
            }

            App.Logger.WriteLine(LOG_IDENT, $"Closed {closed} singleton handle(s)");

            return closed;
        }

        private static bool CloseIfNamed(IntPtr process, IntPtr remoteHandle, HashSet<string> wanted)
        {
            IntPtr self = GetCurrentProcess();

            if (!DuplicateHandle(process, remoteHandle, self, out IntPtr local, 0, false, DuplicateSameAccess))
                return false;

            bool matches;

            try
            {
                string? type = QueryUnicodeString(local, ObjectTypeInformation);

                if (!String.Equals(type, "Event", StringComparison.Ordinal) &&
                    !String.Equals(type, "Mutant", StringComparison.Ordinal))
                    return false;

                // Only query names for object types that can't block
                string? name = QueryUnicodeString(local, ObjectNameInformation);

                matches = name is not null && wanted.Contains(LastSegment(name));
            }
            finally
            {
                CloseHandle(local);
            }

            if (!matches)
                return false;

            if (!DuplicateHandle(process, remoteHandle, self, out IntPtr closedCopy, 0, false, DuplicateCloseSource))
                return false;

            if (closedCopy != IntPtr.Zero)
                CloseHandle(closedCopy);

            return true;
        }

        private static string LastSegment(string kernelPath)
        {
            int slash = kernelPath.LastIndexOf('\\');
            return slash >= 0 ? kernelPath[(slash + 1)..] : kernelPath;
        }

        private static Dictionary<int, List<IntPtr>> SnapshotHandles(HashSet<int> targets)
        {
            var byProcess = new Dictionary<int, List<IntPtr>>();
            int size = 1024 * 1024;

            while (true)
            {
                IntPtr buffer = Marshal.AllocHGlobal(size);

                try
                {
                    int status = NtQuerySystemInformation(SystemExtendedHandleInformation, buffer, size, out int needed);

                    if (status == StatusInfoLengthMismatch || status == StatusBufferOverflow || status == StatusBufferTooSmall)
                    {
                        int next = Math.Max(needed + 256 * 1024, size * 2);

                        if (next > MaxSnapshotBytes)
                            throw new InvalidOperationException("The system handle table is too large to inspect");

                        size = next;
                        continue;
                    }

                    if (status < 0)
                        throw new InvalidOperationException($"Could not read the system handle table (NTSTATUS 0x{status:X8})");

                    long count = Marshal.ReadIntPtr(buffer).ToInt64();
                    long available = (size - EntriesOffset) / EntrySize;

                    if (count > available)
                        count = available;

                    for (long i = 0; i < count; i++)
                    {
                        IntPtr entryPointer = IntPtr.Add(buffer, checked(EntriesOffset + (int)(i * EntrySize)));
                        HandleEntry entry = Marshal.PtrToStructure<HandleEntry>(entryPointer);

                        long pid = entry.UniqueProcessId.ToInt64();

                        if (pid <= 0 || pid > int.MaxValue || !targets.Contains((int)pid))
                            continue;

                        if (!byProcess.TryGetValue((int)pid, out List<IntPtr>? list))
                            byProcess[(int)pid] = list = new List<IntPtr>();

                        list.Add(entry.HandleValue);
                    }

                    return byProcess;
                }
                finally
                {
                    Marshal.FreeHGlobal(buffer);
                }
            }
        }

        private static string? QueryUnicodeString(IntPtr handle, int infoClass)
        {
            int size = 1024;

            for (int attempt = 0; attempt < 4; attempt++)
            {
                IntPtr buffer = Marshal.AllocHGlobal(size);

                try
                {
                    int status = NtQueryObject(handle, infoClass, buffer, size, out int needed);

                    if (status == StatusInfoLengthMismatch || status == StatusBufferOverflow || status == StatusBufferTooSmall)
                    {
                        size = Math.Max(needed, size * 2);
                        continue;
                    }

                    if (status < 0)
                        return null;

                    // Both OBJECT_NAME_INFORMATION and OBJECT_TYPE_INFORMATION start with a UNICODE_STRING
                    UnicodeString text = Marshal.PtrToStructure<UnicodeString>(buffer);

                    if (text.Buffer == IntPtr.Zero || text.Length == 0)
                        return String.Empty;

                    return Marshal.PtrToStringUni(text.Buffer, text.Length / 2);
                }
                finally
                {
                    Marshal.FreeHGlobal(buffer);
                }
            }

            return null;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct HandleEntry
        {
            public IntPtr Object;
            public IntPtr UniqueProcessId;
            public IntPtr HandleValue;
            public uint GrantedAccess;
            public ushort CreatorBackTraceIndex;
            public ushort ObjectTypeIndex;
            public uint HandleAttributes;
            public uint Reserved;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct UnicodeString
        {
            public ushort Length;
            public ushort MaximumLength;
            public IntPtr Buffer;
        }

        [DllImport("ntdll.dll")]
        private static extern int NtQuerySystemInformation(int systemInformationClass, IntPtr systemInformation, int systemInformationLength, out int returnLength);

        [DllImport("ntdll.dll")]
        private static extern int NtQueryObject(IntPtr handle, int objectInformationClass, IntPtr objectInformation, int objectInformationLength, out int returnLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(uint desiredAccess, bool inheritHandle, int processId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool DuplicateHandle(IntPtr sourceProcess, IntPtr sourceHandle, IntPtr targetProcess, out IntPtr targetHandle, uint desiredAccess, bool inheritHandle, uint options);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetCurrentProcess();

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr handle);
    }
}