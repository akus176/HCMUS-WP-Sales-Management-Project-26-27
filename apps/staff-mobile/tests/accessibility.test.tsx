import {
  act,
  fireEvent,
  render,
  screen,
  waitFor,
} from '@testing-library/react-native';
import { Dimensions } from 'react-native';
import { QueryClient } from '@tanstack/react-query';
import type { ReactNode } from 'react';

import LoginScreen from '../app/login';
import { PreparationScreen } from '../src/barista/PreparationScreen';
import { DiscountApprovals } from '../src/features/approvals/DiscountApprovals';
import { InventoryWorkspace } from '../src/features/inventory/InventoryWorkspace';
import { DeliveryScreen } from '../src/features/delivery/DeliveryScreen';
import { DeliveryDetail } from '../src/features/delivery/DeliveryDetail';
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
function page(items: unknown[]) {
  return { items, page: 1, pageSize: 100, totalCount: items.length };
}

const scenes: {
  name: string;
  content: ReactNode;
  loaded: string;
  open?: string;
  selected?: string;
}[] = [
  { name: 'login', content: <LoginScreen />, loaded: 'Staff sign-in' },
  { name: 'home', content: <StaffHome user={staff} />, loaded: 'Staff home' },
  {
    name: 'preparation queue',
    content: <PreparationScreen />,
    loaded: 'ORD-186 · Confirmed',
    selected: 'All active',
  },
  {
    name: 'preparation detail',
    content: <PreparationScreen />,
    loaded: 'ORD-186 · Confirmed',
    open: 'Open ticket ORD-186',
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
];

function SignedIn({ children }: { children: ReactNode }) {
  return useSession().state.user ? children : null;
}

describe.each([
  ['phone', { width: 360, height: 800, scale: 3, fontScale: 2 }],
  ['tablet', { width: 800, height: 1280, scale: 2, fontScale: 2 }],
] as const)('%s at 200%% text', (_profile, size) => {
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
          if (path.endsWith('/discounts/pending'))
            return json(page([discount]));
          if (path.endsWith('/ingredients')) return json(page([ingredient]));
          if (path.endsWith('/courier/assignments'))
            return json(page([assignment]));
          if (path.includes('/courier/assignments/')) return json(assignment);
          if (path.endsWith('/preparation-tickets'))
            return json(page([ticket]));
          if (path.includes('/preparation-tickets/')) return json(ticket);
          throw new Error('Unexpected accessibility fixture request: ' + path);
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
            <SignedIn>
              {scene.name === 'login' || scene.name === 'home' ? (
                scene.content
              ) : (
                <ShellPage>{scene.content}</ShellPage>
              )}
            </SignedIn>
          </ConnectivityProvider>
        </SessionProvider>,
      );
      await waitFor(() =>
        expect(client.getSnapshot().status).toBe('signedOut'),
      );
      await act(async () => client.login('synthetic', 'fixture-password'));
      await screen.findByText(scene.loaded);
      if (scene.open)
        await fireEvent.press(screen.getByRole('button', { name: scene.open }));
      if (scene.name === 'preparation detail')
        await screen.findByText('Status: Confirmed');
      if (scene.selected)
        expect(
          screen.getByRole('button', { name: scene.selected, selected: true }),
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
        expect(button).toHaveStyle({ minHeight: 48 });
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
});
