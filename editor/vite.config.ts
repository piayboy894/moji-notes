import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import path from 'node:path'
export default defineConfig({base:'./',plugins:[react()],resolve:{alias:{'@':path.resolve(__dirname,'src')}},build:{target:'chrome90',outDir:'../assets/editor',emptyOutDir:true,assetsInlineLimit:4096,rollupOptions:{output:{entryFileNames:'editor-[hash].js',chunkFileNames:'chunk-[hash].js',assetFileNames:'editor-[hash].[ext]'}}}})
