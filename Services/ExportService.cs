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
        sb.AppendLine("    <title>ArborGraph — Storage Audit Report</title>");
        sb.AppendLine("    <style>");
        sb.AppendLine(@"
        :root {
            --bg: #0C0F14;
            --surface: #12161F;
            --surface-elevated: #181E2A;
            --border: #222B3D;
            --border-strong: #2F3B52;
            --text-main: #F1F5F9;
            --text-secondary: #94A3B8;
            --text-muted: #64748B;
            --accent: #E2E8F0;
            --mono: ui-monospace, 'Cascadia Code', 'JetBrains Mono', 'SF Mono', Consolas, monospace;
            --sans: -apple-system, BlinkMacSystemFont, 'Segoe UI', system-ui, sans-serif;
        }

        * { box-sizing: border-box; margin: 0; padding: 0; }
        body {
            font-family: var(--sans);
            background-color: var(--bg);
            color: var(--text-main);
            line-height: 1.5;
            padding: 36px 28px;
            -webkit-font-smoothing: antialiased;
        }
        .container { max-width: 1240px; margin: 0 auto; }

        /* Report Meta Header */
        .report-header {
            display: flex;
            justify-content: space-between;
            align-items: flex-start;
            padding-bottom: 24px;
            border-bottom: 1px solid var(--border);
            margin-bottom: 28px;
            flex-wrap: wrap;
            gap: 20px;
        }
        .report-title-group { display: flex; flex-direction: column; gap: 4px; }
        .system-tag {
            font-family: var(--mono);
            font-size: 11px;
            letter-spacing: 1.2px;
            text-transform: uppercase;
            color: var(--text-muted);
            font-weight: 600;
        }
        .report-title {
            font-size: 22px;
            font-weight: 700;
            letter-spacing: -0.3px;
            color: var(--text-main);
        }
        .report-meta-timestamp {
            font-size: 12px;
            color: var(--text-secondary);
            font-family: var(--mono);
        }

        .meta-strip {
            display: flex;
            gap: 10px;
            flex-wrap: wrap;
        }
        .meta-cell {
            background: var(--surface);
            border: 1px solid var(--border);
            padding: 8px 14px;
            border-radius: 3px;
            font-size: 12px;
            font-family: var(--mono);
        }
        .meta-label {
            color: var(--text-muted);
            font-size: 10px;
            text-transform: uppercase;
            letter-spacing: 0.5px;
            margin-bottom: 2px;
        }
        .meta-value {
            color: var(--text-main);
            font-weight: 600;
        }

        /* Metric Summary Grid */
        .summary-grid {
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(240px, 1fr));
            gap: 14px;
            margin-bottom: 32px;
        }
        .summary-card {
            background: var(--surface);
            border: 1px solid var(--border);
            border-radius: 3px;
            padding: 20px 22px;
            display: flex;
            flex-direction: column;
            justify-content: space-between;
        }
        .summary-card-title {
            font-size: 11px;
            font-family: var(--mono);
            font-weight: 600;
            text-transform: uppercase;
            letter-spacing: 0.8px;
            color: var(--text-muted);
        }
        .summary-card-value {
            font-size: 28px;
            font-weight: 700;
            font-family: var(--mono);
            color: var(--text-main);
            margin: 8px 0 3px 0;
            letter-spacing: -0.5px;
        }
        .summary-card-desc {
            font-size: 12px;
            color: var(--text-secondary);
        }

        /* Audit Sections */
        .audit-section {
            background: var(--surface);
            border: 1px solid var(--border);
            border-radius: 3px;
            padding: 22px 24px;
            margin-bottom: 26px;
        }
        .section-headline {
            display: flex;
            justify-content: space-between;
            align-items: baseline;
            margin-bottom: 18px;
            padding-bottom: 12px;
            border-bottom: 1px solid var(--border);
        }
        .section-title {
            font-size: 13.5px;
            font-weight: 700;
            font-family: var(--mono);
            text-transform: uppercase;
            letter-spacing: 0.8px;
            color: var(--text-main);
        }
        .section-count {
            font-size: 12px;
            font-family: var(--mono);
            color: var(--text-muted);
        }

        /* Tables */
        table {
            width: 100%;
            border-collapse: collapse;
            font-size: 12.5px;
            text-align: left;
        }
        th {
            background: var(--surface-elevated);
            color: var(--text-muted);
            font-family: var(--mono);
            font-weight: 600;
            padding: 9px 12px;
            border-bottom: 1px solid var(--border);
            text-transform: uppercase;
            font-size: 10px;
            letter-spacing: 0.7px;
        }
        td {
            padding: 10px 12px;
            border-bottom: 1px solid var(--border);
            color: var(--text-main);
            vertical-align: middle;
        }
        tr:last-child td { border-bottom: none; }
        tr:hover td { background-color: rgba(255, 255, 255, 0.015); }

        .mono-num {
            font-family: var(--mono);
            font-variant-numeric: tabular-nums;
        }
        .bold-num {
            font-family: var(--mono);
            font-weight: 600;
            color: var(--text-main);
        }

        .distribution-bar {
            display: flex;
            height: 10px;
            width: 100%;
            border-radius: 3px;
            overflow: hidden;
            background: var(--surface-elevated);
            border: 1px solid var(--border);
            margin-bottom: 20px;
            gap: 1px;
        }
        .dist-segment {
            height: 100%;
            min-width: 2px;
            transition: opacity 0.15s ease;
        }
        .dist-segment:hover {
            opacity: 0.85;
        }

        .ratio-bar-track {
            background: var(--surface-elevated);
            height: 4px;
            border-radius: 1px;
            width: 90px;
            overflow: hidden;
            display: inline-block;
            vertical-align: middle;
            margin-right: 8px;
        }
        .ratio-bar-fill {
            height: 100%;
            border-radius: 1px;
        }

        .cat-tag {
            display: inline-flex;
            align-items: center;
            gap: 6px;
            padding: 2px 8px;
            border-radius: 3px;
            font-size: 11px;
            font-family: var(--mono);
            font-weight: 500;
            line-height: 1.4;
            letter-spacing: 0.02em;
            background: var(--surface-elevated);
            border: 1px solid var(--border);
            color: var(--text-secondary);
            white-space: nowrap;
        }
        .cat-tag-dot {
            width: 6px;
            height: 6px;
            border-radius: 50%;
            display: inline-block;
            flex-shrink: 0;
            background: currentColor;
        }

        .cat-tag.cat-video { background: rgba(167, 139, 250, 0.12); color: #C4B5FD; border-color: rgba(167, 139, 250, 0.35); }
        .cat-tag.cat-video .cat-tag-dot { background: #A78BFA; }

        .cat-tag.cat-audio { background: rgba(244, 114, 182, 0.12); color: #F9A8D4; border-color: rgba(244, 114, 182, 0.35); }
        .cat-tag.cat-audio .cat-tag-dot { background: #F472B6; }

        .cat-tag.cat-images { background: rgba(52, 211, 153, 0.12); color: #6EE7B7; border-color: rgba(52, 211, 153, 0.35); }
        .cat-tag.cat-images .cat-tag-dot { background: #34D399; }

        .cat-tag.cat-photoshop { background: rgba(56, 189, 248, 0.12); color: #7DD3FC; border-color: rgba(56, 189, 248, 0.35); }
        .cat-tag.cat-photoshop .cat-tag-dot { background: #38BDF8; }

        .cat-tag.cat-archives { background: rgba(251, 191, 36, 0.12); color: #FCD34D; border-color: rgba(251, 191, 36, 0.35); }
        .cat-tag.cat-archives .cat-tag-dot { background: #FBBF24; }

        .cat-tag.cat-executables { background: rgba(251, 113, 133, 0.12); color: #FDA4AF; border-color: rgba(251, 113, 133, 0.35); }
        .cat-tag.cat-executables .cat-tag-dot { background: #FB7185; }

        .cat-tag.cat-code { background: rgba(45, 212, 191, 0.12); color: #5EEAD4; border-color: rgba(45, 212, 191, 0.35); }
        .cat-tag.cat-code .cat-tag-dot { background: #2DD4BF; }

        .cat-tag.cat-documents { background: rgba(96, 165, 250, 0.12); color: #93C5FD; border-color: rgba(96, 165, 250, 0.35); }
        .cat-tag.cat-documents .cat-tag-dot { background: #60A5FA; }

        .cat-tag.cat-database { background: rgba(129, 140, 248, 0.12); color: #A5B4FC; border-color: rgba(129, 140, 248, 0.35); }
        .cat-tag.cat-database .cat-tag-dot { background: #818CF8; }

        .cat-tag.cat-browser { background: rgba(245, 158, 11, 0.12); color: #FCD34D; border-color: rgba(245, 158, 11, 0.35); }
        .cat-tag.cat-browser .cat-tag-dot { background: #F59E0B; }

        .cat-tag.cat-developer { background: rgba(45, 212, 191, 0.12); color: #5EEAD4; border-color: rgba(45, 212, 191, 0.35); }
        .cat-tag.cat-developer .cat-tag-dot { background: #2DD4BF; }

        .cat-tag.cat-system { background: rgba(56, 189, 248, 0.12); color: #7DD3FC; border-color: rgba(56, 189, 248, 0.35); }
        .cat-tag.cat-system .cat-tag-dot { background: #38BDF8; }

        .cat-tag.cat-design { background: rgba(192, 132, 252, 0.12); color: #E9D5FF; border-color: rgba(192, 132, 252, 0.35); }
        .cat-tag.cat-design .cat-tag-dot { background: #C084FC; }

        .cat-tag.cat-application { background: rgba(96, 165, 250, 0.12); color: #93C5FD; border-color: rgba(96, 165, 250, 0.35); }
        .cat-tag.cat-application .cat-tag-dot { background: #60A5FA; }

        .cat-tag.cat-other { background: rgba(148, 163, 184, 0.12); color: #CBD5E1; border-color: rgba(148, 163, 184, 0.25); }
        .cat-tag.cat-other .cat-tag-dot { background: #94A3B8; }

        .path-cell {
            font-family: var(--mono);
            font-size: 11.5px;
            color: var(--text-secondary);
            word-break: break-all;
        }

        .count-badge {
            display: inline-block;
            font-family: var(--mono);
            font-size: 11px;
            padding: 1px 6px;
            background: var(--surface-elevated);
            border: 1px solid var(--border);
            border-radius: 2px;
            color: var(--text-secondary);
        }

        /* Footer */
        .report-footer {
            display: flex;
            justify-content: space-between;
            align-items: center;
            padding-top: 24px;
            color: var(--text-muted);
            font-size: 11px;
            font-family: var(--mono);
            border-top: 1px solid var(--border);
            flex-wrap: wrap;
            gap: 12px;
        }

        @media print {
            body { background: #FFFFFF; color: #0F172A; padding: 0; }
            .summary-card, .audit-section, .meta-cell { background: #FFFFFF; border: 1px solid #CBD5E1; color: #0F172A; }
            th { background: #F1F5F9; color: #475569; border-bottom: 1px solid #CBD5E1; }
            td { color: #0F172A; border-bottom: 1px solid #E2E8F0; }
            .path-cell { color: #334155; }
            .report-header, .report-footer { border-color: #CBD5E1; }
            .ratio-bar-track { background: #E2E8F0; }
            .distribution-bar { background: #E2E8F0; border-color: #CBD5E1; }
            .cat-tag { background: #F8FAFC !important; border-color: #CBD5E1 !important; color: #0F172A !important; }
        }
        ");
        sb.AppendLine("    </style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");
        sb.AppendLine("<div class=\"container\">");

        // Header
        sb.AppendLine("    <div class=\"report-header\">");
        sb.AppendLine("        <div class=\"report-title-group\">");
        sb.AppendLine("            <span class=\"system-tag\">ArborGraph &bull; Storage Telemetry Audit</span>");
        sb.AppendLine("            <h1 class=\"report-title\">Filesystem Storage Audit Report</h1>");
        sb.AppendLine($"            <span class=\"report-meta-timestamp\">Report generated: {WebUtility.HtmlEncode(report.GeneratedAtUtc)}</span>");
        sb.AppendLine("        </div>");
        sb.AppendLine("        <div class=\"meta-strip\">");
        sb.AppendLine("            <div class=\"meta-cell\">");
        sb.AppendLine("                <div class=\"meta-label\">Target Volume</div>");
        sb.AppendLine($"                <div class=\"meta-value\">{WebUtility.HtmlEncode(report.TargetRoots)}</div>");
        sb.AppendLine("            </div>");
        sb.AppendLine("            <div class=\"meta-cell\">");
        sb.AppendLine("                <div class=\"meta-label\">Scan Duration</div>");
        sb.AppendLine($"                <div class=\"meta-value\">{WebUtility.HtmlEncode(report.ScanDuration)}</div>");
        sb.AppendLine("            </div>");
        if (report.FilesPerSecond > 0)
        {
            sb.AppendLine("            <div class=\"meta-cell\">");
            sb.AppendLine("                <div class=\"meta-label\">Throughput</div>");
            sb.AppendLine($"                <div class=\"meta-value\">{report.FilesPerSecond:N0} files/sec</div>");
            sb.AppendLine("            </div>");
        }
        sb.AppendLine("        </div>");
        sb.AppendLine("    </div>");

        // Metric Summary Grid
        sb.AppendLine("    <div class=\"summary-grid\">");
        sb.AppendLine("        <div class=\"summary-card\">");
        sb.AppendLine("            <div class=\"summary-card-title\">Total Indexed Volume</div>");
        sb.AppendLine($"            <div class=\"summary-card-value\">{WebUtility.HtmlEncode(report.FormattedTotalSize)}</div>");
        sb.AppendLine($"            <div class=\"summary-card-desc\">{report.TotalFilesIndexed:N0} total files indexed</div>");
        sb.AppendLine("        </div>");

        sb.AppendLine("        <div class=\"summary-card\">");
        sb.AppendLine("            <div class=\"summary-card-title\">Disposable Caches</div>");
        sb.AppendLine($"            <div class=\"summary-card-value\">{WebUtility.HtmlEncode(report.FormattedCleanableJunk)}</div>");
        sb.AppendLine($"            <div class=\"summary-card-desc\">{report.JunkTargets.Count:N0} identified cache locations</div>");
        sb.AppendLine("        </div>");

        sb.AppendLine("        <div class=\"summary-card\">");
        sb.AppendLine("            <div class=\"summary-card-title\">Duplicate Redundancy</div>");
        sb.AppendLine($"            <div class=\"summary-card-value\">{WebUtility.HtmlEncode(report.FormattedDuplicateWasted)}</div>");
        sb.AppendLine("            <div class=\"summary-card-desc\">Redundant duplicate copy storage</div>");
        sb.AppendLine("        </div>");

        sb.AppendLine("        <div class=\"summary-card\">");
        sb.AppendLine("            <div class=\"summary-card-title\">File Categories</div>");
        sb.AppendLine($"            <div class=\"summary-card-value\">{report.Categories.Count} Types</div>");
        sb.AppendLine("            <div class=\"summary-card-desc\">Classified by file extension</div>");
        sb.AppendLine("        </div>");
        sb.AppendLine("    </div>");

        // Category Breakdown
        sb.AppendLine("    <div class=\"audit-section\">");
        sb.AppendLine("        <div class=\"section-headline\">");
        sb.AppendLine("            <div class=\"section-title\">01 // Storage by File Category</div>");
        sb.AppendLine($"            <div class=\"section-count\">{report.Categories.Count} categories mapped</div>");
        sb.AppendLine("        </div>");

        if (report.Categories.Count > 0)
        {
            sb.AppendLine("        <div class=\"distribution-bar\">");
            foreach (var cat in report.Categories)
            {
                if (cat.PercentageOfTotal > 0.05)
                {
                    string color = GetCategoryColor(cat.Category);
                    sb.AppendLine($"            <div class=\"dist-segment\" style=\"width: {cat.PercentageOfTotal:0.0}%; background: {color};\" title=\"{WebUtility.HtmlEncode(cat.Category)}: {cat.PercentageOfTotal:0.0}% ({WebUtility.HtmlEncode(cat.FormattedSize)})\"></div>");
                }
            }
            sb.AppendLine("        </div>");
        }

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
            string slug = GetCategorySlug(cat.Category);
            string color = GetCategoryColor(cat.Category);
            sb.AppendLine("                <tr>");
            sb.AppendLine($"                    <td><span class=\"cat-tag cat-{slug}\"><span class=\"cat-tag-dot\"></span>{WebUtility.HtmlEncode(cat.Category)}</span></td>");
            sb.AppendLine($"                    <td class=\"mono-num\" style=\"text-align: right;\">{cat.FileCount:N0}</td>");
            sb.AppendLine($"                    <td class=\"bold-num\" style=\"text-align: right;\">{WebUtility.HtmlEncode(cat.FormattedSize)}</td>");
            sb.AppendLine("                    <td class=\"mono-num\" style=\"text-align: right;\">");
            sb.AppendLine($"                        <div class=\"ratio-bar-track\"><div class=\"ratio-bar-fill\" style=\"width: {cat.PercentageOfTotal:0.#}%; background: {color};\"></div></div>");
            sb.AppendLine($"                        <span>{cat.PercentageOfTotal:0.0}%</span>");
            sb.AppendLine("                    </td>");
            sb.AppendLine("                </tr>");
        }

        sb.AppendLine("            </tbody>");
        sb.AppendLine("        </table>");
        sb.AppendLine("    </div>");

        // Top Largest Files
        if (report.TopLargestFiles.Count > 0)
        {
            sb.AppendLine("    <div class=\"audit-section\">");
            sb.AppendLine("        <div class=\"section-headline\">");
            sb.AppendLine("            <div class=\"section-title\">02 // Largest Files by Volume</div>");
            sb.AppendLine($"            <div class=\"section-count\">Top {report.TopLargestFiles.Count} files</div>");
            sb.AppendLine("        </div>");
            sb.AppendLine("        <table>");
            sb.AppendLine("            <thead>");
            sb.AppendLine("                <tr>");
            sb.AppendLine("                    <th style=\"width: 44px;\">#</th>");
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
                string slug = GetCategorySlug(f.Category);
                sb.AppendLine("                <tr>");
                sb.AppendLine($"                    <td class=\"mono-num\" style=\"color: var(--text-muted);\">{rank++}</td>");
                sb.AppendLine($"                    <td><strong>{WebUtility.HtmlEncode(f.Name)}</strong></td>");
                sb.AppendLine($"                    <td><span class=\"cat-tag cat-{slug}\"><span class=\"cat-tag-dot\"></span>{WebUtility.HtmlEncode(f.Category)}</span></td>");
                sb.AppendLine($"                    <td class=\"bold-num\" style=\"text-align: right;\">{WebUtility.HtmlEncode(f.FormattedSize)}</td>");
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
            sb.AppendLine("    <div class=\"audit-section\">");
            sb.AppendLine("        <div class=\"section-headline\">");
            sb.AppendLine("            <div class=\"section-title\">03 // Reclaimable Cache &amp; Temporary Directories</div>");
            sb.AppendLine($"            <div class=\"section-count\">{report.JunkTargets.Count} targets &bull; {WebUtility.HtmlEncode(report.FormattedCleanableJunk)} reclaimable</div>");
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
                string slug = GetCategorySlug(junk.Category.ToString());
                sb.AppendLine("                <tr>");
                sb.AppendLine($"                    <td><span class=\"cat-tag cat-{slug}\"><span class=\"cat-tag-dot\"></span>{WebUtility.HtmlEncode(junk.Category.ToString())}</span></td>");
                sb.AppendLine($"                    <td><strong>{WebUtility.HtmlEncode(junk.Name)}</strong></td>");
                sb.AppendLine($"                    <td class=\"mono-num\">{junk.FileCount:N0} files</td>");
                sb.AppendLine($"                    <td class=\"bold-num\" style=\"text-align: right;\">{WebUtility.HtmlEncode(junk.FormattedSize)}</td>");
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
            sb.AppendLine("    <div class=\"audit-section\">");
            sb.AppendLine("        <div class=\"section-headline\">");
            sb.AppendLine("            <div class=\"section-title\">04 // Verified Duplicate Files</div>");
            sb.AppendLine($"            <div class=\"section-count\">{report.DuplicateGroups.Count} groups &bull; {WebUtility.HtmlEncode(report.FormattedDuplicateWasted)} redundant</div>");
            sb.AppendLine("        </div>");
            sb.AppendLine("        <table>");
            sb.AppendLine("            <thead>");
            sb.AppendLine("                <tr>");
            sb.AppendLine("                    <th>Unit Size</th>");
            sb.AppendLine("                    <th style=\"text-align: center;\">Copies</th>");
            sb.AppendLine("                    <th style=\"text-align: right;\">Redundant Space</th>");
            sb.AppendLine("                    <th>SHA-256 Hash Prefix</th>");
            sb.AppendLine("                    <th>File Paths</th>");
            sb.AppendLine("                </tr>");
            sb.AppendLine("            </thead>");
            sb.AppendLine("            <tbody>");

            foreach (var dup in report.DuplicateGroups)
            {
                sb.AppendLine("                <tr>");
                sb.AppendLine($"                    <td class=\"bold-num\">{WebUtility.HtmlEncode(dup.FormattedSize)}</td>");
                sb.AppendLine($"                    <td style=\"text-align: center;\"><span class=\"count-badge\">{dup.FileCount}</span></td>");
                sb.AppendLine($"                    <td class=\"bold-num\" style=\"text-align: right;\">{WebUtility.HtmlEncode(dup.FormattedWasted)}</td>");
                sb.AppendLine($"                    <td class=\"mono-num\" style=\"color: var(--text-muted);\">{WebUtility.HtmlEncode(dup.ShortHash)}</td>");
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
        sb.AppendLine("    <div class=\"report-footer\">");
        sb.AppendLine("        <div>ArborGraph Storage Engine &bull; Deterministic Filesystem Telemetry</div>");
        sb.AppendLine("        <div>Compiled offline directly from SQLite index &bull; Zero external cloud dependencies</div>");
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

    public async Task StreamQueryToCsvAsync(
        DatabaseService dbService,
        string filePath,
        long minSize = 0,
        long maxSize = long.MaxValue,
        string? category = null,
        string? search = null,
        string sortBy = "size",
        bool sortDesc = true,
        int? minDaysOld = null,
        string? extension = null,
        string? locationPrefix = null,
        CancellationToken ct = default)
    {
        await using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 65536, useAsync: true);
        await using var writer = new StreamWriter(fs, Encoding.UTF8);

        await writer.WriteLineAsync("\"Path\",\"Name\",\"Parent\",\"Size\",\"SizeFormatted\",\"Category\",\"Extension\",\"ModifiedTimeUtc\"");

        long count = 0;
        await Task.Run(() =>
        {
            dbService.StreamFilteredFiles(file =>
            {
                string modUtc = file.ModifiedTime > 0
                    ? DateTimeOffset.FromUnixTimeSeconds((long)file.ModifiedTime).ToString("yyyy-MM-dd HH:mm:ss")
                    : string.Empty;

                writer.Write(CsvEscape(file.Path)); writer.Write(',');
                writer.Write(CsvEscape(file.Name)); writer.Write(',');
                writer.Write(CsvEscape(file.Parent)); writer.Write(',');
                writer.Write(file.Size); writer.Write(',');
                writer.Write(CsvEscape(file.FormattedSize)); writer.Write(',');
                writer.Write(CsvEscape(file.Category)); writer.Write(',');
                writer.Write(CsvEscape(file.Extension)); writer.Write(',');
                writer.WriteLine(CsvEscape(modUtc));

                count++;
                if (count % 1000 == 0)
                {
                    writer.Flush();
                }
            }, minSize, maxSize, category, search, sortBy, sortDesc, minDaysOld, extension, locationPrefix, ct);

            writer.Flush();
        }, ct);
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

    private static string GetCategoryColor(string? category)
    {
        return (category?.Trim().ToLowerInvariant()) switch
        {
            "video" or "videos" => "#A78BFA",
            "audio" => "#F472B6",
            "images" or "image" => "#34D399",
            "photoshop" => "#38BDF8",
            "archives" or "archive" or "compressed" => "#FBBF24",
            "executables" or "executable" => "#FB7185",
            "code" => "#2DD4BF",
            "documents" or "document" => "#60A5FA",
            "database" => "#818CF8",
            "browser" => "#F59E0B",
            "developer" => "#2DD4BF",
            "system" => "#38BDF8",
            "design" => "#C084FC",
            "application" => "#60A5FA",
            _ => "#94A3B8"
        };
    }

    private static string GetCategorySlug(string? category)
    {
        if (string.IsNullOrWhiteSpace(category)) return "other";
        return category.Trim().ToLowerInvariant() switch
        {
            "video" or "videos" => "video",
            "audio" => "audio",
            "images" or "image" => "images",
            "photoshop" => "photoshop",
            "archives" or "archive" or "compressed" => "archives",
            "executables" or "executable" => "executables",
            "code" => "code",
            "documents" or "document" => "documents",
            "database" => "database",
            "browser" => "browser",
            "developer" => "developer",
            "system" => "system",
            "design" => "design",
            "application" => "application",
            _ => "other"
        };
    }
}
