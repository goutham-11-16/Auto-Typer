# Integration Test for Auto-Typer Named Pipe IPC
# Usage: powershell -ExecutionPolicy Bypass -File .\Test-IpcIntegration.ps1

$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$pipeName = "AutoTyper_GameBar_IPC"
$pipePath = "\\.\pipe\$pipeName"

Write-Host "====================================================" -ForegroundColor Cyan
Write-Host "   Auto-Typer Named Pipe IPC Integration Test        " -ForegroundColor Cyan
Write-Host "====================================================" -ForegroundColor Cyan

# 1. Locate AutoTyper executable
$candidates = @(
    (Join-Path $ScriptDir "AutoTyper\bin\Release\net8.0-windows\win-x64\AutoTyper-byGo.exe"),
    (Join-Path $ScriptDir "AutoTyper\bin\Debug\net8.0-windows\win-x64\AutoTyper-byGo.exe"),
    (Join-Path $ScriptDir "AutoTyper\bin\Release\net8.0-windows\win-x64\publish\AutoTyper-byGo.exe"),
    (Join-Path $ScriptDir "AutoTyper\bin\Release\net8.0-windows\AutoTyper-byGo.exe"),
    (Join-Path $ScriptDir "AutoTyper\bin\Debug\net8.0-windows\AutoTyper-byGo.exe")
)

$exePath = $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $exePath) {
    Write-Error "AutoTyper executable not found. Please run build.bat first."
}

Write-Host "AutoTyper Executable: $exePath" -ForegroundColor White

# 2. Check if AutoTyper is already running or start a test process
$proc = Get-Process "AutoTyper-byGo" -ErrorAction SilentlyContinue
$startedProcess = $false

if (-not $proc) {
    Write-Host "Launching AutoTyper test instance..." -ForegroundColor Yellow
    $proc = Start-Process -FilePath $exePath -WorkingDirectory (Split-Path -Parent $exePath) -PassThru
    $startedProcess = $true
    
    Write-Host "Waiting for AutoTyper to initialize and create named pipe..." -ForegroundColor Yellow
    $pipeFound = $false
    for ($i = 1; $i -le 40; $i++) {
        Start-Sleep -Seconds 1
        if ([System.IO.Directory]::GetFiles('\\.\pipe\') -match 'AutoTyper_GameBar_IPC') {
            Write-Host "`nNamed pipe created in $i seconds!" -ForegroundColor Green
            $pipeFound = $true
            break
        }
        Write-Host -NoNewline "."
    }

    if (-not $pipeFound) {
        Write-Error "Named pipe did not appear within 40 seconds."
    }
} else {
    Write-Host "Connecting to running AutoTyper process (PID: $($proc.Id))..." -ForegroundColor Yellow
}

try {
    # 3. Connect to Named Pipe
    Write-Host "`nConnecting to Named Pipe: $pipePath ..." -ForegroundColor White
    $pipeClient = New-Object System.IO.Pipes.NamedPipeClientStream(".", $pipeName, [System.IO.Pipes.PipeDirection]::InOut)
    $pipeClient.Connect(10000) # 10 second timeout

    $reader = New-Object System.IO.StreamReader($pipeClient, [System.Text.Encoding]::UTF8)
    $writer = New-Object System.IO.StreamWriter($pipeClient, [System.Text.Encoding]::UTF8)
    $writer.AutoFlush = $true

    Write-Host "[PASS] Connected to Named Pipe successfully!" -ForegroundColor Green

    # Function to send JSON command and read line response
    function Send-IpcCommand([string]$jsonCommand) {
        $writer.WriteLine($jsonCommand)
        $response = $reader.ReadLine()
        return $response
    }

    # Test 1: STATUS command
    Write-Host "`n[Test 1] Sending STATUS command..." -ForegroundColor Yellow
    $respJson = Send-IpcCommand '{"command":"STATUS"}'
    Write-Host "Response: $respJson" -ForegroundColor Gray
    $status = $respJson | ConvertFrom-Json
    if ($status.type -eq "STATUS" -and $status.success -eq $true) {
        Write-Host "[PASS] STATUS returned success! State: $($status.state), Mode: $($status.mode)" -ForegroundColor Green
    } else {
        Write-Error "STATUS command failed or invalid response."
    }

    # Test 2: GET_SNIPPETS command
    Write-Host "`n[Test 2] Sending GET_SNIPPETS command..." -ForegroundColor Yellow
    $snipJson = Send-IpcCommand '{"command":"GET_SNIPPETS"}'
    Write-Host "Response: $snipJson" -ForegroundColor Gray
    $snipResp = $snipJson | ConvertFrom-Json
    if ($snipResp.snippets -ne $null) {
        Write-Host "[PASS] GET_SNIPPETS returned $($snipResp.snippets.Count) snippets!" -ForegroundColor Green
    } else {
        Write-Error "GET_SNIPPETS command failed."
    }

    # Test 3: SET_SPEED command
    Write-Host "`n[Test 3] Sending SET_SPEED command..." -ForegroundColor Yellow
    $speedJson = Send-IpcCommand '{"command":"SET_SPEED","delayPerChar":15}'
    Write-Host "Response: $speedJson" -ForegroundColor Gray
    $speedResp = $speedJson | ConvertFrom-Json
    if ($speedResp.delayPerChar -eq 15) {
        Write-Host "[PASS] SET_SPEED updated delayPerChar to 15ms successfully!" -ForegroundColor Green
    } else {
        Write-Error "SET_SPEED command failed."
    }

    # Test 4: PAUSE / RESUME command
    Write-Host "`n[Test 4] Sending PAUSE and RESUME commands..." -ForegroundColor Yellow
    $pauseJson = Send-IpcCommand '{"command":"PAUSE"}'
    Write-Host "Pause Response: $pauseJson" -ForegroundColor Gray
    $pauseResp = $pauseJson | ConvertFrom-Json
    if ($pauseResp.isPaused -eq $true) {
        Write-Host "[PASS] PAUSE toggled isPaused to true!" -ForegroundColor Green
    }

    $resumeJson = Send-IpcCommand '{"command":"RESUME"}'
    Write-Host "Resume Response: $resumeJson" -ForegroundColor Gray
    $resumeResp = $resumeJson | ConvertFrom-Json
    if ($resumeResp.isPaused -eq $false) {
        Write-Host "[PASS] RESUME toggled isPaused back to false!" -ForegroundColor Green
    }

    Write-Host "`n====================================================" -ForegroundColor Cyan
    Write-Host "  ALL IPC INTEGRATION TESTS PASSED SUCCESSFULLY!    " -ForegroundColor Green
    Write-Host "====================================================" -ForegroundColor Cyan
}
finally {
    if ($pipeClient) {
        $pipeClient.Dispose()
    }
    if ($startedProcess -and $proc -and -not $proc.HasExited) {
        Write-Host "`nCleaning up test process (PID: $($proc.Id))..." -ForegroundColor Yellow
        Stop-Process -Id $proc.Id -Force
    }
}
