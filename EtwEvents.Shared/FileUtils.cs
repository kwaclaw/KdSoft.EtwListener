namespace KdSoft.EtwEvents
{
    public static class FileUtils
    {
        const int ERROR_SHARING_VIOLATION = 0x20;

        public static FileStream OpenFileWithRetry(string path, FileMode mode, FileAccess fileAccess, FileShare fileShare, int bufferSize = 4096, FileOptions options = FileOptions.None) {
            var autoResetEvent = new AutoResetEvent(false);
            var fullPath = Path.GetFullPath(path);

            while (true) {
                try {
                    return new FileStream(path, mode, fileAccess, fileShare, bufferSize, options);
                }
                catch (IOException ex) {
                    int win32Error = ex.HResult & 0xFFFF;
                    if (win32Error == ERROR_SHARING_VIOLATION) {
                        var pathDir = Path.GetDirectoryName(path) ?? Path.GetPathRoot(path);
                        if (pathDir is null) {
                            throw;
                        }
                        var filter = "*" + Path.GetExtension(path);
                        using (var fileSystemWatcher = new FileSystemWatcher(pathDir, filter) { EnableRaisingEvents = true }) {
                            fileSystemWatcher.Changed += (o, e) => {
                                if (Path.GetFullPath(e.FullPath) == fullPath) {
                                    autoResetEvent.Set();
                                }
                            };

                            autoResetEvent.WaitOne();
                        }
                    }
                    else {
                        throw;
                    }
                }
            }
        }


        public static void WriteFileAtomic(ReadOnlySpan<byte> buffer, string filePath, string? backupPath = null) {
            // Requirement of File.Replace(): the source file and target file must be on the same drive/volume
            var replacePath = Path.GetDirectoryName(filePath) ?? Path.GetPathRoot(filePath);
            var replaceFile = Path.Combine(replacePath!, Guid.NewGuid().ToString());
            try {
                using (var replaceStream = new FileStream(replaceFile, FileMode.Create, FileAccess.Write, FileShare.None)) {
                    replaceStream.Write(buffer);
                }
                // this operation is atomic, it should work when filePath is open with FileShare.Delete
                File.Replace(replaceFile, filePath, backupPath);

            }
            catch {
                if (replaceFile != null) {
                    File.Delete(replaceFile);
                }
                throw;
            }
        }
    }
}
