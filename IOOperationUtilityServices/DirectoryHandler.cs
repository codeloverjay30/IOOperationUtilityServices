using System;
using System.Collections.Generic;
using System.Text;
using CustomDataAnnotations.Maintenance;

namespace IOOperationUtilityServices
{
    /// <summary>
    /// utility class that handles about directory
    /// </summary>
    /// <remarks>
    /// This utility class uses non-static class (e.g. `System.IO.Path`,
    /// which can't perform DI and mock
    /// because it uses `System.IO.Path` etc
    /// so that it needs to perform I/O operation on OS.
    /// Thus, use <seealso cref="DirectoryUtilityService"/> class instead.
    /// And it is obsolete so that it will no longer be maintained.
    /// </remarks>
    [Obsolete("use <seealso cref=\"DirectoryUtilityService\"/> class instead")]
    [TechnicalDebt(CategoryType.OtherIssue, "DirectoryUtilityService")]
    public static class DirectoryHandler
    {
        /// <summary>
        /// return true iff `sourceFileFullPath` exists and it is a directory. (validate by `System.IO.FileAttributes`)
        /// </summary>
        /// <param name="sourceFileFullPath"></param>
        /// <returns></returns>
        public static bool IsDirectory(string sourceFileFullPath)
        {
            if(!System.IO.File.Exists(sourceFileFullPath) && !System.IO.Directory.Exists(sourceFileFullPath))
            {
                return false;
            }
            return System.IO.File.GetAttributes(sourceFileFullPath).HasFlag(System.IO.FileAttributes.Directory);
        }

        /// <summary>
        /// return true iff `sourceFileFullPath` exists and it is a file. (validate by `System.IO.FileAttributes`)
        /// 
        /// > [!NOTE]
        /// > it is not equivalent to negation of `IsDirectory` method call.  
        /// </summary>
        /// <param name="sourceFileFullPath"></param>
        /// <returns></returns>
        public static bool IsFile(string sourceFileFullPath)
        {
            if(!System.IO.File.Exists(sourceFileFullPath) && !System.IO.Directory.Exists(sourceFileFullPath))
            {
                return false;
            }
            return !System.IO.File.GetAttributes(sourceFileFullPath).HasFlag(System.IO.FileAttributes.Directory);
        }

        /// <summary>
        /// return true iff `sourceFileFullPath` is a directory or file. 
        /// </summary>
        /// <param name="sourceFileFullPath"></param>
        /// <returns></returns>
        /// <remarks>
        /// Use <seealso cref="AnyExists"/> method instead as this method name is misleading and does not accurately reflect its functionality.
        /// </remarks>
        [Obsolete("Use AnyExists method instead as this method name is misleading and does not accurately reflect its functionality.")]
        [TechnicalDebt(CategoryType.NamingIssue ,"AnyExists")]

        public static bool Exists(string sourceFileFullPath) => System.IO.Directory.Exists(sourceFileFullPath) || System.IO.File.Exists(sourceFileFullPath);

        /// <summary>
        /// return true iff `sourceFileFullPath` is a directory or file. 
        /// </summary>
        /// <param name="sourceFileFullPath"></param>
        /// <returns></returns>
        public static bool AnyExists(string sourceFileFullPath) => System.IO.Directory.Exists(sourceFileFullPath) || System.IO.File.Exists(sourceFileFullPath);

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
        [TechnicalDebt(CategoryType.NamingIssue ,"CopyFileToDirectory")]

        public static void AddChild(
            string sourceFileFullPath ,
            string destinationDirectory ,
            string childName
        )
        {
            string destinationFileFullPath = System.IO.Path.Combine(destinationDirectory , childName);

            bool exists = System.IO.Directory.Exists(destinationDirectory);
            if(!exists)
            {
                System.IO.Directory.CreateDirectory(destinationDirectory);
            }

            FileHandler.Copy(sourceFileFullPath , destinationFileFullPath);

            return;
        }

        /// <summary>
        /// alternative method name for <seealso cref="AddChild(string, string, string)"/> to provide better semantic support.
        /// </summary>
        /// <param name="sourceFileFullPath"></param>
        /// <param name="destinationDirectory"></param>
        /// <param name="childName"></param>
        public static void CopyFileToDirectory(
            string sourceFileFullPath ,
            string destinationDirectory ,
            string childName
        )
        {
            string destinationFileFullPath = System.IO.Path.Combine(destinationDirectory , childName);

            bool exists = System.IO.Directory.Exists(destinationDirectory);
            if(!exists)
            {
                System.IO.Directory.CreateDirectory(destinationDirectory);
            }

            FileHandler.Copy(sourceFileFullPath , destinationFileFullPath);

            return;
        }

        /// <summary>
        /// Async version of <seealso cref="AddChild(string, string, string)"/> method.
        /// </summary>
        /// <param name="sourceFileFullPath"></param>
        /// <param name="destinationDirectory"></param>
        /// <param name="childName"></param>
        /// <returns></returns>
        [Obsolete("AddChildAsync method. Use CopyFileToDirectoryAsync method instead for better semantic support.")]
        [TechnicalDebt(CategoryType.NamingIssue ,"CopyFileToDirectoryAsync")]
        public static async Task AddChildAsync(
            string sourceFileFullPath ,
            string destinationDirectory ,
            string childName
        )
        {
            string destinationFileFullPath = Path.Combine(destinationDirectory , childName);

            if(!Directory.Exists(destinationDirectory))
            {
                Directory.CreateDirectory(destinationDirectory);
            }

            await FileHandler.CopyAsync(sourceFileFullPath , destinationFileFullPath);
        }

        /// <summary>
        /// alternative of <seealso cref="AddChildAsync(string, string, string)"/>.
        /// </summary>
        /// <param name="sourceFileFullPath"></param>
        /// <param name="destinationDirectory"></param>
        /// <param name="childName"></param>
        /// <returns></returns>
        public static async Task CopyToDirectoryAsync(
            string sourceFileFullPath ,
            string destinationDirectory ,
            string childName
        )
        {
            string destinationFileFullPath = Path.Combine(destinationDirectory , childName);

            if(!Directory.Exists(destinationDirectory))
            {
                Directory.CreateDirectory(destinationDirectory);
            }

            await FileHandler.CopyAsync(sourceFileFullPath , destinationFileFullPath);
        }

        /// <summary>
        /// delete directory.
        /// 
        /// delete all children of `sourceDirectory`,
        /// 
        /// then delete the folder `sourceDirectory`
        /// </summary>
        /// <param name="sourceDirectory"></param>
        public static void DeleteDirectory(string sourceDirectory)
        {
            // 優化刪除邏輯：直接利用 .NET 內建的遞迴刪除
            if(System.IO.Directory.Exists(sourceDirectory))
            {
              System.IO.Directory.Delete(sourceDirectory , true); // true 代表遞迴刪除所有內容
            }
        }

        /// <summary>
        /// Async version of <seealso cref="DeleteDirectory(string)"/> method.
        /// </summary>
        /// <param name="sourceDirectory"></param>
        /// <returns></returns>
        public static Task DeleteDirectoryAsync(string sourceDirectory)
        {
            return Task.Run(() =>
            {
                if(Directory.Exists(sourceDirectory))
                {
                    Directory.Delete(sourceDirectory , true);
                }
            });
        }

        /// <summary>
        /// delete specific child of a directory.
        /// 
        /// delete specific child whose name is `childName` of a directory `sourceDirectory`.
        /// </summary>
        /// <param name="sourceDirectory"></param>
        /// <param name="childName"></param>
        public static void DeleteChild(string sourceDirectory , string childName)
        {
            string fileFullPath = System.IO.Path.Combine(sourceDirectory , childName);
            DirectoryHandler.DeleteFileOrFolder(fileFullPath);
        }

        /// <summary>
        /// delete all children of a directory.
        /// 
        /// delete all children of a directory `sourceDirectory`.
        /// </summary>
        /// <param name="sourceDirectory"></param>
        public static void DeleteChildren(string sourceDirectory)
        {
            List<string> children = DirectoryHandler.FindChildren(sourceDirectory);
            int childrenLength = children.Count();
            for(int i = 0; i < childrenLength; i++)
            {
                string child = children [ i ];
                DirectoryHandler.DeleteFileOrFolder(child);
            }
        }

        /// <summary>
        /// Async version of <seealso cref="DeleteChildren(string)"/> method.
        /// </summary>
        /// <param name="sourceDirectory"></param>
        /// <returns></returns>
        public static async Task DeleteChildrenAsync(string sourceDirectory)
        {
            // 使用 EnumerateFileSystemEntries 避免一次載入所有路徑到記憶體
            var children = Directory.EnumerateFileSystemEntries(sourceDirectory);

            var tasks = children.Select(child => DeleteFileOrFolderAsync(child));
            await Task.WhenAll(tasks); // 並行刪除所有子項，速度更快
        }

        /// <summary>
        /// delete a file or folder whose full path is `sourceFileFullPath`.
        /// </summary>
        /// <param name="sourceFileFullPath"></param>
        [Obsolete("DeleteFileOrFolder method is deprecated. Use DeleteFileSystemItem method instead for better semantic support.")]
        [TechnicalDebt(CategoryType.NamingIssue ,"DeleteFileSystemItem")]
        public static void DeleteFileOrFolder(string sourceFileFullPath)
        {
            bool isDirectory = DirectoryHandler.IsDirectory(sourceFileFullPath);

            if(isDirectory)
            {
                DirectoryHandler.DeleteDirectory(sourceFileFullPath);
            }
            else
            {
                System.IO.File.Delete(sourceFileFullPath);
            }
        }

        /// <summary>
        /// alternative of <seealso cref="DeleteFileOrFolder(string)"/> to provide better semantic support.
        /// </summary>
        /// <param name="sourceFileFullPath"></param>
        public static void DeleteFileSystemItem(string sourceFileFullPath)
        {
            bool isDirectory = DirectoryHandler.IsDirectory(sourceFileFullPath);

            if(isDirectory)
            {
                DirectoryHandler.DeleteDirectory(sourceFileFullPath);
            }
            else
            {
                System.IO.File.Delete(sourceFileFullPath);
            }
        }

        /// <summary>
        /// Async version of <seealso cref="DeleteFileOrFolder(string)"/> method.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        [Obsolete("DeleteFileOrFolderAsync method is deprecated. Use DeleteFileSystemItemAsync method instead for better semantic support.")]
        [TechnicalDebt(CategoryType.NamingIssue ,"DeleteFileSystemItemAsync")]
        public static async Task DeleteFileOrFolderAsync(string path)
        {
            if(IsDirectory(path))
            {
                await DeleteDirectoryAsync(path);
            }
            else
            {
                // File.Delete 本身沒有非同步版，建議封裝在 Task 中避免 I/O 阻塞
                await Task.Run(() => File.Delete(path));
            }
        }

        /// <summary>
        /// alternative of <seealso cref="DeleteFileOrFolderAsync(string)"/> to provide better semantic support.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static async Task DeleteFileSystemItemAsync(string path)
        {
            if(IsDirectory(path))
            {
                await DeleteDirectoryAsync(path);
            }
            else
            {
                // File.Delete 本身沒有非同步版，建議封裝在 Task 中避免 I/O 阻塞
                await Task.Run(() => File.Delete(path));
            }
        }

        /// <summary>
        /// find all children of a directory.
        /// 
        /// find all children of a directory `sourceDirectory`.
        /// </summary>
        /// <param name="sourceDirectory"></param>
        /// <returns></returns>
        [Obsolete("FindChildren method is deprecated. Use EnumerateChildren method instead for better performance and semantic support.")]
        [TechnicalDebt(CategoryType.NamingIssue ,"EnumerateChildren")]
        public static List<string> FindChildren(string sourceDirectory)
        {
            // 使用 EnumerateFileSystemEntries 減少記憶體占用
            return System.IO.Directory.EnumerateFileSystemEntries(sourceDirectory).ToList();
        }

        /// <summary>
        /// alternative of <seealso cref="FindChildren(string)"/> to provide better performance and semantic support.
        /// </summary>
        /// <param name="sourceDirectory"></param>
        /// <returns></returns>
        public static List<string> EnumerateChildren(string sourceDirectory)
        {
            // 使用 EnumerateFileSystemEntries 減少記憶體占用
            return System.IO.Directory.EnumerateFileSystemEntries(sourceDirectory).ToList();
        }

        /// <summary>
        /// Async of <seealso cref="FindChildren(string)"/> method.
        /// </summary>
        /// <param name="sourceDirectory"></param>
        /// <returns></returns>
        [Obsolete("FindChildrenAsync method is deprecated. Use EnumerateChildrenAsync method instead for better performance and semantic support.")]
        [TechnicalDebt(CategoryType.NamingIssue ,"EnumerateChildrenAsync")]
        public static Task<List<string>> FindChildrenAsync(string sourceDirectory)
        {
            return Task.Run(() => Directory.EnumerateFileSystemEntries(sourceDirectory).ToList());
        }

        /// <summary>
        /// alternative of <seealso cref="FindChildrenAsync(string)"/> method to provide better performance and semantic support.
        /// </summary>
        /// <param name="sourceDirectory"></param>
        /// <returns></returns>
        public static Task<List<string>> EnumerateChildrenAsync(string sourceDirectory)
        {
            return Task.Run(() => Directory.EnumerateFileSystemEntries(sourceDirectory).ToList());
        }

        /// <summary>
        /// get specific child of a directory.
        /// 
        /// get specific child whose file name is `targetFileName` of a directory `sourceDirectory`.
        /// </summary>
        /// <param name="sourceDirectory"></param>
        /// <param name="targetFileName"></param>
        /// <returns></returns>
        [Obsolete("GetChild method is deprecated. Use GetChildPath method instead for better semantic support.")]
        [TechnicalDebt(CategoryType.NamingIssue ,"GetChildPath")]
        public static string GetChild(string sourceDirectory , string targetFileName)
        {
            bool hasSpecificChild = DirectoryHandler.HasChild(sourceDirectory , targetFileName);

            if(hasSpecificChild)
            {
                string targetFileFullPath = System.IO.Path.Combine(sourceDirectory , targetFileName);
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
        public static string GetChildPath(string sourceDirectory , string targetFileName)
        {
            bool hasSpecificChild = DirectoryHandler.HasChild(sourceDirectory , targetFileName);

            if(hasSpecificChild)
            {
                string targetFileFullPath = System.IO.Path.Combine(sourceDirectory , targetFileName);
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
        public static bool HasChild(string sourceDirectory , string targetFileName)
        {
            // 直接在檔案系統層級搜尋，不要抓回整個 List 再找，效能差異極大
            return System.IO.Directory.EnumerateFileSystemEntries(sourceDirectory)
                                   .Any(path => System.IO.Path.GetFileName(path)
                                   .Equals(targetFileName , StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Async version of <seealso cref="HasChild(string, string)"/> method.
        /// </summary>
        /// <param name="sourceDirectory"></param>
        /// <param name="targetFileName"></param>
        /// <returns></returns>
        public static async Task<bool> HasChildAsync(string sourceDirectory , string targetFileName)
        {
            return await Task.Run(() =>
                Directory.EnumerateFileSystemEntries(sourceDirectory)
                         .Any(path => Path.GetFileName(path)
                         .Equals(targetFileName , StringComparison.OrdinalIgnoreCase)));
        }
    }
}
