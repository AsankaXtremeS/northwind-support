import { afterEach, describe, expect, it, vi } from 'vitest';
import { clearSession, getSession, onSessionChange, saveSession, type Session } from './session';

const now = Date.parse('2026-10-27T12:00:00Z');

function session(expiresAtUtc = '2026-10-27T20:00:00Z'): Session {
  return {
    token: 'header.payload.signature',
    expiresAtUtc,
    user: { id: 4, fullName: 'Sara Lindqvist', email: 'sara.lindqvist@northwind-support.example' },
  };
}

describe('session', () => {
  afterEach(() => {
    sessionStorage.clear();
  });

  it('keeps a saved session in sessionStorage, so it survives a page reload', () => {
    saveSession(session());

    expect(getSession(now)).toEqual(session());
    expect(sessionStorage.getItem('supportdesk.session')).not.toBeNull();
  });

  it('is signed out when nothing is stored', () => {
    expect(getSession(now)).toBeUndefined();
  });

  it('forgets an expired session', () => {
    saveSession(session('2026-10-27T11:59:59Z'));

    expect(getSession(now)).toBeUndefined();
    expect(sessionStorage.getItem('supportdesk.session')).toBeNull();
  });

  it('treats a corrupt or wrongly shaped value as signed out', () => {
    sessionStorage.setItem('supportdesk.session', '{not json');
    expect(getSession(now)).toBeUndefined();

    sessionStorage.setItem('supportdesk.session', JSON.stringify({ token: 'x', user: null }));
    expect(getSession(now)).toBeUndefined();
  });

  it('clears the session and tells listeners', () => {
    const listener = vi.fn();
    const unsubscribe = onSessionChange(listener);
    saveSession(session());

    clearSession();
    unsubscribe();
    saveSession(session());

    expect(getSession(now)).toEqual(session());
    expect(listener.mock.calls).toEqual([[session()], [undefined]]);
  });
});
