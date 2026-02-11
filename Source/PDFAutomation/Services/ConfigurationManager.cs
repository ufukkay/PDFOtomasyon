using System;
using System.IO;
using Newtonsoft.Json;
using PDFAutomation.Models;

namespace PDFAutomation.Services
{
    public class ConfigurationManager
    {
        // Paylaşılan config yolu - hem WPF hem de Windows Service aynı config'i kullanır
        private static readonly string SharedConfigDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "PDFAutomation");
        private static readonly string ConfigPath = Path.Combine(SharedConfigDir, "config.json");
        private static readonly string OldConfigPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");
        private AppConfig _config;

        public event EventHandler ConfigurationChanged;

        public AppConfig Config => _config;

        public ConfigurationManager()
        {
            // Paylaşılan klasörü oluştur
            Directory.CreateDirectory(SharedConfigDir);

            // Eski konumdan taşı (varsa ve yeni konumda yoksa)
            if (!File.Exists(ConfigPath) && File.Exists(OldConfigPath))
            {
                try
                {
                    File.Copy(OldConfigPath, ConfigPath, false);
                }
                catch { }
            }

            LoadConfiguration();
        }

        public void LoadConfiguration()
        {
            try
            {
                if (File.Exists(ConfigPath))
                {
                    var json = File.ReadAllText(ConfigPath);
                    _config = JsonConvert.DeserializeObject<AppConfig>(json);
                }
                else
                {
                    // --- Varsayılan Ayarları Yükle ---
                    SetDefaultConfiguration();
                    SaveConfiguration();
                }

                // Klasörleri oluştur
                EnsureDirectoriesExist();

                // --- Varsayılan Ayarları Zorla (Eğer boşsa) ---
                if (string.IsNullOrWhiteSpace(_config.Smtp.Sunucu))
                {
                    SetDefaultConfiguration();
                    SaveConfiguration();
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Yapılandırma yüklenirken hata: {ex.Message}", ex);
            }
        }

        private void SetDefaultConfiguration()
        {
            if (_config == null) _config = new AppConfig();

            _config.Eposta.Gonderen = "user@example.com";
            _config.Eposta.Alici = "archive@example.com";
            _config.Eposta.KonuFormati = "{Kullanici}, {Tarih}, {DosyaAdi}";

            _config.Smtp.Sunucu = "smtp.office365.com";
            _config.Smtp.Port = 587;
            _config.Smtp.KullaniciAdi = "user@example.com";
            _config.Smtp.Sifre = "password";
            _config.Smtp.OAuth2Kullan = false;
        }

        public void SaveConfiguration()
        {
            try
            {
                var json = JsonConvert.SerializeObject(_config, Formatting.Indented);
                File.WriteAllText(ConfigPath, json);
                ConfigurationChanged?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                throw new Exception($"Yapılandırma kaydedilirken hata: {ex.Message}", ex);
            }
        }

        private void EnsureDirectoriesExist()
        {
            Directory.CreateDirectory(_config.Klasorler.Incoming);
            Directory.CreateDirectory(_config.Klasorler.Sent);
            Directory.CreateDirectory(_config.Klasorler.Failed);
            Directory.CreateDirectory(_config.Klasorler.Logs);
        }

        public bool ValidateConfiguration()
        {
            if (string.IsNullOrWhiteSpace(_config.Eposta.Gonderen))
                return false;
            if (string.IsNullOrWhiteSpace(_config.Eposta.Alici))
                return false;
            if (string.IsNullOrWhiteSpace(_config.Smtp.Sunucu))
                return false;

            if (_config.Smtp.OAuth2Kullan)
            {
                if (string.IsNullOrWhiteSpace(_config.Smtp.TenantId) ||
                    string.IsNullOrWhiteSpace(_config.Smtp.ClientId) ||
                    string.IsNullOrWhiteSpace(_config.Smtp.ClientSecret))
                    return false;
            }
            else
            {
                if (string.IsNullOrWhiteSpace(_config.Smtp.KullaniciAdi) ||
                    string.IsNullOrWhiteSpace(_config.Smtp.Sifre))
                    return false;
            }

            return true;
        }
    }
}
