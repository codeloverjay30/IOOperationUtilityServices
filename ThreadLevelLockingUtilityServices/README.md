# Description
Wrapper class to more easily queue lots of task or threads (on same platform, and on same app but on different platform)

# Version Release
## 1.0.0-preview-1.0.0
### Added
1. Wrapper class of `SemaphoreSlim` class so that

one can more easily execute different tasks with lock on same platform which avoids thread hungry.

Additionally, it can queue the tasks and ensure tasks with most highest priority are always executed.

2. Wrapper class of `Mutex` class so that

one can more easily execute different tasks with lock on same platform which avoids thread hungry.

3. Wrapper class of `Mutex` class so that

one can more easily execute different tasks with lock on same platform which avoids thread hungry.

## 1.0.1-preview-1.0.0
### Added
1. some public getter property for `SemaphoreSlimService`

## 1.0.2-preview-1.0.0
### Deleted
1. some public getter property for `SemaphoreSlimService`

### Updated
1. make some property (from private) public for `SemaphoreSlimService` for mocking (`Moq` package)

## 1.0.3-preview-1.0.0
### Updated
1. make some property (from private) public for `ISemaphoreSlimService` for mocking (`Moq` package)

## 2.0.0-preview-1.0.0
### Major updates
+ Rename project name

+ Rename namespace

+ Make a documentation.

## 2.1.0-preview-1.0.0
### Added
1. Add retry.

### Major Updates
1. For new features and stablity in multiple threads and performance,

I refactor it will Polly v.8.