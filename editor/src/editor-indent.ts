import { Extension, type Editor } from '@tiptap/core'
import { AllSelection, TextSelection } from '@tiptap/pm/state'

function indent(editor: Editor, reverse: boolean): boolean {
  if (!editor.isEditable) return false
  const { selection, tr } = editor.state
  const { $from, from, to } = selection
  // Code blocks have their own line-aware Tab and Shift+Tab commands.
  if ($from.parent.type.name === 'codeBlock') return false

  for (let depth = $from.depth; depth > 0; depth--) {
    const item = $from.node(depth)
    if (!['listItem', 'taskItem'].includes(item.type.name)) continue
    const list = $from.node(depth - 1)
    const level = list.attrs.indent || 0
    if (!reverse && editor.commands.sinkListItem(item.type.name)) return true
    if (reverse && !level) {
      editor.commands.liftListItem(item.type.name)
    } else {
      // The first item has no previous sibling to nest under. Indent its list.
      tr.setNodeMarkup($from.before(depth - 1), undefined, { ...list.attrs, indent: level + (reverse ? -1 : 1) })
      editor.view.dispatch(tr.scrollIntoView())
    }
    return true
  }

  if (!(selection instanceof TextSelection || selection instanceof AllSelection)) return true
  if (selection.empty && !reverse) {
    editor.view.dispatch(tr.insertText('\t').scrollIntoView())
    return true
  }
  // For a selection, indent whole paragraphs without replacing selected text.
  editor.state.doc.nodesBetween(from, to, (node, pos) => {
    if (['orderedList', 'bulletList', 'taskList'].includes(node.type.name)) {
      const level = Math.max(0, (node.attrs.indent || 0) + (reverse ? -1 : 1))
      tr.setNodeMarkup(tr.mapping.map(pos), undefined, { ...node.attrs, indent: level })
      return false
    }
    if (!node.isTextblock) return
    const start = pos + 1
    const offset = selection.empty ? $from.parentOffset : 0
    const before = node.textBetween(0, offset)
    const at = selection.empty && /\t$/.test(before) ? offset - 1 : 0
    const leading = node.textBetween(at, Math.min(at + 4, node.content.size)).match(/^(?:\t| {1,4})/)
    const mapped = tr.mapping.map(start + at)
    if (!reverse) tr.insertText('\t', mapped)
    else if (leading) tr.delete(mapped, mapped + leading[0].length)
    return false
  })
  if (tr.docChanged) editor.view.dispatch(tr.scrollIntoView())
  return true
}

export const EditorIndent = Extension.create({
  name: 'editorIndent',
  priority: 1100,
  addGlobalAttributes() {
    return [{
      types: ['orderedList', 'bulletList', 'taskList'],
      attributes: { indent: {
        default: 0,
        parseHTML: el => Math.max(0, parseInt(el.dataset.mojiIndent || '0', 10) || 0),
        renderHTML: ({ indent }) => indent ? { 'data-moji-indent': indent, style: `margin-inline-start:${indent * 1.5}em` } : {},
      } },
    }]
  },
  addKeyboardShortcuts() {
    return { Tab: () => indent(this.editor, false), 'Shift-Tab': () => indent(this.editor, true) }
  },
})
