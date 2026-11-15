import type {EditorView} from '@tiptap/pm/view'

// Android WebView 124 can expose the old DOM caret while committing hardware
// keyboard text with the IME hidden. Use ProseMirror's standard text-input path
// for cancelable commits; never intercept composition or replacement events.
export function handleLegacyAndroidInput(view:EditorView,event:Event):boolean {
  const input=event as InputEvent
  const version=/Chrome\/(\d+)/.exec(navigator.userAgent)
  if(!/Android/.test(navigator.userAgent)||!version||Number(version[1])>124||
    input.inputType!=='insertText'||!input.data||!input.cancelable||input.isComposing||view.composing)return false
  const {from,to}=view.state.selection
  const text=input.data
  const insert=()=>view.state.tr.insertText(text,from,to).scrollIntoView()
  if(!view.someProp('handleTextInput',handler=>handler(view,from,to,text,insert)))view.dispatch(insert())
  input.preventDefault()
  return true
}
