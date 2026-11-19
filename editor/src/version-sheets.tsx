import { useEffect, useRef, useState } from 'react'
import { flushSync } from 'react-dom'
import { EditorContent, type Editor } from '@tiptap/react'
import {
  FloatingPortal, autoUpdate, flip, offset, safePolygon, shift,
  useClick, useDismiss, useFloating, useHover, useInteractions,
} from '@floating-ui/react'
import { Button } from '@/components/tiptap-ui-primitive/button'
import { TrashIcon } from '@/components/tiptap-icons/trash-icon'
import { addVersion, deleteVersion, jumpToVersion, renameVersion, versionLabel, type PageState } from './native'

export function VersionNavigation({ state }: { state: PageState }) {
  const [open, setOpen] = useState(false)
  const { refs, floatingStyles, context } = useFloating({
    open, onOpenChange: setOpen, placement: 'bottom-end',
    middleware: [offset(4), flip(), shift({ padding: 8 })], whileElementsMounted: autoUpdate,
  })
  const hover = useHover(context, { mouseOnly: true, handleClose: safePolygon() })
  const click = useClick(context)
  const dismiss = useDismiss(context)
  const { getReferenceProps, getFloatingProps } = useInteractions([hover, click, dismiss])
  useEffect(() => { if (!state.editable) setOpen(false) }, [state.editable])
  return <>
    <Button ref={refs.setReference} type="button" data-style="ghost" aria-label="版本导航"
      aria-expanded={open} aria-controls={open ? 'version-navigation' : undefined}
      {...getReferenceProps()}>
      <svg aria-hidden="true" className="tiptap-button-icon" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.7">
        <rect x="3" y="4" width="18" height="16" rx="2" /><path d="M10 4v16m3-11h5m-5 4h5" />
      </svg>
      导航
    </Button>
    {open && <FloatingPortal>
      <nav id="version-navigation" aria-label="版本导航" className="moji-version-nav"
        ref={refs.setFloating} style={floatingStyles} {...getFloatingProps()}>
        {state.pages.map((page, index) => <button type="button" key={page.instanceId}
          aria-current={page === state.active ? 'page' : undefined}
          onClick={() => { setOpen(false); jumpToVersion(page) }}>{versionLabel(page, index)}</button>)}
      </nav>
    </FloatingPortal>}
  </>
}

function VersionHeading({ page, index, editable }: { page: Editor, index: number, editable: boolean }) {
  const label = versionLabel(page, index)
  const [draft, setDraft] = useState<string | null>(null)
  const original = useRef(label)
  const input = useRef<HTMLInputElement>(null)
  const lastTap = useRef(-Infinity)
  const editing = editable && draft !== null
  useEffect(() => { if (editing) { input.current?.focus(); input.current?.select() } }, [editing])
  useEffect(() => { if (!editable) setDraft(null) }, [editable])
  function start() { original.current = label; setDraft(label) }
  function save(value: string) { renameVersion(page, value.trim() === '版本 ' + (index + 1) ? '' : value) }
  return <h2 id={'version-heading-' + index}>
    {editing ? <input ref={input} aria-label="版本名称" value={draft} onChange={event => {
      setDraft(event.target.value)
      save(event.target.value)
    }} onBlur={() => setDraft(null)} onKeyDown={event => {
      if (event.nativeEvent.isComposing || event.keyCode === 229) return
      if (event.key === 'Escape') save(original.current)
      if (event.key === 'Enter' || event.key === 'Escape') {
        event.preventDefault()
        setDraft(null)
      }
    }} /> : editable ? <button type="button" className="moji-version-name" title="双击修改版本名称"
      onDoubleClick={start} onClick={event => { if (event.detail === 0) start() }}
      onPointerUp={event => {
        if (event.pointerType !== 'touch') return
        if (event.timeStamp - lastTap.current < 350) start()
        lastTap.current = event.timeStamp
      }}>{label}</button> : label}
  </h2>
}

function DeleteVersionButton({ page, label }: { page: Editor, label: string }) {
  const dialog = useRef<HTMLDialogElement>(null)
  function remove() {
    dialog.current?.close()
    let next: Editor | undefined
    flushSync(() => { next = deleteVersion(page) })
    if (next) jumpToVersion(next)
  }
  return <>
    <button type="button" className="moji-delete-version" aria-label={'删除版本：' + label}
      title="删除版本" onClick={() => dialog.current?.showModal()}><TrashIcon aria-hidden="true" /></button>
    <dialog className="moji-delete-dialog" ref={dialog} aria-label={'删除“' + label + '”？'}>
      <p>删除“{label}”？</p>
      <div>
        <button type="button" onClick={() => dialog.current?.close()}>取消</button>
        <button type="button" className="confirm" onClick={remove}>删除</button>
      </div>
    </dialog>
  </>
}

export function VersionSheets({ state, primary }: { state: PageState, primary: Editor | null }) {
  const pages = state.pages.length ? state.pages : primary ? [primary] : []
  function append() {
    let page: Editor | undefined
    flushSync(() => { page = addVersion() })
    if (page) jumpToVersion(page)
  }
  return <div className={state.prompt ? 'moji-version-stack' : 'moji-plain-body'}>
    {pages.map((page, index) => <section className={state.prompt ? 'moji-version-sheet' : 'moji-plain-body'} key={page.instanceId}
      aria-labelledby={state.prompt ? 'version-heading-' + index : undefined}>
      {state.prompt && <header key={state.session} className="moji-sheet-header">
        <VersionHeading page={page} index={index} editable={state.editable} />
        {state.editable && pages.length > 1 && <DeleteVersionButton page={page} label={versionLabel(page, index)} />}
      </header>}
      <EditorContent editor={page} role="presentation" className="simple-editor-content" />
    </section>)}
    {state.prompt && state.editable && <button type="button" className="moji-add-version" onClick={append}>
      <span aria-hidden="true">＋</span> 添加版本
    </button>}
  </div>
}
