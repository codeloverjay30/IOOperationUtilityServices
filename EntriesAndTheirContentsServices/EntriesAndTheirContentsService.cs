using System.Diagnostics.Contracts;
using IOOperationUtilityServices;
using FileCategorizationUtilityServices;
using System.IO.Abstractions;
using System.CodeDom.Compiler;

namespace EntriesAndTheirContentsServices;

public class EntriesAndTheirContentsService : IEntriesAndTheirContentsService
{
    private readonly IFileSystem _fileSystem;
    private readonly IDirectoryUtilityService _directoryUtilityService;
    private readonly IFileUtilityService _fileUtilityService;
    private readonly IFileExtensionChecker _fileExtensionChecker;
    private readonly IExcludedEntriesUtilityService _excludedEntriesUtilityService;
    public EntriesAndTheirContentsService(
        IFileSystem fileSystem,
        IFileUtilityService fileUtilityService,
        IDirectoryUtilityService directoryUtilityService
    )
    {
        ArgumentNullException.ThrowIfNull(fileSystem, nameof(fileSystem));
        ArgumentNullException.ThrowIfNull(fileUtilityService, nameof(fileUtilityService));
        ArgumentNullException.ThrowIfNull(directoryUtilityService, nameof(directoryUtilityService));

        _fileSystem = fileSystem;
        _fileUtilityService = fileUtilityService;
        _directoryUtilityService = directoryUtilityService;
        
        // 安全地在防禦檢查後建立
        _fileExtensionChecker = new FileExtensionChecker(fileUtilityService.FileSystem);
        _excludedEntriesUtilityService = new ExcludedEntriesUtilityService();
    }

    public EntriesAndTheirContentsService(
        IFileSystem fileSystem,
        IFileUtilityService fileUtilityService,
        IDirectoryUtilityService directoryUtilityService,
        IFileExtensionChecker fileExtensionChecker,
        IExcludedEntriesUtilityService excludedEntriesUtilityService
    )
    {
        ArgumentNullException.ThrowIfNull(fileSystem, nameof(fileSystem));
        ArgumentNullException.ThrowIfNull(fileUtilityService, nameof(fileUtilityService));
        ArgumentNullException.ThrowIfNull(directoryUtilityService, nameof(directoryUtilityService));
        ArgumentNullException.ThrowIfNull(fileExtensionChecker, nameof(fileExtensionChecker));
        ArgumentNullException.ThrowIfNull(excludedEntriesUtilityService, nameof(excludedEntriesUtilityService));

        _fileSystem = fileSystem;
        _fileUtilityService = fileUtilityService;
        _directoryUtilityService = directoryUtilityService;
        _fileExtensionChecker = fileExtensionChecker;
        _excludedEntriesUtilityService = excludedEntriesUtilityService;
    }

    /// <summary>
    /// Logs the entries of a specified directory and their contents to a file, with options to exclude certain types of entries.
    /// </summary>
    /// <param name="directory"></param>
    /// <param name="pattern"></param>
    /// <param name="logFilePath"></param>
    /// <param name="logEntriesOptions"></param>
    /// <param name="maxFileSizeInBytes"></param>
    public void LogEntriesOfDirectoryAndTheirContentsToFile(
        string directory,
        string pattern,
        string logFilePath,
        LogEntriesOptions logEntriesOptions = LogEntriesOptions.All,
        long maxFileSizeInBytes = 2 * 1024 * 1024
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory, nameof(directory));
        ArgumentException.ThrowIfNullOrWhiteSpace(logFilePath, nameof(logFilePath));

        // 1. 初始化或清空目標日誌檔
        _fileUtilityService.CreateOrClearFile(logFilePath);

        // 2. 獲取安全過濾後的檔案結果
        IEnumerable<FileEntryResult> safeEntries = GetSafeEntriesAndContents(directory, maxFileSizeInBytes);

        // 3. 建立基礎與縮排寫入器
        using (TextWriter baseWriter = _fileSystem.File.CreateText(logFilePath))
        // 第二個參數為縮排字串，預設使用四個空白 "    "，亦可傳入 "\t"
        using (var indentedWriter = new IndentedTextWriter(baseWriter, "    "))
        {
            // --- 區塊一：全局日誌標頭 (無縮排) ---
            indentedWriter.WriteLine("================================================================================");
            indentedWriter.WriteLine($"Repository Scan Log - Executed at: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            indentedWriter.WriteLine($"Target Directory: {directory}");
            indentedWriter.WriteLine($"Scan Pattern: {pattern}");
            indentedWriter.WriteLine("================================================================================");
            indentedWriter.WriteLine();

            // --- 區塊二：走訪條目並控制縮排層級 ---
            foreach (var entry in safeEntries)
            {
                string fullPath = _fileSystem.Path.Combine(directory, entry.RelativePath);

                // 防禦日誌自我衝突
                if (string.Equals(_fileSystem.Path.GetFullPath(fullPath), _fileSystem.Path.GetFullPath(logFilePath), StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                // 檢查是否在排除選項內
                if (ShouldIgnoreToLog(fullPath, logEntriesOptions))
                {
                    continue;
                }

                // 寫入項目外殼標頭 (層級 0)
                indentedWriter.WriteLine($"[File Entry] : {entry.RelativePath}");
                indentedWriter.WriteLine($"[Size (Bytes)]: {entry.SizeInBytes:N0}");
                indentedWriter.WriteLine("```");

                // 進入核心內容區塊，推進縮排 (層級 1)
                indentedWriter.Indent++;

                // 防禦性優化：若檔案有內容，逐行寫入，確保每行的開頭都自動套用目前縮排
                if (!string.IsNullOrEmpty(entry.Content))
                {
                    using (var contentReader = new StringReader(entry.Content))
                    {
                        string? line;
                        while ((line = contentReader.ReadLine()) != null)
                        {
                            indentedWriter.WriteLine(line);
                        }
                    }
                }
                else
                {
                    indentedWriter.WriteLine("// (Empty File or No Content Loaded Safely)");
                }

                // 離開內容區塊，恢復縮排 (層級 0)
                indentedWriter.Indent--;
                indentedWriter.WriteLine("```");
                indentedWriter.WriteLine(); // 項目間距留空
            }

            // --- 區塊三：全局結束標尾 ---
            indentedWriter.WriteLine("================================================================================");
            indentedWriter.WriteLine("Scan Completed Successfully.");
            indentedWriter.WriteLine("================================================================================");
        }
    }

    /// <summary>
    /// Safely retrieves all valid text file entries and their contents from a root directory based on defensive constraints.
    /// </summary>
    /// <param name="rootPath">The target directory path to scan.</param>
    /// <param name="maxFileSizeInBytes">The hard limit for individual file sizes to prevent OutOfMemoryException.</param>
    /// <returns>An enumerable collection of verified file entries and contents.</returns>
    /// <exception cref="DirectoryNotFoundException">Thrown when the specified root path does not exist.</exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when the runtime lacks sufficient OS permissions.</exception>
    public IEnumerable<FileEntryResult> GetSafeEntriesAndContents(
        string rootPath,
        long maxFileSizeInBytes
    )
    {
        if (string.IsNullOrWhiteSpace(rootPath))
        {
            throw new ArgumentException("Root path cannot be null or empty.", nameof(rootPath));
        }

        if (!_fileSystem.Directory.Exists(rootPath))
        {
            throw new DirectoryNotFoundException($"The targeted directory context was not found: {rootPath}");
        }

        string[] allFiles;
        try
        {
            // Defensive optimization: Use top-level or strategic search pattern depending on scaling requirements
            allFiles = _fileSystem.Directory.GetFiles(rootPath, "*.*", SearchOption.AllDirectories);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new UnauthorizedAccessException("Inadequate OS permissions to traverse directory structure.", ex);
        }

        foreach (string file in allFiles)
        {
            IFileInfo fileInfo = _fileSystem.FileInfo.New(file);

            // Defense 1: Skip files exceeding individual token/memory thresholds (Prevent LOH contamination)
            if (fileInfo.Length > maxFileSizeInBytes)
            {
                continue;
            }

            string content;
            try
            {
                // Defense 2: Atomic streaming read to avoid locking system handles indefinitely
                using (var reader = _fileSystem.File.OpenText(file))
                {
                    content = reader.ReadToEnd();
                }
            }
            catch (IOException)
            {
                // Defensively bypass files currently locked by concurrent OS threads or processes
                continue;
            }

            string relativePath = _fileSystem.Path.GetRelativePath(rootPath, file);
            yield return new FileEntryResult(relativePath, content, fileInfo.Length);
        }
    }


    private bool ShouldIgnoreToLog(
        string path,
        LogEntriesOptions logEntriesOptions
    )
    {
        switch (logEntriesOptions)
        {
            case LogEntriesOptions.ExcludeFolders:
            {
               if (_directoryUtilityService.IsDirectory(path))
                {
                    return true;
                } 
                break;     
            }
            case LogEntriesOptions.ExcludeNetCachedFolders:
            {
                if (
                    _directoryUtilityService.IsDirectory(path) &&
                    _excludedEntriesUtilityService.IsExcludedFolderName(path)
                )
                    {
                        return true;
                    }
                break;
            }
            case LogEntriesOptions.ExcludeFiles:
            {
                if (_fileUtilityService.FileExists(path))
                {
                    return true;
                }
                break;
            }
            case LogEntriesOptions.NetSolution:
            {
                if (
                    _directoryUtilityService.IsDirectory(path) &&
                    _excludedEntriesUtilityService.IsExcludedFolderName(path)
                )
                {
                    return true;
                }

                if (
                    _fileUtilityService.FileExists(path) &&
                    _fileExtensionChecker.NeedsToBeReplaced(path)
                )
                {
                    return false;
                }
                return true;
            }
            case LogEntriesOptions.All:
            {
                return false;
            }
        }
        return false;
    }
}
