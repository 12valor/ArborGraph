using System.IO;
using DiskScope.Models;

namespace DiskScope.Services;

public class DiskService
{
    public List<DiskDriveInfo> GetSystemDrives()
    {
        var list = new List<DiskDriveInfo>();

        try
        {
            var drives = DriveInfo.GetDrives();
            foreach (var d in drives)
            {
                if (!d.IsReady) continue;

                try
                {
                    list.Add(new DiskDriveInfo
                    {
                        Name = d.Name,
                        VolumeLabel = d.VolumeLabel,
                        TotalBytes = d.TotalSize,
                        FreeBytes = d.AvailableFreeSpace,
                        FileSystem = d.DriveFormat,
                        IsSelected = d.Name.StartsWith("C:", StringComparison.OrdinalIgnoreCase)
                    });
                }
                catch
                {
                    // Drive not accessible or disconnected
                }
            }
        }
        catch
        {
            // Fallback: at least try C:\
            try
            {
                var c = new DriveInfo("C");
                if (c.IsReady)
                {
                    list.Add(new DiskDriveInfo
                    {
                        Name = c.Name,
                        VolumeLabel = c.VolumeLabel,
                        TotalBytes = c.TotalSize,
                        FreeBytes = c.AvailableFreeSpace,
                        FileSystem = c.DriveFormat,
                        IsSelected = true
                    });
                }
            }
            catch { }
        }

        return list;
    }
}
