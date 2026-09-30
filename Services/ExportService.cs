using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using DiskScope.Models;

namespace DiskScope.Services;

public class ExportService
{
    public async Task<AuditReport> BuildAuditReportAsync(
        DatabaseService dbService,
        JunkCleanerService? junkService = null,
        ScanStats? stats = null,
        string targetRoots = "",
        IEnumerable<DuplicateGroup>? duplicates = null)
    {
        return await Task.Run(async () =>
        {
            var (totalFiles, totalBytes) = dbService.GetTotalIndexedStorage();

            var report = new AuditReport
            {
                TargetRoots = string.IsNullOrWhiteSpace(targetRoots) ? "All Scanned Targets" : targetRoots,
                TotalFilesIndexed = totalFiles,
                TotalLogicalBytes = totalBytes,
                ScanDuration = stats?.FormattedElapsed ?? "N/A",
                FilesPerSecond = stats?.FilesPerSecond ?? 0
            };

            // Category breakdown
            var catDict = dbService.GetCategoryBreakdown();
            double totalDbl = Math.Max(1L, totalBytes);
            foreach (var (catName, (count, size)) in catDict.OrderByDescending(kv => kv.Value.TotalSize))
            {
                report.Categories.Add(new CategoryDistributionItem
                {
                    Category = catName,
                    FileCount = (int)count,
                    TotalBytes = size,
                    PercentageOfTotal = (double)size / totalDbl * 100.0
                });
            }

            // Top largest files
            report.TopLargestFiles = dbService.GetFilesPaged(0, 50, sortBy: "size", sortDesc: true);

            // Duplicate groups
            if (duplicates != null && duplicates.Any())
            {
                report.DuplicateGroups = duplicates.ToList();
                report.TotalDuplicateWastedBytes = report.DuplicateGroups.Sum(g => g.WastedBytes);
            }
            else
            {
                var dupOverview = dbService.GetDuplicateOverview();
                report.TotalDuplicateWastedBytes = dupOverview.PotentialWastedBytes;
            }

            // Junk targets
            if (junkService != null)
            {
                try
                {
                    var defaultTargets = junkService.GetDefaultTargets();
                    var junkScanTask = junkService.ScanAllAsync(defaultTargets);
                    if (await Task.WhenAny(junkScanTask, Task.Delay(1500)) == junkScanTask)
                    {
                        report.JunkTargets = defaultTargets.Where(t => t.SizeInBytes > 0).ToList();
                        report.TotalCleanableJunkBytes = report.JunkTargets.Sum(j => j.SizeInBytes);
                    }
                }
                catch
                {
                    // Non-fatal if junk scan fails
                }
            }

            return report;
        });
    }

    public async Task ExportToHtmlAsync(AuditReport report, string filePath)
    {
        var sb = new StringBuilder();

        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"en\">");
        sb.AppendLine("<head>");
        sb.AppendLine("    <meta charset=\"UTF-8\">");
        sb.AppendLine("    <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
        sb.AppendLine("    <title>DiskScope — Storage Audit Report</title>");
        sb.AppendLine("    <style>");
        sb.AppendLine(@"
        :root {
            --bg-canvas: #0B0F19;
            --bg-surface: #111827;
            --bg-surface-elevated: #1F2937;
            --border: #374151;
            --text-primary: #F9FAFB;
            --text-secondary: #9CA3AF;
            --text-muted: #6B7280;
            --accent: #3B82F6;
            --accent-purple: #8B5CF6;
            --success: #10B981;
            --warning: #F59E0B;
            --danger: #EF4444;
            --rose: #F43F5E;
        }

        * { box-sizing: border-box; margin: 0; padding: 0; }
        body {
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif;
            background-color: var(--bg-canvas);
            color: var(--text-primary);
            line-height: 1.5;
            padding: 32px 24px;
        }
        .container { max-width: 1200px; margin: 0 auto; }

        /* Header */
        .header {
            display: flex;
            justify-content: space-between;
            align-items: flex-start;
            padding-bottom: 24px;
            border-bottom: 1px solid var(--border);
            margin-bottom: 28px;
            flex-wrap: wrap;
            gap: 16px;
        }
        .brand { display: flex; align-items: center; gap: 12px; }
        .logo-badge {
            background: linear-gradient(135deg, #3B82F6, #8B5CF6);
            color: #fff;
            font-weight: 800;
            font-size: 14px;
            padding: 6px 12px;
            border-radius: 6px;
            letter-spacing: 0.5px;
        }
        .title h1 { font-size: 24px; font-weight: 700; color: var(--text-primary); }
        .title p { font-size: 13px; color: var(--text-secondary); margin-top: 2px; }

        .meta-badges { display: flex; gap: 8px; flex-wrap: wrap; }
        .badge {
            background: var(--bg-surface-elevated);
            border: 1px solid var(--border);
            padding: 6px 12px;
            border-radius: 6px;
            font-size: 12px;
            color: var(--text-secondary);
        }
        .badge strong { color: var(--text-primary); }

        /* KPI Cards */
        .kpi-grid {
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(240px, 1fr));
            gap: 16px;
            margin-bottom: 32px;
        }
        .kpi-card {
            background: var(--bg-surface);
            border: 1px solid var(--border);
            border-radius: 8px;
            padding: 20px;
            position: relative;
            overflow: hidden;
        }
        .kpi-card::before {
            content: '';
            position: absolute;
            top: 0; left: 0; right: 0; height: 3px;
            background: var(--accent);
        }
        .kpi-card.purple::before { background: var(--accent-purple); }
        .kpi-card.green::before { background: var(--success); }
        .kpi-card.amber::before { background: var(--warning); }

        .kpi-title { font-size: 12px; font-weight: 600; text-transform: uppercase; letter-spacing: 0.5px; color: var(--text-muted); }
        .kpi-value { font-size: 28px; font-weight: 700; color: var(--text-primary); margin: 6px 0 2px 0; }
        .kpi-desc { font-size: 12px; color: var(--text-secondary); }

        /* Section */
        .section {
            background: var(--bg-surface);
            border: 1px solid var(--border);
            border-radius: 8px;
            padding: 24px;
            margin-bottom: 28px;
        }
        .section-header {
            display: flex;
            justify-content: space-between;
            align-items: center;
            margin-bottom: 18px;
            padding-bottom: 12px;
            border-bottom: 1px solid var(--border);
        }
        .section-title { font-size: 16px; font-weight: 700; color: var(--text-primary); }
        .section-badge { font-size: 12px; color: var(--text-muted); }

        /* Tables */
        table { width: 100%; border-collapse: collapse; font-size: 13px; text-align: left; }
        th {
            background: var(--bg-surface-elevated);
            color: var(--text-muted);
            font-weight: 600;
            padding: 10px 14px;
            border-bottom: 1px solid var(--border);
            text-transform: uppercase;
            font-size: 11px;
            letter-spacing: 0.5px;
        }
        td {
            padding: 10px 14px;
            border-bottom: 1px solid var(--border);
            color: var(--text-primary);
        }
        tr:last-child td { border-bottom: none; }
        tr:hover td { background-color: rgba(255, 255, 255, 0.02); }

        .progress-bar-bg {
            background: var(--bg-surface-elevated);
            height: 6px;
            border-radius: 3px;
            width: 120px;
            overflow: hidden;
            display: inline-block;
            vertical-align: middle;
            margin-right: 8px;
        }
        .progress-bar-fill {
            height: 100%;
            background: var(--accent);
            border-radius: 3px;
        }

        .cat-tag {
            display: inline-block;
            padding: 2px 8px;
            border-radius: 4px;
            font-size: 11px;
            font-weight: 600;
            background: var(--bg-surface-elevated);
            border: 1px solid var(--border);
        }
        .cat-tag.Video { color: #A78BFA; }
        .cat-tag.Photoshop, .cat-tag.Images { color: #34D399; }
        .cat-tag.Executables { color: #FB7185; }
        .cat-tag.Archives { color: #FBBF24; }
        .cat-tag.Documents { color: #38BDF8; }
        .cat-tag.Code { color: #22D3EE; }
        .cat-tag.Audio { color: #F472B6; }

        .path-cell {
            font-family: Consolas, 'Courier New', monospace;
            font-size: 12px;
            color: var(--text-secondary);
            word-break: break-all;
        }

        .risk-safe { color: var(--success); font-weight: 600; }
        .risk-caution { color: var(--warning); font-weight: 600; }

        /* Footer */
        .footer {
            text-align: center;
            padding-top: 24px;
            color: var(--text-muted);
            font-size: 12px;
            border-top: 1px solid var(--border);
        }

        @media print {
            body { background: #fff; color: #000; padding: 0; }
            .kpi-card, .section { background: #fff; border: 1px solid #ccc; color: #000; }
            th { background: #eee; color: #333; }
            td { color: #111; }
            .path-cell { color: #444; }
            .logo-badge { background: #333; color: #fff; }
        }
        ");
        sb.AppendLine("    </style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");
        sb.AppendLine("<div class=\"container\">");

        // Header
        sb.AppendLine("    <div class=\"header\">");
        sb.AppendLine("        <div class=\"brand\">");
        sb.AppendLine("            <div class=\"logo-badge\">DISKSCOPE</div>");
        sb.AppendLine("            <div class=\"title\">");
        sb.AppendLine("                <h1>Storage Audit &amp; Optimization Report</h1>");
        sb.AppendLine($"                <p>Generated on {WebUtility.HtmlEncode(report.GeneratedAtUtc)}</p>");
        sb.AppendLine("            </div>");
        sb.AppendLine("        </div>");
        sb.AppendLine("        <div class=\"meta-badges\">");
        sb.AppendLine($"            <div class=\"badge\">Target: <strong>{WebUtility.HtmlEncode(report.TargetRoots)}</strong></div>");
        sb.AppendLine($"            <div class=\"badge\">Duration: <strong>{WebUtility.HtmlEncode(report.ScanDuration)}</strong></div>");
        if (report.FilesPerSecond > 0)
        {
            sb.AppendLine($"            <div class=\"badge\">Throughput: <strong>{report.FilesPerSecond:N0} files/sec</strong></div>");
        }
        sb.AppendLine("        </div>");
        sb.AppendLine("    </div>");

        // KPI Cards
        sb.AppendLine("    <div class=\"kpi-grid\">");
        sb.AppendLine("        <div class=\"kpi-card\">");
        sb.AppendLine("            <div class=\"kpi-title\">Total Indexed Volume</div>");
        sb.AppendLine($"            <div class=\"kpi-value\">{WebUtility.HtmlEncode(report.FormattedTotalSize)}</div>");
        sb.AppendLine($"            <div class=\"kpi-desc\">Across {report.TotalFilesIndexed:N0} total files</div>");
        sb.AppendLine("        </div>");

        sb.AppendLine("        <div class=\"kpi-card green\">");
        sb.AppendLine("            <div class=\"kpi-title\">Cleanable Junk &amp; Caches</div>");
        sb.AppendLine($"            <div class=\"kpi-value\">{WebUtility.HtmlEncode(report.FormattedCleanableJunk)}</div>");
        sb.AppendLine($"            <div class=\"kpi-desc\">{report.JunkTargets.Count:N0} discovered junk locations</div>");
        sb.AppendLine("        </div>");

        sb.AppendLine("        <div class=\"kpi-card amber\">");
        sb.AppendLine("            <div class=\"kpi-title\">Wasted Duplicate Storage</div>");
        sb.AppendLine($"            <div class=\"kpi-value\">{WebUtility.HtmlEncode(report.FormattedDuplicateWasted)}</div>");
        sb.AppendLine($"            <div class=\"kpi-desc\">Redundant copies occupying disk space</div>");
        sb.AppendLine("        </div>");

        sb.AppendLine("        <div class=\"kpi-card purple\">");
        sb.AppendLine("            <div class=\"kpi-title\">File Categories</div>");
        sb.AppendLine($"            <div class=\"kpi-value\">{report.Categories.Count} Types</div>");
        sb.AppendLine("            <div class=\"kpi-desc\">Grouped by file signatures</div>");
        sb.AppendLine("        </div>");
        sb.AppendLine("    </div>");

        // Category Breakdown
        sb.AppendLine("    <div class=\"section\">");
        sb.AppendLine("        <div class=\"section-header\">");
        sb.AppendLine("            <div class=\"section-title\">Storage Distribution by Category</div>");
        sb.AppendLine($"            <div class=\"section-badge\">{report.Categories.Count} categories mapped</div>");
        sb.AppendLine("        </div>");
        sb.AppendLine("        <table>");
        sb.AppendLine("            <thead>");
        sb.AppendLine("                <tr>");
        sb.AppendLine("                    <th>Category</th>");
        sb.AppendLine("                    <th style=\"text-align: right;\">File Count</th>");
        sb.AppendLine("                    <th style=\"text-align: right;\">Total Volume</th>");
        sb.AppendLine("                    <th style=\"text-align: right;\">Share of Disk</th>");
        sb.AppendLine("                </tr>");
        sb.AppendLine("            </thead>");
        sb.AppendLine("            <tbody>");

        foreach (var cat in report.Categories)
        {
            sb.AppendLine("                <tr>");
            sb.AppendLine($"                    <td><span class=\"cat-tag {WebUtility.HtmlEncode(cat.Category)}\">{WebUtility.HtmlEncode(cat.Category)}</span></td>");
            sb.AppendLine($"                    <td style=\"text-align: right;\">{cat.FileCount:N0}</td>");
            sb.AppendLine($"                    <td style=\"text-align: right;\"><strong>{WebUtility.HtmlEncode(cat.FormattedSize)}</strong></td>");
            sb.AppendLine("                    <td style=\"text-align: right;\">");
            sb.AppendLine($"                        <div class=\"progress-bar-bg\"><div class=\"progress-bar-fill\" style=\"width: {cat.PercentageOfTotal:0.#}%\"></div></div>");
            sb.AppendLine($"                        <span>{cat.PercentageOfTotal:0.1}%</span>");
            sb.AppendLine("                    </td>");
            sb.AppendLine("                </tr>");
        }

        sb.AppendLine("            </tbody>");
        sb.AppendLine("        </table>");
        sb.AppendLine("    </div>");

        // Top Largest Files
        if (report.TopLargestFiles.Count > 0)
        {
            sb.AppendLine("    <div class=\"section\">");
            sb.AppendLine("        <div class=\"section-header\">");
            sb.AppendLine("            <div class=\"section-title\">Top 50 Largest Files (Storage Hogs)</div>");
            sb.AppendLine($"            <div class=\"section-badge\">Showing top {report.TopLargestFiles.Count} files</div>");
            sb.AppendLine("        </div>");
            sb.AppendLine("        <table>");
            sb.AppendLine("            <thead>");
            sb.AppendLine("                <tr>");
            sb.AppendLine("                    <th style=\"width: 40px;\">#</th>");
            sb.AppendLine("                    <th>File Name</th>");
            sb.AppendLine("                    <th>Category</th>");
            sb.AppendLine("                    <th style=\"text-align: right;\">Size</th>");
            sb.AppendLine("                    <th>Full Path</th>");
            sb.AppendLine("                </tr>");
            sb.AppendLine("            </thead>");
            sb.AppendLine("            <tbody>");

            int rank = 1;
            foreach (var f in report.TopLargestFiles)
            {
                sb.AppendLine("                <tr>");
                sb.AppendLine($"                    <td style=\"color: var(--text-muted);\">{rank++}</td>");
                sb.AppendLine($"                    <td><strong>{WebUtility.HtmlEncode(f.Name)}</strong></td>");
                sb.AppendLine($"                    <td><span class=\"cat-tag {WebUtility.HtmlEncode(f.Category)}\">{WebUtility.HtmlEncode(f.Category)}</span></td>");
                sb.AppendLine($"                    <td style=\"text-align: right; color: var(--accent); font-weight: 600;\">{WebUtility.HtmlEncode(f.FormattedSize)}</td>");
                sb.AppendLine($"                    <td class=\"path-cell\">{WebUtility.HtmlEncode(f.Path)}</td>");
                sb.AppendLine("                </tr>");
            }

            sb.AppendLine("            </tbody>");
            sb.AppendLine("        </table>");
            sb.AppendLine("    </div>");
        }

        // Cleanable Junk Targets
        if (report.JunkTargets.Count > 0)
        {
            sb.AppendLine("    <div class=\"section\">");
            sb.AppendLine("        <div class=\"section-header\">");
            sb.AppendLine("            <div class=\"section-title\">Discovered Junk &amp; Developer Cache Targets</div>");
            sb.AppendLine($"            <div class=\"section-badge\">{report.JunkTargets.Count} items • {WebUtility.HtmlEncode(report.FormattedCleanableJunk)} reclaimable</div>");
            sb.AppendLine("        </div>");
            sb.AppendLine("        <table>");
            sb.AppendLine("            <thead>");
            sb.AppendLine("                <tr>");
            sb.AppendLine("                    <th>Category</th>");
            sb.AppendLine("                    <th>Target Name</th>");
            sb.AppendLine("                    <th>Items</th>");
            sb.AppendLine("                    <th style=\"text-align: right;\">Reclaimable</th>");
            sb.AppendLine("                    <th>Path / Directories</th>");
            sb.AppendLine("                </tr>");
            sb.AppendLine("            </thead>");
            sb.AppendLine("            <tbody>");

            foreach (var junk in report.JunkTargets)
            {
                string targetPath = string.Join("; ", junk.TargetDirectories);
                sb.AppendLine("                <tr>");
                sb.AppendLine($"                    <td>{WebUtility.HtmlEncode(junk.Category.ToString())}</td>");
                sb.AppendLine($"                    <td><strong>{WebUtility.HtmlEncode(junk.Name)}</strong></td>");
                sb.AppendLine($"                    <td>{junk.FileCount:N0} files</td>");
                sb.AppendLine($"                    <td style=\"text-align: right; color: var(--success); font-weight: 600;\">{WebUtility.HtmlEncode(junk.FormattedSize)}</td>");
                sb.AppendLine($"                    <td class=\"path-cell\">{WebUtility.HtmlEncode(targetPath)}</td>");
                sb.AppendLine("                </tr>");
            }

            sb.AppendLine("            </tbody>");
            sb.AppendLine("        </table>");
            sb.AppendLine("    </div>");
        }

        // Duplicate Groups
        if (report.DuplicateGroups.Count > 0)
        {
            sb.AppendLine("    <div class=\"section\">");
            sb.AppendLine("        <div class=\"section-header\">");
            sb.AppendLine("            <div class=\"section-title\">Identified Cryptographic Duplicate Groups</div>");
            sb.AppendLine($"            <div class=\"section-badge\">{report.DuplicateGroups.Count} groups • {WebUtility.HtmlEncode(report.FormattedDuplicateWasted)} wasted</div>");
            sb.AppendLine("        </div>");
            sb.AppendLine("        <table>");
            sb.AppendLine("            <thead>");
            sb.AppendLine("                <tr>");
            sb.AppendLine("                    <th>Unit Size</th>");
            sb.AppendLine("                    <th style=\"text-align: center;\">Copies</th>");
            sb.AppendLine("                    <th style=\"text-align: right;\">Wasted Space</th>");
            sb.AppendLine("                    <th>SHA-256 Prefix</th>");
            sb.AppendLine("                    <th>File Paths</th>");
            sb.AppendLine("                </tr>");
            sb.AppendLine("            </thead>");
            sb.AppendLine("            <tbody>");

            foreach (var dup in report.DuplicateGroups)
            {
                sb.AppendLine("                <tr>");
                sb.AppendLine($"                    <td><strong>{WebUtility.HtmlEncode(dup.FormattedSize)}</strong></td>");
                sb.AppendLine($"                    <td style=\"text-align: center;\"><span class=\"badge\">{dup.FileCount}</span></td>");
                sb.AppendLine($"                    <td style=\"text-align: right; color: var(--warning); font-weight: 600;\">{WebUtility.HtmlEncode(dup.FormattedWasted)}</td>");
                sb.AppendLine($"                    <td style=\"font-family: Consolas;\">{WebUtility.HtmlEncode(dup.ShortHash)}</td>");
                sb.AppendLine("                    <td class=\"path-cell\">");
                foreach (var f in dup.Files)
                {
                    sb.AppendLine($"                        <div>{WebUtility.HtmlEncode(f.Path)}</div>");
                }
                sb.AppendLine("                    </td>");
                sb.AppendLine("                </tr>");
            }

            sb.AppendLine("            </tbody>");
            sb.AppendLine("        </table>");
            sb.AppendLine("    </div>");
        }

        // Footer
        sb.AppendLine("    <div class=\"footer\">");
        sb.AppendLine("        <p>Generated by <strong>DiskScope</strong> • Standalone Windows Storage Analyzer &amp; Cleaner • Fully offline report</p>");
        sb.AppendLine("    </div>");

        sb.AppendLine("</div>");
        sb.AppendLine("</body>");
        sb.AppendLine("</html>");

        await File.WriteAllTextAsync(filePath, sb.ToString(), Encoding.UTF8);
    }

    public async Task ExportFilesToCsvAsync(IEnumerable<FileRecord> files, string filePath)
    {
        var sb = new StringBuilder();
        sb.AppendLine("\"Path\",\"Name\",\"Parent\",\"Size\",\"SizeFormatted\",\"Category\",\"Extension\",\"ModifiedTimeUtc\"");

        foreach (var f in files)
        {
            string modUtc = f.ModifiedTime > 0
                ? DateTimeOffset.FromUnixTimeSeconds((long)f.ModifiedTime).ToString("yyyy-MM-dd HH:mm:ss")
                : string.Empty;

            sb.Append(CsvEscape(f.Path)).Append(',');
            sb.Append(CsvEscape(f.Name)).Append(',');
            sb.Append(CsvEscape(f.Parent)).Append(',');
            sb.Append(f.Size).Append(',');
            sb.Append(CsvEscape(f.FormattedSize)).Append(',');
            sb.Append(CsvEscape(f.Category)).Append(',');
            sb.Append(CsvEscape(f.Extension)).Append(',');
            sb.AppendLine(CsvEscape(modUtc));
        }

        await File.WriteAllTextAsync(filePath, sb.ToString(), Encoding.UTF8);
    }

    public async Task ExportDuplicatesToCsvAsync(IEnumerable<DuplicateGroup> duplicates, string filePath)
    {
        var sb = new StringBuilder();
        sb.AppendLine("\"Size\",\"SizeFormatted\",\"FileCount\",\"WastedBytes\",\"WastedFormatted\",\"SHA256\",\"Paths\"");

        foreach (var d in duplicates)
        {
            string pathsJoined = string.Join(" | ", d.Files.Select(f => f.Path));
            sb.Append(d.ExactSize).Append(',');
            sb.Append(CsvEscape(d.FormattedSize)).Append(',');
            sb.Append(d.FileCount).Append(',');
            sb.Append(d.WastedBytes).Append(',');
            sb.Append(CsvEscape(d.FormattedWasted)).Append(',');
            sb.Append(CsvEscape(d.Sha256 ?? string.Empty)).Append(',');
            sb.AppendLine(CsvEscape(pathsJoined));
        }

        await File.WriteAllTextAsync(filePath, sb.ToString(), Encoding.UTF8);
    }

    public async Task ExportJunkToCsvAsync(IEnumerable<JunkTarget> targets, string filePath)
    {
        var sb = new StringBuilder();
        sb.AppendLine("\"Category\",\"Name\",\"Directories\",\"SizeBytes\",\"SizeFormatted\",\"FileCount\",\"Status\"");

        foreach (var j in targets)
        {
            string dirsJoined = string.Join(" | ", j.TargetDirectories);
            sb.Append(CsvEscape(j.Category.ToString())).Append(',');
            sb.Append(CsvEscape(j.Name)).Append(',');
            sb.Append(CsvEscape(dirsJoined)).Append(',');
            sb.Append(j.SizeInBytes).Append(',');
            sb.Append(CsvEscape(j.FormattedSize)).Append(',');
            sb.Append(j.FileCount).Append(',');
            sb.AppendLine(CsvEscape(j.Status));
        }

        await File.WriteAllTextAsync(filePath, sb.ToString(), Encoding.UTF8);
    }

    public async Task ExportToJsonAsync(AuditReport report, string filePath)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true
        };
        await using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
        await JsonSerializer.SerializeAsync(fs, report, options);
    }

    private static string CsvEscape(string? val)
    {
        if (string.IsNullOrEmpty(val)) return "\"\"";
        return "\"" + val.Replace("\"", "\"\"") + "\"";
    }
}
