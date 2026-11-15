import {Extension} from '@tiptap/core'
import {Plugin,TextSelection} from '@tiptap/pm/state'
import type {EditorView} from '@tiptap/pm/view'

// WebView 124 may report the old DOM caret for a committed IME insertion when
// the hardware keyboard hides the IME. Preserve native input and correct only
// that stale selection in ProseMirror's own default transaction. Input rules
// run first; composing text, replacement events and newer WebViews are untouched.
export const LegacyAndroidInput=Extension.create({
  name:'legacyAndroidInput',
  priority:1,
  addProseMirrorPlugins(){
    const version=/Chrome\/(\d+)/.exec(navigator.userAgent)
    if(!/Android/.test(navigator.userAgent)||!version||Number(version[1])>124)return []
    const pending=new WeakMap<EditorView,{from:number,to:number,text:string}>()
    return [new Plugin({props:{
      handleDOMEvents:{beforeinput(view,event){
        const input=event as InputEvent
        if(input.inputType==='insertText'&&input.data&&!input.isComposing&&!view.composing)
          pending.set(view,{from:view.state.selection.from,to:view.state.selection.to,text:input.data})
        else pending.delete(view)
        return false
      }},
      handleTextInput(view,from,to,text,defaultTransaction){
        const commit=pending.get(view)
        pending.delete(view)
        if(!commit||commit.from!==from||commit.to!==to||commit.text!==text)return false
        const transaction=defaultTransaction()
        if(!transaction.selection.empty||transaction.selection.from!==from)return false
        transaction.setSelection(TextSelection.create(transaction.doc,from+text.length))
        view.dispatch(transaction)
        return true
      },
    }})]
  },
})
