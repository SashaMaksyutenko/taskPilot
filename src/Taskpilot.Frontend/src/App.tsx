import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom'
import { ToastContainer } from 'react-toastify'
import 'react-toastify/dist/ReactToastify.css'
import AdminRoute from './components/AdminRoute'
import GuestRoute from './components/GuestRoute'
import ProtectedRoute from './components/ProtectedRoute'
import AdminPage from './pages/AdminPage'
import AuditPage from './pages/AuditPage'
import BoardPage from './pages/BoardPage'
import CalendarPage from './pages/CalendarPage'
import ChatPage from './pages/ChatPage'
import ForumPage from './pages/ForumPage'
import MarketplacePage from './pages/MarketplacePage'
import MarketplaceTaskPage from './pages/MarketplaceTaskPage'
import NotesPage from './pages/NotesPage'
import SearchPage from './pages/SearchPage'
import ProjectsPage from './pages/ProjectsPage'
import RootPage from './pages/RootPage'
import SettingsPage from './pages/SettingsPage'
import TopicPage from './pages/TopicPage'
import UserProfilePage from './pages/UserProfilePage'
import RegisterPage from './pages/RegisterPage'
import LoginPage from './pages/LoginPage'
import GoogleCallbackPage from './pages/GoogleCallbackPage'

/**
 * Root component. Sets up client-side routing with auth guards:
 * - Guest-only routes (login, register) redirect logged-in users home.
 * - Protected routes (home) redirect anonymous users to login.
 */
function App() {
  return (
    <BrowserRouter>
      {/* Global toast notifications (top-right, auto-dismiss). */}
      <ToastContainer position="top-right" autoClose={3000} theme="colored" newestOnTop />
      <Routes>
        {/* Guest-only pages */}
        <Route element={<GuestRoute />}>
          <Route path="/login" element={<LoginPage />} />
          <Route path="/register" element={<RegisterPage />} />
        </Route>

        {/* Google OAuth redirect target (handles the ?code= exchange). */}
        <Route path="/auth/google/callback" element={<GoogleCallbackPage />} />

        {/* Site root: landing for guests, dashboard for logged-in users. */}
        <Route path="/" element={<RootPage />} />

        {/* Authenticated-only pages */}
        <Route element={<ProtectedRoute />}>
          <Route path="/chat" element={<ChatPage />} />
          <Route path="/projects" element={<ProjectsPage />} />
          <Route path="/projects/:projectId" element={<BoardPage />} />
          <Route path="/calendar" element={<CalendarPage />} />
          <Route path="/forum" element={<ForumPage />} />
          <Route path="/forum/:topicId" element={<TopicPage />} />
          <Route path="/marketplace" element={<MarketplacePage />} />
          <Route path="/marketplace/:taskId" element={<MarketplaceTaskPage />} />
          <Route path="/settings" element={<SettingsPage />} />
          <Route path="/notes" element={<NotesPage />} />
          <Route path="/search" element={<SearchPage />} />
          <Route path="/users/:userId" element={<UserProfilePage />} />
          <Route element={<AdminRoute />}>
            <Route path="/admin" element={<AdminPage />} />
            <Route path="/admin/audit" element={<AuditPage />} />
          </Route>
        </Route>

        {/* Unknown routes go home (which itself redirects to login if needed). */}
        <Route path="*" element={<Navigate to="/" replace />} />
      </Routes>
    </BrowserRouter>
  )
}

export default App
