param(
    [string]$Runtime = "win-x64",
    [string]$Configuration = "Release",
    [switch]$Zip
)

$ErrorActionPreference = "Stop"

$root = Resolve-Path (Join-Path $PSScriptRoot "..")
$artifactsRoot = Join-Path $root "artifacts"
$publishRoot = Join-Path $artifactsRoot "release"
$appPublishRoot = Join-Path $artifactsRoot "publish\app"
$appOut = Join-Path $publishRoot "app"
$dataOut = Join-Path $publishRoot "data"
$launcherOut = Join-Path $publishRoot "Launcher.exe"
$zipOut = Join-Path $artifactsRoot "LocalPlayer-portable.zip"

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

Remove-PathIfExists $publishRoot
Remove-PathIfExists $appPublishRoot
New-Item -ItemType Directory -Force -Path $publishRoot | Out-Null
New-Item -ItemType Directory -Force -Path $appPublishRoot | Out-Null

dotnet publish (Join-Path $root "LocalPlayer.csproj") -c $Configuration -r $Runtime -o $appPublishRoot
dotnet publish (Join-Path $root "Launcher\LocalPlayer.Launcher.csproj") -c $Configuration -r $Runtime -o (Join-Path $artifactsRoot "publish\launcher")

Copy-Folder $appPublishRoot $appOut
Copy-Folder (Join-Path $root "data") $dataOut

$launcherExe = Join-Path $artifactsRoot "publish\launcher\Launcher.exe"
if (-not (Test-Path $launcherExe)) {
    throw "Launcher.exe not found after build."
}
Copy-Item -LiteralPath $launcherExe -Destination $launcherOut -Force

if ($Zip) {
    Remove-PathIfExists $zipOut
    Compress-Archive -Path (Join-Path $publishRoot '*') -DestinationPath $zipOut -Force
    Write-Host "Created: $zipOut"
} else {
    Write-Host "Created: $publishRoot"
}
