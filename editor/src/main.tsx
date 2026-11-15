import {createRoot} from 'react-dom/client'
import {SimpleEditor} from '@/components/tiptap-templates/simple/simple-editor'
import '@/native'
import './index.scss'
import './moji.scss'
createRoot(document.getElementById('app')!).render(<SimpleEditor />)
