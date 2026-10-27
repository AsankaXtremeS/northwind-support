import type { AuthUser, LoginPayload, LoginResponse } from '../types/api';
import { request } from './client';

export const authApi = {
  /** Anonymous: a 401 here means wrong credentials, not an expired session. */
  login: (payload: LoginPayload) =>
    request<LoginResponse>(
      '/auth/login',
      { method: 'POST', body: JSON.stringify(payload) },
      { anonymous: true },
    ),

  me: () => request<AuthUser>('/auth/me'),
};
