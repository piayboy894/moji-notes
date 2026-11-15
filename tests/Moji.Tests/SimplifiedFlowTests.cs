using Moji.Core;
using Xunit;
namespace Moji.Tests;
public class SimplifiedFlowTests
{
    [Fact] public void FreshSpaceHasNoSamplesOrPresetRepository() { var w=Seed.Create(); Assert.Empty(w.Notes); Assert.Equal("",w.Sync.Repository); Assert.Equal(3,w.LiveBooks.Count()); }
    [Fact] public void CreationNeverChoosesAnotherNotebookOrCreatesTemplates()
    {
        var w=Seed.Create(); var second=new Notebook{Mode=NoteMode.Prompt,Name="Another prompt book"}; w=w with{Notebooks=[..w.Notebooks,second]};
        foreach(var b in w.LiveBooks){var n=NoteCreation.InNotebook(w,b.Id);Assert.Equal(b.Id,n.NotebookId);Assert.Empty(n.Content);Assert.Empty(n.Password);}
        Assert.Throws<InvalidOperationException>(()=>NoteCreation.InNotebook(w,"home")); Assert.Empty(w.Notes);
        Assert.Throws<InvalidOperationException>(()=>NoteCreation.InNotebook(w with{Notebooks=[second with{DeletedAt=Clock.Now}]},second.Id));
    }
    [Fact] public void ShortcutsPersistValidateConflictsAndDisableOldKeys()
    {
        var p=new Preferences{Shortcuts=new(){{"new","Ctrl+Shift+N"},{"search",""}}};
        p=Json.Deserialize<Preferences>(Json.Serialize(p));Assert.Null(Shortcuts.Resolve(p,"Ctrl+N"));Assert.Equal("new",Shortcuts.Resolve(p,"Ctrl+Shift+N"));Assert.Null(Shortcuts.Resolve(p,"Ctrl+K"));
        Assert.NotNull(Shortcuts.Error(p,"search","Ctrl+Shift+N"));Assert.NotNull(Shortcuts.Error(p,"new","N"));Assert.NotNull(Shortcuts.Error(p,"new","Ctrl+C"));Assert.Null(Shortcuts.Error(p,"new","F8"));
    }
    [Fact] public void RemovedTagsNeverAffectSearch() { var n=new Note{Title="Hello",Tags=["hidden-tag"]};Assert.False(RecordTools.Matches(n,"hidden-tag",NoteMode.Text));Assert.False(RecordTools.Matches(n,"#hidden-tag",NoteMode.Text)); }
}
public class ExplicitVariableTests
{
    [Fact] public void TypedBracesAreAlwaysLiteralAndOnlyInsertedTokensCount()
    {
        var note=new Note{Content="用户手打 {{名字}}，粘贴 {{另一段}}"};Assert.Empty(PromptVariables.Used(note));
        var variable=PromptVariables.Named(note,"名字");note=note with{Variables=[variable],Content=note.Content+"\n"+PromptVariables.Token(variable)};
        Assert.Single(PromptVariables.Used(note));Assert.Equal("用户手打 {{名字}}，粘贴 {{另一段}}\n张三",PromptVariables.Fill(note,new Dictionary<string,string>{{variable.Id,"张三"}}));
        Assert.Single(PromptVariables.Used(Json.Deserialize<Note>(Json.Serialize(note))));
    }
    [Fact] public void ValuesNeverRecursivelyTriggerMoreVariables()
    {
        var first=new PromptVariable{Name="first"};var second=new PromptVariable{Name="second"};var note=new Note{Variables=[first,second],Content=PromptVariables.Token(first)+" "+PromptVariables.Token(second)};
        var values=new Dictionary<string,string>{{first.Id,PromptVariables.Token(second)},{second.Id,"last"}};
        Assert.Equal(PromptVariables.Token(second)+" last",PromptVariables.Fill(note,values));
    }
}

public class VisibleSearchTests { [Xunit.Fact] public void RichTextSearchUsesVisibleWords(){Xunit.Assert.Equal("Hello world left|right #literal",Moji.Core.RecordTools.Plain("**Hello** world left\\|right \\#literal"));} }

public class UserMessageTests{[Xunit.Fact]public void RuntimeDetailsNeverReachUser(){var error=new InvalidOperationException("WindowInteropHelper.EnsureHandle ShowDialog Close");Xunit.Assert.Equal("操作未完成，请重试。",Moji.Core.UserMessages.For(error));Xunit.Assert.DoesNotContain("WindowInteropHelper",Moji.Core.UserMessages.For(error));}}

public class NotebookStyleTests{[Xunit.Fact]public void AppearanceSurvivesWholeWorkspaceSave(){var w=Moji.Core.Seed.Create();var b=w.Notebooks[0] with{Color="#6543C2",Icon="Star"};w=w with{Notebooks=[b,..w.Notebooks.Skip(1)]};var next=Moji.Core.GameSaves.Apply(Moji.Core.Seed.Create(),Moji.Core.GameSaves.Capture(w));Xunit.Assert.Equal(b.Color,next.Notebooks[0].Color);Xunit.Assert.Equal("Star",next.Notebooks[0].Icon);Xunit.Assert.Equal(b.Mode,next.Notebooks[0].Mode);}}
