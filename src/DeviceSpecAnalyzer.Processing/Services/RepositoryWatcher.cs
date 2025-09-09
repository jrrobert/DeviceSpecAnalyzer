using DeviceSpecAnalyzer.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;

namespace DeviceSpecAnalyzer.Processing.Services;

public class RepositoryWatcher : IRepositoryWatcher, IDisposable
{
    private readonly ILogger<RepositoryWatcher> _logger;
    private readonly IDocumentProcessor _documentProcessor;
    private readonly RepositoryWatcherOptions _options;
    
    private FileSystemWatcher? _fileWatcher;
    private readonly Dictionary<string, DateTime> _recentChanges = new();
    private readonly Timer _debounceTimer;
    private readonly object _lockObject = new();
    private bool _disposed;

    public RepositoryWatcher(
        ILogger<RepositoryWatcher> logger, 
        IDocumentProcessor documentProcessor,
        IOptions<RepositoryWatcherOptions> options)
    {
        _logger = logger;
        _documentProcessor = documentProcessor;
        _options = options.Value;
        
        WatchPath = _options.RepositoryPath ?? throw new ArgumentException("Repository path must be configured");
        
        _debounceTimer = new Timer(ProcessPendingChanges, null, Timeout.Infinite, Timeout.Infinite);
    }

    public bool IsWatching { get; private set; }
    public string WatchPath { get; set; }

    public event EventHandler<FileChangedEventArgs>? FileAdded;
    public event EventHandler<FileChangedEventArgs>? FileChanged;
    public event EventHandler<FileChangedEventArgs>? FileDeleted;

    public void StartWatching()
    {
        if (IsWatching)
        {
            _logger.LogWarning("Repository watcher is already running");
            return;
        }

        try
        {
            if (!Directory.Exists(WatchPath))
            {
                Directory.CreateDirectory(WatchPath);
                _logger.LogInformation("Created repository directory: {Path}", WatchPath);
            }

            _fileWatcher = new FileSystemWatcher(WatchPath)
            {
                Filter = "*.pdf",
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.Size | NotifyFilters.LastWrite | NotifyFilters.CreationTime
            };

            _fileWatcher.Created += OnFileCreated;
            _fileWatcher.Changed += OnFileChanged;
            _fileWatcher.Deleted += OnFileDeleted;
            _fileWatcher.Renamed += OnFileRenamed;
            _fileWatcher.Error += OnError;

            _fileWatcher.EnableRaisingEvents = true;
            IsWatching = true;

            _logger.LogInformation("Repository watcher started. Monitoring: {Path}", WatchPath);

            ScanForExistingFiles();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start repository watcher");
            throw;
        }
    }

    public void StopWatching()
    {
        if (!IsWatching)
        {
            return;
        }

        try
        {
            if (_fileWatcher != null)
            {
                _fileWatcher.EnableRaisingEvents = false;
                _fileWatcher.Created -= OnFileCreated;
                _fileWatcher.Changed -= OnFileChanged;
                _fileWatcher.Deleted -= OnFileDeleted;
                _fileWatcher.Renamed -= OnFileRenamed;
                _fileWatcher.Error -= OnError;
                _fileWatcher.Dispose();
                _fileWatcher = null;
            }

            IsWatching = false;
            _logger.LogInformation("Repository watcher stopped");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping repository watcher");
        }
    }

    private void OnFileCreated(object sender, FileSystemEventArgs e)
    {
        HandleFileChange(e.FullPath, FileChangeType.Created);
    }

    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        HandleFileChange(e.FullPath, FileChangeType.Modified);
    }

    private void OnFileDeleted(object sender, FileSystemEventArgs e)
    {
        HandleFileChange(e.FullPath, FileChangeType.Deleted);
    }

    private void OnFileRenamed(object sender, RenamedEventArgs e)
    {
        HandleFileChange(e.FullPath, FileChangeType.Renamed);
    }

    private void OnError(object sender, ErrorEventArgs e)
    {
        _logger.LogError(e.GetException(), "File system watcher error");
    }

    private void HandleFileChange(string filePath, FileChangeType changeType)
    {
        try
        {
            if (!IsPdfFile(filePath))
                return;

            lock (_lockObject)
            {
                _recentChanges[filePath] = DateTime.UtcNow;
            }

            _debounceTimer.Change(_options.DebounceDelayMs, Timeout.Infinite);

            _logger.LogDebug("File change detected: {FilePath} ({ChangeType})", filePath, changeType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling file change for {FilePath}", filePath);
        }
    }

    private void ProcessPendingChanges(object? state)
    {
        lock (_lockObject)
        {
            var cutoffTime = DateTime.UtcNow.AddMilliseconds(-_options.DebounceDelayMs);
            var filesToProcess = _recentChanges
                .Where(kvp => kvp.Value <= cutoffTime)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var filePath in filesToProcess)
            {
                _recentChanges.Remove(filePath);
                _ = Task.Run(() => ProcessFileChangeAsync(filePath));
            }

            if (_recentChanges.Any())
            {
                var nextChange = _recentChanges.Values.Min().AddMilliseconds(_options.DebounceDelayMs);
                var delay = (int)Math.Max(0, (nextChange - DateTime.UtcNow).TotalMilliseconds);
                _debounceTimer.Change(delay, Timeout.Infinite);
            }
        }
    }

    private async Task ProcessFileChangeAsync(string filePath)
    {
        try
        {
            var changeType = DetermineChangeType(filePath);
            var eventArgs = await CreateFileChangedEventArgs(filePath, changeType);

            switch (changeType)
            {
                case FileChangeType.Created:
                    await _documentProcessor.ProcessNewDocumentAsync(filePath);
                    FileAdded?.Invoke(this, eventArgs);
                    break;

                case FileChangeType.Modified:
                    await _documentProcessor.ProcessDocumentUpdateAsync(filePath);
                    FileChanged?.Invoke(this, eventArgs);
                    break;

                case FileChangeType.Deleted:
                    await _documentProcessor.ProcessDocumentDeletionAsync(filePath);
                    FileDeleted?.Invoke(this, eventArgs);
                    break;

                case FileChangeType.Renamed:
                    await _documentProcessor.ProcessDocumentUpdateAsync(filePath);
                    FileChanged?.Invoke(this, eventArgs);
                    break;
            }

            _logger.LogInformation("Processed file change: {FilePath} ({ChangeType})", filePath, changeType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing file change for {FilePath}", filePath);
        }
    }

    private FileChangeType DetermineChangeType(string filePath)
    {
        return File.Exists(filePath) ? FileChangeType.Modified : FileChangeType.Deleted;
    }

    private async Task<FileChangedEventArgs> CreateFileChangedEventArgs(string filePath, FileChangeType changeType)
    {
        var eventArgs = new FileChangedEventArgs
        {
            FilePath = filePath,
            ChangeType = changeType,
            ChangedAt = DateTime.UtcNow
        };

        if (File.Exists(filePath))
        {
            try
            {
                var fileInfo = new FileInfo(filePath);
                eventArgs.FileSize = fileInfo.Length;
                eventArgs.FileHash = await CalculateFileHashAsync(filePath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not get file info for {FilePath}", filePath);
            }
        }

        return eventArgs;
    }

    private async Task<string> CalculateFileHashAsync(string filePath)
    {
        using var fileStream = File.OpenRead(filePath);
        using var sha256 = SHA256.Create();
        var hashBytes = await Task.Run(() => sha256.ComputeHash(fileStream));
        return Convert.ToHexString(hashBytes);
    }

    private void ScanForExistingFiles()
    {
        try
        {
            var pdfFiles = Directory.GetFiles(WatchPath, "*.pdf", SearchOption.AllDirectories);
            
            _logger.LogInformation("Found {Count} existing PDF files in repository", pdfFiles.Length);

            foreach (var filePath in pdfFiles)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _documentProcessor.ProcessNewDocumentAsync(filePath);
                        _logger.LogDebug("Processed existing file: {FilePath}", filePath);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to process existing file: {FilePath}", filePath);
                    }
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scanning for existing files in repository");
        }
    }

    private static bool IsPdfFile(string filePath)
    {
        return Path.GetExtension(filePath).Equals(".pdf", StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        StopWatching();
        _debounceTimer.Dispose();
        _disposed = true;
    }
}

public class RepositoryWatcherOptions
{
    public string? RepositoryPath { get; set; }
    public int DebounceDelayMs { get; set; } = 2000;
    public bool ProcessExistingFiles { get; set; } = true;
    public string[] SupportedExtensions { get; set; } = { ".pdf" };
}