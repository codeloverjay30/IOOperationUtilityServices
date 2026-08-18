using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.Text;
using CustomDataAnnotations.Maintenance;

namespace IOOperationUtilityServices
{
    public interface IFileUtilityService
    {
        IFileSystem FileSystem { get; }

        bool FileExists(string filename);
        void Copy(
            string sourceFileFullPath,
            string destinationFileFullPath
        );
        Task CopyAsync(
            string sourceFileFullPath,
            string destinationFileFullPath
        );

        [Obsolete("Use ToLongPath method instead for better semantic support.")]
        [TechnicalDebt(CategoryType.NamingIssue, "ToLongPath")]
        string GetSafeLongPath(string path);
        string ToLongPath(string path);
        bool CreateOrClearFile(string filePath);

        void TryToDeleteFile(
            string filePath
        );

        Task TryToDeleteFileAsync(
            string filePath
        );

        void ProcessFiles(
           string rootPath,
           string pattern,
           EnumerationOptions enumerationOptions,
           Action<string> callback
       );

        Task ProcessFilesAsync(
             string rootPath,
             string pattern,
             EnumerationOptions enumerationOptions,
             Action<string> callback
         );

        void BackupFile(
           string filePath,
           string backupExtensoion
       );
        Task BackupFileAsync(
            string filePath,
            string backupExtensoion
        );

        void BackupFiles(
            string rootPath,
            string pattern,
            string backupExtensoion,
            EnumerationOptions enumerationOptions = default
        );

        Task BackupFilesAsync(
            string rootPath,
            string pattern,
            string backupExtensoion,
            EnumerationOptions enumerationOptions = default
        );

        void CleanupMigrationFiles(
            string rootPath,
            string pattern,
            EnumerationOptions enumerationOptions = default
        );

        Task CleanupMigrationFilesAsync(
            string rootPath,
            string pattern,
            EnumerationOptions enumerationOptions = default
        );

        void RollbackMigration(
            string rootPath,
            string pattern,
            string generatedFilePath,
            EnumerationOptions enumerationOptions = default
        );

        Task RollbackMigrationAsync(
            string rootPath,
            string pattern,
            string generatedFilePath,
            EnumerationOptions enumerationOptions = default
        );
    }
}
