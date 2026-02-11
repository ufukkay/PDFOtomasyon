$ErrorActionPreference = "SilentlyContinue"
$ScriptPath = $PSScriptRoot

Write-Host "--- Temizlik Baslatiliyor ---" -ForegroundColor Cyan

# Silinecek klasörler
$FoldersToRemove = @("bin", "obj", "Release", "Payload", ".vs")

# Ana dizinden (PDFOtomasyon) başlayarak temizle
$RootPath = Join-Path $ScriptPath ".."

Get-ChildItem -Path $RootPath -Recurse -Directory | Where-Object { $FoldersToRemove -contains $_.Name } | ForEach-Object {
    Write-Host "Siliniyor: $($_.FullName)"
    Remove-Item $_.FullName -Recurse -Force
}

# Source içindeki Release klasörünü de (varsa) sil
$ReleasePath = Join-Path $ScriptPath "..\Release"
if (Test-Path $ReleasePath) {
    Write-Host "Siliniyor: $ReleasePath"
    Remove-Item $ReleasePath -Recurse -Force
}

Write-Host "--- Temizlik Tamamlandi! ---" -ForegroundColor Green
Write-Host "Proje GitHub'a yuklenmeye hazir."
