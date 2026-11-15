package app.moji.android

import androidx.compose.ui.text.AnnotatedString
import androidx.compose.ui.text.SpanStyle
import androidx.compose.ui.text.font.FontWeight
import com.mohamedrejeb.richeditor.model.RichTextState
import com.mohamedrejeb.richeditor.ui.renderInlineLabels
import org.junit.Assert.*
import org.junit.Test

class InlineLabelsTest {
    @Test fun registeredTokensHaveVisibleLabelsAndAtomicCursorMapping() {
        val state=RichTextState().apply{setMarkdown("**Before** ![变量](moji-variable:one) and ![变量](moji-variable:two) after {{literal}}")}
        val original=state.annotatedString
        val display=renderInlineLabels(original,mapOf("moji-variable:one" to "受众","moji-variable:two" to "Long variable"))
        assertTrue("Original=$original annotations=${original.getStringAnnotations(0,original.length)} display=${display.text} html=${state.toHtml()}",display.text.text.contains("受众"))
        assertTrue("Second label: ${display.text}",display.text.text.contains("Long variable"))
        assertTrue("Literal: ${display.text}",display.text.text.contains("{{literal}}"))
        assertFalse(display.text.text.contains('\uFFFC'))
        assertTrue(display.text.spanStyles.any{it.item.fontWeight==FontWeight.Bold})
        val mapping=display.offsetMapping
        var previous=-1
        for(i in 0..original.length){val position=mapping.originalToTransformed(i);assertTrue(position>=previous);assertEquals(i,mapping.transformedToOriginal(position));previous=position}
        previous=-1
        for(i in 0..display.text.length){val position=mapping.transformedToOriginal(i);assertTrue(position>=previous);assertTrue(position in 0..original.length);previous=position}
        assertEquals(display.text.length,mapping.originalToTransformed(original.length))
        assertTrue(state.toHtml().contains("moji-variable:one"))
        state.selection=androidx.compose.ui.text.TextRange(original.length)
        state.addTextAfterSelection(" tail")
        assertTrue(state.toHtml().contains("tail"))
        assertTrue(state.toHtml().contains("moji-variable:one"));assertTrue(state.toHtml().contains("moji-variable:two"))
    }
    @Test fun deletingAndReplacingAnAtomicVariableKeepsSurroundingText() {
        val state=RichTextState().apply{setMarkdown("Keep {{literal}} ![变量](moji-variable:one)")}
        state.selection=androidx.compose.ui.text.TextRange(18);state.addTextAfterSelection(" tail")
        state.removeTextRange(androidx.compose.ui.text.TextRange(17,18))
        assertEquals("Keep {{literal}}  tail",state.annotatedString.text)
        assertFalse(state.toHtml().contains("moji-variable:"))
        state.setMarkdown("A ![变量](moji-variable:one) B ![变量](moji-variable:one) C")
        state.replaceTextRange(androidx.compose.ui.text.TextRange(2,3),"replacement")
        assertEquals("A replacement B \uFFFC C",state.annotatedString.text)
        assertEquals(1,Regex("moji-variable:one").findAll(state.toHtml()).count())
    }
    @Test fun unregisteredLiteralTextKeepsIdentityMapping() {
        val text=AnnotatedString("{{not a variable}}")
        val display=renderInlineLabels(text,emptyMap())
        assertEquals(text,display.text)
        for(i in 0..text.length)assertEquals(i,display.offsetMapping.originalToTransformed(i))
    }
}
