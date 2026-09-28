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
                    "DiskScope Pro", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Unable to open file:\n\n{ex.Message}",
                "DiskScope Pro", MessageBoxButton.OK, MessageBoxImage.Error);
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
                        "DiskScope Pro", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Unable to reveal file in Windows Explorer:\n\n{ex.Message}",
                "DiskScope Pro", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    public void CopyPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;

        try
        {
            Clipboard.SetText(path);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not copy path to clipboard:\n\n{ex.Message}",
                "DiskScope Pro", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
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
                "DiskScope Pro", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
