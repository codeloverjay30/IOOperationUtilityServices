namespace EntriesAndTheirContentsServices;

public interface IEntriesAndTheirContentsService
{
    void LogEntriesOfDirectoryAndTheirContentsToFile(
        string directory,
        string pattern,
        string logFilePath,
        LogEntriesOptions logEntriesOptions = LogEntriesOptions.All,
        long maxFileSizeInBytes = 2 * 1024 * 1024
    );

    IEnumerable<FileEntryResult> GetSafeEntriesAndContents(
        string rootPath,
        long maxFileSizeInBytes
    );
}
