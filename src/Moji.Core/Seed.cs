namespace Moji.Core;
public static class Seed
{
    public static Workspace Create() => new() { Notebooks = [
        new() { Name = "日常随记", Mode = NoteMode.Text, Color = "#75876D" },
        new() { Name = "灵感提示词", Mode = NoteMode.Prompt, Color = "#B2946E" },
        new() { Name = "私人密码库", Mode = NoteMode.Password, Color = "#8393AC" }
    ] };
}
public static class NoteCreation
{
    public static Note InNotebook(Workspace state, string notebookId)
    {
        var book = state.LiveBooks.FirstOrDefault(b => b.Id == notebookId) ?? throw new InvalidOperationException("请选择记事本");
        return new Note { NotebookId = book.Id, Title = book.Mode switch { NoteMode.Password => "新账号", NoteMode.Prompt => "新提示词", _ => "未命名记录" } };
    }
}
