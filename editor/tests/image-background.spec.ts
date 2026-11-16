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
