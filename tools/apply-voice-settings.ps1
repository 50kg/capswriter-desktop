param([Parameter(Mandatory=$true)][string]$SettingsFile)
$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = New-Object System.Text.UTF8Encoding($false)
$root = Split-Path -Parent $PSScriptRoot
$appDir = Join-Path $root 'CapsWriter\CapsWriter-Offline'
. (Join-Path $PSScriptRoot 'voice_autostart.ps1')
$statusFile = Join-Path $root '临时文件\语音状态.json'
$stopFile = Join-Path $root '临时文件\停止请求.flag'
$settingsMutex = New-Object System.Threading.Mutex($false, 'Local\CapsWriterVoiceSettingsApply')
if (-not $settingsMutex.WaitOne(0)) { throw '设置正在应用，请稍后再试。' }
function Assert-Idle {
    $state = $null
    try { $state = Get-Content -LiteralPath $statusFile -Raw -Encoding UTF8 | ConvertFrom-Json } catch {}
    $now = ([DateTime]::UtcNow - [DateTime]'1970-01-01').TotalSeconds
    if ($state -and $now - $state.updatedAt -lt 4 -and ($state.recording -or $state.phase -eq 'recognizing')) {
        throw '请先结束当前录音，等识别完成后再保存。'
    }
}
function Save-Atomic([string]$destination,[object]$value,[string]$backupName) {
    $staged = $destination + '.saving'
    [System.IO.File]::WriteAllText($staged,($value | ConvertTo-Json -Compress),(New-Object System.Text.UTF8Encoding($false)))
    if (Test-Path -LiteralPath $destination) {
        [System.IO.File]::Replace($staged,$destination,(Join-Path $root ('部署记录\'+$backupName)),$true)
    } else { [System.IO.File]::Move($staged,$destination) }
}
$micPending = Join-Path $root 'microphone.pending.json'
$micMarker = Join-Path $root 'tools\microphone-valid.txt'
$micSaved = Join-Path $root 'microphone.json'
$micCommitted = $false
$voiceCommitted = $false
$startupChanged = $false
$oldStartup = Get-VoiceStartupRegistration
try {
    $choice = [System.IO.File]::ReadAllText($SettingsFile) | ConvertFrom-Json
    $keyPattern = '^(caps_lock|num_lock|scroll_lock|pause|print_screen|insert|menu|ctrl_l|ctrl_r|shift_l|shift_r|alt_l|alt_gr|x1|x2|f([1-9]|1[0-9]|2[0-4])|[a-z0-9])$'
    if ($choice.version -notin @(1,2) -or $choice.holdEnabled -isnot [bool] -or $choice.toggleEnabled -isnot [bool]) { throw '设置格式不正确。' }
    if ($choice.PSObject.Properties.Name -contains 'holdKeys') {
        if ($null -eq $choice.holdKeys -or $choice.holdKeys -is [string] -or $choice.holdKeys -isnot [System.Collections.IEnumerable]) { throw '按住说话按键列表不正确。' }
        $holdKeys = @($choice.holdKeys)
    } else { $holdKeys = @($choice.holdKey) }
    if ($holdKeys.Count -gt 75 -or ($choice.holdEnabled -and $holdKeys.Count -eq 0)) { throw '请至少添加一个按住说话按键。' }
    $seen=@{}
    foreach($key in $holdKeys) {
        if ($key -isnot [string] -or $key -cnotmatch $keyPattern) { throw '请选择支持的单键或鼠标侧键。' }
        if ($seen.ContainsKey($key)) { throw '按住说话按键不能重复。' }
        $seen[$key]=$true
    }
    if ($choice.toggleKey -isnot [string] -or $choice.toggleKey -cnotmatch $keyPattern) { throw '请选择支持的点按说话按键。' }
    if (-not $choice.holdEnabled -and -not $choice.toggleEnabled) { throw '至少启用一种说话方式。' }
    if ($choice.holdEnabled -and $choice.toggleEnabled -and $choice.toggleKey -cin $holdKeys) { throw '两种说话方式不能使用同一个键。' }
    if (($choice.PSObject.Properties.Name -contains 'autoStart') -and $choice.autoStart -isnot [bool]) { throw '开机自动启动选项格式不正确。' }
    Assert-Idle
    $oldMic = [System.IO.File]::ReadAllText($micSaved) | ConvertFrom-Json
    $expectedDevice = $oldMic.name
    $micChanged = $false
    if ($choice.PSObject.Properties.Name -contains 'microphone') {
        $newMic = $choice.microphone
        $expectedDevice = $newMic.name
        if ($null -eq $newMic -or -not ($newMic.PSObject.Properties.Name -contains 'name') -or -not ($newMic.PSObject.Properties.Name -contains 'hostapi')) { throw '请选择有效的麦克风。' }
        if (-not (($null -eq $newMic.name -and $null -eq $newMic.hostapi) -or ($newMic.name -is [string] -and $newMic.hostapi -is [string] -and $newMic.name.Trim().Length -gt 0 -and $newMic.hostapi.Trim().Length -gt 0))) { throw '请选择有效的麦克风。' }
        $micChanged = $oldMic.name -cne $newMic.name -or $oldMic.hostapi -cne $newMic.hostapi
        if ($micChanged) {
            Remove-Item -LiteralPath $micMarker -ErrorAction SilentlyContinue
            [System.IO.File]::WriteAllText($micPending,(@{name=$newMic.name;hostapi=$newMic.hostapi} | ConvertTo-Json -Compress),(New-Object System.Text.UTF8Encoding($false)))
            try {
                & (Join-Path $PSScriptRoot 'run-verification.ps1') -Script (Join-Path $PSScriptRoot 'validate_microphone.py') | Out-Null
                if (-not (Test-Path -LiteralPath $micMarker)) { throw 'unavailable' }
            } catch { throw '所选麦克风无法打开，请刷新设备或选择同一设备的其他接口。原设置未改变。' }
        }
    }
    Assert-Idle
    if ($choice.PSObject.Properties.Name -contains 'autoStart') {
        $requestedStartup = $null
        if ($choice.autoStart) { $requestedStartup = Get-VoiceStartupCommand }
        if ($oldStartup -cne $requestedStartup) { Set-VoiceStartupRegistration $requestedStartup; $startupChanged=$true }
    }
    if ($micChanged) { Save-Atomic $micSaved @{name=$newMic.name;hostapi=$newMic.hostapi} 'microphone.previous.json'; $micCommitted=$true }
    $voice = [ordered]@{version=2;holdEnabled=$choice.holdEnabled;holdKeys=@($holdKeys);toggleEnabled=$choice.toggleEnabled;toggleKey=$choice.toggleKey}
    Save-Atomic (Join-Path $root 'voice-settings.json') $voice 'voice-settings.previous.json'
    $voiceCommitted = $true
    $exe = Join-Path $appDir 'start_client.exe'
    $clients = Get-CimInstance Win32_Process -Filter "name='start_client.exe'" | Where-Object {$_.ExecutablePath -eq $exe}
    if ((Test-Path -LiteralPath $stopFile) -or -not $clients) { Write-Output '已保存，下次启动语音输入时生效。'; exit 0 }
    $oldPids = @($clients.ProcessId)
    & (Join-Path $PSScriptRoot 'restart-client.ps1')
    for ($i=0; $i -lt 60; $i++) {
        Start-Sleep -Milliseconds 200
        if (Test-Path -LiteralPath $stopFile) { Write-Output '已保存，语音输入已退出，下次启动时生效。'; exit 0 }
        try {
            $current = Get-Content -LiteralPath $statusFile -Raw -Encoding UTF8 | ConvertFrom-Json
            $applied=$current.voiceSettings
            $keysMatch=$applied -and (($applied.holdKeys -join '|') -ceq ($holdKeys -join '|')) -and $applied.holdEnabled -eq $choice.holdEnabled -and $applied.toggleEnabled -eq $choice.toggleEnabled -and $applied.toggleKey -ceq $choice.toggleKey
            if ($current.pid -notin $oldPids -and $current.connected -and (-not $expectedDevice -or $current.device -eq $expectedDevice) -and $current.audioReady -and $current.liveInputReady -and $keysMatch -and $current.segmentSeconds -eq 60 -and $current.overlapSeconds -eq 4) {
                Write-Output '全部设置已保存并生效。'
                exit 0
            }
        } catch {}
    }
    if ($micChanged) {
        Save-Atomic $micSaved $oldMic 'microphone.failed.json'
        & (Join-Path $PSScriptRoot 'restart-client.ps1')
        throw '所选麦克风未能正常启动，已恢复原麦克风。请测试收音后再选其他接口。'
    }
    throw '设置已保存，但收音或识别尚未就绪。请检查设备，或重新启动语音输入。'
} catch {
    if ($startupChanged -and -not $voiceCommitted) { try { Set-VoiceStartupRegistration $oldStartup } catch {} }
    if ($micCommitted -and -not $voiceCommitted) {
        try { Save-Atomic $micSaved $oldMic 'microphone.failed.json' } catch {}
    }
    Write-Output $_.Exception.Message
    exit 1
} finally {
    Remove-Item -LiteralPath $micPending,$micMarker -ErrorAction SilentlyContinue
    $settingsMutex.ReleaseMutex()
    $settingsMutex.Dispose()
}
