using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PDFAutomation.Services;

namespace PDFAutomation
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private ConfigurationManager _configManager;
        private LoggingService _loggingService;
        private EmailService _emailService;
        private RetryService _retryService;
        private FileWatcherService _fileWatcher;

        public Worker(ILogger<Worker> logger)
        {
            _logger = logger;
        }

        public override Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("PDF Otomasyon Servisi başlatılıyor...");

            try
            {
                // config.json dosyasını servis exe'nin yanından oku
                _configManager = new ConfigurationManager();
                _loggingService = new LoggingService(_configManager);
                _emailService = new EmailService(_configManager, _loggingService);
                _retryService = new RetryService(_configManager, _emailService, _loggingService);
                _fileWatcher = new FileWatcherService(_configManager, _emailService, _retryService, _loggingService);

                _fileWatcher.StatusChanged += (s, status) =>
                {
                    _logger.LogInformation("Servis durumu: {Status}", status);
                };

                _fileWatcher.FileProcessed += (s, message) =>
                {
                    _logger.LogInformation("Dosya işlendi: {Message}", message);
                };

                _fileWatcher.Start();
                _logger.LogInformation("PDF Otomasyon Servisi başarıyla başlatıldı. Incoming klasörü izleniyor: {Path}",
                    _configManager.Config.Klasorler.Incoming);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Servis başlatılırken hata oluştu");
                throw;
            }

            return base.StartAsync(cancellationToken);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // FileWatcherService kendi event loop'unda çalışıyor,
            // burada sadece servisin hayatta kalmasını sağlıyoruz
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
        }

        public override Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("PDF Otomasyon Servisi durduruluyor...");

            try
            {
                _fileWatcher?.Stop();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Servis durdurulurken hata oluştu");
            }

            return base.StopAsync(cancellationToken);
        }
    }
}
