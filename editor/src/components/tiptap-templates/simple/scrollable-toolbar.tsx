import { useLayoutEffect, useRef, useState, type ReactNode } from "react"
import { Toolbar } from "@/components/tiptap-ui-primitive/toolbar"
import { ChevronDownIcon } from "@/components/tiptap-icons/chevron-down-icon"

export function ScrollableToolbar({ children, view }: { children: ReactNode, view: string }) {
  const row = useRef<HTMLDivElement>(null)
  const viewport = useRef<HTMLDivElement>(null)
  const toolbar = useRef<HTMLDivElement>(null)
  const [overflow, setOverflow] = useState(false)
  const [atStart, setAtStart] = useState(true)
  const [atEnd, setAtEnd] = useState(false)

  useLayoutEffect(() => {
    const frame = row.current!, scroll = viewport.current!, content = toolbar.current!
    function measure() {
      setOverflow(content.offsetWidth > frame.clientWidth + 1)
      setAtStart(scroll.scrollLeft <= 1)
      setAtEnd(scroll.scrollWidth - scroll.clientWidth - scroll.scrollLeft <= 1)
    }
    scroll.scrollLeft = 0
    const observer = new ResizeObserver(measure)
    for (const element of [frame, scroll, content]) observer.observe(element)
    scroll.addEventListener("scroll", measure, { passive: true })
    measure()
    return () => { observer.disconnect(); scroll.removeEventListener("scroll", measure) }
  }, [view])

  function arrow(next: boolean) {
    const label = next ? "显示右侧工具" : "显示左侧工具"
    return <button type="button" className="moji-toolbar-arrow" aria-label={label} title={label}
      disabled={next ? atEnd : atStart} onMouseDown={event => event.preventDefault()} onClick={() => {
        const scroll = viewport.current!
        scroll.scrollBy({ left: (next ? 1 : -1) * Math.max(32, scroll.clientWidth - 40) })
      }}><ChevronDownIcon style={{ transform: next ? "rotate(-90deg)" : "rotate(90deg)" }} /></button>
  }
  return <div ref={row} className="moji-toolbar-row">
    {overflow && arrow(false)}
    <div ref={viewport} className="moji-toolbar-viewport">
      <Toolbar ref={toolbar}>{children}</Toolbar>
    </div>
    {overflow && arrow(true)}
  </div>
}
