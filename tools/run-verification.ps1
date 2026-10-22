param([Parameter(Mandatory=$true)][string]$Script)
$ErrorActionPreference='Stop'
[Console]::OutputEncoding=New-Object Text.UTF8Encoding($false)
$root=Split-Path -Parent $PSScriptRoot
$appDir=Join-Path $root 'CapsWriter\CapsWriter-Offline'
$config=Join-Path $appDir 'config_client.py'
$verifyMutex=New-Object Threading.Mutex($false,'Local\CapsWriterLocalVoiceVerify')
if(-not $verifyMutex.WaitOne(10000)){ $verifyMutex.Dispose(); throw '设备检测正在运行，请稍后重试。' }
$original=[IO.File]::ReadAllText($config)
$hook="import sys`nfor _s in (sys.stdout, sys.stderr):`n    if hasattr(_s, 'reconfigure'): _s.reconfigure(encoding='utf-8', errors='replace')`nif '--deployment-verify' in sys.argv:`n    import runpy`n    runpy.run_path(sys.argv[2], run_name='__main__')`n    raise SystemExit(0)`n"
$tempDir=Join-Path $root '临时文件'
$env:TEMP=$tempDir;$env:TMP=$tempDir;$env:PYTHONIOENCODING='utf-8'
$probe=$null
try {
    [IO.File]::WriteAllText($config,$hook+$original,(New-Object Text.UTF8Encoding($false)))
    $info=New-Object Diagnostics.ProcessStartInfo
    $info.FileName=Join-Path $appDir 'start_client.exe'
    $info.Arguments='--deployment-verify "'+$Script+'"'
    $info.WorkingDirectory=$appDir
    $info.UseShellExecute=$false;$info.CreateNoWindow=$true;$info.WindowStyle='Hidden'
    $info.RedirectStandardOutput=$true;$info.RedirectStandardError=$true
    $info.StandardOutputEncoding=New-Object Text.UTF8Encoding($false)
    $info.StandardErrorEncoding=New-Object Text.UTF8Encoding($false)
    $probe=New-Object Diagnostics.Process
    $probe.StartInfo=$info
    [void]$probe.Start()
    $stdout=$probe.StandardOutput.ReadToEndAsync();$stderr=$probe.StandardError.ReadToEndAsync()
    if(-not $probe.WaitForExit(18000)) { $probe.Kill(); throw '设备检查超时，请重新选择录音设备后再试。' }
    $exitCode=$probe.ExitCode
    [void][Threading.Tasks.Task]::WaitAll([Threading.Tasks.Task[]]@($stdout,$stderr),1500)
    $outText='';$errText=''
    if($stdout.IsCompleted){$outText=$stdout.Result};if($stderr.IsCompleted){$errText=$stderr.Result}
    [IO.File]::WriteAllText((Join-Path $root '验证\验证.stdout.log'),$outText,(New-Object Text.UTF8Encoding($false)))
    [IO.File]::WriteAllText((Join-Path $root '验证\验证.stderr.log'),$errText,(New-Object Text.UTF8Encoding($false)))
    Write-Output "Verification process exit code: $exitCode"
    if($outText){Write-Output $outText.Trim()};if($errText){Write-Output $errText.Trim()}
    if($exitCode -ne 0){throw "检测程序未成功完成，退出代码 $exitCode。"}
} finally {
    [IO.File]::WriteAllText($config,$original,(New-Object Text.UTF8Encoding($false)))
    if($probe){$probe.Dispose()}
    $verifyMutex.ReleaseMutex();$verifyMutex.Dispose()
}
