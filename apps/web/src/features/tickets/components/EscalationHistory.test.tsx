import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { ticketEscalation } from '../../../test/fixtures';
import type { TicketEscalation } from '../../../types/api';
import { EscalationHistory } from './EscalationHistory';

function renderHistory(
  escalations: TicketEscalation[] | undefined,
  { isLoading = false, error, onRetry = vi.fn() }: { isLoading?: boolean; error?: string; onRetry?: () => void } = {},
) {
  render(
    <EscalationHistory escalations={escalations} isLoading={isLoading} error={error} onRetry={onRetry} />,
  );

  return onRetry;
}

describe('EscalationHistory', () => {
  it('lists the escalations in the order the API returns them, newest first', () => {
    renderHistory([
      ticketEscalation({ id: 2, fromPriority: 'High', toPriority: 'Critical', reason: 'Still blocked' }),
      ticketEscalation({ id: 1, fromPriority: 'Medium', toPriority: 'High', reason: 'Customer chased' }),
    ]);

    const items = screen.getAllByRole('listitem');

    expect(items).toHaveLength(2);
    expect(items[0]).toHaveTextContent('High to Critical');
    expect(items[0]).toHaveTextContent('Still blocked');
    expect(items[1]).toHaveTextContent('Medium to High');
    expect(items[1]).toHaveTextContent('Customer chased');
  });

  it('shows who escalated, and the agent before and after', () => {
    renderHistory([ticketEscalation({ fromAgent: null })]);

    const item = screen.getByRole('listitem');

    expect(within(item).getByText(/by Dana/)).toBeInTheDocument();
    expect(item).toHaveTextContent('Unassigned to Tom Okafor');
  });

  it('says so when the ticket has never been escalated', () => {
    renderHistory([]);

    expect(screen.getByText('This ticket has not been escalated.')).toBeInTheDocument();
  });

  it('offers a retry when the history could not be loaded', async () => {
    const onRetry = renderHistory(undefined, { error: 'Unable to load the escalation history.' });

    await userEvent.click(screen.getByRole('button', { name: 'Try again' }));

    expect(onRetry).toHaveBeenCalled();
  });
});
