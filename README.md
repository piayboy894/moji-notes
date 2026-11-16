# 墨记 Moji Notes

Windows 和 Android 的个人文本管理软件。三个记事本类型：普通文本、提示词、账号密码。首次启动为空，先建记事本，再在本子内创建记录。

## 下载

- [Windows 2.4.1](https://github.com/piayboy894/moji-notes/releases/tag/v2.4.1)：x64 / ARM64 轻量独立 EXE 与便携 ZIP。
- [Android 2.4.1](https://github.com/piayboy894/moji-notes/releases/tag/android-v2.4.1)：固定发行密钥签名的 APK。

## 编辑器

正文直接采用 MIT 开源的 **Tiptap 官方 Simple Editor**，两端打包同一份离线资源。WPF 内嵌 WebView2，Compose 内嵌 Android WebView，不需要打开外部浏览器或访问编辑器网站。

引用、待办、列表、标题、文字样式、链接、图片和撤销全部由 Tiptap / ProseMirror 处理。没有旧的分块编辑器、源码视图、双栏预览、表格或查找替换兼容层。提示词中选中文字后点击“转化为变量”，选中文字成为变量名。填写区直接编辑，清空后显示“请在这里输入变量名”。变量名与填写值分别保存；复制全文只复制实际填写的内容。没有“使用提示词”弹窗，手打 `{{内容}}` 始终是普通文字。

**旧存档可以读取，旧正文按普通文本原样打开，Markdown 等代码不会恢复成旧排版。** 已登记的提示词变量保留。新内容保存为带版本标记的编辑器 HTML，手机和电脑读取相同文档。

## 日常功能

- 本地自动保存、版本历史、收藏、置顶、归档、回收站、搜索和批量管理；回收站可一次清空全部已删除记录。
- 标题中按 Enter 或 Tab 进入内容；两端复用同一编辑器实例，切换记录不再重新启动编辑器。
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

Windows 标准包内含 .NET，启用官方单文件压缩，不裁剪 WPF 或编辑器功能。使用系统共享的 Evergreen WebView2；缺失时运行包内约 2 MB 的 Microsoft 签名安装引导程序，首次需要联网，安装后可离线使用。源码构建标准包先运行 `tools/prepare-webview-bootstrapper.ps1`，发布时传入 `-p:MojiWebViewBootstrapper=安装引导程序绝对路径`。

离线使用前需已安装 WebView2；完全离线部署可使用 [Microsoft 官方离线安装程序](https://developer.microsoft.com/microsoft-edge/webview2/)。自行构建包含固定 Runtime 的全包，运行 `tools/prepare-webview.ps1` 并传入 `-p:MojiWebViewRuntimeCab=CAB绝对路径`。以前的 2.0.0 完整离线包继续保留在其发行页，不能代替 2.1.0 的新增功能。Android 的 WebView 由系统提供。

2.1.0 可读取以前的文档；新的填写区内容采用新版文档标记，手机与电脑均更新至 2.1.0 后交换新存档。安装时直接覆盖，保留本机记录与 GitHub 连接。源码 ZIP 与 `BUILD-PROVENANCE.json` 对应各自测试的构建来源。

发布工作流分别验证 Windows 内嵌编辑器、Android 手机/平板窗口、真实签名 APK 的输入、变量、图片选择与重开。完整设计和边界见 [架构说明](docs/ARCHITECTURE.md)，开源来源见 [editor/UPSTREAM.md](editor/UPSTREAM.md)。

2.2.0 增加密码记录自定义属性、统一的手动记事本顺序，以及独立的回收站卡片网格。电脑支持拖动框选，手机支持复选框多选。提示词填写区随段落自然换行，正文与标题对齐。新存档 schema 2 保护新增属性，继续读取 schema 1；交换新存档前请同时升级手机和电脑。

Android 拖动排序采用 [Reorderable 2.5.1](https://github.com/Calvin-LL/Reorderable)（Apache 2.0），许可随源码和 APK 的 assets/reorderable-LICENSE.txt 提供。

外观提供三套浅色和三套深色主题，编辑器与原生界面共用 `assets/themes.json`。记录卡片右下角依次为创建副本与置顶，右键菜单也能创建副本。[设计批评、色卡与参考资料](docs/design/THEMES.md)。
