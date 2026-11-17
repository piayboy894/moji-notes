import {test,expect} from '@playwright/test'
const payload=(id:string)=>({id,session:id,content:'已保存的正文 '+id,variables:[],theme:'light',fontSize:16,shortcuts:{},defaults:{}})
test('background readiness and document acknowledgement do not depend on animation frames',async({page})=>{
 await page.addInitScript(()=>{const w=window as any;w.nativeMessages=[];w.MojiNative={postMessage:(m:string)=>w.nativeMessages.push(JSON.parse(m))};w.requestAnimationFrame=()=>1})
 await page.goto('/');await expect.poll(()=>page.evaluate(()=>(window as any).nativeMessages.some((m:any)=>m.type==='ready'))).toBe(true)
 await page.evaluate(p=>(window as any).MojiEditor.load(p),payload('background'))
 await expect.poll(()=>page.evaluate(()=>(window as any).nativeMessages.some((m:any)=>m.type==='loaded'&&m.session==='background'))).toBe(true)
 expect(await page.locator('.tiptap').textContent()).toContain('已保存的正文 background')
})
test('desktop message commands acknowledge load, flush and errors without script RPC results',async({page})=>{
 await page.addInitScript(()=>{const w=window as any;w.nativeMessages=[];w.chrome??={};w.chrome.webview={postMessage:(m:any)=>w.nativeMessages.push(m),addEventListener:(_:string,handler:any)=>w.deliver=handler}})
 await page.goto('/');await expect.poll(()=>page.evaluate(()=>(window as any).nativeMessages.some((m:any)=>m.type==='ready'))).toBe(true)
 for(const [request,command,value] of [['one','ping',null],['two','load',payload('message')],['three','flush',null],['four','unknown',null]])await page.evaluate(m=>(window as any).deliver({data:{protocol:'moji',request:m[0],command:m[1],value:m[2]}}),[request,command,value])
 const replies=await page.evaluate(()=>(window as any).nativeMessages.filter((m:any)=>m.type==='reply').map((m:any)=>[m.request,m.ok]))
 expect(replies).toEqual([['one',true],['two',true],['three',true],['four',false]])
 expect(await page.locator('.tiptap').textContent()).toContain('已保存的正文 message')
})
test('superseded loads never announce the previous document as ready',async({page})=>{
 await page.addInitScript(()=>{const w=window as any;w.nativeMessages=[];w.MojiNative={postMessage:(m:string)=>w.nativeMessages.push(JSON.parse(m))}})
 await page.goto('/');await page.waitForFunction(()=>!!(window as any).MojiEditor?.editor)
 await page.evaluate(([a,b])=>{const w=window as any;w.nativeMessages=[];w.MojiEditor.load(a);w.MojiEditor.load(b)},[payload('old'),payload('latest')])
 await expect.poll(()=>page.evaluate(()=>(window as any).nativeMessages.filter((m:any)=>m.type==='loaded').map((m:any)=>m.id))).toEqual(['latest'])
})
