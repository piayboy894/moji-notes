# Android 发布签名

Android APK 使用项目自己的固定签名密钥，不需要购买 Windows 发行者证书。密钥决定以后能否覆盖升级，必须持续保管。

## 在你自己的 Windows 电脑生成与保存

打开仓库的 `android/tools/New-AndroidSigningSecrets.ps1`，在 Windows PowerShell 中粘贴并运行其中的代码。脚本使用 Windows 自带的证书工具，无需管理员权限或 JDK。

它会：

1. 在你自己的「文档/Moji-Android-Signing」生成一个 3072 位 RSA 的 PKCS#12 签名文件和随机密码。
2. 将第一个 Secret 的值复制到剪贴板，提示你保存 `ANDROID_KEYSTORE_BASE64`。
3. 按 Enter 后，将第二个值复制到剪贴板，提示你保存 `ANDROID_KEYSTORE_PASSWORD`。
4. 最后清空剪贴板。不会把密钥或密码打印到控制台，也不会发送到第三方服务。

到 GitHub 仓库的 **Settings → Secrets and variables → Actions → New repository secret** 添加以上两个 Secret。不要添加到 Variables，不要提交进源码，不要发到聊天。

脚本保存的 `password.dpapi` 绑定当前 Windows 用户配置，不能作为跨电脑恢复密码的唯一备份。请同时将密码保存到自己的密码管理器，并妥善备份 `.p12` 文件。重复运行脚本会复用原来的密钥，不会换签名。

## 发布

Secrets 配好后，推送 `android-v1.0.0` 标签，或使用包含 `[android-release]` 的提交消息。工作流会先编译、测试，再用固定签名构建 APK；原生界面测试通过后发布。

GitHub 自动注入的 `GITHUB_TOKEN` 只负责上传 Release，不能替代 Android 的签名密钥。开发环境中临时生成的测试密钥不会用于用户配置密钥后的正式 APK。

本地发布构建也可通过 `MOJI_KEYSTORE_PATH` 和 `MOJI_KEYSTORE_PASSWORD` 环境变量传入签名。构建会自动读取 PKCS#12 的私钥别名。
