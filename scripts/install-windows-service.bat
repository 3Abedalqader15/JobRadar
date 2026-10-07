@echo off
echo ===================================================
echo Registering JobRadar.Workers as a Windows Service...
echo ===================================================

set BINPATH=%~dp0..\src\JobRadar.Workers\bin\Debug\net8.0\JobRadar.Workers.exe

if not exist "%BINPATH%" (
    echo Error: %BINPATH% does not exist. Please run 'dotnet build' first.
    pause
    exit /b 1
)

sc.exe create JobRadarWorkers binpath= "%BINPATH%" start= auto DisplayName= "JobRadar Background Workers"
if %errorlevel% neq 0 (
    echo.
    echo Note: If this failed with Access Denied, please run this script as Administrator.
    pause
    exit /b %errorlevel%
)

sc.exe description JobRadarWorkers "JobRadar ingestion, crawling, and AI enrichment background worker service."

:: Configure automatic restart on failure: restart after 30s for 1st, 2nd, and subsequent crashes
sc.exe failure JobRadarWorkers reset= 0 actions= restart/30000/restart/30000/restart/30000

echo Starting service...
sc.exe start JobRadarWorkers

echo Service registered and started successfully!
pause
