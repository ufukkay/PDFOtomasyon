$ErrorActionPreference = "Stop"
$ScriptPath = $PSScriptRoot
$PayloadDir = Join-Path $ScriptPath "Payload"
$InstallerDir = Join-Path $ScriptPath "PDFAutomation.Installer"
$OutputDir = Join-Path $ScriptPath "..\Release"

# .NET Assembly yukle (Zip icin)
Add-Type -AssemblyName System.IO.Compression.FileSystem

Write-Host "--- PDF Otomasyon Build Baslatiliyor (v2) ---" -ForegroundColor Cyan

# 1. Temizlik
Write-Host "1. Temizlik yapiliyor..."
if (Test-Path $PayloadDir) { Remove-Item $PayloadDir -Recurse -Force }
if (!(Test-Path $OutputDir)) { New-Item $OutputDir -ItemType Directory -Force | Out-Null }
New-Item $PayloadDir -ItemType Directory -Force | Out-Null

# 2. UI Derleme (Self-Contained)
Write-Host "2. PDFAutomation (UI) derleniyor (Self-Contained)..."
$UiProject = Join-Path $ScriptPath "PDFAutomation\PDFAutomation.csproj"
dotnet publish $UiProject -c Release -o "$PayloadDir\App" --self-contained true -r win-x64
if ($LASTEXITCODE -ne 0) { throw "UI derleme hatasi!" }

# 2.1 Dil klasorlerini temizle (Sadece tr ve en kalsin)
Write-Host "2.1 Dil klasorleri temizleniyor (Sadece tr ve en kalsin)..."
Get-ChildItem -Path "$PayloadDir\App" -Directory | Where-Object { 
    $_.Name -match "^[a-z]{2}(-[A-Z]{2,4})?$" -and $_.Name -ne "tr" -and $_.Name -ne "en"
} | Remove-Item -Recurse -Force

# 3. Servis Derleme (Self-Contained)
Write-Host "3. PDFAutomation.Service derleniyor (Self-Contained)..."
$ServiceProject = Join-Path $ScriptPath "PDFAutomation.Service\PDFAutomation.Service.csproj"
dotnet publish $ServiceProject -c Release -o "$PayloadDir\Service" --self-contained true -r win-x64
if ($LASTEXITCODE -ne 0) { throw "Servis derleme hatasi!" }

# 4. Ek Dosyalar (Varsa)
# Config dosyasini App klasorune kopyala (varsayilan config)
$ConfigFile = Join-Path $ScriptPath "PDFAutomation\config.json"
if (Test-Path $ConfigFile) {
    Copy-Item $ConfigFile "$PayloadDir\App\config.json"
}

# Icon dosyasini App klasorune kopyala (Program Ekle/Kaldir icin)
$IconFile = Join-Path $ScriptPath "PDFAutomation\icon.ico"
if (Test-Path $IconFile) {
    Copy-Item $IconFile "$PayloadDir\App\icon.ico"
    Write-Host "   Icon.ico kopyalandi."
}

# Kisa bir bekleme (Dosya kilitleri icin)
Start-Sleep -Seconds 2

# 5. Payload Paketleme (ZIP)
Write-Host "5. Payload.zip olusturuluyor..."
$ZipPath = Join-Path $InstallerDir "payload.zip"
if (Test-Path $ZipPath) { Remove-Item $ZipPath -Force }

# .NET ZipFile kullanarak sikistirma (Daha hizli ve guvenli)
try {
    [System.IO.Compression.ZipFile]::CreateFromDirectory($PayloadDir, $ZipPath)
    Write-Host "   Payload.zip boyutu: $((Get-Item $ZipPath).Length / 1MB).ToString('F2') MB"
} catch {
    Write-Error "ZIP Hatasi: $_"
    throw
}

# 6. Installer Derleme (Self-Contained Single File)
Write-Host "6. Installer derleniyor (Portable)..."
$InstallerProject = Join-Path $InstallerDir "PDFAutomation.Installer.csproj"
# Clean
dotnet clean $InstallerProject -c Release
# Self-contained ve SingleFile olarak publish et
dotnet publish $InstallerProject -c Release -o "$OutputDir" -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
if ($LASTEXITCODE -ne 0) { throw "Installer derleme hatasi!" }

# Setup adini duzenle (varsa eskiyi sil)
$SetupExe = Join-Path $OutputDir "PDFOtomasyon_Setup.exe"
$BuiltExe = Join-Path $OutputDir "PDFAutomation.Installer.exe"

if (Test-Path $BuiltExe) {
    if (Test-Path $SetupExe) { Remove-Item $SetupExe -Force }
    Rename-Item $BuiltExe "PDFOtomasyon_Setup.exe"
}

# Gereksiz pdb ve diğer dosyaları temizle (Sadece exe kalsın)
Get-ChildItem $OutputDir | Where-Object { $_.Name -ne "PDFOtomasyon_Setup.exe" } | Remove-Item -Force

Write-Host "--- ISLEM TAMAMLANDI ---" -ForegroundColor Green
Write-Host "Setup dosyasi hazir: $SetupExe"
