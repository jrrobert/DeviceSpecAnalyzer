namespace DeviceSpecAnalyzer.Core.Interfaces;

public interface IRepositoryWatcher
{
    void StartWatching();
    void StopWatching();
    bool IsWatching { get; }
    string WatchPath { get; set; }
    
    event EventHandler<FileChangedEventArgs>? FileAdded;
    event EventHandler<FileChangedEventArgs>? FileChanged;
    event EventHandler<FileChangedEventArgs>? FileDeleted;
}

public interface IDocumentProcessor
{
    Task<bool> ProcessNewDocumentAsync(string filePath);
    Task<bool> ProcessDocumentUpdateAsync(string filePath);
    Task<bool> ProcessDocumentDeletionAsync(string filePath);
    Task<ProcessingResult> ProcessDocumentAsync(string filePath);
}

public class FileChangedEventArgs : EventArgs
{
    public string FilePath { get; set; } = string.Empty;
    public FileChangeType ChangeType { get; set; }
    public DateTime ChangedAt { get; set; }
    public long FileSize { get; set; }
    public string? FileHash { get; set; }
}

public enum FileChangeType
{
    Created,
    Modified,
    Deleted,
    Renamed
}

public class ProcessingResult
{
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
    public int? DocumentId { get; set; }
    public string? ProcessedFilePath { get; set; }
    public DateTime ProcessedAt { get; set; }
    public TimeSpan ProcessingDuration { get; set; }
    public Dictionary<string, object>? Metadata { get; set; }
}