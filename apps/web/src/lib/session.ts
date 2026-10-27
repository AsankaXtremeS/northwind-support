import type { LoginResponse } from '../types/api';

/** What the browser keeps between page loads: the token, when it expires, and who it is for. */
export type Session = LoginResponse;

type SessionListener = (session: Session | undefined) => void;

/**
 * The session is kept in sessionStorage. It survives a page refresh, is not shared with other
 * tabs, and is gone when the tab is closed. The trade-off: like anything script can read, an
 * XSS bug could steal it. An httpOnly cookie would not be readable by script, but needs cookie
 * auth and CSRF protection on the API, which is more than this app needs today.
 */
const storageKey = 'supportdesk.session';

const listeners = new Set<SessionListener>();

/** The current session, or undefined when signed out or the token has expired. */
export function getSession(now: number = Date.now()): Session | undefined {
  const session = read();

  if (session && Date.parse(session.expiresAtUtc) <= now) {
    // Expired: forget it quietly. The next request would be refused anyway.
    remove();
    return undefined;
  }

  return session;
}

export function saveSession(session: Session): void {
  try {
    sessionStorage.setItem(storageKey, JSON.stringify(session));
  } catch {
    // Storage blocked or full: the session still works until the page is reloaded.
  }

  notify(session);
}

export function clearSession(): void {
  remove();
  notify(undefined);
}

/** Tells the listener whenever the session is saved or cleared. Returns an unsubscribe. */
export function onSessionChange(listener: SessionListener): () => void {
  listeners.add(listener);

  return () => {
    listeners.delete(listener);
  };
}

function notify(session: Session | undefined) {
  listeners.forEach((listener) => listener(session));
}

function read(): Session | undefined {
  try {
    const raw = sessionStorage.getItem(storageKey);
    if (raw === null) {
      return undefined;
    }

    const parsed: unknown = JSON.parse(raw);

    return isSession(parsed) ? parsed : undefined;
  } catch {
    // Unreadable storage or a corrupt value: treat as signed out.
    return undefined;
  }
}

function remove() {
  try {
    sessionStorage.removeItem(storageKey);
  } catch {
    // Nothing to remove if storage is unavailable.
  }
}

/** Only trust a stored value that has the shape we wrote. */
function isSession(value: unknown): value is Session {
  if (typeof value !== 'object' || value === null) {
    return false;
  }

  const { token, expiresAtUtc, user } = value as Record<string, unknown>;

  if (typeof token !== 'string' || typeof expiresAtUtc !== 'string' || Number.isNaN(Date.parse(expiresAtUtc))) {
    return false;
  }

  if (typeof user !== 'object' || user === null) {
    return false;
  }

  const { id, fullName, email } = user as Record<string, unknown>;

  return typeof id === 'number' && typeof fullName === 'string' && typeof email === 'string';
}
