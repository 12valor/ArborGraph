using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;

namespace DiskScope.Services;

public class FileActionService
{
    [DllImport("shell32.dll", CharSet = CharSet.Auto)]
    private static extern bool ShellExecuteEx(ref SHELLEXECUTEINFO lpExecInfo);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct SHELLEXECUTEINFO
    {
        public int cbSize;
        public uint fMask;
        public IntPtr hwnd;
        [MarshalAs(UnmanagedType.LPTStr)]
        public string lpVerb;
        [MarshalAs(UnmanagedType.LPTStr)]
        public string lpFile;
        [MarshalAs(UnmanagedType.LPTStr)]
        public string? lpParameters;
        [MarshalAs(UnmanagedType.LPTStr)]
        public string? lpDirectory;
        public int nShow;
        public IntPtr hInstApp;
        public IntPtr lpIDList;
        [MarshalAs(UnmanagedType.LPTStr)]
        public string? lpClass;
        public IntPtr hkeyClass;
        public uint dwHotKey;
        public IntPtr hIcon;
        public IntPtr hProcess;
    }

    private const uint SEE_MASK_INVOKEIDLIST = 0x0000000C;
    private const int SW_SHOW = 5;

    public void OpenFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;

        try
        {
            if (File.Exists(path) || Directory.Exists(path))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true
                });
            }
            else
            {
                MessageBox.Show($"The file or directory does not exist on disk:\n\n{path}",
                    "ArborGraph", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Unable to open file:\n\n{ex.Message}",
                "ArborGraph", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    public void OpenFileLocation(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;

        try
        {
            if (File.Exists(path))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"/select,\"{path}\"",
                    UseShellExecute = false
                });
            }
            else if (Directory.Exists(path))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"\"{path}\"",
                    UseShellExecute = false
                });
            }
            else
            {
                string? parent = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(parent) && Directory.Exists(parent))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        Arguments = $"\"{parent}\"",
                        UseShellExecute = false
                    });
                }
                else
                {
                    MessageBox.Show($"File or parent directory does not exist:\n\n{path}",
                        "ArborGraph", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Unable to reveal file in Windows Explorer:\n\n{ex.Message}",
                "ArborGraph", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    public void CopyPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;

        // Clipboard operations on Windows can intermittently fail if locked by another app
        for (int attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                Clipboard.SetDataObject(path, true);
                return;
            }
            catch (COMException)
            {
                System.Threading.Thread.Sleep(50);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not copy path to clipboard:\n\n{ex.Message}",
                    "ArborGraph", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
        }
    }

    public bool IsProtectedPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return true;

        try
        {
            string trimmed = path.Trim().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (trimmed.Length <= 2 && trimmed.EndsWith(":")) return true;

            string fullPath = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string? root = Path.GetPathRoot(fullPath)?.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            // Never delete drive roots (e.g. C: or C:\)
            if (string.Equals(fullPath, root, StringComparison.OrdinalIgnoreCase)) return true;

            // Never delete Windows directory or its direct subdirectories
            string winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows).TrimEnd(Path.DirectorySeparatorChar);
            if (fullPath.StartsWith(winDir, StringComparison.OrdinalIgnoreCase)) return true;

            // Never delete Program Files or Program Files (x86) roots
            string progFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles).TrimEnd(Path.DirectorySeparatorChar);
            if (string.Equals(fullPath, progFiles, StringComparison.OrdinalIgnoreCase)) return true;

            string progFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86).TrimEnd(Path.DirectorySeparatorChar);
            if (!string.IsNullOrEmpty(progFilesX86) && string.Equals(fullPath, progFilesX86, StringComparison.OrdinalIgnoreCase)) return true;

            // Never delete User Profile root (e.g. C:\Users\Username)
            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile).TrimEnd(Path.DirectorySeparatorChar);
            if (string.Equals(fullPath, userProfile, StringComparison.OrdinalIgnoreCase)) return true;

            return false;
        }
        catch
        {
            return true;
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct SHFILEOPSTRUCT
    {
        public IntPtr hwnd;
        public uint wFunc;
        [MarshalAs(UnmanagedType.LPTStr)]
        public string pFrom;
        [MarshalAs(UnmanagedType.LPTStr)]
        public string? pTo;
        public ushort fFlags;
        public bool fAnyOperationsAborted;
        public IntPtr hNameMappings;
        [MarshalAs(UnmanagedType.LPTStr)]
        public string? lpszProgressTitle;
    }

    private const uint FO_DELETE = 0x0003;
    private const ushort FOF_ALLOWUNDO = 0x0040;
    private const ushort FOF_NOCONFIRMATION = 0x0010;
    private const ushort FOF_SILENT = 0x0004;
    private const ushort FOF_NOERRORUI = 0x0400;

    [DllImport("shell32.dll", CharSet = CharSet.Auto)]
    private static extern int SHFileOperation(ref SHFILEOPSTRUCT FileOp);

    public bool MoveToRecycleBin(string path, out string? errorMessage)
    {
        errorMessage = null;
        if (string.IsNullOrWhiteSpace(path))
        {
            errorMessage = "Path cannot be empty.";
            return false;
        }

        if (IsProtectedPath(path))
        {
            errorMessage = $"Protected system or root path cannot be deleted:\n{path}";
            MessageBox.Show(errorMessage, "Safety Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        bool isFile = File.Exists(path);
        bool isDir = Directory.Exists(path);
        if (!isFile && !isDir)
        {
            errorMessage = $"Target does not exist on disk:\n{path}";
            return false;
        }

        try
        {
            // Primary method: Microsoft.VisualBasic.FileIO (official Windows Desktop API for Recycle Bin)
            if (isFile)
            {
                Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(
                    path,
                    Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
                    Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
            }
            else
            {
                Microsoft.VisualBasic.FileIO.FileSystem.DeleteDirectory(
                    path,
                    Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
                    Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
            }
            return true;
        }
        catch (Exception ex)
        {
            // Fallback: Native SHFileOperation with FOF_ALLOWUNDO
            try
            {
                var fileOp = new SHFILEOPSTRUCT
                {
                    wFunc = FO_DELETE,
                    pFrom = path + '\0' + '\0',
                    fFlags = FOF_ALLOWUNDO | FOF_NOCONFIRMATION | FOF_SILENT | FOF_NOERRORUI
                };

                int result = SHFileOperation(ref fileOp);
                if (result == 0 && !fileOp.fAnyOperationsAborted)
                {
                    return true;
                }

                errorMessage = $"Windows Recycle Bin operation failed (error code {result}): {ex.Message}";
                return false;
            }
            catch (Exception fallbackEx)
            {
                errorMessage = $"Unable to move to Recycle Bin: {fallbackEx.Message}";
                return false;
            }
        }
    }

    public bool DeletePermanently(string path, out string? errorMessage, bool skipConfirmation = false)
    {
        errorMessage = null;
        if (string.IsNullOrWhiteSpace(path))
        {
            errorMessage = "Path cannot be empty.";
            return false;
        }

        if (IsProtectedPath(path))
        {
            errorMessage = $"Protected system or root path cannot be permanently deleted:\n{path}";
            if (!skipConfirmation)
            {
                MessageBox.Show(errorMessage, "Safety Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            return false;
        }

        bool isFile = File.Exists(path);
        bool isDir = Directory.Exists(path);
        if (!isFile && !isDir)
        {
            errorMessage = $"Target does not exist on disk:\n{path}";
            return false;
        }

        if (!skipConfirmation)
        {
            var res = MessageBox.Show(
                $"Are you sure you want to PERMANENTLY delete this {(isFile ? "file" : "directory")}?\n\n{path}\n\nWARNING: This cannot be undone.",
                "Confirm Permanent Deletion",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (res != MessageBoxResult.Yes)
            {
                return false;
            }
        }

        try
        {
            if (isFile)
            {
                var fi = new FileInfo(path);
                if (fi.IsReadOnly) fi.IsReadOnly = false;
                fi.Delete();
            }
            else
            {
                Directory.Delete(path, recursive: true);
            }
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = $"Permanent deletion failed: {ex.Message}";
            return false;
        }
    }

    public (int Succeeded, int Failed) DeleteFilesBatch(
        IReadOnlyList<string> paths,
        bool permanent,
        IProgress<(int Completed, int Total, string CurrentItem)>? progress = null,
        CancellationToken ct = default)
    {
        int succeeded = 0;
        int failed = 0;
        int total = paths.Count;

        for (int i = 0; i < total; i++)
        {
            if (ct.IsCancellationRequested) break;

            string path = paths[i];
            progress?.Report((i + 1, total, path));

            if (IsProtectedPath(path))
            {
                failed++;
                continue;
            }

            try
            {
                bool ok = permanent
                    ? DeletePermanently(path, out _, skipConfirmation: true)
                    : MoveToRecycleBin(path, out _);

                if (ok) succeeded++; else failed++;
            }
            catch
            {
                failed++;
            }
        }

        return (succeeded, failed);
    }

    public async Task<(int Succeeded, int Failed)> DeleteFilesBatchAsync(
        IReadOnlyList<string> paths,
        bool permanent,
        IProgress<(int Completed, int Total, string CurrentItem)>? progress = null,
        CancellationToken ct = default)
    {
        return await Task.Run(() => DeleteFilesBatch(paths, permanent, progress, ct), ct);
    }

    public void ShowProperties(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;

        try
        {
            var info = new SHELLEXECUTEINFO
            {
                cbSize = Marshal.SizeOf<SHELLEXECUTEINFO>(),
                lpVerb = "properties",
                lpFile = path,
                nShow = SW_SHOW,
                fMask = SEE_MASK_INVOKEIDLIST
            };

            ShellExecuteEx(ref info);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Unable to open file properties:\n\n{ex.Message}",
                "ArborGraph", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
