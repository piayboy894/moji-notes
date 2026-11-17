import {Image} from '@tiptap/extension-image'
import {type Editor,useEditorState} from '@tiptap/react'
import {BubbleMenu} from '@tiptap/react/menus'
import {NodeSelection} from '@tiptap/pm/state'
import {closeHistory} from '@tiptap/pm/history'
import {useEffect} from 'react'
export const imageBackgrounds=[{id:'white',name:'白色背景'},{id:'black',name:'黑色背景'},{id:'transparent',name:'透明背景'},{id:'checker',name:'透明网格'}] as const
const valid=(value:unknown)=>imageBackgrounds.some(b=>b.id===value)?String(value):'transparent'
const checker='background-color:#fff;background-image:linear-gradient(45deg,#d4d7dc 25%,transparent 25%,transparent 75%,#d4d7dc 75%),linear-gradient(45deg,#d4d7dc 25%,transparent 25%,transparent 75%,#d4d7dc 75%);background-size:20px 20px;background-position:0 0,10px 10px'
export const MojiImage=Image.extend({
 addAttributes(){return {...this.parent?.(),mojiBackground:{default:'transparent',parseHTML:element=>valid(element.getAttribute('data-moji-image-background')),renderHTML:attrs=>{const background=valid(attrs.mojiBackground);return {'data-moji-image-background':background,style:background==='checker'?checker:background==='transparent'?'background:transparent':'background:'+ (background==='white'?'#fff':'#000')}}}}},
})
function Mounted({ready}:{ready?:()=>void}){useEffect(()=>{ready?.()},[ready]);return null}
export function ImageBackgroundMenu({editor,onReady}:{editor:Editor,onReady?:()=>void}){
 const apply=(id:string)=>{if(valid(editor.getAttributes('image').mojiBackground)===id)return;editor.view.dispatch(closeHistory(editor.state.tr));editor.chain().focus().updateAttributes('image',{mojiBackground:id}).run();editor.view.dispatch(closeHistory(editor.state.tr))}
 const background=useEditorState({editor,selector:({editor})=>valid(editor.getAttributes('image').mojiBackground)})
 return <BubbleMenu editor={editor} pluginKey="mojiImageBackground" updateDelay={0} appendTo={()=>document.body} options={{strategy:'fixed',placement:'top',offset:8,flip:{padding:8},shift:{padding:8}}} shouldShow={({editor,state,view})=>editor.isEditable&&state.selection instanceof NodeSelection&&state.selection.node.type.name==='image'&&(view.hasFocus()||!!document.activeElement?.closest('.moji-image-backgrounds'))} className="moji-image-backgrounds" role="toolbar" aria-label="图片背景">
 <Mounted ready={onReady}/>
 {imageBackgrounds.map(item=><button type="button" key={item.id} title={item.name} aria-label={item.name} aria-pressed={background===item.id} onMouseDown={e=>e.preventDefault()} onClick={()=>apply(item.id)}><span aria-hidden="true" className={'moji-image-swatch '+item.id}/></button>)}
 </BubbleMenu>
}
