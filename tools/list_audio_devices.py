import json
from pathlib import Path
import sounddevice as sd
root = Path.cwd().parent.parent
hosts = sd.query_hostapis()
devices = []
for i, d in enumerate(sd.query_devices()):
    if d['max_input_channels'] > 0:
        devices.append({'index': i, 'name': d['name'], 'hostapi': hosts[d['hostapi']]['name']})
priority={'Windows WASAPI':0,'MME':1,'Windows DirectSound':2,'Windows WDM-KS':3}
devices=[d for d in devices if d['name'] not in ('Microsoft Sound Mapper - Input','Primary Sound Capture Driver')]
devices.sort(key=lambda d:(priority.get(d['hostapi'],4),'Realtek' not in d['name'],d['name']))
(root/'tools'/'audio-devices.json').write_text(json.dumps({'default': sd.query_devices(kind='input')['name'], 'devices': devices}, ensure_ascii=False), encoding='utf-8')
print('Audio input device list updated.')
