param(
    [string]$Runtime = "win-x64",
    [string]$Configuration = "Release",
    [string]$Version = "",
    [switch]$Zip
)

$ErrorActionPreference = "Stop"

$root = Resolve-Path (Join-Path $PSScriptRoot "..")
$artifactsRoot = Join-Path $root "artifacts"
$publishRoot = Join-Path $artifactsRoot "publish"
$launcherTempRoot = Join-Path $artifactsRoot "publish-temp\launcher"
$appTempRoot = Join-Path $artifactsRoot "publish-temp\app"
$appOut = Join-Path $publishRoot "app"
$dataOut = Join-Path $publishRoot "data"
$launcherOut = Join-Path $publishRoot "AniNest.Launcher.exe"
$zipOut = Join-Path $artifactsRoot "AniNest-portable.zip"

if ([string]::IsNullOrWhiteSpace($Version)) {
    $Version = Get-Date -Format "yyyy.MM.dd.HHmmss"
}

function Remove-PathIfExists([string]$path) {
    if (Test-Path $path) {
        Remove-Item -LiteralPath $path -Recurse -Force
    }
}

function Copy-Folder([string]$source, [string]$destination) {
    if (-not (Test-Path $source)) {
        return
    }

    New-Item -ItemType Directory -Force -Path $destination | Out-Null
    Get-ChildItem -LiteralPath $source -Force | ForEach-Object {
        $target = Join-Path $destination $_.Name
        if ($_.PSIsContainer) {
            Copy-Folder $_.FullName $target
        } else {
            Copy-Item -LiteralPath $_.FullName -Destination $target -Force
        }
    }
}

function Write-AppManifest([string]$appDir, [string]$version) {
    $manifestPath = Join-Path $appDir "manifest.json"
    $manifest = @{
        appId = "AniNest"
        packageType = "full"
        version = $version
        baseVersion = ""
        generatedAtUtc = [DateTime]::UtcNow.ToString("o")
        description = "publish"
        files = @()
    }

    $manifest | ConvertTo-Json -Depth 5 | Set-Content -Path $manifestPath -Encoding UTF8
}

function Write-Step([string]$message) {
    $timestamp = Get-Date -Format "HH:mm:ss"
    Write-Host "[$timestamp] $message" -ForegroundColor Cyan
}

Write-Step "开始清理输出目录..."
Remove-PathIfExists $publishRoot
Remove-PathIfExists $launcherTempRoot
Remove-PathIfExists $appTempRoot
New-Item -ItemType Directory -Force -Path $publishRoot | Out-Null
New-Item -ItemType Directory -Force -Path $appTempRoot | Out-Null

Write-Step "正在发布 AniNest (主程序)，这可能需要一些时间..."
dotnet publish (Join-Path $root "src\AniNest\AniNest.csproj") -c $Configuration -r $Runtime -o $appTempRoot
Write-Step "AniNest 发布完成"

Write-Step "正在发布 Launcher (启动器)，这可能需要一些时间..."
dotnet publish (Join-Path $root "src\Launcher\AniNest.Launcher.csproj") -c $Configuration -r $Runtime -o $launcherTempRoot
Write-Step "Launcher 发布完成"

Write-Step "正在复制文件..."
Copy-Folder $appTempRoot $appOut
Copy-Folder (Join-Path $root "data") $dataOut
Write-Step "正在写入版本清单..."
Write-AppManifest $appOut $Version

$launcherExe = Join-Path $launcherTempRoot "AniNest.Launcher.exe"
if (-not (Test-Path $launcherExe)) {
    throw "AniNest.Launcher.exe not found after build."
}
Copy-Item -LiteralPath $launcherExe -Destination $launcherOut -Force
Remove-PathIfExists $launcherTempRoot
Remove-PathIfExists $appTempRoot

if ($Zip) {
    Write-Step "正在打包..."
    Remove-PathIfExists $zipOut
    Compress-Archive -Path (Join-Path $publishRoot '*') -DestinationPath $zipOut -Force
    Write-Step "打包完成: $zipOut"
} else {
    Write-Step "发布完成: $publishRoot"
}
