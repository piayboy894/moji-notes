import {Node, mergeAttributes, type Editor} from '@tiptap/core'
import {Fragment} from '@tiptap/pm/model'
import {TextSelection,AllSelection} from '@tiptap/pm/state'
import {closeHistory} from '@tiptap/pm/history'

export function selectedRange(editor:Editor){const {selection,doc}=editor.state;if(selection instanceof AllSelection&&doc.childCount===1&&doc.firstChild?.isTextblock)return {from:1,to:doc.content.size-1};return {from:selection.from,to:selection.to}}
export function selectedVariableName(editor:Editor):string|null {
 if(document.activeElement?.closest('.moji-variable-input'))return null
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
  let current=node,alive=true,composing=false
  const dom=document.createElement('span');dom.className='moji-variable';dom.contentEditable='false'
  // A separate plain-text editing host can fragment across line boxes. Unlike
  // a textarea, it is not an indivisible inline box in the paragraph layout.
  const input=document.createElement('span');input.className='moji-variable-input moji-variable-content';input.contentEditable='plaintext-only';input.spellcheck=false;input.setAttribute('role','textbox');input.setAttribute('aria-multiline','true');input.setAttribute('autocapitalize','off');input.setAttribute('autocorrect','off')
  dom.append(input)
  const read=()=>input.textContent||''
  const offsets=()=>{const selection=document.getSelection();if(!selection||!input.contains(selection.anchorNode)||!input.contains(selection.focusNode))return null;const offset=(node:globalThis.Node,pos:number)=>{const range=document.createRange();range.selectNodeContents(input);range.setEnd(node,pos);return range.toString().length};return {anchor:offset(selection.anchorNode!,selection.anchorOffset),head:offset(selection.focusNode!,selection.focusOffset)}}
  const trail=()=>{const end=input.querySelector('br[data-moji-trailing]');if(read().endsWith('\n')){if(!end){const br=document.createElement('br');br.dataset.mojiTrailing='true';br.setAttribute('aria-hidden','true');input.append(br)}}else end?.remove()}
  const restore=(anchor:number,head:number)=>{const point=(offset:number):[globalThis.Node,number]=>{const walker=document.createTreeWalker(input,NodeFilter.SHOW_TEXT);let node=walker.nextNode(),last:globalThis.Node=input;while(node){last=node;const size=node.textContent?.length||0;if(offset<=size)return [node,offset];offset-=size;node=walker.nextNode()}return [last,last===input?0:last.textContent?.length||0]};const a=point(anchor),b=point(head);document.getSelection()?.setBaseAndExtent(a[0],a[1],b[0],b[1])}
  const update=(next:typeof node)=>{
   if(next.type.name!=='mojiVariable')return false;current=next
   dom.dataset.mojiVariable=next.attrs.id;dom.dataset.name=next.attrs.name;dom.dataset.mojiField='true'
   input.dataset.placeholder='请在这里输入'+next.attrs.name;input.setAttribute('aria-label',next.attrs.name)
   if(read()!==(next.attrs.value||'')){const selection=offsets();input.textContent=next.attrs.value||'';if(document.activeElement===input&&selection)restore(selection.anchor,selection.head)}
   if(!composing)trail();input.dataset.empty=read()?'false':'true';return true
  }
  const commit=()=>{if(!alive)return;const value=read();if(!value&&!composing){input.replaceChildren();if(document.activeElement===input)restore(0,0)};input.dataset.empty=value?'false':'true';const pos=getPos();if(typeof pos!=='number'||current.attrs.value===value)return;view.dispatch(view.state.tr.setNodeMarkup(pos,undefined,{...current.attrs,value}))}
  input.addEventListener('input',commit);input.addEventListener('compositionstart',()=>{composing=true});input.addEventListener('compositionend',()=>{composing=false;commit()})
  const insertText=(text:string)=>{const selected=offsets();const a=selected?Math.min(selected.anchor,selected.head):read().length;const b=selected?Math.max(selected.anchor,selected.head):a;input.textContent=read().slice(0,a)+text+read().slice(b);trail();input.focus({preventScroll:true});restore(a+text.length,a+text.length);commit()};
  input.addEventListener('beforeinput',event=>{if((event as InputEvent).inputType==='insertParagraph'||(event as InputEvent).inputType==='insertLineBreak'){event.preventDefault();insertText('\n')}})
  input.addEventListener('paste',event=>{const text=event.clipboardData?.getData('text/plain');if(text!==undefined){event.preventDefault();insertText(text)}})
  input.addEventListener('keydown',event=>{
   if(event.key==='End'||event.key==='Home'){event.preventDefault();const selected=offsets();const point=event.key==='End'?read().length:0;restore(event.shiftKey&&selected?selected.anchor:point,point)}
   if((event.ctrlKey||event.metaKey)&&!event.altKey&&event.key.toLowerCase()==='a'){event.preventDefault();const range=document.createRange();range.selectNodeContents(input);const selection=document.getSelection();selection?.removeAllRanges();selection?.addRange(range)}
   if(event.key==='Tab'&&!event.ctrlKey&&!event.altKey){event.preventDefault();const pos=getPos();if(typeof pos==='number'){editor.commands.setTextSelection(event.shiftKey?pos:pos+current.nodeSize);editor.view.focus()}}
  })
  for(const type of ['copy','cut'])input.addEventListener(type,event=>{const selected=offsets(),clipboard=(event as ClipboardEvent).clipboardData;if(!selected||!clipboard)return;const a=Math.min(selected.anchor,selected.head),b=Math.max(selected.anchor,selected.head);clipboard.setData('text/plain',read().slice(a,b));event.preventDefault();if(type==='cut')insertText('')})
  dom.addEventListener('click',()=>{const selection=document.getSelection();if(selection&&!selection.isCollapsed&&input.contains(selection.anchorNode)&&input.contains(selection.focusNode))return;const selected=offsets();input.focus({preventScroll:true});restore(selected?.anchor??read().length,selected?.head??read().length)})
  update(node)
  return {dom,update,destroy(){alive=false},stopEvent:event=>input.contains(event.target as globalThis.Node),ignoreMutation:()=>true}
 }},
})
