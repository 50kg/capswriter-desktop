$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
$failed=$false
foreach($directory in @('scripts','tools')){foreach($file in Get-ChildItem -LiteralPath (Join-Path $root $directory) -Filter '*.ps1' -File){
    $tokens=$null;$issues=$null
    [Management.Automation.Language.Parser]::ParseFile($file.FullName,[ref]$tokens,[ref]$issues) | Out-Null
    if($issues.Count){$failed=$true;$issues|ForEach-Object {Write-Host ($file.Name+': '+$_.Message)}}
}}
if($failed){throw 'PowerShell 脚本存在语法错误。'}
Write-Output 'PowerShell 脚本语法检查通过。'
