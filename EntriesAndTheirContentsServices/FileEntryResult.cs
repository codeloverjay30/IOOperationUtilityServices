using System;
using System.IO;

namespace EntriesAndTheirContentsServices;

/// <summary>
/// Represents a defensive value object containing file entry metadata and its safely loaded content.
/// </summary>
public sealed class FileEntryResult
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FileEntryResult"/> class.
    /// </summary>
    public FileEntryResult(string relativePath, string content, long sizeInBytes)
    {
        RelativePath = relativePath ?? throw new ArgumentNullException(nameof(relativePath));
        Content = content ?? throw new ArgumentNullException(nameof(content));
        SizeInBytes = sizeInBytes;
    }

    /// <summary>
    /// Gets the normalized relative path of the file entry.
    /// </summary>
    public string RelativePath { get; }

    /// <summary>
    /// Gets the safely read text content of the file.
    /// </summary>
    public string Content { get; }

    /// <summary>
    /// Gets the actual file size in bytes at the time of reading.
    /// </summary>
    public long SizeInBytes { get; }
}


