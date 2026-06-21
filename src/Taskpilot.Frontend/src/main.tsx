import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { Provider } from 'react-redux'
import { store } from './store/store'
import './index.css'
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
