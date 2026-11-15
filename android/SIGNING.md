# Android 发布签名

Android APK 使用项目自己的固定签名密钥，不需要购买 Windows 发行者证书。密钥决定以后能否覆盖升级，必须持续保管。

## 当前项目与备份

当前项目已配置固定发布身份。后续保留这两个 Actions Secrets；需要恢复配置时，从签名备份包中读取同名 TXT 文件填入。备份包包含 `.p12` 和匹配密码，请私下保管。

已经安装的正式版只能被相同签名身份覆盖升级。下方生成脚本适用于尚未发布的新项目；不要用新密钥替换本项目已绑定的发布密钥。

## 新项目在 Windows 生成密钥

打开仓库的 `android/tools/New-AndroidSigningSecrets.ps1`，在 Windows PowerShell 中粘贴并运行其中的代码。脚本使用 Windows 自带的证书工具，无需管理员权限或 JDK。

它会：

1. 在你自己的「文档/Moji-Android-Signing」生成一个 3072 位 RSA 的 PKCS#12 签名文件和随机密码。
2. 将第一个 Secret 的值复制到剪贴板，提示你保存 `ANDROID_KEYSTORE_BASE64`。
3. 按 Enter 后，将第二个值复制到剪贴板，提示你保存 `ANDROID_KEYSTORE_PASSWORD`。
4. 最后清空剪贴板。不会把密钥或密码打印到控制台，也不会发送到第三方服务。

到 GitHub 仓库的 **Settings → Secrets and variables → Actions → New repository secret** 添加以上两个 Secret。不要添加到 Variables，不要提交进源码，不要发到聊天。

脚本保存的 `password.dpapi` 绑定当前 Windows 用户配置，不能作为跨电脑恢复密码的唯一备份。请同时将密码保存到自己的密码管理器，并妥善备份 `.p12` 文件。重复运行脚本会复用原来的密钥，不会换签名。

## 发布

当前已使用配对发布密钥配置 Actions Secrets。后续发布持续沿用此密钥，保管好下载的签名备份包。

Secrets 配好后，推送 `android-v1.0.0` 标签，或使用包含 `[android-release]` 的提交消息。工作流会先编译、测试，再用固定签名构建 APK；原生界面测试通过后发布。

GitHub 自动注入的 `GITHUB_TOKEN` 只负责上传 Release，不能替代 Android 的签名密钥。当前固定发布证书的公开 SHA-256 指纹保存在 `signing/publisher-certificate.sha256`，CI 会核对 APK 签名身份；私钥及其密码不进入源码仓库。

本地发布构建也可通过 `MOJI_KEYSTORE_PATH` 和 `MOJI_KEYSTORE_PASSWORD` 环境变量传入签名。构建会自动读取 PKCS#12 的私钥别名。
