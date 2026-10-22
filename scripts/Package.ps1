param([string]$OutputDirectory)
$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
if(-not $OutputDirectory){$OutputDirectory=Join-Path $root 'dist'}
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$version=(Get-Content -LiteralPath (Join-Path $root 'manifest.json') -Raw | ConvertFrom-Json).version
& (Join-Path $PSScriptRoot 'Build.ps1')
$archive=Join-Path ([IO.Path]::GetFullPath($OutputDirectory)) ('capswriter-desktop-'+$version+'-windows-x64.zip')
git -C $root archive --format=zip --prefix=capswriter-desktop/ ('--output='+$archive) HEAD
if($LASTEXITCODE -ne 0){throw '请先提交要打包的源码。'}
Add-Type -AssemblyName System.IO.Compression,System.IO.Compression.FileSystem
$zip=[IO.Compression.ZipFile]::Open($archive,[IO.Compression.ZipArchiveMode]::Update)
try{foreach($name in @('语音状态条.exe','语音输入设置.exe','输入目标检测.exe')){[IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip,(Join-Path $root ('tools\'+$name)),('capswriter-desktop/tools/'+$name))|Out-Null}}finally{$zip.Dispose()}
$digest=(Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText(($archive+'.sha256'),($digest+'  '+[IO.Path]::GetFileName($archive)+"`n"),(New-Object Text.UTF8Encoding($false)))
Write-Output $archive
