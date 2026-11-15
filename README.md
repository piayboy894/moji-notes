# 墨记 · Moji Notes

Windows 与 Android 原生文本管理软件，支持普通文本、提示词、账号密码三种记事本。

## 下载

- [Windows 1.2.1](https://github.com/piayboy894/moji-notes/releases/tag/v1.2.1)：Intel / AMD 选 x64，Windows ARM 选 ARM64。提供自包含 EXE 和便携 ZIP，无需安装 .NET。
- [Android 1.1.1](https://github.com/piayboy894/moji-notes/releases/tag/android-v1.1.1)：Android 8.0 及以上，固定发布密钥签名，可覆盖升级。

打开即可使用，初始没有记事本或记录，点击虚线「＋」创建自己的本子。无主密码、演示内容、标签或自动锁定。首次升级旧版加密数据时，需要输入一次旧密码完成迁移；之后直接打开。已有记录不会因移除演示功能而被擅自删除。

## 编辑与整理

- 正文只有所见即所得编辑界面；标题、粗体、斜体、删除线、列表、引用、链接、图片、可点击的待办和可编辑表格。
- 表格可增删行列；编辑支持撤销、重做、查找替换、复制、自动保存和历史恢复。
- 提示词通过「插入变量」添加独立变量块，再点「使用提示词」填写、复制。手打或粘贴 `{{文字}}` 只是文字。
- 密码记录默认只读，可选择和复制；点击「编辑密码」后修改，点击「完成」恢复只读。密码直接显示，支持生成密码。
- 工作台只创建记事本；进入具体本子后才能新建对应类型的记录。全部记录、收藏、归档和回收站仅整理已有记录，不提供新建入口；快捷键遵守同样层级。
- 缩小的书架封面与末尾虚线「＋」新增卡片；每本可独立选择 16 种纯色、12 种渐变、36 个图标，也可调整自定义颜色及渐变两端。电脑右键本子、手机长按本子，选择「编辑记事本」。
- 多记事本、全局搜索、排序、收藏、置顶、归档、同类型移动、副本、历史、回收站与批量操作。
- TXT / Markdown 导入、文本导出、全库本地存档、文本 ZIP；Windows 另支持 RTF 与系统打印 / PDF。
- 统一矢量图标、浅色 / 深色 / 跟随系统、字号和自定义快捷键。手机、平板、横屏、分屏和可调整窗口布局。

## 全局手动云存档

数据仓库默认为空。在设置中填写自己的 `用户名/仓库名`、仅授权该仓库 Contents 读写的 GitHub Token。无需存档码，其他设备填写同一仓库即可手动读取。

| 操作 | 范围 |
| --- | --- |
| 新建存档 | 为全部记事本和记录保存一份新存档 |
| 覆盖存档 | 用当前全部数据覆盖选中存档 |
| 读取存档 | 用选中存档整体替换本机项目数据 |
| 删除存档 | 删除选中的云存档，本机内容不变 |

没有后台云同步、自动方向判断或自动合并。存档列表手动刷新；覆盖、读取、删除需要确认。读取保留本机连接和设备偏好。云端只包含项目数据，不包含 GitHub Token。

每份存档涵盖记事本、记录、密码、变量、收藏、归档、回收站和历史。仓库专属目录为 `moji-notes/saves/`，可以和其他软件共用数据仓库；只更新此目录。云存档是普通 JSON，使用私有仓库的访问权限保护；Token 不包含在存档中。已配置过旧版连接的设备仍可手动读取或覆盖转换旧版加密存档。

## 本地数据与升级

本地改为普通 JSON：Windows 位于 `%LOCALAPPDATA%\MojiNotes\workspace.json`，Android 位于应用私有目录。原子写入保留上一份 `.bak`。本地存档也是普通数据文件，包含密码记录，但不包含连接 Token；请按私人文件保管。本地移除加密后，设备上的项目数据及连接配置不再由主密码保护。

旧版 `vault.moji` 只用于一次迁移，旧备份仍需创建时的密码。读取存档是整库替换，失败时不会改动当前数据。GitHub 历史可能保留已覆盖或删除的旧云存档。

软件更新读取公开 Releases，无需用户提供 Token 或 Secret。Windows 下载后核对 SHA-256，点击重启更新；Android 额外核对包名、版本和签名，再交给系统安装器。Windows 当前未附 Authenticode 发行者证书。

## 开发

Windows：.NET 10 LTS / WPF / RichTextBox / FlowDocument。Android：Kotlin / Jetpack Compose / 原生富文本控件。没有浏览器、WebView 或 Web 服务，也没有 AI API 调用。

```powershell
dotnet test tests/Moji.Tests -c Release
dotnet run --project src/Moji.App
dotnet publish src/Moji.App -c Release -r win-x64 --self-contained true -o publish/win-x64
```

```sh
cd android
./gradlew :app:testDebugUnitTest :app:assembleDebug
./gradlew :app:connectedDebugAndroidTest
```

完整源码、许可证与原生测试报告随发行版提供。[架构](docs/ARCHITECTURE.md) · [验证范围](docs/PROGRESS.md) · [Android 签名](android/SIGNING.md)。
