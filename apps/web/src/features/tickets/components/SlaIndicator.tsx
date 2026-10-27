import { Badge } from '../../../components/Badge';
import { formatDateTime } from '../../../lib/format';
import type { SlaStatus } from '../../../types/api';
import { slaStates } from '../slaStates';

interface SlaIndicatorProps {
  slaStatus: SlaStatus;
  dueAtUtc: string | null;
}

/** Where a ticket stands against its SLA, and when it is due. Used by the list and the detail page. */
export function SlaIndicator({ slaStatus, dueAtUtc }: SlaIndicatorProps) {
  const state = slaStates[slaStatus];

  return (
    <div className="sla-indicator">
      <Badge tone={state.tone}>
        <span className="sla-indicator__symbol" aria-hidden="true">
          {state.symbol}
        </span>
        <span>{state.label}</span>
      </Badge>
      <span className="sla-indicator__due">{formatDateTime(dueAtUtc)}</span>
    </div>
  );
}
