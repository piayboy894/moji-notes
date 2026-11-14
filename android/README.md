# 墨记 Android

原生 Kotlin + Jetpack Compose 客户端，最低 Android 8.0（API 26）。无 WebView。

## 当前状态

源码与调试构建已保存，正式签名和发布按用户要求暂停。当前稳定 Windows Release 不包含这次新的界面；详细进度见 [PROGRESS](../docs/PROGRESS.md)。

## 界面与设备

- 简约书架首页：绿色文本本、米色提示词本、蓝色密码本；布纹封面、绑带和书签由原生 Canvas 绘制。
- 手机使用双列记事本封面和单页编辑。
- 平板、横向窗口使用列表与编辑分栏；大窗口增加侧栏。
- 没有固定屏幕方向或比例限制。窗口宽度、密度、旋转与键盘变化由界面响应。
- `resizeableActivity`、三星多窗口/DeX 元数据、可选触屏能力支持普通分屏、弹窗及鼠标键盘环境。
- 使用实际窗口约束，而非设备品牌或固定屏幕像素决定布局。
- Android WindowManager 提供铰链信息，分隔的竖向铰链可作为分栏间隔。
- 失去窗口焦点不会锁定；进入后台 10 秒后锁定，系统锁屏立即锁定。

## 功能

普通文本、提示词、账号密码三种记事本；标签、收藏、排序、归档、回收站与历史版本；原生 Markdown 编辑预览、表格、清单与内嵌图片；提示词变量；强密码、TOTP、设备验证解锁；系统分享、拖入文本、文件导入、加密备份、文本 ZIP；GitHub 加密同步与检查更新。

## 与 Windows 同步

在两端设置相同数据仓库和同步码。默认 `piayboy894/xuxian-data`，仅写入 `moji-notes/<同步码 SHA-256 前 40 位>.moji`。GitHub Token 只保存在本机加密文件内，云端不包含授权或设备偏好。

## 构建

需要 JDK 17、Android SDK 36、Build Tools 35.0.0：

```sh
./gradlew :app:testDebugUnitTest :app:assembleDebug
./gradlew :app:assembleRelease
./gradlew :app:connectedDebugAndroidTest
```

Gradle Wrapper 固定 8.11.1，并校验官方分发包 SHA-256。正式签名见 [SIGNING](SIGNING.md)，没有签名配置时 `assembleRelease` 仅生成未签名文件。

## 验证范围

核心测试覆盖实际 Windows 生成的加密测试向量、Unicode 密码、密文篡改、离线冲突、删除标记、原子保存、TOTP 和 GitHub 条件写入。原生 UI 测试覆盖手机、平板、横屏、窄窗口和超宽比例，截图由 Android 模拟器直接取得。

这些尺寸检查不替代三星真机分屏、DeX 或特定设备固件的实测。
