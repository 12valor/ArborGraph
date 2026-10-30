using System.Runtime.InteropServices;
using DiskScope.Models;
using Microsoft.Win32.SafeHandles;

namespace DiskScope.Services;

internal static class DuplicateStorageAccounting
{
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFileW(string path, uint access, uint share, IntPtr security, uint creation, uint flags, IntPtr template);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandleEx(SafeFileHandle handle, int kind, out FileId info, uint size);
    [DllImport("kernel32.dll", EntryPoint = "GetFileInformationByHandleEx", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileStandardInfo(SafeFileHandle handle, int kind, out StandardInfo info, uint size);

    [StructLayout(LayoutKind.Sequential)]
    private struct FileId { public ulong Volume, Low, High; }
    [StructLayout(LayoutKind.Sequential)]
    private struct StandardInfo
    {
        public long AllocationSize, EndOfFile;
        public uint NumberOfLinks;
        public byte DeletePending, Directory;
    }

    public static long? Calculate(IEnumerable<FileRecord> files, long expectedSize)
    {
        try
        {
            var physical = new Dictionary<(ulong, ulong, ulong), (int Names, uint Links, long Bytes)>();
            foreach (var file in files)
            {
                if ((File.GetAttributes(file.Path) & FileAttributes.ReparsePoint) != 0) return null;
                string path = Path.GetFullPath(file.Path);
                if (!path.StartsWith(@"\\?\", StringComparison.Ordinal))
                    path = path.StartsWith(@"\\", StringComparison.Ordinal) ? @"\\?\UNC\" + path[2..] : @"\\?\" + path;
                using var handle = CreateFileW(path, 0, 7, IntPtr.Zero, 3, 0, IntPtr.Zero);
                if (handle.IsInvalid || !GetFileInformationByHandleEx(handle, 18, out var id, (uint)Marshal.SizeOf<FileId>()) ||
                    !GetFileStandardInfo(handle, 1, out var info, (uint)Marshal.SizeOf<StandardInfo>()) ||
                    info.Directory != 0 || info.EndOfFile != expectedSize || info.AllocationSize < 0 || info.NumberOfLinks == 0 || (id.Low == 0 && id.High == 0))
                    return null;
                var key = (id.Volume, id.Low, id.High);
                physical.TryGetValue(key, out var existing);
                if (existing.Names > 0 && (existing.Links != info.NumberOfLinks || existing.Bytes != info.AllocationSize)) return null;
                physical[key] = (existing.Names + 1, info.NumberOfLinks, info.AllocationSize);
            }
            if (physical.Count < 2) return 0;
            var removable = physical.Values.Where(item => item.Names >= item.Links).Select(item => item.Bytes).ToList();
            if (removable.Count == 0) return 0;
            return removable.Sum() - (removable.Count == physical.Count ? removable.Min() : 0);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException or OverflowException)
        {
            return null;
        }
    }
}
