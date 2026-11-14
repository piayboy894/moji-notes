# 墨记 · Moji Notes

密码、提示词和普通文本，安放在同一个私人空间里。

**原生 Windows 桌面应用：.NET 10 LTS + WPF + AvalonEdit。没有 Electron、Chromium、WebView、网页外壳或后台 Web 服务。**

## 下载与启动

从 [Releases](https://github.com/piayboy894/moji-notes/releases/latest) 下载：

- 常见 Intel / AMD Windows 电脑：`Moji-Notes-1.0.1-win-x64.exe`。
- Windows ARM 设备：`Moji-Notes-1.0.1-win-arm64.exe`。
- 也提供包含使用说明的 ZIP 压缩包。

EXE 自包含 .NET 运行库，Windows 10 / 11 解压后可直接启动，不需要安装 .NET 或浏览器组件。建议放在用户可写的独立目录，例如 `D:\Apps\Moji`，方便一键更新。首次启动设置至少 10 位的主密码；也可先使用不落盘的演示空间。

当前构建未附 Windows 发行者证书，Windows 可能显示未知发行者。下载页提供 `SHA256SUMS.json`，可用 `Get-FileHash 文件名.exe -Algorithm SHA256` 核对。正式代码签名可以后续接入证书，和应用内更新检查是不同的功能。

## 功能

| 模式 | 功能 |
| --- | --- |
| 普通文本 | 原生编辑器、Markdown 原生预览、编辑/双栏/阅读、撤销重做、行号、自动换行、字号、文档大纲、字数与行列统计、粗体/斜体/删除线/标题/引用/列表/任务清单/代码/表格/链接/内嵌图片、打印与 PDF |
| 提示词 | 上述编辑能力、`{{变量}}` 自动识别、变量填写、成品预览、一键复制、可复用模板 |
| 账号密码 | 隐藏与限时显示、账号/密码复制、强密码生成器、长度与字符设置、密码强度、弱密码与重复密码检查、网站、备注、六位 TOTP 动态验证码 |

所有模式均支持多个记事本、名称与颜色、标签、全局搜索、当前列表搜索、标题/创建/编辑时间排序、收藏、置顶、归档、同模式移动、复制记录、多选批量操作、40 个历史版本、手动版本快照、回收站、恢复与永久清除。

其他功能包括原生系统文件对话框、TXT/Markdown 导入、加密备份恢复与合并、按记事本导出 Markdown ZIP、单条导出 Markdown/TXT/RTF、系统打印 PDF、浅色/深色/跟随 Windows、专注模式、快捷键、加密本地备份、自动锁定、系统锁屏与休眠锁定、敏感剪贴板 30 秒清除、GitHub 加密同步与冲突副本。

## GitHub 云同步

源码与数据分开：

```text
piayboy894/moji-notes       源码、构建工作流、Releases 和软件更新
piayboy894/xuxian-data
└─ moji-notes/
   └─ <同步码的 SHA-256 前 40 位>.moji   仅加密数据
```

在「设置 → GitHub 同步」填写：

1. 数据仓库：默认 `piayboy894/xuxian-data`。
2. GitHub 精细权限 Token：仅选择数据仓库，授予 **Contents: Read and write**。也可使用具有相应仓库权限的已有 Token。
3. 12–128 位数字或字母同步码，推荐点击“生成”得到 24 位随机码。
4. 点击“保存并同步”，其他电脑填写同一个仓库、Token 和同步码。

同步码用于定位和加密云端空间，**不能代替 GitHub 对私有仓库的访问授权**。本机主密码可以与同步码不同。不要将 Token 填入源码、README 或聊天。Token 和同步设置只保存在本机加密文件内，云端数据不包含 Token 或设备偏好。

同步采用记录级合并、删除标记与 SHA 乐观并发保护。同一记录的两端修改会保留“同步副本”，而不是静默覆盖；云端文件在写入前发生变化时停止本次写入，重新同步即可。离线仍可完整编辑，自动同步只在解锁时每 5 分钟执行。

首次同步会合并本地与远端内容，因此新设备的示例记事本可能与已有记事本并存。更换同步码相当于使用新的云端空间，不会自动删除原文件。

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
src/Moji.Core/   数据模型、加密、原子存储、记录级合并、GitHub 同步、更新、TOTP、模板
src/Moji.App/    WPF 原生视图、工作空间 ViewModel、AvalonEdit、FlowDocument Markdown 渲染
tests/Moji.Tests/ 加密/同步/更新/持久化/版本/模板测试
.github/workflows/ Windows 检查与自包含 EXE 发布
```

详见 [架构说明](docs/ARCHITECTURE.md)。MIT License。
