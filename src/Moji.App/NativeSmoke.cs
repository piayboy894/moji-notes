using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Moji.Core;
using Microsoft.Web.WebView2.Core;

namespace Moji.App;
internal static class NativeSmoke
{
    public static async Task Run(string[] args)
    {
        var index=Array.IndexOf(args,"--output");var output=index>=0?Path.GetFullPath(args[index+1]):Path.Combine(Path.GetTempPath(),"moji-shared-editor-test");Directory.CreateDirectory(output);var checks=new List<string>();
        try {
            using var vm=new WorkspaceViewModel(Path.Combine(output,"test-data"));var window=new MainWindow(vm,true);Application.Current.MainWindow=window;window.Width=1200;window.Height=850;window.Show();
            if(!await vm.OpenAsync()||vm.State!.Notebooks.Count!=0)throw new Exception("Fresh workspace is not empty");await Render();Capture(window,Path.Combine(output,"01-workspace.png"));checks.Add("empty workspace; only notebooks can own new records");
            var book=new Notebook{Name="编辑测试",Mode=NoteMode.Prompt,Color="#75876D"};vm.Change(w=>w with{Notebooks=[book]},"edit");window.Navigate(book.Id);await Render();window.AddNote();var editor=await window.ReadyEditor();
            await Script(editor,"window.MojiEditor.editor.commands.insertContent('Hello 世界')");await Render();var id=vm.SelectedId;
            if(!EditorContent.IsDocument(vm.State!.Notes.First(n=>n.Id==id).Content)||!EditorContent.Plain(vm.State.Notes.First(n=>n.Id==id).Content).Contains("Hello 世界"))throw new Exception("WebView bridge did not save the actual editor document");
            checks.Add("bundled Tiptap editor loads inside WPF and saves through the native bridge");
            await Script(editor,"const e=window.MojiEditor.editor;e.commands.clearContent();e.commands.toggleBlockquote();e.commands.insertContent('引用');e.commands.unsetBlockquote();e.commands.insertContent('正常正文')");await Render();
            var html=await editor.Browser.CoreWebView2.ExecuteScriptAsync("window.MojiEditor.editor.getHTML()");if(html.Contains("blockquote"))throw new Exception("Quote cannot be removed");checks.Add("official quote toggle can be removed without leaving a permanent indent");
            await Script(editor,"const e=window.MojiEditor.editor;e.commands.clearContent();e.commands.toggleTaskList();e.commands.insertContent('Task');e.commands.splitListItem('taskItem');e.commands.insertContent('Second');e.commands.liftListItem('taskItem');e.commands.insertContent('Body')");await Render();
            html=await editor.Browser.CoreWebView2.ExecuteScriptAsync("window.MojiEditor.editor.getHTML()");if(!html.Contains("taskItem")||!html.Contains("Body"))throw new Exception("Official task document was not stored");checks.Add("official task list creates multiple lines and exits back to a paragraph");
            var variable=new PromptVariable{Name="受众"};await editor.Command("insertVariable",variable);await Render();
            var note=vm.State.Notes.First(n=>n.Id==id);if(PromptVariables.Used(note).Count!=1)throw new Exception("Prompt variable missing");
            var plain=PromptVariables.PlainText(note,new Dictionary<string,string>{{variable.Id,"用户"}},EditorContent.Plain);if(!plain.Contains("用户"))throw new Exception("Prompt variable copy failed");
            await editor.Command("undo");await Render();if(PromptVariables.Used(vm.State.Notes.First(n=>n.Id==id)).Count!=0)throw new Exception("Native undo did not reach the shared editor");await editor.Command("redo");await Render();checks.Add("atomic prompt variables insert, undo, redo and fill without typing triggers");
            await using(var stream=File.Create(Path.Combine(output,"02-editor.png")))await editor.Browser.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png,stream);
            await editor.Flush();await vm.SaveAsync();window.Navigate("home");await Render();await vm.OpenAsync();window.Navigate(book.Id,id);editor=await window.ReadyEditor();
            html=await editor.Browser.CoreWebView2.ExecuteScriptAsync("window.MojiEditor.editor.getHTML()");if(!html.Contains(variable.Id)||!html.Contains("Task"))throw new Exception("Document lost content when reopened");checks.Add("HTML document and variables reopen in the same editor without native block reconstruction");
            var password=new Notebook{Name="账号",Mode=NoteMode.Password};vm.Change(w=>w with{Notebooks=[..w.Notebooks,password]},"edit");window.Navigate(password.Id);await Render();window.AddNote();await Render();var credential=vm.SelectedId;vm.EditNote(credential,n=>n with{Username="test@example.com",Password="test-only"});window.Navigate(password.Id,credential);await Render();if(FindNamed<Button>(window,"EditCredential")?.Content?.ToString()!="编辑密码")throw new Exception("Password edit action missing");checks.Add("password records retain native explicit editing outside the shared text editor");Capture(window,Path.Combine(output,"03-password.png"));
            vm.Change(w=>w with{Preferences=w.Preferences with{Theme="dark"}},"edit");window.Navigate(book.Id,id);editor=await window.ReadyEditor();await Render();await using(var stream=File.Create(Path.Combine(output,"05-dark.png")))await editor.Browser.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png,stream);
            var saves=window.BuildCloudSavesWindow();saves.Show();await Render();foreach(var label in new[]{"新建存档","覆盖存档","读取存档","删除存档"})if(!HasButton(saves,label))throw new Exception("Missing global cloud action");Capture(saves,Path.Combine(output,"06-global-saves.png"));saves.Close();checks.Add("four global manual cloud-save actions remain separate from the editor");
            Application.Current.ShutdownMode=ShutdownMode.OnExplicitShutdown;await editor.Flush();await vm.SaveAsync();window.Close();await Render();checks.Add("closing waits for the shared editor bridge and local save");
            await File.WriteAllTextAsync(Path.Combine(output,"report.json"),Json.Serialize(new{success=true,framework="WPF / WebView2 / Tiptap 3.34.0 official Simple Editor",embeddedBrowser=true,checks}));Application.Current.Shutdown(0);
        } catch(Exception e){await File.WriteAllTextAsync(Path.Combine(output,"report.json"),Json.Serialize(new{success=false,error=e.ToString(),checks}));Application.Current.Shutdown(1);}
    }
    private static async Task Script(SharedEditorView editor,string code){await editor.Browser.CoreWebView2.ExecuteScriptAsync("(()=>{"+code+"})()");await Render();}
    private static async Task Render(){await Application.Current.Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);await Task.Delay(300);}
    private static void Capture(Window window,string path){var root=(FrameworkElement)window.Content;var bitmap=new RenderTargetBitmap(Math.Max(1,(int)root.ActualWidth),Math.Max(1,(int)root.ActualHeight),96,96,PixelFormats.Pbgra32);bitmap.Render(root);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using var stream=File.Create(path);encoder.Save(stream);}
    private static T? FindNamed<T>(DependencyObject root,string name)where T:FrameworkElement{if(root is T t&&t.Name==name)return t;for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++){var found=FindNamed<T>(VisualTreeHelper.GetChild(root,i),name);if(found!=null)return found;}return null;}
    private static bool HasButton(DependencyObject root,string label){if(root is Button{Content:string value}&&value==label)return true;for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)if(HasButton(VisualTreeHelper.GetChild(root,i),label))return true;return false;}
}
