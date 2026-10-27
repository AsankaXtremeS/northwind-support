import { ErrorMessage } from '../../../components/ErrorMessage';
import { Spinner } from '../../../components/Spinner';
import { formatDateTime } from '../../../lib/format';
import type { AgentSummary, TicketEscalation } from '../../../types/api';

interface EscalationHistoryProps {
  escalations: TicketEscalation[] | undefined;
  isLoading: boolean;
  error: string | undefined;
  onRetry: () => void;
}

/** Every escalation of the ticket, newest first, as the API returns them. */
export function EscalationHistory({ escalations, isLoading, error, onRetry }: EscalationHistoryProps) {
  return (
    <div className="card">
      <h2>Escalation history</h2>

      {error && <ErrorMessage message={error} onRetry={onRetry} />}

      {isLoading && !escalations && <Spinner label="Loading escalation history" />}

      {escalations?.length === 0 && (
        <p className="field__hint">This ticket has not been escalated.</p>
      )}

      {escalations && escalations.length > 0 && (
        <ol className="escalation-history">
          {escalations.map((escalation) => (
            <li key={escalation.id} className="escalation-history__item">
              <p className="escalation-history__headline">
                {escalation.fromPriority} to {escalation.toPriority}
                <span className="summary__note">
                  by {escalation.escalatedBy}, {formatDateTime(escalation.escalatedAtUtc)}
                </span>
              </p>
              <p className="escalation-history__reason">{escalation.reason}</p>
              <dl className="escalation-history__changes">
                <div>
                  <dt>Agent</dt>
                  <dd>
                    {agentName(escalation.fromAgent)} to {agentName(escalation.toAgent)}
                  </dd>
                </div>
                <div>
                  <dt>Due</dt>
                  <dd>
                    {formatDateTime(escalation.fromDueAtUtc)} to{' '}
                    {formatDateTime(escalation.toDueAtUtc)}
                  </dd>
                </div>
              </dl>
            </li>
          ))}
        </ol>
      )}
    </div>
  );
}

function agentName(agent: AgentSummary | null): string {
  return agent?.fullName ?? 'Unassigned';
}
