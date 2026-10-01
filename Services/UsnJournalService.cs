using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace DiskScope.Services;

public class UsnJournalState
{
    public bool IsAvailable { get; set; }
    public string Volume { get; set; } = string.Empty;
    public ulong JournalId { get; set; }
    public long LowestValidUsn { get; set; }
    public long NextUsn { get; set; }
    public string StatusMessage { get; set; } = string.Empty;
    public bool RequiresElevation { get; set; }
}

public enum UsnChangeType
{
    Modified,
    Created,
    Deleted,
    Renamed
}

public class UsnChangeRecord
{
    public string FileName { get; set; } = string.Empty;
    public ulong FileReferenceNumber { get; set; }
    public ulong ParentFileReferenceNumber { get; set; }
    public long Usn { get; set; }
    public UsnChangeType ChangeType { get; set; }
    public uint Reason { get; set; }
}

public class UsnReadResult
{
    public bool Success { get; set; }
    public string Reason { get; set; } = string.Empty;
    public long NewNextUsn { get; set; }
    public List<UsnChangeRecord> Changes { get; } = [];
}

public class UsnJournalService
{
    private const uint GENERIC_READ = 0x80000000;
    private const uint GENERIC_WRITE = 0x40000000;
    private const uint FILE_SHARE_READ = 0x00000001;
    private const uint FILE_SHARE_WRITE = 0x00000002;
    private const uint OPEN_EXISTING = 3;
    private const uint FILE_FLAG_BACKUP_SEMANTICS = 0x02000000;

    private const uint FSCTL_QUERY_USN_JOURNAL = 0x000900f4;
    private const uint FSCTL_READ_USN_JOURNAL = 0x000900bb;

    // USN Reason flags
    private const uint USN_REASON_DATA_OVERWRITE = 0x00000001;
    private const uint USN_REASON_DATA_EXTEND = 0x00000002;
    private const uint USN_REASON_DATA_TRUNCATION = 0x00000004;
    private const uint USN_REASON_FILE_CREATE = 0x00000100;
    private const uint USN_REASON_FILE_DELETE = 0x00000200;
    private const uint USN_REASON_RENAME_NEW_NAME = 0x00002000;
    private const uint USN_REASON_CLOSE = 0x80000000;

    [StructLayout(LayoutKind.Sequential)]
    private struct USN_JOURNAL_DATA_V0
    {
        public ulong UsnJournalID;
        public long LowestValidUsn;
        public long NextUsn;
        public ulong MaximumSize;
        public ulong AllocationDelta;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct READ_USN_JOURNAL_DATA_V0
    {
        public long StartUsn;
        public uint ReasonMask;
        public uint ReturnOnlyOnClose;
        public ulong Timeout;
        public ulong BytesToWaitFor;
        public ulong UsnJournalID;
    }

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern SafeFileHandle CreateFile(
        string lpFileName,
        uint dwDesiredAccess,
        uint dwShareMode,
        IntPtr lpSecurityAttributes,
        uint dwCreationDisposition,
        uint dwFlagsAndAttributes,
        IntPtr hTemplateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool DeviceIoControl(
        SafeFileHandle hDevice,
        uint dwIoControlCode,
        IntPtr lpInBuffer,
        uint nInBufferSize,
        IntPtr lpOutBuffer,
        uint nOutBufferSize,
        out uint lpBytesReturned,
        IntPtr lpOverlapped);

    /// <summary>
    /// Checks whether the drive volume is formatted as NTFS.
    /// Non-NTFS drives (FAT32, exFAT, ReFS, network) do not support the USN Journal.
    /// </summary>
    public bool IsNtfsVolume(string rootPath)
    {
        try
        {
            string driveRoot = Path.GetPathRoot(Path.GetFullPath(rootPath)) ?? string.Empty;
            if (string.IsNullOrEmpty(driveRoot)) return false;

            var driveInfo = new DriveInfo(driveRoot);
            return string.Equals(driveInfo.DriveFormat, "NTFS", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Queries the current USN Journal state of an NTFS drive volume.
    /// Handles elevation check, non-NTFS drives, and inactive journal states.
    /// </summary>
    public UsnJournalState QueryJournalState(string rootPath)
    {
        var state = new UsnJournalState();

        string driveRoot = Path.GetPathRoot(Path.GetFullPath(rootPath)) ?? string.Empty;
        if (string.IsNullOrEmpty(driveRoot))
        {
            state.StatusMessage = "Invalid drive root path.";
            return state;
        }

        string driveLetter = driveRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        state.Volume = driveLetter;

        if (!IsNtfsVolume(driveRoot))
        {
            state.StatusMessage = $"Volume {driveLetter} is not NTFS. Incremental USN scanning is only supported on NTFS filesystems.";
            return state;
        }

        string volumeDevicePath = $@"\\.\{driveLetter}";
        using var hVolume = CreateFile(
            volumeDevicePath,
            GENERIC_READ | GENERIC_WRITE,
            FILE_SHARE_READ | FILE_SHARE_WRITE,
            IntPtr.Zero,
            OPEN_EXISTING,
            FILE_FLAG_BACKUP_SEMANTICS,
            IntPtr.Zero);

        if (hVolume.IsInvalid)
        {
            int err = Marshal.GetLastWin32Error();
            if (err == 5) // ERROR_ACCESS_DENIED
            {
                state.RequiresElevation = true;
                state.StatusMessage = "Querying NTFS USN Change Journal requires Administrator privileges. Running in Standard Scan Mode.";
            }
            else
            {
                state.StatusMessage = $"Unable to open volume handle for {driveLetter} (Error {err}). Full scan will be used.";
            }
            return state;
        }

        int size = Marshal.SizeOf<USN_JOURNAL_DATA_V0>();
        IntPtr outBuf = Marshal.AllocHGlobal(size);
        try
        {
            bool ok = DeviceIoControl(
                hVolume,
                FSCTL_QUERY_USN_JOURNAL,
                IntPtr.Zero,
                0,
                outBuf,
                (uint)size,
                out uint bytesReturned,
                IntPtr.Zero);

            if (ok && bytesReturned >= (uint)size)
            {
                var data = Marshal.PtrToStructure<USN_JOURNAL_DATA_V0>(outBuf);
                state.IsAvailable = true;
                state.JournalId = data.UsnJournalID;
                state.LowestValidUsn = data.LowestValidUsn;
                state.NextUsn = data.NextUsn;
                state.StatusMessage = $"USN Change Journal active (JournalID: {data.UsnJournalID:X16}, NextUsn: {data.NextUsn}).";
            }
            else
            {
                int err = Marshal.GetLastWin32Error();
                state.StatusMessage = $"FSCTL_QUERY_USN_JOURNAL failed (Error {err}). Full scan will be used.";
            }
        }
        finally
        {
            Marshal.FreeHGlobal(outBuf);
        }

        return state;
    }

    /// <summary>
    /// Reads USN Journal change records from a stored checkpoint to current state.
    /// If journal reset, journal ID change, or records purged, signals fallback to full scan.
    /// </summary>
    public UsnReadResult ReadChanges(string rootPath, ulong expectedJournalId, long startUsn)
    {
        var result = new UsnReadResult();

        // 1. Verify volume state
        var currentState = QueryJournalState(rootPath);
        if (!currentState.IsAvailable)
        {
            result.Success = false;
            result.Reason = $"USN Journal unavailable ({currentState.StatusMessage}). Fallback to full scan.";
            return result;
        }

        // 2. Validate Journal ID matching
        if (currentState.JournalId != expectedJournalId)
        {
            result.Success = false;
            result.Reason = $"USN Journal was recreated or ID changed ({expectedJournalId:X16} -> {currentState.JournalId:X16}). Fallback to full scan.";
            return result;
        }

        // 3. Check for truncation / purge
        if (startUsn < currentState.LowestValidUsn)
        {
            result.Success = false;
            result.Reason = $"USN Journal was truncated since last scan (StartUSN: {startUsn} < LowestValid: {currentState.LowestValidUsn}). Fallback to full scan.";
            return result;
        }

        // If no changes occurred
        if (startUsn >= currentState.NextUsn)
        {
            result.Success = true;
            result.NewNextUsn = currentState.NextUsn;
            result.Reason = "No filesystem changes recorded in USN Journal since last scan.";
            return result;
        }

        string driveRoot = Path.GetPathRoot(Path.GetFullPath(rootPath)) ?? string.Empty;
        string driveLetter = driveRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string volumeDevicePath = $@"\\.\{driveLetter}";

        using var hVolume = CreateFile(
            volumeDevicePath,
            GENERIC_READ | GENERIC_WRITE,
            FILE_SHARE_READ | FILE_SHARE_WRITE,
            IntPtr.Zero,
            OPEN_EXISTING,
            FILE_FLAG_BACKUP_SEMANTICS,
            IntPtr.Zero);

        if (hVolume.IsInvalid)
        {
            result.Success = false;
            result.Reason = "Access denied opening volume device handle. Fallback to full scan.";
            return result;
        }

        const int bufferSize = 64 * 1024; // 64 KB read buffer
        IntPtr inBuf = IntPtr.Zero;
        IntPtr outBuf = IntPtr.Zero;

        try
        {
            inBuf = Marshal.AllocHGlobal(Marshal.SizeOf<READ_USN_JOURNAL_DATA_V0>());
            outBuf = Marshal.AllocHGlobal(bufferSize);

            long currentUsn = startUsn;

            while (currentUsn < currentState.NextUsn)
            {
                var readData = new READ_USN_JOURNAL_DATA_V0
                {
                    StartUsn = currentUsn,
                    ReasonMask = 0xFFFFFFFF, // all reasons
                    ReturnOnlyOnClose = 0,
                    Timeout = 0,
                    BytesToWaitFor = 0,
                    UsnJournalID = expectedJournalId
                };

                Marshal.StructureToPtr(readData, inBuf, false);

                bool ok = DeviceIoControl(
                    hVolume,
                    FSCTL_READ_USN_JOURNAL,
                    inBuf,
                    (uint)Marshal.SizeOf<READ_USN_JOURNAL_DATA_V0>(),
                    outBuf,
                    bufferSize,
                    out uint bytesReturned,
                    IntPtr.Zero);

                if (!ok || bytesReturned < sizeof(long))
                {
                    break;
                }

                // First 8 bytes of outBuf is the next USN
                long nextUsnMarker = Marshal.ReadInt64(outBuf);
                long offset = sizeof(long);

                while (offset < bytesReturned)
                {
                    IntPtr recordPtr = IntPtr.Add(outBuf, (int)offset);
                    uint recordLength = (uint)Marshal.ReadInt32(recordPtr);
                    if (recordLength == 0) break;

                    // USN_RECORD_V2 has major version = 2
                    ushort majorVersion = (ushort)Marshal.ReadInt16(recordPtr, 4);
                    if (majorVersion == 2)
                    {
                        ulong fileRef = (ulong)Marshal.ReadInt64(recordPtr, 8);
                        ulong parentRef = (ulong)Marshal.ReadInt64(recordPtr, 16);
                        long usn = Marshal.ReadInt64(recordPtr, 24);
                        uint reason = (uint)Marshal.ReadInt32(recordPtr, 40);
                        ushort fileNameLength = (ushort)Marshal.ReadInt16(recordPtr, 56);
                        ushort fileNameOffset = (ushort)Marshal.ReadInt16(recordPtr, 58);

                        string fileName = Marshal.PtrToStringUni(IntPtr.Add(recordPtr, fileNameOffset), fileNameLength / 2);

                        var changeType = UsnChangeType.Modified;
                        if ((reason & USN_REASON_FILE_CREATE) != 0) changeType = UsnChangeType.Created;
                        else if ((reason & USN_REASON_FILE_DELETE) != 0) changeType = UsnChangeType.Deleted;
                        else if ((reason & USN_REASON_RENAME_NEW_NAME) != 0) changeType = UsnChangeType.Renamed;

                        result.Changes.Add(new UsnChangeRecord
                        {
                            FileName = fileName,
                            FileReferenceNumber = fileRef,
                            ParentFileReferenceNumber = parentRef,
                            Usn = usn,
                            ChangeType = changeType,
                            Reason = reason
                        });
                    }

                    offset += recordLength;
                }

                if (nextUsnMarker <= currentUsn) break;
                currentUsn = nextUsnMarker;
            }

            result.Success = true;
            result.NewNextUsn = currentState.NextUsn;
            result.Reason = $"Successfully read {result.Changes.Count} filesystem changes from USN Journal.";
            return result;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Reason = $"Error reading USN Journal: {ex.Message}. Fallback to full scan.";
            return result;
        }
        finally
        {
            if (inBuf != IntPtr.Zero) Marshal.FreeHGlobal(inBuf);
            if (outBuf != IntPtr.Zero) Marshal.FreeHGlobal(outBuf);
        }
    }
}
