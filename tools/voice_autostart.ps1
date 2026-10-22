function Get-VoiceStartupCommand {
    $launcher = Join-Path $root '启动语音输入.vbs'
    $wscript = Join-Path ([Environment]::GetFolderPath([Environment+SpecialFolder]::System)) 'wscript.exe'
    return '"' + $wscript + '" "' + $launcher + '"'
}
function Get-VoiceStartupRegistration {
    $key = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey('Software\Microsoft\Windows\CurrentVersion\Run')
    try { if ($key) { return $key.GetValue('CapsWriterLocalVoice',$null) } } finally { if ($key) { $key.Dispose() } }
}
function Set-VoiceStartupRegistration([object]$value) {
    $key = [Microsoft.Win32.Registry]::CurrentUser.CreateSubKey('Software\Microsoft\Windows\CurrentVersion\Run')
    try {
        if ($null -eq $value) { $key.DeleteValue('CapsWriterLocalVoice',$false) }
        else { $key.SetValue('CapsWriterLocalVoice',[string]$value,[Microsoft.Win32.RegistryValueKind]::String) }
    } finally { $key.Dispose() }
}
