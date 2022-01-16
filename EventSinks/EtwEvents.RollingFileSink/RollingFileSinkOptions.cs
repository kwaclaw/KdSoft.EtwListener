namespace KdSoft.EtwEvents.EventSinks
{
    public class RollingFileSinkOptions
    {
        public RollingFileSinkOptions() { }

        /// <summary>
        /// Path to file directory. Can be relative or absolute.
        /// </summary>
        public string Directory { get; set; } = ".";

        /// <summary>
        /// .NET format string to generate file name from timestamp. When the file name would change
        /// based on the current timestamp, then a new file will be created with the updated file name.
        /// </summary>
        /// <remarks>The actual file name will have a sequnce number inserted before the extension.</remarks>
        public string FileNameFormat { get; set; } = "app-{0:yyyy-MM-dd}";

        /// <summary>
        /// File extension, must start with '.'.
        /// </summary>
        public string FileExtension { get; set; } = ".log";

        /// <summary>
        /// Use local time instead of UTC time.
        /// </summary>
        public bool UseLocalTime { get; set; } = true;

        /// <summary>
        /// The file size limit determines at which size a file should be closed and a new file should be created.
        /// When the file name would stay the same (based on <see cref="FileNameFormat")/> then the sequence number
        /// usded in the file name format will be incremented.
        /// </summary>
        public int FileSizeLimitKB { get; set; } = 4096;

        /// <summary>
        /// Maximum number of files that should be kept. Oldest files will be purged first.
        /// </summary>
        public int MaxFileCount { get; set; } = 10;

        /// <summary>
        /// Determines if a new file should be created on event sink startup, even if an existing file
        /// could be continued based on the timestamp and its file size.
        /// </summary>
        public bool NewFileOnStartup { get; set; }

        /// <summary>
        /// Allows for bypassing some character escapes that sometimes may be a security risk.
        /// For instance, '+' is usually encoded as '\u002B' which can be confusing in UTC time stamps.
        /// True by default.
        /// </summary>
        public bool RelaxedJsonEscaping { get; set; } = true;
    }
}
