# Android

Kotlin / Jetpack Compose 原生外壳，Android 8.0+。2.0.0 起，正文直接嵌入离线 Tiptap 官方 Simple Editor，与 Windows 使用同一份 `assets/editor/`；不是打开浏览器网站。

支持手机、平板、横竖屏、分屏和可调整窗口。系统 WebView 负责统一正文的输入、选区、引用、待办和撤销，Compose 负责记事本、变量弹窗、账号密码、存档与系统文件选择器。旧正文按普通文本原样载入，不兼容旧排版。

先在项目根目录执行 `npm ci --prefix editor && npm run build --prefix editor`，再执行 `./gradlew :app:testDebugUnitTest :app:assembleDebug`。发行工作流使用既有固定密钥，并测试优化后的真实签名 APK。无需新增 GitHub Secrets。

发行版关闭 R8 混淆和资源裁剪，保留完整的原生桥接与 Compose 代码；APK 仍为不可调试的正式签名版本。
