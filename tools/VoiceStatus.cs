using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;

class VoiceStatus : Form {
    readonly string root=Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\')).FullName;
    readonly JavaScriptSerializer json=new JavaScriptSerializer();
    readonly System.Windows.Forms.Timer timer=new System.Windows.Forms.Timer();
    readonly ContextMenuStrip menu=new ContextMenuStrip();
    readonly ToolTip tip=new ToolTip();
    readonly NotifyIcon tray;
    readonly ToolStripMenuItem trayState=new ToolStripMenuItem("本地语音输入 · 正在启动");
    bool quitting;
    readonly HashSet<string> pendingCommands=new HashSet<string>();
    readonly EventWaitHandle showSignal;
    readonly EventWaitHandle quitSignal;
    Dictionary<string,object> data=new Dictionary<string,object>();
    string phase="starting", previousPhase="";
    double now,lastRead,lastHealth,lastProcessCheck,errorUntil;
    int checkedPid;
    bool processAlive,rendering;
    float level;
    readonly Color ink=Color.FromArgb(28,33,37),green=Color.FromArgb(43,218,166),amber=Color.FromArgb(255,199,93);

    [STAThread] static void Main(string[] args) {
        Application.EnableVisualStyles();
        if(args.Length>0&&args[0]=="--quit"){try{using(var signal=EventWaitHandle.OpenExisting(@"Local\CapsWriterVoiceQuit"))signal.Set();}catch{}return;}
        bool owner;
        using(var mutex=new Mutex(true,@"Local\CapsWriterVoiceStatus",out owner)) {
            if(args.Length>1 && args[0]=="--render") {
                using(var form=new VoiceStatus(true)) {
                    form.rendering=true;form.Location=new Point(-32000,-32000);
                    form.phase=args.Length>2?args[2]:"recording";form.level=.65f;
                    form.now=UnixNow();form.data["lastAudioAt"]=form.now;
                    form.Show();Application.DoEvents();form.timer.Stop();
                    using(var bitmap=new Bitmap(form.Width,form.Height)) {
                        form.DrawToBitmap(bitmap,new Rectangle(Point.Empty,bitmap.Size));bitmap.Save(args[1]);
                    }
                }
                return;
            }
            if(!owner) {try{using(var signal=EventWaitHandle.OpenExisting(@"Local\CapsWriterVoiceStatusShow"))signal.Set();}catch{}return;}
            Application.Run(new VoiceStatus());
        }
    }
    VoiceStatus(bool noTray=false) {
        showSignal=new EventWaitHandle(false,EventResetMode.AutoReset,@"Local\CapsWriterVoiceStatusShow");
        quitSignal=new EventWaitHandle(false,EventResetMode.AutoReset,@"Local\CapsWriterVoiceQuit");
        Text="本地语音输入 · 收音状态";Name="VoiceStatus";
        AutoScaleMode=AutoScaleMode.None;ClientSize=new Size(148,40);
        BackColor=ink;ForeColor=Color.White;Font=new Font("Microsoft YaHei UI",9);
        FormBorderStyle=FormBorderStyle.None;ShowInTaskbar=false;TopMost=true;
        StartPosition=FormStartPosition.Manual;DoubleBuffered=true;
        using(var path=Rounded(new Rectangle(0,0,Width,Height),20))Region=new Region(path);
        trayState.Enabled=false;menu.Items.Add(trayState);menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("设置…",null,delegate{OpenSettings();});
        menu.Items.Add("复制结果",null,delegate{SendCommand("copy_result");});
        menu.Items.Add("日记 / 录音与记录",null,delegate{
            try{string folder=Path.Combine(root,@"CapsWriter\CapsWriter-Offline",DateTime.Now.ToString("yyyy"),DateTime.Now.ToString("MM"));Directory.CreateDirectory(folder);Process.Start(new ProcessStartInfo(folder){UseShellExecute=true});}catch{}
        });
        menu.Items.Add("上下文…",null,delegate{SendCommand("context");});
        menu.Items.Add("热词…",null,delegate{OpenRelativeFile("hot.txt");});
        menu.Items.Add("清除记忆",null,delegate{SendCommand("clear_memory");});
        menu.Items.Add("重开音频",null,delegate{if(CanRestart())SendCommand("restart_audio");});
        var advanced=new ToolStripMenuItem("更多热词与配置");
        advanced.DropDownItems.Add("服务端热词…",null,delegate{OpenRelativeFile("hot-server.txt");});
        advanced.DropDownItems.Add("热词替换规则…",null,delegate{OpenRelativeFile("hot-rule.txt");});
        advanced.DropDownItems.Add("输入端配置…",null,delegate{OpenRelativeFile("config_client.py");});
        advanced.DropDownItems.Add("识别服务配置…",null,delegate{OpenRelativeFile("config_server.py");});
        advanced.DropDownItems.Add("润色角色文件夹",null,delegate{OpenFolder("LLM");});
        menu.Items.Add(advanced);
        var logs=new ToolStripMenuItem("运行窗口与日志");
        logs.DropDownItems.Add("显示 / 隐藏输入端窗口",null,delegate{SendCommand("show_hide");});
        logs.DropDownItems.Add("输入端日志…",null,delegate{OpenLog("client.stdout.log");});
        logs.DropDownItems.Add("识别服务日志…",null,delegate{OpenLog("server.stdout.log");});
        logs.DropDownItems.Add("打开日志文件夹",null,delegate{try{Process.Start(Path.Combine(root,"部署记录"));}catch(Exception e){Notice(e.Message);}});
        menu.Items.Add(logs);
        var restart=new ToolStripMenuItem("重启");
        restart.DropDownItems.Add("重启输入端",null,delegate{RunMaintenance("restart-client.ps1");});
        restart.DropDownItems.Add("重启整套语音输入",null,delegate{RunMaintenance("restart-all.ps1");});
        menu.Items.Add(restart);
        menu.Items.Add(new ToolStripSeparator());menu.Items.Add("退出语音输入",null,delegate{ExitVoice();});
        if(!noTray){
            tray=new NotifyIcon();
            tray.Icon=new Icon(Path.Combine(root,@"CapsWriter\CapsWriter-Offline\assets\icon.ico"));
            tray.Text="本地语音输入";tray.ContextMenuStrip=menu;tray.DoubleClick+=delegate{OpenSettings();};tray.Visible=true;
        }
        MouseDown+=delegate(object s,MouseEventArgs e){if(e.Button==MouseButtons.Right)menu.Show(this,e.Location);};
        timer.Interval=40;timer.Tick+=delegate{TickStatus();};timer.Start();
        Shown+=delegate{if(!rendering)TickStatus();};
    }
    void OpenSettings(){try{Process.Start(new ProcessStartInfo(Path.Combine(root,@"tools\语音输入设置.exe")){UseShellExecute=true});}catch{}}
    void Notice(string text){if(tray!=null){tray.BalloonTipTitle="本地语音输入";tray.BalloonTipText=text.Length>250?text.Substring(0,250):text;tray.ShowBalloonTip(3000);}}
    bool CanRestart(){if(phase=="recording"||phase=="recognizing"){Notice("请先结束当前录音，等待识别完成。");return false;}return true;}
    void EditFile(string path){try{if(!File.Exists(path))throw new Exception("文件不存在："+Path.GetFileName(path));Process.Start(new ProcessStartInfo("notepad.exe","\""+path+"\""){UseShellExecute=true}).Dispose();}catch(Exception e){Notice(e.Message);}}
    void OpenRelativeFile(string path){EditFile(Path.Combine(root,@"CapsWriter\CapsWriter-Offline",path));}
    void OpenLog(string name){EditFile(Path.Combine(root,"部署记录",name));}
    void OpenFolder(string name){try{Process.Start(new ProcessStartInfo(Path.Combine(root,@"CapsWriter\CapsWriter-Offline",name)){UseShellExecute=true}).Dispose();}catch(Exception e){Notice(e.Message);}}
    void RunMaintenance(string script){if(!CanRestart())return;try{var p=new ProcessStartInfo("powershell.exe","-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \""+Path.Combine(root,"tools",script)+"\"");p.UseShellExecute=false;p.CreateNoWindow=true;p.WindowStyle=ProcessWindowStyle.Hidden;Process.Start(p).Dispose();Notice("正在重新连接语音输入…");}catch(Exception e){Notice(e.Message);}}
    void SendCommand(string action){
        ReadStatus();if(!processAlive||now-Number("updatedAt")>4){Notice("输入端尚未就绪，请稍后再试。");return;}
        try{
            string id=Guid.NewGuid().ToString("N"),folder=Path.Combine(root,@"临时文件\托盘命令");Directory.CreateDirectory(folder);
            string temporary=Path.Combine(folder,id+".tmp"),path=Path.Combine(folder,id+".json");
            File.WriteAllText(temporary,json.Serialize(new{id=id,action=action,pid=(int)Number("pid"),createdAt=UnixNow()}),new UTF8Encoding(false));
            File.Move(temporary,path);pendingCommands.Add(id);
        }catch(Exception e){Notice(e.Message);}
    }
    void ReadCommandReply(){
        try{
            var reply=json.Deserialize<Dictionary<string,object>>(File.ReadAllText(Path.Combine(root,@"临时文件\托盘操作结果.json"),Encoding.UTF8));
            string id=Convert.ToString(reply["id"]);if(pendingCommands.Remove(id)&&now-Convert.ToDouble(reply["updatedAt"])<320)Notice(Convert.ToString(reply["message"]));
        }catch{}
    }
    string[] MenuLabels(ToolStripItemCollection items){var labels=new List<string>();foreach(ToolStripItem item in items){if(item is ToolStripSeparator||item==trayState)continue;labels.Add(item.Text);var nested=item as ToolStripMenuItem;if(nested!=null)labels.AddRange(MenuLabels(nested.DropDownItems));}return labels.ToArray();}
    void ExitVoice(){
        if(quitting)return;quitting=true;
        if(tray!=null)tray.Visible=false;
        try{
            File.WriteAllText(Path.Combine(root,@"临时文件\停止请求.flag"),"stop");
            var p=new ProcessStartInfo("powershell.exe","-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \""+Path.Combine(root,@"tools\stop.ps1")+"\"");
            p.UseShellExecute=false;p.CreateNoWindow=true;p.WindowStyle=ProcessWindowStyle.Hidden;Process.Start(p).Dispose();
        }catch{}
        Close();
    }
    protected override bool ShowWithoutActivation {get{return true;}}
    protected override CreateParams CreateParams {get{var p=base.CreateParams;p.ExStyle|=0x08000000|0x00000080;return p;}}
    protected override void SetVisibleCore(bool value) {
        if(value && !rendering && !ActivePhase())value=false;
        base.SetVisibleCore(value);
    }
    protected override void WndProc(ref Message m) {if(m.Msg==0x21){m.Result=new IntPtr(3);return;}base.WndProc(ref m);}
    static double UnixNow(){return(DateTime.UtcNow-new DateTime(1970,1,1)).TotalSeconds;}
    string Value(string key,string fallback=""){object v;return data.TryGetValue(key,out v)&&v!=null?Convert.ToString(v):fallback;}
    double Number(string key,double fallback=0){object v;try{return data.TryGetValue(key,out v)?Convert.ToDouble(v):fallback;}catch{return fallback;}}
    bool ActivePhase(){return phase=="recording"||phase=="recognizing"||now<errorUntil;}
    void ReadStatus() {
        try {
            using(var stream=new FileStream(Path.Combine(root,@"临时文件\语音状态.json"),FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete))
            using(var reader=new StreamReader(stream,Encoding.UTF8))data=json.Deserialize<Dictionary<string,object>>(reader.ReadToEnd());
            int pid=(int)Number("pid");
            if(now-lastProcessCheck>1||checkedPid!=pid) {
                checkedPid=pid;lastProcessCheck=now;processAlive=false;
                try{using(var proc=Process.GetProcessById(pid))processAlive=!proc.HasExited&&String.Equals(proc.MainModule.FileName,Path.Combine(root,@"CapsWriter\CapsWriter-Offline\start_client.exe"),StringComparison.OrdinalIgnoreCase);}catch{}
            }
            phase=processAlive&&now-Number("updatedAt")<4?Value("phase","starting"):"offline";
        }catch{phase="offline";}
    }
    void TickStatus() {
        if(rendering)return;
        if(quitSignal.WaitOne(0)){ExitVoice();return;}
        now=UnixNow();showSignal.WaitOne(0);
        if(now-lastRead>.1) {
            lastRead=now;ReadStatus();
            trayState.Text="本地语音输入 · "+(phase=="recording"?"正在录音":phase=="recognizing"?"正在识别":phase=="ready"||phase=="done"?"已就绪":"正在连接");
            if(phase!=previousPhase) {
                if((phase=="error"||phase=="offline")&&(previousPhase=="recording"||previousPhase=="recognizing"))errorUntil=now+5;
                else if(phase=="recording"||phase=="recognizing"||phase=="done"||phase=="ready")errorUntil=0;
                if(phase=="recording"||phase=="recognizing") {
                    var area=Screen.FromPoint(Cursor.Position).WorkingArea;
                    Location=new Point(area.Left+(area.Width-Width)/2,area.Bottom-Height-20);
                }
                previousPhase=phase;
            }
            bool active=ActivePhase();
            if(active&&!Visible)Show();else if(!active&&Visible)Hide();
            tip.SetToolTip(this,phase=="recording"?"正在录音 · "+Value("shortcutHint","按 F9 或松开 Caps Lock 结束"):phase=="recognizing"?"正在本地识别":Value("error","识别服务未连接"));
        }
        level+=(float)(Number("level")-level)*.3f;
        Invalidate();
        if(now-lastHealth>1) {
            lastHealth=now;ReadCommandReply();
            try{File.WriteAllText(Path.Combine(root,@"临时文件\状态条健康.json"),json.Serialize(new{pid=Process.GetCurrentProcess().Id,visible=Visible,handleCreated=IsHandleCreated,x=Left,y=Top,width=Width,height=Height,phase=phase,trayVisible=tray!=null&&tray.Visible,trayName="本地语音输入",trayMenu=MenuLabels(menu.Items),previewVisible=false,nonActivating=true,updatedAt=now}));}catch{}
        }
    }
    static GraphicsPath Rounded(Rectangle r,int radius) {
        var p=new GraphicsPath();int d=radius*2;
        p.AddArc(r.X,r.Y,d,d,180,90);p.AddArc(r.Right-d,r.Y,d,d,270,90);
        p.AddArc(r.Right-d,r.Bottom-d,d,d,0,90);p.AddArc(r.X,r.Bottom-d,d,d,90,90);p.CloseFigure();return p;
    }
    protected override void OnPaint(PaintEventArgs e) {
        base.OnPaint(e);var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;
        using(var p=Rounded(new Rectangle(0,0,Width-1,Height-1),19))using(var pen=new Pen(Color.FromArgb(66,74,77)))g.DrawPath(pen,p);
        if(phase=="recording") {
            bool fresh=now-Number("lastAudioAt",now)<.8;
            Color color=fresh?green:amber;
            using(var pen=new Pen(color,1.8f)) {
                g.DrawArc(pen,23,10,9,9,180,180);g.DrawLine(pen,23,14,23,20);g.DrawLine(pen,32,14,32,20);
                g.DrawArc(pen,23,16,9,9,0,180);g.DrawArc(pen,19,16,17,13,0,180);g.DrawLine(pen,27.5f,29,27.5f,32);
            }
            using(var pen=new Pen(color,2.4f)) {
                pen.StartCap=pen.EndCap=LineCap.Round;
                for(int i=0;i<17;i++) {
                    double shape=.45+.55*Math.Sin(i*.65+now*5)*Math.Sin(i*.65+now*5);
                    float h=2+(fresh?level:0)*(float)(5+17*shape);
                    g.DrawLine(pen,49+i*4.6f,20-h/2,49+i*4.6f,20+h/2);
                }
            }
        } else if(phase=="recognizing") {
            using(var track=new Pen(Color.FromArgb(70,78,83),2))g.DrawEllipse(track,23,12,16,16);
            using(var pen=new Pen(amber,2)){pen.StartCap=pen.EndCap=LineCap.Round;g.DrawArc(pen,23,12,16,16,(float)(now*240%360),110);}
            TextRenderer.DrawText(g,"识别中",Font,new Rectangle(49,0,77,40),Color.FromArgb(235,238,240),TextFormatFlags.VerticalCenter|TextFormatFlags.HorizontalCenter);
        } else {
            using(var pen=new Pen(amber,2)){g.DrawEllipse(pen,23,12,16,16);g.DrawLine(pen,31,16,31,21);g.DrawLine(pen,31,24,31,25);}
            TextRenderer.DrawText(g,"请检查",Font,new Rectangle(49,0,77,40),amber,TextFormatFlags.VerticalCenter|TextFormatFlags.HorizontalCenter);
        }
    }
    protected override void Dispose(bool disposing){if(disposing){if(tray!=null){tray.Visible=false;tray.Icon.Dispose();tray.Dispose();}timer.Dispose();menu.Dispose();tip.Dispose();showSignal.Dispose();quitSignal.Dispose();}base.Dispose(disposing);}
}
