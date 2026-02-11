using System.Windows;

namespace PDFAutomation.Installer
{
    public partial class App : Application 
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            this.DispatcherUnhandledException += (s, args) =>
            {
                MessageBox.Show($"KRİTİK HATA (Kurulum):\n\n{args.Exception.Message}\n\nStack:\n{args.Exception.StackTrace}", 
                    "Sistem Hatası", MessageBoxButton.OK, MessageBoxImage.Error);
                args.Handled = true;
            };
            base.OnStartup(e);
        }
    }
}
