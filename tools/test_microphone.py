from pathlib import Path
import json
import math
import os
import time
import numpy as np
import sounddevice as sd

root=Path.cwd().parent.parent
choice=json.loads((root/'临时文件/microphone.test.pending.json').read_text(encoding='utf-8-sig'))
index=None
if choice.get('name'):
    hosts=sd.query_hostapis()
    index=next(i for i,d in enumerate(sd.query_devices()) if d['max_input_channels'] and d['name']==choice['name'] and hosts[d['hostapi']]['name']==choice['hostapi'])
device=sd.query_devices(index,'input');channels=min(2,int(device['max_input_channels']))
latest={'rms':0.0,'peak':0.0,'blocks':0};all_peak=0.0;max_rms=0.0
def callback(data,frames,timing,status):
    latest.update(rms=float(np.sqrt(np.mean(data*data))),peak=float(np.max(np.abs(data))),blocks=latest['blocks']+1)
path=root/'临时文件/microphone.test.live.json'
with sd.InputStream(device=index,samplerate=48000,channels=channels,dtype='float32',callback=callback):
    begin=time.monotonic()
    while time.monotonic()-begin<4:
        snapshot=dict(latest);all_peak=max(all_peak,snapshot['peak']);max_rms=max(max_rms,snapshot['rms'])
        snapshot.update(id=choice['id'],seconds=time.monotonic()-begin,device=device['name'],level=max(0,min(100,int((20*math.log10(max(snapshot['rms'],1e-9))+90)/.7))))
        temporary=path.with_suffix('.tmp');temporary.write_text(json.dumps(snapshot,ensure_ascii=False),encoding='utf-8');os.replace(temporary,path)
        time.sleep(.1)
result=dict(id=choice['id'],device=device['name'],blocks=latest['blocks'],maxRms=max_rms,peak=all_peak,
            message='已收到声音信号，请说话确认强度随声音变化。' if max_rms>.0001 else '音频已连接，但声音很弱或没有声音；请确认麦克风未静音，并靠近说话。')
assert latest['blocks']>0,'No audio callbacks arrived'
(root/'临时文件/microphone.test.result.json').write_text(json.dumps(result,ensure_ascii=False),encoding='utf-8')
print(result['message'])
