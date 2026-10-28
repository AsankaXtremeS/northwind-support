import { useState } from 'react';
import { ApiError, toErrorMessage } from '../../../api/client';
import { ticketsApi } from '../../../api/tickets';
import { Field } from '../../../components/Field';
import { humanize } from '../../../lib/format';
import type { EscalatedTicket, TicketDetail, TicketPriority } from '../../../types/api';

/** The same limits the API checks, on the trimmed text. */
const reasonMinLength = 5;
const reasonMaxLength = 500;

const nextPriority: Record<TicketPriority, TicketPriority | undefined> = {
  Low: 'Medium',
  Medium: 'High',
  High: 'Critical',
  Critical: undefined,
};

type FormErrors = { reason?: string };

interface EscalationPanelProps {
  ticket: TicketDetail;
  onEscalated: (escalated: EscalatedTicket) => void;
}

/**
 * Raises the ticket one priority level, with a reason. Says why when it cannot. The history
 * records the signed-in agent, so there is no name to type.
 */
export function EscalationPanel({ ticket, onEscalated }: EscalationPanelProps) {
  const [reason, setReason] = useState('');
  const [errors, setErrors] = useState<FormErrors>({});
  const [isSaving, setIsSaving] = useState(false);
  const [submitError, setSubmitError] = useState<string>();

  const target = nextPriority[ticket.priority];

  if (!ticket.canEscalate || target === undefined) {
    return (
      <div className="card">
        <h2>Escalation</h2>
        <p className="field__hint">{whyNot(ticket)}</p>
      </div>
    );
  }

  const submit = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();

    const validationErrors = validate(reason);
    setErrors(validationErrors);
    setSubmitError(undefined);

    if (Object.keys(validationErrors).length > 0) {
      return;
    }

    setIsSaving(true);

    try {
      const escalated = await ticketsApi.escalate(ticket.id, { reason: reason.trim() });
      setReason('');
      onEscalated(escalated);
    } catch (caught) {
      if (caught instanceof ApiError) {
        setErrors(fromApiErrors(caught.fieldErrors));
      }

      setSubmitError(toErrorMessage(caught, 'Could not escalate the ticket.'));
    } finally {
      setIsSaving(false);
    }
  };

  return (
    <form className="card form" onSubmit={submit} noValidate>
      <h2>Escalation</h2>
      <p className="field__hint">
        Raises the priority from {ticket.priority} to {target}, restarts the SLA from now and
        re-checks who owns the ticket.
      </p>

      <Field
        id="escalation-reason"
        label="Reason"
        error={errors.reason}
        hint={`${reasonMinLength} to ${reasonMaxLength} characters.`}
      >
        {(fieldProps) => (
          <textarea
            {...fieldProps}
            rows={3}
            value={reason}
            maxLength={reasonMaxLength}
            onChange={(event) => setReason(event.target.value)}
          />
        )}
      </Field>

      {submitError && (
        <p className="field__error" role="alert">
          {submitError}
        </p>
      )}

      <div className="button-row">
        <button type="submit" className="button button--primary" disabled={isSaving}>
          {isSaving ? 'Escalating...' : `Escalate to ${target}`}
        </button>
      </div>
    </form>
  );
}

function whyNot(ticket: TicketDetail): string {
  if (ticket.status === 'Resolved' || ticket.status === 'Closed') {
    return `This ticket is ${humanize(ticket.status).toLowerCase()}, so it cannot be escalated.`;
  }

  return 'This ticket is already Critical, the highest priority.';
}

function validate(reason: string): FormErrors {
  const reasonLength = reason.trim().length;

  return reasonLength < reasonMinLength || reasonLength > reasonMaxLength
    ? { reason: `Give a reason of ${reasonMinLength} to ${reasonMaxLength} characters.` }
    : {};
}

/** The API names the field "Reason"; the form shows its first message. */
function fromApiErrors(fieldErrors: Record<string, string[]>): FormErrors {
  return { reason: fieldErrors.Reason?.[0] };
}
