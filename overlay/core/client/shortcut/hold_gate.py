"""Start dictation only after a solitary physical hold; never synthesize input."""
import threading


class HoldGate:
    def __init__(self, delay=.3, timer_factory=threading.Timer, can_start=lambda: True,
                 on_activate=lambda name: None, on_cancel=lambda name: None, on_release=lambda name: None):
        self.on_activate = on_activate
        self.on_cancel = on_cancel
        self.on_release = on_release
        self.can_start = can_start
        self.delay = delay
        self.timer_factory = timer_factory
        self.lock = threading.RLock()
        self.down = set()
        self.pending = {}

    def _invalidate(self, entry):
        entry['combined'] = True
        entry['timer'].cancel()
        if entry['started'] and entry['task'].is_recording:
            entry['task'].cancel()
        self.on_cancel(entry['name'])

    def key_down(self, name, task=None):
        with self.lock:
            if name in self.down:
                return
            self.down.add(name)
            for other, entry in self.pending.items():
                if other != name:
                    self._invalidate(entry)
            if task is None:
                return
            entry = dict(name=name, task=task, started=False, combined=len(self.down)!=1)
            def activate():
                with self.lock:
                    if (self.pending.get(name) is entry and not entry['combined']
                            and self.down == {name} and self.can_start()):
                        self.on_activate(name)
                        try:
                            task.launch()
                        except Exception:
                            self.on_cancel(name)
                            raise
                        entry['started'] = True
            timer = self.timer_factory(self.delay, activate)
            timer.daemon = True
            entry['timer'] = timer
            self.pending[name] = entry
            if not entry['combined']:
                timer.start()

    def key_up(self, name):
        with self.lock:
            self.down.discard(name)
            entry = self.pending.pop(name, None)
            if entry:
                entry['timer'].cancel()
                if entry['started'] and entry['task'].is_recording:
                    entry['task'].finish()
                self.on_release(name)
            return entry

    def interrupt(self):
        with self.lock:
            for entry in self.pending.values():
                self._invalidate(entry)

    def close(self):
        with self.lock:
            self.interrupt()
            self.pending.clear()
            self.down.clear()
