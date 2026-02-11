using System;

namespace PDFAutomation.Models
{
    public class AppConfig
    {
        public FolderSettings Klasorler { get; set; } = new FolderSettings();
        public SmtpSettings Smtp { get; set; } = new SmtpSettings();
        public EmailSettings Eposta { get; set; } = new EmailSettings();
        public RetrySettings Retry { get; set; } = new RetrySettings();
        public FileSettings Dosya { get; set; } = new FileSettings();
        public LogSettings Loglama { get; set; } = new LogSettings();
    }

    public class FolderSettings
    {
        private static string BasePath => System.IO.Path.Combine(
            System.Environment.GetFolderPath(System.Environment.SpecialFolder.CommonApplicationData), 
            "PDFAutomation", "PDF_Out");
        
        public string Incoming { get; set; } = System.IO.Path.Combine(BasePath, "Incoming");
        public string Sent { get; set; } = System.IO.Path.Combine(BasePath, "Sent");
        public string Failed { get; set; } = System.IO.Path.Combine(BasePath, "Failed");
        public string Logs { get; set; } = System.IO.Path.Combine(BasePath, "Logs");
    }

    public class SmtpSettings
    {
        public string Sunucu { get; set; } = "smtp.office365.com";
        public int Port { get; set; } = 587;
        public bool OAuth2Kullan { get; set; } = false;
        public string TenantId { get; set; } = "";
        public string ClientId { get; set; } = "";
        public string ClientSecret { get; set; } = "";
        public string KullaniciAdi { get; set; } = "";
        public string Sifre { get; set; } = "";
    }

    public class EmailSettings
    {
        public string Gonderen { get; set; } = "pdfsender@domain.com";
        public string Alici { get; set; } = "arsiv@domain.com";
        public string KonuFormati { get; set; } = "PDF Çıktı - {Kullanici} - {Tarih} - {DosyaAdi}";
    }

    public class RetrySettings
    {
        public int MaksimumDeneme { get; set; } = 5;
        public int AralikDakika { get; set; } = 10;
    }

    public class FileSettings
    {
        public int MaksimumBoyutMB { get; set; } = 35;
        public bool SikistirmaEtkin { get; set; } = true;
    }

    public class LogSettings
    {
        public string Format { get; set; } = "json";
        public string Seviye { get; set; } = "Info";
    }
}
