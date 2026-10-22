"""Local file commands for the single tray; no network listener."""
import json
import os
import threading
import time
from pathlib import Path


class CommandBridge:
    def __init__(self, root, actions, pid=None):
        self.root = Path(root)
        self.directory = self.root / '临时文件' / '托盘命令'
        self.directory.mkdir(parents=True, exist_ok=True)
        self.actions = dict(actions)
        self.pid = os.getpid() if pid is None else pid
        self.stopping = threading.Event()
        self.active = set()
        self.lock = threading.Lock()

    def start(self):
        self._write(self.root / '临时文件' / '托盘桥接健康.json',
                    dict(pid=self.pid, actions=sorted(self.actions), updatedAt=time.time()))
        self.thread = threading.Thread(target=self._poll, name='UnifiedTrayCommands', daemon=True)
        self.thread.start()

    def stop(self):
        self.stopping.set()

    @staticmethod
    def _write(path, value):
        temporary = path.with_name(path.name + '.' + str(threading.get_ident()) + '.tmp')
        temporary.write_text(json.dumps(value, ensure_ascii=False), encoding='utf-8')
        os.replace(temporary, path)

    def _reply(self, request, ok, message):
        self._write(self.root / '临时文件' / '托盘操作结果.json',
                    dict(id=request.get('id'), action=request.get('action'), ok=ok,
                         message=message, pid=self.pid, updatedAt=time.time()))

    def process(self, request):
        if (request.get('pid') != self.pid or
                not -2 <= time.time() - request.get('createdAt', 0) <= 15):
            return False
        action = request.get('action')
        if action not in self.actions:
            self._reply(request, False, '不支持此操作。')
            return False
        with self.lock:
            if action in self.active:
                self._reply(request, False, '该窗口或操作已在进行。')
                return False
            self.active.add(action)
        def execute():
            try:
                result = self.actions[action]()
                self._reply(request, True, result or '操作已完成。')
            except Exception as error:
                self._reply(request, False, '操作未完成：' + str(error))
            finally:
                with self.lock:
                    self.active.discard(action)
        threading.Thread(target=execute, daemon=True, name='Tray-' + action).start()
        return True

    def _poll(self):
        while not self.stopping.wait(.15):
            for path in self.directory.glob('*.json'):
                try:
                    request = json.loads(path.read_text(encoding='utf-8-sig'))
                    path.unlink(missing_ok=True)
                    self.process(request)
                except Exception:
                    try:
                        path.unlink(missing_ok=True)
                    except OSError:
                        pass
