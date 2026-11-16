import { QueryClient, QueryObserver } from '@tanstack/react-query';

import {
  operationalEventType,
  refreshOperationalQueries,
} from '../src/realtime/operationalEvents';
import { deferred } from './helpers';

const userId = 'current-staff';

test.each([
  ['inventory-balances', userId, 'balances', 'beans'],
  ['inventory-balances', userId, 'low-stock', ''],
  ['inventory-movements', userId, 'ingredient'],
  ['inventory-movements', userId, 'all'],
  ['purchase-receipts', userId],
  ['purchase-receipt', userId, 'receipt'],
])(
  'inventory events refetch %j without accepting a late stale read',
  async (...key) => {
    const client = new QueryClient();
    const otherUserKey = [key[0], 'different-staff', ...key.slice(2)];
    const unrelatedKey = ['sales', userId, 'orders'];
    client.setQueryData(key, { revision: 2 });
    client.setQueryData(otherUserKey, 'another account');
    client.setQueryData(unrelatedKey, 'unrelated view');
    const stale = deferred<{ revision: number }>();
    const queryFn = jest
      .fn()
      .mockReturnValueOnce(stale.promise)
      .mockResolvedValue({ revision: 4 });
    const observer = new QueryObserver(client, { queryKey: key, queryFn });
    const unsubscribe = observer.subscribe(() => undefined);
    try {
      await refreshOperationalQueries(client, userId, () => true, 'inventory');
      stale.resolve({ revision: 1 });
      await stale.promise;
      expect(queryFn).toHaveBeenCalledTimes(2);
      expect(client.getQueryData(key)).toEqual({ revision: 4 });
      expect(client.getQueryState(otherUserKey)?.isInvalidated).toBe(false);
      expect(client.getQueryState(unrelatedKey)?.isInvalidated).toBe(false);
    } finally {
      stale.resolve({ revision: 1 });
      unsubscribe();
      client.clear();
    }
  },
);

test('an older identifier-only event refetches and cannot overwrite newer server data', async () => {
  const client = new QueryClient();
  const key = ['sales', userId, 'orders'];
  const otherKey = ['delivery', userId, 'list'];
  client.setQueryData(key, { revision: 2 });
  client.setQueryData(otherKey, { revision: 3 });
  const stale = deferred<{ revision: number }>();
  const queryFn = jest
    .fn()
    .mockReturnValueOnce(stale.promise)
    .mockResolvedValue({ revision: 4 });
  const observer = new QueryObserver(client, { queryKey: key, queryFn });
  const unsubscribe = observer.subscribe(() => undefined);

  const type = operationalEventType({
    type: 'payment',
    id: 'opaque',
    updatedAt: '2026-11-08T04:30:00Z',
  });
  expect(type).toBe('payment');
  await refreshOperationalQueries(client, userId, () => true, type);
  stale.resolve({ revision: 1 });
  await stale.promise;

  expect(queryFn).toHaveBeenCalledTimes(2);
  expect(client.getQueryData(key)).toEqual({ revision: 4 });
  expect(client.getQueryState(otherKey)?.isInvalidated).toBe(false);
  expect(
    operationalEventType({
      type: '__proto__',
      id: 'opaque',
      updatedAt: '2026-11-08T04:30:00Z',
    }),
  ).toBeUndefined();
  unsubscribe();
  client.clear();
});

test('reconnect refetches active views even when no events were received', async () => {
  const client = new QueryClient();
  const key = ['delivery', userId, 'detail', 'order'];
  const otherKey = ['delivery', 'different-staff', 'detail', 'order'];
  client.setQueryData(key, 'assigned');
  client.setQueryData(otherKey, 'separate session');
  const queryFn = jest.fn().mockResolvedValue('out for delivery');
  const observer = new QueryObserver(client, {
    queryKey: key,
    queryFn,
    staleTime: Infinity,
  });
  const unsubscribe = observer.subscribe(() => undefined);
  expect(queryFn).not.toHaveBeenCalled();

  await refreshOperationalQueries(client, userId, () => true);
  expect(queryFn).toHaveBeenCalledTimes(1);
  expect(client.getQueryData(key)).toBe('out for delivery');
  expect(client.getQueryState(otherKey)?.isInvalidated).toBe(false);
  unsubscribe();
  client.clear();
});
