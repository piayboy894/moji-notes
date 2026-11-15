package com.mohamedrejeb.richeditor.ui.test
import com.mohamedrejeb.richeditor.model.RichSpan
import androidx.compose.ui.util.fastForEachIndexed

internal fun getRichTextStyleTreeRepresentation(
    stringBuilder: StringBuilder,
    index: Int,
    richSpan: RichSpan,
    startText: String
) {
    stringBuilder.append("${startText}Text $index `$richSpan`: ${richSpan.children.size} children")
    stringBuilder.appendLine()
    richSpan.children.fastForEachIndexed { childIndex, childRichSpan ->
        getRichTextStyleTreeRepresentation(stringBuilder, childIndex, childRichSpan, "$startText-")
    }
}