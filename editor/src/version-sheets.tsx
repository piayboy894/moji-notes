import { useEffect, useState } from 'react'
import { flushSync } from 'react-dom'
import { EditorContent, type Editor } from '@tiptap/react'
import {
  FloatingPortal, autoUpdate, flip, offset, safePolygon, shift,
  useClick, useDismiss, useFloating, useHover, useInteractions,
} from '@floating-ui/react'
import { Button } from '@/components/tiptap-ui-primitive/button'
import { addVersion, jumpToVersion, type PageState } from './native'

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
          onClick={() => { setOpen(false); jumpToVersion(page) }}>版本 {index + 1}</button>)}
      </nav>
    </FloatingPortal>}
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
      {state.prompt && <h2 id={'version-heading-' + index}>版本 {index + 1}</h2>}
      <EditorContent editor={page} role="presentation" className="simple-editor-content" />
    </section>)}
    {state.prompt && state.editable && <button type="button" className="moji-add-version" onClick={append}>
      <span aria-hidden="true">＋</span> 添加版本
    </button>}
  </div>
}
