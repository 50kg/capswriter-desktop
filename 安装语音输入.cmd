@echo off
chcp 65001 >nul
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\Install.ps1" %*
if errorlevel 1 (
    echo 安装未完成，请查看上面的错误提示。
    pause
    exit /b 1
)
echo 安装完成，请双击“启动语音输入.vbs”。
pause
