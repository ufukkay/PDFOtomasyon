using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Identity.Client;
using MimeKit;
using PDFAutomation.Models;

namespace PDFAutomation.Services
{
    public class EmailService
    {
        private readonly ConfigurationManager _configManager;
        private readonly LoggingService _loggingService;

        public EmailService(ConfigurationManager configManager, LoggingService loggingService)
        {
            _configManager = configManager;
            _loggingService = loggingService;
        }

        public async Task<bool> SendEmailAsync(EmailMessage message, int attemptNumber = 1)
        {
            var startTime = DateTime.Now;

            try
            {
                // Dosya boyutu kontrolü
                var fileInfo = new FileInfo(message.DosyaYolu);
                var maxSizeMB = _configManager.Config.Dosya.MaksimumBoyutMB;
                var maxSizeBytes = maxSizeMB * 1024 * 1024;

                string attachmentPath = message.DosyaYolu;
                bool isCompressed = false;

                if (fileInfo.Length > maxSizeBytes && _configManager.Config.Dosya.SikistirmaEtkin)
                {
                    // Sıkıştırmayı dene
                    attachmentPath = await CompressFileAsync(message.DosyaYolu);
                    isCompressed = true;

                    var compressedInfo = new FileInfo(attachmentPath);
                    if (compressedInfo.Length > maxSizeBytes)
                    {
                        // Sıkıştırılmış hali hala çok büyük
                        var duration = (DateTime.Now - startTime).TotalSeconds;
                        _loggingService.LogFailure(message.DosyaAdi, message.DosyaYolu,
                            message.Alici, message.Gonderen, attemptNumber, duration,
                            fileInfo.Length, message.Kullanici,
                            $"Dosya boyutu çok büyük: {compressedInfo.Length / 1024 / 1024} MB (Maksimum: {maxSizeMB} MB)");

                        if (isCompressed && File.Exists(attachmentPath))
                            File.Delete(attachmentPath);

                        return false;
                    }
                }

                // E-posta oluştur
                var mimeMessage = new MimeMessage();
                mimeMessage.From.Add(MailboxAddress.Parse(message.Gonderen));

                var recipients = message.Alici.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var recipient in recipients)
                {
                    mimeMessage.To.Add(MailboxAddress.Parse(recipient.Trim()));
                }

                mimeMessage.Subject = message.Konu;

                var builder = new BodyBuilder();
                builder.HtmlBody = message.Govde;

                // Ek ekle
                builder.Attachments.Add(attachmentPath);

                mimeMessage.Body = builder.ToMessageBody();

                // SMTP ile gönder
                using (var client = new SmtpClient())
                {
                    await client.ConnectAsync(_configManager.Config.Smtp.Sunucu,
                        _configManager.Config.Smtp.Port, SecureSocketOptions.StartTls);

                    if (_configManager.Config.Smtp.OAuth2Kullan)
                    {
                        var authToken = await GetOAuth2TokenAsync();
                        var oauth2 = new SaslMechanismOAuth2(_configManager.Config.Eposta.Gonderen, authToken);
                        await client.AuthenticateAsync(oauth2);
                    }
                    else
                    {
                        await client.AuthenticateAsync(_configManager.Config.Smtp.KullaniciAdi,
                            _configManager.Config.Smtp.Sifre);
                    }

                    await client.SendAsync(mimeMessage);
                    await client.DisconnectAsync(true);
                }

                // Sıkıştırılmış geçici dosyayı sil
                if (isCompressed && File.Exists(attachmentPath))
                    File.Delete(attachmentPath);

                var successDuration = (DateTime.Now - startTime).TotalSeconds;
                _loggingService.LogSuccess(message.DosyaAdi, message.DosyaYolu,
                    message.Alici, message.Gonderen, attemptNumber, successDuration,
                    fileInfo.Length, message.Kullanici);

                return true;
            }
            catch (Exception ex)
            {
                var duration = (DateTime.Now - startTime).TotalSeconds;
                _loggingService.LogFailure(message.DosyaAdi, message.DosyaYolu,
                    message.Alici, message.Gonderen, attemptNumber, duration,
                    new FileInfo(message.DosyaYolu).Length, message.Kullanici,
                    ex.Message);

                return false;
            }
        }

        private async Task<string> GetOAuth2TokenAsync()
        {
            var app = Microsoft.Identity.Client.ConfidentialClientApplicationBuilder
                .Create(_configManager.Config.Smtp.ClientId)
                .WithClientSecret(_configManager.Config.Smtp.ClientSecret)
                .WithAuthority(new Uri($"https://login.microsoftonline.com/{_configManager.Config.Smtp.TenantId}"))
                .Build();

            // Office 365 SMTP scope
            var scopes = new[] { "https://outlook.office365.com/.default" };

            var result = await app.AcquireTokenForClient(scopes).ExecuteAsync();
            return result.AccessToken;
        }

        private async Task<string> CompressFileAsync(string filePath)
        {
            var tempZipPath = Path.Combine(Path.GetTempPath(), 
                Path.GetFileNameWithoutExtension(filePath) + ".zip");

            await Task.Run(() =>
            {
                if (File.Exists(tempZipPath))
                    File.Delete(tempZipPath);

                using (var archive = System.IO.Compression.ZipFile.Open(tempZipPath, System.IO.Compression.ZipArchiveMode.Create))
                {
                    archive.CreateEntryFromFile(filePath, Path.GetFileName(filePath), System.IO.Compression.CompressionLevel.Optimal);
                }
            });

            return tempZipPath;
        }

        public string FormatSubject(string dosyaAdi, string kullanici)
        {
            var format = _configManager.Config.Eposta.KonuFormati;
            var tarih = DateTime.Now.ToString("dd.MM.yyyy HH:mm");

            return format
                .Replace("{Kullanici}", kullanici)
                .Replace("{Tarih}", tarih)
                .Replace("{DosyaAdi}", dosyaAdi);
        }

        public string FormatBody(string dosyaAdi, string kullanici, string kaynak)
        {
            return $@"
                <html>
                <body style='font-family: Arial, sans-serif;'>
                    <h2>PDF Çıktı Bildirimi</h2>
                    <table style='border-collapse: collapse; width: 100%;'>
                        <tr>
                            <td style='padding: 8px; border: 1px solid #ddd;'><strong>Dosya Adı:</strong></td>
                            <td style='padding: 8px; border: 1px solid #ddd;'>{dosyaAdi}</td>
                        </tr>
                        <tr>
                            <td style='padding: 8px; border: 1px solid #ddd;'><strong>Kullanıcı:</strong></td>
                            <td style='padding: 8px; border: 1px solid #ddd;'>{kullanici}</td>
                        </tr>
                        <tr>
                            <td style='padding: 8px; border: 1px solid #ddd;'><strong>Tarih/Saat:</strong></td>
                            <td style='padding: 8px; border: 1px solid #ddd;'>{DateTime.Now:dd.MM.yyyy HH:mm:ss}</td>
                        </tr>
                        <tr>
                            <td style='padding: 8px; border: 1px solid #ddd;'><strong>Kaynak Klasör:</strong></td>
                            <td style='padding: 8px; border: 1px solid #ddd;'>{kaynak}</td>
                        </tr>
                    </table>
                    <p style='margin-top: 20px; color: #666;'>
                        Bu e-posta otomatik olarak PDF Otomasyon Sistemi tarafından gönderilmiştir.
                    </p>
                </body>
                </html>";
        }
    }
}
