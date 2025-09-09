using DeviceSpecAnalyzer.Core.Interfaces;
using DeviceSpecAnalyzer.Processing.Services;
using Microsoft.Extensions.Options;

namespace DeviceSpecAnalyzer.Web.Services;

public class RepositoryWatcherHostedService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<RepositoryWatcherHostedService> _logger;
    private readonly RepositoryWatcherOptions _options;
    private RepositoryWatcher? _repositoryWatcher;

    public RepositoryWatcherHostedService(
        IServiceProvider serviceProvider,
        ILogger<RepositoryWatcherHostedService> logger,
        IOptions<RepositoryWatcherOptions> options)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var documentProcessor = scope.ServiceProvider.GetRequiredService<IDocumentProcessor>();
            var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
            
            var repositoryWatcherLogger = loggerFactory.CreateLogger<RepositoryWatcher>();
            
            _repositoryWatcher = new RepositoryWatcher(
                repositoryWatcherLogger,
                documentProcessor,
                Options.Create(_options));

            _repositoryWatcher.StartWatching();
            
            _logger.LogInformation("Repository watcher started successfully");

            // Keep the service running until cancellation is requested
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // This is expected when cancellation is requested
            _logger.LogInformation("Repository watcher service is stopping");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in repository watcher service");
            throw;
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping repository watcher service");
        
        _repositoryWatcher?.StopWatching();
        _repositoryWatcher?.Dispose();
        
        await base.StopAsync(cancellationToken);
    }
}