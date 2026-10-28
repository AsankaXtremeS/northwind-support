import { useEffect, useMemo, useState } from 'react';
import { setUnauthorizedHandler } from '../../api/client';
import { clearSession, getSession, onSessionChange, saveSession } from '../../lib/session';
import type { AuthUser, LoginResponse } from '../../types/api';
import { AuthContext, type AuthState } from './authContext';

/**
 * Holds who is signed in, kept in step with the stored session. When any call comes back 401,
 * the API client clears the session; this notices, and the route guard sends the user to the
 * login page, which then says their session has ended.
 */
export function AuthProvider({ children }: { children: React.ReactNode }) {
  const [user, setUser] = useState<AuthUser | undefined>(() => getSession()?.user);
  const [sessionExpired, setSessionExpired] = useState(false);

  useEffect(() => onSessionChange((session) => setUser(session?.user)), []);

  useEffect(() => setUnauthorizedHandler(() => setSessionExpired(true)), []);

  const value = useMemo<AuthState>(
    () => ({
      user,
      sessionExpired,
      signIn: (login: LoginResponse) => {
        setSessionExpired(false);
        saveSession(login);
      },
      signOut: () => {
        setSessionExpired(false);
        clearSession();
      },
    }),
    [user, sessionExpired],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}
