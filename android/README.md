# 墨记 Android

原生 Kotlin + Jetpack Compose，Android 8.0+，无 WebView。1.1.0 使用原固定密钥签名，和 Windows 1.2.0 共用项目及云存档格式。

正文采用单一所见即所得编辑器；提示词变量显式插入；密码记录先只读，再点击编辑。新装无演示、标签、主密码或自动锁定，数据仓库默认空。本机自动写入普通 JSON，云端提供四种全局手动存档。

手机单页编辑，宽窗口列表加正文，大窗口增加导航；适配旋转、分屏、可调整窗口、鼠标和自定义快捷键。三星多窗口 / DeX 配置保留在原生 Activity 中，产品设置仅称「快捷键」。模拟器尺寸测试不代替三星设备实测。

构建需要 JDK 17、SDK 36、Build Tools 35.0.0。Gradle Wrapper 固定 8.11.1 并验证分发包摘要。

```sh
./gradlew :app:testDebugUnitTest :app:assembleDebug
./gradlew :app:assembleRelease
./gradlew :app:connectedDebugAndroidTest
```

签名见 [SIGNING](SIGNING.md)。没有签名配置时只生成未签名 Release。原生测试包含多尺寸、编辑工具、密码只读、快捷键与本地保存；正式包额外验证安装、重装和从公开 1.0.0 升级迁移。

[完整功能与数据说明](../README.md) · [架构](../docs/ARCHITECTURE.md) · [验证状态](../docs/PROGRESS.md)。
