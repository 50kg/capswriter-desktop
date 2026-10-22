# 第三方来源与许可

## CapsWriter-Offline

- 作者：Haujet Zhao。
- 来源：https://github.com/HaujetZhao/CapsWriter-Offline
- 使用版本：v2.6，发布资产 `CapsWriter-Offline-20260914.zip`。
- 许可：MIT，原始版权声明和完整许可文本保留在本仓库 `LICENSE`。
- 本仓库的 `overlay/core/client/` 包含在上游代码上修改的客户端文件，以及新增的状态、长按判定和输入目标检测组件。没有把识别引擎重新标为本项目原创。
- 轻量包由安装器额外下载官方 Windows 软件包；完整离线包重新分发该软件包中的运行环境与依赖，并保留其许可文件。内置 Python、原生库、字体、图标等仍按原始包内各自的许可使用。

## Fun-ASR-Nano GGUF

- 默认模型资产：https://github.com/HaujetZhao/CapsWriter-Offline/releases/tag/models
- 原始模型项目：https://github.com/QwenAudio/Fun-ASR
- 原始模型卡：https://huggingface.co/FunAudioLLM/Fun-ASR-Nano-2512 ，标明 Apache-2.0。完整许可见 [third_party/Fun-ASR-LICENSE.txt](third_party/Fun-ASR-LICENSE.txt)。
- 模型权重不进入 Git 仓库；轻量包由安装器下载，完整离线包包含固定摘要对应的官方 ONNX + GGUF q5 转换资源，并在模型目录随附许可与来源说明。
- 模型及转换资源的来源与摘要见 [third_party/SOURCES.md](third_party/SOURCES.md) 和 `manifest.json`。本项目 MIT 许可证不能替代模型或各第三方依赖的许可。

## 本项目新增部分

本项目增加单托盘桌面 UI、多单键与长按等待、麦克风设置、开机启动设置、剪贴板兜底，以及安装、构建、文档和验证脚本，按仓库 MIT 许可证发布。
