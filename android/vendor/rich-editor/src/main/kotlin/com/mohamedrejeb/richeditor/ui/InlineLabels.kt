package com.mohamedrejeb.richeditor.ui

import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.AnnotatedString
import androidx.compose.ui.text.SpanStyle
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.input.OffsetMapping
import androidx.compose.ui.text.input.TransformedText

/** Native editable labels for atomic image tokens. BasicTextField does not render InlineTextContent. */
public fun renderInlineLabels(text: AnnotatedString, labels: Map<String, String>): TransformedText {
    val replacements = text.getStringAnnotations(0, text.length).mapNotNull { range ->
        labels.entries.firstOrNull { range.item.startsWith(it.key + "-") }?.let { Triple(range.start, range.end, it.value) }
    }.distinctBy { it.first }.sortedBy { it.first }
    if (replacements.isEmpty()) return TransformedText(text, OffsetMapping.Identity)
    val output = StringBuilder()
    val chips = mutableListOf<Pair<Int, Int>>()
    val forward = IntArray(text.length + 1)
    val reverse = mutableListOf(0)
    var cursor = 0
    fun literal(end: Int) {
        while (cursor < end) {
            forward[cursor] = output.length
            output.append(text.text[cursor])
            cursor++
            reverse.add(cursor)
        }
    }
    for ((start, end, label) in replacements) {
        if (start < cursor || end > text.length || start == end) continue
        literal(start)
        val at = output.length
        forward[start] = at
        val shown = "\u00a0$label\u00a0"
        output.append(shown)
        chips.add(at to output.length)
        repeat(shown.length) { index -> reverse.add(if (index + 1 < (shown.length + 1) / 2) start else end) }
        for (position in start + 1..end) forward[position] = output.length
        cursor = end
    }
    literal(text.length)
    forward[text.length] = output.length
    val annotated = AnnotatedString.Builder(output.toString())
    text.spanStyles.forEach { annotated.addStyle(it.item, forward[it.start], forward[it.end]) }
    text.paragraphStyles.forEach { annotated.addStyle(it.item, forward[it.start], forward[it.end]) }
    chips.forEach { (start, end) -> annotated.addStyle(SpanStyle(color = Color(0xFF3F5639), background = Color(0xFFE6EDDE), fontWeight = FontWeight.Medium), start, end) }
    return TransformedText(annotated.toAnnotatedString(), object : OffsetMapping {
        override fun originalToTransformed(offset: Int) = forward[offset.coerceIn(0, forward.lastIndex)]
        override fun transformedToOriginal(offset: Int) = reverse[offset.coerceIn(0, reverse.lastIndex)]
    })
}
