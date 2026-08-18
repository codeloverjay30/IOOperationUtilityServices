# Description
Utility Service about I/O Operation 

# Features
## 1.0.0-preview-1.0.0
### Added 
+ Batch CRUD for files and directories.

## 2.0.0-preview-1.0.0
### Major changes
+ rename method name (by copying the older ones and then mark them as obsolete) to make them more semantic and easier to use.

## 3.0.0-preview-1.0.0
### Added
+ Batch CRUD for files and directories (`async` version)

## 4.0.0-preview-1.0.0
### Major Updates
+ use interface

    - `DirectoryHandler` -> `DirectoryUtilityService`
    
    - `FileHandler` -> `FileUtilityService`

### Added tests
+ Added unit tests by `MockFileSystem` (which mocks the file system)

## 4.1.0-preview-1.0.0
### Added
+ Fast enumerate entries under specific directory with glob pattern

### Added API
+ `FastEnumerateFile`: Fast enumerate entries under specific directory with glob pattern

+ `FastEnumerateFileAsync`: async version of `FastEnumerateFile`

## 5.0.0-preview-1.0.0
### Fixed
+ can not check an entry is a directory

+ dependency circular (among `DirectoryUtilityService` and `FileUtilityService`) when performing DI (using `Lazy<T>`)

## 6.0.0-preview-1.0.0
### Major Updates
See below

### Unsupported
Unsupports these version

+ .NET 9.0

## 7.0.0-preview-1.0.0
### Fixed
+ Can't move directory

### Major Updates
+ Move directory between same drives -> Move directory between different drives

+ Keep or restore permissions (`ACL`) in Windows 11 after moving directory in Windows 11.

### Supported
Supports on these platforms

+ Windows 11

+ Linux

+ MacOs