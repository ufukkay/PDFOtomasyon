using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using PDFAutomation.Services;
using PDFAutomation.Models;
using System.IO;
using System.ServiceProcess;
using System.Diagnostics;

namespace PDFAutomation
{
    public partial class MainWindow : Window
    {
        private ConfigurationManager _configManager;
        private LoggingService _loggingService;
        private EmailService _emailService;
        private RetryService _retryService;
        private FileWatcherService _fileWatcher;
        private DispatcherTimer _updateTimer;
        private const string SERVICE_NAME = "PDFOtomasyonServisi";

        public MainWindow()
        {
            InitializeComponent();
            SetWindowIcon();
            InitializeServices();
            LoadSettings();
            SetupTimer();
            UpdateDashboard();
            CheckWindowsServiceStatus();

            // --- OTOMATİK BAŞLAT ---
            // Uygulama açıldığında servisi otomatik başlat
            if (_configManager != null && _configManager.ValidateConfiguration())
            {
                try
                {
                    _fileWatcher.Start();
                    UpdateStartStopButton();
                }
                catch { }
            }
        }

        private void SetWindowIcon()
        {
            try
            {
                var iconUri = new Uri("pack://application:,,,/PDFAutomation;component/icon.ico", UriKind.Absolute);
                this.Icon = System.Windows.Media.Imaging.BitmapFrame.Create(iconUri);
            }
            catch { }

            // Sistem tepsisi ikonunu ayarla - her zaman göster
            try
            {
                if (File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "icon.ico")))
                {
                    TrayIcon.Icon = new System.Drawing.Icon(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "icon.ico"));
                }
                else
                {
                    TrayIcon.Icon = System.Drawing.SystemIcons.Application;
                }
            }
            catch
            {
                TrayIcon.Icon = System.Drawing.SystemIcons.Application;
            }
        }

        private void InitializeServices()
        {
            try
            {
                _configManager = new ConfigurationManager();
                _loggingService = new LoggingService(_configManager);
                _emailService = new EmailService(_configManager, _loggingService);
                _retryService = new RetryService(_configManager, _emailService, _loggingService);
                _fileWatcher = new FileWatcherService(_configManager, _emailService, _retryService, _loggingService);

                _fileWatcher.StatusChanged += (s, status) =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        TxtStatus.Text = status;
                        TxtServiceStatus.Text = status;
                        UpdateStartStopButton();
                    });
                };

                _fileWatcher.FileProcessed += (s, message) =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        UpdateDashboard();
                    });
                };

                _loggingService.LogEntryAdded += (s, entry) =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        UpdateDashboard();
                    });
                };
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Servisler başlatılırken hata: {ex.Message}", "Hata", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SetupTimer()
        {
            _updateTimer = new DispatcherTimer();
            _updateTimer.Interval = TimeSpan.FromSeconds(5);
            _updateTimer.Tick += (s, e) => UpdateDashboard();
            _updateTimer.Start();
        }

        private void LoadSettings()
        {
            var config = _configManager.Config;

            // SMTP
            TxtSmtpServer.Text = config.Smtp.Sunucu;
            TxtSmtpPort.Text = config.Smtp.Port.ToString();
            ChkOAuth2.IsChecked = config.Smtp.OAuth2Kullan;
            
            // Basic Auth
            TxtSmtpUsername.Text = config.Smtp.KullaniciAdi;
            TxtSmtpPassword.Password = config.Smtp.Sifre;
            
            // OAuth2
            TxtTenantId.Text = config.Smtp.TenantId;
            TxtClientId.Text = config.Smtp.ClientId;
            TxtClientSecret.Password = config.Smtp.ClientSecret;

            UpdateAuthUiState();

            // E-posta
            TxtSender.Text = config.Eposta.Gonderen;
            TxtRecipient.Text = config.Eposta.Alici;
            TxtSubjectFormat.Text = config.Eposta.KonuFormati;

            // Klasörler
            TxtIncoming.Text = config.Klasorler.Incoming;
            TxtSent.Text = config.Klasorler.Sent;
            TxtFailedPath.Text = config.Klasorler.Failed;
            TxtLogs.Text = config.Klasorler.Logs;

            // Retry
            TxtMaxRetry.Text = config.Retry.MaksimumDeneme.ToString();
            TxtRetryInterval.Text = config.Retry.AralikDakika.ToString();

            // Dosya
            TxtMaxFileSize.Text = config.Dosya.MaksimumBoyutMB.ToString();
            ChkCompression.IsChecked = config.Dosya.SikistirmaEtkin;

            // Startup durumu kontrol et
            try {
                using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", false))
                {
                    var val = key?.GetValue("PDFAutomation");
                    if (val != null) ChkRunAtStartup.IsChecked = true;
                }
            } catch { }
        }

        private void ChkRunAtStartup_CheckedChanged(object sender, RoutedEventArgs e)
        {
            try
            {
                using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true))
                {
                    if (ChkRunAtStartup.IsChecked == true)
                    {
                        var exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName;
                        key.SetValue("PDFAutomation", $"\"{exePath}\"");
                    }
                    else
                    {
                        key.DeleteValue("PDFAutomation", false);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Başlangıç ayarı değiştirilirken hata: {ex.Message}", "Hata");
            }
        }

        private void UpdateAuthUiState()
        {
            if (ChkOAuth2.IsChecked == true)
            {
                PnlBasicAuth.Visibility = Visibility.Collapsed;
                PnlOAuth2.Visibility = Visibility.Visible;
            }
            else
            {
                PnlBasicAuth.Visibility = Visibility.Visible;
                PnlOAuth2.Visibility = Visibility.Collapsed;
            }
        }

        private void ChkOAuth2_Click(object sender, RoutedEventArgs e)
        {
            UpdateAuthUiState();
        }

        private async void SetupPrinter_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var printerName = "PDF Otomasyon";
                
                // Önce yazıcının var olup olmadığını kontrol et
                var checkScript = $@"
$printer = Get-Printer -Name '{printerName}' -ErrorAction SilentlyContinue
if ($printer) {{
    Write-Output 'EXISTS'
}} else {{
    Write-Output 'NOT_EXISTS'
}}
";
                
                var checkResult = await Task.Run(() =>
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = "powershell.exe",
                        Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{checkScript}\"",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true
                    };
                    var proc = Process.Start(psi);
                    var output = proc.StandardOutput.ReadToEnd().Trim();
                    proc.WaitForExit();
                    return output;
                });

                // Yazıcıyı kur (Varsa önce kaldırır)
                var incomingDir = _configManager.Config.Klasorler.Incoming;
                if (!Directory.Exists(incomingDir))
                {
                    Directory.CreateDirectory(incomingDir);
                }

                var portName = Path.Combine(incomingDir, "temp_print_job.pdf");
                var driverName = "Microsoft Print To PDF";

                // PowerShell betiği - Önce temizlik yapar sonra kurar
                var script = $@"
$portName = '{portName}'
$printerName = '{printerName}'
$driverName = '{driverName}'
$incomingDir = '{incomingDir}'

# 1. Temizlik (Varsa kaldır)
Write-Host 'Eski yazici kontrol ediliyor...'
if (Get-Printer -Name $printerName -ErrorAction SilentlyContinue) {{
    Write-Host 'Eski yazici kaldiriliyor...'
    Remove-Printer -Name $printerName -ErrorAction SilentlyContinue
}}
if (Get-PrinterPort -Name $portName -ErrorAction SilentlyContinue) {{
    Write-Host 'Eski port kaldiriliyor...'
    Remove-PrinterPort -Name $portName -ErrorAction SilentlyContinue
}}

# 2. Klasör Yetkileri
if (Test-Path $incomingDir) {{
    $acl = Get-Acl $incomingDir
    $permission = 'BUILTIN\Users','FullControl','Allow'
    $accessRule = New-Object System.Security.AccessControl.FileSystemAccessRule($permission)
    $acl.SetAccessRule($accessRule)
    Set-Acl $incomingDir $acl
}}

# 3. Port ve Yazıcı Ekleme
Write-Host 'Yazici kuruluyor...'
$regPath = 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Ports'
Set-ItemProperty -Path $regPath -Name $portName -Value ''
Restart-Service -Name Spooler -Force
Start-Sleep -Seconds 3

try {{
    Add-Printer -Name $printerName -DriverName $driverName -PortName $portName -ErrorAction Stop
}} catch {{
    $drivers = Get-PrinterDriver | Where-Object {{ $_.Name -like '*Print*to*PDF*' }}
    if ($drivers) {{
        Add-Printer -Name $printerName -DriverName $drivers[0].Name -PortName $portName -ErrorAction Stop
    }} else {{
        throw $_.Exception
    }}
}}
";
                // Yönetici olarak çalıştır
                await RunPowerShellScript(script);
                
                MessageBox.Show(
                    $"✅ Yazıcı başarıyla kuruldu: {printerName}\n\n" +
                    "Artık Word/Excel'den bu yazıcıya yazdırabilirsiniz.\n" +
                    "Yazdırılan dosyalar otomatik olarak e-posta ile gönderilecek.",
                    "Başarılı",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Yazıcı kurulumu sırasında bir hata oluştu:\n\n" + ex.Message + "\n\n" +
                    "Lütfen uygulamayı yönetici olarak çalıştırmayı veya manuel kurulum yapmayı deneyin.",
                    "Hata",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private Task RunPowerShellScript(string script)
        {
            return Task.Run(() =>
            {
                string tempFile = Path.Combine(Path.GetTempPath(), $"setup_{Guid.NewGuid():N}.ps1");
                try
                {
                    // PowerShell'in karakterleri doğru algılaması için UTF8 with BOM kullan
                    var encoding = new System.Text.UTF8Encoding(true);
                    File.WriteAllText(tempFile, script, encoding);

                    var psi = new ProcessStartInfo
                    {
                        FileName = "powershell.exe",
                        // Start-Process -Verb RunAs kullanarak yönetici izni iste, -File ile temp scripti çalıştır
                        Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"Start-Process powershell -Verb RunAs -ArgumentList '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', '{tempFile}' -Wait\"",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardError = true,
                        RedirectStandardOutput = true
                    };

                    using (var process = Process.Start(psi))
                    {
                        process.WaitForExit();
                    }
                }
                finally
                {
                    if (File.Exists(tempFile))
                    {
                        try { File.Delete(tempFile); } catch { }
                    }
                }
            });
        }

        private void BtnSaveSettings_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var config = _configManager.Config;

                // SMTP
                config.Smtp.Sunucu = TxtSmtpServer.Text;
                config.Smtp.Port = int.Parse(TxtSmtpPort.Text);
                config.Smtp.OAuth2Kullan = ChkOAuth2.IsChecked ?? false;
                config.Smtp.KullaniciAdi = TxtSmtpUsername.Text;
                config.Smtp.Sifre = TxtSmtpPassword.Password;
                config.Smtp.TenantId = TxtTenantId.Text;
                config.Smtp.ClientId = TxtClientId.Text;
                config.Smtp.ClientSecret = TxtClientSecret.Password;

                // E-posta
                config.Eposta.Gonderen = TxtSender.Text;
                config.Eposta.Alici = TxtRecipient.Text;
                config.Eposta.KonuFormati = TxtSubjectFormat.Text;

                // Klasörler
                config.Klasorler.Incoming = TxtIncoming.Text;
                config.Klasorler.Sent = TxtSent.Text;
                config.Klasorler.Failed = TxtFailedPath.Text;
                config.Klasorler.Logs = TxtLogs.Text;

                // Retry
                config.Retry.MaksimumDeneme = int.Parse(TxtMaxRetry.Text);
                config.Retry.AralikDakika = int.Parse(TxtRetryInterval.Text);

                // Dosya
                config.Dosya.MaksimumBoyutMB = int.Parse(TxtMaxFileSize.Text);
                config.Dosya.SikistirmaEtkin = ChkCompression.IsChecked ?? true;

                _configManager.SaveConfiguration();

                MessageBox.Show("Ayarlar başarıyla kaydedildi!", "Başarılı", 
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ayarlar kaydedilirken hata: {ex.Message}", "Hata", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnResetSettings_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Tüm ayarlar varsayılan değerlere döndürülecek. Emin misiniz?", 
                "Onay", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                _configManager.Config.Smtp = new SmtpSettings();
                _configManager.Config.Eposta = new EmailSettings();
                _configManager.Config.Klasorler = new FolderSettings();
                _configManager.Config.Retry = new RetrySettings();
                _configManager.Config.Dosya = new FileSettings();
                _configManager.SaveConfiguration();
                LoadSettings();

                MessageBox.Show("Ayarlar varsayılan değerlere döndürüldü.", "Bilgi", 
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void BtnStartStop_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_fileWatcher.IsRunning)
                {
                    _fileWatcher.Stop();
                }
                else
                {
                    if (!_configManager.ValidateConfiguration())
                    {
                        MessageBox.Show("Lütfen önce ayarları yapılandırın!", "Uyarı", 
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    _fileWatcher.Start();
                }

                UpdateStartStopButton();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Servis başlatılırken hata: {ex.Message}", "Hata", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdateStartStopButton()
        {
            if (_fileWatcher.IsRunning)
            {
                BtnStartStop.Content = "⏸️ Durdur";
                BtnStartStop.Background = System.Windows.Media.Brushes.OrangeRed;
                MenuStartStop.Header = "Durdur";
            }
            else
            {
                BtnStartStop.Content = "▶️ Başlat";
                BtnStartStop.Background = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(76, 175, 80));
                MenuStartStop.Header = "Başlat";
            }
        }

        private async void BtnTestEmail_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                TxtTestResult.Text = "Test e-postası gönderiliyor...";
                TxtTestResult.Foreground = System.Windows.Media.Brushes.Blue;

                if (!_configManager.ValidateConfiguration())
                {
                    TxtTestResult.Text = "❌ Hata: Lütfen önce ayarları yapılandırın!";
                    TxtTestResult.Foreground = System.Windows.Media.Brushes.Red;
                    return;
                }

                // Test PDF oluştur
                var testPdfPath = Path.Combine(Path.GetTempPath(), "test.pdf");
                File.WriteAllText(testPdfPath, "%PDF-1.4\nTest PDF");

                var kullanici = Environment.UserDomainName + "\\" + Environment.UserName;
                
                // Email formatını hazırla
                var dosyaAdi = "test.pdf";
                var govde = _emailService.FormatBody(dosyaAdi, kullanici, "Test Butonu");

                var message = new EmailMessage
                {
                    DosyaYolu = testPdfPath,
                    DosyaAdi = dosyaAdi,
                    Gonderen = _configManager.Config.Eposta.Gonderen,
                    Alici = _configManager.Config.Eposta.Alici,
                    Konu = "Test - PDF Otomasyon Sistemi",
                    Govde = govde,
                    DosyaBoyutu = new FileInfo(testPdfPath).Length,
                    Kullanici = kullanici,
                    Tarih = DateTime.Now
                };

                var success = await _emailService.SendEmailAsync(message);

                if (success)
                {
                    TxtTestResult.Text = "✅ Test e-postası başarıyla gönderildi!";
                    TxtTestResult.Foreground = System.Windows.Media.Brushes.Green;
                }
                else
                {
                    TxtTestResult.Text = "❌ Test e-postası gönderilemedi. Lütfen logları kontrol edin.";
                    TxtTestResult.Foreground = System.Windows.Media.Brushes.Red;
                }

                // Test dosyasını sil
                if (File.Exists(testPdfPath))
                    File.Delete(testPdfPath);
            }
            catch (Exception ex)
            {
                TxtTestResult.Text = $"❌ Hata: {ex.Message}";
                TxtTestResult.Foreground = System.Windows.Media.Brushes.Red;
            }
        }



        private void UpdateDashboard()
        {
            try
            {
                var logs = _loggingService.GetTodaysLogs();

                TxtSentToday.Text = logs.Count(l => l.Durum == "basarili").ToString();
                TxtPending.Text = _retryService.GetPendingCount().ToString();
                TxtFailed.Text = logs.Count(l => l.Durum == "basarisiz").ToString();

                // Son 10 işlemi göster
                LstRecentLogs.ItemsSource = logs.OrderByDescending(l => l.Zaman).Take(10).ToList();

                // Tüm logları göster
                RefreshLogs();
            }
            catch
            {
                // Hata durumunda sessizce devam et
            }
        }

        private void BtnRefreshLogs_Click(object sender, RoutedEventArgs e)
        {
            RefreshLogs();
        }

        private void CmbLogFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            RefreshLogs();
        }

        private void ChkAutoRefresh_Checked(object sender, RoutedEventArgs e)
        {
            if (_updateTimer != null && !_updateTimer.IsEnabled)
            {
                _updateTimer.Start();
            }
        }

        private void ChkAutoRefresh_Unchecked(object sender, RoutedEventArgs e)
        {
            if (_updateTimer != null && _updateTimer.IsEnabled)
            {
                _updateTimer.Stop();
            }
        }

        private void RefreshLogs()
        {
            try
            {
                var logs = _loggingService.GetTodaysLogs();
                var filter = (CmbLogFilter.SelectedItem as ComboBoxItem)?.Content.ToString();

                if (filter == "Başarılı")
                    logs = logs.Where(l => l.Durum == "basarili").ToList();
                else if (filter == "Başarısız")
                    logs = logs.Where(l => l.Durum == "basarisiz").ToList();
                else if (filter == "Tekrar")
                    logs = logs.Where(l => l.Durum == "tekrar").ToList();

                LstLogs.ItemsSource = logs.OrderByDescending(l => l.Zaman).ToList();
                TxtLogCount.Text = $"Toplam: {logs.Count} kayıt";
            }
            catch
            {
                // Hata durumunda sessizce devam et
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // Pencereyi kapat yerine minimize et
            e.Cancel = true;
            this.Hide();
        }

        private void TrayIcon_TrayLeftMouseDown(object sender, RoutedEventArgs e)
        {
            this.Show();
            this.WindowState = WindowState.Normal;
            this.Activate();
        }

        private void MenuOpen_Click(object sender, RoutedEventArgs e)
        {
            this.Show();
            this.WindowState = WindowState.Normal;
            this.Activate();
        }

        private void MenuStartStop_Click(object sender, RoutedEventArgs e)
        {
            BtnStartStop_Click(sender, e);
        }

        private void MenuExit_Click(object sender, RoutedEventArgs e)
        {
            _fileWatcher?.Stop();
            TrayIcon?.Dispose();
            Application.Current.Shutdown();
        }

        // ===== Windows Servis Yönetimi =====

        private void CheckWindowsServiceStatus()
        {
            try
            {
                using (var sc = new ServiceController(SERVICE_NAME))
                {
                    var status = sc.Status;
                    Dispatcher.Invoke(() =>
                    {
                        switch (status)
                        {
                            case ServiceControllerStatus.Running:
                                TxtWinServiceStatus.Text = "Durum: ✅ Çalışıyor";
                                ServiceStatusBorder.Background = new System.Windows.Media.SolidColorBrush(
                                    System.Windows.Media.Color.FromRgb(232, 245, 233));
                                TxtWinServiceDetail.Text = "Servis arka planda çalışıyor. Oturum kapatılsa bile dosya izleme devam edecek.";
                                BtnInstallService.IsEnabled = false;
                                BtnUninstallService.IsEnabled = true;
                                break;
                            case ServiceControllerStatus.Stopped:
                                TxtWinServiceStatus.Text = "Durum: ⏹ Durduruldu";
                                ServiceStatusBorder.Background = new System.Windows.Media.SolidColorBrush(
                                    System.Windows.Media.Color.FromRgb(255, 243, 224));
                                TxtWinServiceDetail.Text = "Servis kurulu ama durdurulmuş. Yeniden başlatmak için 'Servisi Kur' butonuna tıklayın.";
                                BtnInstallService.IsEnabled = true;
                                BtnUninstallService.IsEnabled = true;
                                break;
                            default:
                                TxtWinServiceStatus.Text = $"Durum: {status}";
                                ServiceStatusBorder.Background = new System.Windows.Media.SolidColorBrush(
                                    System.Windows.Media.Color.FromRgb(238, 238, 238));
                                TxtWinServiceDetail.Text = "";
                                break;
                        }
                    });
                }
            }
            catch (InvalidOperationException)
            {
                // Servis kurulu değil
                Dispatcher.Invoke(() =>
                {
                    TxtWinServiceStatus.Text = "Durum: ❌ Kurulu Değil";
                    ServiceStatusBorder.Background = new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromRgb(255, 235, 238));
                    TxtWinServiceDetail.Text = "Windows Servisi henüz kurulmamış. Kurmak için 'Servisi Kur' butonuna tıklayın.";
                    BtnInstallService.IsEnabled = true;
                    BtnUninstallService.IsEnabled = false;
                });
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() =>
                {
                    TxtWinServiceStatus.Text = "Durum: ⚠ Kontrol edilemedi";
                    TxtWinServiceDetail.Text = ex.Message;
                });
            }
        }

        private async void BtnInstallService_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                BtnInstallService.IsEnabled = false;
                TxtWinServiceStatus.Text = "Durum: ⏳ Servis kuruluyor...";

                // Publish klasörünü bul
                string serviceExePath = null;
                var searchPaths = new[]
                {
                    // Kurulum dizini - Program Files
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "PDFAutomation", "Service"),
                    // Kurulum dizini - alternatif
                    @"C:\Program Files\PDFAutomation\Service",
                    // Uygulama dizini yanında
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "Service"),
                    // Geliştirme ortamı - proje yanında
                    Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "PDFAutomation.Service", "publish")),
                    // Aynı dizinde
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "PDFAutomation.Service", "publish"),
                    // Üst dizinde
                    Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "PDFAutomation.Service", "publish")),
                };

                foreach (var searchPath in searchPaths)
                {
                    var candidate = Path.Combine(searchPath, "PDFAutomation.Service.exe");
                    if (File.Exists(candidate))
                    {
                        serviceExePath = candidate;
                        break;
                    }
                }

                // Bulunamadıysa publish et
                if (serviceExePath == null)
                {
                    var projectDir = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "PDFAutomation.Service"));
                    var csprojPath = Path.Combine(projectDir, "PDFAutomation.Service.csproj");

                    if (!File.Exists(csprojPath))
                    {
                        TxtWinServiceStatus.Text = "Durum: ❌ Hata";
                        TxtWinServiceDetail.Text = "PDFAutomation.Service projesi bulunamadı. Lütfen publish-service.bat dosyasını manuel çalıştırın.";
                        BtnInstallService.IsEnabled = true;
                        return;
                    }

                    var publishDir = Path.Combine(projectDir, "publish");
                    TxtWinServiceDetail.Text = "Proje derleniyor, lütfen bekleyin...";
                    
                    var publishResult = await Task.Run(() =>
                    {
                        var psi = new ProcessStartInfo
                        {
                            FileName = "dotnet",
                            Arguments = $"publish \"{csprojPath}\" -c Release -o \"{publishDir}\" --self-contained false",
                            UseShellExecute = false,
                            CreateNoWindow = true,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true
                        };
                        var proc = Process.Start(psi);
                        proc.WaitForExit(60000);
                        return proc.ExitCode;
                    });

                    serviceExePath = Path.Combine(publishDir, "PDFAutomation.Service.exe");
                    if (publishResult != 0 || !File.Exists(serviceExePath))
                    {
                        TxtWinServiceStatus.Text = "Durum: ❌ Hata";
                        TxtWinServiceDetail.Text = "Servis projesi derlenemedi. Lütfen publish-service.bat dosyasını manuel çalıştırın.";
                        BtnInstallService.IsEnabled = true;
                        return;
                    }
                }

                // Config artık C:\ProgramData\PDFAutomation\config.json da paylaşılıyor,
                // kopyalamaya gerek yok

                // PowerShell ile servisi kur (Yönetici izni iste)
                var script = $@"
$serviceName = '{SERVICE_NAME}'
$exePath = '{serviceExePath.Replace("'", "''")}'

# Eski servisi durdur ve sil
$existing = Get-Service -Name $serviceName -ErrorAction SilentlyContinue
if ($existing) {{
    Stop-Service -Name $serviceName -Force -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 2
    sc.exe delete $serviceName
    Start-Sleep -Seconds 2
}}

# Yeni servisi oluştur
sc.exe create $serviceName binPath= $exePath start= auto DisplayName= 'PDF Otomasyon Servisi'
sc.exe description $serviceName 'PDF dosyalarini otomatik olarak e-posta ile gonderen servis.'
sc.exe failure $serviceName reset= 86400 actions= restart/60000/restart/120000/restart/300000

# Servisi başlat
Start-Service -Name $serviceName
";

                await RunPowerShellScript(script);

                await Task.Delay(2000);
                CheckWindowsServiceStatus();

                MessageBox.Show(
                    "Windows Servisi başarıyla kuruldu!\n\n" +
                    "• Servis otomatik başlatılacak (PC açılışında)\n" +
                    "• Oturum kapatılsa bile çalışmaya devam edecek\n" +
                    "• Hata durumunda otomatik yeniden başlayacak",
                    "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                TxtWinServiceStatus.Text = "Durum: ❌ Hata";
                TxtWinServiceDetail.Text = ex.Message;
                MessageBox.Show($"Servis kurulurken hata: {ex.Message}", "Hata",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                BtnInstallService.IsEnabled = true;
            }
        }

        private async void BtnUninstallService_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show(
                "Windows Servisi kaldırılacak. Emin misiniz?\n\n" +
                "Not: Servis kaldırıldıktan sonra uygulama kapalıyken dosya izleme çalışmayacaktır.",
                "Onay", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            try
            {
                BtnUninstallService.IsEnabled = false;
                TxtWinServiceStatus.Text = "Durum: ⏳ Servis kaldırılıyor...";

                var script = $@"
$serviceName = '{SERVICE_NAME}'
Stop-Service -Name $serviceName -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 2
sc.exe delete $serviceName
";

                await RunPowerShellScript(script);
                await Task.Delay(2000);
                CheckWindowsServiceStatus();

                MessageBox.Show("Windows Servisi başarıyla kaldırıldı.", "Bilgi",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Servis kaldırılırken hata: {ex.Message}", "Hata",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                BtnUninstallService.IsEnabled = true;
            }
        }

        private void BtnRefreshServiceStatus_Click(object sender, RoutedEventArgs e)
        {
            CheckWindowsServiceStatus();
        }
    }
}