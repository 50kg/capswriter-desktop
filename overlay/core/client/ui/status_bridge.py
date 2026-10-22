"""Small local-only status feed. Audio callback never performs disk I/O."""
import json
import math
import os
from pathlib import Path
import threading
import time

_path = Path(__file__).resolve().parents[5] / '临时文件' / '语音状态.json'
_lock = threading.Lock()
_state = dict(pid=os.getpid(), phase='starting', connected=False, device='',
              recording=False, audioReady=False, startedAt=0.0, phaseAt=time.time(), level=0.0,
              lastAudioAt=0.0, preview='', error='')
_last_meter = 0.0

def _change(**values):
    with _lock:
        _state.update(values)

def connection(connected):
    with _lock:
        _state['connected'] = connected
        if connected and _state['phase'] in ('starting', 'offline'):
            _state.update(phase='ready', phaseAt=time.time(), error='')
        elif not connected and not _state['recording']:
            _state.update(phase='offline', phaseAt=time.time(), error='识别服务未连接，正在重连')

def audio_ready(ready):
    _change(audioReady=bool(ready))

def device(name):
    _change(device=name)

def live_input(enabled):
    _change(liveInputReady=enabled)

def preferences(hint, settings, segment_seconds, overlap_seconds):
    _change(shortcutHint=hint, voiceSettings=settings,
            segmentSeconds=segment_seconds, overlapSeconds=overlap_seconds)

def output_destination(destination):
    _change(outputDestination=destination, outputAt=time.time())

def begin(started_at):
    _change(phase='recording', recording=True, startedAt=started_at,
            phaseAt=time.time(), preview='', level=0.0, lastAudioAt=0.0, error='', outputDestination='')

def finish():
    _change(phase='recognizing', recording=False, phaseAt=time.time(), level=0.0)

def cancel():
    _change(phase='ready', recording=False, phaseAt=time.time(), level=0.0, preview='')

def error(message):
    _change(phase='error', error=message, recording=False, phaseAt=time.time(), level=0.0)

def preview(text, started_at, final=False):
    with _lock:
        # A delayed result from a previous recording must not replace the current one.
        if abs(_state['startedAt'] - started_at) > 0.05:
            return
        _state['preview'] = text
        if final:
            _state.update(phase='done', phaseAt=time.time(), recording=False, level=0.0)

def meter(rms):
    global _last_meter
    now = time.monotonic()
    if now - _last_meter < 0.1:
        return
    _last_meter = now
    # This Realtek microphone speaks around -60 dBFS; retain weak but real input.
    level = max(0.0, min(1.0, (20 * math.log10(max(float(rms), 1e-9)) + 90) / 70))
    if _lock.acquire(False):
        try:
            _state.update(level=level, lastAudioAt=time.time())
        finally:
            _lock.release()

def _writer():
    try:
        _path.parent.mkdir(exist_ok=True)
    except OSError:
        return
    temporary = _path.with_name('语音状态.' + str(os.getpid()) + '.tmp')
    while True:
        try:
            with _lock:
                snapshot = dict(_state, updatedAt=time.time())
            temporary.write_text(json.dumps(snapshot, ensure_ascii=False), encoding='utf-8')
            os.replace(temporary, _path)
        except Exception:
            # A display issue must never interrupt dictation.
            pass
        time.sleep(0.12 if snapshot.get('recording') else 0.3)

threading.Thread(target=_writer, name='LocalVoiceStatus', daemon=True).start()
