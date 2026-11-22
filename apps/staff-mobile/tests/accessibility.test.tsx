import {
  act,
  fireEvent,
  render,
  screen,
  waitFor,
} from '@testing-library/react-native';
import { Dimensions, StyleSheet } from 'react-native';
import { QueryClient } from '@tanstack/react-query';
import type { ReactNode } from 'react';

import LoginScreen from '../app/login';
import { PreparationScreen } from '../src/barista/PreparationScreen';
import { DiscountApprovals } from '../src/features/approvals/DiscountApprovals';
import { InventoryWorkspace } from '../src/features/inventory/InventoryWorkspace';
import { DeliveryScreen } from '../src/features/delivery/DeliveryScreen';
import { DeliveryDetail } from '../src/features/delivery/DeliveryDetail';
import { OrdersScreen } from '../src/features/orders/OrdersScreen';
import { StaffAdministration } from '../src/features/administration/StaffAdministration';
import { LowStockRecord } from '../src/notifications/LowStockRecord';
import { PermissionBoundary } from '../src/shell/PermissionBoundary';
import { ConnectivityPresentation } from '../src/connectivity/ConnectivityBanner';
import { ConnectivityProvider } from '../src/connectivity/connectivity';
import { SessionProvider, useSession } from '../src/session/context';
import { createSessionClient } from '../src/session/session';
import { StaffHome } from '../src/shell/StaffHome';
import { ShellPage } from '../src/shell/controls';
import { QueryCleanup } from './query-cleanup';
import { json, pair, user } from './helpers';

jest.mock('expo-router', () => ({
  router: { push: jest.fn(), replace: jest.fn(), back: jest.fn() },
}));
jest.mock('../src/connectivity/networkAdapter', () => ({
  getNetworkStateAsync: jest
    .fn()
    .mockResolvedValue({ isConnected: true, isInternetReachable: true }),
  addNetworkStateListener: jest.fn(() => ({ remove: jest.fn() })),
}));

const staff = {
  ...user,
  permissions: [
    'orders.fulfill',
    'discounts.approve',
    'inventory.manage',
    'inventory.adjust',
    'inventory.receive',
    'reports.lowstock',
    'delivery.update',
    'orders.create',
    'payments.record',
    'orders.cancel',
    'delivery.assign',
    'users.manage',
  ],
};
const id = '00000000-0000-0000-0000-000000000186';
const ticket = {
  orderId: id,
  orderNumber: 'ORD-186',
  status: 'confirmed',
  lines: [
    {
      itemName: 'Latte with a long preparation description',
      sizeName: 'Large',
      quantity: 2,
      addons: ['Oat milk'],
    },
  ],
};
const discount = {
  discountId: id,
  orderId: id,
  orderNumber: 'ORD-186',
  type: 'fixed',
  value: 1,
  reason: 'Service recovery',
  orderTotal: 10,
  computedAmount: 1,
};
const ingredient = {
  ingredientId: id,
  name: 'Milk',
  baseUnit: 'l',
  availableQuantity: 1,
  reorderThreshold: 3,
};
const assignment = {
  orderId: id,
  orderNumber: 'ORD-186',
  deliveryStatus: 'out_for_delivery',
  recipientName: 'Synthetic recipient',
  contactNumber: '1234567890',
  addressLine:
    'Synthetic long delivery address with required operational details',
};
const account = {
  id,
  username: 'audit-staff',
  fullName: 'Audit staff member',
  email: 'audit@example.test',
  phone: null,
  roles: ['Cashier'],
  isActive: true,
  createdAt: '2026-11-19T00:00:00Z',
  deactivatedAt: null,
};
const receipt = {
  id,
  receiptNumber: 'RCPT-219',
  supplierId: id,
  status: 'draft',
  postedAt: null,
  lines: [{ id, ingredientId: id, quantity: 4, unitCost: 2 }],
};
const order = {
  id,
  orderNumber: 'ORD-219',
  status: 'ready',
  channel: 'delivery',
  total: 10,
  paidAmount: 0,
  tableNumber: null,
  assignedStaffId: null,
  deliveryStatus: 'unassigned',
  pendingDiscountIds: [],
};
const orderDetail = {
  ...order,
  subtotal: 10,
  discountTotal: 0,
  tableId: null,
  customerId: null,
  discounts: [],
  lines: [
    {
      itemName: 'Audit latte',
      sizeName: 'Large',
      quantity: 2,
      unitPrice: 5,
      total: 10,
      addons: [],
    },
  ],
};
function page(items: unknown[]) {
  return { items, page: 1, pageSize: 100, totalCount: items.length };
}

const scenes: {
  name: string;
  content: ReactNode;
  loaded: string;
  open?: string | string[];
  entered?: { label: string; value: string }[];
  afterEntry?: string;
  settled?: string;
  selected?: string;
}[] = [
  { name: 'login', content: <LoginScreen />, loaded: 'Staff sign-in' },
  { name: 'home', content: <StaffHome user={staff} />, loaded: 'Staff home' },
  {
    name: 'preparation queue',
    content: <PreparationScreen />,
    loaded: 'ORD-186',
    selected: 'All active',
  },
  {
    name: 'preparation detail',
    content: <PreparationScreen />,
    loaded: 'ORD-186',
    open: ['Select ticket ORD-186', 'Open ORD-186'],
  },
  {
    name: 'preparation ready confirmation',
    content: <PreparationScreen ticketId={id} />,
    loaded: 'ORD-186',
    open: 'Mark ready',
    settled: 'This updates the whole ticket.',
  },
  {
    name: 'approval inbox',
    content: <DiscountApprovals user={staff} />,
    loaded: 'Reason: Service recovery',
  },
  {
    name: 'approval detail',
    content: <DiscountApprovals user={staff} discountId={id} />,
    loaded: 'Reason: Service recovery',
  },
  {
    name: 'inventory balances',
    content: <InventoryWorkspace user={staff} />,
    loaded: 'Milk',
    selected: 'Ingredient balances',
  },
  {
    name: 'inventory adjustment',
    content: <InventoryWorkspace user={staff} />,
    loaded: 'Milk',
    open: 'Record adjustment for Milk',
  },
  {
    name: 'delivery list',
    content: <DeliveryScreen />,
    loaded: 'ORD-186 · out for delivery',
    selected: 'Active deliveries',
  },
  {
    name: 'delivery detail',
    content: <DeliveryDetail orderId={id} />,
    loaded: 'Address: ' + assignment.addressLine,
  },
  {
    name: 'counter search',
    content: <OrdersScreen user={staff} />,
    loaded: 'ORD-219 · ready · delivery',
  },
  {
    name: 'counter filters',
    content: <OrdersScreen user={staff} />,
    loaded: 'ORD-219 · ready · delivery',
    open: 'Filter orders: All',
    selected: 'Show all orders',
  },
  {
    name: 'payment form',
    content: <OrdersScreen user={staff} />,
    loaded: 'ORD-219 · ready · delivery',
    open: 'Open order ORD-219',
    settled: 'Record payment',
    selected: 'Pay by cash',
  },
  {
    name: 'payment review',
    content: <OrdersScreen user={staff} />,
    loaded: 'ORD-219 · ready · delivery',
    open: 'Open order ORD-219',
    settled: 'Record payment',
    entered: [{ label: 'Payment amount', value: '5' }],
    afterEntry: 'Review payment',
  },
  {
    name: 'manager order and cancellation form',
    content: <OrdersScreen user={staff} manager />,
    loaded: 'ORD-219 · ready · delivery',
    open: 'Open order ORD-219',
    settled: 'Authorize cancellation',
  },
  {
    name: 'cancellation review',
    content: <OrdersScreen user={staff} manager />,
    loaded: 'ORD-219 · ready · delivery',
    open: 'Open order ORD-219',
    settled: 'Authorize cancellation',
    entered: [{ label: 'Cancellation reason', value: 'Customer request' }],
    afterEntry: 'Review cancellation',
  },
  {
    name: 'delivery assignment review',
    content: <OrdersScreen user={staff} manager />,
    loaded: 'ORD-219 · ready · delivery',
    open: ['Open order ORD-219', 'Select Delivery colleague'],
    afterEntry: 'Review assignment',
  },
  {
    name: 'low stock',
    content: <InventoryWorkspace user={staff} />,
    loaded: 'Milk',
    open: 'Low stock',
    selected: 'Low stock',
  },
  {
    name: 'receipt list',
    content: <InventoryWorkspace user={staff} />,
    loaded: 'Milk',
    open: 'Purchase receipts',
    settled: 'RCPT-219',
    selected: 'Purchase receipts',
  },
  {
    name: 'receipt detail',
    content: <InventoryWorkspace user={staff} />,
    loaded: 'Milk',
    open: ['Purchase receipts', 'Review receipt RCPT-219'],
    settled: 'Receipt RCPT-219',
  },
  {
    name: 'movement history',
    content: <InventoryWorkspace user={staff} />,
    loaded: 'Milk',
    open: 'Movement history',
    settled: 'No stock movements are recorded.',
    selected: 'Movement history',
  },
  {
    name: 'staff accounts',
    content: <StaffAdministration user={staff} />,
    loaded: 'Audit staff member',
    selected: 'Staff accounts',
  },
  {
    name: 'account detail',
    content: <StaffAdministration user={staff} />,
    loaded: 'Audit staff member',
    open: 'View details for Audit staff member',
    settled: 'Account details',
  },
  {
    name: 'security events',
    content: <StaffAdministration user={staff} />,
    loaded: 'Audit staff member',
    open: 'Security events',
    settled: 'No security events match this filter.',
    selected: 'Security events',
  },
  {
    name: 'low-stock notification',
    content: <LowStockRecord id={id} />,
    loaded: 'Available: 1 l',
  },
  {
    name: 'permission denied',
    content: (
      <PermissionBoundary moduleId="unknown">{() => null}</PermissionBoundary>
    ),
    loaded: 'Access unavailable. This module is not available.',
  },
];

function SignedIn({ children }: { children: ReactNode }) {
  return useSession().state.user ? children : null;
}

describe.each([
  ['phone normal text', { width: 360, height: 800, scale: 3, fontScale: 1 }],
  ['tablet normal text', { width: 800, height: 1280, scale: 2, fontScale: 1 }],
  ['phone', { width: 360, height: 800, scale: 3, fontScale: 2 }],
  ['tablet', { width: 800, height: 1280, scale: 2, fontScale: 2 }],
] as const)(
  '%s semantics (font scale supplied by profile)',
  (_profile, size) => {
    const original = Dimensions.get('window');
    beforeEach(() => Dimensions.set({ window: size, screen: size }));
    afterEach(() => Dimensions.set({ window: original, screen: original }));

    test.each(scenes)(
      '$name keeps scalable content, named actions and a scrollable layout',
      async (scene) => {
        const client = createSessionClient({
          origin: 'https://staff.example',
          storage: {
            get: async () => null,
            set: async () => {},
            remove: async () => {},
          },
          clearData: () => {},
          fetch: (async (request: Request) => {
            const path = new URL(request.url).pathname;
            if (path.endsWith('/auth/login'))
              return json({ ...pair(), user: staff });
            if (path.endsWith('/auth/me')) return json(staff);
            if (path.endsWith('/users')) return json([account]);
            if (path.includes('/users/')) return json(account);
            if (path.endsWith('/security-events')) return json([]);
            if (path.endsWith('/delivery-staff/options'))
              return json([{ id, fullName: 'Delivery colleague' }]);
            if (path.endsWith('/orders')) return json([order]);
            if (path.includes('/orders/')) return json(orderDetail);
            if (path.endsWith('/pos/tables')) return json([]);
            if (path.endsWith('/purchase-receipts')) return json([receipt]);
            if (path.includes('/purchase-receipts/')) return json(receipt);
            if (
              path.endsWith('/stock-movements') ||
              path.endsWith('/movements')
            )
              return json([]);
            if (path.endsWith('/ingredients/low-stock'))
              return json(page([ingredient]));
            if (path.includes('/ingredients/'))
              return json({ ...ingredient, isActive: true });
            if (path.endsWith('/discounts/pending'))
              return json(page([discount]));
            if (path.endsWith('/ingredients')) return json(page([ingredient]));
            if (path.endsWith('/courier/assignments'))
              return json(page([assignment]));
            if (path.includes('/courier/assignments/')) return json(assignment);
            if (path.endsWith('/preparation-tickets'))
              return json(page([ticket]));
            if (path.includes('/preparation-tickets/'))
              return json(
                scene.name === 'preparation ready confirmation'
                  ? { ...ticket, status: 'preparing' }
                  : ticket,
              );
            throw new Error(
              'Unexpected accessibility fixture request: ' + path,
            );
          }) as typeof fetch,
        });
        const view = await render(
          <SessionProvider
            client={client}
            queryClient={
              new QueryClient({
                defaultOptions: { queries: { retry: false, gcTime: 0 } },
              })
            }
          >
            <QueryCleanup />
            <ConnectivityProvider>
              <ConnectivityPresentation
                branded={scene.name.startsWith('preparation')}
              >
                <SignedIn>
                  {scene.name === 'login' || scene.name === 'home' ? (
                    scene.content
                  ) : (
                    <ShellPage>{scene.content}</ShellPage>
                  )}
                </SignedIn>
              </ConnectivityPresentation>
            </ConnectivityProvider>
          </SessionProvider>,
        );
        await waitFor(() =>
          expect(client.getSnapshot().status).toBe('signedOut'),
        );
        await act(async () => client.login('synthetic', 'fixture-password'));
        await screen.findByText(scene.loaded);
        for (const action of scene.open
          ? Array.isArray(scene.open)
            ? scene.open
            : [scene.open]
          : [])
          await fireEvent.press(
            await screen.findByRole('button', { name: action }),
          );
        if (scene.settled) await screen.findByText(scene.settled);
        for (const entry of scene.entered ?? [])
          await fireEvent.changeText(
            await screen.findByLabelText(entry.label),
            entry.value,
          );
        if (scene.afterEntry)
          await fireEvent.press(
            await screen.findByRole('button', { name: scene.afterEntry }),
          );
        if (scene.name === 'preparation detail')
          await screen.findByLabelText('Status: Confirmed');
        if (scene.selected)
          expect(
            screen.getByRole('button', {
              name: scene.selected,
              selected: true,
            }),
          ).toBeVisible();
        expect(
          screen.container.queryAll((node) => node.type === 'RCTScrollView')
            .length,
        ).toBeGreaterThan(0);
        const texts = screen.container.queryAll((node) => node.type === 'Text');
        expect(texts.length).toBeGreaterThan(0);
        for (const text of texts) {
          expect(text.props.allowFontScaling).not.toBe(false);
          expect(text.props.numberOfLines).toBeUndefined();
        }
        for (const button of screen.getAllByRole('button')) {
          expect(button.props.accessibilityLabel?.trim()).toBeTruthy();
          expect(
            StyleSheet.flatten(button.props.style).minHeight,
          ).toBeGreaterThanOrEqual(48);
        }
        for (const input of screen.container.queryAll(
          (node) => node.type === 'TextInput',
        )) {
          expect(input.props.accessibilityLabel?.trim()).toBeTruthy();
          expect(input.props.allowFontScaling).not.toBe(false);
        }
        await view.unmount();
      },
    );
  },
);
