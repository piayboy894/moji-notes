using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using System.Windows.Documents;
using Moji.Core;
using Microsoft.Win32;

namespace Moji.App;
public partial class MainWindow : Window
{
    public WorkspaceViewModel VM { get; }
    private readonly DispatcherTimer toastTimer = new() { Interval = TimeSpan.FromSeconds(4) };
    private readonly DispatcherTimer sessionTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private bool closing, closePending, loadingEditor, focusMode;
    private ListBox? noteList;
    private readonly Dictionary<string,TextBlock> notebookCounts=[];
    private readonly ObservableCollection<NoteRow> noteRows = [];
    private ContentControl? editorHost;
    private SharedEditorView? sharedEditor;
    private SharedEditorView? cachedEditor;
    private Task editorRelease=Task.CompletedTask;
    private int editorRequest;
    private TextBox? titleBox;
    private string renderedId = "";
    private string editingCredentialId = "";
    private string clipboardSecret = "";
    private long clipboardAt;
    private UpdatePackage? availableUpdate;
    private string? downloadedUpdate;
    private readonly UpdateService updater = new();
    public MainWindow(WorkspaceViewModel? viewModel = null, bool smoke = false)
    {
        InitializeComponent(); SourceInitialized+=(_,_)=>DesktopNativeUi.ApplyCaption(this); VM = viewModel ?? new(); VM.DataChanged += DataChanged; VM.Notice += Toast;GlobalSearchInput.ContextMenu=DesktopNativeUi.TextMenu(GlobalSearchInput);
        VM.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(VM.SaveStatus)) SaveText.Text = VM.SaveStatus; };
        TrashNav.ContextMenu=BuildTrashNavigationMenu();TrashNav.ContextMenuOpening+=(_,_)=>TrashNav.ContextMenu=BuildTrashNavigationMenu();
        Microsoft.Win32.SystemEvents.UserPreferenceChanged+=SystemThemeChanged;
        ToastPopup.PlacementTarget=Root;ToastPopup.CustomPopupPlacementCallback=(popup,target,offset)=>[new CustomPopupPlacement(new Point((target.Width-popup.Width)/2,Math.Max(0,target.Height-popup.Height-24)),PopupPrimaryAxis.Vertical)];
        toastTimer.Tick += (_, _) => { ToastPopup.IsOpen=false; toastTimer.Stop(); };
        sessionTimer.Tick += (_, _) => SessionTick(); sessionTimer.Start();
        PreviewKeyDown += KeyDownHandler; Closing += ClosingHandler;
        Closed += (_,_) => { Microsoft.Win32.SystemEvents.UserPreferenceChanged-=SystemThemeChanged;ClearSecretClipboard();ToastPopup.IsOpen=false;CloseGlobalSearch(); sessionTimer.Stop(); toastTimer.Stop(); cachedEditor?.Dispose();VM.Dispose(); };
        VM.FlushEditor=async()=>{await editorRelease;if(cachedEditor!=null)await cachedEditor.Flush();};
        Loaded += async (_, _) => { if (smoke) return; await OpenWorkspaceAsync(); };
    }
    private void SystemThemeChanged(object sender,Microsoft.Win32.UserPreferenceChangedEventArgs e){Dispatcher.BeginInvoke(()=>{if(!IsLoaded||VM.State?.Preferences.Theme!="system")return;Ui.Theme(VM.State.Preferences);if(cachedEditor!=null)_=cachedEditor.RefreshAppearance();if(VM.View=="home")RenderDashboard();});}
    private void SessionTick()
    {
        if (!VM.IsUnlocked) return;
        if (!string.IsNullOrEmpty(clipboardSecret) && Clock.Now - clipboardAt >= 30000) ClearSecretClipboard();

    }
    private void DataChanged(string kind)
    {
        if(kind is "open" or "replace"){CloseGlobalSearch();cachedEditor?.Dispose();cachedEditor=null;sharedEditor=null;EditorWarmHost.Children.Clear();editorRequest++;WarmEditor();}
        if (kind == "open")
        { AuthHost.Content = null; AuthHost.Visibility = Visibility.Collapsed; Shell.Visibility = Visibility.Visible; Ui.Theme(VM.State!.Preferences); RefreshShell(); var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) }; timer.Tick += async (_, _) => { timer.Stop(); if (VM.IsUnlocked) await CheckUpdates(false); }; timer.Start(); return; }
        if (kind == "edit") { Ui.Theme(VM.State!.Preferences);var byId=VM.State!.Notes.ToDictionary(n=>n.Id);foreach(var row in noteRows)if(byId.TryGetValue(row.Note.Id,out var n)&&!ReferenceEquals(row.Note,n))row.Replace(n); UpdateStats();if(cachedEditor!=null)_ = cachedEditor.RefreshAppearance();return; }
        RefreshShell();
    }
    public void Navigate(string view, string? selected = null, bool editCredential = false)
    { CloseGlobalSearch();if(selectingRecords)SetRecordSelection(false);editingCredentialId = editCredential ? selected ?? "" : ""; VM.View = view; VM.Search = ""; VM.SelectedId = selected ?? ""; renderedId = ""; focusMode = false; Shell.ColumnDefinitions[0].Width = new GridLength(270); RefreshShell(); }
    public void RefreshShell()
    {
        if (VM.State == null) return; Ui.Theme(VM.State.Preferences); UpdateStats();
        BooksPanel.Children.Clear();notebookCounts.Clear(); foreach (var book in VM.State.LiveBooks)
        {
            var button = Ui.Button("", () => {if(!draggingNotebook)Navigate(book.Id);}); button.Style = (Style)FindResource("NavButton");
            var row = new DockPanel(); var count = Ui.Text(VM.State.LiveNotes.Count(n => n.NotebookId == book.Id && !n.Archived).ToString(), 10); notebookCounts[book.Id]=count;count.Tag="count";count.Foreground = Ui.Resource("Sub"); count.HorizontalAlignment = HorizontalAlignment.Right; DockPanel.SetDock(count, Dock.Right); row.Children.Add(count);
            row.Children.Add(new NativeIcon { Tag=book.Id, Kind = NativeIcon.ForNotebook(book), Width = 22, Height = 22, Foreground = new SolidColorBrush(NotebookAppearance.Parse(book.Color)), Margin = new Thickness(0,0,13,0) }); row.Children.Add(Ui.Text(book.Name, 13)); button.Content = row;SetNavigationSelection(button,VM.View==book.Id);
            var menu = new ContextMenu(); menu.Items.Add(MenuItem("编辑记事本", () => NotebookDialog(book))); menu.Items.Add(MenuItem("删除记事本", () => DeleteNotebook(book))); AddOrderActions(menu,book); button.ContextMenu = menu;
            button.AllowDrop = true; button.DragOver += (_, e) => { if(e.Data.GetDataPresent("MojiNotebook"))return; e.Effects = e.Data.GetDataPresent("MojiNote") ? DragDropEffects.Move : DragDropEffects.None; e.Handled = true; }; button.Drop += (_, e) => { if(e.Data.GetDataPresent("MojiNotebook"))return; var id = e.Data.GetData("MojiNote") as string; var note = VM.State.Notes.FirstOrDefault(n => n.Id == id); if (note != null && VM.State.Notebooks.First(b => b.Id == note.NotebookId).Mode == book.Mode) { VM.EditNote(note.Id, n => n with { NotebookId = book.Id }); Navigate(book.Id, note.Id); } }; AttachNotebookDrag(button,book,false);BooksPanel.Children.Add(button);
        }
        SetNavigationSelection(HomeNav,VM.View=="home");SetNavigationSelection(AllNav,VM.View=="all");SetNavigationSelection(StarNav,VM.View=="star");SetNavigationSelection(TrashNav,VM.View=="trash");SetNavigationSelection(ArchiveNav,VM.View=="archive");
        NewRecordButton.Visibility = CanCreateRecord ? Visibility.Visible : Visibility.Collapsed;
        PageTitle.Text = ViewTitle(); if (VM.View == "home") RenderDashboard(); else if(VM.View=="trash")RenderTrash();else RenderWorkspace();
    }
    private void SetNavigationSelection(Button button,bool selected)
    {
        button.Background=selected?Ui.Resource("RecordSelected"):Brushes.Transparent;button.BorderThickness=new Thickness(0);button.SetResourceReference(Control.ForegroundProperty,selected?"RecordSelectedInk":"Ink");
        void Tint(UIElement element){if(element is TextBlock text)text.SetResourceReference(TextBlock.ForegroundProperty,selected?(text.Tag?.ToString()=="count"?"RecordSelectedSub":"RecordSelectedInk"):(text.Tag?.ToString()=="count"?"Sub":"Ink"));else if(element is NativeIcon icon){if(selected)icon.SetResourceReference(NativeIcon.ForegroundProperty,"RecordSelectedInk");else if(VM.State!.LiveBooks.FirstOrDefault(b=>b.Id==icon.Tag?.ToString()) is {} book)icon.Foreground=new SolidColorBrush(NotebookAppearance.Parse(book.Color));else icon.SetResourceReference(NativeIcon.ForegroundProperty,"Sub");}else if(element is Panel panel)foreach(UIElement child in panel.Children)Tint(child);}
        if(button.Content is UIElement content)Tint(content);
    }
    private UIElement NavContent(string kind, string title, int? count = null)
    {
        var row = new DockPanel();
        if (count != null) { var number = Ui.Text(count.Value.ToString(), 11); number.Tag="count";number.Foreground = Ui.Resource("Sub"); DockPanel.SetDock(number, Dock.Right); row.Children.Add(number); }
        row.Children.Add(new NativeIcon { Kind = kind, Width = 22, Height = 22, Foreground = Ui.Resource("Sub"), Margin = new Thickness(0,0,14,0) }); row.Children.Add(Ui.Text(title, 13)); return row;
    }
    private void SetNavContent(Button button,string icon,string title,int? count=null){if(button.Content is not DockPanel row){button.Content=NavContent(icon,title,count);return;}if(count!=null&&row.Children.OfType<TextBlock>().FirstOrDefault(t=>t.Tag?.ToString()=="count") is {} number&&number.Text!=count.ToString())number.Text=count.ToString();}
    private void UpdateStats()
    {
        if (VM.State == null) return;
        SetNavContent(HomeNav,"Dashboard","工作台");
        SetNavContent(AllNav,"Document","全部记录",VM.State.LiveNotes.Count(n=>!n.Archived));
        SetNavContent(StarNav,"Star","我的收藏",VM.State.LiveNotes.Count(n=>n.Favorite&&!n.Archived));
        SetNavContent(TrashNav,"Trash","回收站",VM.State.Notes.Count(n=>n.DeletedAt!=null&&n.PurgedAt==null));
        var counts=VM.State.LiveNotes.Where(n=>!n.Archived).GroupBy(n=>n.NotebookId).ToDictionary(g=>g.Key,g=>g.Count());foreach(var (id,label)in notebookCounts){var count=counts.GetValueOrDefault(id).ToString();if(label.Text!=count)label.Text=count;}
        SaveText.Text = VM.SaveStatus;
    }
    private bool CanCreateRecord => VM.State?.LiveBooks.Any(b => b.Id == VM.View) == true;
    private string ViewTitle() => VM.State?.LiveBooks.FirstOrDefault(b => b.Id == VM.View)?.Name ?? VM.View switch { "home" => "工作台", "star" => "我的收藏", "archive" => "归档记录", "trash" => "回收站", _ => "全部记录" };
    internal Note? CurrentNote() => VM.State?.Notes.FirstOrDefault(n => n.Id == VM.SelectedId);
    public void Toast(string message) { var owner=Application.Current.Windows.OfType<Window>().LastOrDefault(w=>w.IsActive);var target=owner?.Content as FrameworkElement??Root;ToastPopup.PlacementTarget=target;ToastBorder.MaxWidth=Math.Max(160,Math.Min(780,target.ActualWidth-40));ToastText.Text=message;ToastPopup.IsOpen=true;toastTimer.Stop();toastTimer.Start(); }
    public void Copy(string value, bool secret = false)
    { try { Clipboard.SetText(value); if (secret) { clipboardSecret = value; clipboardAt = Clock.Now; } Toast(secret ? "已复制，30 秒后清除剪贴板" : "已复制"); } catch { Toast("复制失败，剪贴板正被其他应用使用"); } }
    private void ClearSecretClipboard() { try { if (Clipboard.ContainsText() && Clipboard.GetText() == clipboardSecret) Clipboard.Clear(); } catch { } clipboardSecret = ""; }
    private void ClosingHandler(object? sender, CancelEventArgs e)
    {
        if(closing)return;e.Cancel=true;if(closePending)return;closePending=true;IsEnabled=false;
        // Even a completed SaveAsync must resume after WPF has left its Closing callback.
        Dispatcher.BeginInvoke(new Action(async()=>{
            try{await editorRelease;if(cachedEditor!=null)await cachedEditor.Flush();await VM.SaveAsync();closing=true;closePending=false;Close();}
            catch(Exception){closing=false;closePending=false;IsEnabled=true;ShowSaveFailure();}
        }),DispatcherPriority.Background);
    }
    private void ShowSaveFailure()
    {
        var panel=Ui.Stack(Ui.Text("内容仍在当前窗口中。请重试保存，或先导出一份存档。",13));panel.Margin=new Thickness(24);Window? dialog=null;
        var status=Ui.Text("",12);panel.Children.Add(Ui.Spaced(status,new Thickness(0,14,0,14)));
        var retry=Ui.Button("重试保存",async()=>{try{await VM.SaveAsync();dialog!.Close();Close();}catch(Exception){status.Text="暂时无法保存，请导出存档到其他位置。";}},true);
        panel.Children.Add(Ui.Row(Ui.Button("继续编辑",()=>dialog!.Close()),Ui.Button("导出存档",()=>_ = ExportBackup()),retry));
        dialog=Ui.Dialog(this,"暂时无法保存",panel,560);dialog.ShowDialog();
    }
    private void KeyDownHandler(object sender, KeyEventArgs e)
    {
        if (!VM.IsUnlocked || e.IsRepeat) return;
        if(e.Key==Key.Escape&&SearchDropdownOpen){e.Handled=true;CloseGlobalSearch();return;}
        var action = Shortcuts.Resolve(VM.State!.Preferences, KeyChord(e));
        switch (action) {
            case "search": e.Handled = true; FocusGlobalSearch(); break;
            case "new": if (CanCreateRecord) { e.Handled = true; AddNote(); } break;
            case "cloud": e.Handled = true; ShowCloudSaves(); break;
            case "save": e.Handled = true; if (CurrentNote() is { } n) { VM.Checkpoint(n.Id); _ = VM.SaveAsync(); Toast("当前版本已保存"); } break;
            case "focus": e.Handled = true; ToggleFocus(); break;

            case "undo": case "redo": case "bold": if (sharedEditor != null && Keyboard.FocusedElement is not System.Windows.Controls.Primitives.TextBoxBase) { e.Handled = true; _ = sharedEditor.Command(action); } break;
        }

        if (e.Key == Key.Escape && focusMode) { e.Handled = true; ToggleFocus(); }
    }
    private void HomeClick(object sender, RoutedEventArgs e) => Navigate("home");
    private void AllClick(object sender, RoutedEventArgs e) => Navigate("all");
    private void StarClick(object sender, RoutedEventArgs e) => Navigate("star");
    private void ArchiveClick(object sender, RoutedEventArgs e) => Navigate("archive");
    private void TrashClick(object sender, RoutedEventArgs e) => Navigate("trash");
    private void NewNotebookClick(object sender, RoutedEventArgs e) => NotebookDialog();
    private void NewNoteClick(object sender, RoutedEventArgs e) => AddNote();
    private void SettingsClick(object sender, RoutedEventArgs e) => SettingsDialog();
    private void SyncClick(object sender, RoutedEventArgs e) => ShowCloudSaves();
    private void ImportClick(object sender, RoutedEventArgs e) => _ = ImportFiles();
    private MenuItem MenuItem(string title, Action action)=>DesktopNativeUi.Item(title,action);
    private void ToggleFocus() { focusMode = !focusMode; Shell.ColumnDefinitions[0].Width = new GridLength(focusMode ? 0 : 270); if (Body.Content is Grid grid && grid.ColumnDefinitions.Count > 1) grid.ColumnDefinitions[0].Width = new GridLength(focusMode ? 0 : 285); }
}
internal sealed class NoteRow(Note note) : INotifyPropertyChanged
{
    public Note Note { get; private set; } = note;
    public string Title => Note.Title;
    public bool IsSelecting {get;private set;}
    public void SetSelecting(bool value){IsSelecting=value;PropertyChanged?.Invoke(this,new(nameof(IsSelecting)));}
    public bool IsPinned=>Note.Pinned;
    public string PinIcon=>Note.Pinned?"PinFilled":"Pin";
    public string PinLabel=>Note.Pinned?"取消置顶":"置顶记录";
    public string Snippet => (Note.Password.Length > 0 || Note.Username.Length > 0 ? Note.Username : RecordTools.Plain(PromptVariables.Display(Note))).Truncate(82);
    public string Meta => RecordTools.Relative(Note.UpdatedAt);
    public event PropertyChangedEventHandler? PropertyChanged;
    public void Replace(Note value) { Note = value; PropertyChanged?.Invoke(this, new(null)); }
}
internal static class Strings { public static string Truncate(this string value, int length) => value.Length <= length ? value : value[..length] + "…"; }
