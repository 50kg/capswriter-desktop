# 完整离线包的第三方来源

- 桌面增强层：本仓库源码和 MIT 许可证。
- 运行环境：Haujet Zhao / CapsWriter-Offline v2.6，保留官方包的 `LICENSE` 和 `internal/` 内各依赖许可。原始资源及固定 SHA-256 见 `manifest.json`。
- 识别模型：FunAudioLLM / Fun-ASR-Nano-2512，由 Tongyi Lab / QwenAudio 发布，模型卡标明 Apache-2.0： https://huggingface.co/FunAudioLLM/Fun-ASR-Nano-2512 。
- 本包采用 HaujetZhao 发布的 ONNX + GGUF q5 转换资源，未重新训练模型。资源： https://github.com/HaujetZhao/CapsWriter-Offline/releases/tag/models 。
- 模型的完整 Apache-2.0 许可保留为 `Fun-ASR-LICENSE.txt`，并随完整离线包复制到模型目录。文本来源为 QwenAudio/Fun-ASR 的提交 `c0a20e2bb698bbad11d143bdccd6c0760e9d6e5b` 下的 `LICENSE`： https://github.com/QwenAudio/Fun-ASR/blob/c0a20e2bb698bbad11d143bdccd6c0760e9d6e5b/LICENSE 。

模型权重只放在 Release 的完整离线 ZIP 中，不进入 Git 源码历史。轻量包仍由安装器单独下载。不同组件的许可不能相互替代。

完整包仅从已校验的官方压缩包和本仓库已提交源码组装，覆盖公开默认配置；不复制开发机器的日常部署、录音、日志、热词或设备列表。上游在线 LLM 角色配置不随完整包分发，保留中性的 `LLM/__init__.py` 模板供高级用户自行扩展。
