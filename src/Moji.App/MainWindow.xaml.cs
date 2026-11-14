using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using ICSharpCode.AvalonEdit;
using Moji.Core;
using Microsoft.Win32;

namespace Moji.App;
public partial class MainWindow : Window
{
    public WorkspaceViewModel VM { get; }
    private readonly DispatcherTimer toastTimer = new() { Interval = TimeSpan.FromSeconds(4) };
    private readonly DispatcherTimer sessionTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly DispatcherTimer previewTimer = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private long lastInput = Clock.Now;
    private long lastAutoSync;
    private bool locking, closing, loadingEditor, focusMode;
    private ListBox? noteList;
    private readonly ObservableCollection<NoteRow> noteRows = [];
    private ContentControl? editorHost;
    private TextEditor? textEditor;
    private RichTextBox? preview;
    private TextBox? titleBox;
    private TextBlock? editorStatus;
    private TextBlock? otpText;
    private string renderedId = "";
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
        previewTimer.Tick += (_, _) => { previewTimer.Stop(); RefreshPreview(); };
        sessionTimer.Tick += async (_, _) => await SessionTick(); sessionTimer.Start();
        InputManager.Current.PreProcessInput += InputReceived;
        SystemEvents.SessionSwitch += SessionSwitch; SystemEvents.PowerModeChanged += PowerModeChanged;
        PreviewKeyDown += KeyDownHandler; Closing += ClosingHandler;
        Loaded += async (_, _) => { if (smoke) return; await ShowAuthAsync(); };
    }
    private void InputReceived(object sender, PreProcessInputEventArgs e) { if (e.StagingItem.Input is KeyEventArgs or MouseButtonEventArgs or MouseWheelEventArgs) lastInput = Clock.Now; }
    private void SessionSwitch(object sender, SessionSwitchEventArgs e) { if (e.Reason == SessionSwitchReason.SessionLock) Dispatcher.InvokeAsync(async () => await LockWorkspace()); }
    private void PowerModeChanged(object sender, PowerModeChangedEventArgs e) { if (e.Mode == PowerModes.Suspend) Dispatcher.InvokeAsync(async () => await LockWorkspace()); }
    private async Task SessionTick()
    {
        if (!VM.IsUnlocked || locking) return;
        if (Clock.Now - lastInput >= VM.State!.Preferences.AutoLockMinutes * 60_000) { await LockWorkspace(); return; }
        if (!string.IsNullOrEmpty(clipboardSecret) && Clock.Now - clipboardAt >= 30000) ClearSecretClipboard();
        var note = CurrentNote();
        if (otpText != null && note?.Otp.Length > 0) { try { otpText.Text = RecordTools.Totp(note.Otp) + $"    {30 - DateTimeOffset.Now.ToUnixTimeSeconds() % 30}s"; } catch { otpText.Text = "密钥格式不正确"; } }
        if (VM.State.Preferences.AutoSync && !VM.Demo && !string.IsNullOrEmpty(VM.State.Sync.Token) && Clock.Now - lastAutoSync >= 300000) { lastAutoSync = Clock.Now; try { await VM.SyncAsync(); } catch (Exception e) { Toast(e.Message); } }
    }
    private void DataChanged(string kind)
    {
        if (kind == "lock") { Shell.Visibility = Visibility.Collapsed; AuthHost.Visibility = Visibility.Visible; Body.Content = null; noteRows.Clear(); textEditor = null; preview = null; titleBox = null; otpText = null; renderedId = ""; foreach (var window in OwnedWindows.Cast<Window>().ToList()) window.Close(); Ui.Theme("light"); _ = ShowAuthAsync(); return; }
        if (kind == "unlock")
        { AuthHost.Content = null; AuthHost.Visibility = Visibility.Collapsed; Shell.Visibility = Visibility.Visible; lastInput = Clock.Now; lastAutoSync = Clock.Now; Ui.Theme(VM.State!.Preferences.Theme); RefreshShell(); var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) }; timer.Tick += async (_, _) => { timer.Stop(); if (VM.IsUnlocked && !VM.Demo) await CheckUpdates(false); }; timer.Start(); return; }
        if (kind == "edit") { foreach (var row in noteRows) { var n = VM.State!.Notes.FirstOrDefault(n => n.Id == row.Note.Id); if (n != null) row.Replace(n); } UpdateStats(); return; }
        RefreshShell();
    }
    public void Navigate(string view, string? selected = null)
    { VM.View = view; VM.Search = ""; VM.SelectedId = selected ?? ""; renderedId = ""; focusMode = false; Shell.ColumnDefinitions[0].Width = new GridLength(270); RefreshShell(); }
    public void RefreshShell()
    {
        if (VM.State == null) return; Ui.Theme(VM.State.Preferences.Theme); UpdateStats();
        BooksPanel.Children.Clear(); foreach (var book in VM.State.LiveBooks)
        {
            var button = Ui.Button("", () => Navigate(book.Id)); button.Style = (Style)FindResource("NavButton");
            var row = new DockPanel(); var count = Ui.Text(VM.State.LiveNotes.Count(n => n.NotebookId == book.Id && !n.Archived).ToString(), 10); count.Foreground = Ui.Resource("Sub"); count.HorizontalAlignment = HorizontalAlignment.Right; DockPanel.SetDock(count, Dock.Right); row.Children.Add(count);
            row.Children.Add(new NativeIcon { Kind = NativeIcon.ForMode(book.Mode), Width = 18, Height = 18, Foreground = Ui.Resource("Sub"), Margin = new Thickness(0,0,13,0) }); row.Children.Add(Ui.Text(book.Name, 13)); button.Content = row; button.Background = VM.View == book.Id ? Ui.Resource("AccentLight") : Brushes.Transparent;
            var menu = new ContextMenu(); menu.Items.Add(MenuItem("编辑记事本", () => NotebookDialog(book))); menu.Items.Add(MenuItem("删除记事本", () => DeleteNotebook(book))); button.ContextMenu = menu;
            button.AllowDrop = true; button.DragOver += (_, e) => { e.Effects = e.Data.GetDataPresent("MojiNote") ? DragDropEffects.Move : DragDropEffects.None; e.Handled = true; }; button.Drop += (_, e) => { var id = e.Data.GetData("MojiNote") as string; var note = VM.State.Notes.FirstOrDefault(n => n.Id == id); if (note != null && VM.State.Notebooks.First(b => b.Id == note.NotebookId).Mode == book.Mode) { VM.EditNote(note.Id, n => n with { NotebookId = book.Id }); Navigate(book.Id, note.Id); } }; BooksPanel.Children.Add(button);
        }
        TagsPanel.Children.Clear(); foreach (var tag in VM.State.LiveNotes.SelectMany(n => n.Tags).Distinct().Order().Take(22)) { var b = Ui.Small("# " + tag, () => { Navigate("all"); VM.Search = "#" + tag; RenderWorkspace(); }); b.Margin = new Thickness(3); b.FontSize = 10; b.Padding = new Thickness(7,4,7,4); b.Foreground = Ui.Resource("Sub"); TagsPanel.Children.Add(b); }
        HomeNav.Background = VM.View == "home" ? Ui.Resource("AccentLight") : Brushes.Transparent; AllNav.Background = VM.View == "all" ? Ui.Resource("AccentLight") : Brushes.Transparent; StarNav.Background = VM.View == "star" ? Ui.Resource("AccentLight") : Brushes.Transparent;
        PageTitle.Text = ViewTitle(); if (VM.View == "home") RenderDashboard(); else RenderWorkspace();
    }
    private UIElement NavContent(string kind, string title, int? count = null)
    {
        var row = new DockPanel();
        if (count != null) { var number = Ui.Text(count.Value.ToString(), 11); number.Foreground = Ui.Resource("Sub"); DockPanel.SetDock(number, Dock.Right); row.Children.Add(number); }
        row.Children.Add(new NativeIcon { Kind = kind, Width = 19, Height = 19, Foreground = Ui.Resource("Sub"), Margin = new Thickness(0,0,14,0) }); row.Children.Add(Ui.Text(title, 13)); return row;
    }
    private void UpdateStats()
    {
        if (VM.State == null) return;
        HomeNav.Content = NavContent("Dashboard", "工作台");
        AllNav.Content = NavContent("Document", "全部记录", VM.State.LiveNotes.Count(n => !n.Archived));
        StarNav.Content = NavContent("Star", "我的收藏", VM.State.LiveNotes.Count(n => n.Favorite && !n.Archived));
        TrashNav.Content = NavContent("Trash", "回收站", VM.State.Notes.Count(n => n.DeletedAt != null && n.PurgedAt == null));
        CloudStatus.Text = VM.Demo ? "演示空间" : VM.State.Sync.LastSync > 0 ? "已同步 · " + RecordTools.Relative(VM.State.Sync.LastSync) : "本地私人空间";
        SyncButton.Content = VM.State.Sync.LastSync > 0 ? "立即同步  ↻" : "连接 GitHub  ↗";
        SaveText.Text = VM.Demo ? "演示模式" : VM.SaveStatus;
        ProfileName.Text = VM.Demo ? "演示空间" : "我的私人空间";
    }
    private string ViewTitle() => VM.State?.LiveBooks.FirstOrDefault(b => b.Id == VM.View)?.Name ?? VM.View switch { "home" => "我的工作台", "star" => "我的收藏", "archive" => "归档记录", "trash" => "回收站", _ => "全部记录" };
    internal static string ModeGlyph(NoteMode mode) => mode switch { NoteMode.Text => "▤", NoteMode.Prompt => "✧", _ => "⚿" };
    internal Note? CurrentNote() => VM.State?.Notes.FirstOrDefault(n => n.Id == VM.SelectedId);
    public void Toast(string message) { ToastText.Text = message; ToastBorder.Visibility = Visibility.Visible; toastTimer.Stop(); toastTimer.Start(); }
    public void Copy(string value, bool secret = false)
    { try { Clipboard.SetText(value); if (secret) { clipboardSecret = value; clipboardAt = Clock.Now; } Toast(secret ? "已复制，30 秒后清除剪贴板" : "已复制"); } catch { Toast("复制失败，剪贴板正被其他应用使用"); } }
    private void ClearSecretClipboard() { try { if (Clipboard.ContainsText() && Clipboard.GetText() == clipboardSecret) Clipboard.Clear(); } catch { } clipboardSecret = ""; }
    private async Task LockWorkspace()
    { if (!VM.IsUnlocked || locking) return; locking = true; IsEnabled = false; try { await VM.LockAsync(); ClearSecretClipboard(); } catch (Exception e) { Toast("锁定前保存失败：" + e.Message); } finally { IsEnabled = true; locking = false; } }
    private async void ClosingHandler(object? sender, CancelEventArgs e)
    { if (closing) return; e.Cancel = true; IsEnabled = false; try { await VM.SaveAsync(); closing = true; ClearSecretClipboard(); sessionTimer.Stop(); previewTimer.Stop(); InputManager.Current.PreProcessInput -= InputReceived; SystemEvents.SessionSwitch -= SessionSwitch; SystemEvents.PowerModeChanged -= PowerModeChanged; VM.Dispose(); Close(); } catch (Exception error) { IsEnabled = true; Toast("保存失败，程序已保留打开。" + error.Message); } }
    private void KeyDownHandler(object sender, KeyEventArgs e)
    { if (!VM.IsUnlocked) return; var ctrl = Keyboard.Modifiers.HasFlag(ModifierKeys.Control); if (ctrl && e.Key == Key.K) { e.Handled = true; SearchDialog(); } else if (ctrl && e.Key == Key.N) { e.Handled = true; AddNote(); } else if (ctrl && e.Key == Key.L && Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) { e.Handled = true; _ = LockWorkspace(); } else if (ctrl && e.Key == Key.S) { e.Handled = true; if (CurrentNote() is { } n) { VM.Checkpoint(n.Id); _ = VM.SaveAsync(); Toast("当前版本已保存"); } } else if (e.Key == Key.F11) { e.Handled = true; ToggleFocus(); } else if (e.Key == Key.Escape && focusMode) { ToggleFocus(); } }
    private void HomeClick(object sender, RoutedEventArgs e) => Navigate("home");
    private void AllClick(object sender, RoutedEventArgs e) => Navigate("all");
    private void StarClick(object sender, RoutedEventArgs e) => Navigate("star");
    private void ArchiveClick(object sender, RoutedEventArgs e) => Navigate("archive");
    private void TrashClick(object sender, RoutedEventArgs e) => Navigate("trash");
    private void SearchClick(object sender, RoutedEventArgs e) => SearchDialog();
    private void NewNotebookClick(object sender, RoutedEventArgs e) => NotebookDialog();
    private void NewNoteClick(object sender, RoutedEventArgs e) { if (VM.View == "home") TemplateDialog(); else AddNote(); }
    private void SettingsClick(object sender, RoutedEventArgs e) => SettingsDialog();
    private void LockClick(object sender, RoutedEventArgs e) => _ = LockWorkspace();
    private async void SyncClick(object sender, RoutedEventArgs e) { if (VM.State?.Sync.Token.Length == 0 || VM.Demo) { SettingsDialog("sync"); return; } SyncButton.IsEnabled = false; try { await VM.SyncAsync(); } catch (Exception error) { Toast(error.Message); } finally { SyncButton.IsEnabled = true; } }
    private void ImportClick(object sender, RoutedEventArgs e) => _ = ImportFiles();
    private MenuItem MenuItem(string title, Action action) { var item = new MenuItem { Header = title }; item.Click += (_, _) => action(); return item; }
    private void ToggleFocus() { focusMode = !focusMode; Shell.ColumnDefinitions[0].Width = new GridLength(focusMode ? 0 : 270); if (Body.Content is Grid grid && grid.ColumnDefinitions.Count > 1) grid.ColumnDefinitions[0].Width = new GridLength(focusMode ? 0 : 285); }
}
internal sealed class NoteRow(Note note) : INotifyPropertyChanged
{
    public Note Note { get; private set; } = note;
    public string Title => (Note.Pinned ? "⌖  " : "") + (Note.Favorite ? "☆  " : "") + Note.Title;
    public string Snippet => (Note.Password.Length > 0 || Note.Username.Length > 0 ? Note.Username : RecordTools.Plain(Note.Content)).Truncate(82);
    public string Meta => RecordTools.Relative(Note.UpdatedAt) + (Note.Tags.Count > 0 ? "     #" + Note.Tags[0] : "");
    public event PropertyChangedEventHandler? PropertyChanged;
    public void Replace(Note value) { Note = value; PropertyChanged?.Invoke(this, new(null)); }
}
internal static class Strings { public static string Truncate(this string value, int length) => value.Length <= length ? value : value[..length] + "…"; }
