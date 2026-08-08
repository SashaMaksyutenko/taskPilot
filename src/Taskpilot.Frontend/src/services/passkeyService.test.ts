import { beforeEach, describe, expect, it, vi } from 'vitest'
import { passkeyService } from './passkeyService'

const { post, get, del } = vi.hoisted(() => ({ post: vi.fn(), get: vi.fn(), del: vi.fn() }))
vi.mock('../lib/api', () => ({ default: { post, get, delete: del } }))

// --- helpers to build/inspect the WebAuthn binary <-> base64url boundary ---
function bytesToBuffer(bytes: number[]): ArrayBuffer {
  return new Uint8Array(bytes).buffer
}
/** Mirrors the service's own encoder so tests assert the exact wire value. */
function base64url(bytes: number[]): string {
  let binary = ''
  for (const b of bytes) binary += String.fromCharCode(b)
  return btoa(binary).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '')
}

beforeEach(() => {
  vi.clearAllMocks()
  // jsdom has no WebAuthn — stub the pieces the service touches.
  ;(window as unknown as { PublicKeyCredential: unknown }).PublicKeyCredential = function () {}
  Object.defineProperty(navigator, 'credentials', {
    value: { create: vi.fn(), get: vi.fn() },
    configurable: true,
  })
})

describe('passkeyService.register', () => {
  it('decodes options for the authenticator and re-encodes the attestation as base64url', async () => {
    const challenge = [1, 2, 3, 4]
    const userId = [9, 8, 7]
    post.mockResolvedValueOnce({
      data: { challenge: base64url(challenge), user: { id: base64url(userId) }, pubKeyCredParams: [] },
    })
    post.mockResolvedValueOnce({ data: null }) // the register POST

    const rawId = [10, 20, 30]
    const attObj = [40, 50]
    const clientData = [60, 70]
    const create = navigator.credentials.create as ReturnType<typeof vi.fn>
    create.mockResolvedValue({
      id: 'cred-1',
      rawId: bytesToBuffer(rawId),
      type: 'public-key',
      getClientExtensionResults: () => ({}),
      response: { attestationObject: bytesToBuffer(attObj), clientDataJSON: bytesToBuffer(clientData) },
    })

    await passkeyService.register('MacBook')

    // The authenticator must receive real ArrayBuffers, not the base64url strings.
    const optionsPassed = create.mock.calls[0][0].publicKey
    expect(optionsPassed.challenge).toBeInstanceOf(ArrayBuffer)
    expect(new Uint8Array(optionsPassed.challenge)).toEqual(new Uint8Array(challenge))
    expect(new Uint8Array(optionsPassed.user.id)).toEqual(new Uint8Array(userId))

    // The attestation goes back base64url-encoded, with the name.
    const [url, body] = post.mock.calls[1]
    expect(url).toBe('/api/auth/passkeys/register')
    expect(body.name).toBe('MacBook')
    expect(body.attestationResponse.rawId).toBe(base64url(rawId))
    expect(body.attestationResponse.response.attestationObject).toBe(base64url(attObj))
    expect(body.attestationResponse.response.clientDataJSON).toBe(base64url(clientData))
  })
})

describe('passkeyService.login', () => {
  it('parses the options JSON, signs, and returns the auth tokens', async () => {
    const challenge = [5, 6, 7]
    const credId = [100, 101]
    post.mockResolvedValueOnce({
      data: {
        ceremonyId: 'ceremony-1',
        optionsJson: JSON.stringify({
          challenge: base64url(challenge),
          allowCredentials: [{ id: base64url(credId), type: 'public-key' }],
        }),
      },
    })
    const authResponse = { accessToken: 'a', refreshToken: 'r' }
    post.mockResolvedValueOnce({ data: authResponse })

    const sig = [11, 12]
    const get2 = navigator.credentials.get as ReturnType<typeof vi.fn>
    get2.mockResolvedValue({
      id: 'cred-1',
      rawId: bytesToBuffer(credId),
      type: 'public-key',
      getClientExtensionResults: () => ({}),
      response: {
        authenticatorData: bytesToBuffer([1]),
        clientDataJSON: bytesToBuffer([2]),
        signature: bytesToBuffer(sig),
        userHandle: null,
      },
    })

    const result = await passkeyService.login('me@example.com')

    expect(result).toEqual(authResponse)
    const optionsPassed = get2.mock.calls[0][0].publicKey
    expect(new Uint8Array(optionsPassed.challenge)).toEqual(new Uint8Array(challenge))
    expect(optionsPassed.allowCredentials[0].id).toBeInstanceOf(ArrayBuffer)

    const [url, body] = post.mock.calls[1]
    expect(url).toBe('/api/auth/passkey/login')
    expect(body.ceremonyId).toBe('ceremony-1')
    expect(body.assertionResponse.response.signature).toBe(base64url(sig))
    expect(body.assertionResponse.response.userHandle).toBeNull()
  })
})

describe('passkeyService.supported', () => {
  it('is true when the browser exposes PublicKeyCredential', () => {
    expect(passkeyService.supported()).toBe(true)
  })
})
