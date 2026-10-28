import { createBrowserRouter, Navigate, Outlet } from 'react-router-dom';
import { App } from './App';
import { NotFoundPage } from './features/NotFoundPage';
import { AuthProvider } from './features/auth/AuthProvider';
import { LoginPage } from './features/auth/LoginPage';
import { RequireAuth } from './features/auth/RequireAuth';
import { CustomerDetailPage } from './features/customers/CustomerDetailPage';
import { CustomerListPage } from './features/customers/CustomerListPage';
import { CreateTicketPage } from './features/tickets/CreateTicketPage';
import { TicketDetailPage } from './features/tickets/TicketDetailPage';
import { TicketListPage } from './features/tickets/TicketListPage';

/**
 * /login is the only page open to everyone. Everything else sits under RequireAuth, so a new
 * page is protected by being added here, without anything to remember.
 */
export const router = createBrowserRouter([
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
              { index: true, element: <Navigate to="/tickets" replace /> },
              { path: 'tickets', element: <TicketListPage /> },
              { path: 'tickets/new', element: <CreateTicketPage /> },
              { path: 'tickets/:id', element: <TicketDetailPage /> },
              { path: 'customers', element: <CustomerListPage /> },
              { path: 'customers/:id', element: <CustomerDetailPage /> },
              { path: '*', element: <NotFoundPage /> },
            ],
          },
        ],
      },
    ],
  },
]);
