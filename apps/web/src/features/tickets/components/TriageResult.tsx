import { formatDateTime, formatMinutes } from '../../../lib/format';
import type { RaisedTicket } from '../../../types/api';
import { PriorityBadge } from './TicketBadges';

interface TriageResultProps {
  raised: RaisedTicket;
}

/** What triage decided for a new ticket, and why, so nobody has to guess. */
export function TriageResult({ raised: { ticket, triage } }: TriageResultProps) {
  return (
    <dl className="summary triage">
      <div>
        <dt>Priority</dt>
        <dd>
          <PriorityBadge priority={triage.priority} />
          <span className="summary__note">{triage.priorityReason}</span>
        </dd>
      </div>

      <div>
        <dt>SLA</dt>
        <dd>
          {formatMinutes(triage.slaWindowMinutes)}, due {formatDateTime(ticket.dueAtUtc)}
          <span className="summary__note">{triage.slaWindowReason}</span>
        </dd>
      </div>

      <div>
        <dt>Assigned agent</dt>
        <dd>
          {triage.assignedAgent?.fullName ?? 'Unassigned'}
          <span className="summary__note">{triage.assignmentReason}</span>
        </dd>
      </div>
    </dl>
  );
}
