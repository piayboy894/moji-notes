using System.Text.RegularExpressions;
namespace Moji.Core;
public static class Shortcuts
{
    public static readonly (string Id, string Label, string Default)[] Actions = [
        ("search", "全局搜索", "Ctrl+K"), ("new", "新建记录", "Ctrl+N"),
        ("save", "保存历史版本", "Ctrl+S"), ("cloud", "云存档", "Ctrl+Shift+S"),
        ("undo", "撤销", "Ctrl+Z"), ("redo", "重做", "Ctrl+Y"),
        ("bold", "加粗", "Ctrl+B"), ("focus", "专注模式", "F11")
    ];
    public static string Binding(Preferences preferences, string action) => preferences.Shortcuts.TryGetValue(action, out var value) ? value : Actions.First(a => a.Id == action).Default;
    public static string? Resolve(Preferences preferences, string chord) => string.IsNullOrEmpty(chord) ? null : Actions.FirstOrDefault(a => Binding(preferences, a.Id) == chord).Id;
    public static string? Error(Preferences preferences, string action, string chord)
    {
        if (chord.Length == 0) return null;
        if (!Regex.IsMatch(chord, @"^(?:(?:Ctrl\+)(?:Alt\+)?(?:Shift\+)?|(?:Alt\+)(?:Shift\+)?)(?:[A-Z0-9]|F(?:[1-9]|1[0-2]))$|^F(?:[1-9]|1[0-2])$")) return "请使用 Ctrl / Alt 组合键或 F1–F12";
        if (new[] { "Ctrl+C", "Ctrl+V", "Ctrl+X", "Ctrl+A", "Alt+F4" }.Contains(chord)) return "此组合用于系统或基础编辑";
        var conflict = Actions.FirstOrDefault(a => a.Id != action && Binding(preferences, a.Id) == chord);
        return conflict.Id == null ? null : "已用于「" + conflict.Label + "」";
    }
}
