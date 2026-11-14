# 原生 Windows 架构

墨记面向 Windows 10/11，以 .NET 10 LTS、WPF 与 AvalonEdit 实现原生桌面交互。所有视图、文件选择、打印、文本编辑、密码输入与 Markdown 预览都由 Windows/.NET 控件完成，不依赖 WebView、Electron 或浏览器进程。

## 分层

`Moji.Core` 不依赖 WPF，处理不可变记录、版本、AES-GCM、密钥生命周期、原子存储、TOTP、搜索、模板、同步和更新。`Moji.App` 的 `WorkspaceViewModel` 管理会话、异步存储与状态；视图分为工作台、编辑器、账户字段和模态操作。AvalonEdit 提供原生文本缓冲、选区、撤销与查找；Markdig 只解析 Markdown AST，由 `MarkdownDocument` 生成原生 FlowDocument，不生成网页。

## 状态与持久化

UI 修改产生新的 Workspace / Note 记录。350 ms 防抖写入经过单写者信号量，序列化后加密。临时文件刷盘后使用 File.Replace 原子替换，同时保留上一份密文。锁定、关闭与更新先等待保存完成；保存失败时保留窗口与数据供备份。

密钥只在解锁会话保留，以不可重新导出的字节数组包装；锁定时清零。设备上只存密文，演示空间不落盘。每次加密使用新的 nonce。

## 多端同步

一个同步码对应私有数据仓库 `moji-notes/<hash>.moji`。远端只接收 notebooks、notes 和 schema，不接收 GitHub Token、同步配置与设备偏好。

每条记录有稳定 ID、修改版本和删除标记。本地保存上次成功同步的版本基线。单端编辑更新原记录；双端编辑保留副本；删除不会被未修改的旧设备复活。记事本删除会将关联记录纳入回收站。GitHub Contents API 的内容 SHA 提供并发写入条件；不匹配时失败，避免覆盖别人刚提交的密文。同步进行期间产生的本地修改还会与返回结果合并。

## 更新

UpdateService 只读取指定公开项目的稳定 Releases，过滤架构与固定文件名，不读取数据同步授权。EXE 和 SHA256SUMS.json 都限定到该项目的 GitHub release 下载路径。完整下载、长度和 SHA-256 检查通过后才允许启动更新。

新的自包含 EXE 以 `--apply-update` 模式运行，等待旧进程退出、保存上一版本、替换程序并重新启动。没有 PowerShell 脚本、运行时远程代码或静默提权。目录不可写时保留已下载文件并显示手动替换路径。

SHA 校验不替代发行者身份签名；Authenticode 可在正式对外发行时接入。当前信任边界是发布仓库控制权与 GitHub HTTPS。

## 选择与边界

WPF 提供成熟 Windows 控件、可访问性、字体渲染、系统剪贴板与打印支持，适合 Windows 优先的文本工具。自包含发行换来较大的文件体积，但终端用户不必安装 .NET。没有为当前仅 Windows 的需求引入跨平台 Web 容器或远程服务。

原生 Markdown 支持常见标题、强调、列表、清单、代码、引用、表格、链接及内嵌图片；HTML 按文字展示。当前未实现富文本所见即所得和实时多人共同编辑；Markdown 是文本的标准存储格式。

测试包括跨平台 Core 单元测试、Windows 编译、发布版 WPF 进程启动、实际原生编辑控件修改、密文检查、锁定解锁和界面截图。没有使用浏览器测试桌面程序。
