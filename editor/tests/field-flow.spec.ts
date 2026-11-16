import {test,expect} from '@playwright/test'
const id='12345678901234567890123456789012';const prefix='<!--moji:tiptap:2-->'
async function js(page:any,s:string){return page.evaluate((v:string)=>(0,eval)(v),s)}
test.beforeEach(async({page})=>{await page.goto('/');await page.waitForFunction(()=>!!(window as any).MojiEditor?.editor)})
test('inline field fills from the preceding text and breaks across normal text lines',async({page})=>{
 const value='很长的填写内容'.repeat(30)
 await page.evaluate(({id,prefix,value})=>(window as any).MojiEditor.load({id:'flow',session:'flow',content:prefix+`<p>请你参考项目：的<span data-moji-variable="${id}" data-name="项目" data-moji-field="true">${value}</span> 后续正文</p>`,variables:[{id,name:'项目'}],theme:'light',fontSize:16,shortcuts:{},defaults:{}}),{id,prefix,value})
 const field=page.locator('.moji-variable-content');await expect(field).toHaveText(value)
 const rect=await field.evaluate(e=>{const r=document.createRange();r.selectNodeContents(e);return {pieces:[...r.getClientRects()].map(x=>({x:x.x,y:x.y,width:x.width})),prefix:(()=>{const p=document.createRange();p.selectNode(e.parentElement!.previousSibling!);return p.getBoundingClientRect().toJSON()})()}})
 expect(rect.pieces.length).toBeGreaterThan(1);expect(Math.abs(rect.pieces[0].y-rect.prefix.y)).toBeLessThan(3);expect(Math.abs(rect.pieces[0].x-(rect.prefix.x+rect.prefix.width))).toBeLessThan(5)
 expect(rect.pieces[1].x).toBeLessThan(rect.pieces[0].x);expect(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth)).toBe(true)
})
test('native editing remains inside a field at empty and end boundaries, clear and refill, IME and undo',async({page})=>{
 await js(page,"window.MojiEditor.load({id:'fill',content:'受众',variables:[],theme:'light',fontSize:16,shortcuts:{},defaults:{}});window.MojiEditor.editor.commands.setTextSelection({from:1,to:3});window.MojiEditor.action('convertVariable')")
 const field=page.locator('.moji-variable-content');await expect(field).toHaveAttribute('data-placeholder','请在这里输入受众');await expect(field).toBeFocused()
 await page.keyboard.type('ABC123');await expect(field).toHaveText('ABC123');
 for(let i=0;i<6;i++)await page.keyboard.press('Backspace');await expect(field).toHaveText('');await page.keyboard.type('Refill');await expect(field).toHaveText('Refill')
 await field.press('Control+a');await page.keyboard.press('Backspace');await expect(field).toHaveText('');await expect(page.locator('.moji-variable')).toHaveCount(1)
 await page.keyboard.insertText('中文');await expect(field).toHaveText('中文');await page.keyboard.press('End');await page.keyboard.insertText('末尾');await expect(field).toHaveText('中文末尾')
 await page.keyboard.press('Tab');await page.keyboard.insertText('外部');await expect(field).toHaveText('中文末尾');expect(await js(page,'window.MojiEditor.editor.getText()')).toBe('中文末尾外部')
})

test('inline fill accepts Chinese composition, explicit newlines and plain text paste without losing its placeholder',async({page})=>{
 await js(page,"window.MojiEditor.load({id:'ime',content:'主题',variables:[],theme:'light',fontSize:16,shortcuts:{undo:'Ctrl+Z',redo:'Ctrl+Y'},defaults:{}});window.MojiEditor.editor.commands.setTextSelection({from:1,to:3});window.MojiEditor.action('convertVariable')")
 const field=page.locator('.moji-variable-content');await expect(field).toBeFocused();const cdp=await page.context().newCDPSession(page)
 await cdp.send('Input.imeSetComposition',{text:'中文输入',selectionStart:4,selectionEnd:4});await cdp.send('Input.insertText',{text:'中文输入'});await expect(field).toHaveText('中文输入')
 await page.keyboard.press('Enter');await page.keyboard.insertText('第二行');await expect.poll(()=>js(page,'window.MojiEditor.editor.getText()')).toBe('中文输入\n第二行')

 await field.evaluate(e=>{const data=new DataTransfer();data.setData('text/plain','\n粘贴文字');data.setData('text/html','<b>不能变成格式代码</b>');e.dispatchEvent(new ClipboardEvent('paste',{bubbles:true,cancelable:true,clipboardData:data}))})
 await expect.poll(()=>js(page,'window.MojiEditor.editor.getText()')).toBe('中文输入\n第二行\n粘贴文字')
 const saved=await js(page,'window.MojiEditor.snapshot()');await page.evaluate(saved=>(window as any).MojiEditor.load({...saved,id:'ime-again',theme:'light',fontSize:16,shortcuts:{},defaults:{}}),saved);await expect.poll(()=>js(page,'window.MojiEditor.editor.getText()')).toBe('中文输入\n第二行\n粘贴文字')
 await field.click();await field.press('Control+a');await page.keyboard.press('Backspace');await expect(field).toHaveAttribute('data-empty','true');expect(await js(page,'window.MojiEditor.editor.getText()')).toBe('')
})

test('body uses its available width and native title inset instead of a centered narrow column',async({page})=>{
 await page.setViewportSize({width:1360,height:800});await js(page,"window.MojiEditor.load({id:'width',content:'正文',variables:[],theme:'light',fontSize:16,contentInset:28,shortcuts:{},defaults:{}})")
 const bounds=await page.getByRole('textbox',{name:'笔记正文'}).evaluate(e=>({x:e.getBoundingClientRect().x,width:e.getBoundingClientRect().width,padding:parseFloat(getComputedStyle(e).paddingLeft)}))
 expect(bounds.x+bounds.padding).toBe(28);expect(bounds.width).toBeGreaterThan(1300)
})
