using System;
using System.Collections.Generic;
using System.Text;
using CustomDataAnnotations.Maintenance;

namespace IOOperationUtilityServices
{
    /// <summary>
    /// utility class that handles file. 
    /// </summary>
    /// <remarks>
    /// This utility class uses non-static class (e.g. `System.IO.Path`,
    /// which can't perform DI and mock
    /// because it uses `System.IO.Path` etc
    /// so that it needs to perform I/O operation on OS.
    /// Thus, use <seealso cref="FileUtilityService"/> class instead.
    /// And it is obsolete so that it will no longer be maintained.
    /// </remarks>
    [Obsolete("use <seealso cref=\"FileUtilityService\"/> class instead")]
    [TechnicalDebt(CategoryType.OtherIssue , "FileUtilityService")]
    public static class FileHandler
    {
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
        /// <exception cref="System.IO.FileNotFoundException">When `sourceFileFullPath` does NOT exist or it is not a file, it will throw Exception</exception>
        public static void Copy(string sourceFileFullPath , string destinationFileFullPath)
        {
            // 檢查來源是否存在，避免 File.Copy 丟出不夠明確的異常
            if(!System.IO.File.Exists(sourceFileFullPath))
            {
                throw new System.IO.FileNotFoundException("Source file not found." , sourceFileFullPath);
            }

            // 確保目的地的資料夾路徑存在
            string destDirectory = System.IO.Path.GetDirectoryName(destinationFileFullPath);
            if(!string.IsNullOrEmpty(destDirectory) && !System.IO.Directory.Exists(destDirectory))
            {
                System.IO.Directory.CreateDirectory(destDirectory);
            }

            // 直接 Copy，overwrite: true 會處理覆寫，若不存在也會自動建立
            System.IO.File.Copy(sourceFileFullPath , destinationFileFullPath , true);
        }

        /// <summary>
        /// Async version of <seealso cref="FileHandler.Copy"/> method.
        /// </summary>
        public static async Task CopyAsync(string sourceFileFullPath , string destinationFileFullPath)
        {
            if(!File.Exists(sourceFileFullPath))
            {
                throw new FileNotFoundException("Source file not found." , sourceFileFullPath);
            }

            string destDirectory = Path.GetDirectoryName(destinationFileFullPath);
            if(!string.IsNullOrEmpty(destDirectory) && !Directory.Exists(destDirectory))
            {
                Directory.CreateDirectory(destDirectory);
            }

            // 使用 FileStream 進行非同步讀寫
            using(FileStream sourceStream = new FileStream(sourceFileFullPath , FileMode.Open , FileAccess.Read , FileShare.Read , bufferSize: 4096 , useAsync: true))
            using(FileStream destinationStream = new FileStream(destinationFileFullPath , FileMode.Create , FileAccess.Write , FileShare.None , bufferSize: 4096 , useAsync: true))
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
        [Obsolete("此方法名稱不夠精確，建議改用 ToLongPath 以獲得更好的語意支持。")]
        [TechnicalDebt(CategoryType.NamingIssue ,"ToLongPath")]
        public static string GetSafeLongPath(string path)
        {
            if(string.IsNullOrEmpty(path))
            {
                return path;
            }

            // 1. 取得絕對路徑（這會處理掉 . 與 ..）
            // 注意：Path.GetFullPath 在 .NET 4.6.2 以前的版本本身也受 260 字元限制
            string fullPath = Path.GetFullPath(path);

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
        public static string ToLongPath(string path)
        {
            if(string.IsNullOrEmpty(path))
            {
                return path;
            }

            // 1. 取得絕對路徑（這會處理掉 . 與 ..）
            // 注意：Path.GetFullPath 在 .NET 4.6.2 以前的版本本身也受 260 字元限制
            string fullPath = Path.GetFullPath(path);

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
        [Obsolete("此方法名稱不夠精確，建議改用 CreateOrClearFile 以獲得更好的語意支持。")]
        [TechnicalDebt(CategoryType.NamingIssue ,"CreateOrClearFile")]

        public static bool TryToOverwriteFile(string filePath)
        {
            try
            {
                if(string.IsNullOrEmpty(filePath))
                {
                    throw new ArgumentNullException($"引數錯誤。檔案路徑'{filePath}'為null或empty。無法判斷檔案的存在性和建立檔案");
                }

                // 取得安全路徑(針對超長路徑)
                /// filePath = FileHandler.GetSafeLongPath(filePath);
                if(File.Exists(filePath)) //如果檔案存在
                {
                    //將空字串寫入檔案，以達到清空檔案的效果
                    File.WriteAllText(filePath , string.Empty);
                }
                //如果檔案不存在
                string? directoryPath = Path.GetDirectoryName(filePath);
                if(string.IsNullOrEmpty(directoryPath))
                {
                    throw new ArgumentNullException($"引數錯誤。'{filePath}'的父目錄為null或empty。無法判斷目錄的存在性和建立目錄");
                }
                //如果目錄已存在，此行不會有負面影響；如果不存在，會連同父目錄一起建立
                Directory.CreateDirectory(directoryPath);
                //將空字串寫入檔案，以達到建立空白檔案的效果
                File.WriteAllText(filePath , string.Empty);
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
        public static bool CreateOrClearFile(string filePath)
        {
            try
            {
                if(string.IsNullOrEmpty(filePath))
                {
                    throw new ArgumentNullException($"引數錯誤。檔案路徑'{filePath}'為null或empty。無法判斷檔案的存在性和建立檔案");
                }

                // 取得安全路徑(針對超長路徑)
                /// filePath = FileHandler.GetSafeLongPath(filePath);
                if(File.Exists(filePath)) //如果檔案存在
                {
                    //將空字串寫入檔案，以達到清空檔案的效果
                    File.WriteAllText(filePath , string.Empty);
                }
                //如果檔案不存在
                string? directoryPath = Path.GetDirectoryName(filePath);
                if(string.IsNullOrEmpty(directoryPath))
                {
                    throw new ArgumentNullException($"引數錯誤。'{filePath}'的父目錄為null或empty。無法判斷目錄的存在性和建立目錄");
                }
                //如果目錄已存在，此行不會有負面影響；如果不存在，會連同父目錄一起建立
                Directory.CreateDirectory(directoryPath);
                //將空字串寫入檔案，以達到建立空白檔案的效果
                File.WriteAllText(filePath , string.Empty);
                return true;
            }
            catch(Exception ex)
            {
                // Logic for Exception handling
                // ...
                throw;
            }
        }
    }
}
