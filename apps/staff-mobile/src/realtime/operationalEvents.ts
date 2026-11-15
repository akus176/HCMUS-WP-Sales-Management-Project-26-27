import type { QueryClient } from '@tanstack/react-query';

const affectedViews: Readonly<Record<string, readonly string[]>> = {
  order: ['preparation', 'sales', 'delivery', 'discount-approvals'],
  discount: ['discount-approvals', 'sales'],
  payment: ['sales'],
  inventory: [
    'inventory-balances',
    'inventory-movements',
    'purchase-receipts',
    'purchase-receipt',
  ],
  delivery: ['delivery', 'sales'],
  account: ['administration'],
};

export function operationalEventType(event: unknown) {
  if (!event || typeof event !== 'object') return;
  const { type, id, updatedAt } = event as Record<string, unknown>;
  if (
    typeof type === 'string' &&
    Object.hasOwn(affectedViews, type) &&
    typeof id === 'string' &&
    typeof updatedAt === 'string' &&
    Number.isFinite(Date.parse(updatedAt))
  )
    return type;
}

export async function refreshOperationalQueries(
  queries: QueryClient,
  userId: string,
  isCurrent: () => boolean,
  eventType?: string,
) {
  const views = eventType ? affectedViews[eventType] : undefined;
  const filters = {
    predicate: ({ queryKey }: { queryKey: readonly unknown[] }) =>
      queryKey[1] === userId && (!views || views.includes(String(queryKey[0]))),
  };
  // Discard in-flight reads that started before the server's committed change.
  // The event contains no authoritative values to write into the cache.
  await queries.cancelQueries(filters);
  if (isCurrent()) await queries.invalidateQueries(filters);
}
