namespace DiskScope.Models;

public static class FileCategory
{
    public const string Photoshop = "Photoshop";
    public const string Images = "Images";
    public const string Video = "Video";
    public const string Audio = "Audio";
    public const string Archives = "Archives";
    public const string Documents = "Documents";
    public const string Code = "Code";
    public const string Executables = "Executables";
    public const string Other = "Other";

    private static readonly HashSet<string> PhotoshopExts = new(StringComparer.OrdinalIgnoreCase)
    {
        ".psd", ".psb", ".pdd", ".abr", ".asl", ".atn", ".pat"
    };

    private static readonly HashSet<string> ImageExts = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".tiff", ".tif", ".webp", ".svg",
        ".raw", ".cr2", ".nef", ".arw", ".dng", ".heic", ".ico"
    };

    private static readonly HashSet<string> VideoExts = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".mkv", ".mov", ".avi", ".wmv", ".flv", ".webm", ".m4v", ".mpg", ".mpeg", ".3gp", ".ts"
    };

    private static readonly HashSet<string> AudioExts = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp3", ".wav", ".flac", ".aac", ".ogg", ".m4a", ".wma", ".alac", ".aiff", ".mid", ".midi"
    };

    private static readonly HashSet<string> ArchiveExts = new(StringComparer.OrdinalIgnoreCase)
    {
        ".zip", ".rar", ".7z", ".tar", ".gz", ".bz2", ".xz", ".iso", ".cab", ".dmg", ".tgz", ".wim", ".vhd", ".vhdx"
    };

    private static readonly HashSet<string> DocumentExts = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".docx", ".doc", ".xlsx", ".xls", ".pptx", ".ppt", ".txt", ".rtf", ".odt", ".csv", ".md", ".epub"
    };

    private static readonly HashSet<string> CodeExts = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cs", ".js", ".ts", ".tsx", ".jsx", ".html", ".htm", ".css", ".scss", ".json", ".xml", ".yaml", ".yml",
        ".py", ".cpp", ".c", ".h", ".hpp", ".java", ".go", ".rs", ".sql", ".sh", ".ps1", ".php", ".rb", ".swift", ".kt"
    };

    private static readonly HashSet<string> ExecutableExts = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".dll", ".msi", ".sys", ".drv", ".bat", ".cmd", ".com", ".scr", ".ocx"
    };

    public static string FromExtension(string? extension)
    {
        if (string.IsNullOrWhiteSpace(extension)) return Other;

        string ext = extension.StartsWith('.') ? extension : "." + extension;

        if (PhotoshopExts.Contains(ext)) return Photoshop;
        if (ImageExts.Contains(ext)) return Images;
        if (VideoExts.Contains(ext)) return Video;
        if (AudioExts.Contains(ext)) return Audio;
        if (ArchiveExts.Contains(ext)) return Archives;
        if (DocumentExts.Contains(ext)) return Documents;
        if (CodeExts.Contains(ext)) return Code;
        if (ExecutableExts.Contains(ext)) return Executables;

        return Other;
    }

    public static readonly string[] AllCategories =
    [
        Photoshop,
        Images,
        Video,
        Audio,
        Archives,
        Documents,
        Code,
        Executables,
        Other
    ];
}
