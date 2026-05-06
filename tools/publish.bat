@echo off
setlocal

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0publish.ps1" -Runtime win-x64 -Configuration Release

endlocal
