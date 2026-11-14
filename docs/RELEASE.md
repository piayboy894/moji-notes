墨记原生 Windows 稳定版 v1.0.1。

修正主背景与原生截图边界，并新增截图不透明性验证。

- .NET 10 LTS + WPF，原生编辑器与预览，无浏览器组件。
- 普通文本、提示词和账号密码三种记事本。
- 版本、收藏、标签、归档、回收站、批量操作、导入导出与打印。
- 主密码加密、本地备份、锁定、密码生成与 TOTP。
- 私有数据仓库加密同步；公开 Releases 自动检测和点击更新。
- Windows x64 / ARM64 自包含 EXE 和便携 ZIP，无需安装 .NET。

一般 Windows 电脑下载 x64。ARM Windows 下载 arm64。SHA256SUMS.json 提供下载完整性校验。当前 EXE 没有商业 Windows 发行者签名。

首次启动设置主密码。同步在设置中连接 `piayboy894/xuxian-data`，Token 仅需该仓库 Contents 读写权限。
