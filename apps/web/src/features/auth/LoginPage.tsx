import { useState } from 'react';
import { Navigate, useLocation } from 'react-router-dom';
import { authApi } from '../../api/auth';
import { toErrorMessage } from '../../api/client';
import { Field } from '../../components/Field';
import { returnPathFrom, useAuth } from './authContext';

interface FormState {
  email: string;
  password: string;
}

type FormErrors = Partial<Record<keyof FormState, string>>;

const emailPattern = /^\S+@\S+\.\S+$/;

/** Sign-in for agents. On success, goes back to the page the visitor wanted, or the ticket list. */
export function LoginPage() {
  const { user, sessionExpired, signIn } = useAuth();
  const location = useLocation();

  const [form, setForm] = useState<FormState>({ email: '', password: '' });
  const [errors, setErrors] = useState<FormErrors>({});
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [submitError, setSubmitError] = useState<string>();

  // Signed in, either already or just now: on to where they were going.
  if (user) {
    return <Navigate to={returnPathFrom(location.state)} replace />;
  }

  const update = (patch: Partial<FormState>) => setForm((current) => ({ ...current, ...patch }));

  const submit = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();

    const validationErrors = validate(form);
    setErrors(validationErrors);
    setSubmitError(undefined);

    if (Object.keys(validationErrors).length > 0) {
      return;
    }

    setIsSubmitting(true);

    try {
      signIn(await authApi.login({ email: form.email.trim(), password: form.password }));
    } catch (caught) {
      // The API gives one message for every rejection, and so does this page.
      setSubmitError(toErrorMessage(caught, 'Could not sign in.'));
      update({ password: '' });
      setIsSubmitting(false);
    }
  };

  return (
    <main className="app__main login">
      <form className="card form" onSubmit={submit} noValidate aria-labelledby="login-heading">
        <h1 id="login-heading">Sign in</h1>
        <p className="field__hint">Northwind Support agents only.</p>

        {sessionExpired && (
          <p className="notice login__notice" role="status">
            Your session has ended. Sign in again to carry on.
          </p>
        )}

        <Field id="login-email" label="Email" error={errors.email}>
          {(fieldProps) => (
            <input
              {...fieldProps}
              type="email"
              autoComplete="username"
              value={form.email}
              onChange={(event) => update({ email: event.target.value })}
            />
          )}
        </Field>

        <Field id="login-password" label="Password" error={errors.password}>
          {(fieldProps) => (
            <input
              {...fieldProps}
              type="password"
              autoComplete="current-password"
              value={form.password}
              onChange={(event) => update({ password: event.target.value })}
            />
          )}
        </Field>

        {submitError && (
          <p className="field__error" role="alert">
            {submitError}
          </p>
        )}

        <div className="button-row">
          <button type="submit" className="button button--primary" disabled={isSubmitting}>
            {isSubmitting ? 'Signing in...' : 'Sign in'}
          </button>
        </div>
      </form>
    </main>
  );
}

function validate(form: FormState): FormErrors {
  const errors: FormErrors = {};

  if (form.email.trim() === '') {
    errors.email = 'Enter your email address.';
  } else if (!emailPattern.test(form.email.trim())) {
    errors.email = 'Enter an email address like name@example.com.';
  }

  if (form.password === '') {
    errors.password = 'Enter your password.';
  }

  return errors;
}
