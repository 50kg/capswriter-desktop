"""Build only from committed source and hash-verified official archives."""
import argparse
import hashlib
import json
from pathlib import Path, PurePosixPath
import shutil
import zipfile

PREFIX = 'capswriter-desktop/'
APP = PREFIX + 'CapsWriter/CapsWriter-Offline/'
MODEL = APP + 'models/Fun-ASR-Nano/Fun-ASR-Nano-GGUF/'
MODEL_FILES = {'Fun-ASR-Nano-CTC.fp16.onnx', 'Fun-ASR-Nano-Decoder.q5_k.gguf',
               'Fun-ASR-Nano-Encoder-Adaptor.fp16.onnx', 'tokens.txt'}

def sha256(path):
    digest = hashlib.sha256()
    with Path(path).open('rb') as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b''):
            digest.update(block)
    return digest.hexdigest()

def checked_relative(name, prefix):
    if not name.startswith(prefix):
        raise ValueError('Unexpected archive root: ' + name)
    relative = name[len(prefix):]
    parts = relative.split('/')
    if relative.startswith(('/', '\\')) or '\\' in relative or any(p in ('.', '..') or ':' in p for p in parts):
        raise ValueError('Unsafe archive path: ' + name)
    return relative

def allowed_runtime(relative):
    parts = PurePosixPath(relative).parts
    if not parts:
        return False
    if parts[0] in {'logs', 'models', '.git', '.cache', '__pycache__'}:
        return False
    if len(parts[0]) == 4 and parts[0].isdigit():
        return False
    # Only the neutral package/template is shipped. Online role profiles are absent.
    if parts[0] == 'LLM':
        return relative == 'LLM/__init__.py'
    if parts[0] != 'internal' and ('__pycache__' in parts or relative.endswith(('.log', '.wav', '.mp3', '.pyc'))):
        return False
    return True

def build(source_package, archive_directory, output):
    source_package, archive_directory, output = map(Path, (source_package, archive_directory, output))
    if output.exists():
        raise ValueError('Refusing to overwrite an existing release artifact: ' + str(output))
    with zipfile.ZipFile(source_package) as source:
        manifest = json.loads(source.read(PREFIX + 'manifest.json'))
        archives = {}
        for kind in ('upstream', 'model'):
            asset = manifest[kind]
            path = archive_directory / asset['name']
            print('Verifying ' + asset['name'], flush=True)
            if sha256(path) != asset['sha256']:
                raise ValueError('Official archive checksum mismatch: ' + asset['name'])
            archives[kind] = path
        with zipfile.ZipFile(archives['upstream']) as runtime, zipfile.ZipFile(archives['model']) as model:
            # destination -> (input zip, input member) or bytes
            plan = {}
            def add(name, provider):
                if name in plan:
                    raise ValueError('Duplicate output path: ' + name)
                plan[name] = provider
            for entry in source.infolist():
                checked_relative(entry.filename, PREFIX)
                if not entry.is_dir():
                    add(entry.filename, (source, entry))
            overlay = {name[len(PREFIX+'overlay/'):]: (source, source.getinfo(name))
                       for name in source.namelist() if name.startswith(PREFIX+'overlay/') and not name.endswith('/')}
            templates = {'hot.txt', 'hot-server.txt', 'hot-rule.txt'}
            for entry in runtime.infolist():
                relative = checked_relative(entry.filename, 'CapsWriter-Offline/')
                if entry.is_dir() or not allowed_runtime(relative) or relative in overlay or relative in templates:
                    continue
                add(APP + relative, (runtime, entry))
            for relative, provider in overlay.items():
                add(APP + relative, provider)
            for name in templates:
                add(APP + name, source.read(PREFIX+'defaults/'+name))
            for name in ('voice-settings.json', 'microphone.json'):
                add(PREFIX + name, source.read(PREFIX+'defaults/'+name))
            present = set()
            for entry in model.infolist():
                relative = checked_relative(entry.filename, 'Fun-ASR-Nano-GGUF/')
                if entry.is_dir():
                    continue
                if relative not in MODEL_FILES:
                    raise ValueError('Unexpected model asset: ' + relative)
                present.add(relative)
                add(MODEL + relative, (model, entry))
            if present != MODEL_FILES:
                raise ValueError('Model archive is incomplete')
            add(MODEL+'LICENSE.txt', source.read(PREFIX+'third_party/Fun-ASR-LICENSE.txt'))
            add(MODEL+'MODEL-SOURCES.md', source.read(PREFIX+'third_party/SOURCES.md'))
            marker = dict(version=manifest['version'], upstreamSha256=manifest['upstream']['sha256'],
                          modelSha256=manifest['model']['sha256'], status='installed', bundled=True)
            add(PREFIX+'.capswriter-desktop.json', json.dumps(marker, indent=2).encode('utf-8'))
            add(PREFIX+'解压即用说明.txt', ('完整离线包：无需运行安装脚本。\r\n'
                '双击“启动语音输入.vbs”；默认按住 Caps Lock 说话，松开结束，F9 点按开始/结束。\r\n'
                '托盘右键打开设置，选择麦克风并测试收音。\r\n'
                '已包含官方 Python 运行环境、Fun-ASR-Nano 模型和桌面组件；默认听写不需要联网。\r\n'
                '支持 Windows 10/11 x64；不要同时启动多个副本。\r\n').encode('utf-8-sig'))
            required = {APP+'start_client.exe', APP+'start_server.exe', APP+'internal/python313.dll',
                        PREFIX+'启动语音输入.vbs', PREFIX+'tools/语音状态条.exe', PREFIX+'tools/语音输入设置.exe', PREFIX+'tools/输入目标检测.exe'}
            if not required <= plan.keys():
                raise ValueError('Runtime archive is missing required files: ' + str(required-plan.keys()))
            print('Writing complete offline package: ' + str(len(plan)) + ' files', flush=True)
            output.parent.mkdir(parents=True, exist_ok=True)
            with zipfile.ZipFile(output, 'w', compression=zipfile.ZIP_DEFLATED, compresslevel=1, allowZip64=True) as target:
                for index, (name, provider) in enumerate(plan.items(), 1):
                    if isinstance(provider, bytes):
                        target.writestr(name, provider)
                    else:
                        origin, member = provider
                        with origin.open(member) as reader, target.open(name, 'w', force_zip64=True) as writer:
                            shutil.copyfileobj(reader, writer, 1024*1024)
                    if index % 750 == 0:
                        print('Packed ' + str(index) + '/' + str(len(plan)), flush=True)
                for folder in ('临时文件', '部署记录', '验证'):
                    target.writestr(PREFIX+folder+'/', b'')
    digest = sha256(output)
    output.with_name(output.name+'.sha256').write_text(digest+'  '+output.name+'\n', encoding='utf-8')
    print(json.dumps(dict(path=str(output), bytes=output.stat().st_size, sha256=digest)), flush=True)

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source-package', required=True)
    parser.add_argument('--archives', required=True)
    parser.add_argument('--output', required=True)
    arguments = parser.parse_args()
    build(arguments.source_package, arguments.archives, arguments.output)

if __name__ == '__main__':
    main()
