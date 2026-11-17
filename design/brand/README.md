# 墨记图标

采用用户选定的白底蓝紫笔触：白色圆角底不透明，外侧透明；两条笔触保留原图的蓝紫渐变和柔和阴影。

`moji-master.png` 是背景移除后的图像源。`moji-icon.svg` 只定义导出时的精确圆角裁切和白底，保留原始笔触像素。运行 `bash tools/export-brand.sh` 导出共用 512 PNG、16–256 像素 ICO 和 Android 软件内 Logo。源设计位于 design 目录，不打包进 APK。

Android 从 manifest 引用 AdaptiveIconDrawable；常规前景取共用图像，留出 10% 的安全边距，底层为白色。Android 13 的系统主题图标使用同一两笔轮廓的单色描边。普通启动图标保持白底蓝紫色；系统主动启用主题图标时由系统染色。

依据：[Android 自适应图标](https://developer.android.com/develop/ui/compose/system/icon_design_adaptive)、[Apple 图标设计原则](https://developer.apple.com/design/human-interface-guidelines/app-icons)。
