import { clearSession, getSession } from '../lib/session';
import type { ProblemDetails } from '../types/api';

/**
 * Where the API lives. In development this stays '/api' and the Vite dev server proxies it
 * to the backend; set VITE_API_BASE_URL to point a build somewhere else. See .env.example.
 */
const baseUrl = import.meta.env.VITE_API_BASE_URL ?? '/api';

/** A failed HTTP call, carrying the problem document the API returned. */
export class ApiError extends Error {
  readonly status: number;

  readonly problem?: ProblemDetails;

  constructor(status: number, message: string, problem?: ProblemDetails) {
    super(message);
    this.name = 'ApiError';
    this.status = status;
    this.problem = problem;
  }

  /** Field name to messages, when the failure was a validation failure. */
  get fieldErrors(): Record<string, string[]> {
    return this.problem?.errors ?? {};
  }
}

interface RequestOptions {
  /**
   * Sends no token, and a 401 is just an error. For signing in, where a 401 means wrong
   * credentials rather than an expired session.
   */
  anonymous?: boolean;
}

let unauthorizedHandler: (() => void) | undefined;

/**
 * Runs whenever a signed-in request comes back 401 (an expired or revoked token), after the
 * session has been cleared. The app uses it to send the user to the login page. Returns a
 * function that removes the handler.
 */
export function setUnauthorizedHandler(handler: () => void): () => void {
  unauthorizedHandler = handler;

  return () => {
    if (unauthorizedHandler === handler) {
      unauthorizedHandler = undefined;
    }
  };
}

/**
 * The single place that talks to the API. Attaches the signed-in agent's token, returns
 * parsed JSON, or throws an {@link ApiError} that callers can show to the user.
 */
export async function request<T>(path: string, init?: RequestInit, options: RequestOptions = {}): Promise<T> {
  const token = options.anonymous ? undefined : getSession()?.token;

  const response = await fetch(`${baseUrl}${path}`, {
    ...init,
    headers: {
      'Content-Type': 'application/json',
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
      ...(init?.headers ?? {}),
    },
  });

  if (!response.ok) {
    const problem = await readProblem(response);

    if (response.status === 401 && !options.anonymous) {
      clearSession();
      unauthorizedHandler?.();
    }

    throw new ApiError(
      response.status,
      problem?.detail ?? problem?.title ?? response.statusText,
      problem,
    );
  }

  if (response.status === 204) {
    return undefined as T;
  }

  return (await response.json()) as T;
}

/** Turns anything thrown by a fetch into a message worth showing a user. */
export function toErrorMessage(error: unknown, fallback: string): string {
  if (error instanceof ApiError) {
    return error.message;
  }

  if (error instanceof Error) {
    return `${fallback} (${error.message})`;
  }

  return fallback;
}

async function readProblem(response: Response): Promise<ProblemDetails | undefined> {
  try {
    return (await response.json()) as ProblemDetails;
  } catch {
    // Not every failure has a JSON body - a dead server certainly will not.
    return undefined;
  }
}
