import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { getSession, saveSession } from '../lib/session';
import { authApi } from './auth';
import { ApiError, request, setUnauthorizedHandler } from './client';

const fetchMock = vi.fn<typeof fetch>();

/** The Authorization header of the last request that reached fetch. */
function sentAuthorization(): string | undefined {
  const init = fetchMock.mock.calls.at(-1)?.[1];
  const headers = new Headers(init?.headers);

  return headers.get('Authorization') ?? undefined;
}

function respond(status: number, body: unknown = {}) {
  fetchMock.mockResolvedValue(new Response(JSON.stringify(body), { status }));
}

function signIn() {
  saveSession({
    token: 'sara-token',
    expiresAtUtc: new Date(Date.now() + 60 * 60 * 1000).toISOString(),
    user: { id: 4, fullName: 'Sara Lindqvist', email: 'sara.lindqvist@northwind-support.example' },
  });
}

describe('request', () => {
  beforeEach(() => {
    vi.stubGlobal('fetch', fetchMock);
  });

  afterEach(() => {
    vi.unstubAllGlobals();
    fetchMock.mockReset();
    sessionStorage.clear();
  });

  it('attaches the token to every call when signed in', async () => {
    signIn();
    respond(200, []);

    await request('/tickets');

    expect(sentAuthorization()).toBe('Bearer sara-token');
  });

  it('sends no token when signed out', async () => {
    respond(200, []);

    await request('/tickets');

    expect(sentAuthorization()).toBeUndefined();
  });

  it('clears the session and calls the handler when a signed-in call gets a 401', async () => {
    signIn();
    respond(401, { title: 'Unauthorized', detail: 'Sign in to continue.' });
    const onUnauthorized = vi.fn();
    const remove = setUnauthorizedHandler(onUnauthorized);

    await expect(request('/tickets')).rejects.toBeInstanceOf(ApiError);

    remove();
    expect(getSession()).toBeUndefined();
    expect(onUnauthorized).toHaveBeenCalledTimes(1);
  });

  it('keeps the session for errors other than 401', async () => {
    signIn();
    respond(409, { title: 'Conflict', detail: 'Ticket TCK-0010 is already Critical.' });
    const onUnauthorized = vi.fn();
    const remove = setUnauthorizedHandler(onUnauthorized);

    await expect(request('/tickets/10/escalate', { method: 'POST' })).rejects.toThrow('already Critical');

    remove();
    expect(getSession()).toBeDefined();
    expect(onUnauthorized).not.toHaveBeenCalled();
  });

  it('treats a 401 from login as wrong credentials: an error, no redirect, no token sent', async () => {
    signIn();
    respond(401, { title: 'Unauthorized', detail: 'Email or password is incorrect.' });
    const onUnauthorized = vi.fn();
    const remove = setUnauthorizedHandler(onUnauthorized);

    await expect(
      authApi.login({ email: 'sara.lindqvist@northwind-support.example', password: 'wrong' }),
    ).rejects.toThrow('Email or password is incorrect.');

    remove();
    expect(onUnauthorized).not.toHaveBeenCalled();
    expect(sentAuthorization()).toBeUndefined();
  });
});
