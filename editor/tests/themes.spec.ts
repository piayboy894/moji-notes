import {test,expect} from '@playwright/test'
import {readFileSync} from 'node:fs'
const palettes=JSON.parse(readFileSync(new URL('../../assets/themes.json',import.meta.url),'utf8')) as {id:string,dark:boolean,colors:Record<string,string>}[]
test('all six native palettes reach canvas, toolbar and fields without changing document or undo history',async({page})=>{
 await page.goto('/');await page.waitForFunction(()=>!!(window as any).MojiEditor?.editor)
 await page.evaluate(()=>(window as any).MojiEditor.load({id:'theme',content:'<!--moji:tiptap:2--><p>正文与主题</p>',variables:[],theme:'light',fontSize:16,shortcuts:{},defaults:{}}))
 await page.evaluate(()=>(window as any).MojiEditor.editor.commands.insertContent('保留编辑'))
 const content=await page.evaluate(()=>(window as any).MojiEditor.snapshot().content)
 for(const p of palettes){
  await page.evaluate(p=>(window as any).MojiEditor.configure({theme:p.dark?'dark':'light',palette:p.id}),p)
  await expect(page.locator('html')).toHaveAttribute('data-palette',p.id)
  const colors=await page.evaluate(()=>{const body=getComputedStyle(document.body),toolbar=getComputedStyle(document.querySelector('.tiptap-toolbar')!);return{body:body.color,bg:body.backgroundColor,toolbar:toolbar.backgroundColor,selection:getComputedStyle(document.querySelector(".tiptap")!,"::selection").backgroundColor,selectionInk:getComputedStyle(document.querySelector(".tiptap")!,"::selection").color}})
  const rgb=(hex:string)=>'rgb('+[1,3,5].map(i=>parseInt(hex.slice(i,i+2),16)).join(', ')+')'
  expect(colors).toEqual({body:rgb(p.colors.Ink),bg:rgb(p.colors.Panel),toolbar:rgb(p.colors.Panel),selection:rgb(p.colors.Selection),selectionInk:rgb(p.colors.Ink)})
  expect(await page.evaluate(()=>(window as any).MojiEditor.snapshot().content)).toBe(content)
  expect(await page.evaluate(()=>(window as any).MojiEditor.editor.can().undo())).toBe(true)
 }
 await page.emulateMedia({media:'print'});expect(await page.locator('body').evaluate(e=>getComputedStyle(e).color)).toBe('rgb(36, 39, 47)')
})

test('display-font changes reach the existing editor without changing content, history or current selection',async({page})=>{
 await page.goto('/');await page.waitForFunction(()=>!!(window as any).MojiEditor?.editor)
 await page.evaluate(()=>{const api=(window as any).MojiEditor;api.load({id:'fonts',content:'<!--moji:tiptap:2--><p>墨记 Sample iii WWW 123</p>',variables:[],theme:'light',fontSize:16,shortcuts:{},defaults:{}});api.editor.commands.insertContent('保留');api.editor.commands.setTextSelection({from:1,to:3});(window as any).__originalEditor=api.editor})
 const before=await page.evaluate(()=>{const api=(window as any).MojiEditor;return {content:api.snapshot().content,selection:api.editor.state.selection.toJSON(),undo:api.editor.can().undo()}})
 for(const fontFamily of ['serif','monospace','']){
  await page.evaluate(fontFamily=>(window as any).MojiEditor.configure({fontFamily,fontUrl:''}),fontFamily)
  await expect(page.locator('.tiptap')).toHaveCSS('font-family',fontFamily?fontFamily+', system-ui, sans-serif':'system-ui, "Microsoft YaHei", sans-serif')
  expect(await page.evaluate(()=>{const api=(window as any).MojiEditor;return {content:api.snapshot().content,selection:api.editor.state.selection.toJSON(),undo:api.editor.can().undo()}})).toEqual(before)
  expect(await page.evaluate(()=>(window as any).__originalEditor===(window as any).MojiEditor.editor)).toBe(true)
 }
})
