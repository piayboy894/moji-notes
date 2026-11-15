# 进度与验证范围

当前版本：Windows 1.2.0 / Android 1.1.0。实现与使用方法见 [README](../README.md)。

本轮已完成两端的单一所见即所得编辑、显式变量、交互待办与表格、密码只读/编辑、快捷键自定义、矢量图标，以及移除演示、标签、本地主密码/加密和自动锁定。云端继续为全局手动存档。

本地核心测试与原生项目编译后，使用 GitHub Actions 运行 Windows WPF 实际控件检查和 Android 原生仪器测试。发布步骤必须在测试通过后进行；每个版本附带实际原生测试报告。

- [Windows 流水线](https://github.com/piayboy894/moji-notes/actions/workflows/release.yml)
- [Android 流水线](https://github.com/piayboy894/moji-notes/actions/workflows/android.yml)
- 固定 Android 发布证书 SHA-256：`4c74ca5b73e47d9435c7f0fcc68edd2e56b9923177b65f33a32e92d67e661198`

实际三星分屏 / DeX 外接显示器、特殊固件、折叠屏铰链及 Windows ARM64 真机运行仍需硬件验证。私钥、密码和用户数据不进入源码或公开附件。
