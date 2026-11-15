import {Node, mergeAttributes, type Editor} from '@tiptap/core'
import {Fragment} from '@tiptap/pm/model'
import {TextSelection,AllSelection} from '@tiptap/pm/state'
import {closeHistory} from '@tiptap/pm/history'

export function selectedRange(editor:Editor){const {selection,doc}=editor.state;if(selection instanceof AllSelection&&doc.childCount===1&&doc.firstChild?.isTextblock)return {from:1,to:doc.content.size-1};return {from:selection.from,to:selection.to}}
export function selectedVariableName(editor:Editor):string|null {
 const {selection,doc}=editor.state
 if(!(selection instanceof TextSelection||selection instanceof AllSelection)||selection.empty)return null
 const {from,to}=selectedRange(editor);const a=doc.resolve(from),b=doc.resolve(to);if(!a.sameParent(b)||a.parent.type.name==='mojiVariable')return null
 const fragment=doc.slice(from,to).content
 let plain=true;fragment.forEach(node=>{if(!node.isText)plain=false})
 const name=doc.textBetween(from,to,'').trim()
 return plain&&name.length>0&&name.length<=2000&&!/[\u0000-\u001f\u007f]/.test(name)?name:null
}
export function convertVariable(editor:Editor,expected?:{from:number,to:number,name:string}):boolean {
 if(expected){if(!Number.isInteger(expected.from)||!Number.isInteger(expected.to)||expected.from<0||expected.to>editor.state.doc.content.size||expected.from>=expected.to)return false;editor.commands.setTextSelection({from:expected.from,to:expected.to});if(selectedVariableName(editor)!==expected.name)return false}
 const name=selectedVariableName(editor);if(!name)return false
 const {from,to}=selectedRange(editor)
 editor.view.dispatch(closeHistory(editor.state.tr))
 const result=editor.chain().focus().insertContentAt({from,to},{type:'mojiVariable',attrs:{id:crypto.randomUUID().replaceAll('-',''),name,value:''}}).run()
 requestAnimationFrame(()=>{const dom=editor.view.nodeDOM(from) as HTMLElement|null;dom?.querySelector<HTMLTextAreaElement>('.moji-variable-input')?.focus()})
 editor.view.dispatch(closeHistory(editor.state.tr))
 return result
}
export const MojiVariable=Node.create({
 name:'mojiVariable',group:'inline',inline:true,atom:true,selectable:true,
 addAttributes(){return {
  id:{default:'',parseHTML:el=>el.getAttribute('data-moji-variable')},
  name:{default:'变量',parseHTML:el=>el.getAttribute('data-name')||el.textContent||'变量'},
  value:{default:'',rendered:false,parseHTML:el=>el.hasAttribute('data-moji-field')?el.textContent||'':''},
 }},
 parseHTML(){return [{tag:'span[data-moji-variable]'}]},
 renderHTML({node,HTMLAttributes}){return ['span',mergeAttributes(HTMLAttributes,{'data-moji-variable':node.attrs.id,'data-name':node.attrs.name,'data-moji-field':'true',class:'moji-variable',style:'white-space:pre-wrap'}),node.attrs.value||'']},
 renderText({node}){return node.attrs.value||''},
 addNodeView(){return ({node,view,getPos,editor})=>{
  let current=node,alive=true
  const dom=document.createElement('span');dom.className='moji-variable';dom.contentEditable='false'
  const size=document.createElement('span');size.className='moji-variable-content';size.setAttribute('aria-hidden','true')
  const input=document.createElement('textarea');input.className='moji-variable-input';input.rows=1;input.autocomplete='off';input.spellcheck=false;input.setAttribute('autocapitalize','off');input.setAttribute('autocorrect','off')
  dom.append(size,input)
  const update=(next:typeof node)=>{
   if(next.type.name!=='mojiVariable')return false;current=next
   dom.dataset.mojiVariable=next.attrs.id;dom.dataset.name=next.attrs.name;dom.dataset.mojiField='true'
   const placeholder='请在这里输入'+next.attrs.name;input.placeholder=placeholder;input.setAttribute('aria-label',next.attrs.name);size.dataset.placeholder=placeholder;size.textContent=next.attrs.value||''
   if(input.value!==(next.attrs.value||'')){const start=input.selectionStart,end=input.selectionEnd;input.value=next.attrs.value||'';if(document.activeElement===input)input.setSelectionRange(Math.min(start,input.value.length),Math.min(end,input.value.length))}
   return true
  }
  input.addEventListener('input',()=>{if(!alive)return;const pos=getPos();if(typeof pos!=='number'||current.attrs.value===input.value)return;view.dispatch(view.state.tr.setNodeMarkup(pos,undefined,{...current.attrs,value:input.value}))})
  input.addEventListener('keydown',event=>{if(event.key==='Tab'&&!event.ctrlKey&&!event.altKey){event.preventDefault();const pos=getPos();if(typeof pos==='number'){editor.commands.setTextSelection(event.shiftKey?pos:pos+current.nodeSize);editor.commands.focus()}}})
  update(node)
  return {dom,update,destroy(){alive=false},stopEvent:event=>input.contains(event.target as globalThis.Node),ignoreMutation:()=>true}
 }},
})
