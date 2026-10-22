$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
[System.IO.File]::WriteAllText((Join-Path $root '临时文件\停止请求.flag'),'stop')
$appDir = [System.IO.Path]::GetFullPath((Join-Path $root 'CapsWriter\CapsWriter-Offline')).TrimEnd('\') + '\'
$owned = Get-CimInstance Win32_Process | Where-Object {
    $_.ExecutablePath -and (($_.ExecutablePath.StartsWith($appDir, [StringComparison]::OrdinalIgnoreCase) -and
    $_.Name -in @('start_server.exe', 'start_client.exe')) -or $_.ExecutablePath -in @((Join-Path $root 'tools\语音状态条.exe'),(Join-Path $root 'tools\语音输入设置.exe'),(Join-Path $root 'tools\选择麦克风.exe')))
}
foreach ($process in $owned) { Stop-Process -Id $process.ProcessId -ErrorAction SilentlyContinue }
