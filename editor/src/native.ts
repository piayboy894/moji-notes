import {Editor} from '@tiptap/core'
import {type Node as DocumentNode} from '@tiptap/pm/model'
import {editorOptions} from './editor-options'
import palettes from '../../assets/themes.json'
import {convertVariable,selectedVariableName,selectedRange} from './prompt-field'
export {MojiVariable} from './prompt-field'
import {EditorState} from '@tiptap/pm/state'
export const PREFIX='<!--moji:tiptap:2-->'
type Variable={id:string,name:string}
type Payload={id:string,session?:string,content:string,prompt?:boolean,variables:Variable[],theme:string,fontSize:number,fontFamily?:string,fontUrl?:string,contentInset?:number,shortcuts:Record<string,string>,defaults:Record<string,string>,readOnly?:boolean,palette?:string}
let primary: Editor | null = null
let editor: Editor | null = null
let pages: Editor[] = []
const versionNames = new WeakMap<Editor, string>()
export function versionLabel(page: Editor, index: number) {
  return versionNames.get(page) || '版本 ' + (index + 1)
}
export function renameVersion(page: Editor, name: string) {
  if (!current?.prompt || !page.isEditable || !pages.includes(page)) return
  const title = name.trim()
  if (title === (versionNames.get(page) || '')) return
  versionNames.set(page, title)
  dirty = true
  emit()
  publishPages()
}
export type PageState = { pages: Editor[], active: Editor | null, prompt: boolean, editable: boolean, session?: string }
let pageState: PageState = { pages, active: null, prompt: false, editable: false }
const listeners = new Set<() => void>()
export const getPageState = () => pageState
export function subscribePages(listener: () => void) {
  listeners.add(listener)
  return () => { listeners.delete(listener) }
}
function publishPages() {
  pageState = { pages, active: editor, prompt: !!current?.prompt, editable: !!editor?.isEditable, session: currentDocument() }
  listeners.forEach(listener => listener())
}

let current:Payload|null=null
let dirty=false
const native=(message:object)=>{message={id:current?.id,session:current?.session,...message};const text=JSON.stringify(message);const w=window as any;if(w.chrome?.webview)w.chrome.webview.postMessage(message);else w.MojiNative?.postMessage(text)}
export const currentDocument=()=>current?.session||current?.id
export const reportImageError=()=>native({type:'action',id:current?.id,action:'image-error'})
export const openExternal=(url:string)=>{if(/^https?:\/\//i.test(url))native({type:'openLink',id:current?.id,url})}
const pageCache = new WeakMap<Editor, { doc: DocumentNode, html: string, variables: Variable[], text: string }>()
function pageSnapshot(page: Editor) {
  const doc = page.state.doc
  const cached = pageCache.get(page)
  if (cached?.doc === doc) return cached
  const variables: Variable[] = []
  doc.descendants(node => {
    if (node.type.name === 'mojiVariable' && /^[a-f0-9]{32}$/.test(node.attrs.id))
      variables.push({ id: node.attrs.id, name: node.attrs.name })
  })
  const value = { doc, html: page.getHTML(), variables, text: page.getText() }
  pageCache.set(page, value)
  return value
}
function snapshot() {
  const parts = pages.map(pageSnapshot)
  const html = parts.length > 1 || (pages[0] && versionNames.get(pages[0]))
    ? parts.map((p, index) => {
      const name = versionNames.get(pages[index])
      const attribute = name ? ' data-moji-title="' + name.replace(/&/g, '&amp;').replace(/"/g, '&quot;').replace(/</g, '&lt;').replace(/>/g, '&gt;') + '"' : ''
      return '<section data-moji-version="true"' + attribute + '>' + p.html + '</section>'
    }).join('')
    : parts[0]?.html || '<p></p>'
  const variables = new Map<string, Variable>()
  for (const page of parts) for (const v of page.variables) variables.set(v.id, v)
  return { content: PREFIX + html, variables: [...variables.values()], text: parts.map(p => p.text).join('\n\n') }
}
function emit(){if(!editor||!current||!dirty)return;native({type:'change',id:current.id,session:current.session,...snapshot()});dirty=false}
function literal(content:string,variables:Variable[]){const box=document.createElement('div');const pattern=/!\[[^\]]*\]\(moji-variable:([a-f0-9]{32})\)/g
 for(const line of content.split('\n')){const p=document.createElement('p');let at=0;for(const m of line.matchAll(pattern)){const v=variables.find(v=>v.id===m[1]);if(!v)continue;p.append(document.createTextNode(line.slice(at,m.index)));const span=document.createElement('span');span.dataset.mojiVariable=v.id;span.dataset.name=v.name;span.dataset.mojiField='true';p.append(span);at=m.index!+m[0].length}p.append(document.createTextNode(line.slice(at)));box.append(p)}return box.innerHTML}
function theme(payload:Partial<Payload>){
 const root=document.documentElement;const dark=payload.theme==='dark';root.classList.toggle('dark',dark)
 const palette=palettes.find(p=>p.id===payload.palette&&p.dark===dark)||palettes.find(p=>p.dark===dark)!,c=palette.colors
 root.dataset.palette=palette.id
 if(payload.contentInset!==undefined)root.style.setProperty('--moji-content-inset',payload.contentInset+'px')
 root.style.setProperty('--moji-font-size',`${payload.fontSize||16}px`)
 const family=payload.fontFamily?.trim()||''
 root.style.setProperty('--moji-font-family',family?(/^(serif|sans-serif|monospace|system-ui)$/.test(family)?family:JSON.stringify(family))+',system-ui,sans-serif':'system-ui,"Microsoft YaHei",sans-serif')
 let font=document.querySelector<HTMLStyleElement>('#moji-display-font');if(!font){font=document.createElement('style');font.id='moji-display-font';document.head.append(font)}
 const url=payload.fontUrl||''
 const face=/^https:\/\/appassets\.androidplatform\.net\/fonts\/[a-z0-9.-]+$/.test(url)?'@font-face{font-family:"Moji Display";src:url('+JSON.stringify(url)+');font-display:swap}':''
 if(font.textContent!==face)font.textContent=face
 if(face)root.style.setProperty('--moji-font-family','"Moji Display",system-ui,sans-serif')

 for(const [key,value]of Object.entries(c))root.style.setProperty('--moji-'+key.toLowerCase(),value)
 const tokens:Record<string,string>={'--tt-bg-color':c.Panel,'--tt-theme-text':c.Ink,'--tt-border-color':c.Line,'--tt-border-color-tint':c.Line,'--tt-sidebar-bg-color':c.Side,'--tt-scrollbar-color':c.ScrollThumb,'--tt-cursor-color':c.Accent,'--tt-selection-color':c.Selection,'--tt-card-bg-color':c.Raised,'--tt-card-border-color':c.Line,'--tt-toolbar-bg-color':c.Panel,'--tt-toolbar-border-color':c.Line,'--white':c.Panel,'--black':c.Panel}
 for(const [key,value]of Object.entries(tokens))root.style.setProperty(key,value)
 const rgb=(hex:string)=>[1,3,5].map(i=>parseInt(hex.slice(i,i+2),16)).join(',')
 for(const mode of ['light','dark'])for(const [i,key]of [ [50,'Panel'],[100,'Soft'],[200,'Line'],[300,'Outline'],[400,'Sub'],[500,'Sub'],[600,'Sub'],[700,'Ink'],[800,'Ink'],[900,'Ink']] as const){root.style.setProperty(`--tt-gray-${mode}-${i}`,c[key]);root.style.setProperty(`--tt-gray-${mode}-a-${i}`,i<=300?`rgba(${rgb(c.Ink)},${i===50?.04:i===100?.06:i===200?.1:.18})`:c[key])}
 for(const i of [50,100,200,300,400,500,600,700,800,900,950])root.style.setProperty(`--tt-brand-color-${i}`,i<=300||i>=800?c.AccentLight:c.Accent)
 root.style.backgroundColor=c.Panel
}

function pageContents(payload: Payload): { html: string, name?: string }[] {
  if (!/^<!--moji:tiptap:[12]-->/.test(payload.content)) return [{ html: literal(payload.content, payload.variables || []) }]
  const html = payload.content.slice(PREFIX.length)
  if (!payload.prompt || !html.startsWith('<section data-moji-version=')) return [{ html }]
  const template = document.createElement('template')
  template.innerHTML = html
  const sections = Array.from(template.content.children)
  return sections.length && sections.every(el => el.matches('section[data-moji-version]'))
    ? sections.map(el => ({ html: el.innerHTML, name: el.getAttribute('data-moji-title') || '' })) : [{ html }]
}
function watch(page: Editor) {
  page.on('update', () => { dirty = true; emit(); selection() })
  page.on('selectionUpdate', () => { if (page === editor) selection() })
  page.on('focus', () => activatePage(page))
}
function activatePage(page: Editor) {
  if (!pages.includes(page)) return
  if (editor !== page) { editor = page; publishPages() }
  selection()
}
function newPage(html: string, editable: boolean) {
  const page = new Editor({ ...editorOptions(), content: html, editable, parseOptions: { preserveWhitespace: 'full' } })
  watch(page)
  return page
}
export function addVersion() {
  if (!current?.prompt || !editor?.isEditable) return
  const page = newPage('<p></p>', true)
  pages = [...pages, page]
  editor = page
  dirty = true
  emit()
  publishPages()
  return page
}
export function jumpToVersion(page: Editor) {
  if (!pages.includes(page)) return
  activatePage(page)
  page.commands.setTextSelection(0)
  page.view.focus()
  page.view.dom.closest('.moji-version-sheet')?.scrollIntoView({ block: 'start' })
}
function load(payload: Payload) {
  current = payload
  theme(payload)
  if (!primary) return
  dirty = false
  for (const page of pages.slice(1)) page.destroy()
  const [first, ...rest] = pageContents(payload)
  editor = primary
  versionNames.set(primary, first.name || '')
  editor.commands.setContent(first.html, { emitUpdate: false, parseOptions: { preserveWhitespace: 'full' } })
  editor.view.updateState(EditorState.create({ schema: editor.schema, doc: editor.state.doc, plugins: editor.state.plugins }))
  editor.setEditable(!payload.readOnly, false)
  editor.view.dispatch(editor.state.tr)
  pages = [primary, ...rest.map(({ html, name }) => {
    const page = newPage(html, !payload.readOnly)
    versionNames.set(page, name || '')
    return page
  })]
  publishPages()
  dirty = false
  if (payload.variables?.length && payload.content !== snapshot().content) { dirty = true; emit() }
  document.querySelector('.simple-editor-wrapper')?.scrollTo(0, 0)
  window.dispatchEvent(new Event('moji-document'))
  queueMicrotask(() => {
    if (current === payload) { native({ type: 'loaded', id: payload.id, session: payload.session }); selection() }
  })
}
document.addEventListener('focusin', event => {
  // Inline variable fields have their own editing host.
  const page = pages.find(p => p.view.dom.contains(event.target as Node))
  if (page) activatePage(page)
  else selection()
})
function selection(){if(editor&&current){const name=selectedVariableName(editor);native({type:'selection',id:current.id,session:current.session,canConvert:!!name,selection:name?{...selectedRange(editor),name}:null})}}
function setEditable(editable: boolean) {
  for (const page of pages) { page.setEditable(editable, false); page.view.dispatch(page.state.tr) }
  publishPages()
}
function activate() { setEditable(!current?.readOnly) }
function deactivate() {
  if (editor?.view.dom.contains(document.activeElement)) (document.activeElement as HTMLElement)?.blur()
  editor?.commands.blur()
  setEditable(false)
}
function configure(payload:Partial<Payload>){if(current){for(const key of ['theme','palette','fontSize','fontFamily','fontUrl','contentInset','shortcuts','defaults'] as const)if(payload[key]!==undefined)(current as any)[key]=payload[key]}theme(current||payload);selection()}
function action(name:string,value?:any){
 if(!editor||editor.isDestroyed)return false
 const field=!!document.activeElement?.closest('.moji-variable-content')
 switch(name){
  case 'undo':return field?editor.commands.undo():editor.chain().focus().undo().run()
  case 'redo':return field?editor.commands.redo():editor.chain().focus().redo().run()
  case 'bold':return field?true:editor.chain().focus().toggleBold().run()
  case 'focus':return editor.commands.focus(value||'start')
  case 'convertVariable':return convertVariable(editor,value)
  default:return false
 }
}
const mounted = new WeakSet<Editor>()
export function bindEditor(instance: Editor) {
  if (primary === instance) return
  primary = editor = instance
  pages = [instance]
  watch(instance)
  const pending = current || (window as any).__mojiLoad
  if (pending) load(pending)
  else publishPages()
}
// The parent effect runs after child menus and their plugins are mounted. A core
// onCreate callback alone is too early for a native host that loads immediately.
export function editorMounted(instance:Editor){bindEditor(instance);if(mounted.has(instance))return;mounted.add(instance);queueMicrotask(()=>{if(primary===instance&&!instance.isDestroyed)native({type:'ready'})})}
;(window as any).MojiEditor={load,theme,configure,activate,deactivate,action,flush:(token?:string)=>{emit();native({type:'flushed',id:current?.id,session:current?.session,token})},snapshot,get recordId(){return current?.id},get editor(){return editor}}
document.addEventListener('keydown',event=>{if(!current||event.isComposing)return;const chord=(event.ctrlKey?'Ctrl+':'')+(event.altKey?'Alt+':'')+(event.shiftKey?'Shift+':'')+event.key.toUpperCase();const item=Object.entries(current.shortcuts||{}).find(([,binding])=>binding&&binding===chord)
 if(item){event.preventDefault();event.stopImmediatePropagation();if(!action(item[0]))native({type:'action',id:current.id,action:item[0]})}
 else if(Object.entries(current.defaults||{}).some(([id,binding])=>binding===chord&&current!.shortcuts[id]!==binding)){event.preventDefault();event.stopImmediatePropagation()}
},true)

// Desktop commands use asynchronous messages, with an acknowledgement owned by
// the native host. Readiness never depends on background animation frames.
const desktop=(window as any).chrome?.webview
desktop?.addEventListener('message',(event:{data:any})=>{
 const m=event.data;if(m?.protocol!=='moji'||typeof m.request!=='string')return
 try{
  switch(m.command){
   case 'ping':break
   case 'load':load(m.value);break
   case 'flush':emit();break
   case 'configure':configure(m.value);break
   case 'activate':activate();break
   case 'deactivate':deactivate();break
   case 'action':action(m.value.name,m.value.argument);break
   default:throw new Error('Unknown host command')
  }
  native({type:'reply',request:m.request,ok:true})
 }catch{native({type:'reply',request:m.request,ok:false})}
})
