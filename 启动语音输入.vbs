Option Explicit
Dim fso, root, shell, command
Set fso = CreateObject("Scripting.FileSystemObject")
root = fso.GetParentFolderName(WScript.ScriptFullName)
Set shell = CreateObject("WScript.Shell")
If Not fso.FileExists(root & "\CapsWriter\CapsWriter-Offline\start_client.exe") Then
    shell.Popup "Please run the installer (CMD file) before starting.", 0, "Voice input", 64
    WScript.Quit 1
End If
command = "powershell.exe -NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File """ & root & "\tools\start.ps1"""
shell.Run command, 0, False
