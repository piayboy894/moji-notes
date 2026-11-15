package app.moji.android
import app.moji.android.data.*
import org.junit.Assert.*
import org.junit.Test
class SimplifiedFlowTest {
    @Test fun freshSpaceHasNoSamplesOrPresetRepository(){val w=Seed.create();assertTrue(w.notes.isEmpty());assertEquals("",w.sync.repository);assertTrue(w.notebooks.isEmpty())}
    @Test fun creationRequiresExactNotebook(){val base=TestWorkspace.create().copy(notes=emptyList());val second=Notebook(name="Second prompts",mode=NoteMode.Prompt);val w=base.copy(notebooks=base.notebooks+second);w.liveBooks.forEach{val n=NoteCreation.inNotebook(w,it.id);assertEquals(it.id,n.notebookId);assertEquals("",n.content)};assertThrows(IllegalStateException::class.java){NoteCreation.inNotebook(w,"home")};assertThrows(IllegalStateException::class.java){NoteCreation.inNotebook(w.copy(notebooks=listOf(second.copy(deletedAt=now()))),second.id)}}
    @Test fun shortcutsAreEditableAndRejectConflicts(){val p=Preferences(shortcuts=mapOf("new" to "Ctrl+Shift+N","search" to ""));assertNull(Shortcuts.resolve(p,"Ctrl+N"));assertEquals("new",Shortcuts.resolve(p,"Ctrl+Shift+N"));assertNull(Shortcuts.resolve(p,"Ctrl+K"));assertNotNull(Shortcuts.error(p,"search","Ctrl+Shift+N"));assertNotNull(Shortcuts.error(p,"new","N"));assertNotNull(Shortcuts.error(p,"new","Ctrl+C"));assertNull(Shortcuts.error(p,"new","F8"))}
    @Test fun removedTagsNeverAffectSearch(){val n=Note(title="Hello",tags=listOf("hidden-tag"));assertFalse(Records.matches(n,"hidden-tag",NoteMode.Text));assertFalse(Records.matches(n,"#hidden-tag",NoteMode.Text))}
}
class ExplicitVariableTest {
    @Test fun typedBracesRemainLiteral(){var note=Note(content="用户输入 {{名字}}");assertTrue(PromptVariables.used(note).isEmpty());val v=PromptVariables.named(note,"名字");note=note.copy(variables=listOf(v),content=note.content+"\n"+PromptVariables.token(v));assertEquals(1,PromptVariables.used(note).size);assertEquals("用户输入 {{名字}}\n张三",PromptVariables.fill(note,mapOf(v.id to "张三")))}
    @Test fun variableValuesDoNotRecursivelyTriggerCommands(){val a=PromptVariable(name="a");val b=PromptVariable(name="b");val n=Note(variables=listOf(a,b),content=PromptVariables.token(a)+" "+PromptVariables.token(b));assertEquals(PromptVariables.token(b)+" end",PromptVariables.fill(n,mapOf(a.id to PromptVariables.token(b),b.id to "end")))}
}

class NotebookStyleTest{@org.junit.Test fun cloudSnapshotsKeepAppearanceIndependentOfMode(){val w=TestWorkspace.create();val book=w.notebooks[0].copy(color="#A1C4FD",colorEnd="#C2E9FB",icon="Leaf");val styled=w.copy(notebooks=listOf(book)+w.notebooks.drop(1));val next=app.moji.android.sync.GameSaves.apply(Seed.create(),app.moji.android.sync.GameSaves.capture(styled));org.junit.Assert.assertEquals(book,next.notebooks[0])}}
