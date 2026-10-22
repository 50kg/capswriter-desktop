$root = Split-Path -Parent $PSScriptRoot
$exe = Join-Path $PSScriptRoot '语音状态条.exe'
if (Test-Path -LiteralPath $exe) {
    Start-Process -FilePath $exe -WorkingDirectory $root -WindowStyle Normal | Out-Null
}
