"""Bounded, read-only Windows input-target checks. No recognition text is logged."""
import asyncio
import ctypes
import json
from pathlib import Path
import subprocess

class InputTarget:
    def __init__(self):
        self.helper = Path(__file__).resolve().parents[5] / 'tools' / '输入目标检测.exe'

    def _snapshot(self, expected):
        try:
            result = subprocess.run([str(self.helper)], input=json.dumps({'expected': expected}),
                capture_output=True, text=True, encoding='utf-8', timeout=1.5,
                creationflags=getattr(subprocess, 'CREATE_NO_WINDOW', 0))
            return json.loads(result.stdout.lstrip('\ufeff')) if result.returncode == 0 else {}
        except (OSError, ValueError, subprocess.TimeoutExpired):
            return {}

    async def snapshot(self, expected=''):
        return await asyncio.to_thread(self._snapshot, expected)

    @staticmethod
    def confirmed(before, after):
        return bool(before.get('readable') and after.get('readable')
            and before.get('foreground') == after.get('foreground')
            and before.get('identity') and before['identity'] == after.get('identity')
            and before.get('digest') != after.get('digest') and after.get('expectedPresent'))

    @staticmethod
    def clipboard_is_plain_text():
        """Avoid replacing images, files or formatted clipboard data just to type."""
        try:
            user32 = ctypes.windll.user32
            if not user32.OpenClipboard(None):
                return False
            try:
                formats = set()
                current = 0
                while True:
                    current = user32.EnumClipboardFormats(current)
                    if not current:
                        break
                    formats.add(current)
                return 13 in formats and formats <= {1, 7, 13, 16}
            finally:
                user32.CloseClipboard()
        except Exception:
            return False
