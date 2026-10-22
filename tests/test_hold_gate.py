import importlib.util
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location('hold_gate_under_test', ROOT/'overlay/core/client/shortcut/hold_gate.py')
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)

class Clock:
    def __init__(self): self.now=0; self.timers=[]
    def factory(self, delay, callback):
        clock=self
        class Timer:
            def start(self): self.due=clock.now+delay; self.active=True; clock.timers.append(self)
            def cancel(self): self.active=False
        timer=Timer();timer.callback=callback
        return timer
    def advance(self, seconds):
        self.now+=seconds
        for timer in list(self.timers):
            if timer.active and timer.due<=self.now:
                timer.active=False;timer.callback()

class Task:
    threshold=99
    def __init__(self): self.is_recording=False;self.starts=0;self.finishes=0;self.cancels=0
    def launch(self): self.is_recording=True;self.starts+=1
    def finish(self): self.is_recording=False;self.finishes+=1
    def cancel(self): self.is_recording=False;self.cancels+=1

class HoldTests(unittest.TestCase):
    def setUp(self):
        self.clock=Clock();self.gate=module.HoldGate(timer_factory=self.clock.factory);self.task=Task()

    def test_uniform_threshold_ignores_old_threshold(self):
        for key in ['ctrl_l','alt_l','caps_lock','f12','mouse:x2']:
            with self.subTest(key=key):
                previous=self.task.starts
                self.gate.key_down(key,self.task);self.clock.advance(.299)
                self.assertEqual(self.task.starts,previous)
                self.clock.advance(.002);self.assertTrue(self.task.is_recording)
                self.gate.key_up(key);self.assertFalse(self.task.is_recording)

    def test_short_press_and_repeat_do_not_bypass_delay(self):
        self.gate.key_down('ctrl_l',self.task);self.clock.advance(.1)
        self.gate.key_down('ctrl_l',self.task);self.clock.advance(.1)
        self.gate.key_up('ctrl_l');self.clock.advance(1)
        self.assertEqual(self.task.starts,0)

    def test_combinations_cancel_pending_and_active_recording(self):
        for other in ['c','v','a','shift_l']:
            self.gate.key_down('ctrl_l',self.task);self.clock.advance(.1)
            self.gate.key_down(other);self.clock.advance(1)
            self.gate.key_up(other);self.gate.key_up('ctrl_l')
        self.assertEqual(self.task.starts,0)
        self.gate.key_down('ctrl_l',self.task);self.clock.advance(.31)
        self.gate.key_down('c');self.assertEqual(self.task.cancels,1)
        self.gate.key_up('c');self.gate.key_up('ctrl_l')
        self.assertEqual(self.task.finishes,0)

    def test_alternative_keys_have_independent_sessions(self):
        first,second=Task(),Task()
        for key,task in [('alt_l',first),('ctrl_l',second),('alt_l',first),('ctrl_l',second)]:
            self.gate.key_down(key,task);self.clock.advance(.31)
            self.assertTrue(task.is_recording);self.gate.key_up(key)
            self.assertFalse(task.is_recording);self.assertFalse(self.gate.down)
        self.assertEqual(first.finishes,2);self.assertEqual(second.finishes,2)

    def test_busy_and_close_do_not_leak_pending_activation(self):
        gate=module.HoldGate(timer_factory=self.clock.factory,can_start=lambda:False)
        gate.key_down('alt_l',self.task);self.clock.advance(1)
        self.assertEqual(self.task.starts,0);gate.close();self.assertFalse(gate.down)

if __name__=='__main__':
    unittest.main()
