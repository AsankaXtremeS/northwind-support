import { describe, expect, it } from 'vitest';
import { returnPathFrom } from './authContext';

describe('returnPathFrom', () => {
  it.each([
    [{ from: '/tickets/12' }, '/tickets/12'],
    [{ from: '/tickets?status=Open' }, '/tickets?status=Open'],
    [undefined, '/tickets'],
    [{ from: 42 }, '/tickets'],
    [{ from: 'https://evil.example' }, '/tickets'],
    [{ from: '//evil.example' }, '/tickets'],
    [{ from: '/login' }, '/tickets'],
  ])('given %j returns %s', (state, expected) => {
    expect(returnPathFrom(state)).toBe(expected);
  });
});
