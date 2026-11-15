using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Windows.Documents;
using Moji.Core;

namespace Moji.App;
internal static class NativeSmoke
{
    public static async Task Run(string[] args)
    {
        var index=Array.IndexOf(args,"--output");var output=index>=0&&args.Length>index+1?Path.GetFullPath(args[index+1]):Path.Combine(Path.GetTempPath(),"moji-native-smoke");Directory.CreateDirectory(output);var data=Path.Combine(output,"test-data");var checks=new List<string>();
        try
        {
            using var vm = new WorkspaceViewModel(data); var window = new MainWindow(vm,true); Application.Current.MainWindow = window; window.Show();
            if (!await vm.OpenAsync() || vm.State!.Notes.Count != 0 || vm.State.Sync.Repository != "") throw new Exception("Fresh start was not empty and password-free");
            var savedData = GameSaves.Capture(vm.State); await Render(); Capture(window,Path.Combine(output,"01-workspace.png")); checks.Add("password-free start has empty notebooks and no preset repository or samples");
            var book = vm.State.LiveBooks.First(b=>b.Mode==NoteMode.Text); window.Navigate(book.Id); window.AddNote(); await Render();
            var editor = Find<RichTextBox>(window) ?? throw new Exception("Native editor not created"); new TextRange(editor.Document.ContentStart,editor.Document.ContentEnd).Text = "Native edit\n\nPlain local persistence."; await vm.SaveAsync(); var noteId = vm.SelectedId;
            if (!(await File.ReadAllTextAsync(vm.Store.VaultPath)).Contains("Plain local persistence")) throw new Exception("Local data was not plain text");
            await vm.OpenAsync(); if (!vm.State!.Notes.Any(n=>n.Id==noteId && n.Content.Contains("Plain local persistence"))) throw new Exception("Restart lost data");
            window.Navigate(book.Id,noteId); await Render(); Capture(window,Path.Combine(output,"02-editor.png")); checks.Add("native editing autosaves to ordinary JSON and reopens without a password");
            var variable=new PromptVariable{Name="受众"};var richSource="# Heading\n\n**Bold** and *italic* and ~~strike~~\n\n- [ ] Task\n\n| A | B |\n| --- | --- |\n| one | two |\n\n"+PromptVariables.Token(variable)+"\n\n用户手打 {{原样保留}}";
            vm.EditNote(noteId,n=>n with{Content=richSource,Variables=[variable]});window.Navigate(book.Id,noteId);await Render();editor=Find<RichTextBox>(window)!;
            var visible=new TextRange(editor.Document.ContentStart,editor.Document.ContentEnd).Text;if(visible.Contains("**")||visible.Contains("# Heading"))throw new Exception("WYSIWYG displayed source markup");
            var task=Find<CheckBox>(editor)??throw new Exception("Task is not an interactive checkbox");task.IsChecked=true;await Render();
            if(!vm.State.Notes.First(n=>n.Id==noteId).Content.Contains("[x]"))throw new Exception("Task state did not persist");
            var roundTrip=RichDocument.Serialize(editor.Document);if(!roundTrip.Contains("**Bold**")||!roundTrip.Contains("| one | two |")||!roundTrip.Contains("moji-variable:"+variable.Id))throw new Exception("Rich format or variables lost on save");
            if(PromptVariables.Used(vm.State.Notes.First(n=>n.Id==noteId)).Count!=1)throw new Exception("Typed braces triggered a variable");checks.Add("WYSIWYG renders styles, clickable tasks, editable tables and explicit variable blocks without source markup");
            var table=editor.Document.Blocks.OfType<Table>().Single();var firstCell=table.RowGroups[0].Rows[0].Cells[0];editor.CaretPosition=firstCell.ContentStart;
            FindButton(window,"添加列")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));if(table.RowGroups[0].Rows[0].Cells.Count!=3)throw new Exception("Add column did not affect table");
            FindTool(window,"撤销")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));table=editor.Document.Blocks.OfType<Table>().Single();if(table.RowGroups[0].Rows[0].Cells.Count!=2)throw new Exception("Table operation cannot be undone");
            FindTool(window,"重做")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));table=editor.Document.Blocks.OfType<Table>().Single();if(table.RowGroups[0].Rows[0].Cells.Count!=3)throw new Exception("Table operation cannot be redone");
            firstCell=table.RowGroups[0].Rows[0].Cells[0];new TextRange(firstCell.ContentStart,firstCell.ContentEnd).Text="left|right";var savedRich=vm.State.Notes.First(n=>n.Id==noteId);
            var reread=MarkdownDocument.Render(savedRich.Content,15,savedRich.Variables).Blocks.OfType<Table>().Single();if(reread.RowGroups[0].Rows[0].Cells.Count!=3||!new TextRange(reread.ContentStart,reread.ContentEnd).Text.Contains("left|right"))throw new Exception("Table text broke columns after reopen");
            var copied=RichDocument.PlainText(savedRich);if(copied.Contains("**")||copied.Contains("moji-variable:")||!copied.Contains("受众")||!copied.Contains("{{原样保留}}"))throw new Exception("Copy full text leaked source or lost literal text");
            Capture(window,Path.Combine(output,"07-rich-tools.png"));checks.Add("table add-column, undo, redo, literal pipe round-trip and plain full-text copy work in native controls");
            var passwordBook = vm.State.LiveBooks.First(b=>b.Mode==NoteMode.Password); window.Navigate(passwordBook.Id); window.AddNote(); await Render();
            if (vm.State.Notes.First(n=>n.Id==vm.SelectedId).NotebookId!=passwordBook.Id || FindNamed<TextBox>(window,"PasswordEdit")?.IsReadOnly!=false) throw new Exception("New password did not open in its own notebook editor");
            var credentialId=vm.SelectedId; vm.EditNote(credentialId,n=>n with { Username="test@example.com",Password="Test-only-2026!" }); window.Navigate(passwordBook.Id,credentialId); await Render();
            var readAccount=FindNamed<TextBox>(window,"AccountValue"); if(readAccount?.IsReadOnly!=true || FindNamed<Button>(window,"EditCredential")?.Content?.ToString()!="编辑密码")throw new Exception("Password view must be read-only with an edit action");
            FindNamed<Button>(window,"EditCredential")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); await Render(); if(FindNamed<TextBox>(window,"PasswordEdit")?.IsReadOnly!=false)throw new Exception("Edit button did not enable password editor");
            FindNamed<Button>(window,"EditCredential")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); await Render(); if(FindNamed<TextBox>(window,"AccountValue")?.IsReadOnly!=true)throw new Exception("Done did not return to read-only");
            await Render(); Capture(window,Path.Combine(output,"03-password.png")); checks.Add("password records open read-only; Edit password and Done change the editing state");
            window.Navigate(vm.State.LiveBooks.First(b=>b.Mode==NoteMode.Prompt).Id); window.AddNote(); await Render(); Capture(window,Path.Combine(output,"04-prompt.png"));
            if(vm.State.Notebooks.First(b=>b.Id==vm.View).Mode!=NoteMode.Prompt)throw new Exception("Prompt created in wrong notebook");
            checks.Add("new text, prompt and password records stay in the selected notebook");
            vm.EditNote(noteId,n=>n with{Favorite=true}); window.Navigate("star"); await Render();
            var options=FindNamed<Button>(window,"ListOptions")??throw new Exception("Missing list options"); options.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            if(options.ContextMenu?.IsOpen!=true)throw new Exception("Favorites menu did not open");
            var removeStar=options.ContextMenu.Items.OfType<MenuItem>().First(i=>i.Header?.ToString()=="取消收藏所选"); if(!removeStar.IsEnabled)throw new Exception("Favorite action disabled"); removeStar.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            if(vm.State.Notes.First(n=>n.Id==noteId).Favorite)throw new Exception("Unfavorite had no effect"); options.ContextMenu.IsOpen=false; checks.Add("favorites ellipsis opens real actions and unfavorite updates data");
            vm.Change(v=>v with{Preferences=v.Preferences with{Theme="dark"}}); window.Navigate("home"); await Render(); Capture(window,Path.Combine(output,"05-dark.png"));
            await vm.ReplaceDataAsync(savedData); if(vm.State!.Notes.Count!=0||vm.State.Preferences.Theme!="dark")throw new Exception("Global restore scope incorrect");
            vm.Change(v=>v with{Preferences=v.Preferences with{Theme="light"}}); window.Navigate("home"); var saves=window.BuildCloudSavesWindow(); saves.Show(); await Render();
            foreach(var label in new[]{"新建存档","覆盖存档","读取存档","删除存档"})if(!HasButton(saves,label))throw new Exception("Missing global save action: "+label);
            Capture(saves,Path.Combine(output,"06-global-saves.png"),860,650); saves.Close(); checks.Add("four global manual save actions retained");
            var oldDirectory=Path.Combine(output,"migration-test"); Directory.CreateDirectory(oldDirectory); var oldState=Seed.Create();
            using(var oldKey=CryptoService.NewKey("Legacy-test-password"))await File.WriteAllTextAsync(Path.Combine(oldDirectory,"vault.moji"),Json.Serialize(CryptoService.Encrypt(oldState,oldKey)));
            using(var migration=new WorkspaceViewModel(oldDirectory)) { if(await migration.OpenAsync())throw new Exception("Encrypted legacy file was skipped"); await migration.MigrateAsync("Legacy-test-password"); if(!await migration.OpenAsync()||migration.State!.Id!=oldState.Id)throw new Exception("Migration did not preserve old data"); }
            checks.Add("legacy data migrates once and then reopens without password or key");
            Application.Current.ShutdownMode=ShutdownMode.OnExplicitShutdown;await vm.SaveAsync();var didClose=false;window.Closed+=(_,_)=>didClose=true;window.Close();await Render();if(!didClose)throw new Exception("Clean close did not complete without a recursive Closing call");checks.Add("already-saved window closes without reentrant callbacks or error prompts");
            await File.WriteAllTextAsync(Path.Combine(output,"report.json"),Json.Serialize(new{success=true,framework="WPF / .NET 10",embeddedBrowser=false,checks}));Application.Current.Shutdown(0);
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
    private static T? FindNamed<T>(DependencyObject root,string name) where T:FrameworkElement { if(root is T item && item.Name==name)return item; for(var i=0;i<VisualTreeHelper.GetChildrenCount(root);i++){var match=FindNamed<T>(VisualTreeHelper.GetChild(root,i),name);if(match!=null)return match;}return null; }
    private static Button? FindButton(DependencyObject root,string label){if(root is Button{Content:string value} button&&value==label)return button;for(var i=0;i<VisualTreeHelper.GetChildrenCount(root);i++){var found=FindButton(VisualTreeHelper.GetChild(root,i),label);if(found!=null)return found;}return null;}
    private static Button? FindTool(DependencyObject root,string label){if(root is Button button&&button.ToolTip?.ToString()==label)return button;for(var i=0;i<VisualTreeHelper.GetChildrenCount(root);i++){var found=FindTool(VisualTreeHelper.GetChild(root,i),label);if(found!=null)return found;}return null;}
    private static bool HasButton(DependencyObject root,string label){if(root is Button{Content:string value}&&value==label)return true;for(var i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)if(HasButton(VisualTreeHelper.GetChild(root,i),label))return true;return false;}
    private static T? Find<T>(DependencyObject root)where T:DependencyObject{if(root is T match)return match;for(var i=0;i<VisualTreeHelper.GetChildrenCount(root);i++){var child=Find<T>(VisualTreeHelper.GetChild(root,i));if(child!=null)return child;}return null;}
}
