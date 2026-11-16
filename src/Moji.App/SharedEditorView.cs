using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using Moji.Core;

namespace Moji.App;
// One browser per window. Documents switch inside the existing Tiptap instance.
internal sealed class SharedEditorView : Grid, IDisposable
{
    internal readonly WebView2 Browser=new();
    private readonly WorkspaceViewModel vm;
    private TaskCompletionSource pageReady=new(TaskCreationOptions.RunContinuationsAsynchronously);
    private TaskCompletionSource ready=new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly SemaphoreSlim switching=new(1,1);
    private readonly Dictionary<string,TaskCompletionSource> flushing=[];
    private bool disposed,initializing,active;
    private ContextMenu? browserMenu;
    private int request;
    private long epoch=-1;
    private string noteId="",session="",lastContent="",readySession="";
    private JsonElement? selectedRange;
    public Task Ready=>ready.Task;
    public Task Preloaded=>pageReady.Task;
    public Task Preload()=>StartInitialize();
    public string NoteId=>noteId;
    public bool CanConvert {get;private set;}
    public Action<bool>? SelectionChanged {get;set;}
    public event Action<string>? ActionRequested;
    public SharedEditorView(WorkspaceViewModel model)
    {
        vm=model;Name="SharedDocumentEditor";Children.Add(Browser);ApplyBackground();
        Loaded+=async(_,_)=>await StartInitialize();
    }
    private void ApplyBackground(){var hex=Ui.Palette(vm.State?.Preferences??new()).Colors["Panel"];Browser.DefaultBackgroundColor=System.Drawing.ColorTranslator.FromHtml(hex);Background=Ui.Brush(hex);}
    private async Task StartInitialize()
    {
        if(initializing||disposed||Browser.CoreWebView2!=null)return;initializing=true;
        try{
            var folder=EditorFiles.Extract();var runtime=await EditorFiles.Runtime();if(disposed)return;
            Environment.SetEnvironmentVariable("WEBVIEW2_DEFAULT_BACKGROUND_COLOR","FF"+Ui.Palette(vm.State?.Preferences??new()).Colors["Panel"][1..]);
            var environment=await CoreWebView2Environment.CreateAsync(runtime,Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"MojiNotes.WebView"));
            if(disposed)return;await Browser.EnsureCoreWebView2Async(environment);var core=Browser.CoreWebView2!;
            core.Settings.AreDevToolsEnabled=false;core.Settings.AreBrowserAcceleratorKeysEnabled=false;core.Settings.AreDefaultScriptDialogsEnabled=false;core.Settings.IsStatusBarEnabled=false;core.Settings.IsPasswordAutosaveEnabled=false;core.Settings.IsGeneralAutofillEnabled=false;
            core.SetVirtualHostNameToFolderMapping("moji.editor",folder,CoreWebView2HostResourceAccessKind.DenyCors);
            core.NavigationStarting+=(_,e)=>{if(!e.Uri.StartsWith("https://moji.editor/",StringComparison.Ordinal))e.Cancel=true;};core.NewWindowRequested+=(_,e)=>e.Handled=true;
            core.ContextMenuRequested+=(_,e)=>{browserMenu=DesktopNativeUi.BrowserMenu(e,Browser,action=>_ = Command(action));if(browserMenu.Items.Count>0)browserMenu.IsOpen=true;};
            core.NavigationCompleted+=(_,e)=>{if(!e.IsSuccess&&!disposed&&e.WebErrorStatus!=CoreWebView2WebErrorStatus.OperationCanceled)LoadFailed(new IOException("编辑器页面未能加载"));};
            core.WebMessageReceived+=(_,e)=>Receive(e);var hash=Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(Path.Combine(folder,"index.html"))));core.Navigate("https://moji.editor/index.html?v="+hash+"&theme="+(Ui.IsDark(vm.State?.Preferences.Theme)?"dark":"light")+"&bg="+Ui.Palette(vm.State?.Preferences??new()).Colors["Panel"][1..]);
        }catch(Exception error){if(disposed)return;LoadFailed(error);}
        finally{initializing=false;}
    }
    private void LoadFailed(Exception error)
    {
        if(pageReady.Task.IsCompleted)pageReady=new(TaskCreationOptions.RunContinuationsAsynchronously);pageReady.TrySetException(error);ready.TrySetException(error);Children.Clear();var message=error is EditorRuntimeUnavailableException?error.Message:"编辑器未能加载，请重试";
        Children.Add(Ui.Stack(Ui.Text(message,15),Ui.Button("重试",()=>{pageReady=new(TaskCreationOptions.RunContinuationsAsynchronously);Children.Clear();Children.Add(Browser);if(Browser.CoreWebView2!=null)Browser.CoreWebView2.Reload();else _ = StartInitialize();ActionRequested?.Invoke("retry");})));
    }
    private void Receive(CoreWebView2WebMessageReceivedEventArgs e)
    {
        if(disposed||!e.Source.StartsWith("https://moji.editor/",StringComparison.Ordinal))return;
        try{
            using var data=JsonDocument.Parse(e.WebMessageAsJson);var root=data.RootElement;var type=root.GetProperty("type").GetString();
            if(type=="ready"){pageReady.TrySetResult();return;}
            if(type=="flushed"){if(root.TryGetProperty("token",out var t)&&flushing.Remove(t.GetString()??"",out var pending))pending.TrySetResult();return;}
            if(vm.WorkspaceEpoch!=epoch||!root.TryGetProperty("id",out var id)||id.GetString()!=noteId||!root.TryGetProperty("session",out var token)||token.GetString()!=session)return;
            if(type=="loaded"){if(session==readySession)ready.TrySetResult();return;}
            if(type=="selection"){CanConvert=active&&readySession==session&&ready.Task.IsCompletedSuccessfully&&root.GetProperty("canConvert").GetBoolean();selectedRange=CanConvert?root.GetProperty("selection").Clone():null;SelectionChanged?.Invoke(CanConvert);return;}
            if(type=="change"){
                var content=root.GetProperty("content").GetString()!;if(!EditorContent.IsDocument(content)||content.Length>30_000_000)return;
                var vars=Json.Deserialize<List<PromptVariable>>(root.GetProperty("variables").GetRawText());if(vars==null||vars.Any(v=>!System.Text.RegularExpressions.Regex.IsMatch(v.Id,"^[a-f0-9]{32}$")||v.Name.Length is <1 or >2000))return;
                var old=vm.State?.Notes.FirstOrDefault(n=>n.Id==noteId);if(old==null||old.PurgedAt!=null)return;
                lastContent=content;if(old.Content!=content||!old.Variables.SequenceEqual(vars))vm.EditNote(noteId,n=>n with{Content=content,Variables=vars});return;
            }
            if(!active)return;
            if(type=="openLink"&&Uri.TryCreate(root.GetProperty("url").GetString(),UriKind.Absolute,out var link)&&link.Scheme is "http" or "https")System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(link.AbsoluteUri){UseShellExecute=true});
            if(type=="action")ActionRequested?.Invoke(root.GetProperty("action").GetString()!);
        }catch(JsonException){}
    }
    private string lastAppearance="";
    public Task RefreshAppearance()
    {
        if(vm.State==null||disposed)return Task.CompletedTask;var p=vm.State.Preferences;var configuration=Json.Serialize(new{theme=Ui.IsDark(p.Theme)?"dark":"light",palette=Ui.Palette(p).Id,fontSize=p.FontSize,shortcuts=Shortcuts.Actions.ToDictionary(a=>a.Id,a=>Shortcuts.Binding(p,a.Id)),defaults=Shortcuts.Actions.ToDictionary(a=>a.Id,a=>a.Default)});
        if(configuration==lastAppearance)return Task.CompletedTask;lastAppearance=configuration;ApplyBackground();return Browser.CoreWebView2!=null&&pageReady.Task.IsCompletedSuccessfully?Browser.CoreWebView2.ExecuteScriptAsync("window.MojiEditor?.configure("+configuration+")"):Task.CompletedTask;
    }
    public async Task Show(Note note)
    {
        var p=vm.State!.Preferences;
        var configuration=Json.Serialize(new{theme=Ui.IsDark(p.Theme)?"dark":"light",palette=Ui.Palette(p).Id,fontSize=p.FontSize,shortcuts=Shortcuts.Actions.ToDictionary(a=>a.Id,a=>Shortcuts.Binding(p,a.Id)),defaults=Shortcuts.Actions.ToDictionary(a=>a.Id,a=>a.Default)});
        if(note.Id==noteId&&epoch==vm.WorkspaceEpoch&&note.Content==lastContent&&ready.Task.IsCompletedSuccessfully){active=true;ApplyBackground();await Browser.CoreWebView2.ExecuteScriptAsync("window.MojiEditor.configure("+configuration+");window.MojiEditor.activate()");return;}
        var version=++request;var external=note.Id==noteId&&note.Content!=lastContent;
        var completion=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);ready.TrySetCanceled();ready=completion;readySession="";active=true;CanConvert=false;SelectionChanged?.Invoke(false);ApplyBackground();
        try{await pageReady.Task;}catch(OperationCanceledException){return;}await switching.WaitAsync();
        try{
            if(disposed||version!=request)return;
            if(external){session="";}else await FlushCurrent();
            if(disposed||version!=request)return;
            note=vm.State?.Notes.FirstOrDefault(n=>n.Id==note.Id)??note;noteId=note.Id;epoch=vm.WorkspaceEpoch;session=Guid.NewGuid().ToString("N");readySession=session;lastContent=note.Content;
            var payload=Json.Serialize(new{id=note.Id,session,content=note.Content,variables=note.Variables,theme=Ui.IsDark(p.Theme)?"dark":"light",palette=Ui.Palette(p).Id,fontSize=p.FontSize,shortcuts=Shortcuts.Actions.ToDictionary(a=>a.Id,a=>Shortcuts.Binding(p,a.Id)),defaults=Shortcuts.Actions.ToDictionary(a=>a.Id,a=>a.Default)});
            await Browser.CoreWebView2.ExecuteScriptAsync("window.MojiEditor.load("+payload+")");
        }finally{switching.Release();}
    }
    public void Deactivate(){active=false;CanConvert=false;SelectionChanged?.Invoke(false);if(Browser.CoreWebView2!=null)_ = Browser.CoreWebView2.ExecuteScriptAsync("window.MojiEditor.deactivate()");}
    public void ClearSelectionAvailability(){selectedRange=null;CanConvert=false;SelectionChanged?.Invoke(false);}
    public async Task Command(string command,object? value=null){if(command=="convertVariable"){if(!CanConvert)return;value=selectedRange;}var target=request;try{await Ready;}catch(OperationCanceledException){return;}if(disposed||!active||target!=request)return;Browser.Focus();await Browser.CoreWebView2.ExecuteScriptAsync("window.MojiEditor.action("+Json.Serialize(command)+","+Json.Serialize(value)+")");}
    private async Task FlushCurrent(){if(disposed||!pageReady.Task.IsCompletedSuccessfully||noteId==""||epoch!=vm.WorkspaceEpoch)return;var token=Guid.NewGuid().ToString("N");var pending=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);flushing[token]=pending;try{await Browser.CoreWebView2.ExecuteScriptAsync("window.MojiEditor.flush("+Json.Serialize(token)+")");await pending.Task.WaitAsync(TimeSpan.FromSeconds(10));}finally{flushing.Remove(token);}}
    public Task Flush()=>FlushCurrent();
    public void Dispose(){if(disposed)return;if(browserMenu!=null)browserMenu.IsOpen=false;disposed=true;pageReady.TrySetCanceled();ready.TrySetCanceled();Browser.Dispose();}
}
internal static class EditorFiles
{
    private static readonly SemaphoreSlim runtimeLock=new(1,1);
    private static int forcedBootstrapUsed;
    public static int BootstrapperInvocations { get; private set; }
    public static bool HasBootstrapper=>typeof(EditorFiles).Assembly.GetManifestResourceNames().Contains("Moji.WebViewBootstrapper.exe");
    public static bool HasFixedRuntime=>typeof(EditorFiles).Assembly.GetManifestResourceNames().Contains("Moji.WebViewRuntime.cab");
    private static string Base=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"MojiNotes.Editor","2.3.0");
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
