using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Security.Principal;
using System.Threading.Tasks;
using System.Windows;

namespace PDFAutomation.Installer
{
    public partial class InstallerWindow : Window
    {
        public InstallerWindow()
        {
            InitializeComponent();
            CheckAdminStatus();
            SetWindowIcon();
        }

        private void SetWindowIcon()
        {
            try
            {
                // Pack URI format: pack://application:,,,/AssemblyName;component/ResourcePath
                var iconUri = new Uri("pack://application:,,,/PDFOtomasyon_Setup;component/icon.ico", UriKind.Absolute);
                this.Icon = System.Windows.Media.Imaging.BitmapFrame.Create(iconUri);
            }
            catch 
            {
                // Hata durumunda varsayılan ikonu kullan veya boş bırak (çökme olmaz)
            }
        }

        private void CheckAdminStatus()
        {
            bool isAdmin = new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);
            if (!isAdmin)
            {
                MessageBox.Show(
                    "⚠️ YÖNETİCİ İZNİ YOK!\n\nLütfen Setup dosyasını kapatıp 'SAĞ TIK -> YÖNETİCİ OLARAK ÇALIŞTIR' yapın.",
                    "Kritik Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void BtnBrowse_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Kurulum dizinini seçin",
                FileName = "Klasör Seç",
                Filter = "Klasör|*.folder",
                InitialDirectory = @"C:\Program Files"
            };
            if (dlg.ShowDialog() == true)
            {
                TxtInstallPath.Text = Path.GetDirectoryName(dlg.FileName);
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private async void BtnInstall_Click(object sender, RoutedEventArgs e)
        {
            var installDir = TxtInstallPath.Text.Trim();
            if (string.IsNullOrEmpty(installDir))
            {
                MessageBox.Show("Lütfen kurulum dizini belirleyin.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            BtnInstall.IsEnabled = false;
            BtnCancel.IsEnabled = false;

            try
            {
                var appDestDir = Path.Combine(installDir, "App");
                var serviceDestDir = Path.Combine(installDir, "Service");
                var configDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "PDFAutomation");

                // --- 1. DOSYALARI ÇIKART ---
                UpdateProgress(10, "Dosyalar çıkartılıyor...");
                try {
                    await Task.Run(() => ExtractPayload(installDir));
                    // MessageBox.Show("✅ 1. Adım Başarılı: Dosyalar dizine çıkartıldı.", "Adım Raporu");
                } catch (Exception ex) {
                    MessageBox.Show($"❌ 1. Adım HATA: Dosya çıkartılamadı!\n\n{ex.Message}", "Hata");
                }

                // --- 2. SERVİS KURULUMU ---
                if (ChkService.IsChecked == true)
                {
                    UpdateProgress(40, "Windows Servisi kuruluyor...");
                    try {
                        InstallService(Path.Combine(serviceDestDir, "PDFAutomation.Service.exe"));
                        // MessageBox.Show("✅ 2. Adım Başarılı: Windows Servisi kuruldu ve başlatıldı.", "Adım Raporu");
                    } catch (Exception ex) {
                        MessageBox.Show($"❌ 2. Adım HATA: Servis kurulamadı!\n\n{ex.Message}", "Hata");
                    }
                }

                // --- 3. SANAL YAZICI KURULUMU ---
                UpdateProgress(60, "Sanal Yazıcı oluşturuluyor...");
                try {
                    await InstallVirtualPrinter();
                    // MessageBox.Show("✅ 3. Adım Başarılı: Sanal Yazıcı ve Port oluşturuldu.", "Adım Raporu");
                } catch (Exception ex) {
                    MessageBox.Show($"❌ 3. Adım HATA: Yazıcı kurulamadı!\n\n{ex.Message}", "Hata");
                }

                // --- 4. YETKİLER ---
                UpdateProgress(70, "Klasör yetkileri düzenleniyor...");
                try {
                    var sharedOutDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "PDFAutomation", "PDF_Out");
                    GrantFolderPermissions(sharedOutDir);
                    // MessageBox.Show("✅ 4. Adım Başarılı: Klasör izinleri ayarlandı.", "Adım Raporu");
                } catch (Exception ex) {
                    MessageBox.Show($"❌ 4. Adım HATA: Yetkilendirme başarısız!\n\n{ex.Message}", "Hata");
                }

                // --- 5. KISAYOLLAR ---
                UpdateProgress(80, "Kısayollar oluşturuluyor...");
                try {
                    if (ChkDesktopShortcut.IsChecked == true) await CreateDesktopShortcuts(appDestDir);
                    if (ChkStartMenu.IsChecked == true) await CreateStartMenuShortcut(appDestDir);
                    // MessageBox.Show("✅ 5. Adım Başarılı: Masaüstü ve Başlat Menüsü kısayolları oluşturuldu.", "Adım Raporu");
                } catch (Exception ex) {
                    MessageBox.Show($"❌ 5. Adım HATA: Kısayol oluşturulamadı!\n\n{ex.Message}", "Hata");
                }

                // --- 6. UNINSTALLER VE REGISTRY ---
                UpdateProgress(90, "Sistem kayıtları yapılıyor...");
                try {
                    CreateUninstaller(installDir);
                    WriteUninstallRegistry(installDir);
                    // MessageBox.Show("✅ 6. Adım Başarılı: Uninstaller ve Kayıt Defteri bilgileri eklendi.", "Adım Raporu");
                } catch (Exception ex) {
                    MessageBox.Show($"❌ 6. Adım HATA: Sistem kaydı yapılamadı!\n\n{ex.Message}", "Hata");
                }

                UpdateProgress(100, "✅ KURULUM BİTTİ!");
                // MessageBox.Show("TEBRİKLER! Tüm adımlar raporlandı ve kurulum tamamlandı.", "Kurulum Bitti", MessageBoxButton.OK, MessageBoxImage.Information);

                var appExePath = Path.Combine(appDestDir, "PDFAutomation.exe");
                if (File.Exists(appExePath))
                {
                    var result = MessageBox.Show("Uygulamayı şimdi başlatmak ister misiniz?", "Başlat", MessageBoxButton.YesNo, MessageBoxImage.Question);
                    if (result == MessageBoxResult.Yes)
                    {
                        Process.Start(new ProcessStartInfo { FileName = appExePath, UseShellExecute = true });
                    }
                }

                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Kurulum sırasında beklenmedik hata:\n\n{ex.Message}", "Kritik Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                BtnInstall.IsEnabled = true;
                BtnCancel.IsEnabled = true;
            }
        }

        private void UpdateProgress(int value, string message)
        {
            Dispatcher.Invoke(() => { ProgressBar.Value = value; TxtStatus.Text = message; });
        }

        private void ExtractPayload(string installDir)
        {
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = "PDFAutomation.Installer.payload.zip";

            using (var stream = assembly.GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                    throw new Exception($"Gömülü kaynak bulunamadı: {resourceName}");

                using (var archive = new ZipArchive(stream))
                {
                    foreach (var entry in archive.Entries)
                    {
                        var destinationPath = Path.GetFullPath(Path.Combine(installDir, entry.FullName));
                        
                        if (entry.FullName.EndsWith("/") || entry.FullName.EndsWith("\\"))
                        {
                            Directory.CreateDirectory(destinationPath);
                            continue;
                        }

                        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath));
                        entry.ExtractToFile(destinationPath, true);
                    }
                }
            }
        }

        private void InstallService(string serviceExePath)
        {
            var sn = "PDFOtomasyonServisi";
            RunCmd("sc.exe", $"stop {sn}", true);
            RunCmd("sc.exe", $"delete {sn}", true);
            
            string binPathEscaped = $"\"{serviceExePath}\"";
            RunCmd("sc.exe", $"create {sn} binPath= {binPathEscaped} start= auto DisplayName= \"PDF Otomasyon Servisi\"", false);
            RunCmd("sc.exe", $"description {sn} \"PDF dosyalarını otomatik olarak e-posta ile gönderen arka plan servisi.\"", false);
            RunCmd("sc.exe", $"start {sn}", false);
        }

        private void RunCmd(string file, string args, bool ignoreError)
        {
            try {
                var psi = new ProcessStartInfo { 
                    FileName = file, 
                    Arguments = args, 
                    UseShellExecute = false, 
                    CreateNoWindow = true 
                };
                var p = Process.Start(psi);
                p?.WaitForExit(10000);
            } catch { if (!ignoreError) throw; }
        }

        private async Task CreateDesktopShortcuts(string appDestDir)
        {
            var desktopPaths = new List<string> {
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory)
            };

            foreach (var path in desktopPaths)
            {
                if (string.IsNullOrEmpty(path)) continue;
                var shortcutPath = Path.Combine(path, "PDF Otomasyon.lnk");
                var exePath = Path.Combine(appDestDir, "PDFAutomation.exe");
                
                var script = "$ws = New-Object -ComObject WScript.Shell; " +
                             $"$s = $ws.CreateShortcut('{shortcutPath.Replace("'", "''")}'); " +
                             $"$s.TargetPath = '{exePath.Replace("'", "''")}'; " +
                             $"$s.WorkingDirectory = '{appDestDir.Replace("'", "''")}'; " +
                             $"$s.IconLocation = '{exePath.Replace("'", "''")}'; " +
                             "$s.Save()";
                await RunPowerShellScript(script, "Masaüstü Kısayolu");
            }
        }

        private async Task CreateStartMenuShortcut(string appDestDir)
        {
            var startMenuPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu), "Programs", "PDF Otomasyon");
            Directory.CreateDirectory(startMenuPath);
            var shortcutPath = Path.Combine(startMenuPath, "PDF Otomasyon.lnk");
            var exePath = Path.Combine(appDestDir, "PDFAutomation.exe");

            var script = "$ws = New-Object -ComObject WScript.Shell; " +
                         $"$s = $ws.CreateShortcut('{shortcutPath.Replace("'", "''")}'); " +
                         $"$s.TargetPath = '{exePath.Replace("'", "''")}'; " +
                         $"$s.WorkingDirectory = '{appDestDir.Replace("'", "''")}'; " +
                         $"$s.IconLocation = '{exePath.Replace("'", "''")}'; " +
                         "$s.Save()";
            await RunPowerShellScript(script, "Başlat Menüsü Kısayolu");
        }

        private async Task InstallVirtualPrinter()
        {
            var printerName = "PDF Otomasyon";
            var sharedDataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "PDFAutomation");
            var incomingDir = Path.Combine(sharedDataDir, "PDF_Out", "Incoming");
            var portName = Path.Combine(incomingDir, "temp_print_job.pdf");

            if (!Directory.Exists(incomingDir)) Directory.CreateDirectory(incomingDir);

            var script = $@"
$portName = '{portName}'
$printerName = '{printerName}'
$driverName = 'Microsoft Print to PDF'

if (Get-Printer -Name $printerName -ErrorAction SilentlyContinue) {{ Remove-Printer -Name $printerName }}
$ports = Get-PrinterPort | Where-Object {{ $_.Name -like '*temp_print_job.pdf*' }}
foreach ($p in $ports) {{ Remove-PrinterPort -Name $p.Name -ErrorAction SilentlyContinue }}

$regPath = 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Ports'
Set-ItemProperty -Path $regPath -Name $portName -Value ''
Restart-Service -Name Spooler -Force
Start-Sleep -Seconds 3

try {{
    Add-Printer -Name $printerName -DriverName $driverName -PortName $portName -ErrorAction Stop
}} catch {{
    $drivers = Get-PrinterDriver | Where-Object {{ $_.Name -like '*Print*to*PDF*' }}
    if ($drivers) {{ Add-Printer -Name $printerName -DriverName $drivers[0].Name -PortName $portName }}
}}

if (!(Get-Printer -Name $printerName -ErrorAction SilentlyContinue)) {{
    throw 'Yazıcı doğrulanamadı. Port veya Driver kaynaklı bir sorun olabilir.'
}}
";
            await RunPowerShellScript(script, "Sanal Yazıcı Kurulumu");
        }

        private Task RunPowerShellScript(string script, string taskName)
        {
            return Task.Run(() =>
            {
                string tempFile = Path.Combine(Path.GetTempPath(), $"setup_{Guid.NewGuid():N}.ps1");
                try
                {
                    File.WriteAllText(tempFile, script, new System.Text.UTF8Encoding(true));
                    var psi = new ProcessStartInfo
                    {
                        FileName = "powershell.exe",
                        Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{tempFile}\"",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardError = true
                    };

                    using (var process = Process.Start(psi))
                    {
                        string errors = process.StandardError.ReadToEnd();
                        process.WaitForExit(20000);
                        if (process.ExitCode != 0)
                        {
                            throw new Exception($"{taskName} hatası (Kod: {process.ExitCode}):\n{errors}");
                        }
                    }
                }
                finally
                {
                    if (File.Exists(tempFile)) try { File.Delete(tempFile); } catch { }
                }
            });
        }

        private void CreateUninstaller(string installDir)
        {
            var sharedDataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "PDFAutomation");
            var script = "@echo off\r\n" +
                "title PDF Otomasyon Sistemi - Kaldirma\r\n" +
                "net session >nul 2>&1\r\n" +
                "if %ERRORLEVEL% NEQ 0 (\r\n" +
                "    powershell -Command \"Start-Process '%~f0' -Verb RunAs\"\r\n" +
                "    exit /b\r\n" +
                ")\r\n" +
                "sc stop PDFOtomasyonServisi >nul 2>&1\r\n" +
                "timeout /t 2 /nobreak >nul\r\n" +
                "sc delete PDFOtomasyonServisi >nul 2>&1\r\n" +
                "taskkill /f /im PDFAutomation.exe >nul 2>&1\r\n" +
                "powershell -NoProfile -Command \"Get-Printer -Name 'PDF Otomasyon' -ErrorAction SilentlyContinue | Remove-Printer; Get-PrinterPort | Where-Object { $_.Name -like '*temp_print_job.pdf*' } | Remove-PrinterPort\" >nul 2>&1\r\n" +
                "del /q \"%PUBLIC%\\Desktop\\PDF Otomasyon.lnk\" 2>nul\r\n" +
                "del /q \"%USERPROFILE%\\Desktop\\PDF Otomasyon.lnk\" 2>nul\r\n" +
                "reg delete \"HKLM\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\PDFOtomasyon\" /f 2>nul\r\n" +
                "rmdir /s /q \"" + sharedDataDir + "\" 2>nul\r\n" +
                "echo.\r\n" +
                "echo Basariyla kaldirildi.\r\n" +
                "pause";
            File.WriteAllText(Path.Combine(installDir, "Uninstall.bat"), script);
        }

        private void GrantFolderPermissions(string path)
        {
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);
            RunCmd("icacls.exe", $"\"{path}\" /grant Everyone:(OI)(CI)F /T", false);
        }

        private void WriteUninstallRegistry(string installDir)
        {
            try
            {
                var keyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\PDFOtomasyon";
                using (var key = Microsoft.Win32.Registry.LocalMachine.CreateSubKey(keyPath))
                {
                    if (key != null)
                    {
                        var exePath = Path.Combine(installDir, "App", "PDFAutomation.exe");
                        var iconPath = Path.Combine(installDir, "App", "icon.ico");
                        var uninstallPath = Path.Combine(installDir, "Uninstall.bat");

                        key.SetValue("DisplayName", "PDF Otomasyon Sistemi");
                        key.SetValue("DisplayIcon", iconPath); // Doğrudan .ico dosyasına yönlendirildi
                        key.SetValue("DisplayVersion", "1.0.0");
                        key.SetValue("Publisher", "Ufuk Kaya");
                        key.SetValue("UninstallString", uninstallPath);
                        key.SetValue("InstallLocation", installDir);
                        key.SetValue("NoModify", 1);
                        key.SetValue("NoRepair", 1);
                    }
                }
            }
            catch (Exception ex)
            {
               // Registry yazma hatası kurulumu bozmasın, loglanabilir
               Console.WriteLine("Registry error: " + ex.Message);
            }
        }
    }
}
