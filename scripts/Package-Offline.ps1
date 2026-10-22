param([Parameter(Mandatory=$true)][string]$ArchiveDirectory,[string]$OutputDirectory,[string]$Python='python')
$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
if(-not $OutputDirectory){$OutputDirectory=Join-Path $root 'dist'}
if(git -C $root status --porcelain --untracked-files=no){throw '请先提交本次源码，保证发布包与提交内容一致。'}
& (Join-Path $PSScriptRoot 'Package.ps1') -OutputDirectory $OutputDirectory
$version=(Get-Content -LiteralPath (Join-Path $root 'manifest.json') -Raw | ConvertFrom-Json).version
$light=Join-Path $OutputDirectory ('capswriter-desktop-'+$version+'-windows-x64.zip')
$complete=Join-Path $OutputDirectory ('capswriter-desktop-'+$version+'-windows-x64-offline-full.zip')
& $Python -X utf8 (Join-Path $PSScriptRoot 'build_offline_bundle.py') --source-package $light --archives $ArchiveDirectory --output $complete
if($LASTEXITCODE -ne 0){throw '完整离线包构建失败。'}
Write-Output $complete
