import api from '../lib/api'
import type { AuthResponse } from '../types/auth'

/** A registered passkey, for listing in settings. */
export interface Passkey {
  id: string
  name: string
  createdAt: string
  lastUsedAt: string | null
}

// --- base64url <-> ArrayBuffer (WebAuthn transports binary as base64url) ---
function bufferToBase64url(buffer: ArrayBuffer): string {
  const bytes = new Uint8Array(buffer)
  let binary = ''
  for (const b of bytes) binary += String.fromCharCode(b)
  return btoa(binary).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '')
}

function base64urlToBuffer(base64url: string): ArrayBuffer {
  const padded = base64url + '='.repeat((4 - (base64url.length % 4)) % 4)
  const binary = atob(padded.replace(/-/g, '+').replace(/_/g, '/'))
  const bytes = new Uint8Array(binary.length)
  for (let i = 0; i < binary.length; i++) bytes[i] = binary.charCodeAt(i)
  return bytes.buffer
}

/** WebAuthn / FIDO2 passkeys: register, list, remove and passwordless sign-in. */
export const passkeyService = {
  /** Whether this browser supports WebAuthn. */
  supported(): boolean {
    return typeof window !== 'undefined' && !!window.PublicKeyCredential
  },

  /** Registers a new passkey with the authenticator and stores it server-side. */
  async register(name: string): Promise<void> {
    // 1. Server issues creation options (WebAuthn JSON; binary fields are base64url).
    const options = (await api.post('/api/auth/passkeys/register/options')).data
    options.challenge = base64urlToBuffer(options.challenge)
    options.user.id = base64urlToBuffer(options.user.id)
    if (Array.isArray(options.excludeCredentials)) {
      options.excludeCredentials = options.excludeCredentials.map((c: { id: string }) => ({
        ...c,
        id: base64urlToBuffer(c.id),
      }))
    }

    // 2. The authenticator creates the credential (prompts biometrics/PIN).
    const credential = (await navigator.credentials.create({ publicKey: options })) as PublicKeyCredential
    const response = credential.response as AuthenticatorAttestationResponse

    // 3. Send the attestation back, base64url-encoded, in Fido2's raw-response shape.
    const attestationResponse = {
      id: credential.id,
      rawId: bufferToBase64url(credential.rawId),
      type: credential.type,
      extensions: credential.getClientExtensionResults(),
      response: {
        attestationObject: bufferToBase64url(response.attestationObject),
        clientDataJSON: bufferToBase64url(response.clientDataJSON),
      },
    }
    await api.post('/api/auth/passkeys/register', { attestationResponse, name })
  },

  list(): Promise<Passkey[]> {
    return api.get<Passkey[]>('/api/auth/passkeys').then((r) => r.data)
  },

  remove(id: string): Promise<void> {
    return api.delete(`/api/auth/passkeys/${id}`).then(() => undefined)
  },

  /** Signs in with a passkey for the given account, returning the usual auth tokens. */
  async login(email: string): Promise<AuthResponse> {
    // 1. Server issues assertion options + a ceremony id (options come as a JSON string).
    const { ceremonyId, optionsJson } = (await api.post('/api/auth/passkey/login/options', { email })).data
    const options = JSON.parse(optionsJson)
    options.challenge = base64urlToBuffer(options.challenge)
    if (Array.isArray(options.allowCredentials)) {
      options.allowCredentials = options.allowCredentials.map((c: { id: string }) => ({
        ...c,
        id: base64urlToBuffer(c.id),
      }))
    }

    // 2. The authenticator signs the challenge.
    const credential = (await navigator.credentials.get({ publicKey: options })) as PublicKeyCredential
    const response = credential.response as AuthenticatorAssertionResponse

    // 3. Send the assertion back for verification; the server returns auth tokens.
    const assertionResponse = {
      id: credential.id,
      rawId: bufferToBase64url(credential.rawId),
      type: credential.type,
      extensions: credential.getClientExtensionResults(),
      response: {
        authenticatorData: bufferToBase64url(response.authenticatorData),
        clientDataJSON: bufferToBase64url(response.clientDataJSON),
        signature: bufferToBase64url(response.signature),
        userHandle: response.userHandle ? bufferToBase64url(response.userHandle) : null,
      },
    }
    return (await api.post<AuthResponse>('/api/auth/passkey/login', { ceremonyId, assertionResponse })).data
  },
}
