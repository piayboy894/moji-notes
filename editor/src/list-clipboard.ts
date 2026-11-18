import { getTextBetween, getTextSerializersFromSchema } from '@tiptap/core'
import type { Slice } from '@tiptap/pm/model'
import type { EditorView } from '@tiptap/pm/view'

function orderedMarker(value: number, style: string): string {
  let marker = ''
  if (style.endsWith('alpha') && value > 0) {
    for (let n = value; n > 0; n = Math.floor((n - 1) / 26))
      marker = String.fromCharCode(97 + (n - 1) % 26) + marker
  } else if (style.endsWith('roman') && value > 0 && value < 4000) {
    for (const [size, letters] of [[1000,'m'],[900,'cm'],[500,'d'],[400,'cd'],[100,'c'],[90,'xc'],[50,'l'],[40,'xl'],[10,'x'],[9,'ix'],[5,'v'],[4,'iv'],[1,'i']] as const) {
      marker += letters.repeat(Math.floor(value / size))
      value %= size
    }
  } else return value + '.'
  return (style.startsWith('upper') ? marker.toUpperCase() : marker) + '.'
}

// Read positions in the original document so a selection starting partway
// through a list keeps the numbering visible on screen. HTML uses PM's serializer.
export function listClipboardText(_slice: Slice, view: EditorView): string {
  const { doc, selection: { from, to }, schema } = view.state
  const serializers = getTextSerializersFromSchema(schema)
  const lines: string[] = []
  const styles = new Map<number, string>()
  let hasList = false
  doc.nodesBetween(from, to, (node, pos) => {
    if (!node.isTextblock) {
      if (node.type.name === 'image') lines.push(node.attrs.alt || '')
      return
    }
    const start = Math.max(0, from - pos - 1)
    const end = Math.min(node.content.size, to - pos - 1)
    const line = getTextBetween(node, { from: start, to: end }, { textSerializers: serializers })
    const path = doc.resolve(pos)
    let indent = 0, marker = ''
    for (let depth = 1; depth <= path.depth; depth++) {
      const list = path.node(depth)
      if (!['orderedList', 'bulletList', 'taskList'].includes(list.type.name)) continue
      hasList = true
      indent += 1 + (list.attrs.indent || 0)
      marker = ''
      if (start || depth + 1 !== path.depth || path.index(depth + 1) !== 0) continue
      const listPos = path.before(depth)
      if (!styles.has(listPos)) {
        const dom = view.nodeDOM(listPos)
        styles.set(listPos, dom instanceof Element ? getComputedStyle(dom).listStyleType : '')
      }
      const style = styles.get(listPos)!
      if (list.type.name === 'orderedList') marker = orderedMarker(list.attrs.start + path.index(depth), style)
      else if (list.type.name === 'taskList') marker = path.node(depth + 1).attrs.checked ? '[x]' : '[ ]'
      else marker = style === 'circle' ? '◦' : style === 'square' ? '▪' : '•'
    }
    // Copying a substring at the beginning of a single item is still a text selection.
    const partial = from >= pos + 1 && to < pos + 1 + node.content.size
    const prefix = indent && !start && !partial ? '  '.repeat(indent - 1) + (marker ? marker + ' ' : '  ') : ''
    lines.push(prefix + line)
    return false
  })
  // A falsy result delegates ordinary selections to Tiptap's normal serializer.
  return hasList ? lines.join('\n') : ''
}
