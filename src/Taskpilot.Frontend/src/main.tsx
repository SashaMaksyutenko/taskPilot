import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { Provider } from 'react-redux'
import { store } from './store/store'
import './index.css'
import App from './App.tsx'

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    {/* Provider makes the Redux store available to the whole component tree. */}
    <Provider store={store}>
      <App />
    </Provider>
  </StrictMode>,
)
