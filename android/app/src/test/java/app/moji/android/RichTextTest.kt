package app.moji.android
import app.moji.android.data.*
import org.junit.Assert.*
import org.junit.Test
class RichTextTest {
 @Test fun sharedHtmlIsSearchableWithoutMarkup(){val content=EditorContent.PREFIX+"<h2>标题</h2><p><strong>正文</strong> {{原样}}</p><img src='data:image/png;base64,AAAA' alt='图片'>";assertEquals("标题\n正文 {{原样}}\n图片",EditorContent.plain(content));assertFalse(Records.plain(content).contains("base64"))}
 @Test fun explicitVariablesFillOnceWithoutParsingUserInput(){val v=PromptVariable(name="受众");val note=Note(content=EditorContent.PREFIX+"<p>{{原样}} <span data-moji-variable=\"${v.id}\" data-name=\"受众\">受众</span></p>",variables=listOf(v));assertEquals(listOf(v),PromptVariables.used(note));val value="<script>& {{原样}}";assertEquals("{{原样}} $value",PromptVariables.plainText(note,mapOf(v.id to value)){EditorContent.plain(it)});assertTrue(PromptVariables.used(Note(content="{{受众}}",variables=listOf(v))).isEmpty())}
 @Test fun preexistingTextRemainsLiteral(){assertEquals("# Heading\n- [x] task",EditorContent.plain("# Heading\n- [x] task"))}
}
