package app.moji.android

import app.moji.android.data.*
import app.moji.android.ui.NotebookReorderPreview
import org.junit.Assert.*
import org.junit.Test

class RecordTransferTest {
    private fun state():Workspace {
        val a=Notebook(name="A",mode=NoteMode.Text);val b=Notebook(name="B",mode=NoteMode.Text);val p=Notebook(name="Passwords",mode=NoteMode.Password)
        return Workspace(notebooks=listOf(a,b,p),notes=listOf(Note(notebookId=a.id,title="Original",content="<p>Body</p>",fields=listOf(CredentialField(name="Server",value="Stored")),pinned=true,favorite=true,createdAt=1,updatedAt=2,history=listOf(NoteVersion(content="Before"))),Note(notebookId=p.id,title="Account",password="test-only")))
    }
    @Test fun cutKeepsOriginalUntilPasteAndMovesLatestCompleteRecord() {
        val w=state();val n=w.notes[0];val bundle=RecordTransfer.capture(w,listOf(n.id),true);assertEquals(w.notebooks[0].id,w.notes[0].notebookId)
        val edited=w.copy(notes=listOf(n.copy(content="Last input"),w.notes[1]));val moved=RecordTransfer.apply(edited,bundle,w.notebooks[1].id).state.notes[0]
        assertEquals(n.id,moved.id);assertEquals("Last input",moved.content);assertEquals(n.fields,moved.fields);assertEquals(n.history,moved.history);assertEquals(n.updatedAt,moved.updatedAt);assertEquals(n.createdAt,moved.createdAt);assertTrue(moved.pinned);assertNotEquals(n.revision,moved.revision)
    }
    @Test fun incompatibleBatchNeverMovesOrCopiesSomeItems() {
        for(cut in listOf(false,true)){val w=state();val original=w.notes.toList();val bundle=RecordTransfer.capture(w,w.notes.map{it.id},cut);assertThrows(RecordTransferException::class.java){RecordTransfer.apply(w,bundle,w.notebooks[1].id)};assertEquals(original,w.notes)}
    }
    @Test fun copiesAreIndependentAndRetainFields() {
        val w=state();val n=w.notes[0];val bundle=RecordTransfer.capture(w,listOf(n.id),false);val once=RecordTransfer.apply(w,bundle,w.notebooks[1].id);val twice=RecordTransfer.apply(once.state,bundle,w.notebooks[1].id);val a=twice.state.notes.first{it.id==once.ids[0]};val b=twice.state.notes.first{it.id==twice.ids[0]}
        assertNotEquals(a.id,b.id);assertNotEquals(a.title,b.title);assertEquals(n.fields,a.fields);assertEquals(n.content,a.content);assertFalse(a.pinned);assertFalse(a.favorite);assertTrue(a.history.isEmpty());twice.state.validate()
    }
    @Test fun staleCutCannotResurrectOrMoveAcrossWorkspace() {
        val w=state();val bundle=RecordTransfer.capture(w,listOf(w.notes[0].id),true)
        assertThrows(RecordTransferException::class.java){RecordTransfer.apply(w.copy(id=uid()),bundle,w.notebooks[1].id)}
        assertThrows(RecordTransferException::class.java){RecordTransfer.apply(w.copy(notes=listOf(w.notes[0].copy(deletedAt=1),w.notes[1])),bundle,w.notebooks[1].id)}
        assertSame(w,RecordTransfer.apply(w,bundle,w.notebooks[0].id).state)
    }
    @Test fun previewReordersLocallyAndCommitsOnlyOnceOrCancels() {
        val ids=listOf("a","b","c");val preview=NotebookReorderPreview(ids);var commits=0;var saved=ids
        preview.begin();preview.move("a","c");assertEquals(ids,saved);preview.cancel();assertEquals(ids,preview.order)
        preview.begin();preview.move("a","b");preview.move("a","c");preview.finish{commits++;saved=it};preview.finish{commits++};assertEquals(1,commits);assertEquals(listOf("b","c","a"),saved)
        val w=state();val deleted=Notebook(deletedAt=1);val mixed=w.copy(notebooks=listOf(w.notebooks[0],deleted)+w.notebooks.drop(1));val changed=NotebookOrder.set(mixed,w.liveBooks.map{it.id}.reversed());assertSame(deleted,changed.notebooks[1]);assertSame(w,NotebookOrder.set(w,listOf("absent")))
    }
    @Test fun recordShortcutDefaultsDoNotAllowOtherActionsToClaimTextKeys() {
        val p=Preferences();assertEquals("recordCopy",Shortcuts.resolve(p,"Ctrl+C"));assertNull(Shortcuts.error(p,"recordCopy","Ctrl+C"));assertNotNull(Shortcuts.error(p,"search","Ctrl+C"));assertNull(Shortcuts.error(p,"recordCopy","Ctrl+Shift+C"))
    }
}
