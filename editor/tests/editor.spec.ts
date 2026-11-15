import {test,expect,type Page} from '@playwright/test'
const prefix='<!--moji:tiptap:1-->'
async function load(page:Page,content='',variables:any[]=[]){await page.evaluate(({content,variables})=>{const w=window as any;w.MojiEditor.load({id:'test',content,variables,theme:'light',fontSize:16,shortcuts:{undo:'Ctrl+Z',redo:'Ctrl+Y'},defaults:{}})},{content,variables});await expect(page.getByRole('textbox',{name:'笔记正文'})).toBeVisible()}
async function js(page:Page,script:string){return page.evaluate(s=>(0,eval)(s),script)}
async function html(page:Page){return js(page,'window.MojiEditor.editor.getHTML()')}
test.beforeEach(async({page})=>{await page.addInitScript(()=>{(window as any).nativeMessages=[];(window as any).MojiNative={postMessage:(m:string)=>(window as any).nativeMessages.push(JSON.parse(m))}});await page.goto('/');await page.waitForFunction(()=>!!(window as any).MojiEditor?.editor);await load(page)})
test('quote exits by Enter and Backspace without leaving paragraph indentation',async({page})=>{
 const body=page.getByRole('textbox',{name:'笔记正文'});await body.click();await page.getByRole('button',{name:'引用',exact:true}).click();await page.keyboard.insertText('引用');await page.keyboard.press('Enter');await page.keyboard.press('Enter');await page.keyboard.insertText('后续正文');await expect(body.locator(':scope > p').first()).toHaveText('后续正文');expect(await html(page)).toContain('<blockquote><p>引用</p></blockquote><p>后续正文</p>');
 await load(page,prefix+'<blockquote><p>可删除</p></blockquote>');await js(page,"window.MojiEditor.editor.chain().focus('start').run()");await page.keyboard.press('Backspace');expect(await html(page)).not.toContain('blockquote');
 await body.press('Control+a');await body.press('Backspace');expect(await js(page,'window.MojiEditor.editor.getText()')).toBe('')
})
test('interactive tasks keep checkbox at first line and exit empty item with Enter',async({page})=>{
 const body=page.getByRole('textbox',{name:'笔记正文'});await body.click();await page.getByRole('button',{name:'列表',exact:true}).click();await page.getByRole('menuitem',{name:'待办事项'}).click();await page.keyboard.insertText('多行待办 '.repeat(35));await page.keyboard.press('Shift+Enter');await page.keyboard.insertText('第二行');
 const label=body.locator('li label').first();const box=await label.boundingBox();const para=await body.locator('li p').first().boundingBox();expect(Math.abs(box!.y-para!.y)).toBeLessThan(10);await label.click();await expect(body.locator('li').first()).toHaveAttribute('data-checked','true');
 await js(page,"window.MojiEditor.editor.chain().focus('end').run()");await page.keyboard.press('Enter');await page.keyboard.press('Enter');await page.keyboard.insertText('普通段落');await expect(body.locator(':scope > p').last()).toHaveText('普通段落');
})
test('variables are explicit atomic nodes, literal braces stay text, undo survives insertion',async({page})=>{
 const body=page.getByRole('textbox',{name:'笔记正文'});await body.click();await page.keyboard.insertText('原样 {{变量}} ');expect((await js(page,'window.MojiEditor.snapshot()')).variables).toEqual([]);
 await js(page,"window.MojiEditor.action('insertVariable',{id:'12345678901234567890123456789012',name:'受众'})");await expect(body.locator('.moji-variable')).toHaveText('受众');await page.keyboard.press('Backspace');await expect(body.locator('.moji-variable')).toHaveCount(0);await page.keyboard.press('Control+z');await expect(body.locator('.moji-variable')).toHaveCount(1);
 const saved=await js(page,'window.MojiEditor.snapshot()');await load(page,saved.content,saved.variables);expect(await js(page,'window.MojiEditor.editor.can().undo()')).toBe(false);await expect(body).toContainText('{{变量}}');await expect(body.locator('.moji-variable')).toHaveText('受众');
})
test('images upload locally, display and persist after reopening with no external request',async({page})=>{
 const errors:string[]=[];page.on('pageerror',e=>errors.push(e.message));const remote:string[]=[];page.on('request',r=>{if(!r.url().startsWith('http://127.0.0.1')&&!r.url().startsWith('data:')&&!r.url().startsWith('blob:'))remote.push(r.url())});
 await page.getByRole('textbox',{name:'笔记正文'}).click();await page.getByRole('button',{name:'插入图片',exact:true}).click();
 await page.locator('input[type=file]').setInputFiles({name:'image.png',mimeType:'image/png',buffer:Buffer.from('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aZuoAAAAASUVORK5CYII=','base64')});
 const image=page.locator('.tiptap img');await expect(image).toHaveCount(1);await expect(image).toHaveAttribute('src',/^data:image/);expect(await image.evaluate((i:HTMLImageElement)=>i.complete&&i.naturalWidth>0)).toBe(true);
 const saved=await js(page,'window.MojiEditor.snapshot()');await load(page,saved.content);await expect(image).toHaveCount(1);expect(errors).toEqual([]);expect(remote).toEqual([])
})
test('styles, links, headings and undo use the same document at narrow and wide widths',async({page})=>{
 const body=page.getByRole('textbox',{name:'笔记正文'});await body.click();await page.keyboard.insertText('中文 English');await page.keyboard.press('Control+a');await page.getByRole('button',{name:'加粗',exact:true}).click();expect(await html(page)).toContain('<strong>中文 English</strong>');await js(page,"window.MojiEditor.editor.chain().focus().toggleItalic().setLink({href:'https://example.com'}).run()");expect(await html(page)).toContain('href="https://example.com"');await page.keyboard.press('Control+z');await page.keyboard.press('Control+y');
 await page.setViewportSize({width:340,height:640});await expect(body).toContainText('中文 English');expect(await page.evaluate(()=>document.documentElement.scrollWidth<=window.innerWidth)).toBe(true);await page.setViewportSize({width:1400,height:800});await expect(body).toContainText('中文 English');
})
test('native change and flush acknowledgement carry final edit, switch resets history',async({page})=>{
 const body=page.getByRole('textbox',{name:'笔记正文'});await body.click();await page.keyboard.insertText('最后一次输入');await js(page,"window.MojiEditor.flush('close')");const messages=await js(page,'window.nativeMessages');expect(messages.filter((m:any)=>m.type==='change').at(-1).content).toContain('最后一次输入');expect(messages.at(-1)).toMatchObject({type:'flushed',token:'close'});
 await load(page,prefix+'<p>另一个记录</p>');expect(await js(page,'window.MojiEditor.editor.can().undo()')).toBe(false);await page.keyboard.press('Control+z');await expect(body).toHaveText('另一个记录')
})

test('old archive text stays literal, composition commits Chinese without creating variables',async({page})=>{
 const source='# 标题\n> 引用\n- [x] 待办\n{{原样}}';await load(page,source);expect(await html(page)).not.toContain('<blockquote>');expect(await js(page,"window.MojiEditor.editor.getText({blockSeparator:'\\n'})")).toBe(source);
 await js(page,"window.MojiEditor.editor.chain().focus('end').run()");const cdp=await page.context().newCDPSession(page);await cdp.send('Input.imeSetComposition',{text:'中文输入',selectionStart:4,selectionEnd:4});await cdp.send('Input.insertText',{text:'中文输入'});await expect(page.getByRole('textbox',{name:'笔记正文'})).toContainText('中文输入');expect((await js(page,'window.MojiEditor.snapshot()')).variables).toEqual([])
})
