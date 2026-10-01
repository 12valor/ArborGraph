using System.IO;
using System.Security.Cryptography;
using DiskScope.Models;

namespace DiskScope.Services;

public class DuplicateAnalyzer
{
    private readonly DatabaseService _dbService;

    public DuplicateAnalyzer(DatabaseService dbService)
    {
        _dbService = dbService;
    }

    public Task<List<DuplicateGroup>> FindDuplicatesAsync(
        long minSize = 1024,
        int maxCandidates = 500,
        IProgress<string>? statusProgress = null,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(async () =>
        {
            var confirmedDuplicates = new List<DuplicateGroup>();

            // Step 1: Find size collisions in SQLite
            statusProgress?.Report("Step 1/3: Querying size collisions from SQLite index...");
        var candidates = _dbService.GetDuplicateSizeCandidates(minCount: 2, minSize: minSize, limit: maxCandidates);

        if (candidates.Count == 0)
        {
            statusProgress?.Report("No size collisions found.");
            return confirmedDuplicates;
        }

        int processedCandidates = 0;

        foreach (var (size, _) in candidates)
        {
            if (cancellationToken.IsCancellationRequested) break;

            processedCandidates++;
            if (processedCandidates % 10 == 0 || processedCandidates == candidates.Count)
            {
                statusProgress?.Report($"Step 2/3: Checking hash candidates ({processedCandidates}/{candidates.Count})...");
            }

            var files = _dbService.GetFilesBySize(size);
            if (files.Count < 2) continue;

            // Step 2: Partial hash (first 4KB + last 4KB)
            var partialGroups = new Dictionary<string, List<FileRecord>>();

            foreach (var f in files)
            {
                if (cancellationToken.IsCancellationRequested) break;

                string? partialHash = await ComputePartialHashAsync(f.Path, f.Size);
                if (partialHash == null) continue; // File inaccessible

                if (!partialGroups.TryGetValue(partialHash, out var list))
                {
                    list = [];
                    partialGroups[partialHash] = list;
                }
                list.Add(f);
            }

            // Step 3: Full SHA-256 on matched partial candidates
            foreach (var (_, partialList) in partialGroups)
            {
                if (cancellationToken.IsCancellationRequested) break;
                if (partialList.Count < 2) continue;

                var fullGroups = new Dictionary<string, List<FileRecord>>();

                foreach (var f in partialList)
                {
                    if (cancellationToken.IsCancellationRequested) break;

                    string? fullHash = await ComputeFullSha256Async(f.Path);
                    if (fullHash == null) continue; // File inaccessible or read error

                    if (!fullGroups.TryGetValue(fullHash, out var list))
                    {
                        list = [];
                        fullGroups[fullHash] = list;
                    }
                    list.Add(f);
                }

                foreach (var (hash, verifiedList) in fullGroups)
                {
                    if (verifiedList.Count >= 2)
                    {
                        confirmedDuplicates.Add(new DuplicateGroup
                        {
                            ExactSize = size,
                            Sha256 = hash,
                            Files = new System.Collections.ObjectModel.ObservableCollection<FileRecord>(verifiedList)
                        });
                    }
                }
            }
        }

        // Sort by total wasted space descending
        confirmedDuplicates.Sort((a, b) => b.WastedBytes.CompareTo(a.WastedBytes));

        statusProgress?.Report($"Analysis complete. Found {confirmedDuplicates.Count} confirmed duplicate groups.");
        return confirmedDuplicates;
    });
}

    private static async Task<string?> ComputePartialHashAsync(string path, long size)
    {
        try
        {
            const int chunkSize = 4096;
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 4096, useAsync: true);

            using var md5 = MD5.Create(); // Fast hash for partial filtering stage

            if (size <= chunkSize * 2)
            {
                byte[] buffer = new byte[size];
                int read = await stream.ReadAsync(buffer.AsMemory(0, (int)size));
                byte[] hash = md5.ComputeHash(buffer, 0, read);
                return Convert.ToHexString(hash);
            }
            else
            {
                byte[] head = new byte[chunkSize];
                byte[] tail = new byte[chunkSize];

                int headRead = await stream.ReadAsync(head.AsMemory(0, chunkSize));

                stream.Seek(-chunkSize, SeekOrigin.End);
                int tailRead = await stream.ReadAsync(tail.AsMemory(0, chunkSize));

                byte[] combined = new byte[headRead + tailRead];
                Buffer.BlockCopy(head, 0, combined, 0, headRead);
                Buffer.BlockCopy(tail, 0, combined, headRead, tailRead);

                byte[] hash = md5.ComputeHash(combined);
                return Convert.ToHexString(hash);
            }
        }
        catch
        {
            return null;
        }
    }

    private static async Task<string?> ComputeFullSha256Async(string path)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 65536, useAsync: true);
            using var sha256 = SHA256.Create();
            byte[] hashBytes = await sha256.ComputeHashAsync(stream);
            return Convert.ToHexString(hashBytes);
        }
        catch
        {
            return null;
        }
    }
}
