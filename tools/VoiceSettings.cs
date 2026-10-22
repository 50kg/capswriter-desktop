using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Microsoft.Win32;

class VoiceSettings : Form {
    class Choice { public string Key,Label; public Choice(string k,string t){Key=k;Label=t;} public override string ToString(){return Label;} }
    readonly string root=Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\')).FullName;
    readonly JavaScriptSerializer json=new JavaScriptSerializer();
    readonly CheckBox hold=new CheckBox(),toggle=new CheckBox(),autoStart=new CheckBox();
    readonly ComboBox holdPicker=new ComboBox(),toggleKey=new ComboBox();
    readonly ListBox holdKeys=new ListBox();
    readonly Button addHold=new Button(),removeHold=new Button();
    readonly Label status=new Label();
    readonly Button save=new Button(),reset=new Button(),refresh=new Button(),testMic=new Button();
    readonly ProgressBar micLevel=new ProgressBar();
    bool micTesting;string micTestId="";
    readonly ComboBox microphone=new ComboBox();
    readonly Label deviceHint=new Label();
    readonly List<Dictionary<string,object>> devices=new List<Dictionary<string,object>>();
    readonly EventWaitHandle activateSignal=new EventWaitHandle(false,EventResetMode.AutoReset,@"Local\CapsWriterVoiceSettingsActivate");
    readonly System.Windows.Forms.Timer activationTimer=new System.Windows.Forms.Timer();
    [STAThread] static void Main(string[] args) {
        Application.EnableVisualStyles();
        using(var mutex=new Mutex(false,@"Local\CapsWriterVoiceSettingsWindow")) {
            if(!mutex.WaitOne(0)){try{using(var signal=EventWaitHandle.OpenExisting(@"Local\CapsWriterVoiceSettingsActivate"))signal.Set();}catch{}return;}
            try {
                using(var form=new VoiceSettings()) {
                    if(args.Length>1&&args[0]=="--render") {
                        form.Location=new Point(-32000,-32000);form.Show();Application.DoEvents();
                        using(var b=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(b,new Rectangle(Point.Empty,b.Size));b.Save(args[1]);}
                    } else if(args.Length>1&&args[0]=="--verify-hotkey-controls") {
                        form.Location=new Point(-32000,-32000);form.Shown+=async delegate{await form.VerifyHotkeyControls(args[1]);form.Close();};Application.Run(form);
                    } else if(args.Length>1&&(args[0]=="--verify-save"||args[0]=="--verify-microphone")){
                        var watch=Stopwatch.StartNew();form.Location=new Point(-32000,-32000);if(args.Length>2&&args[0]=="--verify-save"){var candidate=form.json.Deserialize<Dictionary<string,object>>(File.ReadAllText(args[2],Encoding.UTF8));if(candidate.ContainsKey("microphone"))form.LoadDevices((Dictionary<string,object>)candidate["microphone"]);if(candidate.ContainsKey("holdKeys")||candidate.ContainsKey("holdKey"))form.LoadPreferences(candidate);}
                        form.Shown+=async delegate{if(args[0]=="--verify-microphone")await form.TestMicrophone();else await form.Save();File.WriteAllText(args[1],form.json.Serialize(new{seconds=watch.Elapsed.TotalSeconds,message=form.status.Text,isError=form.status.ForeColor==Color.Firebrick,saveEnabled=form.save.Enabled,controlsEnabled=form.microphone.Enabled,holdKeys=form.HoldKeyNames()}),new UTF8Encoding(false));form.Close();};Application.Run(form);
                    } else Application.Run(form);
                }
            }finally{mutex.ReleaseMutex();}
        }
    }
    VoiceSettings() {
        Text="语音输入 · 设置";ClientSize=new Size(660,675);
        StartPosition=FormStartPosition.CenterScreen;FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;
        AutoScaleMode=AutoScaleMode.None;Font=new Font("Microsoft YaHei UI",10);BackColor=Color.FromArgb(247,249,250);
        LabelAt("选择麦克风和说话按键，保存后自动生效。",20,18,620,30,false);
        LabelAt("说话按键",20,207,620,28,true);
        hold.Text="按住说话（多个单键任选其一）";hold.SetBounds(24,244,550,28);hold.AccessibleName="启用按住说话";
        holdKeys.SetBounds(24,280,616,86);holdKeys.AccessibleName="已启用的按住说话单键";holdKeys.IntegralHeight=false;
        InitCombo(holdPicker,24,377,400,"选择要添加的按住说话单键");InitCombo(toggleKey,205,473,435,"点按说话按键");
        AddKeys(holdPicker);AddKeys(toggleKey);holdPicker.SelectedIndex=0;
        addHold.Text="添加单键";addHold.SetBounds(434,376,96,32);removeHold.Text="移除所选";removeHold.SetBounds(540,376,100,32);
        addHold.Click+=delegate{try{AddHoldKey(Key(holdPicker));status.ForeColor=Color.FromArgb(35,110,82);status.Text="已添加单键，点击“保存并应用”后生效。";}catch(Exception e){status.ForeColor=Color.Firebrick;status.Text=e.Message;}UpdateHoldControls(hold.Checked);};
        removeHold.Click+=delegate{if(holdKeys.SelectedIndex>=0){int index=holdKeys.SelectedIndex;holdKeys.Items.RemoveAt(index);if(holdKeys.Items.Count>0)holdKeys.SelectedIndex=Math.Min(index,holdKeys.Items.Count-1);status.ForeColor=Color.FromArgb(35,110,82);status.Text="已移除所选单键，保存后生效。";}UpdateHoldControls(hold.Checked);};
        LabelAt("每个键单独按住 0.3 秒启动录音，松开结束；无需同时按。\r\n短按不启动，等待期间按其他键组成组合键则取消启动。",24,417,616,48,false).ForeColor=Color.FromArgb(92,104,115);
        toggle.Text="点按开始 / 结束";toggle.SetBounds(24,475,165,28);toggle.AccessibleName="启用点按说话";
        LabelAt("识别使用官方默认参数。",24,509,612,24,false).ForeColor=Color.FromArgb(92,104,115);
        status.SetBounds(24,577,612,42);status.ForeColor=Color.FromArgb(35,110,82);
        reset.Text="恢复默认";reset.SetBounds(20,628,116,34);
        save.Text="保存并应用";save.SetBounds(485,628,155,34);
        autoStart.Text="开机自动启动（登录 Windows 后）";autoStart.SetBounds(24,535,612,30);autoStart.AccessibleName="开机自动启动";
        using(var run=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run")){autoStart.Checked=run!=null&&String.Equals(Convert.ToString(run.GetValue("CapsWriterLocalVoice")),StartupCommand(),StringComparison.OrdinalIgnoreCase);}
        Controls.AddRange(new Control[]{autoStart,hold,toggle,holdKeys,holdPicker,addHold,removeHold,toggleKey,status,reset,microphone,refresh,testMic,micLevel,deviceHint,save});
        hold.CheckedChanged+=delegate{UpdateHoldControls(hold.Checked);};toggle.CheckedChanged+=delegate{toggleKey.Enabled=toggle.Checked;};
        reset.Click+=delegate{SetValues(true,new string[]{"caps_lock"},true,"f9");microphone.SelectedIndex=0;autoStart.Checked=false;status.Text="已恢复默认选项，点击“保存并应用”后生效。";};
        refresh.Click+=async delegate{await RefreshDevices();};
        microphone.SelectedIndexChanged+=delegate{UpdateDeviceHint();};
        LabelAt("麦克风",20,62,620,28,true);
        InitCombo(microphone,24,100,400,"麦克风设备");microphone.DropDownWidth=616;
        refresh.Text="刷新设备";refresh.SetBounds(434,99,96,32);
        testMic.Text="测试收音";testMic.SetBounds(540,99,100,32);testMic.Click+=async delegate{await TestMicrophone();};
        micLevel.SetBounds(24,190,616,8);micLevel.Visible=false;
        deviceHint.SetBounds(24,139,616,54);deviceHint.ForeColor=Color.FromArgb(92,104,115);
        LoadDevices(null);
        activationTimer.Interval=200;activationTimer.Tick+=delegate{UpdateMicrophoneMeter();if(activateSignal.WaitOne(0)){if(WindowState==FormWindowState.Minimized)WindowState=FormWindowState.Normal;Show();Activate();}};activationTimer.Start();
        save.Click+=async delegate{await Save();};LoadValues();
    }
    Label LabelAt(string text,int x,int y,int w,int h,bool bold) {
        var label=new Label(){Text=text,Location=new Point(x,y),Size=new Size(w,h)};
        if(bold)label.Font=new Font(Font.FontFamily,11,FontStyle.Bold);Controls.Add(label);return label;
    }
    void InitCombo(ComboBox c,int x,int y,int w,string name){c.SetBounds(x,y,w,30);c.DropDownStyle=ComboBoxStyle.DropDownList;c.AccessibleName=name;c.DropDownHeight=280;}
    void AddKeys(ComboBox c) {
        string[] keys={"caps_lock","num_lock","scroll_lock","pause","print_screen","insert","menu","ctrl_l","ctrl_r","shift_l","shift_r","alt_l","alt_gr","x1","x2"};
        string[] names={"Caps Lock","Num Lock","Scroll Lock","Pause","Print Screen","Insert","菜单键","左 Ctrl","右 Ctrl","左 Shift","右 Shift","左 Alt","右 Alt","鼠标后退键（侧键 X1）","鼠标前进键（侧键 X2）"};
        for(int i=0;i<keys.Length;i++)c.Items.Add(new Choice(keys[i],names[i]));
        for(int i=1;i<=24;i++)c.Items.Add(new Choice("f"+i,"F"+i));
        for(char ch='a';ch<='z';ch++)c.Items.Add(new Choice(ch.ToString(),char.ToUpper(ch).ToString()));
        for(char ch='0';ch<='9';ch++)c.Items.Add(new Choice(ch.ToString(),ch.ToString()));
    }
    string Key(ComboBox c){return c.SelectedItem is Choice?((Choice)c.SelectedItem).Key:"";}
    void Select(ComboBox c,string key){for(int i=0;i<c.Items.Count;i++)if(((Choice)c.Items[i]).Key==key){c.SelectedIndex=i;return;}throw new Exception("设置中有不支持的按键，请重新选择。");}
    string[] HoldKeyNames(){var keys=new List<string>();foreach(Choice item in holdKeys.Items)keys.Add(item.Key);return keys.ToArray();}
    void UpdateHoldControls(bool enabled){holdKeys.Enabled=holdPicker.Enabled=addHold.Enabled=enabled;removeHold.Enabled=enabled&&holdKeys.Items.Count>0;}
    void AddHoldKey(string key){
        foreach(Choice item in holdKeys.Items)if(item.Key==key)throw new Exception("这个单键已在列表中，无需重复添加。");
        foreach(Choice item in holdPicker.Items)if(item.Key==key){holdKeys.Items.Add(new Choice(item.Key,item.Label));holdKeys.SelectedIndex=holdKeys.Items.Count-1;return;}
        throw new Exception("设置中有不支持的按键，请重新选择。");
    }
    void SetValues(bool h,IEnumerable keys,bool t,string tk){holdKeys.Items.Clear();foreach(object key in keys)AddHoldKey(Convert.ToString(key));hold.Checked=h;toggle.Checked=t;Select(toggleKey,tk);UpdateHoldControls(h);toggleKey.Enabled=t;}
    void LoadPreferences(Dictionary<string,object> d){
        IEnumerable keys=d.ContainsKey("holdKeys")?(IEnumerable)d["holdKeys"]:new string[]{Convert.ToString(d["holdKey"])};
        SetValues(Convert.ToBoolean(d["holdEnabled"]),keys,Convert.ToBoolean(d["toggleEnabled"]),Convert.ToString(d["toggleKey"]));
    }
    async Task VerifyHotkeyControls(string reportPath){
        string original=File.ReadAllText(Path.Combine(root,"voice-settings.json"),Encoding.UTF8);
        SetValues(true,new string[]{"alt_l"},true,"f9");Select(holdPicker,"ctrl_l");addHold.PerformClick();
        bool added=String.Join("|",HoldKeyNames())=="alt_l|ctrl_l";
        addHold.PerformClick();bool duplicateBlocked=holdKeys.Items.Count==2&&status.ForeColor==Color.Firebrick;
        removeHold.PerformClick();bool removed=String.Join("|",HoldKeyNames())=="alt_l";
        addHold.PerformClick();Select(toggleKey,"alt_l");await Save();
        bool conflictBlocked=status.ForeColor==Color.Firebrick&&save.Enabled&&original==File.ReadAllText(Path.Combine(root,"voice-settings.json"),Encoding.UTF8);
        bool speedRemoved=true;foreach(Control control in Controls)if(control.Text.Contains("出字速度"))speedRemoved=false;
        File.WriteAllText(reportPath,json.Serialize(new{passed=added&&duplicateBlocked&&removed&&conflictBlocked&&speedRemoved,added=added,duplicateBlocked=duplicateBlocked,removed=removed,conflictBlocked=conflictBlocked,speedRemoved=speedRemoved}),new UTF8Encoding(false));
    }
    void LoadValues() {
        SetValues(true,new string[]{"caps_lock"},true,"f9");
        try {
            string path=Path.Combine(root,"voice-settings.json");if(!File.Exists(path))return;
            var d=json.Deserialize<Dictionary<string,object>>(File.ReadAllText(path,Encoding.UTF8));
            LoadPreferences(d);
        }catch(Exception e){status.ForeColor=Color.Firebrick;status.Text="读取设置失败："+e.Message;}
    }
    Dictionary<string,object> SelectedDevice() {
        return microphone.SelectedIndex>=0&&microphone.SelectedIndex<devices.Count?devices[microphone.SelectedIndex]:new Dictionary<string,object>{{"name",null},{"hostapi",null}};
    }
    void LoadDevices(Dictionary<string,object> selected) {
        if(selected==null){try{selected=json.Deserialize<Dictionary<string,object>>(File.ReadAllText(Path.Combine(root,"microphone.json"),Encoding.UTF8));}catch{}}
        if(selected==null)selected=new Dictionary<string,object>{{"name",null},{"hostapi",null}};
        microphone.Items.Clear();devices.Clear();
        devices.Add(new Dictionary<string,object>{{"name",null},{"hostapi",null}});microphone.Items.Add("跟随系统默认");
        string defaultName="";
        try {
            var cache=json.Deserialize<Dictionary<string,object>>(File.ReadAllText(Path.Combine(root,@"tools\audio-devices.json"),Encoding.UTF8));
            defaultName=Convert.ToString(cache["default"]);
            foreach(object entry in (IEnumerable)cache["devices"]){
                var d=(Dictionary<string,object>)entry;
                devices.Add(new Dictionary<string,object>{{"name",d["name"]},{"hostapi",d["hostapi"]}});
                microphone.Items.Add(Convert.ToString(d["name"])+" ["+Convert.ToString(d["hostapi"])+"]");
            }
        }catch{}
        int index=0;bool found=selected["name"]==null;
        for(int i=1;i<devices.Count;i++)if(Convert.ToString(devices[i]["name"])==Convert.ToString(selected["name"])&&Convert.ToString(devices[i]["hostapi"])==Convert.ToString(selected["hostapi"])){index=i;found=true;break;}
        if(!found){devices.Add(selected);microphone.Items.Add(Convert.ToString(selected["name"])+" ["+Convert.ToString(selected["hostapi"])+"]（未检测到）");index=devices.Count-1;}
        microphone.Tag=defaultName;microphone.SelectedIndex=index;UpdateDeviceHint();
    }
    void UpdateDeviceHint(){deviceHint.Text=microphone.SelectedIndex==0?"当前系统默认："+Convert.ToString(microphone.Tag)+"\r\n只影响本程序。插拔设备后可点击“刷新设备”。":"只影响本程序；普通麦克风可优先选择 Windows WASAPI 接口。\r\n更换后点击“保存并应用”，一次应用全部设置。";}
    async Task RefreshDevices(){
        var selected=SelectedDevice();Busy(true);status.ForeColor=Color.FromArgb(92,104,115);status.Text="正在检测录音设备…";
        try{
            await Task.Run(delegate{
                var p=new ProcessStartInfo("powershell.exe","-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \""+Path.Combine(root,@"tools\run-verification.ps1")+"\" -Script \""+Path.Combine(root,@"tools\list_audio_devices.py")+"\"");
                p.UseShellExecute=false;p.CreateNoWindow=true;p.WindowStyle=ProcessWindowStyle.Hidden;p.RedirectStandardOutput=true;p.RedirectStandardError=true;
                using(var process=Process.Start(p)){var stdout=process.StandardOutput.ReadToEndAsync();var stderr=process.StandardError.ReadToEndAsync();if(!process.WaitForExit(30000))throw new Exception("设备检测仍在进行，请稍后重试。");if(!Task.WaitAll(new Task[]{stdout,stderr},1500)&&process.ExitCode!=0)throw new Exception("操作未完成，请重新选择设备后再试。");if(process.ExitCode!=0)throw new Exception("无法检测设备，请稍后重试。");}
            });LoadDevices(selected);status.Text="设备列表已更新。";status.ForeColor=Color.FromArgb(35,110,82);
        }catch(Exception e){status.ForeColor=Color.Firebrick;status.Text=e.Message;}finally{Busy(false);}
    }
    void UpdateMicrophoneMeter(){
        if(!micTesting)return;
        try{var d=json.Deserialize<Dictionary<string,object>>(File.ReadAllText(Path.Combine(root,@"临时文件\microphone.test.live.json"),Encoding.UTF8));if(Convert.ToString(d["id"])!=micTestId)return;micLevel.Value=Math.Max(0,Math.Min(100,Convert.ToInt32(d["level"])));deviceHint.Text="正在测试收音，请靠近麦克风说话…\r\n声音强度："+micLevel.Value+" / 100（随实际声音变化）";}catch{}
    }
    async Task TestMicrophone(){
        Busy(true);micTesting=true;micTestId=Guid.NewGuid().ToString("N");micLevel.Visible=true;micLevel.Value=0;status.ForeColor=Color.FromArgb(92,104,115);status.Text="正在测试麦克风，请说话；约 4 秒完成。";
        try{
            var selected=SelectedDevice();File.WriteAllText(Path.Combine(root,@"临时文件\microphone.test.pending.json"),json.Serialize(new{id=micTestId,name=selected["name"],hostapi=selected["hostapi"]}),new UTF8Encoding(false));
            await Task.Run(delegate{
                var p=new ProcessStartInfo("powershell.exe","-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \""+Path.Combine(root,@"tools\run-verification.ps1")+"\" -Script \""+Path.Combine(root,@"tools\test_microphone.py")+"\"");p.UseShellExecute=false;p.CreateNoWindow=true;p.WindowStyle=ProcessWindowStyle.Hidden;
                p.RedirectStandardOutput=true;p.RedirectStandardError=true;p.StandardOutputEncoding=Encoding.UTF8;p.StandardErrorEncoding=Encoding.UTF8;
                using(var process=Process.Start(p)){var stdout=process.StandardOutput.ReadToEndAsync();var stderr=process.StandardError.ReadToEndAsync();if(!process.WaitForExit(25000))throw new Exception("收音测试超时，请换一个设备接口再试。");Task.WaitAll(new Task[]{stdout,stderr},1500);if(process.ExitCode!=0)throw new Exception("收音测试未完成："+(stderr.IsCompleted?stderr.Result.Trim():"请重试。"));}
            });
            var result=json.Deserialize<Dictionary<string,object>>(File.ReadAllText(Path.Combine(root,@"临时文件\microphone.test.result.json"),Encoding.UTF8));if(Convert.ToString(result["id"])!=micTestId)throw new Exception("未收到测试结果，请重试。");status.Text=Convert.ToString(result["message"]);status.ForeColor=Color.FromArgb(35,110,82);
        }catch(Exception e){status.ForeColor=Color.Firebrick;status.Text=e.Message;}finally{micTesting=false;Busy(false);}
    }
    protected override void Dispose(bool disposing){if(disposing){activationTimer.Dispose();activateSignal.Dispose();}base.Dispose(disposing);}
    string StartupCommand(){return "\""+Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"wscript.exe")+"\" \""+Path.Combine(root,"启动语音输入.vbs")+"\"";}
    void Busy(bool busy){testMic.Enabled=!busy;autoStart.Enabled=!busy;save.Enabled=reset.Enabled=refresh.Enabled=microphone.Enabled=!busy;hold.Enabled=toggle.Enabled=!busy;UpdateHoldControls(!busy&&hold.Checked);toggleKey.Enabled=!busy&&toggle.Checked;}
    async Task Save() {
        if(!hold.Checked&&!toggle.Checked){status.ForeColor=Color.Firebrick;status.Text="至少启用一种说话方式。";return;}
        if(hold.Checked&&holdKeys.Items.Count==0){status.ForeColor=Color.Firebrick;status.Text="请至少添加一个按住说话单键。";return;}
        if(hold.Checked&&toggle.Checked&&Array.IndexOf(HoldKeyNames(),Key(toggleKey))>=0){status.ForeColor=Color.Firebrick;status.Text="点按按键不能与列表中的按住说话单键重复。";return;}
        string pending=Path.Combine(root,@"临时文件\voice-settings.pending.json");
        Busy(true);status.ForeColor=Color.FromArgb(92,104,115);status.Text="正在保存并应用，请稍候…";
        try {
            File.WriteAllText(pending,json.Serialize(new{version=2,holdEnabled=hold.Checked,holdKeys=HoldKeyNames(),toggleEnabled=toggle.Checked,toggleKey=Key(toggleKey),microphone=SelectedDevice(),autoStart=autoStart.Checked}),new UTF8Encoding(false));
            string result=await Task.Run(delegate {
                var p=new ProcessStartInfo("powershell.exe","-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \""+Path.Combine(root,@"tools\apply-voice-settings.ps1")+"\" -SettingsFile \""+pending+"\"");
                p.UseShellExecute=false;p.CreateNoWindow=true;p.WindowStyle=ProcessWindowStyle.Hidden;
                p.RedirectStandardOutput=true;p.RedirectStandardError=true;p.StandardOutputEncoding=Encoding.UTF8;p.StandardErrorEncoding=Encoding.UTF8;
                using(var process=Process.Start(p)){
                    var stdout=process.StandardOutput.ReadToEndAsync();var stderr=process.StandardError.ReadToEndAsync();
                    if(!process.WaitForExit(55000))throw new Exception("应用仍在进行，请稍后查看语音状态。");
                    if(!Task.WaitAll(new Task[]{stdout,stderr},1500)&&process.ExitCode!=0)throw new Exception("操作未完成，请重新选择设备后再试。");
                    if(process.ExitCode!=0)throw new Exception(stdout.IsCompleted&&stdout.Result.Trim().Length>0?stdout.Result.Trim():stderr.IsCompleted?stderr.Result.Trim():"保存未完成，请重试。");
                    return stdout.IsCompleted&&stdout.Result.Trim().Length>0?stdout.Result.Trim():"全部设置已保存并生效。";
                }
            });
            status.ForeColor=Color.FromArgb(35,110,82);status.Text=result;
        }catch(Exception e){status.ForeColor=Color.Firebrick;status.Text="未能完成应用："+e.Message;}
        finally{Busy(false);try{File.Delete(pending);}catch{}}
    }
}
