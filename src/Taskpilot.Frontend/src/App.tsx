import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom'
import { ToastContainer } from 'react-toastify'
import 'react-toastify/dist/ReactToastify.css'
import AppShell from './components/layout/AppShell'
import AdminRoute from './components/routing/AdminRoute'
import FeatureRoute from './components/routing/FeatureRoute'
import GuestRoute from './components/routing/GuestRoute'
import ProtectedRoute from './components/routing/ProtectedRoute'
import AdminPage from './pages/AdminPage'
import AuditPage from './pages/AuditPage'
import BoardPage from './pages/BoardPage'
import CalendarPage from './pages/CalendarPage'
import ChatPage from './pages/ChatPage'
import ForumPage from './pages/ForumPage'
import MarketplacePage from './pages/MarketplacePage'
import MarketplaceTaskPage from './pages/MarketplaceTaskPage'
import AssistantPage from './pages/AssistantPage'
import NotesPage from './pages/NotesPage'
import BookmarksPage from './pages/BookmarksPage'
import SearchPage from './pages/SearchPage'
import ProjectsPage from './pages/ProjectsPage'
import RootPage from './pages/RootPage'
import SettingsPage from './pages/SettingsPage'
import TopicPage from './pages/TopicPage'
import UserProfilePage from './pages/UserProfilePage'
import RegisterPage from './pages/RegisterPage'
import LoginPage from './pages/LoginPage'
import ForgotPasswordPage from './pages/ForgotPasswordPage'
import ResetPasswordPage from './pages/ResetPasswordPage'
import GoogleCallbackPage from './pages/GoogleCallbackPage'
import GitHubCallbackPage from './pages/GitHubCallbackPage'
import LinkedInCallbackPage from './pages/LinkedInCallbackPage'

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

        {/* Password recovery — reachable regardless of auth (the emailed link must
            always work, even if the user happens to be signed in). */}
        <Route path="/forgot-password" element={<ForgotPasswordPage />} />
        <Route path="/reset-password" element={<ResetPasswordPage />} />

        {/* OAuth redirect targets (handle the ?code= exchange). */}
        <Route path="/auth/google/callback" element={<GoogleCallbackPage />} />
        <Route path="/auth/github/callback" element={<GitHubCallbackPage />} />
        <Route path="/auth/linkedin/callback" element={<LinkedInCallbackPage />} />

        {/* Site root: landing for guests, dashboard for logged-in users. */}
        <Route path="/" element={<RootPage />} />

        {/* Authenticated-only pages (shared sidebar layout) */}
        <Route element={<ProtectedRoute />}>
          <Route element={<AppShell />}>
            <Route path="/chat" element={<ChatPage />} />
            <Route path="/assistant" element={<AssistantPage />} />
            <Route path="/projects" element={<ProjectsPage />} />
            <Route path="/projects/:projectId" element={<BoardPage />} />
            <Route path="/calendar" element={<CalendarPage />} />
            {/* Forum + Marketplace can be switched off org-wide by an admin. */}
            <Route element={<FeatureRoute feature="forum" />}>
              <Route path="/forum" element={<ForumPage />} />
              <Route path="/forum/:topicId" element={<TopicPage />} />
            </Route>
            <Route element={<FeatureRoute feature="marketplace" />}>
              <Route path="/marketplace" element={<MarketplacePage />} />
              <Route path="/marketplace/:taskId" element={<MarketplaceTaskPage />} />
            </Route>
            <Route path="/settings" element={<SettingsPage />} />
            <Route path="/notes" element={<NotesPage />} />
            <Route path="/bookmarks" element={<BookmarksPage />} />
            <Route path="/search" element={<SearchPage />} />
            <Route path="/users/:userId" element={<UserProfilePage />} />
            <Route element={<AdminRoute />}>
              <Route path="/admin" element={<AdminPage />} />
              <Route path="/admin/audit" element={<AuditPage />} />
            </Route>
          </Route>
        </Route>

        {/* Unknown routes go home (which itself redirects to login if needed). */}
        <Route path="*" element={<Navigate to="/" replace />} />
      </Routes>
    </BrowserRouter>
  )
}

export default App
