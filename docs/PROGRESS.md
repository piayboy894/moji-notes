# 当前进度与验证范围

## 本次决定

- 用户已配置安卓发布签名 Secrets，继续生成正式 APK。
- 云端改为全局手动存档，包含软件全部记事本和记录；本机继续自动加密保存。
- 四个操作：新建存档、覆盖存档、读取存档、删除存档。无后台云同步、自动合并或新旧判断；单条记录和单个记事本不放同步按钮。

## 实现

- Windows 1.1.0：.NET 10 + WPF 原生应用、自包含 x64 / ARM64 EXE 和便携 ZIP。
- Android 1.0.0：Kotlin + Jetpack Compose 原生应用，使用固定项目发布密钥签名。
- 三色布纹笔记本界面、原生编辑和预览、提示词变量、密码库、TOTP、历史、归档、回收站与导入导出均保留。
- 云存档目录与整套数据分别加密，以一次 Git 提交原子更新；保留共用数据仓库其他路径。
- 读取整体替换项目数据，保留本机主密码、设备偏好与云连接。旧版单文件由用户明确选择操作。

## 已验证

- Windows 核心测试 23 项通过，原生项目本地编译通过。
- Android JVM 核心测试 22 项通过，原生调试 APK 与仪器测试 APK 编译通过。
- C# 实际生成的全局存档目录与数据向量在 Kotlin 解密通过，包含中文、密码模式和历史。
- 已在私有 `xuxian-data` 的隔离测试目录实际完成新建、读取、覆盖、刷新与删除；其他所有文件 blob SHA 保持不变，测试目录已清理。
- [Windows 发布流水线](https://github.com/piayboy894/moji-notes/actions/runs/43669106617)通过：23 项核心测试、x64 / ARM64 构建、8 项实际 WPF 原生检查（含整库读取和四个全局存档操作）。
- [Android 正式发布流水线](https://github.com/piayboy894/moji-notes/actions/runs/43670304701)通过：22 项核心测试、3 项原生仪器测试、9 张多尺寸与全局存档截图、固定证书核对、正式包安装/创建加密空间/覆盖安装保留数据。
- 正式包会话模型使用显式工厂创建；实际发布 APK 已完成启动与数据保留验证。
- APK SHA-256：`b3052bcdb3844e8e1bc48849cdfc7e693f4f21ba29afd68144aafa7faf078a40`。
- 固定发布证书 SHA-256：`4c74ca5b73e47d9435c7f0fcc68edd2e56b9923177b65f33a32e92d67e661198`。

## 下载与保留

- [Windows 1.1.0](https://github.com/piayboy894/moji-notes/releases/tag/v1.1.0)
- [Android 1.0.0](https://github.com/piayboy894/moji-notes/releases/tag/android-v1.0.0)
- [界面预览](INTERFACE.md) · [架构](ARCHITECTURE.md) · [Android 签名说明](../android/SIGNING.md)

发布版提供完整源码、校验值与原生测试结果；截图和进度说明保存在 Git 提交中。私钥及密码不进入源码或公开附件。

## 尚需真机验证

Windows ARM64 已交叉编译，尚未在 ARM64 真机运行。安卓模拟器尺寸检查不代表三星分屏、DeX 外接显示器或真实双屏设备实测。用户可在自己的设备试用后反馈具体窗口与交互问题。
