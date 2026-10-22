param([string]$OutputDirectory)
$ErrorActionPreference='Stop'
$sourceRoot=Split-Path -Parent $PSScriptRoot
if(-not $OutputDirectory){$OutputDirectory=$sourceRoot}
$targetTools=Join-Path ([IO.Path]::GetFullPath($OutputDirectory)) 'tools'
New-Item -ItemType Directory -Path $targetTools -Force | Out-Null
$csc=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if(-not(Test-Path -LiteralPath $csc)){throw '需要 Windows 10/11 x64 与 .NET Framework 4.x。'}
$assembly=Join-Path $env:WINDIR 'Microsoft.NET\assembly\GAC_MSIL'
foreach($pair in @(@('VoiceStatus.cs','语音状态条.exe'),@('VoiceSettings.cs','语音输入设置.exe'))){
    & $csc /nologo /codepage:65001 /target:winexe /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Web.Extensions.dll ("/out:"+(Join-Path $targetTools $pair[1])) (Join-Path $sourceRoot ('tools\'+$pair[0]))
    if($LASTEXITCODE -ne 0){throw ('编译失败：'+$pair[0])}
}
$references=@('UIAutomationClient','UIAutomationTypes','WindowsBase') | ForEach-Object {"/reference:$assembly\$_\v4.0_4.0.0.0__31bf3856ad364e35\$_.dll"}
& $csc /nologo /codepage:65001 /target:exe /reference:System.Web.Extensions.dll @references ("/out:"+(Join-Path $targetTools '输入目标检测.exe')) (Join-Path $sourceRoot 'tools\InputTargetProbe.cs')
if($LASTEXITCODE -ne 0){throw '输入目标检测组件编译失败。'}
Write-Output '三个桌面组件已构建。'
