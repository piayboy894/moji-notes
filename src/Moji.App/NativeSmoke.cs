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
        var startup=System.Diagnostics.Stopwatch.StartNew();double editorReadyMilliseconds=0;
        try {
            using var vm=new WorkspaceViewModel(Path.Combine(output,"test-data"));var window=new MainWindow(vm,true);Application.Current.MainWindow=window;window.Width=1200;window.Height=850;window.Show();
            if(!await vm.OpenAsync()||vm.State!.Notebooks.Count!=0)throw new Exception("Fresh workspace is not empty");await Render();Capture(window,Path.Combine(output,"01-workspace.png"));checks.Add("empty workspace; only notebooks can own new records");
            var book=new Notebook{Name="编辑测试",Mode=NoteMode.Prompt,Color="#75876D"};vm.Change(w=>w with{Notebooks=[book]},"edit");window.Navigate(book.Id);await Render();window.AddNote();var editor=await window.ReadyEditor();
            editorReadyMilliseconds=startup.Elapsed.TotalMilliseconds;
            if(Environment.GetEnvironmentVariable("MOJI_FORCE_WEBVIEW_BOOTSTRAP")=="1"){
                if(!EditorFiles.HasBootstrapper||EditorFiles.HasFixedRuntime||EditorFiles.BootstrapperInvocations!=1)throw new Exception("Light package must execute the embedded bootstrapper once without a fixed runtime");
                checks.Add("Microsoft-signed setup executes; standard package embeds no fixed WebView2 runtime");
            }
            await Script(editor,"window.MojiEditor.editor.commands.insertContent('Hello 世界')");await Render();var id=vm.SelectedId;
            if(!EditorContent.IsDocument(vm.State!.Notes.First(n=>n.Id==id).Content)||!EditorContent.Plain(vm.State.Notes.First(n=>n.Id==id).Content).Contains("Hello 世界"))throw new Exception("WebView bridge did not save the actual editor document");
            checks.Add("bundled Tiptap editor loads inside WPF and saves through the native bridge");
            await Script(editor,"const e=window.MojiEditor.editor;e.commands.clearContent();e.commands.toggleBlockquote();e.commands.insertContent('引用');e.commands.unsetBlockquote();e.commands.insertContent('正常正文')");await Render();
            var html=await editor.Browser.CoreWebView2.ExecuteScriptAsync("window.MojiEditor.editor.getHTML()");if(html.Contains("blockquote"))throw new Exception("Quote cannot be removed");checks.Add("official quote toggle can be removed without leaving a permanent indent");
            await Script(editor,"const e=window.MojiEditor.editor;e.commands.clearContent();e.commands.toggleTaskList();e.commands.insertContent('Task');e.commands.splitListItem('taskItem');e.commands.insertContent('Second');e.commands.liftListItem('taskItem');e.commands.insertContent('Body')");await Render();
            html=await editor.Browser.CoreWebView2.ExecuteScriptAsync("window.MojiEditor.editor.getHTML()");if(!html.Contains("taskItem")||!html.Contains("Body"))throw new Exception("Official task document was not stored");checks.Add("official task list creates multiple lines and exits back to a paragraph");
            await Script(editor,"const e=window.MojiEditor.editor;e.commands.insertContent('受众');const end=e.state.selection.from;e.commands.setTextSelection({from:end-2,to:end})");
            var convert=FindNamed<Button>(window,"ConvertVariable")??throw new Exception("Conversion button missing");if(!convert.IsEnabled)throw new Exception("Selected text cannot be converted");convert.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await Render();
            var note=vm.State.Notes.First(n=>n.Id==id);var variable=PromptVariables.Used(note).Single();if(variable.Name!="受众")throw new Exception("Selected text was not used as variable name");
            await Script(editor,"const input=document.querySelector('.moji-variable-input');input.value='用户';input.dispatchEvent(new InputEvent('input',{bubbles:true}))");if(!RichDocument.PlainText(vm.State.Notes.First(n=>n.Id==id)).Contains("用户"))throw new Exception("Filled text was not saved/copied");
            await editor.Command("undo");await editor.Command("undo");await Render();if(PromptVariables.Used(vm.State.Notes.First(n=>n.Id==id)).Count!=0)throw new Exception("Undo conversion failed");await editor.Command("redo");await editor.Command("redo");await Render();checks.Add("selected text becomes an inline editable field; entered values save/copy and conversion supports undo/redo");
            var originalBrowser=editor.Browser;var other=new Note{NotebookId=book.Id,Title="Other",Content="Other document"};vm.Change(w=>w with{Notes=[..w.Notes,other]},"edit");window.Navigate(book.Id,other.Id);var otherEditor=await window.ReadyEditor();if(!ReferenceEquals(originalBrowser,otherEditor.Browser))throw new Exception("Switching records recreated the browser");window.Navigate(book.Id,id);editor=await window.ReadyEditor();if(!ReferenceEquals(originalBrowser,editor.Browser))throw new Exception("Returning to a record recreated the browser");checks.Add("record switching reuses one browser and page, without rebuilding the editor engine");
            var title=FindNamed<TextBox>(window,"RecordTitle")!;title.Focus();title.RaiseEvent(new System.Windows.Input.KeyEventArgs(System.Windows.Input.Keyboard.PrimaryDevice,PresentationSource.FromVisual(title),0,System.Windows.Input.Key.Enter){RoutedEvent=System.Windows.Input.Keyboard.PreviewKeyDownEvent});await Render();if(title.Text.Contains('\n')||await editor.Browser.CoreWebView2.ExecuteScriptAsync("document.activeElement===window.MojiEditor.editor.view.dom")!="true")throw new Exception("Enter did not move from title into content");checks.Add("title Enter focuses content without adding a title newline");
            await using(var stream=File.Create(Path.Combine(output,"02-editor.png")))await editor.Browser.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png,stream);
            await editor.Flush();await vm.SaveAsync();window.Navigate("home");await Render();await vm.OpenAsync();window.Navigate(book.Id,id);editor=await window.ReadyEditor();
            html=await editor.Browser.CoreWebView2.ExecuteScriptAsync("window.MojiEditor.editor.getHTML()");if(!html.Contains(variable.Id)||!html.Contains("Task"))throw new Exception("Document lost content when reopened");checks.Add("HTML document and variables reopen in the same editor without native block reconstruction");
            var password=new Notebook{Name="账号",Mode=NoteMode.Password};vm.Change(w=>w with{Notebooks=[..w.Notebooks,password]},"edit");window.Navigate(password.Id);await Render();window.AddNote();await Render();var credential=vm.SelectedId;vm.EditNote(credential,n=>n with{Username="test@example.com",Password="test-only"});window.Navigate(password.Id,credential);await Render();if(FindNamed<Button>(window,"EditCredential")?.Content?.ToString()!="编辑密码")throw new Exception("Password edit action missing");checks.Add("password records retain native explicit editing outside the shared text editor");Capture(window,Path.Combine(output,"03-password.png"));
            vm.Change(w=>w with{Preferences=w.Preferences with{Theme="dark"}},"edit");window.Navigate(book.Id,id);editor=await window.ReadyEditor();await Render();await using(var stream=File.Create(Path.Combine(output,"05-dark.png")))await editor.Browser.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png,stream);
            var saves=window.BuildCloudSavesWindow();saves.Show();await Render();foreach(var label in new[]{"新建存档","覆盖存档","读取存档","删除存档"})if(!HasButton(saves,label))throw new Exception("Missing global cloud action");Capture(saves,Path.Combine(output,"06-global-saves.png"));saves.Close();checks.Add("four global manual cloud-save actions remain separate from the editor");
            var removedA=new Note{NotebookId=book.Id,Title="visible trash",Content="remove",DeletedAt=Clock.Now};var removedB=new Note{NotebookId=book.Id,Title="hidden trash",Content="secret",Password="fake",DeletedAt=Clock.Now};vm.Change(w=>w with{Notes=[..w.Notes,removedA,removedB]},"edit");window.Navigate("trash");vm.Search="visible";window.RefreshShell();await Render();
            var empty=FindNamed<Button>(window,"EmptyTrash")??throw new Exception("Empty trash action missing");
            _ = Application.Current.Dispatcher.BeginInvoke(new Action(()=>{var dialog=Application.Current.Windows.OfType<Window>().First(w=>w.Owner==window);FindButton(dialog,"确认")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));}));empty.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await Render();
            if(vm.State.Notes.Any(n=>n.DeletedAt!=null&&n.PurgedAt==null)||vm.State.Notes.First(n=>n.Id==removedB.Id).Password!=""||vm.State.Notes.First(n=>n.Id==id).PurgedAt!=null)throw new Exception("Empty trash did not clear all deleted records, or affected live records");checks.Add("one empty-trash action removes all deleted records across search filters and preserves live records");
            window.Navigate(book.Id,id);editor=await window.ReadyEditor();
            Application.Current.ShutdownMode=ShutdownMode.OnExplicitShutdown;await editor.Flush();await vm.SaveAsync();window.Close();await Render();checks.Add("closing waits for the shared editor bridge and local save");
            if(Environment.GetEnvironmentVariable("MOJI_FORCE_WEBVIEW_BOOTSTRAP")=="1"&&EditorFiles.BootstrapperInvocations!=1)throw new Exception("Reopening editors must reuse the shared Evergreen runtime");
            await File.WriteAllTextAsync(Path.Combine(output,"report.json"),Json.Serialize(new{success=true,framework="WPF / WebView2 / Tiptap 3.34.0 official Simple Editor",embeddedBrowser=true,editorReadyMilliseconds,bootstrapperInvocations=EditorFiles.BootstrapperInvocations,hasFixedRuntime=EditorFiles.HasFixedRuntime,sharedRuntime=CoreWebView2Environment.GetAvailableBrowserVersionString(),checks}));Application.Current.Shutdown(0);
        } catch(Exception e){await File.WriteAllTextAsync(Path.Combine(output,"report.json"),Json.Serialize(new{success=false,error=e.ToString(),checks}));Application.Current.Shutdown(1);}
    }
    private static async Task Script(SharedEditorView editor,string code){await editor.Browser.CoreWebView2.ExecuteScriptAsync("(()=>{"+code+"})()");await Render();}
    private static async Task Render(){await Application.Current.Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);await Task.Delay(300);}
    private static void Capture(Window window,string path){var root=(FrameworkElement)window.Content;var bitmap=new RenderTargetBitmap(Math.Max(1,(int)root.ActualWidth),Math.Max(1,(int)root.ActualHeight),96,96,PixelFormats.Pbgra32);bitmap.Render(root);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using var stream=File.Create(path);encoder.Save(stream);}
    private static T? FindNamed<T>(DependencyObject root,string name)where T:FrameworkElement{if(root is T t&&t.Name==name)return t;for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++){var found=FindNamed<T>(VisualTreeHelper.GetChild(root,i),name);if(found!=null)return found;}return null;}
    private static Button? FindButton(DependencyObject root,string label){if(root is Button{Content:string value} button&&value==label)return button;for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++){var found=FindButton(VisualTreeHelper.GetChild(root,i),label);if(found!=null)return found;}return null;}
    private static bool HasButton(DependencyObject root,string label){if(root is Button{Content:string value}&&value==label)return true;for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)if(HasButton(VisualTreeHelper.GetChild(root,i),label))return true;return false;}
}
