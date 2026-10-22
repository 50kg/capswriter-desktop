import ast
import contextlib
import importlib.util
import io
import json
from pathlib import Path
import types
import unittest

ROOT = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location('preferences_under_test', ROOT / 'tools/voice_settings.py')
settings = importlib.util.module_from_spec(spec)
spec.loader.exec_module(settings)

class PreferencesTests(unittest.TestCase):
    def test_legacy_settings_keep_key_but_remove_speed(self):
        actual = settings.validate(dict(version=1, holdEnabled=True, holdKey='alt_l',
            toggleEnabled=True, toggleKey='f9', speed='fast'))
        self.assertEqual(actual['holdKeys'], ['alt_l'])
        self.assertEqual(actual['version'], 2)
        self.assertNotIn('speed', actual)

    def test_multiple_holds_do_not_modify_segmentation(self):
        values = settings.validate(dict(settings.DEFAULTS, holdKeys=['alt_l', 'ctrl_l', 'x2']))
        config = types.SimpleNamespace(mic_seg_duration=60, mic_seg_overlap=4)
        original = settings.load
        try:
            settings.load = lambda: values
            with contextlib.redirect_stdout(io.StringIO()):
                settings.apply(config)
        finally:
            settings.load = original
        self.assertEqual([s['key'] for s in config.shortcuts], ['alt_l', 'ctrl_l', 'x2', 'f9'])
        self.assertEqual([s['hold_mode'] for s in config.shortcuts], [True, True, True, False])
        self.assertEqual([s['suppress'] for s in config.shortcuts], [False, False, True, True])
        self.assertEqual((config.mic_seg_duration, config.mic_seg_overlap), (60, 4))

    def test_invalid_and_conflicting_keys(self):
        for patch in [dict(holdKeys=[]), dict(holdKeys=['ctrl_l', 'ctrl_l']),
                dict(holdKeys='ctrl_l'), dict(holdKeys=[[]]), dict(holdKeys=['ctrl+c']),
                dict(holdKeys=['f9']), dict(holdEnabled=False, toggleEnabled=False),
                dict(holdEnabled='true'), dict(version=3)]:
            with self.subTest(patch=patch), self.assertRaises(ValueError):
                settings.validate(dict(settings.DEFAULTS, **patch))

    def test_disabled_hold_can_have_empty_list(self):
        actual = settings.validate(dict(settings.DEFAULTS, holdEnabled=False, holdKeys=[]))
        self.assertEqual(actual['holdKeys'], [])

    def test_public_defaults_are_generic(self):
        self.assertEqual(settings.validate(json.loads((ROOT/'defaults/voice-settings.json').read_text())), settings.DEFAULTS)
        self.assertEqual(json.loads((ROOT/'defaults/microphone.json').read_text()), {'name': None, 'hostapi': None})

    def test_recognition_defaults_are_chinese_and_offline(self):
        tree = ast.parse((ROOT/'overlay/config_client.py').read_text(encoding='utf-8-sig'))
        cls = next(n for n in tree.body if isinstance(n, ast.ClassDef) and n.name == 'ClientConfig')
        wanted = {'mic_seg_duration', 'mic_seg_overlap', 'language', 'llm_enabled', 'save_audio', 'enter_apps', 'enable_tray'}
        values = {n.targets[0].id: ast.literal_eval(n.value) for n in cls.body
                  if isinstance(n, ast.Assign) and isinstance(n.targets[0], ast.Name) and n.targets[0].id in wanted}
        self.assertEqual((values['mic_seg_duration'], values['mic_seg_overlap']), (60, 4))
        self.assertEqual(values['language'], 'chinese')
        self.assertFalse(values['llm_enabled'])
        self.assertFalse(values['enable_tray'])
        self.assertFalse(values['save_audio'])
        self.assertEqual(values['enter_apps'], [])

if __name__ == '__main__':
    unittest.main()
