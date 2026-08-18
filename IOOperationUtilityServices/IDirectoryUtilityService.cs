using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.Text;
using CustomDataAnnotations.Maintenance;

namespace IOOperationUtilityServices
{
    public interface IDirectoryUtilityService
    {
        IFileSystem FileSystem { get; }
        public bool IsDirectory(string sourceFileFullPath);
        public bool IsFile(string sourceFileFullPath);

        [Obsolete("Use AnyExists method instead for better semantic support.")]
        [TechnicalDebt(CategoryType.NamingIssue, "AnyExists")]

        bool Exists(string sourceFileFullPath);
        bool AnyExists(string sourceFileFullPath);

        [Obsolete("Use CopyFileToDirectory method instead for better semantic support.")]
        [TechnicalDebt(CategoryType.NamingIssue, "CopyFileToDirectory")]
        void AddChild(
            string sourceFileFullPath,
            string destinationDirectory,
            string childName
        );
        void CopyFileToDirectory(
            string sourceFileFullPath,
            string destinationDirectory,
            string childName
        );

        [Obsolete("Use CopyFileToDirectoryAsync method instead for better semantic support.")]
        [TechnicalDebt(CategoryType.NamingIssue, "CopyFileToDirectoryAsync")]
        Task AddChildAsync(
            string sourceFileFullPath,
            string destinationDirectory,
            string childName
        );

        Task CopyToDirectoryAsync(
            string sourceFileFullPath,
            string destinationDirectory,
            string childName
        );

        void DeleteDirectory(string sourceDirectory);
        Task DeleteDirectoryAsync(string sourceDirectory);

        void DeleteChild(
            string sourceDirectory,
            string childName
        );

        Task DeleteChildAsync(
            string sourceDirectory,
            string childName
        );

        [Obsolete("Use TryToDeleteDirectory method instead for better semantic support.")]
        [TechnicalDebt(CategoryType.NamingIssue | CategoryType.DifferentStrategyIssue, "TryToDeleteDirectory")]
        void DeleteChildren(string sourceDirectory);

        void TryToDeleteDirectory(
            string directoryPath
        );
        
        [Obsolete("Use TryToDeleteDirectoryAsync method instead for better semantic support.")]
        [TechnicalDebt(CategoryType.NamingIssue | CategoryType.DifferentStrategyIssue, "TryToDeleteDirectoryAsync")]
        Task DeleteChildrenAsync(string sourceDirectory);

        Task TryToDeleteDirectoryAsync(
            string directoryPath
        );

        [Obsolete("Use DeleteFileSystemItem method instead for better semantic support.")]
        [TechnicalDebt(CategoryType.NamingIssue, "DeleteFileSystemItem")]
        void DeleteFileOrFolder(string sourceFileFullPath);

        [Obsolete("Use DeleteFileSystemItemAsync method instead for better semantic support.")]
        [TechnicalDebt(CategoryType.NamingIssue, "DeleteFileSystemItemAsync")]
        Task DeleteFileOrFolderAsync(string path);
        void DeleteFileSystemItem(string sourceFileFullPath);

        Task DeleteFileSystemItemAsync(string path);

        [Obsolete("Use EnumerateChildren method instead for better performance and semantic support.")]
        [TechnicalDebt(CategoryType.NamingIssue, "EnumerateChildren")]
        List<string> FindChildren(string sourceDirectory);
        List<string> EnumerateChildren(string sourceDirectory);

        Task<List<string>> EnumerateChildrenAsync(string sourceDirectory);

        [Obsolete("Use GetChildPath method instead for better semantic support.")]
        [TechnicalDebt(CategoryType.NamingIssue, "GetChildPath")]
        string GetChild(
            string sourceDirectory,
            string targetFileName
        );
        string GetChildPath(
            string sourceDirectory,
            string targetFileName
        );

        bool HasChild(
            string sourceDirectory,
            string targetFileName
        );

        Task<bool> HasChildAsync(
            string sourceDirectory,
            string targetFileName
        );

        IEnumerable<string> FastEnumerateFiles(
            string path,
            string pattern,
            EnumerationOptions enumerationOptions = default
        );

        IAsyncEnumerable<string> FastEnumerateFilesAsync(
            string path,
            string pattern,
            EnumerationOptions enumerationOptions = default
        );

        void TryToMoveDirectory(
            string srcPath,
            string targetPath
        );

        Task TryToMoveDirectoryAsync(
            string srcPath,
            string targetPath
        );
    }
}
