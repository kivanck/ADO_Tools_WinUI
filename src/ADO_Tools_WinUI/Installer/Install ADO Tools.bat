@echo off
:: ADO Tools — Double-click this file to install.
:: Requests admin elevation, then runs QuickInstall.ps1.

:: Check if already running as admin
net session >nul 2>&1
if %errorlevel% == 0 (
    :: Already elevated — run the script directly
    PowerShell -ExecutionPolicy Bypass -File "%~dp0QuickInstall.ps1"
    exit /b
)

:: Not elevated — relaunch this bat as admin
echo Requesting administrator privileges...
PowerShell -Command "Start-Process cmd.exe -ArgumentList '/c \"\"%~f0\"\"' -Verb RunAs"
