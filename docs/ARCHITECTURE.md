# 架构

Windows 为 .NET 10 / WPF，Android 为 Kotlin / Jetpack Compose。记事本、导航、密码记录、文件选择、凭据存储、更新和全局云存档由原生层负责。正文只有一个 Tiptap 3.34.0 编辑器实例，使用官方 MIT Simple Editor 模板。

`editor/` 包含可构建的官方组件、固定依赖和原生桥接；`assets/editor/` 是提交到仓库的离线构建产物，两端均从这里打包。无 CDN、远程字体、Web 服务或 AI API。

## 文档与变量

每条新文档的 `content` 是 `<!--moji:tiptap:2-->` 加 Tiptap HTML，继续读取版本 1。原生宿主不重建段落、引用、待办或图片，不接管 Enter、Backspace、选区和输入法。这些行为、样式和 SVG 工具图标来自官方编辑器。旧正文作为文字段落载入，不解析 Markdown 或旧表格结构；只有已登记的提示词变量会转换为新的填写区。

变量节点记录 ID、名称和填写值，作为 Tiptap NodeView 嵌入独立的 HTML plaintext-only 行内编辑宿主。使用浏览器内置输入处理中文组合、选区、删除与多行文字，宿主与 ProseMirror 文档隔离，用输入事件提交节点 value，不把填写内容嵌套为 ProseMirror 的子文档。不再使用不可换行的 textarea 或尺寸镜像；行内 span 可跨段落行框，背景使用 box-decoration-break。空值占位和换行末尾的 BR 只用于显示，不进入文档或复制数据。选择正文文字后转化，名称固定为选中文字、值初始为空。每个 input 事件更新节点属性，由 Tiptap 管理文档撤销和持久化；输入过程中不回填相同值，不打断组合输入。HTML 使用 `data-moji-field` 标记，正文包含实际填写值，名称保留在属性中，纯文本复制不包含占位。普通 `{{` 没有特殊规则。

Chrome 124 及更早的 Android WebView 可能在隐藏软键盘的已提交输入中返回旧光标。兼容插件让系统完成原生输入，只在 ProseMirror 默认事务中修正这个错误选区；官方输入规则先运行，组合输入和替换输入不受影响。

官方模板没有表格，项目移除旧的自制表格和查找替换工具。示例数据、外部字体、重复的主题切换和模拟图片上传被移除。图片通过系统选择器交给浏览器图像解码，最长边缩至 1800，必要时继续压缩，以 data URI 离线保存在当前文档，不写入外部图床。

## 桥接与持久化

JS 在文档更新时提交完整 HTML 和变量给宿主。Windows 使用 WebView2 `WebMessageReceived`，Android 使用带 `@JavascriptInterface` 的固定本地桥接。宿主核验记录 ID、独立文档会话、空间代次、长度、文档标记和变量 ID，再执行本地自动保存。每个窗口 / Activity 只有一个浏览器和 Tiptap 页面；切换不同文档前刷新旧文档，再载入新文档并重置撤销历史。同一文档返回时保留编辑状态。Android 由 Activity 持有编辑器，Composable 退出不销毁浏览器，真正销毁 Activity 时才释放。正文保存为单向流：原生界面不能因为自动保存或状态变化再次回填 HTML；显式恢复历史版本才建立新的编辑会话，并拒绝旧会话迟到的更改。字号、主题与快捷键通过独立配置接口更新。入口 URL 含 index 内容校验值，脚本和样式使用内容哈希文件名，避免复用旧版本缓存。原生背景在页面显示前与主题一致。

启动时就预加载，而不是第一次进入记录才创建：Windows 在空间打开时把编辑器接入屏幕外的窗口宿主，页面完成布局后才移到正文区域；Android 把 WebView 附着在不透明 Compose 工作台背后的原生宿主中，阻止焦点与无障碍访问，用 postVisualStateCallback 确认首帧可绘制后再展示。退出记录时停放同一个实例；窗口 / Activity 销毁时释放。静态入口在主脚本前设置主题背景，避免首屏使用默认黑底。

工具栏显示只取决于编辑器支持的功能；临时只读、焦点或选区变化只决定按钮是否禁用，不改变完整布局。共享 hook 直接返回当前 context 的编辑器，避免初次订阅仍读到旧的 null。文档加载和重新激活后分发不改变内容的事务，使按钮状态及时刷新，不制造保存消息或撤销步骤。首条记录未聚焦时也可直接点击工具。

自动保存直接写入已经收到的文档，不依赖编辑进程完成另一轮刷新。显式保存、导出和云存档先请求刷新；即使刷新失败，Windows 也先保存已经收到的状态，再向调用方报告失败。Android 在后台切换与释放 WebView 前等待刷新确认，避免最后一次输入丢失。Android 消息使用主线程 Handler 投递，不依赖已经离开布局的 View.post，确保释放时仍能收到确认。读取存档更换空间代次，旧宿主的迟到消息不能覆盖新空间。

Windows 使用本地虚拟域名映射，Android 使用 WebViewAssetLoader 的本地 HTTPS 源。AndroidView 中的 WebView 明确使用 MATCH_PARENT 布局参数，避免 WRAP_CONTENT 导致网页的百分比高度和视口高度为零。原生测试同时检查页面实际高度与文档内容。CSP 禁止外部连接和外部图片；外部页面不能导航进编辑器。用户点击“打开链接”时由原生层处理 HTTP / HTTPS。发行版不启用开发调试接口。

## 发行

Windows 标准单文件 EXE 内含 .NET 与编辑器，使用 `EnableCompressionInSingleFile` 压缩，不启用 WPF 不支持的 trimming。优先使用系统共享 Evergreen WebView2；缺失时执行嵌入的 Microsoft 官方小型 bootstrapper，首次联网补装，失败可重试或使用完整离线包。构建时核验安装程序的 Authenticode 签名。共享 Runtime 由 Microsoft 维护安全更新，应用不删除现有 Runtime 或缓存。

完整离线包继续保留已经独立验证的 .NET / 编辑器 / Fixed WebView2 CAB 全包。Runtime 地址和版本在 `assets/webview-runtime.json`。标准 x64 原生检查强制执行一次官方 bootstrapper，再验证后续编辑器实例直接复用系统 Evergreen；CI 系统本身已有 Evergreen，此检查不等同于在缺少 Runtime 的全新 Windows 上测试下载安装。ARM64 完成构建，不宣称在 ARM64 真机运行过。压缩只改变分发，首次加载需要解压，内存与磁盘使用不会按下载体积同比缩小。

同版本替换先暂存并核验新资产，再保留原完整包、来源与测试报告并替换默认名称。下载校验值对应实际新字节。Git 标签不重写；新源码归档、构建提交和校验值随发行一起提供。同版本客户端不会自动收到更新提示。

Android 编辑器在侧栏开始展开、或上层对话框出现时，结束原生选择 ActionMode 并向 WebView 传递失焦，收起独立弹窗中的选区手柄；不清空文档或重建编辑器。Compose 文本框也在侧栏展开时退出焦点。全局消息宿主放在导航遮罩上方，并避开侧栏底部设置按钮；打开 Dialog 后，只在最上层 Dialog 的 Android 窗口中显示消息，避免主窗口 Snackbar 被 Dialog 的暗幕遮挡。打开设置或新建记事本时自动收起模态侧栏，关闭弹窗后直接回到原页面。

Android 继续沿用固定发行证书，后续 APK 可覆盖升级。系统 WebView 属于 Android 系统组件。数据采用同一文档格式，通过手动全局云存档跨端交换。发布工作流只发布验证成功的原始产物，不在发布时重新构建。

仅修改验证脚本时，可以复用不可变的 APK 产物。工作流要求原构建、单元测试、签名和原生界面检查已通过，并核验应用、原生测试、共享编辑器、资源及构建配置完全一致；随后在全新模拟器中重验原 APK。发布证明分别记录编译来源和验证来源，不在发布阶段重新打包。

## 包体方案参考

- [Microsoft WebView2 分发指南](https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/distribution)：推荐共享 Evergreen，缺失时通过小型 bootstrapper 安装；完整 Fixed Runtime 的体积超过 250 MB。
- [.NET 单文件与压缩](https://learn.microsoft.com/en-us/dotnet/core/deploying/single-file/overview)及 [WPF trimming 限制](https://learn.microsoft.com/en-us/dotnet/core/deploying/trimming/incompatibilities)：使用受支持的压缩，不通过不兼容的裁剪缩小 WPF。
- [Tauri Windows 分发方案](https://v2.tauri.app/distribute/windows-installer/)与[维护者社区讨论](https://github.com/orgs/tauri-apps/discussions/3048)：共享 WebView2、缺失时补装，是同类桌面软件常用的分发方式。


2.2.0 继续保持一个 WPF 记录界面和原生正文宿主，记录切换不重建标题、工具条或重新挂载浏览器。永久的填充容器与固定高度变量操作行避免异步载入期间 LastChildFill 拉伸按钮。字体、主题和快捷键仅在配置变化时发送；主题更新复用颜色画刷，导航计数只更新对应文本。

密码自定义字段是 Note / NoteVersion 内的有序 id/name/value 列表，参与历史、存档、搜索、清除；新存档 schema 2，读取旧 schema 1。记事本顺序是 notebooks 数组的顺序，移动只重排活跃槽位，不改变已删除记事本位置或记录归属。Android 采用 Reorderable 2.5.1 的 LazyGrid / LazyList，拖动时使用临时顺序、取消恢复、落下后才写入；Windows 将同一个原控件放在顶层 Canvas，原位留空，以固定布局槽位判断落点，并按绘制帧移动。两端没有截图虚影或插入指示线。详见 [拖拽与记录操作](INTERACTIONS.md)。回收站有自己的多选卡片网格，Windows 的矩形选择映射实际项边界、保留 Ctrl / Shift 选择并支持边缘滚动，不挂载正文编辑器。

Windows 应用内控件和弹层统一由 Themes/Controls.xaml 绘制，保留 WPF 原有键盘和无障碍行为。WebView2 右键命令经 ContextMenuRequested 映射到同一菜单样式；复制、粘贴等仍由原生引擎执行，撤销/重做调用共享编辑器。文件选择、保存和打印保留系统标准窗口。通知使用不抢焦点的原生 Popup，定位到当前活动窗口，避免被 WebView 的 HWND 或模态窗口遮挡。Windows 声明 PerMonitorV2 DPI，并使用支持时的 DWM 标题栏色彩。

## 2.3 外观与记录卡片

`assets/themes.json` 是原生与编辑器的唯一语义色表：.NET Core 内嵌读取，Android assets 初始化，编辑器由 Vite 编译同份 JSON。主题模式与浅深配色 ID 分开保存。WPF 共用带绑定的不可冻结画刷，Compose 显式映射所有层级色，WebView 接收配色 ID 并在启动脚本先设置原生提供的正文底色。颜色变化不写正文、不清空撤销、不重建浏览器。

置顶属于单条记录的组织状态，改变 pinned / revision，不改变正文编辑时间或历史；卡片内按钮只重排列表且保留正文选择。副本和右键入口共用实现，保留正文、变量、自定义属性，重置记录身份、历史与置顶状态。

显示字体保存在本机 `Preferences.FontFamily`，不进入 `SyncPayload`。Windows 枚举 `Fonts.SystemFontFamilies` 并通过动态 AppFont 资源更新现有窗口和浮层；Android 解析系统字体配置和公开 SystemFonts，原生 Typeface 与编辑器本地字体资源共用选择。未知字体回退系统默认，不修改正文。

Windows 在 App 构造时关闭 `Switch.System.Windows.Controls.Text.UseAdornerForTextboxSelectionRendering`，选区改为在字形下方绘制；否则 SelectionOpacity=1 会覆盖选中文字。原生验证在实际焦点与全选后检查选区中的字形像素。侧栏页脚使用并排的云存档和设置，记录多选是明确的临时状态，不改变当前正文；安卓删除 / 恢复 / 清除其他记录只清理由操作涉及的当前记录选择。

## 2.6 浏览与后台恢复

记事本入口先显示卡片网格，单击记录进入列表与正文。编辑区左上角的返回按钮回到同一搜索和滚动位置。Windows 网格通过像素滚动虚拟化，只生成可视卡片及两行预取；Android 使用 LazyVerticalGrid。框选、复选框和批量命令保持选择状态，不打开正文；记录剪贴板和拖动共用当前可见记录集合。

过渡仅对左侧记录列表执行一次 170 毫秒位移和透明度变化；不逐帧改变列宽，不复制正文，不对 WebView2 的 HWND 整体透明化。系统关闭动画时尊重系统设置。

Windows 宿主命令改为 PostWebMessageAsJson 与带请求 ID 的应答，不等待 ExecuteScriptAsync 的脚本返回。后台页面的就绪与加载确认使用微任务，不依赖可能被节流的动画帧。普通命令、页面初始化和切换锁均有等待上限。窗口从最小化或长时间失焦恢复后做一次健康探测，不在后台轮询。

超时、渲染进程退出或无响应后，仅重建失效的浏览器，再从宿主已收到的文档恢复当前记录；新建记录的原生标题可以先输入。WebView 回调中的恢复和原生弹窗延后到 Dispatcher，避免回调内重入。会话和空间代次校验拒绝旧浏览器迟到的消息。本机 diagnostics/desktop.jsonl 仅记录固定操作、状态和异常类型，限制大小并异步写入，不记录正文或密码。
