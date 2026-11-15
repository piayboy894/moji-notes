# 进度与验证范围

当前版本：Windows 1.2.0 / Android 1.1.0。实现与使用方法见 [README](../README.md)。

本轮已完成两端的单一所见即所得编辑、显式变量、交互待办与表格、密码只读/编辑、快捷键自定义、矢量图标，以及移除演示、标签、本地主密码/加密和自动锁定。云端继续为全局手动存档。记事本可选 16 种纯色、12 种可调渐变和 36 个图标。

首次打开没有任何本子或记录。工作台只创建记事本，进入具体本子后才显示「新建记录」。全部记录、收藏、归档和回收站不提供新建入口；自定义快捷键也遵循此层级。升级保留已有数据。

2026-11-15，运行代码提交 `14105cc04fe354d4d0ca66687c90559b871ba617` 完成以下验证：

- [Windows 验证](https://github.com/piayboy894/moji-notes/actions/runs/43682474238)：31 项核心测试、12 项原生 WPF 控件检查通过。包含空工作台、本子归属、聚合页面禁止新建、编辑落盘与重开、待办/表格/变量、密码编辑、收藏菜单、旧数据迁移和关闭保存。
- [Android 验证](https://github.com/piayboy894/moji-notes/actions/runs/43682472666)：38 项单元测试、8 项原生仪器测试通过。覆盖上述层级、渐变和图标持久化、快捷键、连续物理键盘输入、格式、表格、变量、密码编辑及多尺寸旋转。
- Android 正式签名 APK 的 3 项安装检查通过：空白新装后创建本子与编辑、覆盖重装保留数据；从已发布的 1.0.0 同签名覆盖升级并迁移旧数据；迁移后重启直接进入。

发行包复用上述成功流水线的原始附件，不重新编译。发布前核对 Windows EXE 校验值与 Android 固定证书指纹，附件含 `BUILD-PROVENANCE.json`、SHA-256 与原生检查报告。源码及原生截图已保留。

- [Windows 流水线](https://github.com/piayboy894/moji-notes/actions/workflows/release.yml)
- [Android 流水线](https://github.com/piayboy894/moji-notes/actions/workflows/android.yml)
- 固定 Android 发布证书 SHA-256：`4c74ca5b73e47d9435c7f0fcc68edd2e56b9923177b65f33a32e92d67e661198`

实际三星分屏 / DeX 外接显示器、特殊固件、折叠屏铰链及 Windows ARM64 真机运行仍需硬件验证。私钥、密码和用户数据不进入源码或公开附件。
