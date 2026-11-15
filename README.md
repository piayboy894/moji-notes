# 墨记 · Moji Notes

密码、提示词和普通文本，安放在同一个私人空间里。

**原生 Windows 桌面应用：.NET 10 LTS + WPF + AvalonEdit。没有 Electron、Chromium、WebView、网页外壳或后台 Web 服务。**

## 下载与启动

- [Windows 1.1.0](https://github.com/piayboy894/moji-notes/releases/tag/v1.1.0)：Intel / AMD 电脑下载 `Moji-Notes-1.1.0-win-x64.exe`，Windows ARM 设备下载 `Moji-Notes-1.1.0-win-arm64.exe`；也提供便携 ZIP。
- [Android 1.0.0](https://github.com/piayboy894/moji-notes/releases/tag/android-v1.0.0)：下载 `Moji-Notes-Android-1.0.0.apk`，使用固定发布密钥签名。
- [新版界面预览](docs/INTERFACE.md) · [Android 项目](android/README.md) · [进度与验证范围](docs/PROGRESS.md)

Windows EXE 自包含 .NET 运行库，Windows 10 / 11 可独立运行。Android 最低为 8.0，支持手机、平板和可调整窗口。首次启动设置本机主密码；也可先查看演示。

## 功能

| 模式 | 功能 |
| --- | --- |
| 普通文本 | 原生编辑器、Markdown 原生预览、编辑/双栏/阅读、撤销重做、行号、自动换行、字号、文档大纲、字数与行列统计、粗体/斜体/删除线/标题/引用/列表/任务清单/代码/表格/链接/内嵌图片、打印与 PDF |
| 提示词 | 上述编辑能力、`{{变量}}` 自动识别、变量填写、成品预览、一键复制、可复用模板 |
| 账号密码 | 隐藏与限时显示、账号/密码复制、强密码生成器、长度与字符设置、密码强度、弱密码与重复密码检查、网站、备注、六位 TOTP 动态验证码 |

所有模式均支持多个记事本、名称与颜色、标签、全局搜索、当前列表搜索、标题/创建/编辑时间排序、收藏、置顶、归档、同模式移动、复制记录、多选批量操作、40 个历史版本、手动版本快照、回收站、恢复与永久清除。

其他功能包括原生系统文件对话框、TXT/Markdown 导入、加密备份恢复与合并、按记事本导出 Markdown ZIP、单条导出 Markdown/TXT/RTF、系统打印 PDF、浅色/深色/跟随 Windows、专注模式、快捷键、加密本地备份、自动锁定、系统锁屏与休眠锁定、敏感剪贴板 30 秒清除、全局手动云存档。

## 全局云存档

本机编辑继续自动加密保存。云端由用户手动管理，入口位于全局侧栏「云存档」，每份存档包含全部记事本、记录、密码库、标签、收藏、归档、回收站和历史版本。单条记录或单个记事本没有同步按钮。

| 操作 | 结果 |
| --- | --- |
| 新建存档 | 为当前整套数据创建一份有名称的新存档，已有存档不变 |
| 覆盖存档 | 确认后，用当前整套数据替换选中的云存档 |
| 读取存档 | 确认后，用选中的存档整体替换本机全部项目数据 |
| 删除存档 | 确认后，删除选中的云存档，本机内容不变 |

不会后台自动同步、判断哪份更新或自动合并。存档列表仅在你点击刷新时读取；读取保留本机主密码、设备偏好和云连接设置。已有旧版单文件可在列表中作为「旧版存档」手动读取、覆盖或删除。

在「设置 → 云存档连接」填写数据仓库、仅有该仓库 **Contents: Read and write** 权限的 GitHub Token，以及 12–128 位数字或字母存档码。其他设备填写相同连接，在全局存档窗口点击刷新，再选择操作。Token 和存档码保存在本机密文中，不要发送到聊天或提交进源码。

```text
piayboy894/moji-notes             源码、构建和软件更新
piayboy894/xuxian-data
└─ moji-notes/<存档码 SHA-256 前 40 位>/
   ├─ index.moji                  加密的存档名称、时间与列表
   └─ <存档 ID>.moji              加密的整套项目数据
```

只修改专属目录，支持与其他软件共用数据仓库。数据和目录通过同一次 Git 提交更新，保留其他路径；仓库发生并发变化时停止操作，等待你刷新后重新确认，不自动重试或合并。GitHub 历史提交可能保留被覆盖或删除的旧密文。

## 自动更新

软件启动并解锁后自动读取公开仓库的稳定 Releases；也可到「关于与更新」手动检查。**检测和下载不使用任何 Token，不需要用户配置 Secret。** 点击下载后核对 SHA-256；再次点击“重启并更新”，由新的原生 EXE 等待旧进程退出、原子替换程序，并重新启动。数据保存在独立的 LocalAppData 目录，不随 EXE 替换。

发布维护者只需更新 `Moji.App.csproj` 的版本，再推送 `v<版本>` 标签；也可使用包含 `[release]` 的提交消息推送到 main。Actions 使用仓库自带 `GITHUB_TOKEN` 构建 x64 / ARM64、运行测试、启动发布版原生界面测试、生成校验值并发布。无需 Tauri 签名密钥或个人 Token。

SHA-256 用于传输完整性校验，不等同于 Windows Authenticode 发行者签名；发布仓库与 GitHub HTTPS 是更新的信任来源。

## 安全与数据

- AES-256-GCM，随机 16 字节盐与 12 字节 nonce，PBKDF2-SHA256 310,000 次派生。
- 本地：`%LOCALAPPDATA%\MojiNotes\vault.moji`；上一份加密快照为 `vault.moji.bak`。原子替换与磁盘刷写防止半写入文件。
- 主密码无法找回，请保存加密备份并妥善保管主密码与同步码。
- 原生 Markdown 渲染不执行 HTML、JavaScript；远程图片不自动联网加载。支持粘贴或导入 2 MB 以内的内嵌图片。
- GitHub 是版本化存储。回收站清除只影响当前数据，历史提交可能保留旧密文；旧备份也仍使用创建时的密码。
- 应用解锁时文本需要存在进程内存中；锁定会释放工作空间引用并清零派生密钥，但不抵御已控制操作系统的恶意软件。
- 无遥测，不接入 AI API；提示词模式负责整理与替换模板，不自动调用付费模型。

## 本地开发

需要 Windows 10 / 11、.NET 10 SDK，可用 Visual Studio 或命令行：

```powershell
dotnet test tests/Moji.Tests -c Release
dotnet run --project src/Moji.App
dotnet publish src/Moji.App -c Release -r win-x64 --self-contained true -o publish/win-x64
```

单文件 EXE 位于 `publish/win-x64/Moji.Notes.exe`。无需启动服务或打开浏览器。ARM64 将 RID 改为 `win-arm64`。

```powershell
# 在 Windows 上对实际发布版执行原生界面测试并生成截图
publish/win-x64/Moji.Notes.exe --smoke-test --output artifacts/smoke
```

## 源码结构

```text
src/Moji.Core/   数据模型、加密、原子存储、手动全局存档、GitHub 存储、更新、TOTP、模板
src/Moji.App/    WPF 原生视图、工作空间 ViewModel、AvalonEdit、FlowDocument Markdown 渲染
tests/Moji.Tests/ 加密/同步/更新/持久化/版本/模板测试
.github/workflows/ Windows 检查与自包含 EXE 发布
```

详见 [架构说明](docs/ARCHITECTURE.md)。MIT License。
