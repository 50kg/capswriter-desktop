$root = Split-Path -Parent $PSScriptRoot
$appDir = Join-Path $root 'CapsWriter\CapsWriter-Offline'
if (Test-Path -LiteralPath (Join-Path $root '临时文件\停止请求.flag')) { return }
& (Join-Path $PSScriptRoot 'start-status.ps1')
$env:TEMP = Join-Path $root '临时文件'; $env:TMP = $env:TEMP
$exe = Join-Path $appDir 'start_client.exe'
Get-CimInstance Win32_Process -Filter "name='start_client.exe'" | Where-Object {$_.ExecutablePath -eq $exe} | ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }
if (Test-Path -LiteralPath (Join-Path $root '临时文件\停止请求.flag')) { return }
Start-Process -FilePath (Join-Path $appDir 'start_client.exe') -WorkingDirectory $appDir -WindowStyle Hidden -RedirectStandardOutput (Join-Path $root '部署记录\client.stdout.log') -RedirectStandardError (Join-Path $root '部署记录\client.stderr.log') | Out-Null
