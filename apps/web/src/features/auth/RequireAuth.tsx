import { Navigate, Outlet, useLocation } from 'react-router-dom';
import { useAuth } from './authContext';

/**
 * Guards every page below it. A visitor who is not signed in, or whose session has just been
 * cleared by a 401, goes to /login, remembering where they were so they can come back.
 */
export function RequireAuth() {
  const { user } = useAuth();
  const location = useLocation();

  if (!user) {
    const from = `${location.pathname}${location.search}${location.hash}`;

    return <Navigate to="/login" replace state={{ from }} />;
  }

  return <Outlet />;
}
