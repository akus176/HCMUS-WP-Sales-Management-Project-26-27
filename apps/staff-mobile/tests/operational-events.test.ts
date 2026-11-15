import { QueryClient, QueryObserver } from '@tanstack/react-query';

import {
  operationalEventType,
  refreshOperationalQueries,
} from '../src/realtime/operationalEvents';
import { deferred } from './helpers';

const userId = 'current-staff';

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

test('an inventory event refreshes each active inventory view for only the current staff member', async () => {
  const client = new QueryClient();
  const affectedKeys = [
    ['inventory-balances', userId, 'balances', ''],
    ['inventory-movements', userId, 'ingredient-1'],
    ['purchase-receipts', userId],
    ['purchase-receipt', userId, 'receipt-1'],
  ];
  const unaffectedKeys = [
    ['sales', userId, 'orders'],
    ['inventory-balances', 'different-staff', 'balances', ''],
  ];
  const observations = [...affectedKeys, ...unaffectedKeys].map((key) => {
    client.setQueryData(key, 'old');
    const queryFn = jest.fn(async () => 'new');
    const observer = new QueryObserver(client, {
      queryKey: key,
      queryFn,
      staleTime: Infinity,
    });
    return { key, queryFn, unsubscribe: observer.subscribe(() => undefined) };
  });

  const type = operationalEventType({
    type: 'inventory',
    id: 'opaque',
    updatedAt: '2026-11-16T00:00:00Z',
  });
  await refreshOperationalQueries(client, userId, () => true, type);

  for (const { key, queryFn } of observations.slice(0, affectedKeys.length)) {
    expect(queryFn).toHaveBeenCalledTimes(1);
    expect(client.getQueryData(key)).toBe('new');
  }
  for (const { key, queryFn } of observations.slice(affectedKeys.length)) {
    expect(queryFn).not.toHaveBeenCalled();
    expect(client.getQueryData(key)).toBe('old');
    expect(client.getQueryState(key)?.isInvalidated).toBe(false);
  }
  observations.forEach(({ unsubscribe }) => unsubscribe());
  client.clear();
});
