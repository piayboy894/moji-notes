using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using Moji.Core;

namespace Moji.App;

// Keep one view during ordinary navigation. Only a failed renderer is replaced.
internal sealed class SharedEditorView : Grid, IDisposable
{
    internal WebView2 Browser { get; private set; } = new();
    private readonly WorkspaceViewModel vm;
    private readonly SemaphoreSlim switching = new(1, 1);
    private readonly Dictionary<string, TaskCompletionSource> pending = [];
    private TaskCompletionSource pageReady = NewCompletion(), ready = NewCompletion();
    private Task? initialization;
    private bool disposed, active, needsRecovery, recoveryQueued;
    private ContextMenu? browserMenu;
    private int request, generation;
    private long epoch = -1;
    private string noteId = "", requestedId = "", session = "", lastContent = "", lastAppearance = "";
    private JsonElement? selectedRange;
    public Task Ready => ready.Task;
    public Task Preloaded => pageReady.Task;
    public Task Preload() => Initialize();
    public string NoteId => noteId;
    public bool CanConvert { get; private set; }
    public Action<bool>? SelectionChanged { get; set; }
    public event Action<string>? ActionRequested;
    private static TaskCompletionSource NewCompletion() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    public SharedEditorView(WorkspaceViewModel model)
    {
        vm = model; Name = "SharedDocumentEditor"; Children.Add(Browser); ApplyBackground();
        Loaded += async (_, _) => { try { await Initialize(); } catch (Exception error) { if (!disposed) ShowFailure(error); } };
    }
    private void ApplyBackground()
    {
        var hex = Ui.Palette(vm.State?.Preferences ?? new()).Colors["Panel"];
        Browser.DefaultBackgroundColor = System.Drawing.ColorTranslator.FromHtml(hex); Background = Ui.Brush(hex);
    }
    private Task Initialize() => initialization ??= InitializeCore();
    private async Task InitializeCore()
    {
        var browser = Browser; var ownGeneration = generation;
        try
        {
            var folder = await Task.Run(EditorFiles.Extract); var runtime = await EditorFiles.Runtime();
            if (disposed || generation != ownGeneration) return;
            Environment.SetEnvironmentVariable("WEBVIEW2_DEFAULT_BACKGROUND_COLOR", "FF" + Ui.Palette(vm.State?.Preferences ?? new()).Colors["Panel"][1..]);
            var environment = await CoreWebView2Environment.CreateAsync(runtime, Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MojiNotes.WebView")).WaitAsync(TimeSpan.FromSeconds(25));
            if (disposed || generation != ownGeneration) return;
            await browser.EnsureCoreWebView2Async(environment).WaitAsync(TimeSpan.FromSeconds(25));
            if(disposed||generation!=ownGeneration)return;var core = browser.CoreWebView2;
            core.Settings.AreDevToolsEnabled = false; core.Settings.AreBrowserAcceleratorKeysEnabled = false;
            core.Settings.AreDefaultScriptDialogsEnabled = false; core.Settings.IsStatusBarEnabled = false;
            core.Settings.IsPasswordAutosaveEnabled = false; core.Settings.IsGeneralAutofillEnabled = false;
            core.SetVirtualHostNameToFolderMapping("moji.editor", folder, CoreWebView2HostResourceAccessKind.DenyCors);
            core.NavigationStarting += (_, e) => { if (!e.Uri.StartsWith("https://moji.editor/", StringComparison.Ordinal)) e.Cancel = true; };
            core.NewWindowRequested += (_, e) => e.Handled = true;
            core.ContextMenuRequested += (_, e) => { browserMenu = DesktopNativeUi.BrowserMenu(e, browser, action => _ = Command(action)); if (browserMenu.Items.Count > 0) browserMenu.IsOpen = true; };
            core.NavigationCompleted += (_, e) => { if (!e.IsSuccess && e.WebErrorStatus != CoreWebView2WebErrorStatus.OperationCanceled && generation == ownGeneration) FailTransport(new IOException("编辑器页面未能加载")); };
            core.WebMessageReceived += (_, e) => Receive(e, ownGeneration);
            core.ProcessFailed += (_, e) =>
            {
                if (disposed || generation != ownGeneration) return;
                AppDiagnostics.Write("editor-process", e.ProcessFailedKind.ToString());
                if (e.ProcessFailedKind is CoreWebView2ProcessFailedKind.RenderProcessExited or CoreWebView2ProcessFailedKind.RenderProcessUnresponsive or CoreWebView2ProcessFailedKind.BrowserProcessExited)
                {
                    FailTransport(new IOException("编辑组件需要恢复"));
                    // WebView2 callbacks must finish before disposing/recreating a view.
                    Dispatcher.BeginInvoke(new Action(() => QueueRecovery()));
                }
            };
            var hash = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(Path.Combine(folder, "index.html"))));
            core.Navigate("https://moji.editor/index.html?v=" + hash + "&theme=" + (Ui.IsDark(vm.State?.Preferences.Theme) ? "dark" : "light") + "&bg=" + Ui.Palette(vm.State?.Preferences ?? new()).Colors["Panel"][1..]);
        }
        catch (Exception error) { if (generation == ownGeneration && !disposed) FailTransport(error); throw; }
    }
    private void FailTransport(Exception error)
    {
        needsRecovery = true; ClearSelectionAvailability(); pageReady.TrySetException(error);
        foreach (var completion in pending.Values) completion.TrySetException(error);
        pending.Clear();
    }
    private void Receive(CoreWebView2WebMessageReceivedEventArgs e, int ownGeneration)
    {
        if (disposed || generation != ownGeneration || !e.Source.StartsWith("https://moji.editor/", StringComparison.Ordinal)) return;
        try
        {
            using var data = JsonDocument.Parse(e.WebMessageAsJson); var root = data.RootElement; var type = root.GetProperty("type").GetString();
            if (type == "ready") { pageReady.TrySetResult(); return; }
            if (type == "reply")
            {
                if (root.TryGetProperty("request", out var token) && pending.Remove(token.GetString() ?? "", out var completion))
                { if (root.GetProperty("ok").GetBoolean()) completion.TrySetResult(); else completion.TrySetException(new IOException("编辑器操作未完成")); }
                return;
            }
            if (vm.WorkspaceEpoch != epoch || !root.TryGetProperty("id", out var id) || id.GetString() != noteId || !root.TryGetProperty("session", out var documentSession) || documentSession.GetString() != session) return;
            if (type == "selection") { CanConvert = active && ready.Task.IsCompletedSuccessfully && root.GetProperty("canConvert").GetBoolean(); selectedRange = CanConvert ? root.GetProperty("selection").Clone() : null; SelectionChanged?.Invoke(CanConvert); return; }
            if (type == "change")
            {
                var content = root.GetProperty("content").GetString()!; if (!EditorContent.IsDocument(content) || content.Length > 30_000_000) return;
                var vars = Json.Deserialize<List<PromptVariable>>(root.GetProperty("variables").GetRawText());
                if (vars == null || vars.Any(v => v == null || v.Id == null || v.Name == null || !System.Text.RegularExpressions.Regex.IsMatch(v.Id, "^[a-f0-9]{32}$") || v.Name.Length is < 1 or > 2000)) return;
                var old = vm.State?.Notes.FirstOrDefault(n => n.Id == noteId); if (old == null || old.PurgedAt != null) return;
                lastContent = content; if (old.Content != content || !old.Variables.SequenceEqual(vars)) vm.EditNote(noteId, n => n with { Content = content, Variables = vars }); return;
            }
            if (!active) return;
            if (type == "openLink" && Uri.TryCreate(root.GetProperty("url").GetString(), UriKind.Absolute, out var link) && link.Scheme is "http" or "https")
                Dispatcher.BeginInvoke(new Action(() => System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(link.AbsoluteUri) { UseShellExecute = true })));
            if (type == "action") { var action = root.GetProperty("action").GetString()!;var fromSession=session;var fromEpoch=epoch;Dispatcher.BeginInvoke(new Action(() => { if (!disposed&&active&&session==fromSession&&epoch==fromEpoch) ActionRequested?.Invoke(action); })); }
        }
        catch (Exception error) when(error is JsonException or InvalidOperationException or KeyNotFoundException or ArgumentException) { AppDiagnostics.Write("editor-message",error.GetType().Name); }
    }
    private string Configuration()
    {
        var p = vm.State?.Preferences ?? new();
        return Json.Serialize(new { theme = Ui.IsDark(p.Theme) ? "dark" : "light", palette = Ui.Palette(p).Id, fontSize = p.FontSize, fontFamily = InstalledFonts.CssFamily(p.FontFamily), shortcuts = Shortcuts.Actions.Where(a => !a.Id.StartsWith("record")).ToDictionary(a => a.Id, a => Shortcuts.Binding(p, a.Id)), defaults = Shortcuts.Actions.Where(a => !a.Id.StartsWith("record")).ToDictionary(a => a.Id, a => a.Default) });
    }
    private async Task Send(string command, object? value = null, TimeSpan? timeout = null, Action? posting = null)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        var token = Guid.NewGuid().ToString("N"); var completion = NewCompletion(); pending[token] = completion;
        try
        {
            var core = Browser.CoreWebView2 ?? throw new IOException("编辑器尚未准备好");
            if (core.IsSuspended) core.Resume();
            // Posting a message does not await a renderer script's COM result.
            var message = command == "load" ? await Task.Run(()=>Json.Serialize(new { protocol = "moji", request = token, command, value })) : Json.Serialize(new { protocol = "moji", request = token, command, value });
            if(disposed||!pending.ContainsKey(token))throw new OperationCanceledException();
            posting?.Invoke();
            core.PostWebMessageAsJson(message);
            await completion.Task.WaitAsync(timeout ?? TimeSpan.FromSeconds(5));
        }
        catch (TimeoutException) { AppDiagnostics.Write("editor-timeout", command); needsRecovery = true; throw; }
        finally { pending.Remove(token); }
    }
    private async Task EnsurePage()
    { await Initialize(); await pageReady.Task.WaitAsync(TimeSpan.FromSeconds(25)); }
    private async Task Load(Note note)
    {
        var config = Json.Deserialize<Dictionary<string, JsonElement>>(Configuration());
        var documentSession=Guid.NewGuid().ToString("N");var documentEpoch=vm.WorkspaceEpoch;
        var payload = new Dictionary<string, object?> { ["id"] = note.Id, ["session"] = documentSession, ["content"] = note.Content, ["variables"] = note.Variables };
        foreach (var (key, value) in config) payload[key] = value;
        await Send("load", payload, TimeSpan.FromSeconds(25),()=>{noteId=note.Id;epoch=documentEpoch;session=documentSession;lastContent=note.Content;});lastAppearance = Configuration();
        if (!active) await Send("deactivate");
    }
    private async Task Recover()
    {
        if (!needsRecovery) return;
        var target = vm.State?.Notes.Any(n=>n.Id==requestedId&&n.PurgedAt==null)==true?requestedId:noteId;var wasActive = vm.SelectedId==target; var old = Browser;
        if(ready.Task.IsFaulted||ready.Task.IsCanceled)ready=NewCompletion();
        generation++; initialization = null; pageReady.TrySetCanceled(); pageReady = NewCompletion();
        foreach (var completion in pending.Values) completion.TrySetException(new IOException("编辑组件正在恢复")); pending.Clear();
        if (browserMenu != null) browserMenu.IsOpen = false;
        Children.Clear(); try { old.Dispose(); } catch (System.Runtime.InteropServices.COMException) { } Browser = new(); Children.Add(Browser); ApplyBackground();
        var status = Ui.Text("正在恢复编辑器…", 13, "Sub"); status.HorizontalAlignment = HorizontalAlignment.Center;
        var overlay = new Border { Background = Background, Child = status }; Children.Add(overlay);
        try
        {
            needsRecovery = false; await EnsurePage();
            if (epoch == vm.WorkspaceEpoch && vm.State?.Notes.FirstOrDefault(n => n.Id == target && n.PurgedAt == null) is { } note)
            { active = wasActive; await Load(note); if (requestedId == target) ready.TrySetResult(); }
            else { noteId = ""; session = ""; }
            Children.Remove(overlay); AppDiagnostics.Write("editor-recovered", "complete");
        }
        catch { needsRecovery = true; throw; }
    }
    private static bool Recoverable(Exception error) => error is not (EditorRuntimeUnavailableException or OperationCanceledException or ObjectDisposedException);
    private async Task WithRecovery(Func<Task> action)
    {
        // Initial component installation may take time; it must not own the
        // document-switch gate while later navigation requests are coalescing.
        try { await Initialize(); } catch(Exception error) when(!disposed&&Recoverable(error)) { needsRecovery=true; }
        using var waiting = new CancellationTokenSource(TimeSpan.FromSeconds(35));
        await switching.WaitAsync(waiting.Token);
        try
        {
            for (var attempt = 0; ; attempt++)
            {
                try { if (needsRecovery) await Recover(); await EnsurePage(); await action(); return; }
                catch (Exception error) when (attempt == 0 && !disposed && Recoverable(error)) { needsRecovery = true; await Recover(); }
            }
        }
        finally { switching.Release(); }
    }
    private void QueueRecovery()
    {
        if (disposed || recoveryQueued) return; recoveryQueued = true;
        _ = Run();
        async Task Run() { try { await WithRecovery(() => Task.CompletedTask); } catch (Exception error) { if (!disposed) ShowFailure(error); } finally { recoveryQueued = false; } }
    }
    internal Task ResumeAsync() => WithRecovery(async () => { await Send("ping", timeout: TimeSpan.FromSeconds(3)); });
    internal bool IsCurrent(Note note)=>!needsRecovery&&epoch==vm.WorkspaceEpoch&&noteId==note.Id&&lastContent==note.Content&&ready.Task.IsCompletedSuccessfully;
    public async Task Show(Note note)
    {
        if(IsCurrent(note)&&active&&lastAppearance==Configuration())return;
        var version = ++request; requestedId = note.Id; var completion = NewCompletion(); ready.TrySetCanceled(); ready = completion;
        ClearSelectionAvailability(); ApplyBackground();
        try
        {
            await WithRecovery(async () =>
            {
                if (version != request || disposed) return;
                if (note.Id == noteId && epoch == vm.WorkspaceEpoch && note.Content == lastContent)
                {
                    if (!active || lastAppearance != Configuration()) { await Send("configure", Json.Deserialize<JsonElement>(Configuration())); await Send("activate"); lastAppearance = Configuration(); }
                    active = true; return;
                }
                if(noteId!="")await Send("deactivate");
                if (note.Id != noteId || note.Content == lastContent) await FlushCurrent();
                if (version != request || disposed) return;
                note = vm.State?.Notes.FirstOrDefault(n => n.Id == note.Id) ?? note; active = true; await Load(note);
            });
            if (version == request && !disposed) completion.TrySetResult(); else completion.TrySetCanceled();
        }
        catch (Exception error) { completion.TrySetException(error); throw; }
    }
    public Task RefreshAppearance() => Refresh();
    private async Task Refresh()
    {
        if (vm.State == null || disposed || !pageReady.Task.IsCompletedSuccessfully || needsRecovery || lastAppearance == Configuration()) return;
        try { await WithRecovery(async () => { var config = Configuration(); await Send("configure", Json.Deserialize<JsonElement>(config)); lastAppearance = config; ApplyBackground(); }); }
        catch (Exception error) { if (!disposed) { needsRecovery = true; AppDiagnostics.Write("editor-appearance", error.GetType().Name); } }
    }
    public void Deactivate()
    {
        active = false; ClearSelectionAvailability(); var ownSession = session;
        if (!disposed && pageReady.Task.IsCompletedSuccessfully) _ = SendDeactivate();
        async Task SendDeactivate() { try { await WithRecovery(async () => { if (!active && ownSession == session) await Send("deactivate"); }); } catch (Exception error) { AppDiagnostics.Write("editor-deactivate", error.GetType().Name); } }
    }
    public void ClearSelectionAvailability() { selectedRange = null; CanConvert = false; SelectionChanged?.Invoke(false); }
    public async Task Command(string command, object? value = null)
    {
        if (command == "convertVariable") { if (!CanConvert) return; value = selectedRange; }
        var target = request;
        try { await Ready.WaitAsync(TimeSpan.FromSeconds(35)); if (disposed || !active || target != request) return; await WithRecovery(async () => { Browser.Focus(); await Send("action", new { name = command, argument = value }); }); }
        catch (OperationCanceledException) { }
        catch (Exception error) { if (!disposed) { AppDiagnostics.Write("editor-command", error.GetType().Name); ShowFailure(error); } }
    }
    private Task FlushCurrent() => noteId == "" || epoch != vm.WorkspaceEpoch ? Task.CompletedTask : Send("flush");
    public Task Flush() => disposed || noteId=="" || epoch!=vm.WorkspaceEpoch ? Task.CompletedTask : WithRecovery(FlushCurrent);
    private void ShowFailure(Exception error)
    {
        AppDiagnostics.Write("editor-unavailable", error.GetType().Name); Children.Clear();
        var message = error is EditorRuntimeUnavailableException ? error.Message : "编辑器暂时不可用，已收到的内容保留在本机";
        var content = Ui.Stack(Ui.Text(message, 14), Ui.Button("重试", () => { needsRecovery = true; QueueRecovery(); }));
        content.HorizontalAlignment = HorizontalAlignment.Center; content.VerticalAlignment = VerticalAlignment.Center; Children.Add(content);
    }
    public void Dispose()
    {
        if (disposed) return; disposed = true; generation++; if (browserMenu != null) browserMenu.IsOpen = false;
        pageReady.TrySetCanceled(); ready.TrySetCanceled(); foreach (var completion in pending.Values) completion.TrySetCanceled(); pending.Clear(); Browser.Dispose();
    }
}

internal static class EditorFiles
{
    private static readonly SemaphoreSlim runtimeLock=new(1,1);
    private static int forcedBootstrapUsed;
    public static int BootstrapperInvocations { get; private set; }
    public static bool HasBootstrapper=>typeof(EditorFiles).Assembly.GetManifestResourceNames().Contains("Moji.WebViewBootstrapper.exe");
    public static bool HasFixedRuntime=>typeof(EditorFiles).Assembly.GetManifestResourceNames().Contains("Moji.WebViewRuntime.cab");
    private static string Base=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"MojiNotes.Editor","2.4.0");
    public static string Extract()
    {
        var folder=Path.Combine(Base,"assets");Directory.CreateDirectory(folder);var assembly=typeof(EditorFiles).Assembly;
        foreach(var name in assembly.GetManifestResourceNames().Where(n=>n.StartsWith("Moji.Editor."))){var path=Path.Combine(folder,name[12..]);using var input=assembly.GetManifestResourceStream(name)!;using var output=File.Create(path);input.CopyTo(output);}
        return folder;
    }
    private static bool SharedRuntimeAvailable(){try{return !string.IsNullOrEmpty(CoreWebView2Environment.GetAvailableBrowserVersionString());}catch(WebView2RuntimeNotFoundException){return false;}}
    public static async Task<string?> Runtime(Action<string>? progress=null)
    {
        var forceFixed=Environment.GetEnvironmentVariable("MOJI_FORCE_FIXED_WEBVIEW")=="1";
        var forceBootstrap=Environment.GetEnvironmentVariable("MOJI_FORCE_WEBVIEW_BOOTSTRAP")=="1"&&Interlocked.Exchange(ref forcedBootstrapUsed,1)==0;
        if(!forceFixed&&!forceBootstrap&&SharedRuntimeAvailable())return null;
        await runtimeLock.WaitAsync();try{
            if(!forceFixed&&!forceBootstrap&&SharedRuntimeAvailable())return null;
            using var input=typeof(EditorFiles).Assembly.GetManifestResourceStream("Moji.WebViewRuntime.cab");
            if(input==null){
                if(forceFixed)throw new EditorRuntimeUnavailableException("此包不包含离线组件，请使用完整离线包");
                progress?.Invoke("正在安装编辑组件…");await InstallSharedRuntime();return null;
            }
            var folder=Path.Combine(Base,"runtime");var executable=Directory.Exists(folder)?Directory.EnumerateFiles(folder,"msedgewebview2.exe",SearchOption.AllDirectories).FirstOrDefault():null;
            if(executable!=null)return Path.GetDirectoryName(executable);
            Directory.CreateDirectory(folder);var cab=Path.Combine(Base,"runtime.cab");await using(var file=File.Create(cab))await input.CopyToAsync(file);
            await Task.Run(()=>{var start=new System.Diagnostics.ProcessStartInfo("expand.exe"){UseShellExecute=false,CreateNoWindow=true};start.ArgumentList.Add(cab);start.ArgumentList.Add("-F:*");start.ArgumentList.Add(folder);using var process=System.Diagnostics.Process.Start(start)!;process.WaitForExit();if(process.ExitCode!=0)throw new IOException("无法解压编辑器运行组件");
                var acl=new System.Diagnostics.ProcessStartInfo("icacls.exe"){UseShellExecute=false,CreateNoWindow=true};foreach(var arg in new[]{folder,"/grant","*S-1-15-2-1:(OI)(CI)(RX)","*S-1-15-2-2:(OI)(CI)(RX)","/T","/Q"})acl.ArgumentList.Add(arg);using var permissions=System.Diagnostics.Process.Start(acl)!;permissions.WaitForExit();});
            File.Delete(cab);return Path.GetDirectoryName(Directory.EnumerateFiles(folder,"msedgewebview2.exe",SearchOption.AllDirectories).First());
        }finally{runtimeLock.Release();}
    }
    private static async Task InstallSharedRuntime()
    {
        using var setup=typeof(EditorFiles).Assembly.GetManifestResourceStream("Moji.WebViewBootstrapper.exe");
        if(setup==null)throw new EditorRuntimeUnavailableException("缺少编辑组件，请安装 Microsoft WebView2，或使用完整离线包");
        var folder=Path.Combine(Base,"setup");Directory.CreateDirectory(folder);var path=Path.Combine(folder,Guid.NewGuid().ToString("N")+".exe");
        try{
            await using(var output=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None,81920,true))await setup.CopyToAsync(output);
            // The official Microsoft-signed bootstrapper is verified before it is
            // embedded. Keep its freshly extracted file locked against writes.
            using var verified=File.Open(path,FileMode.Open,FileAccess.Read,FileShare.Read);
            var start=new System.Diagnostics.ProcessStartInfo(path){UseShellExecute=false,CreateNoWindow=true};start.ArgumentList.Add("/silent");start.ArgumentList.Add("/install");
            using var process=System.Diagnostics.Process.Start(start)??throw new IOException("无法启动安装程序");BootstrapperInvocations++;
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromMinutes(5));
            for(var i=0;i<30;i++){if(SharedRuntimeAvailable())return;await Task.Delay(1000);}
            throw new EditorRuntimeUnavailableException("编辑组件安装未完成，请联网重试，或使用完整离线包");
        }catch(EditorRuntimeUnavailableException){throw;}
        catch(Exception error)when(error is IOException or TimeoutException or System.ComponentModel.Win32Exception or UnauthorizedAccessException){throw new EditorRuntimeUnavailableException("编辑组件安装未完成，请联网重试，或使用完整离线包");}
        finally{try{File.Delete(path);}catch(IOException){}catch(UnauthorizedAccessException){}}
    }
}
internal sealed class EditorRuntimeUnavailableException(string message):Exception(message);
