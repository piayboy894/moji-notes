# 2.0.0 编辑器替换交付

Windows / Android 均采用 Tiptap 3.34.0 官方 MIT Simple Editor，打包同一份离线资源。保留显式提示词变量；不保留旧编辑器排版兼容。旧存档继续读取，旧正文按普通文本原样显示。

共享编辑器 33 项检查通过，覆盖桌面、手机和平板尺寸的引用、待办、变量、图片、撤销、文档切换、旧文本读取、中文组合输入和旧版 WebView 光标回退。Windows 35 项核心测试及 9 项 WPF / WebView2 原生检查通过；Android 33 项单元测试及 9 项原生检查通过，包含两种硬件键盘/软键盘状态、退格换行、引用和待办退出、窗口缩放、旋转、数据保存与重开。

正式签名 APK 已在全新模拟器完成安装、覆盖重装、已发布 1.0.0 的完整原始正文迁移、变量插入、取消选图、系统文件选择器插入大图片及进程重启后的数据核对。图片、变量和正文重开后的实际截图已核对。原子变量和图片不会全部出现在 Android 可编辑文字无障碍值中，因此另核对实际保存节点、图片数据和截图。

- Windows 构建与原生验证：[43710974666](https://github.com/piayboy894/moji-notes/actions/runs/43710974666)。x64 使用包内固定 WebView2 Runtime 验证；ARM64 构建完成。
- Android 原始构建、单元与原生验证：[43713396038](https://github.com/piayboy894/moji-notes/actions/runs/43713396038)。
- 同一 APK 的最终独立签名包验证：[43718784283](https://github.com/piayboy894/moji-notes/actions/runs/43718784283)。应用、原生测试、编辑器、资源和构建配置与原编译完全一致；仅修正验证脚本，未重新生成 APK。

Android 沿用原发行证书，可覆盖安装。发布工作流发布实际验证的二进制并附校验值、源码和来源记录。Windows 发行见 [v2.0.0](https://github.com/piayboy894/moji-notes/releases/tag/v2.0.0)，Android 见 [android-v2.0.0](https://github.com/piayboy894/moji-notes/releases/tag/android-v2.0.0)。
