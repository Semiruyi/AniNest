param(
    [string]$FlutterProject = "frontend/aninest_flutter",
    [switch]$SeedCache,
    [string]$Proxy = ""
)

$ErrorActionPreference = "Stop"

function Write-Step([string]$message) {
    $timestamp = Get-Date -Format "HH:mm:ss"
    Write-Host "[$timestamp] $message" -ForegroundColor Cyan
}

function Get-CMakeCommand {
    $candidates = @(
        "C:\Program Files\Microsoft Visual Studio\18\Community\Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin\cmake.exe",
        "C:\Program Files\Microsoft Visual Studio\17\Community\Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin\cmake.exe"
    )

    foreach ($candidate in $candidates) {
        if (Test-Path $candidate) {
            return $candidate
        }
    }

    $fromPath = Get-Command cmake -ErrorAction SilentlyContinue
    if ($null -ne $fromPath) {
        return $fromPath.Source
    }

    throw "cmake.exe not found."
}

function Get-PackageRoot([object[]]$packages, [string]$name) {
    $package = $packages | Where-Object { $_.name -eq $name } | Select-Object -First 1
    if ($null -eq $package) {
        throw "Package '$name' not found in package_config.json."
    }

    $rootUri = [string]$package.rootUri
    if (-not $rootUri.StartsWith("file:///")) {
        throw "Unsupported rootUri for package '$name': $rootUri"
    }

    return ([System.Uri]$rootUri).LocalPath
}

function Ensure-Directory([string]$path) {
    New-Item -ItemType Directory -Force -Path $path | Out-Null
}

function Set-ArchiveProxy([string]$proxy) {
    if ([string]::IsNullOrWhiteSpace($proxy)) {
        return
    }

    $env:http_proxy = $proxy
    $env:https_proxy = $proxy
}

function Download-Archive([string]$url, [string]$destination, [string]$expectedMd5, [string]$proxy) {
    $needsDownload = $true

    if (Test-Path $destination) {
        $existingMd5 = (Get-FileHash -Algorithm MD5 -LiteralPath $destination).Hash.ToLowerInvariant()
        if ($existingMd5 -eq $expectedMd5.ToLowerInvariant()) {
            $needsDownload = $false
        } else {
            Remove-Item -LiteralPath $destination -Force
        }
    }

    if (-not $needsDownload) {
        return
    }

    Write-Step "Downloading $(Split-Path $destination -Leaf)..."
    Set-ArchiveProxy $proxy
    curl.exe -L $url -o $destination | Out-Null

    $downloadedMd5 = (Get-FileHash -Algorithm MD5 -LiteralPath $destination).Hash.ToLowerInvariant()
    if ($downloadedMd5 -ne $expectedMd5.ToLowerInvariant()) {
        throw "MD5 mismatch for $(Split-Path $destination -Leaf)."
    }
}

function Expand-ArchiveWithCMake([string]$cmake, [string]$archive, [string]$destination) {
    Remove-Item -LiteralPath $destination -Recurse -Force -ErrorAction SilentlyContinue
    Ensure-Directory $destination
    & $cmake -E tar xzf $archive | Out-Null
}

function Seed-StableCache([string]$cmake, [string]$cacheRoot, [string]$proxy) {
    Ensure-Directory $cacheRoot

    $libmpvArchive = Join-Path $cacheRoot "mpv-dev-x86_64-20230924-git-652a1dd.7z"
    $angleArchive = Join-Path $cacheRoot "ANGLE.7z"
    $libmpvDir = Join-Path $cacheRoot "libmpv"
    $angleDir = Join-Path $cacheRoot "ANGLE"

    Download-Archive `
        "https://github.com/media-kit/libmpv-win32-video-build/releases/download/2023-09-24/mpv-dev-x86_64-20230924-git-652a1dd.7z" `
        $libmpvArchive `
        "a832ef24b3a6ff97cd2560b5b9d04cd8" `
        $proxy

    Download-Archive `
        "https://github.com/alexmercerind/flutter-windows-ANGLE-OpenGL-ES/releases/download/v1.0.1/ANGLE.7z" `
        $angleArchive `
        "e866f13e8d552348058afaafe869b1ed" `
        $proxy

    if (-not (Test-Path (Join-Path $libmpvDir "libmpv-2.dll"))) {
        Write-Step "Extracting libmpv stable cache..."
        Push-Location $cacheRoot
        try {
            Expand-ArchiveWithCMake $cmake $libmpvArchive $libmpvDir
        }
        finally {
            Pop-Location
        }
    }

    if (-not (Test-Path (Join-Path $angleDir "libEGL.dll"))) {
        Write-Step "Extracting ANGLE stable cache..."
        Push-Location $cacheRoot
        try {
            Expand-ArchiveWithCMake $cmake $angleArchive $angleDir
        }
        finally {
            Pop-Location
        }
    }

    $nestedHeaders = Join-Path $libmpvDir "include\mpv\client.h"
    $flatHeader = Join-Path $libmpvDir "include\client.h"
    if ((Test-Path $nestedHeaders) -and (-not (Test-Path $flatHeader))) {
        Write-Step "Flattening libmpv headers..."
        Copy-Item -LiteralPath (Join-Path $libmpvDir "include\mpv\*") -Destination (Join-Path $libmpvDir "include") -Recurse -Force
    }
}

$root = Resolve-Path (Join-Path $PSScriptRoot "..")
$flutterProjectPath = Resolve-Path (Join-Path $root $FlutterProject)
$packageConfigPath = Join-Path $flutterProjectPath ".dart_tool\package_config.json"

if (-not (Test-Path $packageConfigPath)) {
    throw "package_config.json not found at $packageConfigPath. Run 'flutter pub get' first."
}

$packageConfig = Get-Content -LiteralPath $packageConfigPath -Raw | ConvertFrom-Json
$mediaKitRoot = Get-PackageRoot $packageConfig.packages "media_kit_libs_windows_video"
$cmakePath = Join-Path $mediaKitRoot "windows\CMakeLists.txt"
$cmakeExe = Get-CMakeCommand

if (-not (Test-Path $cmakePath)) {
    throw "media_kit_libs_windows_video Windows CMakeLists.txt not found at $cmakePath."
}

$patchedCMake = @'
# This file is a part of media_kit (https://github.com/media-kit/media-kit).
#
# Copyright © 2021 & onwards, Hitesh Kumar Saini <saini123hitesh@gmail.com>.
# All rights reserved.
# Use of this source code is governed by MIT license that can be found in the LICENSE file.

cmake_minimum_required(VERSION 3.14)

# This option is read by the other packages which are part of package:media_kit.
option(MEDIA_KIT_LIBS_AVAILABLE "package:media_kit libraries are available." ON)

set(PROJECT_NAME "media_kit_libs_windows_video")
project(${PROJECT_NAME} LANGUAGES CXX)

# Deal with MSVC incompatiblity
add_compile_definitions(_DISABLE_CONSTEXPR_MUTEX_CONSTRUCTOR)

# ------------------------------------------------------------------------------
function(download_and_verify url md5 locationForArchive)
  if(EXISTS "${locationForArchive}")
    file(MD5 "${locationForArchive}" ARCHIVE_MD5)
    if(NOT md5 STREQUAL ARCHIVE_MD5)
      file(REMOVE "${locationForArchive}")
      message(STATUS "MD5 mismatch. File deleted.")
    endif()
  endif()

  if(NOT EXISTS "${locationForArchive}")
    message(STATUS "Downloading archive from ${url}...")
    file(DOWNLOAD "${url}" "${locationForArchive}")
    message(STATUS "Downloaded archive to ${locationForArchive}.")

    file(MD5 "${locationForArchive}" ARCHIVE_MD5)
    if(md5 STREQUAL ARCHIVE_MD5)
      message(STATUS "${locationForArchive} Verification successful.")
    else()
      message(FATAL_ERROR "${locationForArchive} Integrity check failed, please try to re-build project again.")
    endif()
  endif()
endfunction()

function(extract_archive_if_needed archive output_dir expected_path)
  if(EXISTS "${expected_path}")
    return()
  endif()

  message(STATUS "Extracting ${archive} to ${output_dir}...")
  file(REMOVE_RECURSE "${output_dir}")
  file(MAKE_DIRECTORY "${output_dir}")

  execute_process(
    COMMAND "${CMAKE_COMMAND}" -E tar xzf "${archive}"
    WORKING_DIRECTORY "${output_dir}"
    RESULT_VARIABLE extract_result
  )

  if(NOT extract_result EQUAL 0 OR NOT EXISTS "${expected_path}")
    message(FATAL_ERROR "Failed to extract ${archive} into ${output_dir}.")
  endif()
endfunction()

function(flatten_libmpv_headers libmpv_root)
  if(EXISTS "${libmpv_root}/include/mpv/client.h" AND NOT EXISTS "${libmpv_root}/include/client.h")
    file(COPY "${libmpv_root}/include/mpv/" DESTINATION "${libmpv_root}/include")
  endif()
endfunction()

function(sync_directory source_dir destination_dir)
  file(REMOVE_RECURSE "${destination_dir}")
  file(MAKE_DIRECTORY "${destination_dir}")
  file(COPY "${source_dir}/" DESTINATION "${destination_dir}")
endfunction()

# libmpv archive containing the pre-built shared libraries & headers.
set(LIBMPV "mpv-dev-x86_64-20230924-git-652a1dd.7z")
set(LIBMPV_URL "https://github.com/media-kit/libmpv-win32-video-build/releases/download/2023-09-24/${LIBMPV}")
set(LIBMPV_MD5 "a832ef24b3a6ff97cd2560b5b9d04cd8")

set(MEDIA_KIT_CACHE_ROOT "$ENV{LOCALAPPDATA}/media_kit_libs_windows_video" CACHE PATH
  "Stable cache directory for media_kit Windows native libraries.")
if(NOT MEDIA_KIT_CACHE_ROOT)
  set(MEDIA_KIT_CACHE_ROOT "${CMAKE_BINARY_DIR}/media_kit_libs_windows_video")
endif()
file(MAKE_DIRECTORY "${MEDIA_KIT_CACHE_ROOT}")

set(LIBMPV_ARCHIVE "${MEDIA_KIT_CACHE_ROOT}/${LIBMPV}")
set(LIBMPV_CACHE_SRC "${MEDIA_KIT_CACHE_ROOT}/libmpv")
set(LIBMPV_SRC "${CMAKE_BINARY_DIR}/libmpv")
set(LIBMPV_DLL "${LIBMPV_SRC}/libmpv-2.dll")

download_and_verify(
  ${LIBMPV_URL}
  ${LIBMPV_MD5}
  ${LIBMPV_ARCHIVE}
)

extract_archive_if_needed("${LIBMPV_ARCHIVE}" "${LIBMPV_CACHE_SRC}" "${LIBMPV_CACHE_SRC}/libmpv-2.dll")
flatten_libmpv_headers("${LIBMPV_CACHE_SRC}")
sync_directory("${LIBMPV_CACHE_SRC}" "${LIBMPV_SRC}")

# ------------------------------------------------------------------------------

# ANGLE archive containing the pre-built shared libraries & headers.
set(ANGLE "ANGLE.7z")
set(ANGLE_URL "https://github.com/alexmercerind/flutter-windows-ANGLE-OpenGL-ES/releases/download/v1.0.1/ANGLE.7z")
set(ANGLE_MD5 "e866f13e8d552348058afaafe869b1ed")

set(ANGLE_ARCHIVE "${MEDIA_KIT_CACHE_ROOT}/${ANGLE}")
set(ANGLE_CACHE_SRC "${MEDIA_KIT_CACHE_ROOT}/ANGLE")
set(ANGLE_SRC "${CMAKE_BINARY_DIR}/ANGLE")
set(ANGLE_DLL "${ANGLE_SRC}/libEGL.dll")

download_and_verify(
  ${ANGLE_URL}
  ${ANGLE_MD5}
  ${ANGLE_ARCHIVE}
)

extract_archive_if_needed("${ANGLE_ARCHIVE}" "${ANGLE_CACHE_SRC}" "${ANGLE_CACHE_SRC}/libEGL.dll")
sync_directory("${ANGLE_CACHE_SRC}" "${ANGLE_SRC}")

# ------------------------------------------------------------------------------
set(PLUGIN_NAME "media_kit_libs_windows_video_plugin")

add_library(
  ${PLUGIN_NAME} SHARED
  "include/media_kit_libs_windows_video/media_kit_libs_windows_video_plugin_c_api.h"
  "media_kit_libs_windows_video_plugin_c_api.cpp"
)

apply_standard_settings(${PLUGIN_NAME})

set_target_properties(
  ${PLUGIN_NAME}
  PROPERTIES
  CXX_VISIBILITY_PRESET
  hidden
)
target_compile_definitions(
  ${PLUGIN_NAME}
  PRIVATE
  FLUTTER_PLUGIN_IMPL
)

target_include_directories(
  ${PLUGIN_NAME} INTERFACE
  "${CMAKE_CURRENT_SOURCE_DIR}/include"
)
target_link_libraries(
  ${PLUGIN_NAME}
  PRIVATE
  flutter
  flutter_wrapper_plugin
)

set(
  media_kit_libs_windows_video_bundled_libraries
  "${LIBMPV_DLL}"
  "${ANGLE_SRC}/d3dcompiler_47.dll"
  "${ANGLE_SRC}/libEGL.dll"
  "${ANGLE_SRC}/libGLESv2.dll"
  "${ANGLE_SRC}/vk_swiftshader.dll"
  "${ANGLE_SRC}/vulkan-1.dll"
  "${ANGLE_SRC}/zlib.dll"
  PARENT_SCOPE
)
'@

Write-Step "Patching $cmakePath"
[System.IO.File]::WriteAllText($cmakePath, $patchedCMake, (New-Object System.Text.UTF8Encoding($false)))

if ($SeedCache) {
    $cacheRoot = Join-Path $env:LOCALAPPDATA "media_kit_libs_windows_video"
    Write-Step "Seeding stable cache at $cacheRoot"
    Seed-StableCache $cmakeExe $cacheRoot $Proxy
}

Write-Step "media_kit Windows patch applied."
Write-Host "Next steps:" -ForegroundColor Green
Write-Host "  1. cd $flutterProjectPath" -ForegroundColor Green
Write-Host "  2. flutter build windows --debug" -ForegroundColor Green
Write-Host "     or flutter run -d windows" -ForegroundColor Green
