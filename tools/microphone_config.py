"""Per-application input selection; never changes Windows audio defaults."""
import json
from pathlib import Path
import sounddevice as sd

ROOT = Path(__file__).resolve().parent.parent

def apply():
    path = ROOT / 'microphone.json'
    if not path.exists():
        return
    choice = json.loads(path.read_text(encoding='utf-8-sig'))
    if not choice.get('name'):
        return
    hosts = sd.query_hostapis()
    for i, device in enumerate(sd.query_devices()):
        if (device['max_input_channels'] > 0 and device['name'] == choice['name']
                and hosts[device['hostapi']]['name'] == choice['hostapi']):
            sd.check_input_settings(device=i, channels=min(2, device['max_input_channels']), samplerate=48000)
            sd.default.device = (i, sd.default.device[1])
            print('已选择麦克风：' + choice['name'], flush=True)
            return
    print('所选麦克风未连接，已使用系统默认录音设备。', flush=True)
