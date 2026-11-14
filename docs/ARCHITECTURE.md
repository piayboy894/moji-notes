# Windows 与 Android 原生架构

墨记由两个原生客户端组成，共用数据格式、加密协议和 GitHub 同步目录。Windows 10/11 使用 .NET 10 LTS、WPF 与 AvalonEdit；Android 8.0 及以上使用 Kotlin、Jetpack Compose 和 Android 系统接口。两端均离线运行，Markdown 预览使用原生视图。

| 层次 | Windows | Android |
| --- | --- | --- |
| 界面 | WPF、AvalonEdit、FlowDocument | Compose、BasicTextField、Markwon TextView |
| 会话状态 | WorkspaceViewModel | MojiViewModel、StateFlow、协程 |
| 数据与加密 | Moji.Core、AES-GCM | Kotlin 数据层、AES-GCM |
| 系统集成 | 文件对话框、剪贴板、打印、锁屏事件 | SAF、BiometricPrompt、分享、WindowManager、安装器 |
| 发布 | 自包含 x64/ARM64 EXE、ZIP | 固定发布密钥签名 APK |

共用协议通过实际 Windows 密文测试向量验证，原生界面分别适配平台。维护时需要同步审查两个客户端的数据格式与合并规则；这是保留各自系统交互能力的成本。

## 分层

`Moji.Core` 不依赖 WPF，处理不可变记录、版本、AES-GCM、密钥生命周期、原子存储、TOTP、搜索、模板、同步和更新。`Moji.App` 的 `WorkspaceViewModel` 管理会话、异步存储与状态；视图分为工作台、编辑器、账户字段和模态操作。AvalonEdit 提供原生文本缓冲、选区、撤销与查找；Markdig 只解析 Markdown AST，由 `MarkdownDocument` 生成原生 FlowDocument，不生成网页。

Android 的 `MojiViewModel` 负责解锁会话、记录操作、防抖保存与同步；`VaultStore` 只保存密文；`GitHubSync` 负责 Contents API 的条件写入。Compose 按窗口约束选择导航、列表与编辑面板；Markwon 解析后输出原生 TextView，不执行 HTML。AndroidKeyStore 仅保存设备验证所需的包装密钥，不替代空间主密码。

## 状态与持久化

UI 修改产生新的 Workspace / Note 记录。350 ms 防抖写入经过单写者信号量，序列化后加密。临时文件刷盘后使用 File.Replace 原子替换，同时保留上一份密文。锁定、关闭与更新先等待保存完成；保存失败时保留窗口与数据供备份。

密钥只在解锁会话保留，以不可重新导出的字节数组包装；锁定时清零。设备上只存密文，演示空间不落盘。每次加密使用新的 nonce。

## 多端同步

一个同步码对应私有数据仓库 `moji-notes/<hash>.moji`。远端只接收 notebooks、notes 和 schema，不接收 GitHub Token、同步配置与设备偏好。

每条记录有稳定 ID、修改版本和删除标记。本地保存上次成功同步的版本基线。单端编辑更新原记录；双端编辑保留副本；删除不会被未修改的旧设备复活。记事本删除会将关联记录纳入回收站。GitHub Contents API 的内容 SHA 提供并发写入条件；不匹配时失败，避免覆盖别人刚提交的密文。同步进行期间产生的本地修改还会与返回结果合并。

## 安卓窗口与输入

单 Activity 允许调整尺寸与旋转；布局按当前窗口 dp 尺寸决定，手机单页编辑，宽窗口双栏，宽且足够高的窗口增加侧边导航。超宽首页限制内容宽度与列数。WindowManager 的竖向分隔铰链可作为分栏间隔。

三星多窗口与 DeX 使用标准可调整窗口配置、硬件键盘快捷键和鼠标交互。单纯失去焦点不锁定，进入后台或系统锁屏按策略锁定。实际三星设备、外接显示器和特殊固件仍需真机验证，已验证范围见 [PROGRESS](PROGRESS.md)。

## 更新

UpdateService 只读取指定公开项目的稳定 Releases，过滤架构与固定文件名，不读取数据同步授权。EXE 和 SHA256SUMS.json 都限定到该项目的 GitHub release 下载路径。完整下载、长度和 SHA-256 检查通过后才允许启动更新。

新的自包含 EXE 以 `--apply-update` 模式运行，等待旧进程退出、保存上一版本、替换程序并重新启动。没有 PowerShell 脚本、运行时远程代码或静默提权。目录不可写时保留已下载文件并显示手动替换路径。

SHA 校验不替代发行者身份签名；Authenticode 可在正式对外发行时接入。当前信任边界是发布仓库控制权与 GitHub HTTPS。

Android 更新只匹配 `android-v*` Release 与固定 APK 文件名，校验 GitHub 提供的 SHA-256、APK 包名、版本及与当前安装一致的签名证书，再交给系统安装器。设备安装授权由 Android 负责；未配置固定发布签名密钥时不发布正式 APK。Windows 与 Android 的版本标签和资产过滤分开。

## 选择与边界

WPF 提供成熟 Windows 控件、可访问性、字体渲染、系统剪贴板与打印支持，适合 Windows 优先的文本工具。自包含发行换来较大的文件体积，但终端用户不必安装 .NET。Android 使用系统生命周期、输入法、生物验证、分享与多窗口接口。两端无需后台服务；GitHub 存储加密快照，适合个人文本资料，不作为实时多人协作文档服务。

原生 Markdown 支持常见标题、强调、列表、清单、代码、引用、表格、链接及内嵌图片；HTML 按文字展示。当前未实现富文本所见即所得和实时多人共同编辑；Markdown 是文本的标准存储格式。

测试包括两端核心测试、Windows 发布版 WPF 进程启动与编辑、Android 模拟器原生仪器测试、跨语言密文、加密持久化、锁定解锁、窗口旋转与多尺寸布局。截图直接来自 WPF 或 Android 原生渲染。
