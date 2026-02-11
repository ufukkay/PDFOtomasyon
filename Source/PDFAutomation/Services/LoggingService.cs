using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Serilog;
using PDFAutomation.Models;

namespace PDFAutomation.Services
{
    public class LoggingService
    {
        private readonly ConfigurationManager _configManager;
        private readonly object _lockObject = new object();
        private Serilog.ILogger _logger;

        public event EventHandler<LogEntry> LogEntryAdded;

        public LoggingService(ConfigurationManager configManager)
        {
            _configManager = configManager;
            InitializeLogger();
        }

        private void InitializeLogger()
        {
            var logDir = _configManager.Config.Klasorler.Logs;
            var logFile = Path.Combine(logDir, "pdf-automation-.json");

            _logger = new LoggerConfiguration()
                .WriteTo.File(new Serilog.Formatting.Json.JsonFormatter(), logFile, rollingInterval: RollingInterval.Day, shared: true)
                .CreateLogger();
        }

        public void Log(LogEntry entry)
        {
            try
            {
                // UI için event fırlat
                LogEntryAdded?.Invoke(this, entry);

                // Serilog ile dosyaya yaz
                // Not: Serilog kendi JSON formatını kullanır, bu yüzden tam olarak LogEntry yapısını
                // tutturmak için property'leri manuel ekliyoruz
                 _logger.Information("{@LogEntry}", entry);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Log yazma hatası: {ex.Message}");
            }
        }

        public void LogSuccess(string dosyaAdi, string dosyaYolu, string alici, string gonderen, 
            int denemeSayisi, double sure, long dosyaBoyutu, string kullanici)
        {
            var entry = new LogEntry
            {
                Zaman = DateTime.Now,
                DosyaAdi = dosyaAdi,
                OrijinalYol = dosyaYolu,
                Durum = "basarili",
                Alici = alici,
                Gonderen = gonderen,
                DenemeSayisi = denemeSayisi,
                Sure = sure,
                DosyaBoyutu = dosyaBoyutu,
                Hata = null,
                Kullanici = kullanici
            };
            Log(entry);
        }

        public void LogFailure(string dosyaAdi, string dosyaYolu, string alici, string gonderen,
            int denemeSayisi, double sure, long dosyaBoyutu, string kullanici, string hata)
        {
            var entry = new LogEntry
            {
                Zaman = DateTime.Now,
                DosyaAdi = dosyaAdi,
                OrijinalYol = dosyaYolu,
                Durum = "basarisiz",
                Alici = alici,
                Gonderen = gonderen,
                DenemeSayisi = denemeSayisi,
                Sure = sure,
                DosyaBoyutu = dosyaBoyutu,
                Hata = hata,
                Kullanici = kullanici
            };
            Log(entry);
        }

        public void LogRetry(string dosyaAdi, string dosyaYolu, string alici, string gonderen,
            int denemeSayisi, double sure, long dosyaBoyutu, string kullanici, string hata)
        {
            var entry = new LogEntry
            {
                Zaman = DateTime.Now,
                DosyaAdi = dosyaAdi,
                OrijinalYol = dosyaYolu,
                Durum = "tekrar",
                Alici = alici,
                Gonderen = gonderen,
                DenemeSayisi = denemeSayisi,
                Sure = sure,
                DosyaBoyutu = dosyaBoyutu,
                Hata = hata,
                Kullanici = kullanici
            };
            Log(entry);
        }

        public List<LogEntry> GetTodaysLogs()
        {
            var logs = new List<LogEntry>();
            var logFile = GetLogFilePath();

            if (!File.Exists(logFile))
                return logs;

            try
            {
                using (var stream = new FileStream(logFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var reader = new StreamReader(stream))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        if (string.IsNullOrWhiteSpace(line))
                            continue;

                        try
                        {
                            // Serilog JSON format: {"Timestamp":..., "Level":..., "MessageTemplate":..., "Properties":{...}}
                            // Bizim LogEntry nesnemiz Properties içinde olabilir veya kök dizinde
                            dynamic rawLog = JsonConvert.DeserializeObject(line);
                            
                            // 1. Durum: {@LogEntry} kullanıldıysa Properties.LogEntry içindedir
                            // Ama Serilog JsonFormatter varsayılan olarak Properties objesi içinde saklar
                            if (rawLog.Properties != null && rawLog.Properties.LogEntry != null)
                            {
                                var entry = JsonConvert.DeserializeObject<LogEntry>(rawLog.Properties.LogEntry.ToString());
                                logs.Add(entry);
                            }
                            // 2. Durum: Direkt kökte olabilir (farklı formatter kullanılırsa)
                            else 
                            {
                                // Yedek plan: LogEntry alanlarını manuel eşle
                                // Bu kısım gerekirse geliştirilebilir
                            }
                        }
                        catch
                        {
                            // Bozuk satırı atla
                        }
                    }
                }
            }
            catch
            {
                // Dosya okuma hatası
            }

            return logs;
        }

        private string GetLogFilePath()
        {
            var logDir = _configManager.Config.Klasorler.Logs;
            var fileName = $"pdf-automation-{DateTime.Now:yyyyMMdd}.json";
            return Path.Combine(logDir, fileName);
        }

        public void CleanOldLogs(int daysToKeep = 30)
        {
            // Serilog rolling interval ile otomatik temizleme yapmaz, manuel temizleme gerekebilir
            // Ancak şimdilik bu metodu basit tutalım
        }
    }
}
