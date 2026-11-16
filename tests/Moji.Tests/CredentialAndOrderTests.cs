using Moji.Core;
using Xunit;
namespace Moji.Tests;
public class CredentialAndOrderTests
{
    [Fact] public void CustomPropertiesSurviveHistoryLocalSerializationAndGlobalSaves()
    {
        var book=new Notebook{Mode=NoteMode.Password};var field=new CredentialField{Name="账户地区",Value="新加坡"};var note=new Note{NotebookId=book.Id,Fields=[field]};var edited=note.Apply(note with{Fields=[field with{Value="香港"}]},true);
        var state=new Workspace{Notebooks=[book],Notes=[edited]};state.Validate();var restored=GameSaves.Apply(new Workspace(),Json.Deserialize<SyncPayload>(Json.Serialize(GameSaves.Capture(state))));
        Assert.Equal(2,restored.Schema);Assert.Equal("香港",restored.Notes.Single().Fields.Single().Value);Assert.Equal(field.Id,restored.Notes.Single().Fields.Single().Id);Assert.Equal("新加坡",restored.Notes.Single().History.Single().Fields.Single().Value);Assert.True(RecordTools.Matches(restored.Notes.Single(),"账户地区",NoteMode.Password));
        var legacy=Json.Deserialize<Note>("{\"title\":\"旧记录\"}");Assert.Empty(legacy.Fields);
    }
    [Fact] public void NotebookOrderIsSharedAndPreservesDeletedBooksAndNoteOwnership()
    {
        var a=new Notebook{Name="A"};var deleted=new Notebook{Name="D",DeletedAt=Clock.Now};var b=new Notebook{Name="B"};var c=new Notebook{Name="C"};var note=new Note{NotebookId=a.Id};var state=new Workspace{Notebooks=[a,deleted,b,c],Notes=[note]};
        var reordered=state.MoveNotebook(c.Id,a.Id);Assert.Equal(new[]{"C","D","A","B"},reordered.Notebooks.Select(n=>n.Name));Assert.Equal(a.Id,reordered.Notes.Single().NotebookId);
        var restored=GameSaves.Apply(new Workspace(),GameSaves.Capture(reordered));Assert.Equal(new[]{c.Id,a.Id,b.Id},restored.LiveBooks.Select(n=>n.Id));Assert.Same(deleted,reordered.Notebooks[1]);Assert.Equal(new[]{a.Id,b.Id,c.Id},restored.MoveNotebook(c.Id,b.Id,true).LiveBooks.Select(n=>n.Id));Assert.Same(state,state.MoveNotebook(deleted.Id,a.Id));
    }
    [Fact] public void InvalidCustomPropertyDataIsRejectedBeforeReplacingAWorkspace()
    {
        var book=new Notebook{Mode=NoteMode.Password};var f=new CredentialField{Name="属性",Value="值"};var state=new Workspace{Notebooks=[book],Notes=[new Note{NotebookId=book.Id,Fields=[f,f]}]};Assert.Throws<System.IO.InvalidDataException>(state.Validate);
    }
}
