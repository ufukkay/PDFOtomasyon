# 📄 PDF Automation System / PDF Otomasyon Sistemi

![License](https://img.shields.io/badge/license-MIT-blue.svg) ![Platform](https://img.shields.io/badge/platform-Windows-lightgrey.svg) ![Framework](https://img.shields.io/badge/.NET-6.0-purple.svg)

**[English](#english) | [Türkçe](#türkçe)**

---

<a name="english"></a>

## 🇬🇧 English

**PDF Automation System** is a comprehensive Windows desktop application and background service designed to streamline your document workflow. It automatically monitors specific folders (or a virtual printer), processes incoming PDF files, compresses them, and emails them to designated recipients without manual intervention.

### 🚀 Key Features

- **📂 Automated Folder Monitoring:** Instantly detects PDF files dropped into the "Incoming" directory.
- **🖨️ Virtual Printer Integration:** Installs a "PDF Automation" printer driver. Any document printed to this printer is automatically captured, converted to PDF, and processed.
- **📧 Advanced Email Engine:**
  - Supports standard SMTP.
  - **Microsoft 365 (OAuth2)** support for modern, secure authentication.
  - Customizable subject lines and body text.
- **⚙️ Windows Service:** Runs silently in the background (System Tray), ensuring operations continue even when the main UI is closed.
- **📉 Smart Compression:** Optimizes large PDF files under a specified limit (e.g., 35MB) to ensure successful email delivery.
- **🔄 Retry Mechanism:** Automatically retries failed shipments (e.g., during internet outages) at configurable intervals.
- **📊 Dashboard & Reporting:** Visualizes daily statistics, success/failure rates, and detailed logs in real-time.

### 🛠️ Installation

1.  Download the latest release (`PDFOtomasyon_Setup.exe`).
2.  Run the installer and follow the on-screen instructions.
3.  The application will automatically launch.
4.  Navigate to the **Settings** tab to configure your SMTP server and target email addresses.

### 💻 Tech Stack

- **Language:** C#
- **Framework:** .NET 6.0 (WPF for UI, Worker Service for Background)
- **Database:** JSON-based local storage (No SQL required).
- **Libraries:** MailKit (Email), Serilog (Logging), PDFSharp (Processing).

---

<a name="türkçe"></a>

## 🇹🇷 Türkçe

**PDF Otomasyon Sistemi**, belge iş akışınızı hızlandırmak için tasarlanmış kapsamlı bir Windows masaüstü uygulaması ve arka plan servisidir. Belirlediğiniz klasörleri (veya sanal yazıcıyı) izler, gelen PDF dosyalarını otomatik olarak işler, sıkıştırır ve belirlenen alıcılara e-posta ile gönderir.

### 🚀 Öne Çıkan Özellikler

- **📂 Otomatik Klasör İzleme:** "Incoming" klasörüne düşen dosyaları anında algılar ve işleme alır.
- **🖨️ Sanal Yazıcı Desteği:** Sisteme "PDF Otomasyon" adında bir yazıcı kurar. Word, Excel veya herhangi bir programdan bu yazıcıya çıktı gönderdiğinizde dosya otomatik olarak PDF'e çevrilir ve sisteme dahil edilir.
- **📧 Gelişmiş E-posta Motoru:**
  - Standart SMTP sunucuları ile uyumludur.
  - **Microsoft 365 (OAuth2)** desteği ile modern ve güvenli kimlik doğrulama sağlar.
- **⚙️ Windows Servisi:** Uygulama arayüzü kapalı olsa bile arka planda (System Tray) sessizce çalışmaya devam eder.
- **📉 Akıllı Sıkıştırma:** Büyük dosyaları e-posta ek boyutuna (örn. 35MB) sığacak şekilde optimize eder.
- **🔄 Tekrar Deneme (Retry) Mekanizması:** İnternet kesintisi gibi durumlarda gönderilemeyen dosyaları belirlenen aralıklarla tekrar dener.
- **📊 Dashboard & Raporlama:** Günlük gönderim sayılarını, bekleyen ve hatalı işlemleri anlık grafiklerle sunar.

### 🛠️ Kurulum ve Kullanım

1.  **Release** klasöründeki veya GitHub Releases bölümündeki `PDFOtomasyon_Setup.exe` dosyasını indirin.
2.  Kurulumu başlatın; sihirbaz gerekli dosyaları `Program Files` altına yükleyecek ve kısayolları oluşturacaktır.
3.  Uygulamayı açın ve **Ayarlar** sekmesine gidin.
4.  SMTP (Gönderici) bilgilerinizi ve Alıcı e-posta adreslerini girip "Kaydet" butonuna basın.

### 💻 Teknik Altyapı

- **Dil:** C#
- **Altyapı:** .NET 6.0 (Arayüz için WPF, Arka plan için Windows Service)
- **Veri Kaydı:** JSON tabanlı yerel kayıt (Veritabanı kurulumu gerektirmez).
- **Kütüphaneler:** MailKit (E-posta), Serilog (Loglama), PDFSharp (PDF İşleme).

---

### 👨‍💻 Developer / Geliştirici

**Ufuk Kaya**
_Project developed for automated document management workflows._
