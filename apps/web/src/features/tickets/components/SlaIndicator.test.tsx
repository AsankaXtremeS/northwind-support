import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import type { SlaStatus } from '../../../types/api';
import { slaStatusLabel, slaStatuses } from '../slaStates';
import { SlaIndicator } from './SlaIndicator';

const expectedLabels: Record<SlaStatus, string> = {
  WithinSla: 'Within SLA',
  AtRisk: 'At risk',
  Breached: 'Breached',
  Met: 'Met',
  NotApplicable: 'No SLA',
};

describe('SlaIndicator', () => {
  it.each(slaStatuses)('names the %s state in words, not just colour', (status) => {
    render(<SlaIndicator slaStatus={status} dueAtUtc="2026-09-24T16:12:00Z" />);

    expect(screen.getByText(expectedLabels[status])).toBeInTheDocument();
  });

  it('gives every state its own label and its own symbol', () => {
    const { container } = render(
      <>
        {slaStatuses.map((status) => (
          <SlaIndicator key={status} slaStatus={status} dueAtUtc={null} />
        ))}
      </>,
    );

    const labels = slaStatuses.map(slaStatusLabel);
    const symbols = [...container.querySelectorAll('.sla-indicator__symbol')].map((node) => node.textContent);

    expect(new Set(labels).size).toBe(slaStatuses.length);
    expect(new Set(symbols).size).toBe(slaStatuses.length);
  });

  it('hides the symbol from screen readers, which hear the label instead', () => {
    const { container } = render(<SlaIndicator slaStatus="AtRisk" dueAtUtc={null} />);

    expect(container.querySelector('.sla-indicator__symbol')).toHaveAttribute('aria-hidden', 'true');
  });

  it('offers every state the API can return', () => {
    expect([...slaStatuses].sort()).toEqual(Object.keys(expectedLabels).sort());
  });

  it('shows a dash when there is no due date', () => {
    render(<SlaIndicator slaStatus="NotApplicable" dueAtUtc={null} />);

    expect(screen.getByText('-')).toBeInTheDocument();
  });
});
