import { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { ErrorMessage } from '../../components/ErrorMessage';
import { Spinner } from '../../components/Spinner';
import { useReferenceData } from '../../hooks/useReferenceData';
import type { RaisedTicket } from '../../types/api';
import { CreateTicketForm } from './components/CreateTicketForm';
import { TriageResult } from './components/TriageResult';

export function CreateTicketPage() {
  const reference = useReferenceData();
  const navigate = useNavigate();
  const [raised, setRaised] = useState<RaisedTicket>();

  return (
    <section>
      <header className="page-header">
        <div>
          <p className="breadcrumb">
            <Link to="/tickets">Tickets</Link> / New
          </p>
          <h1>Raise a ticket</h1>
        </div>
      </header>

      {reference.isLoading && <Spinner label="Loading customers and categories" />}

      {reference.error && (
        <ErrorMessage message={reference.error} onRetry={() => void reference.reload()} />
      )}

      {raised && (
        <div className="card notice" role="status">
          <p>
            Raised <strong>{raised.ticket.reference}</strong> for {raised.ticket.customer.name}.
          </p>
          <TriageResult raised={raised} />
          <div className="button-row">
            <button
              type="button"
              className="button button--primary"
              onClick={() => void navigate(`/tickets/${raised.ticket.id}`)}
            >
              Open {raised.ticket.reference}
            </button>
            <button type="button" className="button button--ghost" onClick={() => setRaised(undefined)}>
              Raise another
            </button>
          </div>
        </div>
      )}

      {reference.data && !raised && (
        <CreateTicketForm
          customers={reference.data.customers}
          categories={reference.data.categories}
          onCreated={setRaised}
        />
      )}
    </section>
  );
}
