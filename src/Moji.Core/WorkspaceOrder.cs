namespace Moji.Core;
public static class CredentialFields
{
    public static bool Valid(IReadOnlyList<CredentialField>? fields)=>fields!=null&&fields.Count<=128&&fields.All(f=>f!=null&&System.Text.RegularExpressions.Regex.IsMatch(f.Id??"","^[a-f0-9]{32}$")&&!string.IsNullOrWhiteSpace(f.Name)&&f.Name.Length<=80&&!f.Name.Any(char.IsControl)&&f.Value!=null&&f.Value.Length<=2_000_000)&&fields.Select(f=>f.Id).Distinct().Count()==fields.Count;
}
public static class WorkspaceOrder
{
    public static Workspace MoveNotebook(this Workspace state,string source,string target,bool after=false)
    {
        var live=state.LiveBooks.ToList();var item=live.FirstOrDefault(b=>b.Id==source);
        if(item==null||source==target||!live.Any(b=>b.Id==target))return state;
        live.Remove(item);var at=live.FindIndex(b=>b.Id==target)+(after?1:0);live.Insert(at,item);var index=0;
        return state with{Notebooks=state.Notebooks.Select(b=>b.DeletedAt==null?live[index++]:b).ToList()};
    }
}
