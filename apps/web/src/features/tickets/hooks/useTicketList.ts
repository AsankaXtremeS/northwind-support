import { useEffect, useRef, useState } from 'react';
import { ApiError } from '../../../api/client';
import { ticketsApi } from '../../../api/tickets';
import type { PagedResult, TicketFilters, TicketListItem } from '../../../types/api';

export const DEBOUNCE_MS = 300;

export const emptyFilters: TicketFilters = {
  search: '',
  status: undefined,
  priority: undefined,
  categoryId: undefined,
  customerId: undefined,
  assignedAgentId: undefined,
  unassignedOnly: false,
  page: 1,
  pageSize: 20,
  sortBy: 'createdAtUtc',
  sortDirection: 'desc',
};

interface State {
  /** The filters the data or error below were fetched with. */
  query?: TicketFilters;
  data?: PagedResult<TicketListItem>;
  error?: string;
}

/** Fetches one page and turns the outcome into a state patch. Never throws. */
async function fetchPage(query: TicketFilters, signal: AbortSignal): Promise<State> {
  try {
    const data = await ticketsApi.getTickets(query, signal);
    return { query, data, error: undefined };
  } catch (error) {
    const message = error instanceof ApiError ? error.message : 'Unable to load tickets.';
    return { query, error: message };
  }
}

/** Owns the ticket list's filters and the data they produce. */
export function useTicketList() {
  const [filters, setFilters] = useState<TicketFilters>(emptyFilters);
  // The search text the list is fetched with. Trails filters.search while the user is typing.
  const [settledSearch, setSettledSearch] = useState(emptyFilters.search);
  const [state, setState] = useState<State>({});
  // The newest request. Starting another cancels it, so an older response never lands last.
  const inFlight = useRef<AbortController | null>(null);

  // Fetches whenever any filter changes. Only the search text waits for typing to settle.
  useEffect(() => {
    if (filters.search !== settledSearch) {
      return;
    }

    inFlight.current?.abort();
    const controller = new AbortController();
    inFlight.current = controller;

    void fetchPage(filters, controller.signal).then((result) => {
      if (!controller.signal.aborted) {
        setState((current) => ({ ...current, ...result }));
      }
    });

    return () => controller.abort();
  }, [filters, settledSearch]);

  useEffect(() => {
    const handle = window.setTimeout(() => setSettledSearch(filters.search), DEBOUNCE_MS);

    return () => window.clearTimeout(handle);
  }, [filters.search]);

  const updateFilters = (patch: Partial<TicketFilters>) => {
    setFilters((current) => ({ ...current, ...patch, page: patch.page ?? 1 }));

    // Anything other than typing applies at once, taking any half-typed search with it.
    if (!('search' in patch)) {
      setSettledSearch(filters.search);
    }
  };

  const resetFilters = () => {
    setFilters(emptyFilters);
    setSettledSearch(emptyFilters.search);
  };

  const reload = async () => {
    inFlight.current?.abort();
    const controller = new AbortController();
    inFlight.current = controller;

    setState((current) => ({ ...current, query: undefined, error: undefined }));
    const result = await fetchPage(filters, controller.signal);

    if (!controller.signal.aborted) {
      setState((current) => ({ ...current, ...result }));
    }
  };

  // Loading until the data on screen was fetched with exactly the current filters.
  const isLoading = state.query !== filters;

  return { filters, updateFilters, resetFilters, reload, isLoading, data: state.data, error: state.error };
}
