import { createAsyncThunk, createSlice } from '@reduxjs/toolkit'
import { AxiosError } from 'axios'
import { authService } from '../services/authService'
import type {
  AuthResponse,
  LoginRequest,
  RegisterRequest,
  User,
} from '../types/auth'

/** Shape of the authentication slice in the Redux store. */
interface AuthState {
  user: User | null
  accessToken: string | null
  refreshToken: string | null
  isAuthenticated: boolean
  status: 'idle' | 'loading'
  error: string | null
}

// Initialise tokens from localStorage so a page refresh keeps the user logged in.
const initialState: AuthState = {
  user: null,
  accessToken: localStorage.getItem('accessToken'),
  refreshToken: localStorage.getItem('refreshToken'),
  isAuthenticated: !!localStorage.getItem('accessToken'),
  status: 'idle',
  error: null,
}

/** Shape of the error bodies the API returns. */
interface ApiErrorBody {
  error?: string
  errors?: { message?: string }[]
}

/** Turns an unknown thrown error (usually an Axios error) into a readable message. */
function toErrorMessage(error: unknown): string {
  if (error instanceof AxiosError) {
    // The API returns { error: "..." } or { errors: [{ message }] }.
    const data = error.response?.data as ApiErrorBody | undefined
    return data?.error ?? data?.errors?.[0]?.message ?? error.message
  }
  return 'Unexpected error'
}

// --- Async thunks (call the backend via authService) ---

export const login = createAsyncThunk(
  'auth/login',
  async (data: LoginRequest, { rejectWithValue }) => {
    try {
      return await authService.login(data)
    } catch (error) {
      return rejectWithValue(toErrorMessage(error))
    }
  },
)

export const register = createAsyncThunk(
  'auth/register',
  async (data: RegisterRequest, { rejectWithValue }) => {
    try {
      return await authService.register(data)
    } catch (error) {
      return rejectWithValue(toErrorMessage(error))
    }
  },
)

export const fetchMe = createAsyncThunk(
  'auth/me',
  async (_, { rejectWithValue }) => {
    try {
      return await authService.getMe()
    } catch (error) {
      return rejectWithValue(toErrorMessage(error))
    }
  },
)

const authSlice = createSlice({
  name: 'auth',
  initialState,
  reducers: {
    /** Clears auth state and removes persisted tokens (sign out). */
    logout(state) {
      state.user = null
      state.accessToken = null
      state.refreshToken = null
      state.isAuthenticated = false
      state.error = null
      localStorage.removeItem('accessToken')
      localStorage.removeItem('refreshToken')
    },
    /** Clears the last error (e.g. when the user edits the form). */
    clearError(state) {
      state.error = null
    },
  },
  extraReducers: (builder) => {
    builder
      // Login
      .addCase(login.pending, (state) => {
        state.status = 'loading'
        state.error = null
      })
      .addCase(login.fulfilled, (state, action) => {
        const data = action.payload as AuthResponse
        state.status = 'idle'
        // A 2FA-pending response carries no tokens — stay logged out until the
        // client resubmits with the code.
        if (data.requiresTwoFactor) return
        state.accessToken = data.accessToken
        state.refreshToken = data.refreshToken
        state.isAuthenticated = true
        // Persist tokens so the axios interceptor and refreshes can use them.
        localStorage.setItem('accessToken', data.accessToken)
        localStorage.setItem('refreshToken', data.refreshToken)
      })
      .addCase(login.rejected, (state, action) => {
        state.status = 'idle'
        state.error = (action.payload as string) ?? 'Login failed'
      })
      // Register
      .addCase(register.pending, (state) => {
        state.status = 'loading'
        state.error = null
      })
      .addCase(register.fulfilled, (state) => {
        state.status = 'idle'
      })
      .addCase(register.rejected, (state, action) => {
        state.status = 'idle'
        state.error = (action.payload as string) ?? 'Registration failed'
      })
      // Current user
      .addCase(fetchMe.fulfilled, (state, action) => {
        state.user = action.payload as User
        state.isAuthenticated = true
      })
      .addCase(fetchMe.rejected, (state) => {
        // Token is missing/expired — treat as signed out.
        state.user = null
        state.isAuthenticated = false
      })
  },
})

export const { logout, clearError } = authSlice.actions
export default authSlice.reducer
