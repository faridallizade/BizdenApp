let csrfToken: Promise<string> | null = null

function getCsrfToken(refresh = false) {
  if (refresh) csrfToken = null
  csrfToken ??= fetch('/api/host/antiforgery', { credentials: 'include' }).then(async response => {
    if (!response.ok) throw new Error('Təhlükəsizlik tokeni alına bilmədi.')
    return (await response.json() as { token: string }).token
  })
  return csrfToken
}

export async function request<T>(path: string, init?: RequestInit, retryCsrf = true): Promise<T> {
  const method = init?.method?.toUpperCase() ?? 'GET'
  const csrf = path.startsWith('/api/host/') && !path.endsWith('/antiforgery') && !['GET', 'HEAD', 'OPTIONS'].includes(method) ? await getCsrfToken() : undefined
  const response = await fetch(path, { credentials: 'include', headers: { 'Content-Type': 'application/json', ...(csrf ? { 'X-CSRF-TOKEN': csrf } : {}), ...init?.headers }, ...init })
  const data = response.status === 204 ? null : await response.json()
  if (!response.ok) {
    if (csrf && retryCsrf && data?.code === 'INVALID_CSRF') {
      await getCsrfToken(true)
      return request<T>(path, init, false)
    }
    throw new Error(data?.message ?? 'Sorğu tamamlanmadı.')
  }
  return data as T
}
