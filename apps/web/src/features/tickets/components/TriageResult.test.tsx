import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { formatMinutes } from '../../../lib/format';
import { ticketDetail } from '../../../test/fixtures';
import type { RaisedTicket, Triage } from '../../../types/api';
import { TriageResult } from './TriageResult';

function raised(overrides: Partial<Triage> = {}): RaisedTicket {
  return {
    ticket: ticketDetail({ priority: 'Critical' }),
    triage: {
      priority: 'Critical',
      priorityReason: 'Security tickets are always Critical.',
      slaWindowMinutes: 90,
      slaWindowReason: 'Critical tickets are due in 2 h; Premium customers get 75% of that.',
      assignedAgent: { id: 7, fullName: 'Tom Okafor' },
      assignmentReason: 'Tom Okafor has the fewest open tickets among eligible agents.',
      ...overrides,
    },
  };
}

describe('TriageResult', () => {
  it('shows each decision with its reason', () => {
    render(<TriageResult raised={raised()} />);

    expect(screen.getByText('Critical')).toBeInTheDocument();
    expect(screen.getByText('Security tickets are always Critical.')).toBeInTheDocument();
    expect(screen.getByText(/^1 h 30 min, due/)).toBeInTheDocument();
    expect(
      screen.getByText('Critical tickets are due in 2 h; Premium customers get 75% of that.'),
    ).toBeInTheDocument();
    expect(screen.getByText('Tom Okafor')).toBeInTheDocument();
    expect(
      screen.getByText('Tom Okafor has the fewest open tickets among eligible agents.'),
    ).toBeInTheDocument();
  });

  it('says the ticket is unassigned, and why, when no agent was eligible', () => {
    render(
      <TriageResult
        raised={raised({ assignedAgent: null, assignmentReason: 'No eligible agent is available.' })}
      />,
    );

    expect(screen.getByText('Unassigned')).toBeInTheDocument();
    expect(screen.getByText('No eligible agent is available.')).toBeInTheDocument();
  });

  it.each([
    [45, '45 min'],
    [60, '1 h'],
    [90, '1 h 30 min'],
    [1440, '24 h'],
  ])('formats a %i minute SLA window as "%s"', (minutes, expected) => {
    expect(formatMinutes(minutes)).toBe(expected);
  });
});
