using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace DiskScope.Services;

internal static class DirectoryIdentity
{
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFileW(string name, uint access, uint share, IntPtr security, uint creation, uint flags, IntPtr template);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandle(SafeFileHandle handle, out FileInformation info);

    [StructLayout(LayoutKind.Sequential)]
    private struct FileInformation
    {
        public uint Attributes, CreationLow, CreationHigh, AccessLow, AccessHigh, WriteLow, WriteHigh;
        public uint Volume, SizeHigh, SizeLow, LinkCount, IndexHigh, IndexLow;
    }

    public static (uint Volume, ulong Index)? TryGet(string path)
    {
        string fullPath = Path.GetFullPath(path);
        if (!fullPath.StartsWith(@"\\?\", StringComparison.Ordinal))
            fullPath = fullPath.StartsWith(@"\\", StringComparison.Ordinal)
                ? @"\\?\UNC\" + fullPath[2..]
                : @"\\?\" + fullPath;
        using var handle = CreateFileW(fullPath, 0, 7, IntPtr.Zero, 3, 0x02000000, IntPtr.Zero);
        if (handle.IsInvalid || !GetFileInformationByHandle(handle, out var info)) return null;
        ulong index = ((ulong)info.IndexHigh << 32) | info.IndexLow;
        return index == 0 ? null : (info.Volume, index);
    }
}
