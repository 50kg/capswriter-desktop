"""Local dictation preview: revise only our own suffix while the user is idle."""
import ctypes
import threading
from pynput import keyboard, mouse


def revision(old, new):
    """Return the minimal trailing replacement, leaving the common prefix intact."""
    shared = 0
    for a, b in zip(old, new):
        if a != b:
            break
        shared += 1
    return len(old) - shared, new[shared:]


class LiveInput:
    def __init__(self):
        self.lock = threading.RLock()
        self.started_at = None
        self.text = ''
        self.blocked = False
        self.finished = False
        self.target = 0
        self.available = False
        self.ignored_vks = {0x14, 0x78}
        self.ignored_mouse = set()
        try:
            self.user32 = ctypes.windll.user32
            self.user32.GetForegroundWindow.restype = ctypes.c_void_p
            self.keys = keyboard.Listener(win32_event_filter=self._key_event)
            self.mouse = mouse.Listener(win32_event_filter=self._mouse_event)
            self.keys.start()
            self.mouse.start()
            self.keys.wait()
            self.mouse.wait()
            self.available = True
        except Exception:
            # Without edit protection, retain the original final-only output.
            self.available = False

    def _key_event(self, message, event):
        # LLKHF_INJECTED: ignore our own paste/backspace and shortcut restoration.
        if message in (0x100, 0x104) and not event.flags & 0x10:
            if event.vkCode not in self.ignored_vks:
                self.interrupt()
        return True

    def _mouse_event(self, message, event):
        if message in (0x201, 0x204, 0x207, 0x20B, 0x20A, 0x20E):
            if message == 0x20B and (event.mouseData >> 16) & 0xFFFF in self.ignored_mouse:
                return True
            if not event.flags & 1:  # LLMHF_INJECTED
                self.interrupt()
        return True

    def configure_shortcuts(self, shortcuts):
        self.ignored_vks = set()
        self.ignored_mouse = set()
        for shortcut in shortcuts:
            if not shortcut.get('enabled', True):
                continue
            name = shortcut['key']
            if name in ('x1', 'x2'):
                self.ignored_mouse.add(1 if name == 'x1' else 2)
            elif len(name) == 1:
                self.ignored_vks.add(ord(name.upper()))
            else:
                key = getattr(keyboard.Key, name, None)
                if key is not None:
                    self.ignored_vks.add(key.value.vk)

    def interrupt(self):
        with self.lock:
            if self.started_at is not None:
                self.blocked = True

    def begin(self, started_at):
        with self.lock:
            self.started_at = started_at
            self.target = self.user32.GetForegroundWindow() if self.available else 0
            self.text = ''
            self.blocked = False
            self.finished = False

    def cancel(self):
        with self.lock:
            self.blocked = True
            self.text = ''

    def matches(self, started_at):
        return self.started_at is not None and abs(self.started_at - started_at) < .05

    def permitted(self, started_at):
        if not self.available or not self.matches(started_at) or self.blocked or self.finished:
            return False
        if self.target != self.user32.GetForegroundWindow():
            self.blocked = True
            return False
        return True

    async def update(self, text, started_at, emit, erase, final=False, paste=None, replace=None):
        if not self.permitted(started_at):
            return False
        old = self.text
        delete, suffix = revision(old, text)
        # A Chinese phrase is plain text; avoid ambiguous grapheme deletion.
        if delete and any(ord(c) > 0xFFFF or c in '\r\n' for c in old[-delete:]):
            self.interrupt()
            return False
        if replace is not None and (delete or suffix):
            if not await replace(delete, suffix, paste=paste):
                self.interrupt()
                return False
        else:
            if delete:
                erase(delete)
            if suffix:
                await emit(suffix, paste=paste)
        with self.lock:
            # A physical action during paste must also disable later revisions.
            if self.matches(started_at):
                self.text = text
                if final:
                    self.finished = True
        return True

    def close(self):
        for name in ('keys', 'mouse'):
            listener = getattr(self, name, None)
            if listener:
                listener.stop()
