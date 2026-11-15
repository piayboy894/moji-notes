using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ICSharpCode.AvalonEdit;
using Moji.Core;

namespace Moji.App;
internal static class NativeSmoke
{
    public static async Task Run(string[] args)
    {
        var index=Array.IndexOf(args,"--output");var output=index>=0&&args.Length>index+1?Path.GetFullPath(args[index+1]):Path.Combine(Path.GetTempPath(),"moji-native-smoke");Directory.CreateDirectory(output);var data=Path.Combine(output,"test-data");var checks=new List<string>();
        try
        {
            using var vm=new WorkspaceViewModel(data);var window=new MainWindow(vm,true);Application.Current.MainWindow=window;window.Show();await vm.CreateAsync("Smoke-Only-Password-2026");var savedData=GameSaves.Capture(vm.State!);await Render();Capture(window,Path.Combine(output,"01-workspace.png"));checks.Add("native WPF window opened and dashboard rendered");
            var book=vm.State!.LiveBooks.First(b=>b.Mode==NoteMode.Text);window.Navigate(book.Id);await Render();var editor=Find<TextEditor>(window)??throw new Exception("Native AvalonEdit was not created");editor.Text="# Native smoke test\n\nThis text is encrypted.\n\n- [x] native editor\n\n| A | B |\n| --- | --- |\n| 1 | 2 |";await vm.SaveAsync();await Render();Capture(window,Path.Combine(output,"02-editor.png"));checks.Add("native editor updates persisted to encrypted vault");
            var saved=await File.ReadAllTextAsync(vm.Store.VaultPath);if(saved.Contains("This text is encrypted"))throw new Exception("Plaintext leaked to disk");var noteId=vm.SelectedId;await vm.LockAsync();if(vm.Key!=null||vm.State!=null)throw new Exception("Lock did not clear key and state");await vm.UnlockAsync("Smoke-Only-Password-2026");if(!vm.State!.Notes.Any(n=>n.Id==noteId&&n.Content.Contains("This text is encrypted")))throw new Exception("Unlock did not restore the editor content");checks.Add("lock and unlock preserved data and released the key");
            var passwordBook=vm.State.LiveBooks.First(b=>b.Mode==NoteMode.Password);window.Navigate(passwordBook.Id);window.AddNote(("Smoke account",NoteMode.Password,"test memo"));vm.EditNote(vm.SelectedId,n=>n with{Username="sample@example.com",Password="Dummy-Password-2026!"});window.Navigate(passwordBook.Id,vm.SelectedId);await Render();if(Find<PasswordBox>(window)==null)throw new Exception("PasswordBox not found");Capture(window,Path.Combine(output,"03-password.png"));checks.Add("native PasswordBox renders credential mode");
            window.Navigate(vm.State.LiveBooks.First(b=>b.Mode==NoteMode.Prompt).Id);await Render();Capture(window,Path.Combine(output,"04-prompt.png"));checks.Add("prompt mode renders using native controls");
            vm.Change(v=>v with{Preferences=v.Preferences with{Theme="dark"}});window.Navigate("home");await Render();Capture(window,Path.Combine(output,"05-dark.png"));checks.Add("dark native theme renders");
            await vm.ReplaceDataAsync(savedData);if(!vm.State!.Notes.Select(n=>n.Id).SequenceEqual(savedData.Notes.Select(n=>n.Id))||vm.State.Preferences.Theme!="dark")throw new Exception("Global save load merged data or lost device preferences");checks.Add("manual global save replaces all project data and retains device preferences");
            vm.Change(v=>v with{Preferences=v.Preferences with{Theme="light"}});window.Navigate("home");var saves=window.BuildCloudSavesWindow();saves.Show();await Render();foreach(var label in new[]{"新建存档","覆盖存档","读取存档","删除存档"})if(!HasButton(saves,label))throw new Exception("Global save action missing: "+label);Capture(saves,Path.Combine(output,"06-global-saves.png"),860,650);saves.Close();checks.Add("global save manager exposes create, overwrite, load and delete");
            await vm.SaveAsync();await File.WriteAllTextAsync(Path.Combine(output,"report.json"),Json.Serialize(new{success=true,framework="WPF / .NET 10",embeddedBrowser=false,checks}));Application.Current.Shutdown(0);
        }
        catch(Exception error){await File.WriteAllTextAsync(Path.Combine(output,"report.json"),Json.Serialize(new{success=false,error=error.ToString(),checks}));Application.Current.Shutdown(1);}
    }
    private static async Task Render(){await Application.Current.Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);await Task.Delay(450);}
    private static void Capture(Window window,string path,int width=1440,int height=940)
    {
        // Render the actual WPF tree offscreen so CI's small monitor does not crop wide layouts.
        var root=(FrameworkElement)window.Content;
        window.Content=null;
        // ContentPresenter must release its child before offscreen layout; otherwise the
        // live window's old client size is reapplied by WPF during UpdateLayout.
        window.UpdateLayout();
        var host=new Border{Width=width,Height=height,Background=window.Background};
        try
        {
            if(VisualTreeHelper.GetParent(root)!=null)throw new InvalidOperationException("The native view was not detached for offscreen layout.");
            System.Windows.Documents.TextElement.SetFontFamily(host,window.FontFamily);
            System.Windows.Documents.TextElement.SetFontSize(host,window.FontSize);
            System.Windows.Documents.TextElement.SetForeground(host,window.Foreground);
            host.Child=root;root.InvalidateMeasure();host.Measure(new Size(width,height));host.Arrange(new Rect(0,0,width,height));host.UpdateLayout();
            if(Math.Abs(root.ActualWidth-(width-root.Margin.Left-root.Margin.Right))>.1||Math.Abs(root.ActualHeight-(height-root.Margin.Top-root.Margin.Bottom))>.1)throw new InvalidOperationException("The native view did not fill the requested client size.");
            var bitmap=new RenderTargetBitmap(width,height,96,96,PixelFormats.Pbgra32);bitmap.Render(host);
            var pixel=new byte[4];bitmap.CopyPixels(new Int32Rect(width-1,height-1,1,1),pixel,4,0);
            if(pixel[3]!=255)throw new InvalidOperationException("The native client background was cropped.");
            var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using(var stream=File.Create(path))encoder.Save(stream);
        }
        finally {host.Child=null;window.Content=root;window.UpdateLayout();}
    }
    private static bool HasButton(DependencyObject root,string label){if(root is Button{Content:string value}&&value==label)return true;for(var i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)if(HasButton(VisualTreeHelper.GetChild(root,i),label))return true;return false;}
    private static T? Find<T>(DependencyObject root)where T:DependencyObject{if(root is T match)return match;for(var i=0;i<VisualTreeHelper.GetChildrenCount(root);i++){var child=Find<T>(VisualTreeHelper.GetChild(root,i));if(child!=null)return child;}return null;}
}
