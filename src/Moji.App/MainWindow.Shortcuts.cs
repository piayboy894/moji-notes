using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Moji.Core;
namespace Moji.App;
public partial class MainWindow
{
    internal static string KeyChord(KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        var name = key is >= Key.A and <= Key.Z or >= Key.F1 and <= Key.F12 ? key.ToString() : key is >= Key.D0 and <= Key.D9 ? ((int)key - (int)Key.D0).ToString() : "";
        if (name.Length == 0 || Keyboard.Modifiers.HasFlag(ModifierKeys.Windows)) return "";
        var mods = Keyboard.Modifiers;
        return (mods.HasFlag(ModifierKeys.Control) ? "Ctrl+" : "") + (mods.HasFlag(ModifierKeys.Alt) ? "Alt+" : "") + (mods.HasFlag(ModifierKeys.Shift) ? "Shift+" : "") + name;
    }
    private void RenderShortcuts(StackPanel panel, Action refresh)
    {
        panel.Children.Add(Ui.Text("快捷键", 22, null, true));
        foreach (var action in Shortcuts.Actions)
        {
            var row = new DockPanel { Margin = new Thickness(0,18,0,0) };
            var current = Shortcuts.Binding(VM.State!.Preferences, action.Id);
            var button = Ui.Button(current.Length == 0 ? "未设置" : current, () => EditShortcut(action.Id, action.Label, refresh));
            button.MinWidth = 160; DockPanel.SetDock(button, Dock.Right); row.Children.Add(button); row.Children.Add(Ui.Text(action.Label, 12)); panel.Children.Add(row);
        }
        panel.Children.Add(Ui.Spaced(Ui.Button("恢复默认", () => { VM.Change(w => w with { Preferences = w.Preferences with { Shortcuts = [] } }, "edit"); refresh(); }), new Thickness(0,25,0,0)));
    }
    private void EditShortcut(string action, string label, Action refresh)
    {
        var binding = Shortcuts.Binding(VM.State!.Preferences, action); var field = Ui.Input(binding); field.IsReadOnly = true;
        var status = Ui.Text("", 11); var panel = Ui.Stack(Ui.Field("按下新的组合键", field), status); panel.Margin = new Thickness(25);
        Window? dialog = null; var save = Ui.Button("保存", () => { VM.Change(w => w with { Preferences = w.Preferences with { Shortcuts = new(w.Preferences.Shortcuts) { [action] = binding } } }, "edit"); dialog!.Close(); refresh(); }, true);
        field.PreviewKeyDown += (_, e) => { e.Handled = true; var chord = KeyChord(e); if (chord.Length == 0) return; binding = chord; field.Text = chord; var error = Shortcuts.Error(VM.State!.Preferences, action, chord); status.Text = error ?? ""; save.IsEnabled = error == null; };
        panel.Children.Add(Ui.Row(Ui.Button("清除", () => { binding = ""; field.Text = ""; status.Text = ""; save.IsEnabled = true; }), Ui.Spaced(save, new Thickness(12,15,0,0))));
        dialog = Ui.Dialog(this, label, panel,450); dialog.Loaded += (_,_) => field.Focus(); dialog.ShowDialog();
    }
}
