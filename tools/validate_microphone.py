from pathlib import Path
import json
import sounddevice as sd
root = Path.cwd().parent.parent
marker = root/'tools'/'microphone-valid.txt'
marker.unlink(missing_ok=True)
choice = json.loads((root/'microphone.pending.json').read_text(encoding='utf-8-sig'))
index = None
if choice.get('name'):
    hosts = sd.query_hostapis()
    index = next(i for i,d in enumerate(sd.query_devices()) if d['max_input_channels'] and d['name']==choice['name'] and hosts[d['hostapi']]['name']==choice['hostapi'])
device = sd.query_devices(index, 'input')
channels = min(2, int(device['max_input_channels']))
with sd.InputStream(device=index, samplerate=48000, channels=channels, dtype='float32') as stream:
    data, overflow = stream.read(2400)
    assert len(data) == 2400, 'No audio frames received'
marker.write_text('ok', encoding='ascii')
print('Microphone interface opened successfully.')
