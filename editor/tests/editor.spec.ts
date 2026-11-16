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
 const checkbox=body.locator('li input[type=checkbox]').first();await expect(checkbox).toBeVisible();expect(await checkbox.evaluate(e=>getComputedStyle(e).opacity)).toBe('1');expect((await checkbox.boundingBox())!.width).toBeGreaterThan(12);const label=body.locator('li label').first();const box=await label.boundingBox();const para=await body.locator('li p').first().boundingBox();expect(Math.abs(box!.y-para!.y)).toBeLessThan(10);await label.click();await expect(body.locator('li').first()).toHaveAttribute('data-checked','true');
 await js(page,"window.MojiEditor.editor.chain().focus('end').run()");await page.keyboard.press('Enter');await page.keyboard.press('Enter');await page.keyboard.insertText('普通段落');await expect(body.locator(':scope > p').last()).toHaveText('普通段落');
})
test('selection converts to an empty editable field, retains its name and copies only entered text',async({page})=>{
 const body=page.getByRole('textbox',{name:'笔记正文'});await body.click();await page.keyboard.insertText('原样 {{变量}} 受众');
 await js(page,"const e=window.MojiEditor.editor;const end=e.state.doc.content.size-1;e.commands.setTextSelection({from:end-2,to:end});window.MojiEditor.action('convertVariable')");
 const field=body.locator('.moji-variable');await expect(field).toHaveAttribute('data-name','受众');await expect(field).toHaveText('');await expect(field.locator('.moji-variable-content')).toHaveAttribute('data-placeholder','请在这里输入受众');
 await page.keyboard.insertText('产品设计师');await expect(field).toHaveText('产品设计师');expect(await js(page,'window.MojiEditor.editor.getText()')).toBe('原样 {{变量}} 产品设计师');
 const saved=await js(page,'window.MojiEditor.snapshot()');await load(page,saved.content,saved.variables);await expect(field).toHaveText('产品设计师');
 await field.click();await page.keyboard.press('End');await page.keyboard.press('Backspace');await expect(field).toHaveText('产品设计');
 await field.locator('.moji-variable-content').press('Control+a');await page.keyboard.press('Backspace');await expect(field).toHaveText('');await expect(field).toHaveCount(1);
 await page.keyboard.insertText('开发者');await expect(field).toHaveText('开发者');expect((await js(page,'window.MojiEditor.snapshot()')).variables[0].name).toBe('受众');
})
test('field conversion and typing have separate undo boundaries and long values wrap',async({page})=>{
 await load(page,prefix+'<p>主题</p>');await js(page,"window.MojiEditor.editor.commands.setTextSelection({from:1,to:3});window.MojiEditor.action('convertVariable')");await page.keyboard.insertText('很长的填写内容'.repeat(35));
 const field=page.locator('.moji-variable');await expect(field).toContainText('很长的填写内容');expect(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth)).toBe(true);
 await page.keyboard.press('Control+z');await expect(field).toHaveText('');await page.keyboard.press('Control+z');await expect(field).toHaveCount(0);await expect(page.getByRole('textbox',{name:'笔记正文'})).toHaveText('主题');
 await page.keyboard.press('Control+y');await expect(field).toHaveCount(1);await page.keyboard.press('Control+y');await expect(field).toContainText('很长的填写内容');
})
test('ordinary typing does not create fields and invalid selections do not convert',async({page})=>{
 await page.getByRole('textbox',{name:'笔记正文'}).click();await page.keyboard.insertText('{{受众}}');expect((await js(page,'window.MojiEditor.snapshot()')).variables).toEqual([]);expect(await js(page,"window.MojiEditor.action('convertVariable')")).toBe(false);
 const id='12345678901234567890123456789012';await load(page,prefix+`<p><span data-moji-variable="${id}" data-name="受众">受众</span></p>`,[{id,name:'受众'}]);await expect(page.locator('.moji-variable')).toHaveText('');await page.locator('.moji-variable').click();await page.keyboard.insertText('真实内容');await expect(page.locator('.moji-variable')).toHaveText('真实内容');
})
test('images upload locally, display and persist after reopening with no external request',async({page})=>{
 const errors:string[]=[];page.on('pageerror',e=>errors.push(e.message));const remote:string[]=[];page.on('request',r=>{if(!r.url().startsWith('http://127.0.0.1')&&!r.url().startsWith('data:')&&!r.url().startsWith('blob:'))remote.push(r.url())});
 await page.getByRole('textbox',{name:'笔记正文'}).click();const picking=page.waitForEvent('filechooser');await page.getByRole('button',{name:'插入图片',exact:true}).click();
 await (await picking).setFiles({name:'image.png',mimeType:'image/png',buffer:Buffer.from('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aZuoAAAAASUVORK5CYII=','base64')});
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
 await js(page,"window.MojiEditor.editor.chain().focus('end').run()");await expect(page.getByRole('textbox',{name:'笔记正文'})).toBeFocused();const cdp=await page.context().newCDPSession(page);await cdp.send('Input.imeSetComposition',{text:'中文输入',selectionStart:4,selectionEnd:4});await cdp.send('Input.insertText',{text:'中文输入'});await expect(page.getByRole('textbox',{name:'笔记正文'})).toContainText('中文输入');expect((await js(page,'window.MojiEditor.snapshot()')).variables).toEqual([])
})

test('toolbar stays visible after loading, scroll and window resize',async({page})=>{const bar=page.getByRole('toolbar');await expect(bar).toBeInViewport();await js(page,"window.MojiEditor.editor.commands.insertContent('<p>正文</p>'.repeat(100))");await js(page,"window.MojiEditor.editor.chain().focus('end').run()");await expect(bar).toBeInViewport();await page.setViewportSize({width:360,height:420});await expect(bar).toBeInViewport()})
test('official toolbar toggles lists, quotation, code and alignment back to ordinary text',async({page})=>{
 const body=page.getByRole('textbox',{name:'笔记正文'});await body.click();await page.keyboard.insertText('Toolbar text');
 for(const label of ['引用','代码块']){await page.getByRole('button',{name:label,exact:true}).click();await page.getByRole('button',{name:label,exact:true}).click();expect(await html(page)).not.toContain(label==='引用'?'<blockquote>':'<pre>')}
 for(const label of ['项目列表','编号列表','待办事项']){await page.getByRole('button',{name:'列表',exact:true}).click();await page.getByRole('menuitem',{name:label,exact:true}).click();await page.getByRole('button',{name:'列表',exact:true}).click();await page.getByRole('menuitem',{name:label,exact:true}).click();expect(await html(page)).not.toMatch(/<(ul|ol)/)}
 await page.getByRole('button',{name:'居中',exact:true}).click();expect(await html(page)).toContain('text-align: center');await page.getByRole('button',{name:'左对齐',exact:true}).click();
 await body.press('Control+a');for(const label of ['加粗','斜体','下划线','删除线'])await page.getByRole('button',{name:label,exact:true}).click();const text=await html(page);for(const tag of ['strong','em','u','s'])expect(text).toContain('<'+tag+'>');
})

test('canceling image selection leaves the document unchanged',async({page})=>{await page.getByRole('textbox',{name:'笔记正文'}).click();await page.keyboard.insertText('保留正文');const before=await html(page);const picking=page.waitForEvent('filechooser');await page.getByRole('button',{name:'插入图片',exact:true}).click();await (await picking).setFiles([]);expect(await html(page)).toBe(before)})

test.describe('older Android WebView committed input',()=>{
 test.use({userAgent:'Mozilla/5.0 (Linux; Android 15; wv) AppleWebKit/537.36 (KHTML, like Gecko) Version/4.0 Chrome/124.0.6367.219 Mobile Safari/537.36'})
 test('committed characters advance the caret, remain literal and retain undo',async({page})=>{
  const body=page.getByRole('textbox',{name:'笔记正文'});await body.click()
  let expected=''
  for(const data of 'ABC123<{{原样}}'){
   // Reproduce WebView's actual sequence: a committed insertion followed by a
   // stale DOM caret. The editor must receive the native mutation unchanged.
   expect(await page.evaluate(data=>{const view=(window as any).MojiEditor.editor.view;const allowed=view.dom.dispatchEvent(new InputEvent('beforeinput',{bubbles:true,cancelable:false,inputType:'insertText',data}));const selection=window.getSelection()!;const range=selection.getRangeAt(0);let node=selection.anchorNode!;let offset=selection.anchorOffset;if(node.nodeType===Node.TEXT_NODE){(node as Text).insertData(offset,data)}else{node=document.createTextNode(data);range.insertNode(node);offset=0}selection.setPosition(node,offset);view.dom.dispatchEvent(new InputEvent('input',{bubbles:true,inputType:'insertText',data}));return allowed},data)).toBe(true)
   expected+=data;await expect.poll(()=>js(page,'window.MojiEditor.editor.getText()')).toBe(expected)
  }
  await expect(body).toHaveText('ABC123<{{原样}}');expect((await js(page,'window.MojiEditor.snapshot()')).variables).toEqual([])
  await page.waitForTimeout(600);await page.keyboard.press('Backspace');await expect(body).toHaveText('ABC123<{{原样}');await page.keyboard.press('Control+z');await expect(body).toHaveText('ABC123<{{原样}}')
  expect(await js(page,"window.MojiEditor.editor.view.dom.dispatchEvent(new InputEvent('beforeinput',{bubbles:true,cancelable:true,isComposing:true,inputType:'insertCompositionText',data:'中'}))")).toBe(true)
 })
})

test('all toolbar controls exist before body focus and stay in place across activation and document switches',async({page})=>{
 const bar=page.getByRole('toolbar');const buttons=bar.locator('button');
 await expect(buttons).toHaveCount(20);
 const labels=await buttons.evaluateAll(items=>items.map(e=>e.getAttribute('aria-label')));
 expect(await js(page,"document.activeElement===window.MojiEditor.editor.view.dom")).toBe(false);
 await expect(bar.getByRole('button',{name:'加粗',exact:true})).toBeEnabled();
 await expect(bar.getByRole('button',{name:'引用',exact:true})).toBeEnabled();
 await expect(bar.getByRole('button',{name:'撤销',exact:true})).toBeDisabled();
 // The first interaction can be a toolbar command, without first clicking the body.
 await bar.getByRole('button',{name:'引用',exact:true}).click();
 expect(await html(page)).toContain('<blockquote>');
 await js(page,'window.MojiEditor.deactivate()');
 await expect(buttons).toHaveCount(20);await expect(bar.getByRole('button',{name:'加粗',exact:true})).toBeDisabled();
 await js(page,'window.MojiEditor.activate()');
 await expect(bar.getByRole('button',{name:'加粗',exact:true})).toBeEnabled();
 await js(page,"window.MojiEditor.editor.commands.blur()");
 await load(page,prefix+'<p>另一条记录</p>');
 await expect(buttons).toHaveCount(20);expect(await buttons.evaluateAll(items=>items.map(e=>e.getAttribute('aria-label')))).toEqual(labels);
 await expect(bar.getByRole('button',{name:'撤销',exact:true})).toBeDisabled();
 await expect(bar.getByRole('button',{name:'插入图片',exact:true})).toBeEnabled();
})

test('startup page uses requested background before a document is opened and reports painted readiness',async({page})=>{
 await page.goto('/?theme=dark');
 await page.waitForFunction(()=>(window as any).nativeMessages?.some((m:any)=>m.type==='ready'));
 expect(await page.evaluate(()=>getComputedStyle(document.documentElement).backgroundColor)).toBe('rgb(29, 31, 35)');
 expect(await js(page,'window.MojiEditor.recordId')).toBeUndefined();
 await expect(page.getByRole('toolbar').locator('button')).toHaveCount(20);
 await expect(page.getByRole('button',{name:'加粗',exact:true})).toBeDisabled();
 await load(page,prefix+'<p>第一次打开</p>');await expect(page.getByRole('button',{name:'加粗',exact:true})).toBeEnabled();
 expect(await page.evaluate(()=>getComputedStyle(document.documentElement).backgroundColor)).toBe('rgb(255, 255, 255)');
})
