using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Moji.Core;

namespace Moji.App;
public partial class MainWindow
{
    private void ShowCloudSaves()
    {
        if (VM.State == null) return;
        BuildCloudSavesWindow().ShowDialog();
    }
    internal Window BuildCloudSavesWindow()
    {
        var panel = new DockPanel { Margin = new Thickness(28) };
        var window = Ui.Dialog(this, "云存档", panel, 860, 650);
        SaveCatalog? catalog = null; SaveEntry? selected = null; var busy = false;
        var header = new StackPanel(); DockPanel.SetDock(header, Dock.Top); panel.Children.Add(header);
        var title = new DockPanel { Margin = new Thickness(0,0,0,21) };
        var settings = Ui.Small("连接设置", () => { window.Close(); SettingsDialog("sync"); }); DockPanel.SetDock(settings, Dock.Right); title.Children.Add(settings);
        title.Children.Add(Ui.Stack(Ui.Text("云存档", 23, null, true), Ui.Spaced(Ui.Text($"全部记事本 · {VM.State!.LiveBooks.Count()} 本 · {VM.State.LiveNotes.Count()} 条记录", 12), new Thickness(0,10,0,0)))); header.Children.Add(title);
        var actions = new WrapPanel { Margin = new Thickness(0,0,0,16) };
        var create = Ui.Small("新建存档", () => { }); var overwrite = Ui.Small("覆盖存档", () => { }); var read = Ui.Small("读取存档", () => { }); var delete = Ui.Small("删除存档", () => { }); var refresh = Ui.Small("刷新列表", () => { });
        foreach (var button in new[]{create,overwrite,read,delete,refresh}) { button.Margin = new Thickness(0,0,9,0); actions.Children.Add(button); }
        header.Children.Add(actions);
        var status = Ui.Text("", 11); status.TextWrapping = TextWrapping.Wrap; status.Margin = new Thickness(0,0,0,12); header.Children.Add(status);
        var grid = new Grid(); panel.Children.Add(grid); var list = new ListBox { Margin = new Thickness(-5,0,-5,0) }; grid.Children.Add(list);
        var empty = Ui.Text("点击刷新读取云存档", 13); empty.HorizontalAlignment = HorizontalAlignment.Center; empty.VerticalAlignment = VerticalAlignment.Center; empty.Foreground = Ui.Resource("Sub"); grid.Children.Add(empty);
        void Enable()
        {
            create.IsEnabled = catalog != null && !busy;
            overwrite.IsEnabled = read.IsEnabled = delete.IsEnabled = selected != null && !busy;
            refresh.IsEnabled = !busy; settings.IsEnabled = list.IsEnabled = !busy;
        }
        void Render(string? keep = null)
        {
            list.Items.Clear(); selected = null;
            foreach (var entry in catalog?.Entries ?? [])
            {
                var row = new DockPanel(); var at = Ui.Text(entry.SavedAt > 0 ? DateTimeOffset.FromUnixTimeMilliseconds(entry.SavedAt).LocalDateTime.ToString("yyyy-MM-dd HH:mm") : "旧版单文件存档", 11); at.Foreground = Ui.Resource("Sub"); DockPanel.SetDock(at, Dock.Right); row.Children.Add(at);
                var labels = Ui.Stack(Ui.Text(entry.Name, 15, null, true), Ui.Spaced(Ui.Text($"{entry.BookCount} 本 · {entry.NoteCount} 条 · {entry.Platform}", 11), new Thickness(0,10,0,0))); row.Children.Add(labels);
                var item = new ListBoxItem { Content = row, Tag = entry }; list.Items.Add(item); if (entry.Id == keep) list.SelectedItem = item;
            }
            empty.Text = catalog == null ? "点击刷新读取云存档" : "还没有云存档"; empty.Visibility = list.Items.Count == 0 ? Visibility.Visible : Visibility.Collapsed; Enable();
        }
        async Task Run(Func<Task> action)
        {
            busy = true; status.Text = "正在处理…"; Enable();
            try { await action(); status.Foreground = Ui.Resource("Sub"); }
            catch (Exception error) { status.Text = error.Message; status.Foreground = Ui.Brush("#B17458"); }
            finally { busy = false; Enable(); }
        }
        string? AskName(SaveEntry? previous)
        {
            var input = Ui.Input(previous?.Name ?? "存档 " + DateTime.Now.ToString("MM-dd HH:mm")); input.MaxLength = 60; input.IsReadOnly = previous?.Legacy == true;
            var form = new StackPanel { Margin = new Thickness(25) }; form.Children.Add(Ui.Field("存档名称", input));
            var hint = Ui.Text(previous == null ? "保存本机全部记事本和记录。" : "用本机全部记事本和记录替换这份云存档。", 12); hint.Foreground = Ui.Resource("Sub"); form.Children.Add(hint);
            var dialog = Ui.Dialog(window, previous == null ? "新建存档" : "覆盖存档", form, 470);
            var submit = Ui.Button(previous == null ? "创建" : "确认覆盖", () => { if (!string.IsNullOrWhiteSpace(input.Text)) dialog.DialogResult = true; }, true); submit.Margin = new Thickness(0,22,0,0); submit.HorizontalAlignment = HorizontalAlignment.Right; form.Children.Add(submit);
            dialog.Loaded += (_,_) => { input.Focus(); input.SelectAll(); };
            return dialog.ShowDialog() == true ? input.Text.Trim() : null;
        }
        list.SelectionChanged += (_,_) => { selected = (list.SelectedItem as ListBoxItem)?.Tag as SaveEntry; Enable(); };
        refresh.Click += async (_,_) => await Run(async () => { catalog = await VM.ListCloudSavesAsync(); Render(); status.Text = "已刷新"; });
        create.Click += async (_,_) => { var name = AskName(null); if (name != null) await Run(async () => { catalog = await VM.WriteCloudSaveAsync(catalog!, name); Render(); status.Text = "存档已新建"; }); };
        overwrite.Click += async (_,_) => { var target = selected; if (target == null) return; var name = AskName(target); if (name != null) await Run(async () => { catalog = await VM.WriteCloudSaveAsync(catalog!, name, target); Render(target.Id); status.Text = "存档已覆盖"; }); };
        read.Click += async (_,_) => { var target = selected; if (target != null && Confirm("读取「"+target.Name+"」？", "将替换本机全部记事本和记录，不合并。")) await Run(async () => { await VM.ReadCloudSaveAsync(catalog!, target); status.Text = "存档已读取"; }); };
        delete.Click += async (_,_) => { var target = selected; if (target != null && Confirm("删除「"+target.Name+"」？", "删除这份云存档，本机内容保持不变。")) await Run(async () => { catalog = await VM.DeleteCloudSaveAsync(catalog!, target); Render(); status.Text = "存档已删除"; }); };
        window.Closing += (_,e) => { if (busy && VM.IsUnlocked) e.Cancel = true; }; Enable();
        return window;
    }
}
