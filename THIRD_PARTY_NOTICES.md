# 第三方来源与许可

## CapsWriter-Offline

- 作者：Haujet Zhao。
- 来源：https://github.com/HaujetZhao/CapsWriter-Offline
- 使用版本：v2.6，发布资产 `CapsWriter-Offline-20260914.zip`。
- 许可：MIT，原始版权声明和完整许可文本保留在本仓库 `LICENSE`。
- 本仓库的 `overlay/core/client/` 包含在上游代码上修改的客户端文件，以及新增的状态、长按判定和输入目标检测组件。没有把识别引擎重新标为本项目原创。
- 安装器额外下载官方 Windows 软件包；其内置 Python、原生库、字体、图标等依赖仍按原始包内各自的许可使用。

## Fun-ASR-Nano GGUF

- 默认模型资产：https://github.com/HaujetZhao/CapsWriter-Offline/releases/tag/models
- 原始模型项目：https://github.com/FunAudioLLM/Fun-ASR
- 模型权重不包含在 Git 仓库或本项目发布的增强包中，由用户的安装器单独下载。
- 模型及转换后资源的使用范围应以其原始发布方的许可证为准；本项目 MIT 许可证仅覆盖适用的代码，不能替代模型许可。

## 本项目新增部分

本项目增加单托盘桌面 UI、多单键与长按等待、麦克风设置、开机启动设置、剪贴板兜底，以及安装、构建、文档和验证脚本，按仓库 MIT 许可证发布。
