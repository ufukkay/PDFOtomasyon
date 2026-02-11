using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using PDFAutomation.Models;

namespace PDFAutomation.Services
{
    public class FileWatcherService
    {
        private readonly ConfigurationManager _configManager;
        private readonly EmailService _emailService;
        private readonly RetryService _retryService;
        private readonly LoggingService _loggingService;
        private FileSystemWatcher _watcher;
        private bool _isRunning;

        public event EventHandler<string> FileDetected;
        public event EventHandler<string> FileProcessed;
        public event EventHandler<string> StatusChanged;

        public bool IsRunning => _isRunning;

        public FileWatcherService(ConfigurationManager configManager, EmailService emailService,
            RetryService retryService, LoggingService loggingService)
        {
            _configManager = configManager;
            _emailService = emailService;
            _retryService = retryService;
            _loggingService = loggingService;
        }

        public void Start()
        {
            if (_isRunning)
                return;

            try
            {
                var incomingPath = _configManager.Config.Klasorler.Incoming;

                if (!Directory.Exists(incomingPath))
                    Directory.CreateDirectory(incomingPath);

                _watcher = new FileSystemWatcher(incomingPath);
                _watcher.Filter = "*.pdf";
                _watcher.NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite;
                _watcher.Created += OnFileCreated;
                _watcher.EnableRaisingEvents = true;

                _isRunning = true;
                _retryService.Start();

                StatusChanged?.Invoke(this, "Çalışıyor");
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke(this, $"Hata: {ex.Message}");
                throw;
            }
        }

        public void Stop()
        {
            if (!_isRunning)
                return;

            _watcher?.Dispose();
            _watcher = null;
            _isRunning = false;
            _retryService.Stop();

            StatusChanged?.Invoke(this, "Durduruldu");
        }

        private async void OnFileCreated(object sender, FileSystemEventArgs e)
        {
            FileDetected?.Invoke(this, e.Name);
            var filePath = e.FullPath;

            // Eğer dosya sanal yazıcı çıktısı ise (temp_print_job.pdf), ismini değiştir
            if (Path.GetFileName(filePath).Equals("temp_print_job.pdf", StringComparison.OrdinalIgnoreCase))
            {
                // Yazma işleminin bitmesini bekle (kısa bir süre)
                await Task.Delay(1000);

                 // Dosya kilidini bekle
                if (!await WaitForFileUnlockAsync(filePath))
                {
                    _loggingService.LogFailure("temp_print_job.pdf", filePath, "", "", 1, 0, 0, "", "Yazıcı çıktısı kilitli veya erişilemiyor (Zaman aşımı)");
                    return;
                }

                try
                {
                    string newFileName = $"PrintJob_{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid().ToString().Substring(0, 8)}.pdf";
                    string newFilePath = Path.Combine(Path.GetDirectoryName(filePath), newFileName);
                    File.Move(filePath, newFilePath);
                    
                    // İşlenecek yeni dosya yolu artık bu
                    filePath = newFilePath;
                    
                     _loggingService.LogSuccess("temp_print_job.pdf", newFilePath, "", "", 1, 0, 0, "Yazıcı çıktısı algılandı ve yeniden isimlendirildi.");
                }
                catch (Exception ex)
                {
                     _loggingService.LogFailure("temp_print_job.pdf", filePath, "", "", 1, 0, 0, ex.Message, "Yeniden isimlendirme hatası");
                     return;
                }
            }
            else
            {
                 // Normal dosya kilidi bekleme
                if (!await WaitForFileUnlockAsync(filePath))
                {
                    _loggingService.LogFailure(Path.GetFileName(filePath), filePath, "", "", 1, 0, 0, "", "Dosya kilitli veya erişilemiyor (Zaman aşımı)");
                    return;
                }
            }

            // Dosyayı işle
            await ProcessFileAsync(filePath);
        }

        private async Task<bool> WaitForFileUnlockAsync(string filePath, int maxWaitSeconds = 30)
        {
            var startTime = DateTime.Now;
            long lastSize = -1;

            while ((DateTime.Now - startTime).TotalSeconds < maxWaitSeconds)
            {
                try
                {
                    if (!File.Exists(filePath))
                        return false;

                    var currentSize = new FileInfo(filePath).Length;

                    // Dosya boyutu değişiyorsa bekle
                    if (currentSize != lastSize)
                    {
                        lastSize = currentSize;
                        await Task.Delay(1000);
                        continue;
                    }

                    // Dosya boyutu 0 ise bekle (yazma başlamamış olabilir)
                    if (currentSize == 0)
                    {
                        await Task.Delay(1000);
                        continue;
                    }

                    // Dosyayı açmayı dene (Exclusive access)
                    using (var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.None))
                    {
                        return true;
                    }
                }
                catch (IOException)
                {
                    // Dosya hala kilitli, bekle
                    await Task.Delay(1000);
                }
                catch (Exception)
                {
                    // Beklenmedik hata, kısa bekle
                    await Task.Delay(1000);
                }
            }

            return false;
        }

        private async Task ProcessFileAsync(string filePath)
        {
            try
            {
                var dosyaAdi = Path.GetFileName(filePath);
                var kullanici = Environment.UserDomainName + "\\" + Environment.UserName;

                var message = new EmailMessage
                {
                    DosyaYolu = filePath,
                    DosyaAdi = dosyaAdi,
                    Gonderen = _configManager.Config.Eposta.Gonderen,
                    Alici = _configManager.Config.Eposta.Alici,
                    Konu = _emailService.FormatSubject(dosyaAdi, kullanici),
                    Govde = _emailService.FormatBody(dosyaAdi, kullanici, _configManager.Config.Klasorler.Incoming),
                    DosyaBoyutu = new FileInfo(filePath).Length,
                    Kullanici = kullanici,
                    Tarih = DateTime.Now
                };

                var success = await _emailService.SendEmailAsync(message);

                if (success)
                {
                    // Sent klasörüne taşı
                    MoveToSent(filePath);
                    FileProcessed?.Invoke(this, $"{dosyaAdi} başarıyla gönderildi");
                }
                else
                {
                    // Retry kuyruğuna ekle
                    _retryService.AddToRetryQueue(filePath, "İlk gönderim başarısız");
                    FileProcessed?.Invoke(this, $"{dosyaAdi} yeniden deneme kuyruğuna eklendi");
                }
            }
            catch (Exception ex)
            {
                var dosyaAdi = Path.GetFileName(filePath);
                FileProcessed?.Invoke(this, $"{dosyaAdi} işlenirken hata: {ex.Message}");

                // Retry kuyruğuna ekle
                _retryService.AddToRetryQueue(filePath, ex.Message);
            }
        }

        private void MoveToSent(string dosyaYolu)
        {
            try
            {
                var dosyaAdi = Path.GetFileName(dosyaYolu);
                var hedefYol = Path.Combine(_configManager.Config.Klasorler.Sent, dosyaAdi);
                hedefYol = GetUniqueFilePath(hedefYol);

                File.Move(dosyaYolu, hedefYol);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Dosya taşıma hatası: {ex.Message}");
            }
        }

        private string GetUniqueFilePath(string filePath)
        {
            if (!File.Exists(filePath))
                return filePath;

            var directory = Path.GetDirectoryName(filePath);
            var fileNameWithoutExt = Path.GetFileNameWithoutExtension(filePath);
            var extension = Path.GetExtension(filePath);
            var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");

            return Path.Combine(directory, $"{fileNameWithoutExt}_{timestamp}{extension}");
        }
    }
}
