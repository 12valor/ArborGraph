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

    private static readonly Dictionary<string, string> ExtensionMap;

    static FileCategory()
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        void Register(string[] exts, string category)
        {
            foreach (var ext in exts)
            {
                map[ext] = category;
            }
        }

        Register([".psd", ".psb", ".pdd", ".abr", ".asl", ".atn", ".pat"], Photoshop);

        Register([
            ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".tiff", ".tif", ".webp", ".svg",
            ".raw", ".cr2", ".nef", ".arw", ".dng", ".heic", ".ico"
        ], Images);

        Register([
            ".mp4", ".mkv", ".mov", ".avi", ".wmv", ".flv", ".webm", ".m4v", ".mpg", ".mpeg", ".3gp", ".ts"
        ], Video);

        Register([
            ".mp3", ".wav", ".flac", ".aac", ".ogg", ".m4a", ".wma", ".alac", ".aiff", ".mid", ".midi"
        ], Audio);

        Register([
            ".zip", ".rar", ".7z", ".tar", ".gz", ".bz2", ".xz", ".iso", ".cab", ".dmg", ".tgz", ".wim", ".vhd", ".vhdx"
        ], Archives);

        Register([
            ".pdf", ".docx", ".doc", ".xlsx", ".xls", ".pptx", ".ppt", ".txt", ".rtf", ".odt", ".csv", ".md", ".epub"
        ], Documents);

        Register([
            ".cs", ".js", ".ts", ".tsx", ".jsx", ".html", ".htm", ".css", ".scss", ".json", ".xml", ".yaml", ".yml",
            ".py", ".cpp", ".c", ".h", ".hpp", ".java", ".go", ".rs", ".sql", ".sh", ".ps1", ".php", ".rb", ".swift", ".kt"
        ], Code);

        Register([
            ".exe", ".dll", ".msi", ".sys", ".drv", ".bat", ".cmd", ".com", ".scr", ".ocx"
        ], Executables);

        ExtensionMap = map;
    }

    public static string FromExtension(string? extension)
    {
        if (string.IsNullOrWhiteSpace(extension)) return Other;

        string ext = extension.StartsWith('.') ? extension : "." + extension;

        return ExtensionMap.TryGetValue(ext, out var cat) ? cat : Other;
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
