using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using PDFAutomation.Models;

namespace PDFAutomation.Services
{
    public class RetryService
    {
        private readonly ConfigurationManager _configManager;
        private readonly EmailService _emailService;
        private readonly LoggingService _loggingService;
        private readonly Dictionary<string, RetryMetadata> _retryQueue = new Dictionary<string, RetryMetadata>();
        private readonly object _lockObject = new object();
        private Timer _retryTimer;
        private bool _isRunning;

        public RetryService(ConfigurationManager configManager, EmailService emailService, LoggingService loggingService)
        {
            _configManager = configManager;
            _emailService = emailService;
            _loggingService = loggingService;
        }

        public void Start()
        {
            if (_isRunning)
                return;

            _isRunning = true;
            LoadRetryQueue();

            // Her dakika kontrol et
            _retryTimer = new Timer(ProcessRetryQueue, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
        }

        public void Stop()
        {
            _isRunning = false;
            _retryTimer?.Dispose();
            SaveRetryQueue();
        }

        public void AddToRetryQueue(string dosyaYolu, string hata)
        {
            lock (_lockObject)
            {
                var dosyaAdi = Path.GetFileName(dosyaYolu);

                if (_retryQueue.ContainsKey(dosyaYolu))
                {
                    var metadata = _retryQueue[dosyaYolu];
                    metadata.DenemeSayisi++;
                    metadata.SonDeneme = DateTime.Now;
                    metadata.SonrakiDeneme = DateTime.Now.AddMinutes(_configManager.Config.Retry.AralikDakika);
                    metadata.Hatalar.Add($"[{DateTime.Now:HH:mm:ss}] {hata}");
                }
                else
                {
                    _retryQueue[dosyaYolu] = new RetryMetadata
                    {
                        DosyaAdi = dosyaAdi,
                        DosyaYolu = dosyaYolu,
                        DenemeSayisi = 1,
                        SonDeneme = DateTime.Now,
                        SonrakiDeneme = DateTime.Now.AddMinutes(_configManager.Config.Retry.AralikDakika),
                        Hatalar = new List<string> { $"[{DateTime.Now:HH:mm:ss}] {hata}" }
                    };
                }

                SaveRetryQueue();
            }
        }

        private async void ProcessRetryQueue(object state)
        {
            if (!_isRunning)
                return;

            List<string> toRetry;
            lock (_lockObject)
            {
                toRetry = _retryQueue
                    .Where(kvp => kvp.Value.SonrakiDeneme <= DateTime.Now)
                    .Select(kvp => kvp.Key)
                    .ToList();
            }

            foreach (var dosyaYolu in toRetry)
            {
                if (!File.Exists(dosyaYolu))
                {
                    // Dosya bulunamadı, kuyruktan çıkar
                    lock (_lockObject)
                    {
                        _retryQueue.Remove(dosyaYolu);
                    }
                    continue;
                }

                RetryMetadata metadata;
                lock (_lockObject)
                {
                    metadata = _retryQueue[dosyaYolu];
                }

                if (metadata.DenemeSayisi >= _configManager.Config.Retry.MaksimumDeneme)
                {
                    // Maksimum deneme sayısına ulaşıldı, Failed klasörüne taşı
                    MoveToFailed(dosyaYolu);
                    lock (_lockObject)
                    {
                        _retryQueue.Remove(dosyaYolu);
                    }
                    continue;
                }

                // Yeniden göndermeyi dene
                var kullanici = Environment.UserDomainName + "\\" + Environment.UserName;
                var dosyaAdi = Path.GetFileName(dosyaYolu);

                var message = new EmailMessage
                {
                    DosyaYolu = dosyaYolu,
                    DosyaAdi = dosyaAdi,
                    Gonderen = _configManager.Config.Eposta.Gonderen,
                    Alici = _configManager.Config.Eposta.Alici,
                    Konu = _emailService.FormatSubject(dosyaAdi, kullanici),
                    Govde = _emailService.FormatBody(dosyaAdi, kullanici, _configManager.Config.Klasorler.Incoming),
                    DosyaBoyutu = new FileInfo(dosyaYolu).Length,
                    Kullanici = kullanici,
                    Tarih = DateTime.Now
                };

                var success = await _emailService.SendEmailAsync(message, metadata.DenemeSayisi + 1);

                if (success)
                {
                    // Başarılı, Sent klasörüne taşı
                    MoveToSent(dosyaYolu);
                    lock (_lockObject)
                    {
                        _retryQueue.Remove(dosyaYolu);
                    }
                }
                else
                {
                    // Başarısız, tekrar kuyruğa ekle
                    AddToRetryQueue(dosyaYolu, "Yeniden deneme başarısız");
                }
            }

            SaveRetryQueue();
        }

        private void MoveToSent(string dosyaYolu)
        {
            try
            {
                var dosyaAdi = Path.GetFileName(dosyaYolu);
                var hedefYol = Path.Combine(_configManager.Config.Klasorler.Sent, dosyaAdi);
                hedefYol = GetUniqueFilePath(hedefYol);

                File.Move(dosyaYolu, hedefYol);

                // Metadata dosyasını sil
                var metadataPath = dosyaYolu + ".retry.json";
                if (File.Exists(metadataPath))
                    File.Delete(metadataPath);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Dosya taşıma hatası: {ex.Message}");
            }
        }

        private void MoveToFailed(string dosyaYolu)
        {
            try
            {
                var dosyaAdi = Path.GetFileName(dosyaYolu);
                var hedefYol = Path.Combine(_configManager.Config.Klasorler.Failed, dosyaAdi);
                hedefYol = GetUniqueFilePath(hedefYol);

                File.Move(dosyaYolu, hedefYol);

                // Metadata dosyasını da taşı
                var metadataPath = dosyaYolu + ".retry.json";
                if (File.Exists(metadataPath))
                {
                    var hedefMetadataPath = hedefYol + ".retry.json";
                    File.Move(metadataPath, hedefMetadataPath);
                }
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

        private void LoadRetryQueue()
        {
            try
            {
                var incomingDir = _configManager.Config.Klasorler.Incoming;
                var metadataFiles = Directory.GetFiles(incomingDir, "*.retry.json");

                foreach (var metadataFile in metadataFiles)
                {
                    try
                    {
                        var json = File.ReadAllText(metadataFile);
                        var metadata = JsonConvert.DeserializeObject<RetryMetadata>(json);

                        if (File.Exists(metadata.DosyaYolu))
                        {
                            _retryQueue[metadata.DosyaYolu] = metadata;
                        }
                        else
                        {
                            // Dosya bulunamadı, metadata'yı sil
                            File.Delete(metadataFile);
                        }
                    }
                    catch
                    {
                        // Bozuk metadata dosyası
                    }
                }
            }
            catch
            {
                // Klasör okuma hatası
            }
        }

        private void SaveRetryQueue()
        {
            lock (_lockObject)
            {
                foreach (var kvp in _retryQueue)
                {
                    try
                    {
                        var metadataPath = kvp.Key + ".retry.json";
                        var json = JsonConvert.SerializeObject(kvp.Value, Formatting.Indented);
                        File.WriteAllText(metadataPath, json);
                    }
                    catch
                    {
                        // Metadata yazma hatası
                    }
                }
            }
        }

        public int GetPendingCount()
        {
            lock (_lockObject)
            {
                return _retryQueue.Count;
            }
        }
    }
}
