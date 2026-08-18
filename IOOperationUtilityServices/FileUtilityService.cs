using CustomDataAnnotations.Maintenance;
using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.Text;

namespace IOOperationUtilityServices
{
    /// <summary>
    /// Utility class to handling files
    /// </summary>
    public class FileUtilityService: IFileUtilityService
    {
        private readonly IFileSystem _fileSystem;
        private readonly Lazy<IDirectoryUtilityService> _directoryUtilityService;

        public IFileSystem FileSystem => _fileSystem;

        public FileUtilityService(
            IFileSystem fileSystem,
            Lazy<IDirectoryUtilityService> directoryUtilityService
        )
        {
            ArgumentNullException.ThrowIfNull(fileSystem , nameof(fileSystem));
            ArgumentNullException.ThrowIfNull(directoryUtilityService , nameof(directoryUtilityService));
            _fileSystem = fileSystem;
            _directoryUtilityService = directoryUtilityService;
        }

        public bool FileExists(string filename) => _fileSystem.File.Exists(filename);

        /// <summary>
        /// copy file from `sourceFileFullPath` to `destinationFileFullPath` (if `destinationFileFullPath` does not exist, it will be created).
        /// 
        /// > [!NOTE]
        /// > NOTE that 
        /// >
        /// > When `destinationFileFullPath` does exist, the file will be overwritten.
        /// 
        /// > [!CAUTION]
        /// >
        /// > `sourceFileFullPath` must be a full path of file.
        /// >
        /// > If `sourceFileFullPath` is not a file (such as it is a directory), it will throw exception.
        /// </summary>
        /// <param name="sourceFileFullPath"></param>
        /// <param name="destinationFileFullPath"></param>
        /// <exception cref="_fileSystem.FileNotFoundException">When `sourceFileFullPath` does NOT exist or it is not a file, it will throw Exception</exception>
        public void Copy(
            string sourceFileFullPath,
            string destinationFileFullPath
        )
        {
            // 檢查來源是否存在，避免 File.Copy 丟出不夠明確的異常
            if(!FileExists(sourceFileFullPath))
            {
                throw new FileNotFoundException("Source file not found." , sourceFileFullPath);
            }

            // 確保目的地的資料夾路徑存在
            string destDirectory = _fileSystem.Path.GetDirectoryName(destinationFileFullPath);
            if(!string.IsNullOrEmpty(destDirectory) && !_fileSystem.Directory.Exists(destDirectory))
            {
                _fileSystem.Directory.CreateDirectory(destDirectory);
            }

            // 直接 Copy，overwrite: true 會處理覆寫，若不存在也會自動建立
            _fileSystem.File.Copy(sourceFileFullPath , destinationFileFullPath , true);
        }

        /// <summary>
        /// Async version of <seealso cref="global::IOOperationUtilityServices.FileUtilityService.Copy"/> method.
        /// </summary>
        public async Task CopyAsync(
            string sourceFileFullPath,
            string destinationFileFullPath
        )
        {
            if(!_fileSystem.File.Exists(sourceFileFullPath))
            {
                throw new FileNotFoundException("Source file not found." , sourceFileFullPath);
            }

            string destDirectory = _fileSystem.Path.GetDirectoryName(destinationFileFullPath);
            if(!string.IsNullOrEmpty(destDirectory) && !_fileSystem.Directory.Exists(destDirectory))
            {
                _fileSystem.Directory.CreateDirectory(destDirectory);
            }

            // 使用 FileStream 進行非同步讀寫
            using(var sourceStream = _fileSystem.FileStream.New(sourceFileFullPath , FileMode.Open , FileAccess.Read , FileShare.Read , bufferSize: 4096 , useAsync: true))
            using(var destinationStream = _fileSystem.FileStream.New(destinationFileFullPath , FileMode.Create , FileAccess.Write , FileShare.None , bufferSize: 4096 , useAsync: true))
            {
                await sourceStream.CopyToAsync(destinationStream);
            }
        }

        /// <summary>
        /// 轉換成長路徑 (for .NET Framework 4.8.2 及以下版本)，以支援超過 260 字元的路徑。
        /// </summary>
        /// <param name="path">檔案(絕對)路徑</param>
        /// <returns>長路徑</returns>
        /// <remarks>
        /// 此方法名稱不夠精確，建議改用 <seealso cref="ToLongPath"/> 以獲得更好的語意支持。
        /// </remarks>
        [Obsolete("Use ToLongPath method instead for better semantic support.")]
        [TechnicalDebt(CategoryType.NamingIssue , "ToLongPath")]
        public string GetSafeLongPath(string path)
        {
            if(string.IsNullOrEmpty(path))
            {
                return path;
            }

            // 1. 取得絕對路徑（這會處理掉 . 與 ..）
            // 注意：Path.GetFullPath 在 .NET 4.6.2 以前的版本本身也受 260 字元限制
            string fullPath = _fileSystem.Path.GetFullPath(path);

            // 2. 如果已經是長路徑前綴，直接回傳
            if(fullPath.StartsWith(@"\\?\"))
            {
                return fullPath;
            }

            // 3. 處理網路路徑 (UNC)
            // 範例: \\Server\Share -> \\?\UNC\Server\Share
            if(fullPath.StartsWith(@"\\"))
            {
                return @"\\?\UNC\" + fullPath.Substring(2);
            }

            // 4. 處理本地路徑
            // 範例: C:\Folder -> \\?\C:\Folder
            return @"\\?\" + fullPath;
        }

        /// <summary>
        /// alternative method name for <seealso cref="GetSafeLongPath"/> to provide better semantic support.
        /// </summary>
        /// <param name="path">檔案(絕對)路徑</param>
        /// <returns>長路徑</returns>
        public string ToLongPath(string path)
        {
            if(string.IsNullOrEmpty(path))
            {
                return path;
            }

            // 1. 取得絕對路徑（這會處理掉 . 與 ..）
            // 注意：Path.GetFullPath 在 .NET 4.6.2 以前的版本本身也受 260 字元限制
            string fullPath = _fileSystem.Path.GetFullPath(path);

            // 2. 如果已經是長路徑前綴，直接回傳
            if(fullPath.StartsWith(@"\\?\"))
            {
                return fullPath;
            }

            // 3. 處理網路路徑 (UNC)
            // 範例: \\Server\Share -> \\?\UNC\Server\Share
            if(fullPath.StartsWith(@"\\"))
            {
                return @"\\?\UNC\" + fullPath.Substring(2);
            }

            // 4. 處理本地路徑
            // 範例: C:\Folder -> \\?\C:\Folder
            return @"\\?\" + fullPath;
        }

        /// <summary>
        /// 針對不存在的檔案路徑，嘗試建立其父目錄在建立空白檔案。
        /// 
        /// 針對已存在的檔案，將該檔案內容清空。
        /// </summary>
        /// <param name="filePath">檔案(絕對)路徑</param>
        /// <returns>成功狀態</returns>
        /// <remarks>
        /// 此方法名稱不夠精確，建議改用 <seealso cref="CreateOrClearFile"/> 以獲得更好的語意支持。
        /// </remarks>
        [Obsolete("Use CreateOrClearFile method instead for better semantic support.")]
        [TechnicalDebt(CategoryType.NamingIssue , "CreateOrClearFile")]

        public  bool TryToOverwriteFile(string filePath)
        {
            try
            {
                if(string.IsNullOrEmpty(filePath))
                {
                    throw new ArgumentNullException($"引數錯誤。檔案路徑'{filePath}'為null或empty。無法判斷檔案的存在性和建立檔案");
                }

                // 取得安全路徑(針對超長路徑)
                if(FileExists(filePath)) //如果檔案存在
                {
                    //將空字串寫入檔案，以達到清空檔案的效果
                    _fileSystem.File.WriteAllText(filePath , string.Empty);
                }
                //如果檔案不存在
                string? directoryPath = _fileSystem.Path.GetDirectoryName(filePath);
                if(string.IsNullOrEmpty(directoryPath))
                {
                    throw new ArgumentNullException($"引數錯誤。'{filePath}'的父目錄為null或empty。無法判斷目錄的存在性和建立目錄");
                }
                //如果目錄已存在，此行不會有負面影響；如果不存在，會連同父目錄一起建立
                _fileSystem.Directory.CreateDirectory(directoryPath);
                //將空字串寫入檔案，以達到建立空白檔案的效果
                _fileSystem.File.WriteAllText(filePath , string.Empty);
                return true;
            }
            catch(Exception ex)
            {
                // Logic for Exception handling
                // ...
                throw;
            }
        }
        

        /// <summary>
        /// alternative method name for <seealso cref="TryToOverwriteFile"/> to provide better semantic support.
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns></returns>
        public bool CreateOrClearFile(string filePath)
        {
            try
            {
                if (string.IsNullOrEmpty(filePath))
                {
                    throw new ArgumentNullException($"引數錯誤。檔案路徑'{filePath}'為null或empty。無法判斷檔案的存在性和建立檔案");
                }

                // 取得安全路徑(針對超長路徑)
                if (FileExists(filePath)) //如果檔案存在
                {
                    //將空字串寫入檔案，以達到清空檔案的效果
                    _fileSystem.File.WriteAllText(filePath, string.Empty);
                }
                //如果檔案不存在
                string? directoryPath = _fileSystem.Path.GetDirectoryName(filePath);
                if (string.IsNullOrEmpty(directoryPath))
                {
                    throw new ArgumentNullException($"引數錯誤。'{filePath}'的父目錄為null或empty。無法判斷目錄的存在性和建立目錄");
                }
                //如果目錄已存在，此行不會有負面影響；如果不存在，會連同父目錄一起建立
                _fileSystem.Directory.CreateDirectory(directoryPath);
                //將空字串寫入檔案，以達到建立空白檔案的效果
                _fileSystem.File.WriteAllText(filePath, string.Empty);
                return true;
            }
            catch (Exception ex)
            {
                // Logic for Exception handling
                // ...
                throw;
            }
        }

        /// <summary>
        /// Try to delete a file located at <paramref name="filePath"/>
        /// </summary>
        /// <param name="filePath">file path</param>
        public void TryToDeleteFile(
            string filePath
        )
        {
            if (_fileSystem.File.Exists(filePath))
            {
                _fileSystem.File.Delete(filePath);
            }
        }

        /// <summary>
        /// Async version of <seealso cref="TryToDeleteFile"/> method.
        /// </summary>
        public async Task TryToDeleteFileAsync(
            string filePath
        )
        {
            if (_fileSystem.File.Exists(filePath))
            {
                await Task.Run(() => _fileSystem.File.Delete(filePath)).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// For files matching the <paramref name="pattern"/> under <paramref name="rootPath"/>
        /// Do taskes.
        /// </summary>
        /// <param name="rootPath">root path</param>
        /// <param name="pattern">glob pattern for matching</param>
        /// <param name="callback">callback</param>
        /// <returns></returns>

        public void ProcessFiles(
            string rootPath,
            string pattern,
            EnumerationOptions enumerationOptions,
            Action<string> callback
        )
        {
            var files = _directoryUtilityService.Value.FastEnumerateFiles(rootPath, pattern,enumerationOptions);

            foreach (string file in files)
            {
                callback.Invoke(file);
            }
        }

        /// <summary>
        /// Async version of <see cref="global::IOOperationUtilityServices.FileUtilityService.ProcessFiles(string, string, Func{string, Task})"/>
        /// </summary>
        /// <param name="rootPath"></param>
        /// <param name="pattern"></param>
        /// <param name="callback"></param>
        /// <returns></returns>
        public async Task ProcessFilesAsync(
            string rootPath,
            string pattern,
            EnumerationOptions enumerationOptions,
            Action<string> callback
        )
        {
            IAsyncEnumerable<string> files = _directoryUtilityService.Value.FastEnumerateFilesAsync(rootPath, pattern,enumerationOptions);

            await foreach (string file in files)
            {
                // 邊找邊處理，不會卡死執行緒
                callback.Invoke(file);
            }
        }

        /// <summary>
        /// Backup files from <paramref name="filePath"/> to <paramref name="filePath"/> appends the file extension <paramref name="backupExtension"/>
        /// </summary>
        /// <param name="filePath">file path that will be backupped</param>
        /// <param name="backupExtension">extension for backup (e.g. `.bak`)</param>
        public void BackupFile(
            string filePath,
            string backupExtension
        )
        {
            var backupPath = filePath + backupExtension;

            // Overwrite existing backup if a previous aborted run left artifacts behind
            TryToDeleteFile(backupPath);

            Copy(filePath, backupPath);
        }

        /// <summary>
        /// Async version of <see cref="global::IOOperationUtilityServices.FileUtilityService.BackupFile(string, string)"/>
        /// </summary>
        /// <param name="filePath"></param>
        /// <param name="backupExtension">file extension of backup</param>
        /// <returns></returns>
        public async Task BackupFileAsync(
            string filePath,
            string backupExtension
        )
        {
            var backupPath = filePath + backupExtension;

            // Overwrite existing backup if a previous aborted run left artifacts behind
            await TryToDeleteFileAsync(backupPath);

            await CopyAsync(filePath, backupPath);
        }
        
        /// <summary>
        /// Backup lots of files that are matched <paramref name="pattern"/> under <paramref name="rootPath"/> to 
        /// same location but appended extra file extension <paramref name="backupExtension"/> 
        /// </summary>
        /// <param name="rootPath">root directory path</param>
        /// <param name="pattern">glob pattern to match entries</param>
        /// <param name="backupExtension">extension for backup (e.g. `.bak`)</param>
        /// <param name="searchOption">search option</param>
        public void BackupFiles(
            string rootPath,
            string pattern,
            string backupExtension,
            EnumerationOptions enumerationOptions = default
        )
        {
            ProcessFiles(
                rootPath,
                pattern,
                enumerationOptions,
                (file) => BackupFile(file, backupExtension)
            );
        }

        /// <summary>
        /// Async version of <see cref="global::IOOperationUtilityServices.FileUtilityService.BackupFiles(string, string, string, EnumerationOptions)"/> method.
        /// </summary>
        public async Task BackupFilesAsync(
            string rootPath,
            string pattern,
            string backupExtension,
            EnumerationOptions enumerationOptions = default
        )
        {
            await ProcessFilesAsync(
                rootPath,
                pattern,
                enumerationOptions,
                async (file) => await BackupFileAsync(file, backupExtension)
            ).ConfigureAwait(false);
        }

        /// <summary>
        /// Clean generated file during migration 
        /// </summary>
        /// <param name="rootPath">root directory path</param>
        /// <param name="pattern">pattern used as file extension of backup</param>
        /// <param name="enumerationOptions"><see cref="global::System.IO.Abstractions.IFileSystem.Directory.EnumerationOptions"/></param>
        public void CleanupMigrationFiles(
            string rootPath,
            string pattern,
            EnumerationOptions enumerationOptions = default
        )
        {
            var backupFiles = _fileSystem.Directory.GetFiles(rootPath, pattern, enumerationOptions);
            foreach (var backup in backupFiles)
            {
                var originalPath = backup.Replace(pattern, string.Empty);

                if (_fileSystem.File.Exists(originalPath))
                {
                    _fileSystem.File.Delete(originalPath);
                }

                _fileSystem.File.Move(backup, originalPath);
            }
        }

        /// <summary>
        /// Async version of <seealso cref="CleanupMigrationFile"/> method.
        /// </summary>
        public async Task CleanupMigrationFilesAsync(
            string rootPath,
            string pattern,
            EnumerationOptions enumerationOptions = default
        )
        {
            var backupFiles = _directoryUtilityService.Value.FastEnumerateFilesAsync(rootPath, pattern, enumerationOptions);
            await foreach (var backup in backupFiles)
            {
                var originalPath = backup.Replace(pattern, string.Empty);

                if (_fileSystem.File.Exists(originalPath))
                {
                    _fileSystem.File.Delete(originalPath);
                }

                // File.Move 在舊版 .NET 沒有非同步，這裡可以用 Task.Run 封裝確保不阻塞 Thread，或在 .NET Core 直接調用 Move (若抽象層有支援)
                await Task.Run(() => _fileSystem.File.Move(backup, originalPath)).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Rollback migration (usually manually triggers when it fails)
        /// </summary>
        /// <param name="rootPath">root directory path</param>
        /// <param name="pattern">pattern used as file extension of backup</param>
        /// <param name="generatedFilePath">path of expected generated file after migration (if success)</param>
        /// <param name="enumerationOptions"><see cref="global::System.IO.Abstractions.IFileSystem.Directory.EnumerationOptions"/></param>
        public void RollbackMigration(
            string rootPath,
            string pattern,
            string generatedFilePath,
            EnumerationOptions enumerationOptions = default
        )
        {
            CleanupMigrationFiles(rootPath, pattern, enumerationOptions);
            TryToDeleteFile(generatedFilePath);
        }
        
        /// <summary>
        /// Async version of <seealso cref="RollbackMigration"/> method.
        /// </summary>
        public async Task RollbackMigrationAsync(
            string rootPath,
            string pattern,
            string generatedFilePath,
            EnumerationOptions enumerationOptions = default
        )
        {
            await CleanupMigrationFilesAsync(rootPath, pattern, enumerationOptions).ConfigureAwait(false);
            await TryToDeleteFileAsync(generatedFilePath).ConfigureAwait(false);
        }
        
    }
}
