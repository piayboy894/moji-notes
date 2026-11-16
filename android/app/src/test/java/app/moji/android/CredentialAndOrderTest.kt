package app.moji.android
import app.moji.android.data.*
import app.moji.android.sync.GameSaves
import kotlinx.serialization.encodeToString
import org.junit.Assert.*
import org.junit.Test
class CredentialAndOrderTest {
 @Test fun customAttributesSurviveHistorySerializationAndFullSaves(){
  val book=Notebook(mode=NoteMode.Password);val field=CredentialField(name="账户地区",value="新加坡");val note=Note(notebookId=book.id,fields=listOf(field));val edited=note.edited(note.copy(fields=listOf(field.copy(value="香港"))),true);val state=Workspace(notebooks=listOf(book),notes=listOf(edited))
  val restored=GameSaves.apply(Workspace(),json.decodeFromString<SyncPayload>(json.encodeToString(GameSaves.capture(state))));assertEquals(2,restored.schema);assertEquals("香港",restored.notes.single().fields.single().value);assertEquals(field.id,restored.notes.single().fields.single().id);assertEquals("新加坡",restored.notes.single().history.single().fields.single().value);assertTrue(Records.matches(restored.notes.single(),"账户地区",NoteMode.Password));assertTrue(json.decodeFromString<Note>("{\"title\":\"旧记录\"}").fields.isEmpty())
 }
 @Test fun manualNotebookOrderPreservesTombstonesOwnershipAndSaveOrder(){
  val a=Notebook(name="A");val removed=Notebook(name="D",deletedAt=now());val b=Notebook(name="B");val c=Notebook(name="C");val note=Note(notebookId=a.id);val state=Workspace(notebooks=listOf(a,removed,b,c),notes=listOf(note));val reordered=NotebookOrder.move(state,c.id,a.id)
  assertEquals(listOf("C","D","A","B"),reordered.notebooks.map{it.name});assertEquals(a.id,reordered.notes.single().notebookId);assertSame(removed,reordered.notebooks[1]);val restored=GameSaves.apply(Workspace(),GameSaves.capture(reordered));assertEquals(listOf(c.id,a.id,b.id),restored.liveBooks.map{it.id});assertEquals(listOf(a.id,b.id,c.id),NotebookOrder.move(restored,c.id,b.id,true).liveBooks.map{it.id});assertSame(state,NotebookOrder.move(state,removed.id,a.id))
 }
 @Test fun duplicateAttributeIdsAreRejected(){
  val book=Notebook();val field=CredentialField(name="属性");val state=Workspace(notebooks=listOf(book),notes=listOf(Note(notebookId=book.id,fields=listOf(field,field))));assertThrows(IllegalArgumentException::class.java){state.validate()}
 }
}
