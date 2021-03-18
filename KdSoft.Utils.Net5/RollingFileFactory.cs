using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

//TODO look for a place to put this shareable class
namespace KdSoft.Logging
{
    public class RollingFileFactory: IDisposable, IAsyncDisposable
    {
        readonly DirectoryInfo _dirInfo;
        readonly Func<DateTimeOffset, string> _fileNameSelector;
        readonly string _fileExtension;
        readonly long _fileSizeLimitBytes;
        readonly int _maxFileCount;
        readonly bool _useLocalTime;

        public const int MaxFilesToDelete = 5;

        public bool UseLocalTime => _useLocalTime;

        // used to enable file creation on startup (regardless of other checks)
        int _createNewFileOnStartup;
        FileStream? _stream;

        public RollingFileFactory(
            DirectoryInfo dirInfo,
            Func<DateTimeOffset, string> fileNameSelector,
            string fileExtension,
            bool useLocalTime,
            int fileSizeLimitKB,
            int maxFileCount,
            bool newFileOnStartup
        ) {
            this._dirInfo = dirInfo;
            this._fileNameSelector = fileNameSelector;
            this._fileExtension = fileExtension;
            this._useLocalTime = useLocalTime;
            this._fileSizeLimitBytes = fileSizeLimitKB * 1024;
            this._maxFileCount = maxFileCount;
            this._createNewFileOnStartup = newFileOnStartup ? 1 : 0;
        }

        bool TimestampPatternChanged(FileStream stream, DateTimeOffset now) {
            var newFnBase = _fileNameSelector(now);
            var currentFn = Path.GetFileNameWithoutExtension(stream.Name);
            var compared = string.Compare(newFnBase, 0, currentFn, 0, newFnBase.Length, StringComparison.CurrentCultureIgnoreCase);
            return compared != 0;
        }

        bool TryGetSequenceNo(string fileName, out int value) {
            int lastDotIndex = fileName.LastIndexOf('.');
            int lastDashIndex = fileName.LastIndexOf('-', lastDotIndex);
            var sequenceNoSpan = fileName.AsSpan(lastDashIndex + 1, lastDotIndex - lastDashIndex);
            return int.TryParse(sequenceNoSpan, out value);
        }

        FileStream CreateNextSuitableFileStream(DateTimeOffset now, bool alwaysCreateNewFile) {
            var newFnBase = _fileNameSelector(now);
            var enumOptions = new EnumerationOptions {
                IgnoreInaccessible = true,
                RecurseSubdirectories = false,
            };
            var sortedFiles = new SortedList<string, FileInfo>(StringComparer.CurrentCultureIgnoreCase);
            var oldFiles = new List<FileInfo>();

            var newNameStart = $"{newFnBase}-";
            int fileCount = 0;
            foreach (var file in _dirInfo.EnumerateFiles($"*-*{_fileExtension}", enumOptions)) {
                fileCount++;

                // determine the MaxFilesToDelete oldest files
                if (oldFiles.Count == 0)
                    oldFiles.Add(file);
                else {
                    // start with the oldest file
                    bool inserted = false;
                    for (int indx = 0; indx < oldFiles.Count; indx++) {
                        var oldFile = oldFiles[indx];
                        // if file is older then insert at this position 
                        if (file.LastWriteTimeUtc < oldFile.LastWriteTimeUtc) {
                            oldFiles.Insert(indx, file);
                            inserted = true;
                            break;
                        }
                    }
                    if (inserted && oldFiles.Count > MaxFilesToDelete) {
                        oldFiles.RemoveAt(MaxFilesToDelete);
                    }
                    else if (oldFiles.Count < MaxFilesToDelete) {
                        oldFiles.Add(file);
                    }
                }

                // sort the files that match our new name (except for the sequence number)
                if (file.Name.StartsWith(newNameStart))
                    sortedFiles.Add(file.Name, file);
            }

            // if we have too many files, delete at most <MaxFilesToDelete> old files
            var delta = fileCount - _maxFileCount;
            if (delta > oldFiles.Count)
                delta = oldFiles.Count;
            for (int indx = 0; indx < delta; indx++) {
                oldFiles[indx].Delete();
            }

            int lastSequenceNo = 0;
            if (sortedFiles.Count > 0) {
                var lastFileName = sortedFiles.Values[sortedFiles.Count - 1].Name;
                if (!TryGetSequenceNo(lastFileName, out lastSequenceNo)) {
                    lastSequenceNo = 0;
                }
            }

            IOException? ex = null;
            for (int sequenceNo = lastSequenceNo; sequenceNo < 1000; sequenceNo++) {
                var seqNoStr = sequenceNo.ToString("D2");
                var newFn = $"{newFnBase}-{seqNoStr}{_fileExtension}";
                if (sortedFiles.TryGetValue(newFn, out var newFi)) {
                    if (alwaysCreateNewFile || newFi.Length >= _fileSizeLimitBytes) {
                        continue;
                    }
                }
                try {
                    var fileName = Path.Combine(_dirInfo.FullName, newFn);
                    // NOTE: it seems when using "useAsync: true" we get random spurious stretches of null bytes in the output, why??
                    return new FileStream(fileName, FileMode.Append, FileAccess.Write, FileShare.Read, 4096, useAsync: false);
                }
                catch (IOException ioex) {
                    // make one attempt to ignore an IOException because the file may have been created in the meantime
                    if (ex == null) {
                        ex = ioex;
                        continue;
                    }
                    throw ex;
                }
            }
            throw new InvalidOperationException($"Too many files like {newFnBase} for same timestamp.");
        }

        async ValueTask<FileStream> CreateNewFileStream(FileStream? oldStream, DateTimeOffset now, bool alwaysCreateNewFile) {
            var newStream = CreateNextSuitableFileStream(now, alwaysCreateNewFile);
            if (oldStream != null) {
                await oldStream.FlushAsync().ConfigureAwait(false);
                await oldStream.DisposeAsync().ConfigureAwait(false);
            }
            return _stream = newStream;
        }

        /// <summary>
        /// Checks rollover conditions and returns old or new file stream task as a result.
        /// </summary>
        /// <remarks>Not thread-safe, result must be awaited before calling again..
        public ValueTask<FileStream> GetCurrentFileStream() {
            var stream = _stream;
            var now = _useLocalTime ? DateTimeOffset.Now : DateTimeOffset.UtcNow;

            if (stream != null) {
                bool createNewFile = stream.Length >= _fileSizeLimitBytes || TimestampPatternChanged(stream, now);
                if (!createNewFile)
                    return ValueTask.FromResult(stream);
            }

            var oldCreateNewFile = Interlocked.Exchange(ref _createNewFileOnStartup, 0);
            return CreateNewFileStream(stream, now, oldCreateNewFile == 1);
        }

        public void Dispose() {
            var oldStream = Interlocked.Exchange(ref _stream, null);
            if (oldStream != null) {
                oldStream.Flush();
                oldStream.Dispose();
            }
        }

        public async ValueTask DisposeAsync() {
            var oldStream = Interlocked.Exchange(ref _stream, null);
            if (oldStream != null) {
                await oldStream.FlushAsync().ConfigureAwait(false);
                await oldStream.DisposeAsync().ConfigureAwait(false);
            }
        }
    }
}
