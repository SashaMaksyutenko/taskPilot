import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { Provider } from 'react-redux'
import { store } from './store/store'
// Self-hosted Inter font (no external CDN requests) — the app's brand typeface.
import '@fontsource/inter/400.css'
import '@fontsource/inter/500.css'
import '@fontsource/inter/600.css'
import '@fontsource/inter/700.css'
import './index.css'
import './lib/i18n' // initialize localization (i18next) before the app renders
import App from './App.tsx'

// Apply the saved theme before the app renders to avoid a flash of the wrong theme.
if (localStorage.getItem('theme') === 'dark') {
  document.documentElement.classList.add('dark')
}

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    {/* Provider makes the Redux store available to the whole component tree. */}
    <Provider store={store}>
      <App />
    </Provider>
  </StrictMode>,
)
