@echo off
REM Windows convenience wrapper to call the bash setup script.
REM For developers who prefer running from CMD/PowerShell.

where bash >nul 2>&1
if %ERRORLEVEL% NEQ 0 (
    echo ERROR: bash not found. Git Bash is required.
    echo Please install Git for Windows: https://git-scm.com/
    exit /b 1
)

bash "%~dp0setup-hooks.sh"
