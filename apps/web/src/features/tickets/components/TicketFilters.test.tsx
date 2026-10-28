import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { emptyFilters } from '../hooks/useTicketList';
import { TicketFilters } from './TicketFilters';

function renderFilters(onChange = vi.fn()) {
  render(
    <TicketFilters
      filters={emptyFilters}
      agents={[]}
      categories={[]}
      customers={[]}
      onChange={onChange}
      onReset={vi.fn()}
    />,
  );

  return onChange;
}

describe('TicketFilters', () => {
  it('reports the chosen SLA state', async () => {
    const onChange = renderFilters();

    await userEvent.selectOptions(screen.getByLabelText('SLA'), 'At risk');

    expect(onChange).toHaveBeenLastCalledWith({ slaStatus: 'AtRisk' });
  });

  it('offers every SLA state, plus any', () => {
    renderFilters();

    const options = screen.getAllByRole('option', { name: /SLA|risk|Breached|Met/ });

    expect(screen.getByRole('option', { name: 'Any SLA state' })).toBeInTheDocument();
    expect(options.map((option) => option.textContent)).toEqual(
      expect.arrayContaining(['Within SLA', 'At risk', 'Breached', 'Met', 'No SLA']),
    );
  });
});
