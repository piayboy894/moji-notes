# 墨记 Moji Notes

Windows 和 Android 的个人文本管理软件。三个记事本类型：普通文本、提示词、账号密码。首次启动为空，先建记事本，再在本子内创建记录。

## 下载

- [Windows 2.0.0](https://github.com/piayboy894/moji-notes/releases/tag/v2.0.0)：x64 / ARM64 的独立 EXE 与便携 ZIP。
- [Android 2.0.0](https://github.com/piayboy894/moji-notes/releases/tag/android-v2.0.0)：固定发行密钥签名的 APK。

## 编辑器

正文直接采用 MIT 开源的 **Tiptap 官方 Simple Editor**，两端打包同一份离线资源。WPF 内嵌 WebView2，Compose 内嵌 Android WebView，不需要打开外部浏览器或访问编辑器网站。

引用、待办、列表、标题、文字样式、链接、图片和撤销全部由 Tiptap / ProseMirror 处理。没有旧的分块编辑器、源码视图、双栏预览、表格或查找替换兼容层。提示词额外保留“插入变量”和“使用提示词”：变量是可选中、可删除、可撤销的完整节点；手打 `{{内容}}` 始终是普通文字。

**旧存档可以读取，旧正文按普通文本原样打开，Markdown 等代码不会恢复成旧排版。** 已登记的提示词变量保留。新内容保存为带版本标记的编辑器 HTML，手机和电脑读取相同文档。

## 日常功能

- 本地自动保存、版本历史、收藏、置顶、归档、回收站、搜索和批量管理。
- 自定义记事本名称、图标、纯色和渐变封面；浅色、深色、字号与自定义快捷键。
- 密码本默认可选择、复制文字，点击“编辑密码”后修改。无密码显示开关、TOTP 或密码健康检查。
- TXT / Markdown 文件按文字导入，普通文本 / HTML 导出，全库存档和文本 ZIP；Windows 支持系统打印 / PDF。
- Android 支持手机、平板、折叠屏、旋转、分屏、可调整窗口和桌面模式的布局。

## 手动云存档

设置里填写自己的 GitHub 私有数据仓库与专用访问令牌，默认仓库为空。**无存档码，无自动同步或自动合并。** 全部记事本和记录作为一个整体，手动新建存档、覆盖存档、读取存档、删除存档。其他软件可共用仓库，墨记使用独立目录。

本地无需账号、主密码或自动锁定。数据为普通文件；GitHub 令牌保存在系统凭据保护中，不进入云存档。公开发行仓库的检测更新无需用户填写 Token。

## 从源码构建

```bash
npm ci --prefix editor
npm run build --prefix editor
npm test --prefix editor

dotnet test tests/Moji.Tests -c Release
dotnet build src/Moji.App -c Release -r win-x64
cd android
./gradlew :app:testDebugUnitTest :app:assembleDebug
```

Windows 完整离线发行包另运行 `tools/prepare-webview.ps1` 获取并核验 Microsoft 签名的固定 WebView2 Runtime，发布时传入 `-p:MojiWebViewRuntimeCab=绝对路径`。使用系统 Evergreen Runtime 时会由系统维护更新，未安装时使用包内离线组件。Android 的 WebView 由系统提供。

发布工作流分别验证 Windows 内嵌编辑器、Android 手机/平板窗口、真实签名 APK 的输入、变量、图片选择与重开。完整设计和边界见 [架构说明](docs/ARCHITECTURE.md)，开源来源见 [editor/UPSTREAM.md](editor/UPSTREAM.md)。
