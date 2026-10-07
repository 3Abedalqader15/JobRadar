# JobRadar.Workers Process Supervisor / Watchdog
# Supervises the JobRadar.Workers process, automatically restarting it on exit or failure.

$ErrorActionPreference = "Continue"
$projectRoot = Split-Path -Parent $PSScriptRoot
$workerExe = Join-Path $projectRoot "src\JobRadar.Workers\bin\Debug\net8.0\JobRadar.Workers.exe"
$logDir = Join-Path $projectRoot "logs"
if (-not (Test-Path $logDir)) {
    New-Item -ItemType Directory -Path $logDir -Force | Out-Null
}
$supervisorLog = Join-Path $logDir "supervisor.log"

function Write-SupervisorLog($message) {
    $timestamp = (Get-Date -Format "yyyy-MM-dd HH:mm:ss.fff")
    $line = "[$timestamp] [SUPERVISOR] $message"
    Write-Host $line
    Add-Content -Path $supervisorLog -Value $line -Encoding utf8
}

Write-SupervisorLog "Starting JobRadar.Workers process supervisor..."
Write-SupervisorLog "Target executable: $workerExe"

if (-not (Test-Path $workerExe)) {
    Write-SupervisorLog "Worker executable not found at $workerExe. Please run 'dotnet build' first."
    exit 1
}

$env:DOTNET_ENVIRONMENT = "Development"

$restartCount = 0
while ($true) {
    $restartCount++
    Write-SupervisorLog "Launching JobRadar.Workers (run #$restartCount)..."
    
    $startInfo = New-Object System.Diagnostics.ProcessStartInfo
    $startInfo.FileName = $workerExe
    $startInfo.WorkingDirectory = (Split-Path -Parent $workerExe)
    $startInfo.UseShellExecute = $false
    $startInfo.EnvironmentVariables["DOTNET_ENVIRONMENT"] = "Development"
    
    $process = [System.Diagnostics.Process]::Start($startInfo)
    if ($null -eq $process) {
        Write-SupervisorLog "Failed to start JobRadar.Workers process. Retrying in 10s..."
        Start-Sleep -Seconds 10
        continue
    }

    Write-SupervisorLog "JobRadar.Workers PID $($process.Id) is active and running."
    $process.WaitForExit()
    $exitCode = $process.ExitCode

    Write-SupervisorLog "JobRadar.Workers PID $($process.Id) terminated with exit code $exitCode."
    Write-SupervisorLog "Restarting process in 5 seconds (auto-recovery active)..."
    Start-Sleep -Seconds 5
}
