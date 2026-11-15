package app.moji.android
import androidx.compose.ui.text.TextRange
import com.mohamedrejeb.richeditor.model.RichTextState
import app.moji.android.data.RichMarkdown
import org.junit.Test
import org.junit.Assert.*
class EditorBoundaryTest {
 @Test fun emptyAndSelectedContentCanBeSplitAndAcceptVariables() {
  for(text in listOf("", "hello", "**bold**", "第一行\n\n第二行", "- one\n- two")) {
   val original=RichTextState().apply{setMarkdown(text)}
   for(position in 0..original.annotatedString.length) {
    val left=RichTextState().apply{setHtml(original.toHtml())}
    val right=RichTextState().apply{setHtml(original.toHtml())}
    if(position<left.annotatedString.length)left.removeTextRange(TextRange(position,left.annotatedString.length))
    if(position>0)right.removeTextRange(TextRange(0,position))
    RichMarkdown.fromHtml(left.toHtml());RichMarkdown.fromHtml(right.toHtml())
    val inserted=RichTextState().apply{setMarkdown(text);selection=TextRange(position)}
    inserted.insertHtmlAfterSelection("<img src=\"moji-variable:0123456789abcdef0123456789abcdef\" width=\"0\" height=\"0\">")
    assertTrue("text=$text position=$position html=${inserted.toHtml()}",inserted.toHtml().contains("moji-variable:"))
   }
  }
 }
}
