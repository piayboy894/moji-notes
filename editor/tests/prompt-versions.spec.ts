import { test, expect, type Page } from '@playwright/test'

const prefix = '<!--moji:tiptap:2-->'
async function load(page: Page, content: string, prompt = false, variables: object[] = []) {
  await page.evaluate(payload => (window as any).MojiEditor.load(payload), {
    id: prompt ? 'prompt' : 'ordinary', session: 'session-' + Math.random(), content, prompt,
    theme: 'light', fontSize: 16, variables, shortcuts: {}, defaults: {},
  })
}
test.beforeEach(async ({ page }) => {
  await page.addInitScript(() => {
    const w = window as any
    w.messages = []
    w.MojiNative = { postMessage: (data: string) => w.messages.push(JSON.parse(data)) }
  })
  await page.goto('/')
  await page.waitForFunction(() => !!(window as any).MojiEditor?.editor)
})

test('Ctrl+A then Ctrl+C includes visible list markers in plain text and preserves rich HTML', async ({ page, context }) => {
  await context.grantPermissions(['clipboard-read', 'clipboard-write'])
  await load(page, prefix + '<p>开始</p><ol start="7"><li><p>甲</p></li><li><p>乙</p><ol><li><p>嵌套</p></li></ol></li></ol><ul><li><p>结束</p></li></ul><p>尾段</p>')
  const body = page.getByRole('textbox', { name: '笔记正文' })
  await body.press('Control+a')
  await page.keyboard.press('Control+c')
  expect(await page.evaluate(() => navigator.clipboard.readText())).toBe('开始\n7. 甲\n8. 乙\n  a. 嵌套\n• 结束\n尾段')
  const html = await page.evaluate(async () => (await (await navigator.clipboard.read())[0].getType('text/html')).text())
  expect(html).toContain('<ol start="7">')
  await body.locator('li p').nth(1).evaluate(paragraph => {
    const editor = (window as any).MojiEditor.editor
    const start = editor.view.posAtDOM(paragraph, 0)
    editor.commands.setTextSelection({ from: start, to: start + paragraph.textContent!.length })
  })
  await expect.poll(() => page.evaluate(() => window.getSelection()?.toString())).toBe('乙')
  await page.keyboard.press('Control+c')
  await expect.poll(() => page.evaluate(() => navigator.clipboard.readText())).toBe('8. 乙')
})

test('Tab edits the paragraph or list and keeps focus in the body', async ({ page }) => {
  await load(page, prefix + '<p>正文</p><ol><li><p>第一</p></li><li><p>第二</p></li></ol>')
  const body = page.getByRole('textbox', { name: '笔记正文' })
  await body.focus()
  await page.keyboard.press('Control+Home')
  await expect.poll(() => page.evaluate(() => (window as any).MojiEditor.editor.state.selection.from)).toBe(1)
  await page.keyboard.press('Tab')
  expect(await body.locator('p').first().textContent()).toBe('\t正文')
  await expect(body).toBeFocused()
  await page.keyboard.press('Shift+Tab')
  expect(await body.locator('p').first().textContent()).toBe('正文')
  await body.locator('li p').last().click({ position: { x: 6, y: 10 } })
  await expect.poll(() => page.evaluate(() => (window as any).MojiEditor.editor.state.selection.$from.parent.textContent)).toBe('第二')
  await page.keyboard.press('Tab')
  await expect(body.locator('ol ol li')).toHaveText('第二')
  await page.keyboard.press('Shift+Tab')
  await expect(body.locator('ol > li')).toHaveCount(2)
  await expect(body).toBeFocused()
  await expect(page.getByRole('dialog')).toHaveCount(0)
})

test('prompt sheets grow, navigate and reopen together with formatting, fields and empty versions', async ({ page, isMobile }) => {
  const errors: string[] = []
  page.on('pageerror', error => errors.push(error.message))
  await load(page, '旧提示词', true)
  const sheets = page.locator('.moji-version-sheet')
  const add = page.getByRole('button', { name: '添加版本' })
  await expect(sheets).toHaveCount(1)
  await expect(sheets.first()).toContainText('旧提示词')
  await add.click()
  const second = sheets.nth(1).getByRole('textbox', { name: '笔记正文' })
  await expect(second).toBeFocused()
  await page.keyboard.insertText('受众')
  await page.keyboard.press('Control+a')
  await page.evaluate(() => (window as any).MojiEditor.action('convertVariable'))
  await page.keyboard.insertText('设计师')
  await page.keyboard.press('Tab')
  await page.keyboard.insertText('。')
  await add.click()
  await expect(sheets.nth(2).getByRole('textbox')).toBeFocused()
  const first = sheets.first().getByRole('textbox')
  await first.click()
  await page.keyboard.press('End')
  await page.keyboard.insertText('长段落'.repeat(1800))
  await expect.poll(async () => (await sheets.first().boundingBox())!.height).toBeGreaterThan(page.viewportSize()!.height)
  const nav = page.getByRole('button', { name: '版本导航', exact: true })
  if (isMobile) await nav.tap()
  else await nav.hover()
  const menu = page.getByRole('navigation', { name: '版本导航' })
  await menu.getByRole('button', { name: '版本 2' }).click()
  await expect(second).toBeFocused()
  await expect(sheets.nth(1).getByRole('heading')).toBeInViewport()
  await page.keyboard.press('Control+a')
  await page.getByRole('button', { name: '加粗', exact: true }).click()
  await expect(second.locator('strong')).not.toHaveCount(0)
  await expect(first.locator('strong')).toHaveCount(0)
  await page.evaluate(() => (window as any).MojiEditor.flush('save'))
  const saved = await page.evaluate(() => (window as any).messages.filter((m: any) => m.type === 'change').at(-1))
  await load(page, '另一条记录')
  await expect(sheets).toHaveCount(0)
  await expect(nav).toHaveCount(0)
  await load(page, saved.content, true, saved.variables)
  await expect(sheets).toHaveCount(3)
  await expect(first).toContainText('旧提示词长段落')
  await expect(second.locator('.moji-variable-content')).toHaveText('设计师')
  await expect(second.locator('strong')).not.toHaveCount(0)
  await expect(sheets.last().getByRole('textbox')).toHaveText('')
  expect(errors).toEqual([])
})

test('short sheets fit their text and the workspace covers the area under the add button', async ({ page }) => {
  await load(page, '一行', true)
  const sheet = page.locator('.moji-version-sheet').first()
  const body = sheet.getByRole('textbox')
  const compactHeight = (await sheet.boundingBox())!.height
  expect(compactHeight).toBeLessThan(110)
  const background = await page.evaluate(() => {
    const workspace = document.querySelector('.moji-prompt-editor')!
    const bottom = document.elementFromPoint(innerWidth / 2, innerHeight - 2)!
    return {
      covered: workspace === bottom || workspace.contains(bottom),
      color: getComputedStyle(bottom).backgroundColor,
      soft: getComputedStyle(workspace).getPropertyValue('--moji-soft').trim(),
    }
  })
  expect(background.covered).toBe(true)
  expect(background.color).toBe('rgb(' + background.soft.slice(1).match(/../g)!.map(x => parseInt(x, 16)).join(', ') + ')')
  await body.focus()
  await page.keyboard.insertText('长段落'.repeat(650))
  await expect.poll(async () => (await sheet.boundingBox())!.height).toBeGreaterThan(compactHeight * 2)
  await body.press('Control+a')
  await page.keyboard.insertText('短')
  await expect.poll(async () => (await sheet.boundingBox())!.height).toBe(compactHeight)
})

test('rename one or several versions, reopen, navigate and cancel an edit', async ({ page, isMobile }) => {
  await load(page, '初稿正文', true)
  const headings = page.locator('.moji-version-sheet h2')
  const title = headings.first().getByRole('button')
  if (isMobile) { await title.tap(); await title.tap() } else await title.dblclick()
  const input = page.getByRole('textbox', { name: '版本名称' })
  const name = '审校 & <最终> "稿"'
  await input.fill(name)
  await input.press('Enter')
  const saved = () => page.evaluate(() => (window as any).messages.filter((m: any) => m.type === 'change').at(-1).content)
  const single = await saved()
  await load(page, '另一条提示词', true)
  await expect(headings.first()).toHaveText('版本 1')
  await load(page, single, true)
  await expect(headings.first()).toHaveText(name)
  await page.getByRole('button', { name: '添加版本' }).click()
  const second = headings.nth(1).getByRole('button')
  await second.focus()
  await second.press('Enter')
  await input.fill('精简稿')
  await input.press('Tab')
  const both = await saved()
  await load(page, both, true)
  await expect(second).toHaveText('精简稿')
  await second.dblclick()
  await input.fill('取消改名')
  await input.press('Escape')
  await expect(second).toHaveText('精简稿')
  await page.getByRole('button', { name: '版本导航' }).click()
  const menu = page.getByRole('navigation', { name: '版本导航' })
  await expect(menu.getByRole('button', { name })).toBeVisible()
  await menu.getByRole('button', { name: '精简稿' }).click()
  await expect(page.getByRole('textbox', { name: '笔记正文' }).nth(1)).toBeFocused()
})

test('narrow toolbars page through every control and restore full width on resize', async ({ page }) => {
  await load(page, '正文', true)
  await page.setViewportSize({ width: 320, height: 360 })
  const next = page.getByRole('button', { name: '显示右侧工具' })
  const prev = page.getByRole('button', { name: '显示左侧工具' })
  const nav = page.getByRole('button', { name: '版本导航' })
  await expect(prev).toBeDisabled()
  const viewport = page.locator('.moji-toolbar-viewport')
  const steps = await viewport.evaluate(el => Math.ceil((el.scrollWidth - el.clientWidth) / (el.clientWidth - 40)))
  for (let i = 0; i < steps; ++i) await next.click()
  await expect(next).toBeDisabled()
  await expect(nav).toBeInViewport({ ratio: 1 })
  await nav.click()
  await page.getByRole('navigation').getByRole('button', { name: '版本 1' }).click()
  const backSteps = await viewport.evaluate(el => Math.ceil(el.scrollLeft / (el.clientWidth - 40)))
  for (let i = 0; i < backSteps; ++i) await prev.click()
  await expect(prev).toBeDisabled()
  await expect(page.getByRole('button', { name: '撤销', exact: true })).toBeInViewport({ ratio: 1 })
  await page.setViewportSize({ width: 1200, height: 640 })
  await expect(next).toHaveCount(0)
  await expect(nav).toBeInViewport({ ratio: 1 })
})
