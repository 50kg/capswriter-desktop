# CapsWriter Desktop · 离线中文语音输入

面向 Windows 的中文语音输入桌面封装：一个启动入口、一个托盘、可见的收音状态，以及集中管理的麦克风和说话按键设置。

基于 [HaujetZhao/CapsWriter-Offline](https://github.com/HaujetZhao/CapsWriter-Offline) v2.6 开发。**语音识别核心来自上游，本项目提供桌面界面、启动管理和输入行为增强。** 默认使用 Fun-ASR-Nano GGUF q5 的 CPU 模式。

[下载桌面封装](https://github.com/50kg/capswriter-desktop/releases/latest) · [安装说明](#安装与启动) · [常见问题](docs/FAQ.md) · [开发与构建](docs/DEVELOPMENT.md)

## 功能

- **一个入口、一个托盘**：启动后在后台运行，右键托盘打开设置、热词、日志和退出功能。
- **底部收音提示**：录音时显示小胶囊和声音波形，识别时显示处理状态，空闲时隐藏，不抢输入焦点。
- **多个独立说话键**：可同时启用左 Alt、左 Ctrl、Caps Lock 或其他单键，任选一个单独按住 **0.3 秒**启动，松开结束。等待期间出现组合键会取消启动，正常复制粘贴保持可用。
- **点按开始 / 结束**：默认 F9，可与按住说话并存。
- **麦克风选择与收音测试**：选择当前程序使用的设备和音频接口，提供实时声音强度检查，不修改 Windows 的默认麦克风。
- **没有输入位置时只复制**：没有可输入的位置、目标只读或无法确认输入成功时，将完整结果放进剪贴板；你自己按 Ctrl+V。兜底不会再次尝试粘贴。
- **保留原剪贴板**：确认文字正常进入输入框后恢复原剪贴板文字；已有富文本等格式时改用打字方式，避免把剪贴板当作临时粘贴缓冲区。
- **可选开机启动**：设置中自行启用或关闭，默认关闭。
- **保留上游功能入口**：热词、规则替换、上下文、复制上次结果、重开音频、重启，以及高级配置。

默认固定中文识别，使用上游原始的 **60 秒分段、4 秒重叠**，不提供缩短分段的“出字速度”档位。**短句通常在松开按键后出字**；这不是微信输入法式的全程实时识别，也不保证后文能重新纠正此前所有片段。

录音状态组件（由程序自身绘制，实际使用时位于屏幕底部中央）：

![录音状态胶囊](docs/images/recording.png)

## 环境

- Windows 10 / 11，64 位。
- .NET Framework 4.x、Windows PowerShell 5.1；Windows 通常自带。
- 完整包保留上游随附的 MSVC 运行 DLL。若特定系统仍报告依赖缺失，可参考 [Microsoft Visual C++ x64 运行库说明](https://learn.microsoft.com/zh-cn/cpp/windows/latest-supported-vc-redist)。
- 模型使用 CPU，无需独立显卡；至少预留 **4 GB** 磁盘空间。安装包和模型压缩包合计约 **0.95 GB**，解压后还需要额外空间。
- 已在 i5-12400、32 GB 内存、CPU 模式的 Windows 环境验证主要流程，其他硬件的速度和识别效果请自行测试。

终端用户不需要另外安装 Python。完整离线包包含上游的 Python、随附运行 DLL 和中文模型；系统仍需满足 Windows 10/11 x64、.NET Framework 等平台要求。

## 安装与启动

推荐下载 **完整离线包**，区别如下：

| 下载文件 | 是否包含模型与运行环境 | 解压后的操作 |
| --- | --- | --- |
| `capswriter-desktop-版本-windows-x64-offline-full.zip` | 包含，约 1 GB，以 Release 显示为准 | **直接启动，不用安装、不用联网下载** |
| `capswriter-desktop-版本-windows-x64.zip` | 轻量包，不包含 | 先运行安装脚本，首次需要获取官方资源 |

1. 从 [Releases](https://github.com/50kg/capswriter-desktop/releases/latest) 下载文件名带 **`offline-full`** 的 ZIP，完整解压到一个**新的独立目录**。
2. **不用运行安装脚本**，直接双击 **`启动语音输入.vbs`**。托盘出现“本地语音输入”图标后即可使用。
3. 首次启动会自动准备本机麦克风列表，不携带打包机器的设备或个人设置。
4. 点击目标输入框，按住 **Caps Lock** 说话，松开结束；也可以按 **F9** 开始，再按一次结束。
5. 右键托盘 → **设置…**，选择麦克风，测试收音，添加多个说话单键，或启用开机启动；最后点击“保存并应用”。

完整离线包从首次启动开始就无需再联网安装或下载模型。默认语音识别在本机完成，识别服务只监听 `127.0.0.1:6016`，不需要账号或 API Key。

如果下载的是轻量包或 GitHub 自动生成的源码 ZIP，先运行 **`安装语音输入.cmd`**；安装器会从上游官方 Release 获取约 0.95 GB 的运行环境和模型，按固定 SHA-256 校验后安装。已备齐官方压缩包时也可以按下面的方法离线安装。

### 已有官方安装包 / 离线安装

准备这两个未经修改的官方压缩包，放在同一个目录：

- [CapsWriter-Offline-20260914.zip](https://github.com/HaujetZhao/CapsWriter-Offline/releases/download/v2.6/CapsWriter-Offline-20260914.zip)
- [Fun-ASR-Nano-GGUF.zip](https://github.com/HaujetZhao/CapsWriter-Offline/releases/download/models/Fun-ASR-Nano-GGUF.zip)

在项目目录运行：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Install.ps1 -ArchiveDirectory "E:\安装包"
```

安装器会优先使用这些本地文件，仍会校验 SHA-256。也可以用 `-InstallDirectory "E:\语音输入"` 安装到另一个新目录。不要把本项目直接覆盖到个人原有部署上；安装器会拒绝接管未标记的运行环境。

## 设置与使用

| 需求 | 操作 |
| --- | --- |
| 左 Alt、左 Ctrl 都能说话 | 设置 → 按住说话 → 依次选键、添加单键 → 保存；使用时任选一个单独按住 |
| 快速按键不录音 | 单独按住不足 0.3 秒不会启动；点按说话模式除外 |
| 没有文本框 | 说完后结果进入剪贴板，切到目标位置自行 Ctrl+V |
| 常用人名、地名或专业词出错 | 托盘 → 热词；具体格式参阅上游热词文档 |
| 查看或修改识别配置 | 托盘 → 更多热词与配置 |
| 退出 | 托盘 → 退出语音输入；会结束本目录所属的客户端和识别服务 |

“多个单键”指多个可任选的独立入口，不要求同时按下，也不是组合快捷键。所有按住说话入口共用一个录音会话，不会同时开启多路识别。

默认不启用 LLM 润色、不自动发送回车，**新安装默认不保存音频归档**。如需上游录音归档功能，可在高级输入端配置中设置 `save_audio = True`。

## 数据与边界

- Git 源码仓库不含模型权重或 Python 二进制环境；它们仅随 Release 的完整离线包分发。
- 所有发布包均不包含使用者的个人热词、录音、听写日志、麦克风列表或本机设置。完整包从已校验的官方原始包重建，不复制日常运行目录。
- 安装后的 `voice-settings.json`、`microphone.json`、`CapsWriter/`、`临时文件/`、`部署记录/` 与缓存均已加入 Git 忽略规则。
- **运行日志可能包含识别文本**，分享日志或提交 Issue 前请自行脱敏。开启音频归档后，文件保存在本机运行目录的年/月子目录。
- 输入目标检测依赖 Windows 辅助功能接口。某些自定义输入框、权限不匹配的应用或无法确认回执的目标会退回剪贴板；不保证所有软件都能自动填入。
- 收音状态显示表示程序收到音频，不代表识别一定准确。人名、同音词、噪音和麦克风质量仍会影响结果。
- 上游提供可选联网 LLM 等功能；本项目默认关闭。自行开启后，它们的联网行为不属于离线识别承诺。
- 当前使用全局单实例互斥和固定本机端口，不支持同时运行多个安装副本。

## 项目结构

```text
overlay/       对上游源码和默认配置的增强覆盖层
tools/         桌面界面源码、启动管理、设置与音频检查
scripts/       安装、构建、校验和打包脚本
third_party/   模型许可和完整包的第三方来源说明
defaults/      不包含个人信息的初始设置和热词模板
tests/         不依赖模型下载的自动化测试
docs/          常见问题、开发和验证说明
manifest.json  固定的上游版本、官方资源地址与校验值
```

## 开发与贡献

详见 [开发与构建](docs/DEVELOPMENT.md) 和 [贡献说明](CONTRIBUTING.md)。Windows 下可构建三个 C# 桌面组件；Python 单元测试不需要下载模型。

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Build.ps1
python -X utf8 -m unittest discover -s tests -v
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Check.ps1
```

## 致谢与许可证

- [CapsWriter-Offline](https://github.com/HaujetZhao/CapsWriter-Offline)：Haujet Zhao 开发的离线语音输入项目，本项目基于其 v2.6 软件包。保留原始 [MIT LICENSE](LICENSE)。
- [Fun-ASR-Nano GGUF 发布资源](https://github.com/HaujetZhao/CapsWriter-Offline/releases/tag/models)：默认中文识别模型，由安装器单独下载。

本项目的 MIT 许可证不替代模型和第三方依赖各自的许可证。完整包附带模型的 Apache-2.0 许可，并保留官方运行包内各依赖的许可文件。详情见 [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)。
