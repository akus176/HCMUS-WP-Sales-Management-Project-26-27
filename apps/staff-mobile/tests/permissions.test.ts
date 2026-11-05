import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import {
  permittedModules,
  findModule,
  permittedActions,
} from '../src/shell/permissions';

const fixtures: [string, string[], string[]][] = [
  ['Barista', ['orders.fulfill'], ['barista']],
  [
    'InventoryStaff',
    [
      'inventory.receive',
      'inventory.adjust',
      'inventory.manage',
      'reports.lowstock',
    ],
    ['inventory'],
  ],
  ['DeliveryStaff', ['delivery.update'], ['delivery']],
  [
    'Cashier',
    [
      'orders.create',
      'orders.confirm',
      'orders.complete',
      'discounts.request',
      'payments.record',
      'customers.manage',
      'loyalty.redeem',
    ],
    ['cashier'],
  ],
  [
    'Manager',
    [
      'orders.fulfill',
      'discounts.approve',
      'orders.cancel',
      'delivery.assign',
      'payments.record',
      'inventory.manage',
    ],
    ['barista', 'approvals', 'cashier', 'inventory'],
  ],
  [
    'Admin',
    [
      'users.manage',
      'orders.fulfill',
      'discounts.approve',
      'payments.record',
      'inventory.manage',
      'delivery.update',
    ],
    [
      'barista',
      'approvals',
      'cashier',
      'inventory',
      'delivery',
      'administration',
    ],
  ],
];

test.each(fixtures)(
  '%s sees only modules authorized by effective permissions',
  (_role, permissions, ids) => {
    expect(permittedModules(permissions).map((module) => module.id)).toEqual(
      ids,
    );
  },
);
test('combines multi-role permissions without duplicate modules', () => {
  expect(
    permittedModules([
      'orders.fulfill',
      'delivery.update',
      'orders.fulfill',
    ]).map((m) => m.id),
  ).toEqual(['barista', 'delivery']);
});
test('missing, unknown and role-name strings grant no access', () => {
  expect(permittedModules([])).toEqual([]);
  expect(permittedModules(['Admin', '*', 'unknown'])).toEqual([]);
  expect(findModule('unknown')).toBeUndefined();
  expect(findModule(['barista', 'delivery'])).toBeUndefined();
});
test('actions use their individual permissions, not module membership', () => {
  expect(
    permittedActions(findModule('inventory')!, ['reports.lowstock']).map(
      (a) => a.permission,
    ),
  ).toEqual(['reports.lowstock']);
  expect(
    permittedActions(findModule('approvals')!, ['discounts.approve']).map(
      (a) => a.permission,
    ),
  ).toEqual(['discounts.approve']);
  expect(permittedActions(findModule('administration')!, [])).toEqual([]);
});

test('matches all six actual backend seed bundles', () => {
  const seed = readFileSync(
    resolve(
      __dirname,
      '../../../src/CoffeeShop.Infrastructure.Postgres/Identity/StaffRoleSeed.cs',
    ),
    'utf8',
  );
  const expected: Record<string, string[]> = {
    Admin: [
      'barista',
      'approvals',
      'cashier',
      'inventory',
      'delivery',
      'administration',
    ],
    Manager: ['barista', 'approvals', 'cashier', 'inventory'],
    Cashier: ['cashier'],
    Barista: ['barista'],
    InventoryStaff: ['inventory'],
    DeliveryStaff: ['delivery'],
  };
  for (const [role, ids] of Object.entries(expected)) {
    const bundle = seed.match(
      new RegExp('\\["' + role + '"\\] = \\[([^\\]]+)\\]'),
    );
    expect(bundle).not.toBeNull();
    const permissions = [...bundle![1]!.matchAll(/"([^"]+)"/g)].map(
      (match) => match[1]!,
    );
    expect(permittedModules(permissions).map((module) => module.id)).toEqual(
      ids,
    );
  }
});
