using System.IO;
using System.Security.Cryptography;
using DiskScope.Infrastructure;
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
        int maxCandidates = 250,
        IProgress<string>? statusProgress = null,
        Action<DuplicateGroup>? onDuplicateFound = null,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(async () =>
        {
            var confirmedDuplicates = new List<DuplicateGroup>();

            statusProgress?.Report("Step 1/3: Querying size collisions from SQLite index...");
            var candidates = _dbService.GetDuplicateSizeCandidates(minCount: 2, minSize: minSize, limit: maxCandidates);

            if (candidates.Count == 0)
            {
                statusProgress?.Report("No size collisions found.");
                return confirmedDuplicates;
            }

            int processedCandidates = 0;
            int foundDuplicatesCount = 0;
            long foundWastedBytes = 0;
            var syncLock = new object();

            // Process candidates with bounded parallelism to maximize NVMe/SSD and multi-core throughput
            using var throttler = new SemaphoreSlim(4);

            var tasks = candidates.Select(async candidate =>
            {
                await throttler.WaitAsync(cancellationToken);
                try
                {
                    if (cancellationToken.IsCancellationRequested) return;

                    var (size, _) = candidate;
                    var files = _dbService.GetFilesBySize(size);
                    if (files.Count < 2) return;

                    // Step 2: Fast signature (head + tail, plus middle check for files > 1MB)
                    var partialGroups = new Dictionary<string, List<FileRecord>>(StringComparer.Ordinal);
                    foreach (var f in files)
                    {
                        if (cancellationToken.IsCancellationRequested) return;
                        if (!File.Exists(f.Path)) continue;

                        string? signature = await ComputeFastSignatureAsync(f.Path, f.Size, cancellationToken);
                        if (signature == null) continue;

                        if (!partialGroups.TryGetValue(signature, out var list))
                        {
                            list = [];
                            partialGroups[signature] = list;
                        }
                        list.Add(f);
                    }

                    // Step 3: Full streaming SHA-256 on verified partial candidates
                    foreach (var (_, partialList) in partialGroups)
                    {
                        if (cancellationToken.IsCancellationRequested) return;
                        if (partialList.Count < 2) continue;

                        var fullGroups = new Dictionary<string, List<FileRecord>>(StringComparer.Ordinal);
                        foreach (var f in partialList)
                        {
                            if (cancellationToken.IsCancellationRequested) return;

                            string? fullHash = await ComputeFullSha256Async(f.Path, f.Size, cancellationToken);
                            if (fullHash == null) continue;

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
                                var group = new DuplicateGroup
                                {
                                    ExactSize = size,
                                    Sha256 = hash,
                                    Files = new System.Collections.ObjectModel.ObservableCollection<FileRecord>(verifiedList)
                                };

                                lock (syncLock)
                                {
                                    confirmedDuplicates.Add(group);
                                    foundDuplicatesCount++;
                                    foundWastedBytes += group.WastedBytes;
                                }

                                onDuplicateFound?.Invoke(group);
                            }
                        }
                    }
                }
                finally
                {
                    int done = Interlocked.Increment(ref processedCandidates);
                    if (done % 5 == 0 || done == candidates.Count)
                    {
                        statusProgress?.Report($"Step 2/3: Checking hash candidates ({done}/{candidates.Count}) — found {foundDuplicatesCount} duplicate sets ({SizeFormatter.Format(foundWastedBytes)})...");
                    }
                    throttler.Release();
                }
            });

            await Task.WhenAll(tasks);
            cancellationToken.ThrowIfCancellationRequested();

            lock (syncLock)
            {
                confirmedDuplicates.Sort((a, b) => b.WastedBytes.CompareTo(a.WastedBytes));
            }

            statusProgress?.Report($"Analysis complete. Found {confirmedDuplicates.Count:N0} confirmed duplicate groups ({SizeFormatter.Format(confirmedDuplicates.Sum(g => g.WastedBytes))} recoverable space).");
            return confirmedDuplicates;
        }, cancellationToken);
    }

    private static async Task<string?> ComputeFastSignatureAsync(string path, long size, CancellationToken ct)
    {
        try
        {
            const int chunkSize = 4096;
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, chunkSize, useAsync: true);
            if (stream.Length != size) return null;
            using var md5 = MD5.Create();

            if (size <= chunkSize * 2)
            {
                byte[] buffer = new byte[size];
                int read = await stream.ReadAsync(buffer.AsMemory(0, (int)size), ct);
                byte[] hash = md5.ComputeHash(buffer, 0, read);
                return Convert.ToHexString(hash);
            }
            else if (size <= 1024 * 1024)
            {
                byte[] head = new byte[chunkSize];
                byte[] tail = new byte[chunkSize];

                int headRead = await stream.ReadAsync(head.AsMemory(0, chunkSize), ct);
                stream.Seek(-chunkSize, SeekOrigin.End);
                int tailRead = await stream.ReadAsync(tail.AsMemory(0, chunkSize), ct);

                byte[] combined = new byte[headRead + tailRead];
                Buffer.BlockCopy(head, 0, combined, 0, headRead);
                Buffer.BlockCopy(tail, 0, combined, headRead, tailRead);

                byte[] hash = md5.ComputeHash(combined);
                return Convert.ToHexString(hash);
            }
            else
            {
                // > 1MB: 3-point signature (head + middle + tail)
                byte[] head = new byte[chunkSize];
                byte[] mid = new byte[chunkSize];
                byte[] tail = new byte[chunkSize];

                int headRead = await stream.ReadAsync(head.AsMemory(0, chunkSize), ct);
                stream.Seek(size / 2, SeekOrigin.Begin);
                int midRead = await stream.ReadAsync(mid.AsMemory(0, chunkSize), ct);
                stream.Seek(-chunkSize, SeekOrigin.End);
                int tailRead = await stream.ReadAsync(tail.AsMemory(0, chunkSize), ct);

                byte[] combined = new byte[headRead + midRead + tailRead];
                Buffer.BlockCopy(head, 0, combined, 0, headRead);
                Buffer.BlockCopy(mid, 0, combined, headRead, midRead);
                Buffer.BlockCopy(tail, 0, combined, headRead + midRead, tailRead);

                byte[] hash = md5.ComputeHash(combined);
                return Convert.ToHexString(hash);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch
        {
            return null;
        }
    }

    private static async Task<string?> ComputeFullSha256Async(string path, long expectedSize, CancellationToken ct)
    {
        try
        {
            const int bufferSize = 131072; // 128 KB buffer
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize, useAsync: true);
            if (stream.Length != expectedSize) return null;
            using var sha256 = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            byte[] buffer = new byte[bufferSize];
            int bytesRead;
            long totalRead = 0;

            while ((bytesRead = await stream.ReadAsync(buffer.AsMemory(0, bufferSize), ct)) > 0)
            {
                sha256.AppendData(buffer.AsSpan(0, bytesRead));
                totalRead += bytesRead;
            }

            if (totalRead != expectedSize || stream.Length != expectedSize) return null;
            byte[] hashBytes = sha256.GetHashAndReset();
            return Convert.ToHexString(hashBytes);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch
        {
            return null;
        }
    }
}
