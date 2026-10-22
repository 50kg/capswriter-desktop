param([string]$InstallDirectory,[string]$ArchiveDirectory)
$ErrorActionPreference='Stop'
$ProgressPreference='SilentlyContinue'
$sourceRoot=Split-Path -Parent $PSScriptRoot
if(-not $InstallDirectory){$InstallDirectory=$sourceRoot}
$root=[IO.Path]::GetFullPath($InstallDirectory).TrimEnd('\')
$manifest=Get-Content -LiteralPath (Join-Path $sourceRoot 'manifest.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$app=Join-Path $root 'CapsWriter\CapsWriter-Offline'
$marker=Join-Path $root '.capswriter-desktop.json'
$managed=Test-Path -LiteralPath $marker
if([IntPtr]::Size -ne 8){throw '请使用 64 位 Windows PowerShell。'}
if((Test-Path -LiteralPath (Join-Path $app 'start_client.exe')) -and -not $managed){throw '目标包含未受本安装器管理的运行环境。请选择新的空目录，避免覆盖原有数据。'}
if($managed){
    $previous=Get-Content -LiteralPath $marker -Raw -Encoding UTF8 | ConvertFrom-Json
    if($previous.upstreamSha256 -ne $manifest.upstream.sha256){throw '运行环境版本不同，请安装到新目录。'}
}
foreach($folder in @($root,(Join-Path $root 'tools'),(Join-Path $root '临时文件'),(Join-Path $root '部署记录'),(Join-Path $root '验证'),(Join-Path $root '.cache'))){New-Item -ItemType Directory -Path $folder -Force | Out-Null}
$env:TEMP=Join-Path $root '临时文件';$env:TMP=$env:TEMP
[Net.ServicePointManager]::SecurityProtocol=[Net.SecurityProtocolType]::Tls12
function Get-VerifiedArchive($asset){
    $path=Join-Path $root ('.cache\'+$asset.name)
    if($ArchiveDirectory){$supplied=Join-Path ([IO.Path]::GetFullPath($ArchiveDirectory)) $asset.name;if(Test-Path -LiteralPath $supplied){$path=$supplied}}
    if(-not(Test-Path -LiteralPath $path)){
        Write-Host ('正在下载 '+$asset.name+'（首次安装需要联网）…')
        Invoke-WebRequest -Uri $asset.url -UseBasicParsing -OutFile ($path+'.part')
        Move-Item -LiteralPath ($path+'.part') -Destination $path -Force
    }
    Write-Host ('正在校验 '+$asset.name+'…')
    if((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant() -cne $asset.sha256){throw ('下载文件校验失败，未解压：'+$asset.name)}
    return $path
}
Add-Type -AssemblyName System.IO.Compression.FileSystem
if(-not(Test-Path -LiteralPath (Join-Path $app 'start_client.exe'))){
    $runtime=Get-VerifiedArchive $manifest.upstream
    New-Item -ItemType Directory -Path (Join-Path $root 'CapsWriter') -Force | Out-Null
    Write-Host '正在解压官方运行环境…'
    [IO.Compression.ZipFile]::ExtractToDirectory($runtime,(Join-Path $root 'CapsWriter'))
}
[IO.File]::WriteAllText($marker,(@{version=$manifest.version;upstreamSha256=$manifest.upstream.sha256;status='installing'}|ConvertTo-Json),(New-Object Text.UTF8Encoding($false)))
$modelParent=Join-Path $app 'models\Fun-ASR-Nano'
$modelPath=Join-Path $modelParent 'Fun-ASR-Nano-GGUF'
$modelFiles=@('Fun-ASR-Nano-CTC.fp16.onnx','Fun-ASR-Nano-Decoder.q5_k.gguf','Fun-ASR-Nano-Encoder-Adaptor.fp16.onnx','tokens.txt')
$modelMissing=@($modelFiles | Where-Object {-not(Test-Path -LiteralPath (Join-Path $modelPath $_))}).Count -gt 0
if($modelMissing){
    $model=Get-VerifiedArchive $manifest.model
    New-Item -ItemType Directory -Path $modelParent -Force | Out-Null
    Write-Host '正在解压中文识别模型…'
    Expand-Archive -LiteralPath $model -DestinationPath $modelParent -Force
}
if($root -ine [IO.Path]::GetFullPath($sourceRoot).TrimEnd('\')){
    foreach($folder in @('tools','scripts','overlay','defaults','docs','tests')){
        $source=Join-Path $sourceRoot $folder
        if(Test-Path -LiteralPath $source){
            foreach($file in Get-ChildItem -LiteralPath $source -Recurse -File){
                if($file.Extension -notin @('.py','.cs','.ps1','.md','.json','.txt','.png')){continue}
                if($folder -eq 'tools' -and $file.Extension -eq '.json'){continue}
                $relative=$file.FullName.Substring($sourceRoot.Length).TrimStart('\')
                $destination=Join-Path $root $relative
                New-Item -ItemType Directory -Path (Split-Path -Parent $destination) -Force | Out-Null
                Copy-Item -LiteralPath $file.FullName -Destination $destination -Force
            }
        }
    }
    foreach($name in @('README.md','LICENSE','THIRD_PARTY_NOTICES.md','CONTRIBUTING.md','SECURITY.md','CHANGELOG.md','manifest.json','启动语音输入.vbs','安装语音输入.cmd')){Copy-Item -LiteralPath (Join-Path $sourceRoot $name) -Destination (Join-Path $root $name) -Force}
}
foreach($file in Get-ChildItem -LiteralPath (Join-Path $sourceRoot 'overlay') -Recurse -File){
    $relative=$file.FullName.Substring((Join-Path $sourceRoot 'overlay').Length).TrimStart('\')
    if($managed -and $relative -in @('config_client.py','config_server.py')){continue}
    $destination=Join-Path $app $relative
    New-Item -ItemType Directory -Path (Split-Path -Parent $destination) -Force | Out-Null
    Copy-Item -LiteralPath $file.FullName -Destination $destination -Force
}
foreach($name in @('voice-settings.json','microphone.json')){if(-not(Test-Path -LiteralPath (Join-Path $root $name))){Copy-Item -LiteralPath (Join-Path $sourceRoot ('defaults\'+$name)) -Destination (Join-Path $root $name)}}
if(-not $managed){foreach($name in @('hot.txt','hot-server.txt','hot-rule.txt')){Copy-Item -LiteralPath (Join-Path $sourceRoot ('defaults\'+$name)) -Destination (Join-Path $app $name) -Force}}
& (Join-Path $sourceRoot 'scripts\Build.ps1') -OutputDirectory $root
try{& (Join-Path $root 'tools\run-verification.ps1') -Script (Join-Path $root 'tools\list_audio_devices.py')}catch{Write-Warning '暂时未能枚举麦克风。启动后可在设置中刷新设备。'}
[IO.File]::WriteAllText($marker,(@{version=$manifest.version;upstreamSha256=$manifest.upstream.sha256;modelSha256=$manifest.model.sha256;status='installed'}|ConvertTo-Json),(New-Object Text.UTF8Encoding($false)))
Write-Host ('安装完成：'+$root)
Write-Host '双击“启动语音输入.vbs”即可使用。请勿同时运行多个安装副本。'
