param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$Version = "1.0.0",
    [switch]$SkipInstaller
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$appProject = Join-Path $repoRoot "src\CutLab.App\CutLab.App.csproj"
$publishDir = Join-Path $repoRoot "publish\$Runtime"
$distDir = Join-Path $repoRoot "dist"
$issFile = Join-Path $repoRoot "installer\CutLab.iss"

Write-Host "==> CutLab Windows installer build"
Write-Host "    Version: $Version"
Write-Host "    Runtime: $Runtime"

Push-Location $repoRoot
try {
    Write-Host ""
    Write-Host "==> Running tests..."
    dotnet test --configuration $Configuration
    if ($LASTEXITCODE -ne 0) {
        throw "Tests failed. Aborting build."
    }

    if (Test-Path $publishDir) {
        Remove-Item $publishDir -Recurse -Force
    }

    Write-Host ""
    Write-Host "==> Publishing self-contained app..."
    dotnet publish $appProject `
        --configuration $Configuration `
        --runtime $Runtime `
        --self-contained true `
        -p:PublishReadyToRun=true `
        -p:DebugType=None `
        -p:DebugSymbols=false `
        -o $publishDir

    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish failed."
    }

    $exePath = Join-Path $publishDir "CutLab.exe"
    if (-not (Test-Path $exePath)) {
        throw "CutLab.exe not found in publish output."
    }

    New-Item -ItemType Directory -Force -Path $distDir | Out-Null
    $zipPath = Join-Path $distDir "CutLab-$Runtime-$Version.zip"
    if (Test-Path $zipPath) {
        Remove-Item $zipPath -Force
    }

    Write-Host ""
    Write-Host "==> Creating portable ZIP..."
    Compress-Archive -Path (Join-Path $publishDir "*") -DestinationPath $zipPath -Force
    Write-Host "    Output: $zipPath"

    if ($SkipInstaller) {
        Write-Host ""
        Write-Host "Skipped Inno Setup installer."
        return
    }

    $isccCandidates = @(
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
        "${env:LocalAppData}\Programs\Inno Setup 6\ISCC.exe"
    )

    $iscc = $isccCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
    if (-not $iscc) {
        Write-Host ""
        Write-Host "Inno Setup 6 not found. ZIP package only."
        Write-Host "Install from: https://jrsoftware.org/isdl.php"
        return
    }

    Write-Host ""
    Write-Host "==> Compiling installer..."
    & $iscc $issFile
    if ($LASTEXITCODE -ne 0) {
        throw "Inno Setup compile failed."
    }

    $setupExe = Join-Path $distDir "CutLab-Setup-$Version.exe"
    if (Test-Path $setupExe) {
        Write-Host "    Output: $setupExe"
    }
}
finally {
    Pop-Location
}

Write-Host ""
Write-Host "Build finished."
