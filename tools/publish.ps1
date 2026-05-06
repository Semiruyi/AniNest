param(
    [string]$Runtime = "win-x64",
    [string]$Configuration = "Release",
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
$launcherOut = Join-Path $publishRoot "LocalPlayer.Launcher.exe"
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
Remove-PathIfExists $launcherTempRoot
Remove-PathIfExists $appTempRoot
New-Item -ItemType Directory -Force -Path $publishRoot | Out-Null
New-Item -ItemType Directory -Force -Path $appTempRoot | Out-Null

dotnet publish (Join-Path $root "src\LocalPlayer\LocalPlayer.csproj") -c $Configuration -r $Runtime -o $appTempRoot
dotnet publish (Join-Path $root "src\Launcher\LocalPlayer.Launcher.csproj") -c $Configuration -r $Runtime -o $launcherTempRoot

Copy-Folder $appTempRoot $appOut
Copy-Folder (Join-Path $root "data") $dataOut

$launcherExe = Join-Path $launcherTempRoot "LocalPlayer.Launcher.exe"
if (-not (Test-Path $launcherExe)) {
    throw "LocalPlayer.Launcher.exe not found after build."
}
Copy-Item -LiteralPath $launcherExe -Destination $launcherOut -Force
Remove-PathIfExists $launcherTempRoot
Remove-PathIfExists $appTempRoot

if ($Zip) {
    Remove-PathIfExists $zipOut
    Compress-Archive -Path (Join-Path $publishRoot '*') -DestinationPath $zipOut -Force
    Write-Host "Created: $zipOut"
} else {
    Write-Host "Created: $publishRoot"
}
