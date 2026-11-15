# 墨记 Android

原生 Kotlin + Jetpack Compose 客户端，最低 Android 8.0（API 26）。无 WebView。

## 当前状态

Android 1.0.0 使用固定发布密钥签名，与 Windows 1.1.0 共用全局手动云存档。详细验证结果见 [PROGRESS](../docs/PROGRESS.md)。

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

普通文本、提示词、账号密码三种记事本；标签、收藏、排序、归档、回收站与历史版本；原生 Markdown 编辑预览、表格、清单与内嵌图片；提示词变量；强密码、TOTP、设备验证解锁；系统分享、拖入文本、文件导入、加密备份、文本 ZIP；全局手动云存档与检查更新。

## 与 Windows 共用存档

本机继续自动保存。全局「云存档」一次保存全部记事本和记录，提供新建、覆盖、读取和删除；读取整体替换，不自动合并。存档列表仅手动刷新。

两端填写相同数据仓库、存档码和授权。默认 `piayboy894/xuxian-data`，仅修改 `moji-notes/<存档码 SHA-256 前 40 位>/`；目录与数据均加密。旧版单文件作为「旧版存档」手动管理。云端不包含 Token 或设备偏好。

## 构建

需要 JDK 17、Android SDK 36、Build Tools 35.0.0：

```sh
./gradlew :app:testDebugUnitTest :app:assembleDebug
./gradlew :app:assembleRelease
./gradlew :app:connectedDebugAndroidTest
```

Gradle Wrapper 固定 8.11.1，并校验官方分发包 SHA-256。正式签名见 [SIGNING](SIGNING.md)，没有签名配置时 `assembleRelease` 仅生成未签名文件。

## 验证范围

核心测试覆盖实际 Windows 生成的加密测试向量、Unicode 密码、密文篡改、整库替换、归档/回收站/历史保留、原子保存、TOTP、GitHub 原子提交与并发停止。原生 UI 测试覆盖手机、平板、横屏、窄窗口、超宽比例和全局四个存档操作。发布版额外验证安装、创建加密空间及覆盖安装后数据保留。截图由 Android 模拟器直接取得。

这些尺寸检查不替代三星真机分屏、DeX 或特定设备固件的实测。
