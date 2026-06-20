import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom'
import GuestRoute from './components/GuestRoute'
import ProtectedRoute from './components/ProtectedRoute'
import BoardPage from './pages/BoardPage'
import ChatPage from './pages/ChatPage'
import HomePage from './pages/HomePage'
import ProjectsPage from './pages/ProjectsPage'
import RegisterPage from './pages/RegisterPage'
import LoginPage from './pages/LoginPage'

/**
 * Root component. Sets up client-side routing with auth guards:
 * - Guest-only routes (login, register) redirect logged-in users home.
 * - Protected routes (home) redirect anonymous users to login.
 */
function App() {
  return (
    <BrowserRouter>
      <Routes>
        {/* Guest-only pages */}
        <Route element={<GuestRoute />}>
          <Route path="/login" element={<LoginPage />} />
          <Route path="/register" element={<RegisterPage />} />
        </Route>

        {/* Authenticated-only pages */}
        <Route element={<ProtectedRoute />}>
          <Route path="/" element={<HomePage />} />
          <Route path="/chat" element={<ChatPage />} />
          <Route path="/projects" element={<ProjectsPage />} />
          <Route path="/projects/:projectId" element={<BoardPage />} />
        </Route>

        {/* Unknown routes go home (which itself redirects to login if needed). */}
        <Route path="*" element={<Navigate to="/" replace />} />
      </Routes>
    </BrowserRouter>
  )
}

export default App
