@echo off
REM ============================================
REM PEDAGANGPULSA - REBUILD AND RUN TESTS
REM ============================================
REM Script ini untuk rebuild dan run tests setelah cleanup database

echo.
echo ============================================
echo PEDAGANGPULSA - REBUILD AND RUN TESTS
echo ============================================
echo.

REM Step 1: Clean build artifacts
echo [1/3] Cleaning build artifacts...
call dotnet clean
if %ERRORLEVEL% NEQ 0 (
    echo ERROR: Clean failed!
    exit /b %ERRORLEVEL%
)

REM Step 2: Build project
echo.
echo [2/3] Building project...
call dotnet build
if %ERRORLEVEL% NEQ 0 (
    echo ERROR: Build failed!
    exit /b %ERRORLEVEL%
)

REM Step 3: Run tests
echo.
echo [3/3] Running unit tests...
call dotnet test PedagangPulsa.Tests/PedagangPulsa.Tests.csproj --logger "console;verbosity=detailed"
if %ERRORLEVEL% NEQ 0 (
    echo.
    echo ============================================
    echo TESTS FAILED!
    echo ============================================
    echo.
    echo Please check:
    echo 1. Database sudah di-clean dengan CLEANUP_DATABASE.sql
    echo 2. Connection string di TestDbContext.cs sudah benar
    echo 3. Tidak ada duplicate data di database
    echo.
    exit /b %ERRORLEVEL%
)

echo.
echo ============================================
echo ALL TESTS PASSED!
echo ============================================
echo.

pause
