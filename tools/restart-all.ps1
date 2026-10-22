$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$mutex = New-Object Threading.Mutex($false,'Local\CapsWriterVoiceMaintenance')
if (-not $mutex.WaitOne(0)) { $mutex.Dispose(); return }
try {
    $state = Get-Content -LiteralPath (Join-Path $root '临时文件\语音状态.json') -Raw -Encoding UTF8 | ConvertFrom-Json
    $now = ([DateTime]::UtcNow-[DateTime]'1970-01-01').TotalSeconds
    if ($now-$state.updatedAt -lt 4 -and ($state.recording -or $state.phase -eq 'recognizing')) { throw '请先结束当前录音和识别，再重启。' }
    if (Test-Path -LiteralPath (Join-Path $root '临时文件\停止请求.flag')) { return }
    $app = Join-Path $root 'CapsWriter\CapsWriter-Offline'
    Get-CimInstance Win32_Process | Where-Object {$_.ExecutablePath -in @((Join-Path $app 'start_client.exe'),(Join-Path $app 'start_server.exe'))} | ForEach-Object {Stop-Process -Id $_.ProcessId -ErrorAction SilentlyContinue}
    if (Test-Path -LiteralPath (Join-Path $root '临时文件\停止请求.flag')) { return }
    & (Join-Path $PSScriptRoot 'start.ps1')
} catch {
    $_ | Out-String | Add-Content -LiteralPath (Join-Path $root '部署记录\launcher-errors.log') -Encoding UTF8
} finally { $mutex.ReleaseMutex(); $mutex.Dispose() }
