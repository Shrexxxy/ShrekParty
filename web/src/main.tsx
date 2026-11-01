import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { App } from './App'
import '@fontsource/golos-text/cyrillic-500.css'
import '@fontsource/golos-text/cyrillic-700.css'
import '@fontsource/golos-text/cyrillic-800.css'
import '@fontsource/golos-text/latin-500.css'
import '@fontsource/golos-text/latin-800.css'
import '@fontsource/unbounded/cyrillic-800.css'
import '@fontsource/unbounded/cyrillic-900.css'
import '@fontsource/unbounded/latin-900.css'
import './index.css'

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <App />
  </StrictMode>,
)
