import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { useState } from 'react';
import { createMemoryRouter, Outlet, RouterProvider } from 'react-router-dom';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { request, toErrorMessage } from '../../api/client';
import { App } from '../../App';
import { getSession, saveSession } from '../../lib/session';
import type { LoginResponse } from '../../types/api';
import { AuthProvider } from './AuthProvider';
import { LoginPage } from './LoginPage';
import { RequireAuth } from './RequireAuth';

const sara: LoginResponse = {
  token: 'sara-token',
  expiresAtUtc: new Date(Date.now() + 8 * 60 * 60 * 1000).toISOString(),
  user: { id: 4, fullName: 'Sara Lindqvist', email: 'sara.lindqvist@northwind-support.example' },
};

const fetchMock = vi.fn<typeof fetch>();

/** A protected page that loads something, to see what a 401 does to the app. */
function TicketPage() {
  const [message, setMessage] = useState('Ticket TCK-0012');

  const load = async () => {
    try {
      await request('/tickets/12');
    } catch (caught) {
      setMessage(toErrorMessage(caught, 'failed'));
    }
  };

  return (
    <>
      <p>{message}</p>
      <button type="button" onClick={() => void load()}>
        Reload ticket
      </button>
    </>
  );
}

/** The same shape as the real route tree, with stand-in pages. */
function renderApp(startAt: string) {
  const router = createMemoryRouter(
    [
      {
        element: (
          <AuthProvider>
            <Outlet />
          </AuthProvider>
        ),
        children: [
          { path: '/login', element: <LoginPage /> },
          {
            element: <RequireAuth />,
            children: [
              {
                path: '/',
                element: <App />,
                children: [
                  { path: 'tickets', element: <p>Ticket list</p> },
                  { path: 'tickets/:id', element: <TicketPage /> },
                ],
              },
            ],
          },
        ],
      },
    ],
    { initialEntries: [startAt] },
  );

  render(<RouterProvider router={router} />);

  return router;
}

function respondToLogin(status: number, body: unknown) {
  fetchMock.mockImplementation(async (input) =>
    String(input).endsWith('/auth/login')
      ? new Response(JSON.stringify(body), { status })
      : new Response(JSON.stringify({ title: 'Unauthorized', detail: 'Sign in to continue.' }), { status: 401 }),
  );
}

async function fillIn(email: string, password: string) {
  if (email) {
    await userEvent.type(screen.getByLabelText('Email'), email);
  }

  if (password) {
    await userEvent.type(screen.getByLabelText('Password'), password);
  }

  await userEvent.click(screen.getByRole('button', { name: 'Sign in' }));
}

describe('signing in', () => {
  beforeEach(() => {
    vi.stubGlobal('fetch', fetchMock);
  });

  afterEach(() => {
    vi.unstubAllGlobals();
    fetchMock.mockReset();
    sessionStorage.clear();
  });

  it('sends an unauthenticated visitor to /login', () => {
    const router = renderApp('/tickets/12');

    expect(router.state.location.pathname).toBe('/login');
    expect(screen.getByRole('heading', { name: 'Sign in' })).toBeInTheDocument();
    expect(screen.queryByText('Ticket TCK-0012')).not.toBeInTheDocument();
  });

  it('after signing in, lands on the page the visitor was trying to reach', async () => {
    respondToLogin(200, sara);
    const router = renderApp('/tickets/12');

    await fillIn('sara.lindqvist@northwind-support.example', 'web-tests-only-password');

    expect(await screen.findByText('Ticket TCK-0012')).toBeInTheDocument();
    expect(router.state.location.pathname).toBe('/tickets/12');
    expect(getSession()?.token).toBe('sara-token');
  });

  it('goes to the ticket list when the visitor came straight to /login', async () => {
    respondToLogin(200, sara);
    const router = renderApp('/login');

    await fillIn('sara.lindqvist@northwind-support.example', 'web-tests-only-password');

    expect(await screen.findByText('Ticket list')).toBeInTheDocument();
    expect(router.state.location.pathname).toBe('/tickets');
  });

  it('checks the fields before calling the API', async () => {
    renderApp('/login');

    await fillIn('not-an-email', '');

    expect(screen.getByText('Enter an email address like name@example.com.')).toBeInTheDocument();
    expect(screen.getByText('Enter your password.')).toBeInTheDocument();
    expect(screen.getByLabelText('Email')).toHaveAttribute('aria-invalid', 'true');
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it('shows the rejection, stays on /login and keeps no session when the credentials are wrong', async () => {
    respondToLogin(401, { title: 'Unauthorized', detail: 'Email or password is incorrect.' });
    const router = renderApp('/login');

    await fillIn('sara.lindqvist@northwind-support.example', 'wrong-password');

    expect(await screen.findByRole('alert')).toHaveTextContent('Email or password is incorrect.');
    expect(router.state.location.pathname).toBe('/login');
    expect(getSession()).toBeUndefined();
    expect(screen.getByLabelText('Password')).toHaveValue('');
    expect(screen.getByRole('button', { name: 'Sign in' })).toBeEnabled();
  });

  it('shows a pending state while signing in', async () => {
    let finish: (response: Response) => void = () => {};
    fetchMock.mockImplementation(() => new Promise<Response>((resolve) => (finish = resolve)));
    renderApp('/login');

    await fillIn('sara.lindqvist@northwind-support.example', 'web-tests-only-password');

    expect(screen.getByRole('button', { name: 'Signing in...' })).toBeDisabled();
    finish(new Response(JSON.stringify(sara), { status: 200 }));
    expect(await screen.findByText('Ticket list')).toBeInTheDocument();
  });
});

describe('when signed in', () => {
  beforeEach(() => {
    vi.stubGlobal('fetch', fetchMock);
    saveSession(sara);
  });

  afterEach(() => {
    vi.unstubAllGlobals();
    fetchMock.mockReset();
    sessionStorage.clear();
  });

  it("shows the agent's name, and logging out clears the session and returns to /login", async () => {
    const router = renderApp('/tickets');

    expect(screen.getByText('Sara Lindqvist')).toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: 'Log out' }));

    expect(getSession()).toBeUndefined();
    expect(router.state.location.pathname).toBe('/login');
    expect(screen.getByRole('heading', { name: 'Sign in' })).toBeInTheDocument();
  });

  it('a 401 from any call (an expired token) clears the session and sends the user to /login', async () => {
    fetchMock.mockResolvedValue(
      new Response(JSON.stringify({ title: 'Unauthorized', detail: 'Sign in to continue.' }), { status: 401 }),
    );
    const router = renderApp('/tickets/12');

    await userEvent.click(screen.getByRole('button', { name: 'Reload ticket' }));

    expect(await screen.findByText('Your session has ended. Sign in again to carry on.')).toBeInTheDocument();
    expect(router.state.location.pathname).toBe('/login');
    expect(getSession()).toBeUndefined();
  });

  it('after a 401, signing in again returns to the page the user was on', async () => {
    fetchMock.mockResolvedValue(
      new Response(JSON.stringify({ title: 'Unauthorized', detail: 'Sign in to continue.' }), { status: 401 }),
    );
    const router = renderApp('/tickets/12');
    await userEvent.click(screen.getByRole('button', { name: 'Reload ticket' }));
    await screen.findByRole('heading', { name: 'Sign in' });

    respondToLogin(200, sara);
    await fillIn('sara.lindqvist@northwind-support.example', 'web-tests-only-password');

    expect(await screen.findByText('Ticket TCK-0012')).toBeInTheDocument();
    expect(router.state.location.pathname).toBe('/tickets/12');
  });
});
