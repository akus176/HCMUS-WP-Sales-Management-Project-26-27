export type MobileAction = { label: string; permission: string };
export type MobileModule = {
  id: string;
  title: string;
  actions: readonly MobileAction[];
};

// These are API permission codes, not role names. Workflow implementations are
// deliberately separate from this navigation policy (#155, #156 and later).
export const mobileModules: readonly MobileModule[] = [
  {
    id: 'barista',
    title: 'Preparation',
    actions: [
      { label: 'View and prepare tickets', permission: 'orders.fulfill' },
    ],
  },
  {
    id: 'cashier',
    title: 'Counter',
    actions: [
      { label: 'Search orders', permission: 'orders.create' },
      { label: 'Record payments', permission: 'payments.record' },
    ],
  },
  {
    id: 'inventory',
    title: 'Inventory',
    actions: [
      { label: 'View ingredient balances', permission: 'inventory.manage' },
      { label: 'View low stock', permission: 'reports.lowstock' },
      { label: 'Adjust stock', permission: 'inventory.adjust' },
      { label: 'Post prepared receipts', permission: 'inventory.receive' },
    ],
  },
  {
    id: 'delivery',
    title: 'Deliveries',
    actions: [
      {
        label: 'View and update assigned deliveries',
        permission: 'delivery.update',
      },
    ],
  },
  {
    id: 'administration',
    title: 'Administration',
    actions: [
      {
        label: 'Review staff accounts and security events',
        permission: 'users.manage',
      },
    ],
  },
];

export function permittedActions(
  module: MobileModule,
  permissions: readonly string[],
) {
  return module.actions.filter((action) =>
    permissions.includes(action.permission),
  );
}
export function permittedModules(permissions: readonly string[]) {
  return mobileModules.filter(
    (module) => permittedActions(module, permissions).length > 0,
  );
}
const orderOperations: MobileModule = {
  id: 'operations',
  title: 'Order operations',
  actions: [
    { label: 'Authorize cancellations', permission: 'orders.cancel' },
    { label: 'Assign deliveries', permission: 'delivery.assign' },
  ],
};

export function findModule(id: string | string[] | undefined) {
  return typeof id === 'string'
    ? id === 'operations'
      ? orderOperations
      : mobileModules.find((module) => module.id === id)
    : undefined;
}
