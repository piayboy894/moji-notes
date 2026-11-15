import {Node, mergeAttributes, type Editor} from '@tiptap/core'
import {EditorState} from '@tiptap/pm/state'
import {closeHistory} from '@tiptap/pm/history'
export const PREFIX='<!--moji:tiptap:1-->'
type Variable={id:string,name:string}
type Payload={id:string,content:string,variables:Variable[],theme:string,fontSize:number,shortcuts:Record<string,string>,defaults:Record<string,string>,readOnly?:boolean}
let editor:Editor|null=null
let current:Payload|null=null
let dirty=false
const native=(message:object)=>{const text=JSON.stringify(message);const w=window as any;if(w.chrome?.webview)w.chrome.webview.postMessage(message);else w.MojiNative?.postMessage(text)}
export const reportImageError=()=>native({type:'action',id:current?.id,action:'image-error'})
export const openExternal=(url:string)=>{if(/^https?:\/\//i.test(url))native({type:'openLink',id:current?.id,url})}
export const MojiVariable=Node.create({name:'mojiVariable',group:'inline',inline:true,atom:true,selectable:true,
 addAttributes(){return {id:{default:'',parseHTML:el=>el.getAttribute('data-moji-variable')},name:{default:'变量',parseHTML:el=>el.getAttribute('data-name')||el.textContent}}},
 parseHTML(){return [{tag:'span[data-moji-variable]'}]},
 renderHTML({node,HTMLAttributes}){return ['span',mergeAttributes(HTMLAttributes,{'data-moji-variable':node.attrs.id,'data-name':node.attrs.name,class:'moji-variable',contenteditable:'false'}),node.attrs.name]},
 renderText({node}){return node.attrs.name}
})
function snapshot(){const variables:Variable[]=[];editor?.state.doc.descendants(node=>{if(node.type.name==='mojiVariable'&&/^[a-f0-9]{32}$/.test(node.attrs.id)&&!variables.some(v=>v.id===node.attrs.id))variables.push({id:node.attrs.id,name:node.attrs.name})});return {content:PREFIX+(editor?.getHTML()||'<p></p>'),variables,text:editor?.getText()||''}}
function emit(){if(!editor||!current||!dirty)return;native({type:'change',id:current.id,...snapshot()});dirty=false}
function literal(content:string,variables:Variable[]){const box=document.createElement('div');const pattern=/!\[[^\]]*\]\(moji-variable:([a-f0-9]{32})\)/g
 for(const line of content.split('\n')){const p=document.createElement('p');let at=0;for(const m of line.matchAll(pattern)){const v=variables.find(v=>v.id===m[1]);if(!v)continue;p.append(document.createTextNode(line.slice(at,m.index)));const span=document.createElement('span');span.dataset.mojiVariable=v.id;span.dataset.name=v.name;span.textContent=v.name;p.append(span);at=m.index!+m[0].length}p.append(document.createTextNode(line.slice(at)));box.append(p)}return box.innerHTML}
function theme(payload:Partial<Payload>){document.documentElement.classList.toggle('dark',payload.theme==='dark');document.documentElement.style.setProperty('--moji-font-size',`${payload.fontSize||16}px`)}
function load(payload:Payload){current=payload;theme(payload);if(!editor)return;dirty=false;editor.commands.setContent(payload.content.startsWith(PREFIX)?payload.content.slice(PREFIX.length):literal(payload.content,payload.variables||[]),{emitUpdate:false});editor.view.updateState(EditorState.create({schema:editor.schema,doc:editor.state.doc,plugins:editor.state.plugins}));editor.setEditable(!payload.readOnly,false);dirty=false;native({type:'loaded',id:payload.id})}
function action(name:string,value?:any){if(!editor||editor.isDestroyed)return false;const chain=editor.chain().focus();switch(name){case 'undo':return chain.undo().run();case 'redo':return chain.redo().run();case 'bold':return chain.toggleBold().run();case 'insertVariable':{editor.view.dispatch(closeHistory(editor.state.tr));const inserted=chain.insertContent({type:'mojiVariable',attrs:value}).run();editor.view.dispatch(closeHistory(editor.state.tr));return inserted;}default:return false}}
export function bindEditor(instance:Editor){editor=instance;editor.on('update',()=>{dirty=true;emit()});if(current)load(current);else if((window as any).__mojiLoad)load((window as any).__mojiLoad);native({type:'ready'})}
;(window as any).MojiEditor={load,theme,action,flush:(token?:string)=>{emit();native({type:'flushed',id:current?.id,token})},snapshot,get recordId(){return current?.id},get editor(){return editor}}
document.addEventListener('keydown',event=>{if(!current||event.isComposing)return;const chord=(event.ctrlKey?'Ctrl+':'')+(event.altKey?'Alt+':'')+(event.shiftKey?'Shift+':'')+event.key.toUpperCase();const item=Object.entries(current.shortcuts||{}).find(([,binding])=>binding&&binding===chord)
 if(item){event.preventDefault();event.stopImmediatePropagation();if(!action(item[0]))native({type:'action',id:current.id,action:item[0]})}
 else if(Object.entries(current.defaults||{}).some(([id,binding])=>binding===chord&&current!.shortcuts[id]!==binding)){event.preventDefault();event.stopImmediatePropagation()}
},true)
