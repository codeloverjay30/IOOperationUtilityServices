using CustomDataAnnotations.Maintenance;
using DriveInfoUtilityServices;
using EnvironmentUtilityServices;
using SymbolicLinkUtilityServices;
using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.Text;

namespace IOOperationUtilityServices
{
    /// <summary>
    /// Utility class to handle directories
    /// </summary>
    public class DirectoryUtilityService : IDirectoryUtilityService
    {
        private readonly EnumerationOptions _defaultEnumerationOptions = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            // Additional performance flags:
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.System
        };
            
        private readonly IFileSystem _fileSystem;
        private readonly Lazy<IFileUtilityService> _fileUtilityService;
        private readonly Lazy<IEnvironmentService> _environmentService;
        private readonly Lazy<IOsUtilityService> _osUtilityService;

        private readonly Lazy<IDriveInfoUtilityService> _driveInfoUtilityService;
        private readonly Lazy<ISymbolicLinkUtilityService> _symbolicLinkUtilityService;
        public IFileSystem FileSystem => _fileSystem;
        public DirectoryUtilityService(
            IFileSystem fileSystem,
            Lazy<IFileUtilityService> fileUtilityService,
            Lazy<IEnvironmentService> environmentService,
            Lazy<IOsUtilityService> osUtilityService,
            Lazy<IDriveInfoUtilityService> driveInfoUtilityService,
            Lazy<ISymbolicLinkUtilityService> symbolicLinkUtilityService
        )
        {
            ArgumentNullException.ThrowIfNull(fileSystem, nameof(fileSystem));
            ArgumentNullException.ThrowIfNull(fileUtilityService, nameof(fileUtilityService));
            ArgumentNullException.ThrowIfNull(environmentService, nameof(environmentService));
            ArgumentNullException.ThrowIfNull(osUtilityService, nameof(osUtilityService));
            ArgumentNullException.ThrowIfNull(driveInfoUtilityService, nameof(driveInfoUtilityService));
            ArgumentNullException.ThrowIfNull(symbolicLinkUtilityService, nameof(symbolicLinkUtilityService));
            
            this._fileSystem = fileSystem;
            this._fileUtilityService = fileUtilityService;
            this._environmentService = environmentService;
            this._osUtilityService = osUtilityService;
            this._driveInfoUtilityService = driveInfoUtilityService;
            this._symbolicLinkUtilityService = symbolicLinkUtilityService;
        }

        /// <summary>
        /// return true iff `sourceFileFullPath` exists and it is a directory. (validate by `_fileSystem.FileAttributes`)
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public bool IsDirectory(string path)
        {
            if(_fileSystem.Directory.Exists(path))
            {
                return true;
            }
            return _fileSystem.File.GetAttributes(path).HasFlag(FileAttributes.Directory);
        }

        /// <summary>
        /// return true iff `sourceFileFullPath` exists and it is a file. (validate by `_fileSystem.FileAttributes`)
        /// 
        /// > [!NOTE]
        /// > it is not equivalent to negation of `IsDirectory` method call.  
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public bool IsFile(string path)
        {
            if(!_fileSystem.File.Exists(path) && !_fileSystem.Directory.Exists(path))
            {
                return false;
            }
            return !_fileSystem.File.GetAttributes(path).HasFlag(FileAttributes.Directory);
        }

        /// <summary>
        /// return true iff `sourceFileFullPath` is a directory or file. 
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        /// <remarks>
        /// Use <seealso cref="AnyExists"/> method instead as this method name is misleading and does not accurately reflect its functionality.
        /// </remarks>
        [Obsolete("Use AnyExists method instead for better semantic support.")]
        [TechnicalDebt(CategoryType.NamingIssue , "AnyExists")]

        public bool Exists(string path) => _fileSystem.Directory.Exists(path) || _fileSystem.File.Exists(path);

        /// <summary>
        /// return true iff `sourceFileFullPath` is a directory or file. 
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public bool AnyExists(string path) => _fileSystem.Directory.Exists(path) || _fileSystem.File.Exists(path);

        /// <summary>
        /// add a file under directory.
        /// 
        /// add a file whose full path is `sourceFileFullPath` under directory `destinationDirectory`,
        /// 
        /// then rename the new file name (with extension) (exclusive of directory of new file name) as `childName`
        /// </summary>
        /// <param name="sourceFileFullPath"></param>
        /// <param name="destinationDirectory"></param>
        /// <param name="childName"></param>
        [Obsolete("Use CopyFileToDirectory method instead for better semantic support.")]
        [TechnicalDebt(CategoryType.NamingIssue , "CopyFileToDirectory")]
        public void AddChild(
            string sourceFileFullPath ,
            string destinationDirectory ,
            string childName
        )
        {
            string destinationFileFullPath = _fileSystem.Path.Combine(destinationDirectory , childName);

            bool exists = _fileSystem.Directory.Exists(destinationDirectory);
            if(!exists)
            {
                _fileSystem.Directory.CreateDirectory(destinationDirectory);
            }

            _fileUtilityService.Value.Copy(sourceFileFullPath , destinationFileFullPath);

            return;
        }

        /// <summary>
        /// alternative method name for <seealso cref="AddChild(string, string, string)"/> to provide better semantic support.
        /// </summary>
        /// <param name="sourceFileFullPath"></param>
        /// <param name="destinationDirectory"></param>
        /// <param name="childName"></param>
        public void CopyFileToDirectory(
            string sourceFileFullPath ,
            string destinationDirectory ,
            string childName
        )
        {
            string destinationFileFullPath = _fileSystem.Path.Combine(destinationDirectory , childName);

            bool exists = _fileSystem.Directory.Exists(destinationDirectory);
            if(!exists)
            {
                _fileSystem.Directory.CreateDirectory(destinationDirectory);
            }

            _fileUtilityService.Value.Copy(sourceFileFullPath , destinationFileFullPath);

            return;
        }

        /// <summary>
        /// Async version of <seealso cref="AddChild(string, string, string)"/> method.
        /// </summary>
        /// <param name="sourceFileFullPath"></param>
        /// <param name="destinationDirectory"></param>
        /// <param name="childName"></param>
        /// <returns></returns>
        [Obsolete("Use CopyFileToDirectoryAsync method instead for better semantic support.")]
        [TechnicalDebt(CategoryType.NamingIssue , "CopyFileToDirectoryAsync")]
        public async Task AddChildAsync(
            string sourceFileFullPath ,
            string destinationDirectory ,
            string childName
        )
        {
            string destinationFileFullPath = _fileSystem.Path.Combine(destinationDirectory , childName);

            if(!_fileSystem.Directory.Exists(destinationDirectory))
            {
                _fileSystem.Directory.CreateDirectory(destinationDirectory);
            }

            await _fileUtilityService.Value.CopyAsync(sourceFileFullPath , destinationFileFullPath);
        }

        /// <summary>
        /// alternative of <seealso cref="AddChildAsync(string, string, string)"/>.
        /// </summary>
        /// <param name="sourceFileFullPath"></param>
        /// <param name="destinationDirectory"></param>
        /// <param name="childName"></param>
        /// <returns></returns>
        public async Task CopyToDirectoryAsync(
            string sourceFileFullPath ,
            string destinationDirectory ,
            string childName
        )
        {
            string destinationFileFullPath = _fileSystem.Path.Combine(destinationDirectory , childName);

            if(!_fileSystem.Directory.Exists(destinationDirectory))
            {
                _fileSystem.Directory.CreateDirectory(destinationDirectory);
            }

            await _fileUtilityService.Value.CopyAsync(sourceFileFullPath , destinationFileFullPath);
        }

        /// <summary>
        /// delete directory.
        /// 
        /// delete all children of `sourceDirectory`,
        /// 
        /// then delete the folder `sourceDirectory`
        /// </summary>
        /// <param name="path">directory that itself and its entries will be deleted (if it can)</param>
        public void DeleteDirectory(string path)
        {
            // 優化刪除邏輯：直接利用 .NET 內建的遞迴刪除
            _fileSystem.Directory.Delete(path, true); // true 代表遞迴刪除所有內容
        }

        /// <summary>
        /// Async version of <seealso cref="DeleteDirectory(string)"/> method.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public Task DeleteDirectoryAsync(string path)
        {
            return Task.Run(() =>
            {
                DeleteDirectory(path);
            });
        }

        /// <summary>
        /// delete specific child of a directory.
        /// 
        /// delete specific child whose name is `childName` of a directory `sourceDirectory`.
        /// </summary>
        /// <param name="sourceDirectory"></param>
        /// <param name="childName"></param>
        public void DeleteChild(
            string path,
            string childName
        )
        {
            string fileFullPath = _fileSystem.Path.Combine(path, childName);
            DeleteFileSystemItem(fileFullPath);
        }

        /// <summary>
        /// Async version of <seealso cref="DeleteChild(string, string)"/> method.
        /// </summary>
        /// <param name="path"></param>
        /// <param name="childName"></param>
        public async Task DeleteChildAsync(
            string path,
            string childName
        )
        {
            string fileFullPath = _fileSystem.Path.Combine(path, childName);
            await DeleteFileSystemItemAsync(fileFullPath);
        }

        /// <summary>
        /// delete all children of a directory.
        /// 
        /// delete all children of a directory `sourceDirectory`.
        /// </summary>
        /// <param name="path"></param>
        [Obsolete("Use TryToDeleteDirectory method instead for better semantic support.")]
        [TechnicalDebt(CategoryType.NamingIssue | CategoryType.DifferentStrategyIssue, "TryToDeleteDirectory")]
        public void DeleteChildren(string path)
        {
            List<string> children = FindChildren(path);
            int childrenLength = children.Count();
            for (int i = 0; i < childrenLength; i++)
            {
                string child = children[i];
                DeleteFileOrFolder(child);
            }
        }

        /// <summary>
        /// Async version of <seealso cref="DeleteChildren(string)"/> method.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        [Obsolete("Use TryToDeleteDirectoryAsync method instead for better semantic support.")]
        [TechnicalDebt(CategoryType.NamingIssue | CategoryType.DifferentStrategyIssue ,  "TryToDeleteDirectoryAsync")]
        public async Task DeleteChildrenAsync(string path)
        {
            // 使用 EnumerateFileSystemEntries 避免一次載入所有路徑到記憶體
            var children = _fileSystem.Directory.EnumerateFileSystemEntries(path);

            var tasks = children.Select(child => DeleteFileOrFolderAsync(child));
            await Task.WhenAll(tasks); // 並行刪除所有子項，速度更快
        }

        /// <summary>
        /// delete a file or folder whose full path is `sourceFileFullPath`.
        /// </summary>
        /// <param name="path"></param>
        [Obsolete("Use DeleteFileSystemItem method instead for better semantic support.")]
        [TechnicalDebt(CategoryType.NamingIssue , "DeleteFileSystemItem")]
        public void DeleteFileOrFolder(string path)
        {
            if(IsDirectory(path))
            {
                DeleteDirectory(path);
            }
            else
            {
                _fileSystem.File.Delete(path);
            }
        }

        /// <summary>
        /// alternative of <seealso cref="DeleteFileOrFolder(string)"/> to provide better semantic support.
        /// </summary>
        /// <param name="path"></param>
        public void DeleteFileSystemItem(string path)
        {
            bool isDirectory = IsDirectory(path);

            if(isDirectory)
            {
                DeleteDirectory(path);
            }
            else
            {
                _fileSystem.File.Delete(path);
            }
        }

        /// <summary>
        /// Async version of <seealso cref="DeleteFileOrFolder(string)"/> method.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        [Obsolete("Use DeleteFileSystemItemAsync method instead for better semantic support.")]
        [TechnicalDebt(CategoryType.NamingIssue , "DeleteFileSystemItemAsync")]
        public async Task DeleteFileOrFolderAsync(string path)
        {
            if(IsDirectory(path))
            {
                await DeleteDirectoryAsync(path);
            }
            else
            {
                // File.Delete 本身沒有非同步版，建議封裝在 Task 中避免 I/O 阻塞
                await Task.Run(() => _fileSystem.File.Delete(path));
            }
        }

        /// <summary>
        /// alternative of <seealso cref="DeleteFileOrFolderAsync(string)"/> to provide better semantic support.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public async Task DeleteFileSystemItemAsync(string path)
        {
            if(IsDirectory(path))
            {
                await DeleteDirectoryAsync(path);
            }
            else
            {
                // File.Delete 本身沒有非同步版，建議封裝在 Task 中避免 I/O 阻塞
                await Task.Run(() => _fileSystem.File.Delete(path));
            }
        }

        /// <summary>
        /// find all children of a directory.
        /// 
        /// find all children of a directory `sourceDirectory`.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        [Obsolete("Use EnumerateChildren method instead for better performance and semantic support.")]
        [TechnicalDebt(CategoryType.NamingIssue , "EnumerateChildren")]
        public List<string> FindChildren(string path)
        {
            // 使用 EnumerateFileSystemEntries 減少記憶體占用
            return _fileSystem.Directory.EnumerateFileSystemEntries(path).ToList();
        }

        /// <summary>
        /// alternative of <seealso cref="FindChildren(string)"/> to provide better performance and semantic support.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public List<string> EnumerateChildren(string path)
        {
            // 使用 EnumerateFileSystemEntries 減少記憶體占用
            return _fileSystem.Directory.EnumerateFileSystemEntries(path).ToList();
        }

        /// <summary>
        /// Async of <seealso cref="FindChildren(string)"/> method.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        [Obsolete("Use EnumerateChildrenAsync method instead for better performance and semantic support.")]
        [TechnicalDebt(CategoryType.NamingIssue , "EnumerateChildrenAsync")]
        public async Task<List<string>> FindChildrenAsync(string path)
        {
            return await Task.Run(() => _fileSystem.Directory.EnumerateFileSystemEntries(path).ToList());
        }

        /// <summary>
        /// alternative of <seealso cref="FindChildrenAsync(string)"/> method to provide better performance and semantic support.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public async Task<List<string>> EnumerateChildrenAsync(string path)
        {
            return await Task.Run(() => _fileSystem.Directory.EnumerateFileSystemEntries(path).ToList());
        }

        /// <summary>
        /// get specific child of a directory.
        /// 
        /// get specific child whose file name is `targetFileName` of a directory `sourceDirectory`.
        /// </summary>
        /// <param name="sourceDirectory"></param>
        /// <param name="targetFileName"></param>
        /// <returns></returns>
        [Obsolete("Use GetChildPath method instead for better semantic support.")]
        [TechnicalDebt(CategoryType.NamingIssue , "GetChildPath")]
        public string GetChild(string sourceDirectory , string targetFileName)
        {
            if(HasChild(sourceDirectory , targetFileName))
            {
                string targetFileFullPath = _fileSystem.Path.Combine(sourceDirectory , targetFileName);
                return targetFileFullPath;
            }

            return null;
        }

        /// <summary>
        /// alternative of <seealso cref="GetChild(string, string)"/> method to provide better semantic support.
        /// </summary>
        /// <param name="sourceDirectory"></param>
        /// <param name="targetFileName"></param>
        /// <returns></returns>
        public string GetChildPath(string sourceDirectory , string targetFileName)
        {
            if(HasChild(sourceDirectory , targetFileName))
            {
                string targetFileFullPath = _fileSystem.Path.Combine(sourceDirectory , targetFileName);
                return targetFileFullPath;
            }

            return null;
        }

        /// <summary>
        /// check a directory has a specific child.
        /// 
        /// check a directory `sourceDirectory` has a specific child whose name is `targetFileName`.
        /// </summary>
        /// <param name="sourceDirectory"></param>
        /// <param name="targetFileName"></param>
        /// <returns></returns>
        public bool HasChild(string sourceDirectory , string targetFileName)
        {
            // 直接在檔案系統層級搜尋，不要抓回整個 List 再找，效能差異極大
            return _fileSystem.Directory.EnumerateFileSystemEntries(sourceDirectory)
                                   .Any(path => _fileSystem.Path.GetFileName(path)
                                   .Equals(targetFileName , StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Async version of <seealso cref="HasChild(string, string)"/> method.
        /// </summary>
        /// <param name="sourceDirectory"></param>
        /// <param name="targetFileName"></param>
        /// <returns></returns>
        public async Task<bool> HasChildAsync(string sourceDirectory , string targetFileName)
        {
            return await Task.Run(() =>
                _fileSystem.Directory.EnumerateFileSystemEntries(sourceDirectory)
                         .Any(path => _fileSystem.Path.GetFileName(path)
                         .Equals(targetFileName , StringComparison.OrdinalIgnoreCase)));
        }

        /// <summary>
        /// Fast to enumerate files with <paramref name="pattern"/>
        /// </summary>
        /// <param name="path">path</param>
        /// <param name="pattern">pattern</param>
        /// <returns></returns>
        public IEnumerable<string> FastEnumerateFiles(
            string path,
            string pattern,
            EnumerationOptions enumerationOptions = default
        ) {
            enumerationOptions = enumerationOptions == default ? _defaultEnumerationOptions : enumerationOptions;

            // EnumerationOptions 提升搜尋效能
            return _fileSystem.Directory.EnumerateFiles(path, pattern, enumerationOptions);
        }

        /// <summary>
        /// Async version of <see cref="FastEnumerateFiles"/> 
        /// </summary>
        /// <param name="path">directory path</param>
        /// <param name="pattern">pattern</param>
        /// <param name="enumerationOptions">enumeration options</param>
        /// <returns></returns>
        public async IAsyncEnumerable<string> FastEnumerateFilesAsync(
            string path,
            string pattern,
            EnumerationOptions enumerationOptions = default
        )
        {
            enumerationOptions = enumerationOptions == default ? _defaultEnumerationOptions : enumerationOptions;

            // We use Task.Run because the underlying System.IO EnumerateFiles 
            // is a blocking iterator. This moves the iteration to a background thread.
            var files = await Task.Run(() => _fileSystem.Directory.EnumerateFiles(
                path,
                pattern,
                enumerationOptions
            ));

            foreach (var file in files)
            {
                yield return file;
            }
        }

        /// <inheritdoc cref="global::IOOperationUtilityServices.DirectoryUtilityService.DeleteDirectory(string)"/>
        public void TryToDeleteDirectory(
            string directoryPath
        )
        {
            if (_fileSystem.Directory.Exists(directoryPath))
            {
                DeleteDirectory(directoryPath);
            }
        }

        /// <summary>
        /// Async version of <see cref="global::IOOperationUtilityServices.DirectoryUtilityService.TryToDeleteDirectory(string)"/>
        /// </summary>
        /// <param name="directoryPath"></param>
        /// <returns></returns>
        public async Task TryToDeleteDirectoryAsync(
            string directoryPath
        )
        {
            if (_fileSystem.Directory.Exists(directoryPath))
            {
                await DeleteDirectoryAsync(directoryPath);
            }
        }


        private void _TryToMoveDirectory(
            string srcPath,
            string targetPath
        )
        {
            srcPath = _fileSystem.Path.GetFullPath(srcPath);
            targetPath = _fileSystem.Path.GetFullPath(targetPath);
    
            if (_fileSystem.Directory.Exists(srcPath))
            {
                if (_fileSystem.Directory.Exists(targetPath))
                {
                    TryToDeleteDirectory(targetPath);
                }
                _fileSystem.Directory.Move(srcPath, targetPath);
            }
        }

        private void _TryToMoveDirectoryInCrossDrive(
            string srcPath,
            string targetPath
        )
        {
            srcPath = _fileSystem.Path.GetFullPath(srcPath);
            targetPath = _fileSystem.Path.GetFullPath(targetPath);

            if (_fileSystem.Directory.Exists(srcPath))
            {
                if (_fileSystem.Directory.Exists(targetPath))
                {
                    TryToDeleteDirectory(targetPath);
                }
                _fileSystem.Directory.CreateDirectory(targetPath);

                // 1. 複製所有檔案
                foreach (var file in _fileSystem.Directory.GetFiles(srcPath))
                {
                    // 檢查是否為 Symbolic link (ReparsePoint)
                    var attributes = _fileSystem.File.GetAttributes(file);
                    if (attributes.HasFlag(FileAttributes.ReparsePoint))
                    {
                        // TODO: 依需求決定要跳過、報錯，還是重新建立 Link
                        var targetInfo = _fileSystem.File.ResolveLinkTarget(file, returnFinalTarget: true);
                        if (targetInfo == null)
                        {
                            throw new IOException($"Failed to resolve symbolic link target for {file}");
                        }
                        var linkPath = file;
                        var symbolicLinkOptions = SymbolicLinkOptionsBuilder.CreateStrict(
                            linkPath,
                            targetPath
                        ).Build();
                        _symbolicLinkUtilityService.Value.TryToUpdateLink(symbolicLinkOptions);
                        continue;
                    }

                    var fileName = _fileSystem.Path.GetFileName(file);
                    var destFile = _fileSystem.Path.Combine(targetPath, fileName);

                    _fileSystem.File.Copy(file, destFile, overwrite: true);

                    // 針對 其他環境 (e.g Unix /MacOs)：嘗試保留原本的權限 (需要 .NET 6+ 且非 Windows)
                    bool isWindows = _environmentService.Value.IsWindows();
                    if (!isWindows)
                    {
                        var mode = _fileSystem.File.GetUnixFileMode(file);
                        _fileSystem.File.SetUnixFileMode(destFile, mode);
                    }
                }

                // 2. 遞迴複製子目錄
                foreach (var folder in _fileSystem.Directory.GetDirectories(srcPath))
                {
                    var folderAttributes = _fileSystem.File.GetAttributes(folder);
                    if (folderAttributes.HasFlag(FileAttributes.ReparsePoint))
                    {
                        // TODO: 依需求決定要跳過、報錯，還是重新建立 Link
                        var targetInfo = _fileSystem.Directory.ResolveLinkTarget(folder, returnFinalTarget: true);
                        if (targetInfo == null)
                        {
                            throw new IOException($"Failed to resolve symbolic link target for {folder}");
                        }
                        var linkPath = folder;
                        var symbolicLinkOptions = SymbolicLinkOptionsBuilder.CreateStrict(
                            linkPath,
                            targetPath
                        ).Build();
                        _symbolicLinkUtilityService.Value.TryToUpdateLink(symbolicLinkOptions);
                        continue;
                    }
                    var folderName = _fileSystem.Path.GetFileName(folder);
                    var destFolder = _fileSystem.Path.Combine(targetPath, folderName);
                    _TryToMoveDirectoryInCrossDrive(folder, destFolder);
                }

                // 3. 確保留底的刪除動作 (防禦性檢查：確認目標真的存在才刪來源)
                if (_fileSystem.Directory.Exists(targetPath))
                {
                    _fileSystem.Directory.Delete(srcPath, recursive: true);
                }
            }
        }

        /// <summary>
        /// Try to move directory from <paramref name="srcPath"/> to <paramref name="targetPath"/>
        /// </summary>
        /// <param name="srcPath">source directory path</param>
        /// <param name="targetPath">target directory path</param>
        public void TryToMoveDirectory(
            string srcPath,
            string targetPath
        )
        {
            // 嚴格守衛語句 (Guard Clauses) - 徹底防禦平行時空吞異常的副作用
            if (srcPath == null)
            {
                throw new ArgumentNullException(nameof(srcPath), "Source path cannot be null.");
            }
            if (string.IsNullOrWhiteSpace(srcPath))
            {
                throw new ArgumentException("Source path cannot be empty or consist only of white-space characters.", nameof(srcPath));
            }
            if (targetPath == null)
            {
                throw new ArgumentNullException(nameof(targetPath), "Target path cannot be null.");
            }
            if (string.IsNullOrWhiteSpace(targetPath))
            {
                throw new ArgumentException("Target path cannot be empty or consist only of white-space characters.", nameof(targetPath));
            }

            if (_fileSystem.Directory.Exists(srcPath))
            {
                if(_driveInfoUtilityService.Value.IsCrossDrive(srcPath, targetPath))
                {
                    _TryToMoveDirectoryInCrossDrive(srcPath, targetPath);
                }
                else
                {
                    _TryToMoveDirectory(srcPath, targetPath);
                }
            }
        }

        /// <summary>
        /// Async version of <see cref="global::IOOperationUtilityServices.DirectoryUtilityService.TryToMoveDirectory(string, string)"/>
        /// </summary>
        /// <param name="srcPath"></param>
        /// <param name="targetPath"></param>
        /// <returns></returns>
        public async Task TryToMoveDirectoryAsync(
            string srcPath,
            string targetPath
        )
        {
            if (_fileSystem.Directory.Exists(srcPath))
            {
                await Task.Run(() => TryToMoveDirectory(srcPath, targetPath));
            }
        }
    }
}
