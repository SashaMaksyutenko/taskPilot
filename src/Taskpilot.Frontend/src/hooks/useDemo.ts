import { useEffect, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { authService } from '../services/authService'
import { demoLogin, fetchMe } from '../store/authSlice'
import { useAppDispatch } from '../store/hooks'

/**
 * Shared logic for the "Try the live demo" call-to-action: reports whether the server has the
 * no-signup demo turned on, and starts it (spin up a seeded throwaway account → sign in → dashboard).
 * Used by the auth pages and the marketing landing page so they stay in sync.
 */
export function useDemo() {
  const dispatch = useAppDispatch()
  const navigate = useNavigate()

  const [available, setAvailable] = useState(false)
  useEffect(() => {
    authService.demoEnabled().then(setAvailable).catch(() => setAvailable(false))
  }, [])

  const start = async () => {
    try {
      await dispatch(demoLogin()).unwrap()
      await dispatch(fetchMe())
      navigate('/')
    } catch {
      /* error surfaced via the auth store */
    }
  }

  return { available, start }
}
