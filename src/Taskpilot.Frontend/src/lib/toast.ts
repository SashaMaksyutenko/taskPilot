import { toast } from 'react-toastify'

/**
 * Thin wrapper around react-toastify so components import one small helper
 * instead of the library directly. Keeps toast styling/behaviour in one place.
 */
export const notify = {
  success: (message: string) => toast.success(message),
  error: (message: string) => toast.error(message),
  info: (message: string) => toast.info(message),
}
