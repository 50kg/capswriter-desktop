// Read-only input-target evidence for the dictation application's output layer.
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows.Automation;
class InputTargetProbe {
    [StructLayout(LayoutKind.Sequential)] struct Rect {public int left,top,right,bottom;}
    [StructLayout(LayoutKind.Sequential)] struct GuiInfo {public int size,flags;public IntPtr active,focus,capture,menu,move,caret;public Rect rect;}
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr window,out uint process);
    [DllImport("user32.dll")] static extern bool GetGUIThreadInfo(uint thread,ref GuiInfo info);
    [DllImport("user32.dll")] static extern IntPtr GetAncestor(IntPtr window,uint flag);
    static string Digest(string value){using(var sha=SHA256.Create())return Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(value)));}
    [STAThread] static void Main(){
        Console.InputEncoding=new UTF8Encoding(false);Console.OutputEncoding=new UTF8Encoding(false);
        var json=new JavaScriptSerializer();string expected="";
        try{var request=json.Deserialize<Dictionary<string,object>>(Console.ReadLine()??"{}");if(request.ContainsKey("expected"))expected=Convert.ToString(request["expected"]);}catch{}
        var result=new Dictionary<string,object>{{"editable",null},{"readable",false},{"identity",""},{"digest",""},{"expectedPresent",false}};
        IntPtr foreground=GetForegroundWindow();result["foreground"]=foreground.ToInt64();
        uint process;uint thread=GetWindowThreadProcessId(foreground,out process);var gui=new GuiInfo();gui.size=Marshal.SizeOf(typeof(GuiInfo));
        bool caret=GetGUIThreadInfo(thread,ref gui)&&gui.caret!=IntPtr.Zero&&GetAncestor(gui.caret,2)==foreground;
        result["nativeCaret"]=caret;
        result["nativeFocus"]=gui.focus.ToInt64();
        try {
            var element=AutomationElement.FocusedElement;
            if(element!=null){
                var current=element.Current;
                result["controlType"]=current.ControlType.ProgrammaticName;
                if(current.ProcessId!=(int)process){result["editable"]=false;}
                else{
                    int[] id=element.GetRuntimeId();result["identity"]=String.Join(".",id);
                    bool? editable=null;string text=null;object pattern;
                    if(!current.IsEnabled||current.IsPassword)editable=false;
                    else if(element.TryGetCurrentPattern(ValuePattern.Pattern,out pattern)){
                        var value=(ValuePattern)pattern;editable=!value.Current.IsReadOnly;
                        if(editable==true)text=value.Current.Value;
                    }else if(element.TryGetCurrentPattern(TextPattern.Pattern,out pattern)){
                        var range=((TextPattern)pattern).DocumentRange;
                        object readOnly=range.GetAttributeValue(TextPattern.IsReadOnlyAttribute);
                        if(readOnly is bool)editable=!(bool)readOnly;
                        if(editable==true)text=range.GetText(200000);
                    }
                    if(editable==null){
                        if(caret||current.ControlType==ControlType.Edit)editable=true;
                        else if(current.ControlType!=ControlType.Custom&&current.ControlType!=ControlType.Document)editable=false;
                    }
                    result["editable"]=editable;
                    if(text!=null){result["readable"]=true;result["digest"]=Digest(text);result["expectedPresent"]=expected.Length==0||text.Contains(expected);}
                }
            }
        }catch{if(caret)result["editable"]=true;}
        Console.WriteLine(json.Serialize(result));
    }
}
