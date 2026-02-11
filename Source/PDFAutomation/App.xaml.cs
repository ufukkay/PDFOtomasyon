using System.Windows;

namespace PDFAutomation
{
    public partial class App : Application
    {
        private static System.Threading.Mutex _mutex = null;

        protected override void OnStartup(StartupEventArgs e)
        {
            // Global hata yakalama
            this.DispatcherUnhandledException += App_DispatcherUnhandledException;

            const string appName = "PDFAutomationSystem_UniqueInstance";
            bool createdNew;

            _mutex = new System.Threading.Mutex(true, appName, out createdNew);

            if (!createdNew)
            {
                MessageBox.Show("Uygulama zaten çalışıyor.", "Bilgi", MessageBoxButton.OK, MessageBoxImage.Information);
                Application.Current.Shutdown();
                return;
            }

            base.OnStartup(e);

            // Ana pencereyi göster
            var mainWindow = new MainWindow();
            mainWindow.Show();
        }

        private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            MessageBox.Show($"Beklenmedik bir hata oluştu:\n\n{e.Exception.Message}\n\nDetay:\n{e.Exception.StackTrace}", 
                "Kritik Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            
            // Uygulamanın çökmesini engellemek isterseniz:
            // e.Handled = true; 
            // Ancak kritik hatalarda kapatmak daha güvenli olabilir.
        }

        protected override void OnExit(ExitEventArgs e)
        {
            if (_mutex != null)
            {
                _mutex.ReleaseMutex();
                _mutex.Dispose();
            }
            base.OnExit(e);
        }
    }
}
