# 开发与构建

## 组成

这是基于 CapsWriter-Offline v2.6 的 Windows 桌面增强层，而不是独立重新训练的语音模型。`overlay/` 只保存有修改的客户端文件与公开默认配置；安装时覆盖到经过 SHA-256 校验的官方运行包。识别服务的模型代码继续来自上游。

`tools/VoiceStatus.cs` 提供唯一托盘与非激活状态胶囊；`VoiceSettings.cs` 提供统一设置；`InputTargetProbe.cs` 通过 Windows UI Automation 只读获取输入目标证据。Python 输出层负责确认输入并在必要时只复制完整结果。

`hold_gate.py` 为独立单键统一提供 0.3 秒判定。Windows 按键钩子将事件交给顺序工作队列，避免在钩子内等待录音锁或模拟按键。

## 构建

Windows 10/11 x64 和 .NET Framework 4.x 即可编译桌面组件：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Build.ps1
```

输出三个程序到 `tools/`，均不进入 Git。开发测试使用 Python 3.12+ 标准库；日常客户端使用官方包内的 Python 3.13 环境，无需让用户另外配置 Python 依赖。

```powershell
python -X utf8 -m unittest discover -s tests -v
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Check.ps1
```

CI 在 Windows 上构建 UI、执行单元测试并解析 PowerShell 脚本，不下载大模型，也不宣称完成真实麦克风或第三方输入框测试。

## 安装器验证

准备 `manifest.json` 指定的两个官方压缩包，运行：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Install.ps1 -InstallDirectory "E:\capswriter-clean-test" -ArchiveDirectory "E:\official-archives"
```

安装器不会自动启动识别服务。使用独立安装目录，避免与日常实例争用全局互斥或 6016 端口。再次安装应保留现有设置、热词和高级配置。

## 发布包

先提交经过检查的源码，再运行：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Package.ps1
```

打包脚本只使用 `git archive HEAD` 中的已提交内容，并加入从当前源码构建的三个桌面组件，生成 ZIP 和 SHA-256 文件。不包含模型、官方 Python 运行环境或本地运行数据。

## 本地功能验证范围

初版桌面功能已验证：单实例托盘、麦克风切换保存、实时收音检查、左 Alt / 左 Ctrl 独立启动、0.3 秒长按、组合键取消、录音提示显示/隐藏、官方 60/4 分段配置，以及正常输入、无输入位置、只读目标、拒绝粘贴与完整结果剪贴板兜底。

这些结果来自 Windows 上的专用输入测试窗口和实际录音流程，不代表所有应用、音频接口或硬件均兼容。没有把开发机器的录音或识别内容作为公开测试数据。
