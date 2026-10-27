import type { BadgeTone } from '../../components/Badge';
import type { SlaStatus } from '../../types/api';

export interface SlaState {
  label: string;
  /** A shape per state, so the state never depends on colour alone. Hidden from screen readers. */
  symbol: string;
  tone: BadgeTone;
}

/** Every SLA state. A Record, so a new state is a compile error until it is described here. */
export const slaStates: Record<SlaStatus, SlaState> = {
  WithinSla: { label: 'Within SLA', symbol: '○', tone: 'info' },
  AtRisk: { label: 'At risk', symbol: '!', tone: 'warning' },
  Breached: { label: 'Breached', symbol: '✕', tone: 'danger' },
  Met: { label: 'Met', symbol: '✓', tone: 'success' },
  NotApplicable: { label: 'No SLA', symbol: '–', tone: 'neutral' },
};

/** The states in the order the filter offers them. */
export const slaStatuses: SlaStatus[] = ['WithinSla', 'AtRisk', 'Breached', 'Met', 'NotApplicable'];

export function slaStatusLabel(status: SlaStatus): string {
  return slaStates[status].label;
}
