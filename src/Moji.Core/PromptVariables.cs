using System.Text.RegularExpressions;
namespace Moji.Core;
public record PromptVariable
{
    public string Id { get; init; } = Clock.Id;
    public string Name { get; init; } = "";
}
public static class PromptVariables
{
    private static readonly Regex TokenPattern = new(@"!\[[^\]]*\]\(moji-variable:([a-f0-9]{32})\)",RegexOptions.Compiled);
    public static string Token(PromptVariable variable) => "![变量](moji-variable:"+variable.Id+")";
    public static PromptVariable Named(Note note,string name)
    { name=name.Trim();if(name.Length is <1 or >40 || name.Any(char.IsControl))throw new InvalidOperationException("变量名称需为 1–40 个字符");return note.Variables.FirstOrDefault(v=>v.Name==name)??new(){Name=name}; }
    public static List<PromptVariable> Used(Note note)
    { var ids=TokenPattern.Matches(note.Content).Select(m=>m.Groups[1].Value).ToHashSet();return note.Variables.Where(v=>ids.Contains(v.Id)).ToList(); }
    public static string Display(Note note) => Fill(note, note.Variables.ToDictionary(v=>v.Id,v=>v.Name));
    public static string PlainText(Note note,IReadOnlyDictionary<string,string> values,Func<string,string> render)
    {
        var placeholders=Used(note).ToDictionary(v=>v.Id,v=>"MOJIVAR"+v.Id+"MOJI");var plain=render(Fill(note,placeholders));
        return Regex.Replace(plain,@"MOJIVAR([a-f0-9]{32})MOJI",m=>values.TryGetValue(m.Groups[1].Value,out var value)?value:m.Value);
    }
    public static string Fill(Note note,IReadOnlyDictionary<string,string> values) => TokenPattern.Replace(note.Content,m=>{
        var v=note.Variables.FirstOrDefault(v=>v.Id==m.Groups[1].Value);return v!=null&&values.TryGetValue(v.Id,out var value)?value:m.Value;
    });
}
