namespace TextChunker.Models
{
    using System.Text;
    using TextChunker.Enums;

    /// <summary>
    /// File access parameters used when a chunking operation opens a file by name. Controls sharing and encoding.
    /// </summary>
    public class FileChunkingOptions
    {
        private Encoding _Encoding = Encoding.UTF8;

        /// <summary>
        /// File sharing mode. Default SharedRead.
        /// </summary>
        public FileAccessModeEnum AccessMode { get; set; } = FileAccessModeEnum.SharedRead;

        /// <summary>
        /// Text encoding used to read the file. Default UTF-8. Never null.
        /// </summary>
        public Encoding Encoding
        {
            get => _Encoding;
            set => _Encoding = value ?? Encoding.UTF8;
        }

        /// <summary>
        /// When true, a byte order mark at the start of the file overrides the configured encoding. Default true.
        /// </summary>
        public bool DetectEncodingFromByteOrderMarks { get; set; } = true;
    }
}
