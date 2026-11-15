using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using Moji.Core;

namespace Moji.App;
// Native shell only. All document editing belongs to the bundled upstream Tiptap editor.
internal sealed class SharedEditorView : Grid, IDisposable
{
    internal readonly WebView2 Browser=new();
    private readonly WorkspaceViewModel vm;
    private readonly string noteId;
    private readonly TaskCompletionSource ready=new(TaskCreationOptions.RunContinuationsAsynchronously);
    private bool disposed,loadSent;
    private readonly long epoch;
    private readonly Dictionary<string,TaskCompletionSource> flushing=[];
    public Task Ready=>ready.Task;
    public SharedEditorView(WorkspaceViewModel model,Note note)
    {
        vm=model;noteId=note.Id;epoch=vm.WorkspaceEpoch;Children.Add(Browser);Name="SharedDocumentEditor";
        Loaded+=async(_,_)=>{try{await Initialize(note);}catch(Exception){if(disposed)return;Children.Clear();var retry=Ui.Button("重新加载编辑器",()=>{Children.Clear();Children.Add(Browser);_ = Initialize(note);});Children.Add(Ui.Stack(Ui.Text("编辑器未能加载",16),retry));ready.TrySetException(new InvalidOperationException("编辑器未能加载"));}};
    }
    private async Task Initialize(Note note)
    {
        if(Browser.CoreWebView2!=null)return;
        var folder=EditorFiles.Extract();
        var runtime=await EditorFiles.Runtime();
        var environment=await CoreWebView2Environment.CreateAsync(runtime,Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"MojiNotes.WebView"));
        if(disposed)return;await Browser.EnsureCoreWebView2Async(environment);
        var core=Browser.CoreWebView2!;core.Settings.AreDevToolsEnabled=false;core.Settings.AreBrowserAcceleratorKeysEnabled=false;core.Settings.AreDefaultScriptDialogsEnabled=false;core.Settings.IsStatusBarEnabled=false;core.Settings.IsPasswordAutosaveEnabled=false;core.Settings.IsGeneralAutofillEnabled=false;
        core.SetVirtualHostNameToFolderMapping("moji.editor",folder,CoreWebView2HostResourceAccessKind.DenyCors);
        core.NavigationStarting+=(_,e)=>{if(!e.Uri.StartsWith("https://moji.editor/",StringComparison.Ordinal))e.Cancel=true;};
        core.NewWindowRequested+=(_,e)=>e.Handled=true;
        core.WebMessageReceived+=(_,e)=>{
            if(disposed||vm.WorkspaceEpoch!=epoch||!e.Source.StartsWith("https://moji.editor/",StringComparison.Ordinal))return;
            try{using var data=JsonDocument.Parse(e.WebMessageAsJson);var root=data.RootElement;var type=root.GetProperty("type").GetString();
                if(type=="ready"){_ = Load(note);return;}
                if(!root.TryGetProperty("id",out var id)||id.GetString()!=noteId)return;
                if(type=="loaded"){ready.TrySetResult();return;}
                if(type=="flushed"){if(root.TryGetProperty("token",out var token)&&flushing.Remove(token.GetString()??"",out var pending))pending.TrySetResult();return;}
                if(type=="change"){var content=root.GetProperty("content").GetString()!;if(!EditorContent.IsDocument(content)||content.Length>30_000_000)return;var vars=Json.Deserialize<List<PromptVariable>>(root.GetProperty("variables").GetRawText());if(vars==null||vars.Any(v=>!System.Text.RegularExpressions.Regex.IsMatch(v.Id,"^[a-f0-9]{32}$")||v.Name.Length is <1 or >40))return;var old=vm.State?.Notes.FirstOrDefault(n=>n.Id==noteId);if(old!=null&&(old.Content!=content||!old.Variables.SequenceEqual(vars)))vm.EditNote(noteId,n=>n with{Content=content,Variables=vars});}
                if(type=="openLink"&&Uri.TryCreate(root.GetProperty("url").GetString(),UriKind.Absolute,out var link)&&link.Scheme is "http" or "https")System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(link.AbsoluteUri){UseShellExecute=true});
                if(type=="action")ActionRequested?.Invoke(root.GetProperty("action").GetString()!);
            }catch(JsonException){}
        };
        core.NavigationCompleted+=async(_,e)=>{if(e.IsSuccess&&!disposed)await Load(note);};
        core.Navigate("https://moji.editor/index.html");
    }
    public event Action<string>? ActionRequested;
    private Task<string> Load(Note note)
    {
        if(loadSent)return Task.FromResult("null");loadSent=true;
        var preferences=vm.State!.Preferences;
        var payload=Json.Serialize(new{id=note.Id,content=note.Content,variables=note.Variables,theme=preferences.Theme=="dark"?"dark":"light",fontSize=preferences.FontSize,shortcuts=Shortcuts.Actions.ToDictionary(a=>a.Id,a=>Shortcuts.Binding(preferences,a.Id)),defaults=Shortcuts.Actions.ToDictionary(a=>a.Id,a=>a.Default)});
        return Browser.CoreWebView2.ExecuteScriptAsync("window.MojiEditor?window.MojiEditor.load("+payload+"):window.__mojiLoad="+payload);
    }
    public async Task Command(string command,object? value=null){await Ready;if(disposed)return;await Browser.CoreWebView2.ExecuteScriptAsync("window.MojiEditor.action("+Json.Serialize(command)+","+Json.Serialize(value)+")");}
    public async Task Flush(){if(disposed||!ready.Task.IsCompletedSuccessfully||vm.WorkspaceEpoch!=epoch)return;var token=Guid.NewGuid().ToString("N");var pending=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);flushing[token]=pending;try{await Browser.CoreWebView2.ExecuteScriptAsync("window.MojiEditor.flush("+Json.Serialize(token)+")");await pending.Task.WaitAsync(TimeSpan.FromSeconds(10));}finally{flushing.Remove(token);}}
    public void Dispose(){if(disposed)return;disposed=true;Browser.Dispose();}
}
internal static class EditorFiles
{
    private static readonly SemaphoreSlim runtimeLock=new(1,1);
    private static string Base=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"MojiNotes.Editor","2.0.0");
    public static string Extract()
    {
        var folder=Path.Combine(Base,"assets");Directory.CreateDirectory(folder);var assembly=typeof(EditorFiles).Assembly;
        foreach(var name in assembly.GetManifestResourceNames().Where(n=>n.StartsWith("Moji.Editor."))){var path=Path.Combine(folder,name[12..]);using var input=assembly.GetManifestResourceStream(name)!;using var output=File.Create(path);input.CopyTo(output);}
        return folder;
    }
    public static async Task<string?> Runtime()
    {
        // Prefer a system-maintained current runtime; the bundled fallback works offline.
        if(Environment.GetEnvironmentVariable("MOJI_FORCE_FIXED_WEBVIEW")!="1"){try{if(!string.IsNullOrEmpty(CoreWebView2Environment.GetAvailableBrowserVersionString()))return null;}catch(WebView2RuntimeNotFoundException){}}
        await runtimeLock.WaitAsync();try{
            var folder=Path.Combine(Base,"runtime");var executable=Directory.Exists(folder)?Directory.EnumerateFiles(folder,"msedgewebview2.exe",SearchOption.AllDirectories).FirstOrDefault():null;
            if(executable!=null)return Path.GetDirectoryName(executable);
            using var input=typeof(EditorFiles).Assembly.GetManifestResourceStream("Moji.WebViewRuntime.cab");if(input==null)throw new InvalidOperationException("缺少编辑器运行组件");
            Directory.CreateDirectory(folder);var cab=Path.Combine(Base,"runtime.cab");await using(var file=File.Create(cab))await input.CopyToAsync(file);
            await Task.Run(()=>{var start=new System.Diagnostics.ProcessStartInfo("expand.exe"){UseShellExecute=false,CreateNoWindow=true};start.ArgumentList.Add(cab);start.ArgumentList.Add("-F:*");start.ArgumentList.Add(folder);using var process=System.Diagnostics.Process.Start(start)!;process.WaitForExit();if(process.ExitCode!=0)throw new IOException("无法解压编辑器运行组件");
                var acl=new System.Diagnostics.ProcessStartInfo("icacls.exe"){UseShellExecute=false,CreateNoWindow=true};foreach(var arg in new[]{folder,"/grant","*S-1-15-2-1:(OI)(CI)(RX)","*S-1-15-2-2:(OI)(CI)(RX)","/T","/Q"})acl.ArgumentList.Add(arg);using var permissions=System.Diagnostics.Process.Start(acl)!;permissions.WaitForExit();});
            File.Delete(cab);return Path.GetDirectoryName(Directory.EnumerateFiles(folder,"msedgewebview2.exe",SearchOption.AllDirectories).First());
        }finally{runtimeLock.Release();}
    }
}
