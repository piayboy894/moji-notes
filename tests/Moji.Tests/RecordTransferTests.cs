using Moji.Core;
using Xunit;

namespace Moji.Tests;
public class RecordTransferTests
{
    private static Workspace State()
    {
        var a=new Notebook{Name="A",Mode=NoteMode.Text};var b=new Notebook{Name="B",Mode=NoteMode.Text};var p=new Notebook{Name="Passwords",Mode=NoteMode.Password};
        return new(){Notebooks=[a,b,p],Notes=[new(){NotebookId=a.Id,Title="Original",Content="<p>Body</p>",Fields=[new(){Name="Server",Value="Stored"}],Pinned=true,Favorite=true,CreatedAt=1,UpdatedAt=2,History=[new(){Content="Before"}]},new(){NotebookId=p.Id,Title="Account",Username="test",Password="test-only"}]};
    }
    [Fact] public void CutWaitsForPasteThenPreservesIdentityHistoryAndEditTime()
    {
        var w=State();var n=w.Notes[0];var clip=RecordTransfer.Capture(w,[n.Id],true);Assert.Equal(w.Notebooks[0].Id,w.Notes[0].NotebookId);
        var edited=w with{Notes=[n with{Content="Last input"},w.Notes[1]]};var result=RecordTransfer.Apply(edited,clip,w.Notebooks[1].Id);var moved=result.State.Notes[0];
        Assert.Equal(n.Id,moved.Id);Assert.Equal("Last input",moved.Content);Assert.Equal(n.Fields,moved.Fields);Assert.Equal(n.History,moved.History);Assert.Equal(n.UpdatedAt,moved.UpdatedAt);Assert.Equal(n.CreatedAt,moved.CreatedAt);Assert.True(moved.Pinned);Assert.NotEqual(n.Revision,moved.Revision);result.State.Validate();
    }
    [Theory] [InlineData(true)] [InlineData(false)] public void IncompatibleBatchIsAtomic(bool cut)
    {var w=State();var original=w.Notes.ToArray();var bundle=RecordTransfer.Capture(w,w.Notes.Select(n=>n.Id),cut);Assert.Throws<RecordTransferException>(()=>RecordTransfer.Apply(w,bundle,w.Notebooks[1].Id));Assert.Equal(original,w.Notes);}
    [Fact] public void CopyCreatesIndependentCompleteRecordsWithUniqueNames()
    {
        var w=State();var original=w.Notes[0];var clip=RecordTransfer.Capture(w,[original.Id],false);var once=RecordTransfer.Apply(w,clip,w.Notebooks[1].Id);var twice=RecordTransfer.Apply(once.State,clip,w.Notebooks[1].Id);
        var a=twice.State.Notes.First(n=>n.Id==once.Ids[0]);var b=twice.State.Notes.First(n=>n.Id==twice.Ids[0]);Assert.NotEqual(a.Id,b.Id);Assert.NotEqual(a.Title,b.Title);Assert.Equal(original.Fields,a.Fields);Assert.Equal(original.Content,a.Content);Assert.False(a.Pinned);Assert.False(a.Favorite);Assert.Empty(a.History);Assert.Equal(original,w.Notes[0]);twice.State.Validate();
    }
    [Fact] public void StaleCutCannotResurrectDeletedRecordsOrCrossWorkspace()
    {
        var w=State();var bundle=RecordTransfer.Capture(w,[w.Notes[0].Id],true);
        Assert.Throws<RecordTransferException>(()=>RecordTransfer.Apply(w with{Id=Clock.Id},bundle,w.Notebooks[1].Id));
        Assert.Throws<RecordTransferException>(()=>RecordTransfer.Apply(w with{Notes=[w.Notes[0] with{DeletedAt=1},w.Notes[1]]},bundle,w.Notebooks[1].Id));
        Assert.Throws<RecordTransferException>(()=>RecordTransfer.Apply(w,bundle,"absent"));
    }
    [Fact] public void SameNotebookMoveDoesNotCreateRevisionsOrCopies()
    {var w=State();var n=w.Notes[0];var result=RecordTransfer.Apply(w,RecordTransfer.Capture(w,[n.Id],true),n.NotebookId);Assert.Same(w,result.State);Assert.Single(result.Ids);}
    [Fact] public void RecordShortcutsCanBeCustomizedWithoutStealingTextKeys()
    {var p=new Preferences();Assert.Equal("recordCopy",Shortcuts.Resolve(p,"Ctrl+C"));Assert.Null(Shortcuts.Error(p,"recordCopy","Ctrl+C"));Assert.NotNull(Shortcuts.Error(p,"search","Ctrl+C"));Assert.Null(Shortcuts.Error(p,"recordCopy","Ctrl+Shift+C"));}
}
