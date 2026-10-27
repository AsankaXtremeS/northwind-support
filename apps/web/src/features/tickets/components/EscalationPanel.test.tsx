import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { ApiError } from '../../../api/client';
import { ticketsApi } from '../../../api/tickets';
import { ticketDetail, ticketEscalation } from '../../../test/fixtures';
import type { TicketDetail } from '../../../types/api';
import { EscalationPanel } from './EscalationPanel';

vi.mock('../../../api/tickets', () => ({
  ticketsApi: {
    escalate: vi.fn(),
  },
}));

const escalate = vi.mocked(ticketsApi.escalate);

function renderPanel(ticket: TicketDetail = ticketDetail()) {
  const onEscalated = vi.fn();
  render(<EscalationPanel ticket={ticket} onEscalated={onEscalated} />);

  return onEscalated;
}

describe('EscalationPanel', () => {
  afterEach(() => {
    vi.clearAllMocks();
  });

  it('offers to escalate to the next priority', () => {
    renderPanel(ticketDetail({ priority: 'Medium' }));

    expect(screen.getByRole('button', { name: 'Escalate to High' })).toBeInTheDocument();
  });

  it('sends the trimmed reason and name, then hands the response back', async () => {
    const escalated = {
      ticket: ticketDetail({ priority: 'Critical', canEscalate: false }),
      escalation: ticketEscalation(),
    };
    escalate.mockResolvedValue(escalated);
    const onEscalated = renderPanel();

    await userEvent.type(screen.getByLabelText('Reason'), '  Customer is blocked  ');
    await userEvent.type(screen.getByLabelText('Escalated by'), ' Dana ');
    await userEvent.click(screen.getByRole('button', { name: 'Escalate to Critical' }));

    expect(escalate).toHaveBeenCalledWith(12, { reason: 'Customer is blocked', escalatedBy: 'Dana' });
    expect(onEscalated).toHaveBeenCalledWith(escalated);
  });

  it('does not call the API when the trimmed reason is too short or the name is missing', async () => {
    renderPanel();

    await userEvent.type(screen.getByLabelText('Reason'), '  abc   ');
    await userEvent.click(screen.getByRole('button', { name: 'Escalate to Critical' }));

    expect(escalate).not.toHaveBeenCalled();
    expect(screen.getByText('Give a reason of 5 to 500 characters.')).toBeInTheDocument();
    expect(screen.getByText('Say who is escalating.')).toBeInTheDocument();
  });

  it('shows the API message when the escalation is refused', async () => {
    escalate.mockRejectedValue(
      new ApiError(409, 'Ticket TCK-0012 is already Critical and cannot be escalated further.'),
    );
    renderPanel();

    await userEvent.type(screen.getByLabelText('Reason'), 'Customer is blocked');
    await userEvent.type(screen.getByLabelText('Escalated by'), 'Dana');
    await userEvent.click(screen.getByRole('button', { name: 'Escalate to Critical' }));

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'Ticket TCK-0012 is already Critical and cannot be escalated further.',
    );
  });

  it('shows the API field errors next to the fields', async () => {
    escalate.mockRejectedValue(
      new ApiError(400, 'One or more validation errors occurred.', {
        errors: { Reason: ['Give a reason of 5 to 500 characters.'] },
      }),
    );
    renderPanel();

    await userEvent.type(screen.getByLabelText('Reason'), 'Customer is blocked');
    await userEvent.type(screen.getByLabelText('Escalated by'), 'Dana');
    await userEvent.click(screen.getByRole('button', { name: 'Escalate to Critical' }));

    expect(await screen.findByText('Give a reason of 5 to 500 characters.')).toBeInTheDocument();
    expect(screen.getByLabelText('Reason')).toHaveAttribute('aria-invalid', 'true');
  });

  it.each<[string, Partial<TicketDetail>, string]>([
    ['Critical', { priority: 'Critical', canEscalate: false }, 'already Critical'],
    ['Resolved', { status: 'Resolved', canEscalate: false }, 'resolved, so it cannot be escalated'],
    ['Closed', { status: 'Closed', canEscalate: false }, 'closed, so it cannot be escalated'],
  ])('explains why a %s ticket cannot be escalated, with no form', (_, overrides, message) => {
    renderPanel(ticketDetail(overrides));

    expect(screen.getByText(new RegExp(message))).toBeInTheDocument();
    expect(screen.queryByRole('button')).not.toBeInTheDocument();
  });
});
