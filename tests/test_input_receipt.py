import importlib.util
from pathlib import Path
import unittest
ROOT=Path(__file__).resolve().parents[1]
spec=importlib.util.spec_from_file_location('target_under_test',ROOT/'overlay/core/client/output/input_target.py')
module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module)

class ReceiptTests(unittest.TestCase):
    def setUp(self):
        self.before=dict(readable=True,foreground=100,identity='editor-1',digest='before')
        self.after=dict(readable=True,foreground=100,identity='editor-1',digest='after',expectedPresent=True)
    def test_real_change_in_same_editor_is_confirmed(self):
        self.assertTrue(module.InputTarget.confirmed(self.before,self.after))
    def test_focus_changes_and_unreadable_targets_are_not_confirmed(self):
        for patch in [dict(foreground=101),dict(identity='editor-2'),dict(readable=False),
                      dict(digest='before'),dict(expectedPresent=False),dict(identity='')]:
            with self.subTest(patch=patch):
                self.assertFalse(module.InputTarget.confirmed(self.before,dict(self.after,**patch)))

if __name__=='__main__':unittest.main()
