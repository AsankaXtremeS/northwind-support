import { createContext, useContext } from 'react';
import type { AuthUser, LoginResponse } from '../../types/api';

export interface AuthState {
  /** The signed-in agent, or undefined when signed out. */
  user: AuthUser | undefined;
  /** True once a signed-in call has been refused (an expired token), until the next sign-in. */
  sessionExpired: boolean;
  signIn: (login: LoginResponse) => void;
  signOut: () => void;
}

export const AuthContext = createContext<AuthState | undefined>(undefined);

export function useAuth(): AuthState {
  const auth = useContext(AuthContext);

  if (!auth) {
    throw new Error('useAuth must be used inside <AuthProvider>.');
  }

  return auth;
}

/**
 * Where to go after signing in: the page the visitor was sent away from, or the ticket list.
 * Only a path inside this app is accepted, so the login page cannot be used to redirect
 * someone to another site.
 */
export function returnPathFrom(state: unknown): string {
  if (typeof state === 'object' && state !== null && 'from' in state) {
    const { from } = state;

    if (typeof from === 'string' && from.startsWith('/') && !from.startsWith('//') && !from.startsWith('/login')) {
      return from;
    }
  }

  return '/tickets';
}
