package app.moji.android
import app.moji.android.data.*
import org.junit.Assert.*
import org.junit.Test
class RichTextTest {
    @Test fun structuralContentSurvivesNativeBlockRoundTrip(){val source="# Heading\n\n**Bold** and ordinary {{text}}\n\n- [x] Done\n\n| A | B |\n| --- | --- |\n| one | two |\n\n> Quote\n\n```kotlin\nval x = 1\n```\n\n![photo](data:image/png;base64,AAAA)";val blocks=RichBlocks.parse(source);assertEquals(1,blocks.count{it.kind=="table"});assertTrue(blocks.first{it.kind=="task"}.checked);assertEquals("val x = 1",blocks.first{it.kind=="code"}.content);val again=RichBlocks.parse(RichBlocks.serialize(blocks));assertEquals(blocks,again)}
    @Test fun tableEscapedPipeStaysInOneCell(){val blocks=RichBlocks.parse("| A | B |\n| --- | --- |\n| left\\|right | other |");assertEquals(2,blocks[0].rows[1].size);assertEquals("left|right",blocks[0].rows[1][0]);assertEquals(blocks,RichBlocks.parse(RichBlocks.serialize(blocks)))}
    @Test fun nativeFormattingAndLiteralSymbolsExportWithoutMagic(){val html="<p><b>Bold</b> <i>italic</i> <s>gone</s></p><p>{{literal}} # not a heading</p><ul><li>one</li><li>two</li></ul><p><img src='moji-variable:12345678901234567890123456789012'></p>";val md=RichMarkdown.fromHtml(html);assertTrue(md.contains("**Bold**"));assertTrue(md.contains("*italic*"));assertTrue(md.contains("~~gone~~"));assertTrue(md.contains("{{literal}} \\# not a heading"));assertTrue(md.contains("- one\n- two"));assertTrue(md.contains("moji-variable:12345678901234567890123456789012"))}
    @Test fun editedTableCellsDoNotDoubleEscapePipes(){val markdown=RichMarkdown.fromHtml("<p>left|right</p>");val serialized=RichBlocks.serialize(listOf(RichBlock("table",rows=listOf(listOf(markdown,"B")))));assertEquals("left|right",RichBlocks.parse(serialized)[0].rows[0][0])}
    @Test fun visibleSearchDoesNotExposeInterchangeEscapes(){assertEquals("Hello world left|right #literal",Records.plain("**Hello** world left\\|right \\#literal"))}
    @Test fun plaintextImportsDoNotBecomeListsOrCommands(){assertEquals("1\\. literal\n\\- literal\n\\# literal\n{{literal}}",RichMarkdown.escape("1. literal\n- literal\n# literal\n{{literal}}"))}
}
