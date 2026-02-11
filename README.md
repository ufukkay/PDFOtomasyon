# PDF Otomasyon Sistemi

**PDF Otomasyon Sistemi**, belirlediğiniz klasörleri (veya sanal yazıcıyı) izleyerek, buraya düşen PDF dosyalarını otomatik olarak işleyen, sıkıştıran ve belirlenen alıcılara e-posta ile gönderen kapsamlı bir Windows masaüstü uygulaması ve arka plan servisidir.

## 🚀 Özellikler

- **📂 Otomatik Klasör İzleme:** Belirlenen "Incoming" klasörüne düşen dosyaları anında algılar.
- **🖨️ Sanal Yazıcı Desteği:** "PDF Otomasyon" adında bir yazıcı oluşturur. Herhangi bir programdan bu yazıcıya çıktı gönderdiğinizde dosya otomatik işlem sırasına alınır.
- **📧 Gelişmiş E-posta Motoru:**
  - Standart SMTP desteği.
  - **Microsoft 365 (OAuth2)** desteği ile modern ve güvenli gönderim.
- **⚙️ Windows Servisi:** Uygulama kapalı olsa bile arka planda 7/24 çalışmaya devam eder.
- **📉 Akıllı Sıkıştırma:** Büyük PDF dosyalarını e-posta ekine sığacak şekilde optimize eder.
- **🔄 Retry (Tekrar Deneme) Mekanizması:** Gönderim başarısız olursa (internet kesintisi vb.), belirlenen aralıklarla tekrar dener.
- **📊 Dashboard & Raporlama:** Günlük gönderim istatistiklerini, bekleyen ve hatalı işlemleri görsel olarak sunar.

## 🛠️ Kurulum

1.  **Release** klasöründeki `PDFOtomasyon_Setup.exe` dosyasını çalıştırın.
2.  Kurulum sihirbazındaki adımları takip edin.
3.  Uygulama otomatik olarak açılacak ve gerekli ayarları yapmanızı isteyecektir.

## 💻 Geliştirme Ortamı

Proje **.NET 6.0** ve **WPF** kullanılarak geliştirilmiştir.

### Gereksinimler

- Visual Studio 2022
- .NET 6.0 SDK

### Proje Yapısı

- **PDFAutomation:** Ana masaüstü uygulaması (UI).
- **PDFAutomation.Service:** Arka plan Windows servisi.
- **PDFAutomation.Installer:** Kurulum ve dağıtım projesi.

## 📝 Lisans

Bu proje **Ufuk Kaya** tarafından geliştirilmiştir.
