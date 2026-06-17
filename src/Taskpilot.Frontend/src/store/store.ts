import { configureStore } from '@reduxjs/toolkit'
import authReducer from './authSlice'

/**
 * The single Redux store for the app. Feature slices are registered here.
 */
export const store = configureStore({
  reducer: {
    auth: authReducer,
  },
})

/** Type of the whole store state (for useAppSelector). */
export type RootState = ReturnType<typeof store.getState>

/** Type of the store's dispatch (knows about thunks; for useAppDispatch). */
export type AppDispatch = typeof store.dispatch
