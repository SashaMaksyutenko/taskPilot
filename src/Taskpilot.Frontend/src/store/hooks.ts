import { useDispatch, useSelector } from 'react-redux'
import type { TypedUseSelectorHook } from 'react-redux'
import type { AppDispatch, RootState } from './store'

// Pre-typed versions of the Redux hooks. Use these throughout the app instead of
// the plain useDispatch/useSelector so dispatch and state are correctly typed.

/** Typed dispatch that understands async thunks. */
export const useAppDispatch = () => useDispatch<AppDispatch>()

/** Typed selector for reading from the store. */
export const useAppSelector: TypedUseSelectorHook<RootState> = useSelector
