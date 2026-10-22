import importlib.util
import json
from pathlib import Path
import tempfile
import unittest
import zipfile

ROOT=Path(__file__).resolve().parents[1]
spec=importlib.util.spec_from_file_location('offline_bundle_under_test',ROOT/'scripts/build_offline_bundle.py')
bundle=importlib.util.module_from_spec(spec);spec.loader.exec_module(bundle)

class BundleTests(unittest.TestCase):
    def setUp(self):
        base=ROOT/'.work';base.mkdir(exist_ok=True)
        self.temporary=tempfile.TemporaryDirectory(dir=base)
        self.directory=Path(self.temporary.name).resolve()
        self.assertTrue(self.directory.is_relative_to(base.resolve()))
    def tearDown(self):
        self.assertTrue(self.directory.is_relative_to((ROOT/'.work').resolve()))
        self.temporary.cleanup()
    def zip(self,name,files):
        path=self.directory/name
        with zipfile.ZipFile(path,'w') as archive:
            for member,data in files.items():archive.writestr(member,data)
        return path
    def inputs(self):
        runtime=self.zip('runtime.zip',{
            'CapsWriter-Offline/start_client.exe':b'client',
            'CapsWriter-Offline/start_server.exe':b'server',
            'CapsWriter-Offline/internal/python313.dll':b'python',
            'CapsWriter-Offline/internal/vendor/LICENSE':b'vendor license',
            'CapsWriter-Offline/config_client.py':b'upstream-config',
            'CapsWriter-Offline/hot.txt':b'upstream example',
            'CapsWriter-Offline/2026/10/private.wav':b'never include',
            'CapsWriter-Offline/logs/debug.log':b'never include',
            'CapsWriter-Offline/models/AI-Polish.py':b'never include',
            'CapsWriter-Offline/LLM/default.py':b'never include',
            'CapsWriter-Offline/LLM/__init__.py':b'neutral template',
        })
        model=self.zip('model.zip',{'Fun-ASR-Nano-GGUF/'+name:name.encode() for name in bundle.MODEL_FILES})
        manifest=dict(version='0.2.0',upstream=dict(name=runtime.name,sha256=bundle.sha256(runtime)),
                      model=dict(name=model.name,sha256=bundle.sha256(model)))
        source={bundle.PREFIX+'manifest.json':json.dumps(manifest),
                bundle.PREFIX+'overlay/config_client.py':b'public-config',
                bundle.PREFIX+'defaults/voice-settings.json':b'{"holdKeys":["caps_lock"]}',
                bundle.PREFIX+'defaults/microphone.json':b'{"name":null,"hostapi":null}',
                bundle.PREFIX+'third_party/Fun-ASR-LICENSE.txt':b'Apache License 2.0',
                bundle.PREFIX+'third_party/SOURCES.md':b'Official sources',
                bundle.PREFIX+'启动语音输入.vbs':b'launcher'}
        for name in ('hot.txt','hot-server.txt','hot-rule.txt'):source[bundle.PREFIX+'defaults/'+name]=b'# empty defaults'
        for name in ('语音状态条.exe','语音输入设置.exe','输入目标检测.exe'):source[bundle.PREFIX+'tools/'+name]=b'desktop'
        light=self.zip('light.zip',source)
        return light,runtime,model
    def test_runtime_policy_excludes_records_and_online_profiles(self):
        for path in ['2026/10/example.wav','logs/debug.log','models/AI-Polish.py','LLM/default.py','core/__pycache__/x.pyc']:
            with self.subTest(path=path):self.assertFalse(bundle.allowed_runtime(path))
        self.assertTrue(bundle.allowed_runtime('internal/package/code.pyc'))
        self.assertTrue(bundle.allowed_runtime('internal/package/LICENSE'))
        self.assertTrue(bundle.allowed_runtime('LLM/__init__.py'))
    def test_traversal_and_wrong_archive_roots_are_rejected(self):
        for name in ['wrong/root','CapsWriter-Offline/../escape','CapsWriter-Offline/C:/escape','CapsWriter-Offline/\\escape']:
            with self.subTest(name=name),self.assertRaises(ValueError):bundle.checked_relative(name,'CapsWriter-Offline/')
    def test_clean_bundle_contains_runtime_model_defaults_and_licenses(self):
        source,_,_=self.inputs();output=self.directory/'full.zip'
        bundle.build(source,self.directory,output)
        with zipfile.ZipFile(output) as archive:
            names=archive.namelist()
            self.assertEqual(len(names),len(set(names)))
            self.assertEqual(archive.read(bundle.APP+'config_client.py'),b'public-config')
            self.assertEqual(archive.read(bundle.APP+'hot.txt'),b'# empty defaults')
            self.assertEqual(archive.read(bundle.PREFIX+'microphone.json'),b'{"name":null,"hostapi":null}')
            self.assertIn(bundle.MODEL+'LICENSE.txt',names)
            self.assertIn(bundle.APP+'internal/vendor/LICENSE',names)
            for name in bundle.MODEL_FILES:self.assertIn(bundle.MODEL+name,names)
            self.assertFalse(any('/2026/' in name or '/logs/' in name or name.endswith('/LLM/default.py') for name in names))
            self.assertTrue(json.loads(archive.read(bundle.PREFIX+'.capswriter-desktop.json'))['bundled'])
        self.assertTrue(output.with_name(output.name+'.sha256').read_text().startswith(bundle.sha256(output)))
    def test_changed_official_archive_is_rejected(self):
        source,runtime,_=self.inputs();runtime.write_bytes(b'tampered');output=self.directory/'full.zip'
        with self.assertRaisesRegex(ValueError,'checksum mismatch'):bundle.build(source,self.directory,output)
        self.assertFalse(output.exists())
    def test_existing_artifact_is_not_overwritten(self):
        source,_,_=self.inputs();output=self.directory/'full.zip';output.write_bytes(b'existing')
        with self.assertRaises(ValueError):bundle.build(source,self.directory,output)
        self.assertEqual(output.read_bytes(),b'existing')
    def test_apache_model_license_is_in_source(self):
        license=(ROOT/'third_party/Fun-ASR-LICENSE.txt').read_text(encoding='utf-8')
        self.assertIn('Apache License',license);self.assertIn('Version 2.0',license)

if __name__=='__main__':unittest.main()
