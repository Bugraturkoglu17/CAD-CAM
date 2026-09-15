<#
    CadAutomation.Inventor addin'ini yerel geliştirme için derler ve COM'a kaydeder.
    RegAsm registry'ye (HKLM) yazdigi icin YONETICI olarak calistirilmalidir:
    PowerShell'i "Yönetici olarak çalıştır" ile açıp bu scripti çalıştırın.

    Kayıt sadece bu makinede geliştirme/test amaçlıdır. Gerçek dağıtımda (Faz 8 - installer)
    bu adım MSI kurulum paketinin bir custom action'ı tarafından otomatik yapılacak.
#>

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repoRoot "src\CadAutomation.Inventor\CadAutomation.Inventor.csproj"
$outputDll = Join-Path $repoRoot "src\CadAutomation.Inventor\bin\Debug\net48\CadAutomation.Inventor.dll"
$regAsm = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\RegAsm.exe"

$currentPrincipal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $currentPrincipal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Write-Error "Bu script YONETICI olarak calistirilmali (RegAsm registry'ye HKLM altina yazar)."
    exit 1
}

Write-Host "Derleniyor: $projectPath"
dotnet build $projectPath
if ($LASTEXITCODE -ne 0) { Write-Error "Derleme basarisiz."; exit 1 }

Write-Host "COM'a kaydediliyor: $outputDll"
& $regAsm $outputDll /codebase
if ($LASTEXITCODE -ne 0) { Write-Error "RegAsm basarisiz."; exit 1 }

Write-Host ""
Write-Host "Kayit tamamlandi. Inventor 2026'yi kapatip tekrar acin - CAD Automation addin'i yuklenmeli." -ForegroundColor Green
