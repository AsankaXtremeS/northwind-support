import { act, renderHook } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { ticketsApi } from '../../../api/tickets';
import { page, ticketListItem } from '../../../test/fixtures';
import type { TicketFilters } from '../../../types/api';
import { DEBOUNCE_MS, useTicketList } from './useTicketList';

vi.mock('../../../api/tickets', () => ({
  ticketsApi: {
    getTickets: vi.fn(),
  },
}));

const getTickets = vi.mocked(ticketsApi.getTickets);

describe('useTicketList', () => {
  beforeEach(() => {
    vi.useFakeTimers();
    getTickets.mockResolvedValue(page([ticketListItem()]));
  });

  afterEach(() => {
    vi.useRealTimers();
    vi.clearAllMocks();
  });

  it('searches with the text the user typed, once the typing settles', async () => {
    const { result } = renderHook(() => useTicketList());

    await act(async () => {
      await vi.advanceTimersByTimeAsync(DEBOUNCE_MS);
    });

    expect(getTickets).toHaveBeenCalledTimes(1);

    act(() => {
      result.current.updateFilters({ search: 'invoice' });
    });

    // Still inside the debounce window: no second request yet.
    expect(getTickets).toHaveBeenCalledTimes(1);

    await act(async () => {
      await vi.advanceTimersByTimeAsync(DEBOUNCE_MS);
    });

    expect(getTickets).toHaveBeenCalledTimes(2);
    expect(getTickets).toHaveBeenLastCalledWith(
      expect.objectContaining({ search: 'invoice', page: 1 }),
      expect.any(AbortSignal),
    );

    expect(result.current.isLoading).toBe(false);
    expect(result.current.data?.items).toHaveLength(1);
  });

  //Tests
  describe('requests sent when filters change (DEFECT-117)', () => {
    const fetchMock = vi.fn<typeof fetch>();

    /** Query strings of every request that reached fetch, in order. */
    const sentQueries = () =>
      fetchMock.mock.calls.map(([input]) => new URL(String(input), 'http://localhost').searchParams);

    const lastSentQuery = () => sentQueries().at(-1);

    const advance = async (ms: number) => {
      await act(async () => {
        await vi.advanceTimersByTimeAsync(ms);
      });
    };

    /** Lets the debounce run out. Only the initial load and search should need this. */
    const settleDebounce = () => advance(DEBOUNCE_MS);

    /** Flushes pending effects and promises without moving the clock. */
    const flush = () => advance(0);

    beforeEach(async () => {
      // Run the real API module so the URL it builds is what the tests see.
      const actual = await vi.importActual<typeof import('../../../api/tickets')>(
        '../../../api/tickets',
      );
      getTickets.mockImplementation(actual.ticketsApi.getTickets);

      fetchMock.mockImplementation(
        async () => new Response(JSON.stringify(page([ticketListItem()])), { status: 200 }),
      );
      vi.stubGlobal('fetch', fetchMock);
    });

    afterEach(() => {
      vi.unstubAllGlobals();
      fetchMock.mockReset();
    });

    it('immediately sends a new request with the status when the status filter changes on page 1', async () => {
      const { result } = renderHook(() => useTicketList());
      await settleDebounce();
      expect(fetchMock).toHaveBeenCalledTimes(1);

      act(() => {
        result.current.updateFilters({ status: 'InProgress' });
      });
      await flush();

      expect(fetchMock).toHaveBeenCalledTimes(2);
      expect(lastSentQuery()?.get('status')).toBe('InProgress');
      expect(lastSentQuery()?.get('page')).toBe('1');
    });

    it.each<[string, Partial<TicketFilters>, string]>([
      ['priority', { priority: 'High' }, 'High'],
      ['categoryId', { categoryId: 3 }, '3'],
      ['customerId', { customerId: 2 }, '2'],
      ['assignedAgentId', { assignedAgentId: 4 }, '4'],
      ['unassignedOnly', { unassignedOnly: true }, 'true'],
      ['sortBy', { sortBy: 'dueAtUtc' }, 'dueAtUtc'],
      ['sortDirection', { sortDirection: 'asc' }, 'asc'],
    ])(
      'immediately sends a new request with %s when it changes on page 1',
      async (param, patch, expected) => {
        const { result } = renderHook(() => useTicketList());
        await settleDebounce();
        expect(fetchMock).toHaveBeenCalledTimes(1);

        act(() => {
          result.current.updateFilters(patch);
        });
        await flush();

        expect(fetchMock).toHaveBeenCalledTimes(2);
        expect(lastSentQuery()?.get(param)).toBe(expected);
      },
    );

    it('sends only one request, and only once the debounce has passed, when search is typed quickly', async () => {
      const { result } = renderHook(() => useTicketList());
      await settleDebounce();
      expect(fetchMock).toHaveBeenCalledTimes(1);

      // Each keystroke lands well inside the previous one's debounce window.
      const keystrokeGap = Math.floor(DEBOUNCE_MS / 3);

      for (const text of ['i', 'in', 'inv', 'invo']) {
        act(() => {
          result.current.updateFilters({ search: text });
        });
        await advance(keystrokeGap);
      }

      expect(fetchMock).toHaveBeenCalledTimes(1);

      // One tick short of the debounce after the last keystroke: still nothing sent.
      await advance(DEBOUNCE_MS - keystrokeGap - 1);
      expect(fetchMock).toHaveBeenCalledTimes(1);

      await advance(1);

      expect(fetchMock).toHaveBeenCalledTimes(2);
      expect(lastSentQuery()?.get('search')).toBe('invo');
    });

    it('immediately sends a new request without the cleared filter when filters are reset on page 1', async () => {
      const { result } = renderHook(() => useTicketList());
      await settleDebounce();

      act(() => {
        result.current.updateFilters({ status: 'Open' });
      });
      await flush();

      const requestsBeforeReset = fetchMock.mock.calls.length;

      act(() => {
        result.current.resetFilters();
      });
      await flush();

      expect(fetchMock).toHaveBeenCalledTimes(requestsBeforeReset + 1);
      expect(lastSentQuery()?.has('status')).toBe(false);
      expect(lastSentQuery()?.get('page')).toBe('1');
    });
  });
});
