import {test,expect} from '@playwright/test'
test('selected images have four independent saved backgrounds, preserve original bytes and support undo',async({page})=>{
 await page.goto('/');await page.waitForFunction(()=>!!(window as any).MojiEditor?.editor)
 const src='data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aZuoAAAAASUVORK5CYII='
 await page.evaluate(src=>(window as any).MojiEditor.load({id:'pictures',content:'<!--moji:tiptap:2--><p>图片</p><img width="160" src="'+src+'"><p>分隔</p><img width="160" src="'+src+'"><p>结束</p>',variables:[],theme:'dark',palette:'ocean',fontSize:16,shortcuts:{},defaults:{}}),src)
 const first=page.locator('.tiptap img').first(),second=page.locator('.tiptap img').nth(1),bar=page.getByRole('toolbar',{name:'图片背景'})
 await first.click();await expect(bar).toBeVisible()
 for(const [name,id,bg]of [['白色背景','white','rgb(255, 255, 255)'],['黑色背景','black','rgb(0, 0, 0)'],['透明背景','transparent','rgba(0, 0, 0, 0)'],['透明网格','checker','rgb(255, 255, 255)']]){
  await bar.getByRole('button',{name,exact:true}).click();await expect(first).toHaveAttribute('data-moji-image-background',id);expect(await first.evaluate(e=>getComputedStyle(e).backgroundColor)).toBe(bg);await expect(first).toHaveAttribute('src',src);await expect(second).toHaveAttribute('data-moji-image-background','transparent')
 }
 expect(await first.evaluate(e=>getComputedStyle(e).backgroundImage)).toContain('linear-gradient')
 await page.keyboard.press('Control+z');await expect(first).toHaveAttribute('data-moji-image-background','transparent');await page.keyboard.press('Control+z');await expect(first).toHaveAttribute('data-moji-image-background','black');await page.keyboard.press('Control+y');await expect(first).toHaveAttribute('data-moji-image-background','transparent');await page.keyboard.press('Control+y');await expect(first).toHaveAttribute('data-moji-image-background','checker')
 const saved=await page.evaluate(()=>(window as any).MojiEditor.snapshot());await page.evaluate(saved=>(window as any).MojiEditor.load({...saved,id:'again',theme:'light',palette:'paper',fontSize:16,shortcuts:{},defaults:{}}),saved);await expect(first).toHaveAttribute('data-moji-image-background','checker');await expect(second).toHaveAttribute('data-moji-image-background','transparent');await page.getByRole('textbox',{name:'笔记正文'}).click({position:{x:15,y:12}});await expect(bar).toBeHidden()
})
test('upload preserves transparent WebP pixels and background changes leave embedded pixels intact',async({page})=>{
 await page.goto('/');await page.waitForFunction(()=>!!(window as any).MojiEditor?.editor);await page.evaluate(()=>(window as any).MojiEditor.load({id:'upload',content:'',variables:[],theme:'dark',palette:'obsidian',fontSize:16,shortcuts:{},defaults:{}}))
 const webp=await page.evaluate(()=>{const canvas=document.createElement('canvas');canvas.width=120;canvas.height=80;const c=canvas.getContext('2d')!;c.fillStyle='#6790b0';c.fillRect(30,20,60,40);return canvas.toDataURL('image/webp')})
 const picking=page.waitForEvent('filechooser');await page.getByRole('button',{name:'插入图片',exact:true}).click();await(await picking).setFiles({name:'alpha.webp',mimeType:'image/webp',buffer:Buffer.from(webp.split(',')[1],'base64')})
 const image=page.locator('.tiptap img');await expect(image).toHaveAttribute('src',/^data:image\/png/);await expect.poll(()=>image.evaluate((img:HTMLImageElement)=>img.complete&&img.naturalWidth>0)).toBe(true)
 const src=await image.getAttribute('src');expect(await image.evaluate((img:HTMLImageElement)=>{const c=document.createElement('canvas');c.width=img.naturalWidth;c.height=img.naturalHeight;const ctx=c.getContext('2d')!;ctx.drawImage(img,0,0);return ctx.getImageData(0,0,1,1).data[3]})).toBe(0)
 await image.click();await page.getByRole('toolbar',{name:'图片背景'}).getByRole('button',{name:'黑色背景',exact:true}).click();await expect(image).toHaveAttribute('data-moji-image-background','black');await expect(image).toHaveAttribute('src',src!)
})
