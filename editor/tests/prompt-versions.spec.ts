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
