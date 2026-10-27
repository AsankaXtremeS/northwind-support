import { Link, NavLink, Outlet, useNavigate } from 'react-router-dom';
import { useAuth } from './features/auth/authContext';

/** Application shell: the header, the navigation, and wherever the router puts the page. */
export function App() {
  const { user, signOut } = useAuth();
  const navigate = useNavigate();

  const logOut = () => {
    signOut();
    void navigate('/login', { replace: true });
  };

  return (
    <div className="app">
      <header className="app__header">
        <Link className="app__brand" to="/tickets">
          Northwind Support
        </Link>

        <nav className="app__nav" aria-label="Main">
          <NavLink to="/tickets">Tickets</NavLink>
          <NavLink to="/customers">Customers</NavLink>
        </nav>

        {user && (
          <div className="app__user">
            <span>
              Signed in as <strong>{user.fullName}</strong>
            </span>
            <button type="button" className="button button--ghost" onClick={logOut}>
              Log out
            </button>
          </div>
        )}
      </header>

      <main className="app__main">
        <Outlet />
      </main>
    </div>
  );
}
