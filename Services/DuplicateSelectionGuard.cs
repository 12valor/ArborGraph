using System.IO;
using System.Security.Cryptography;
using DiskScope.Models;

namespace DiskScope.Services;

/// <summary>Checks that every affected group has an existing, unselected path.</summary>
public static class DuplicateSelectionGuard
{
    public static bool HasVerifiedCopy(IEnumerable<DuplicateGroup> groups, IEnumerable<FileRecord> targets, FileRecord target)
    {
        var targetPaths = new HashSet<string>(targets.Select(f => f.Path), StringComparer.OrdinalIgnoreCase);
        var group = groups.FirstOrDefault(g => g.Files.Any(f => string.Equals(f.Path, target.Path, StringComparison.OrdinalIgnoreCase)));
        if (group == null || !targetPaths.Contains(target.Path) || !MatchesGroup(target.Path, group)) return false;
        return group.Files.Any(file => !targetPaths.Contains(file.Path) && MatchesGroup(file.Path, group));
    }

    private static bool MatchesGroup(string path, DuplicateGroup group)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (stream.Length != group.ExactSize) return false;
            string hash = Convert.ToHexString(SHA256.HashData(stream));
            return stream.Length == group.ExactSize && string.Equals(hash, group.Sha256, StringComparison.OrdinalIgnoreCase);
        }
        catch (IOException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
        catch (ArgumentException) { return false; }
        catch (NotSupportedException) { return false; }
    }

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
