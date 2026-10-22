"""Validated local preferences, applied only to this dictation client."""
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
DEFAULTS = dict(version=2, holdEnabled=True, holdKeys=['caps_lock'],
                toggleEnabled=True, toggleKey='f9')
KEYS = {'caps_lock', 'num_lock', 'scroll_lock', 'pause', 'print_screen',
        'insert', 'menu', 'ctrl_l', 'ctrl_r', 'shift_l', 'shift_r', 'alt_l', 'alt_gr', 'x1', 'x2'}
KEYS.update('f' + str(i) for i in range(1, 25))
KEYS.update('abcdefghijklmnopqrstuvwxyz0123456789')
NAMES = {'caps_lock': 'Caps Lock', 'num_lock': 'Num Lock',
         'scroll_lock': 'Scroll Lock', 'ctrl_l': '左 Ctrl', 'ctrl_r': '右 Ctrl',
         'shift_l': '左 Shift', 'shift_r': '右 Shift', 'alt_l': '左 Alt', 'alt_gr': '右 Alt',
         'x1': '鼠标后退键', 'x2': '鼠标前进键'}


def validate(data):
    if not isinstance(data, dict):
        raise ValueError('设置格式不正确')
    values = dict(DEFAULTS, **data)
    if values['version'] not in (1, 2):
        raise ValueError('不支持此设置版本')
    for field in ('holdEnabled', 'toggleEnabled'):
        if type(values[field]) is not bool:
            raise ValueError('按键启用状态不正确')
    hold_keys = data.get('holdKeys', [data.get('holdKey', 'caps_lock')])
    if not isinstance(hold_keys, list) or len(hold_keys) > len(KEYS):
        raise ValueError('按住说话按键列表不正确')
    if any(not isinstance(key, str) or key not in KEYS for key in hold_keys):
        raise ValueError('不支持的按键；请选择单键或鼠标侧键')
    if len(set(hold_keys)) != len(hold_keys):
        raise ValueError('按住说话按键不能重复')
    if values['holdEnabled'] and not hold_keys:
        raise ValueError('请至少添加一个按住说话按键')
    if not isinstance(values['toggleKey'], str) or values['toggleKey'] not in KEYS:
        raise ValueError('不支持的点按说话按键')
    if not values['holdEnabled'] and not values['toggleEnabled']:
        raise ValueError('至少启用一种说话方式')
    if (values['holdEnabled'] and values['toggleEnabled']
            and values['toggleKey'] in hold_keys):
        raise ValueError('两种说话方式不能使用同一个键')
    return dict(version=2, holdEnabled=values['holdEnabled'], holdKeys=list(hold_keys),
                toggleEnabled=values['toggleEnabled'], toggleKey=values['toggleKey'])


def load(path=None):
    path = Path(path) if path else ROOT / 'voice-settings.json'
    return validate(json.loads(path.read_text(encoding='utf-8-sig'))) if path.exists() else validate(DEFAULTS)


def apply(config):
    values = load()
    selected = [(key, True) for key in values['holdKeys']] if values['holdEnabled'] else []
    if values['toggleEnabled']:
        selected.append((values['toggleKey'], False))
    config.shortcuts = [dict(key=key, type='mouse' if key in ('x1', 'x2') else 'keyboard',
                             suppress=not (hold and key in ('ctrl_l', 'ctrl_r', 'shift_l', 'shift_r', 'alt_l', 'alt_gr')),
                             hold_mode=hold, enabled=True) for key, hold in selected]
    config.voice_settings = values
    # Recognition segmentation belongs to the original client configuration.
    print('已应用说话按键；识别使用官方默认分段参数。', flush=True)


def shortcut_hint(shortcuts):
    return '；'.join(('单独长按 ' + NAMES.get(sc['key'], sc['key'].upper()) + ' 约0.3秒开始，松开结束')
                    if sc.get('hold_mode', True) else
                    ('按 ' + NAMES.get(sc['key'], sc['key'].upper()) + ' 开始 / 结束')
                    for sc in shortcuts if sc.get('enabled', True))
