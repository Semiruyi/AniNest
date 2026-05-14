@echo off
setlocal
chcp 65001 >nul

powershell -NoProfile -ExecutionPolicy Bypass -Command "[Console]::OutputEncoding = [System.Text.Encoding]::UTF8; & '%~dp0publish.ps1' -Runtime win-x64 -Configuration Release"

endlocal
