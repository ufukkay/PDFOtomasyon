@echo off
setlocal EnableDelayedExpansion

REM Get admin rights
>nul 2>&1 "%SYSTEMROOT%\system32\cacls.exe" "%SYSTEMROOT%\system32\config\system"
if '%errorlevel%' NEQ '0' (
    echo Yonetici izinleri isteniyor...
    goto UACPrompt
) else ( goto gotAdmin )

:UACPrompt
    echo Set UAC = CreateObject^("Shell.Application"^) > "%temp%\getadmin.vbs"
    set params = %*:"=""
    echo UAC.ShellExecute "cmd.exe", "/c %~s0 %params%", "", "runas", 1 >> "%temp%\getadmin.vbs"
    "%temp%\getadmin.vbs"
    del "%temp%\getadmin.vbs"
    exit /B

:gotAdmin
    pushd "%CD%"
    CD /D "%~dp0"

echo ==========================================
echo PDF OTOMASYON YAZICI KURULUMU
echo ==========================================
echo.

set PORTNAME=%USERPROFILE%\Desktop\PDF_Out\Incoming\temp_print_job.pdf
set PRINTERNAME=PDF Otomasyon
set DRIVERNAME=Microsoft Print to PDF

echo 1. Port Olusturuluyor (Registry)...
reg add "HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Ports" /v "%PORTNAME%" /t REG_SZ /d "" /f

if %errorlevel% neq 0 (
    echo HATA: Port olusturulamadi!
    pause
    exit /b
)

echo.
echo 2. Spooler Servisi Yeniden Baslatiliyor...
net stop spooler
net start spooler
timeout /t 3 /nobreak >nul

echo.
echo 3. Yazici Ekleniyor...
powershell -Command "Add-Printer -Name '%PRINTERNAME%' -DriverName '%DRIVERNAME%' -PortName '%PORTNAME%'"

if %errorlevel% neq 0 (
    echo HATA: Yazici eklenemedi! Lutfen yukaridaki hatayi okuyun.
    echo Olasiliklar:
    echo - Surucu adi yanlis olabilir.
    echo - Port henuz hazir degil.
) else (
    echo.
    echo BASARILI: Yazici kuruldu!
)

echo.
echo Islem tamamlandi.
pause
