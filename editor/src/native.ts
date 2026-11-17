import {type Editor} from '@tiptap/core'
import palettes from '../../assets/themes.json'
import {MojiVariable,convertVariable,selectedVariableName,selectedRange} from './prompt-field'
export {MojiVariable} from './prompt-field'
import {EditorState} from '@tiptap/pm/state'
import {closeHistory} from '@tiptap/pm/history'
export const PREFIX='<!--moji:tiptap:2-->'
type Variable={id:string,name:string}
type Payload={id:string,session?:string,content:string,variables:Variable[],theme:string,fontSize:number,fontFamily?:string,fontUrl?:string,contentInset?:number,shortcuts:Record<string,string>,defaults:Record<string,string>,readOnly?:boolean,palette?:string}
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

function load(payload:Payload){current=payload;theme(payload);if(!editor)return;dirty=false;editor.commands.setContent(/^<!--moji:tiptap:[12]-->/.test(payload.content)?payload.content.slice(PREFIX.length):literal(payload.content,payload.variables||[]),{emitUpdate:false,parseOptions:{preserveWhitespace:'full'}});editor.view.updateState(EditorState.create({schema:editor.schema,doc:editor.state.doc,plugins:editor.state.plugins}));editor.setEditable(!payload.readOnly,false);editor.view.dispatch(editor.state.tr);dirty=false;if((payload.variables||[]).length&&payload.content!==snapshot().content){dirty=true;emit()};document.querySelector('.simple-editor-wrapper')?.scrollTo(0,0);window.dispatchEvent(new Event('moji-document'));queueMicrotask(()=>{if(current?.session===payload.session&&current?.id===payload.id){native({type:'loaded',id:payload.id,session:payload.session});selection()}})}
document.addEventListener('focusin',()=>selection());
function selection(){if(editor&&current){const name=selectedVariableName(editor);native({type:'selection',id:current.id,session:current.session,canConvert:!!name,selection:name?{...selectedRange(editor),name}:null})}}
function activate(){if(editor){editor.setEditable(true,false);editor.view.dispatch(editor.state.tr)}}
function deactivate(){if(editor?.view.dom.contains(document.activeElement)) (document.activeElement as HTMLElement)?.blur();editor?.commands.blur();if(editor){editor.setEditable(false,false);editor.view.dispatch(editor.state.tr)}}
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
export function bindEditor(instance:Editor){editor=instance;editor.on('update',()=>{dirty=true;emit();selection()});editor.on('selectionUpdate',selection);editor.on('focus',selection);if(current)load(current);else if((window as any).__mojiLoad)load((window as any).__mojiLoad);queueMicrotask(()=>native({type:'ready'}))}
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
