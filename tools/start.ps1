$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$appDir = Join-Path $root 'CapsWriter\CapsWriter-Offline'
$logDir = Join-Path $root '部署记录'
$tempDir = Join-Path $root '临时文件'
$mutex = New-Object System.Threading.Mutex($false, 'Local\CapsWriterLocalVoiceLauncher')
if (-not $mutex.WaitOne(0)) { exit 0 }
try {
    New-Item -ItemType Directory -Path $logDir,$tempDir,(Join-Path $root '验证') -Force | Out-Null
    Remove-Item -LiteralPath (Join-Path $root '临时文件\停止请求.flag') -ErrorAction SilentlyContinue
    & (Join-Path $PSScriptRoot 'start-status.ps1')
    $env:TEMP = $tempDir
    $env:TMP = $tempDir
    $env:OMP_NUM_THREADS = '4'
    $env:OPENBLAS_NUM_THREADS = '4'
    $env:MKL_NUM_THREADS = '4'
    $serverExe = Join-Path $appDir 'start_server.exe'
    $clientExe = Join-Path $appDir 'start_client.exe'
    if (-not (Test-Path -LiteralPath $serverExe)) { throw '没有找到语音程序，请检查 CapsWriter 文件夹。' }
    $listener = Get-NetTCPConnection -LocalPort 6016 -State Listen -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($listener) {
        $owner = Get-CimInstance Win32_Process -Filter "ProcessId=$($listener.OwningProcess)"
        if ($owner.ExecutablePath -ne $serverExe) { throw '6016 端口被其他程序占用，未启动重复服务。' }
    } else {
        Start-Process -FilePath $serverExe -WorkingDirectory $appDir -WindowStyle Hidden -RedirectStandardOutput (Join-Path $logDir 'server.stdout.log') -RedirectStandardError (Join-Path $logDir 'server.stderr.log') | Out-Null
        $ready = $false
        for ($i = 0; $i -lt 120; $i++) {
            if (Test-Path -LiteralPath (Join-Path $root '临时文件\停止请求.flag')) { return }
            if (Get-NetTCPConnection -LocalPort 6016 -State Listen -ErrorAction SilentlyContinue) { $ready = $true; break }
            Start-Sleep -Milliseconds 500
        }
        if (-not $ready) { throw '语音服务未能启动，请查看部署记录中的日志。' }
    }
    if (Test-Path -LiteralPath (Join-Path $root '临时文件\停止请求.flag')) { return }
    if (-not (Test-Path -LiteralPath (Join-Path $root 'tools\audio-devices.json'))) {
        try { & (Join-Path $PSScriptRoot 'run-verification.ps1') -Script (Join-Path $PSScriptRoot 'list_audio_devices.py') | Out-Null }
        catch { '首次麦克风枚举未完成，可在设置中刷新设备。' | Add-Content -LiteralPath (Join-Path $logDir 'launcher-errors.log') -Encoding UTF8 }
    }
    $clients = Get-CimInstance Win32_Process -Filter "name='start_client.exe'" | Where-Object { $_.ExecutablePath -eq $clientExe }
    if (-not $clients) {
        Start-Process -FilePath $clientExe -WorkingDirectory $appDir -WindowStyle Hidden -RedirectStandardOutput (Join-Path $logDir 'client.stdout.log') -RedirectStandardError (Join-Path $logDir 'client.stderr.log') | Out-Null
    }
} catch {
    $_ | Out-String | Add-Content -LiteralPath (Join-Path $logDir 'launcher-errors.log') -Encoding UTF8
    (New-Object -ComObject WScript.Shell).Popup($_.Exception.Message, 0, '语音输入启动提示', 16) | Out-Null
    exit 1
} finally {
    $mutex.ReleaseMutex()
    $mutex.Dispose()
}
