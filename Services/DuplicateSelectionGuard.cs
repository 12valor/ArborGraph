using System.IO;
using DiskScope.Models;

namespace DiskScope.Services;

/// <summary>Checks that every affected group has an existing, unselected path.</summary>
public static class DuplicateSelectionGuard
{
    public static bool HasRetainedCopy(IEnumerable<DuplicateGroup> groups, IEnumerable<FileRecord> targets)
    {
        var targetPaths = new HashSet<string>(targets.Select(f => f.Path), StringComparer.OrdinalIgnoreCase);
        if (targetPaths.Count == 0) return false;

        var covered = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var group in groups)
        {
            var affected = group.Files.Where(f => targetPaths.Contains(f.Path)).ToList();
            if (affected.Count == 0) continue;
            foreach (var file in affected) covered.Add(file.Path);
            if (!group.Files.Any(f => !targetPaths.Contains(f.Path) && File.Exists(f.Path))) return false;
        }

        return covered.SetEquals(targetPaths);
    }
}
