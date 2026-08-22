import { db, SERVER_URL } from '$lib/connections';

async function callWorktimeEndpoint(path: string, employeeId: string): Promise<unknown> {
  if (!db.authStore.isValid || !db.authStore.token) {
    throw new Error('Your login has expired. Please sign in again.');
  }

  const response = await fetch(`${SERVER_URL}${path}`, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      Authorization: db.authStore.token
    },
    body: JSON.stringify({ employee: employeeId })
  });

  const contentType = response.headers.get('content-type') ?? '';
  const body = contentType.includes('application/json')
    ? await response.json()
    : await response.text();

  if (!response.ok) {
    const message =
      typeof body === 'object' && body && 'message' in body
        ? String((body as { message: unknown }).message)
        : typeof body === 'string' && body
          ? body
          : `Server request failed with status ${response.status}.`;

    throw new Error(message);
  }

  return body;
}

export function startSession(employeeId: string): Promise<unknown> {
  return callWorktimeEndpoint('/api/worktime/start-session', employeeId);
}

export function endSession(employeeId: string): Promise<unknown> {
  return callWorktimeEndpoint('/api/worktime/end-session', employeeId);
}
