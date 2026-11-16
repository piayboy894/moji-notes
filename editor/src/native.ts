import {type Editor} from '@tiptap/core'
import {MojiVariable,convertVariable,selectedVariableName,selectedRange} from './prompt-field'
export {MojiVariable} from './prompt-field'
import {EditorState} from '@tiptap/pm/state'
import {closeHistory} from '@tiptap/pm/history'
export const PREFIX='<!--moji:tiptap:2-->'
type Variable={id:string,name:string}
type Payload={id:string,session?:string,content:string,variables:Variable[],theme:string,fontSize:number,contentInset?:number,shortcuts:Record<string,string>,defaults:Record<string,string>,readOnly?:boolean}
let editor:Editor|null=null
let current:Payload|null=null
let dirty=false
const native=(message:object)=>{message={id:current?.id,session:current?.session,...message};const text=JSON.stringify(message);const w=window as any;if(w.chrome?.webview)w.chrome.webview.postMessage(message);else w.MojiNative?.postMessage(text)}
export const currentDocument=()=>current?.session||current?.id
export const reportImageError=()=>native({type:'action',id:current?.id,action:'image-error'})
export const openExternal=(url:string)=>{if(/^https?:\/\//i.test(url))native({type:'openLink',id:current?.id,url})}
function snapshot(){const variables:Variable[]=[];editor?.state.doc.descendants(node=>{if(node.type.name==='mojiVariable'&&/^[a-f0-9]{32}$/.test(node.attrs.id)&&!variables.some(v=>v.id===node.attrs.id))variables.push({id:node.attrs.id,name:node.attrs.name})});return {content:PREFIX+(editor?.getHTML()||'<p></p>'),variables,text:editor?.getText()||''}}
function emit(){if(!editor||!current||!dirty)return;native({type:'change',id:current.id,session:current.session,...snapshot()});dirty=false}
function literal(content:string,variables:Variable[]){const box=document.createElement('div');const pattern=/!\[[^\]]*\]\(moji-variable:([a-f0-9]{32})\)/g
 for(const line of content.split('\n')){const p=document.createElement('p');let at=0;for(const m of line.matchAll(pattern)){const v=variables.find(v=>v.id===m[1]);if(!v)continue;p.append(document.createTextNode(line.slice(at,m.index)));const span=document.createElement('span');span.dataset.mojiVariable=v.id;span.dataset.name=v.name;span.dataset.mojiField='true';p.append(span);at=m.index!+m[0].length}p.append(document.createTextNode(line.slice(at)));box.append(p)}return box.innerHTML}
function theme(payload:Partial<Payload>){if(payload.contentInset!==undefined)document.documentElement.style.setProperty('--moji-content-inset',payload.contentInset+'px');document.documentElement.classList.toggle('dark',payload.theme==='dark');document.documentElement.style.setProperty('--moji-font-size',`${payload.fontSize||16}px`)}
function load(payload:Payload){current=payload;theme(payload);if(!editor)return;dirty=false;editor.commands.setContent(/^<!--moji:tiptap:[12]-->/.test(payload.content)?payload.content.slice(PREFIX.length):literal(payload.content,payload.variables||[]),{emitUpdate:false,parseOptions:{preserveWhitespace:'full'}});editor.view.updateState(EditorState.create({schema:editor.schema,doc:editor.state.doc,plugins:editor.state.plugins}));editor.setEditable(!payload.readOnly,false);editor.view.dispatch(editor.state.tr);dirty=false;if((payload.variables||[]).length&&payload.content!==snapshot().content){dirty=true;emit()};document.querySelector('.simple-editor-wrapper')?.scrollTo(0,0);window.dispatchEvent(new Event('moji-document'));requestAnimationFrame(()=>requestAnimationFrame(()=>{native({type:'loaded',id:payload.id,session:payload.session});selection()}))}
document.addEventListener('focusin',()=>selection());
function selection(){if(editor&&current){const name=selectedVariableName(editor);native({type:'selection',id:current.id,session:current.session,canConvert:!!name,selection:name?{...selectedRange(editor),name}:null})}}
function activate(){if(editor){editor.setEditable(true,false);editor.view.dispatch(editor.state.tr)}}
function deactivate(){if(editor?.view.dom.contains(document.activeElement)) (document.activeElement as HTMLElement)?.blur();editor?.commands.blur();if(editor){editor.setEditable(false,false);editor.view.dispatch(editor.state.tr)}}
function configure(payload:Partial<Payload>){if(current){for(const key of ['theme','fontSize','contentInset','shortcuts','defaults'] as const)if(payload[key]!==undefined)(current as any)[key]=payload[key]}theme(payload);selection()}
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
export function bindEditor(instance:Editor){editor=instance;editor.on('update',()=>{dirty=true;emit();selection()});editor.on('selectionUpdate',selection);editor.on('focus',selection);if(current)load(current);else if((window as any).__mojiLoad)load((window as any).__mojiLoad);requestAnimationFrame(()=>requestAnimationFrame(()=>native({type:'ready'})))}
;(window as any).MojiEditor={load,theme,configure,activate,deactivate,action,flush:(token?:string)=>{emit();native({type:'flushed',id:current?.id,session:current?.session,token})},snapshot,get recordId(){return current?.id},get editor(){return editor}}
document.addEventListener('keydown',event=>{if(!current||event.isComposing)return;const chord=(event.ctrlKey?'Ctrl+':'')+(event.altKey?'Alt+':'')+(event.shiftKey?'Shift+':'')+event.key.toUpperCase();const item=Object.entries(current.shortcuts||{}).find(([,binding])=>binding&&binding===chord)
 if(item){event.preventDefault();event.stopImmediatePropagation();if(!action(item[0]))native({type:'action',id:current.id,action:item[0]})}
 else if(Object.entries(current.defaults||{}).some(([id,binding])=>binding===chord&&current!.shortcuts[id]!==binding)){event.preventDefault();event.stopImmediatePropagation()}
},true)
