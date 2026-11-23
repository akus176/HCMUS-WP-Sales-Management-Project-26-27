const destinations = {
  assignment: { module: 'delivery', permission: 'delivery.update' },
  'low-stock': { module: 'inventory', permission: 'inventory.manage' },
  'urgent-order': { module: 'barista', permission: 'orders.fulfill' },
} as const;

export const notificationTitle = 'Coffee Shop Staff';
export const notificationBody = 'Open the app to review a staff update.';

export function notificationDestination(payload: unknown) {
  if (!payload || typeof payload !== 'object') return;
  const { kind, recordId } = payload as Record<string, unknown>;
  if (
    typeof kind !== 'string' ||
    !Object.hasOwn(destinations, kind) ||
    typeof recordId !== 'string' ||
    !/^[\da-f]{8}(?:-[\da-f]{4}){3}-[\da-f]{12}$/i.test(recordId) ||
    recordId === '00000000-0000-0000-0000-000000000000'
  )
    return;
  const destination = kind as keyof typeof destinations;
  return { kind: destination, recordId, ...destinations[destination] };
}
