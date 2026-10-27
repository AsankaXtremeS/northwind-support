import type { PagedResult, TicketDetail, TicketEscalation, TicketListItem } from '../types/api';

/** A list row with sensible defaults, so tests only state what they care about. */
export function ticketListItem(overrides: Partial<TicketListItem> = {}): TicketListItem {
  return {
    id: 12,
    reference: 'TCK-0012',
    title: 'Invoice total does not match the order',
    status: 'InProgress',
    priority: 'High',
    customer: { id: 2, name: 'Fabrikam Inc', tier: 'Standard' },
    category: { id: 2, name: 'Billing' },
    assignedAgent: { id: 4, fullName: 'Sara Lindqvist' },
    createdAtUtc: '2026-09-24T08:12:00Z',
    updatedAtUtc: '2026-09-25T10:03:00Z',
    dueAtUtc: '2026-09-24T16:12:00Z',
    resolvedAtUtc: null,
    slaStatus: 'Breached',
    ...overrides,
  };
}

export function page(items: TicketListItem[]): PagedResult<TicketListItem> {
  return {
    items,
    page: 1,
    pageSize: 20,
    totalCount: items.length,
    totalPages: 1,
  };
}

/** An open, High priority ticket that can still be escalated. */
export function ticketDetail(overrides: Partial<TicketDetail> = {}): TicketDetail {
  return {
    id: 12,
    reference: 'TCK-0012',
    title: 'Invoice total does not match the order',
    description: 'The invoice total is higher than the order total.',
    status: 'InProgress',
    priority: 'High',
    customer: { id: 2, name: 'Fabrikam Inc', tier: 'Standard', email: 'ap@fabrikam.example', phone: null },
    category: { id: 2, name: 'Billing', requiresSpecialist: false },
    assignedAgent: { id: 4, fullName: 'Sara Lindqvist' },
    createdAtUtc: '2026-09-24T08:12:00Z',
    updatedAtUtc: '2026-09-25T10:03:00Z',
    dueAtUtc: '2026-09-24T16:12:00Z',
    resolvedAtUtc: null,
    slaStatus: 'Breached',
    canEscalate: true,
    ...overrides,
  };
}

export function ticketEscalation(overrides: Partial<TicketEscalation> = {}): TicketEscalation {
  return {
    id: 1,
    fromPriority: 'High',
    toPriority: 'Critical',
    fromAgent: { id: 4, fullName: 'Sara Lindqvist' },
    toAgent: { id: 7, fullName: 'Tom Okafor' },
    fromDueAtUtc: '2026-09-24T16:12:00Z',
    toDueAtUtc: '2026-09-25T14:03:00Z',
    reason: 'Customer is blocked from paying',
    escalatedBy: 'Dana',
    escalatedAtUtc: '2026-09-25T10:03:00Z',
    ...overrides,
  };
}
