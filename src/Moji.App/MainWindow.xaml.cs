using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
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
    private readonly ObservableCollection<NoteRow> noteRows = [];
    private ContentControl? editorHost;
    private RichTextBox? textEditor;
    private TextBox? titleBox;
    private TextBlock? editorStatus;
    private string renderedId = "";
    private string editingCredentialId = "";
    private string clipboardSecret = "";
    private long clipboardAt;
    private UpdatePackage? availableUpdate;
    private string? downloadedUpdate;
    private readonly UpdateService updater = new();
    public MainWindow(WorkspaceViewModel? viewModel = null, bool smoke = false)
    {
        InitializeComponent(); VM = viewModel ?? new(); VM.DataChanged += DataChanged; VM.Notice += Toast;
        VM.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(VM.SaveStatus)) SaveText.Text = VM.SaveStatus; };
        toastTimer.Tick += (_, _) => { ToastBorder.Visibility = Visibility.Collapsed; toastTimer.Stop(); };
        sessionTimer.Tick += (_, _) => SessionTick(); sessionTimer.Start();
        PreviewKeyDown += KeyDownHandler; Closing += ClosingHandler;
        Closed += (_,_) => { ClearSecretClipboard(); sessionTimer.Stop(); toastTimer.Stop(); VM.Dispose(); };
        Loaded += async (_, _) => { if (smoke) return; await OpenWorkspaceAsync(); };
    }
    private void SessionTick()
    {
        if (!VM.IsUnlocked) return;
        if (!string.IsNullOrEmpty(clipboardSecret) && Clock.Now - clipboardAt >= 30000) ClearSecretClipboard();

    }
    private void DataChanged(string kind)
    {
        if(kind is "open" or "replace"){richUndo.Clear();richRedo.Clear();}
        if (kind == "open")
        { AuthHost.Content = null; AuthHost.Visibility = Visibility.Collapsed; Shell.Visibility = Visibility.Visible; Ui.Theme(VM.State!.Preferences.Theme); RefreshShell(); var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) }; timer.Tick += async (_, _) => { timer.Stop(); if (VM.IsUnlocked) await CheckUpdates(false); }; timer.Start(); return; }
        if (kind == "edit") { foreach (var row in noteRows) { var n = VM.State!.Notes.FirstOrDefault(n => n.Id == row.Note.Id); if (n != null) row.Replace(n); } UpdateStats(); return; }
        RefreshShell();
    }
    public void Navigate(string view, string? selected = null, bool editCredential = false)
    { editingCredentialId = editCredential ? selected ?? "" : ""; VM.View = view; VM.Search = ""; VM.SelectedId = selected ?? ""; renderedId = ""; focusMode = false; Shell.ColumnDefinitions[0].Width = new GridLength(270); RefreshShell(); }
    public void RefreshShell()
    {
        if (VM.State == null) return; Ui.Theme(VM.State.Preferences.Theme); UpdateStats();
        BooksPanel.Children.Clear(); foreach (var book in VM.State.LiveBooks)
        {
            var button = Ui.Button("", () => Navigate(book.Id)); button.Style = (Style)FindResource("NavButton");
            var row = new DockPanel(); var count = Ui.Text(VM.State.LiveNotes.Count(n => n.NotebookId == book.Id && !n.Archived).ToString(), 10); count.Foreground = Ui.Resource("Sub"); count.HorizontalAlignment = HorizontalAlignment.Right; DockPanel.SetDock(count, Dock.Right); row.Children.Add(count);
            row.Children.Add(new NativeIcon { Kind = NativeIcon.ForNotebook(book), Width = 22, Height = 22, Foreground = Ui.Resource("Sub"), Margin = new Thickness(0,0,13,0) }); row.Children.Add(Ui.Text(book.Name, 13)); button.Content = row; button.Background = VM.View == book.Id ? Ui.Resource("AccentLight") : Brushes.Transparent;
            var menu = new ContextMenu(); menu.Items.Add(MenuItem("编辑记事本", () => NotebookDialog(book))); menu.Items.Add(MenuItem("删除记事本", () => DeleteNotebook(book))); button.ContextMenu = menu;
            button.AllowDrop = true; button.DragOver += (_, e) => { e.Effects = e.Data.GetDataPresent("MojiNote") ? DragDropEffects.Move : DragDropEffects.None; e.Handled = true; }; button.Drop += (_, e) => { var id = e.Data.GetData("MojiNote") as string; var note = VM.State.Notes.FirstOrDefault(n => n.Id == id); if (note != null && VM.State.Notebooks.First(b => b.Id == note.NotebookId).Mode == book.Mode) { VM.EditNote(note.Id, n => n with { NotebookId = book.Id }); Navigate(book.Id, note.Id); } }; BooksPanel.Children.Add(button);
        }
        HomeNav.Background = VM.View == "home" ? Ui.Resource("AccentLight") : Brushes.Transparent; AllNav.Background = VM.View == "all" ? Ui.Resource("AccentLight") : Brushes.Transparent; StarNav.Background = VM.View == "star" ? Ui.Resource("AccentLight") : Brushes.Transparent;
        PageTitle.Text = ViewTitle(); if (VM.View == "home") RenderDashboard(); else RenderWorkspace();
    }
    private UIElement NavContent(string kind, string title, int? count = null)
    {
        var row = new DockPanel();
        if (count != null) { var number = Ui.Text(count.Value.ToString(), 11); number.Foreground = Ui.Resource("Sub"); DockPanel.SetDock(number, Dock.Right); row.Children.Add(number); }
        row.Children.Add(new NativeIcon { Kind = kind, Width = 22, Height = 22, Foreground = Ui.Resource("Sub"), Margin = new Thickness(0,0,14,0) }); row.Children.Add(Ui.Text(title, 13)); return row;
    }
    private void UpdateStats()
    {
        if (VM.State == null) return;
        HomeNav.Content = NavContent("Dashboard", "工作台");
        AllNav.Content = NavContent("Document", "全部记录", VM.State.LiveNotes.Count(n => !n.Archived));
        StarNav.Content = NavContent("Star", "我的收藏", VM.State.LiveNotes.Count(n => n.Favorite && !n.Archived));
        TrashNav.Content = NavContent("Trash", "回收站", VM.State.Notes.Count(n => n.DeletedAt != null && n.PurgedAt == null));
        SyncButton.Content = "云存档  ↗";
        SaveText.Text = VM.SaveStatus;
    }
    private string ViewTitle() => VM.State?.LiveBooks.FirstOrDefault(b => b.Id == VM.View)?.Name ?? VM.View switch { "home" => "工作台", "star" => "我的收藏", "archive" => "归档记录", "trash" => "回收站", _ => "全部记录" };
    internal Note? CurrentNote() => VM.State?.Notes.FirstOrDefault(n => n.Id == VM.SelectedId);
    public void Toast(string message) { ToastText.Text = message; ToastBorder.Visibility = Visibility.Visible; toastTimer.Stop(); toastTimer.Start(); }
    public void Copy(string value, bool secret = false)
    { try { Clipboard.SetText(value); if (secret) { clipboardSecret = value; clipboardAt = Clock.Now; } Toast(secret ? "已复制，30 秒后清除剪贴板" : "已复制"); } catch { Toast("复制失败，剪贴板正被其他应用使用"); } }
    private void ClearSecretClipboard() { try { if (Clipboard.ContainsText() && Clipboard.GetText() == clipboardSecret) Clipboard.Clear(); } catch { } clipboardSecret = ""; }
    private void ClosingHandler(object? sender, CancelEventArgs e)
    {
        if(closing)return;e.Cancel=true;if(closePending)return;closePending=true;IsEnabled=false;
        // Even a completed SaveAsync must resume after WPF has left its Closing callback.
        Dispatcher.BeginInvoke(new Action(async()=>{
            try{await VM.SaveAsync();closing=true;closePending=false;Close();}
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
        var action = Shortcuts.Resolve(VM.State!.Preferences, KeyChord(e));
        switch (action) {
            case "search": e.Handled = true; SearchDialog(); break;
            case "new": e.Handled = true; AddNote(); break;
            case "cloud": e.Handled = true; ShowCloudSaves(); break;
            case "save": e.Handled = true; if (CurrentNote() is { } n) { VM.Checkpoint(n.Id); _ = VM.SaveAsync(); Toast("当前版本已保存"); } break;
            case "focus": e.Handled = true; ToggleFocus(); break;
            case "find": case "replace": if (textEditor != null) { e.Handled = true; FindReplaceDialog(); } break;
            case "undo": if (textEditor?.IsKeyboardFocusWithin == true) { e.Handled = true; RichUndo(); } break;
            case "redo": if (textEditor?.IsKeyboardFocusWithin == true) { e.Handled = true; RichUndo(true); } break;
            case "bold": if (textEditor?.IsKeyboardFocusWithin == true) { e.Handled = true; EditingCommands.ToggleBold.Execute(null,textEditor); } break;
        }
        if (!e.Handled && textEditor?.IsKeyboardFocusWithin == true && Shortcuts.Actions.Any(a => a.Default == KeyChord(e) && Shortcuts.Binding(VM.State.Preferences,a.Id) != a.Default)) e.Handled = true;
        if (e.Key == Key.Escape && focusMode) { e.Handled = true; ToggleFocus(); }
    }
    private void HomeClick(object sender, RoutedEventArgs e) => Navigate("home");
    private void AllClick(object sender, RoutedEventArgs e) => Navigate("all");
    private void StarClick(object sender, RoutedEventArgs e) => Navigate("star");
    private void ArchiveClick(object sender, RoutedEventArgs e) => Navigate("archive");
    private void TrashClick(object sender, RoutedEventArgs e) => Navigate("trash");
    private void SearchClick(object sender, RoutedEventArgs e) => SearchDialog();
    private void NewNotebookClick(object sender, RoutedEventArgs e) => NotebookDialog();
    private void NewNoteClick(object sender, RoutedEventArgs e) => AddNote();
    private void SettingsClick(object sender, RoutedEventArgs e) => SettingsDialog();
    private void SyncClick(object sender, RoutedEventArgs e) => ShowCloudSaves();
    private void ImportClick(object sender, RoutedEventArgs e) => _ = ImportFiles();
    private MenuItem MenuItem(string title, Action action) { var item = new MenuItem { Header = title }; item.Click += (_, _) => action(); return item; }
    private void ToggleFocus() { focusMode = !focusMode; Shell.ColumnDefinitions[0].Width = new GridLength(focusMode ? 0 : 270); if (Body.Content is Grid grid && grid.ColumnDefinitions.Count > 1) grid.ColumnDefinitions[0].Width = new GridLength(focusMode ? 0 : 285); }
}
internal sealed class NoteRow(Note note) : INotifyPropertyChanged
{
    public Note Note { get; private set; } = note;
    public string Title => Note.Title;
    public string Snippet => (Note.Password.Length > 0 || Note.Username.Length > 0 ? Note.Username : RecordTools.Plain(PromptVariables.Display(Note))).Truncate(82);
    public string Meta => RecordTools.Relative(Note.UpdatedAt);
    public event PropertyChangedEventHandler? PropertyChanged;
    public void Replace(Note value) { Note = value; PropertyChanged?.Invoke(this, new(null)); }
}
internal static class Strings { public static string Truncate(this string value, int length) => value.Length <= length ? value : value[..length] + "…"; }
