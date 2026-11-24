import { act, fireEvent, waitFor } from '@testing-library/react-native';
import { AppState, type AppStateStatus } from 'react-native';
import { renderRouter, screen } from 'expo-router/testing-library';
import { SessionRoutes } from '../app/_layout';
import StarterScreen from '../app/index';
import SessionScreen from '../app/session';
import { SessionProvider } from '../src/session/context';
import { harness, json, user } from './helpers';
import { QueryCleanup } from './query-cleanup';

jest.mock('../src/realtime/OperationalUpdates', () => ({
  OperationalUpdates: () => null,
}));
jest.mock('../src/connectivity/networkAdapter', () => ({
  getNetworkStateAsync: jest
    .fn()
    .mockResolvedValue({ isConnected: true, isInternetReachable: true }),
  addNetworkStateListener: jest.fn(() => ({ remove: jest.fn() })),
}));

const appStateListeners = new Set<(state: AppStateStatus) => void>();
beforeEach(() => {
  appStateListeners.clear();
  jest
    .spyOn(AppState, 'addEventListener')
    .mockImplementation((_event, listener) => {
      appStateListeners.add(listener);
      return {
        remove: () => {
          appStateListeners.delete(listener);
        },
      };
    });
});

const grants = [
  'orders.create',
  'payments.record',
  'inventory.manage',
  'orders.fulfill',
  'discounts.approve',
  'users.manage',
  'delivery.update',
];
async function setup(permissions = grants) {
  let effective = permissions;
  const h = harness(async (request) => {
    const path = new URL(request.url).pathname;
    if (path.endsWith('/auth/me'))
      return json({ ...user, permissions: effective });
    if (path.endsWith('/orders') || path.endsWith('/pos/tables'))
      return json([]);
    if (path.endsWith('/preparation-tickets'))
      return json({ items: [], page: 1, pageSize: 10, totalCount: 0 });
    if (path.endsWith('/ingredients'))
      return json({
        items: [
          {
            ingredientId: 'oat-milk',
            name: 'Oat milk',
            baseUnit: 'L',
            availableQuantity: 4,
            reorderThreshold: 6,
          },
        ],
        page: 1,
        pageSize: 100,
        totalCount: 1,
      });
    if (path.endsWith('/discounts/pending'))
      return json({ items: [], page: 1, pageSize: 100, totalCount: 0 });
    return json([]);
  });
  function Layout() {
    return (
      <SessionProvider client={h.client}>
        <QueryCleanup />
        <SessionRoutes />
      </SessionProvider>
    );
  }
  await renderRouter(
    { _layout: Layout, index: StarterScreen, session: SessionScreen },
    { initialUrl: '/' },
  );
  await waitFor(() => expect(h.client.getSnapshot().status).toBe('signedOut'));
  await act(async () => h.client.login('synthetic', 'fixture-password'));
  await screen.findByRole('header', { name: 'Staff home' });
  await screen.findAllByRole('tab');
  return {
    ...h,
    revoke: () => {
      effective = [];
    },
  };
}

test('Counter and Preparation are the only work tabs and More contains only Account', async () => {
  const h = await setup();
  expect(
    screen.getAllByRole('tab').map((tab) => tab.props.accessibilityLabel),
  ).toEqual(['Today', 'Counter', 'Preparation', 'More']);
  await fireEvent.press(screen.getByRole('tab', { name: 'Counter' }));
  await screen.findByLabelText('Search menu');
  await fireEvent.press(screen.getByRole('button', { name: 'Orders' }));
  const search = await screen.findByLabelText('Search orders');
  await fireEvent.changeText(search, 'T04');
  await fireEvent.press(screen.getByRole('tab', { name: 'Preparation' }));
  await screen.findByRole('button', { name: 'All active' });
  expect(screen.queryByLabelText('Search orders')).toBeNull();
  expect(screen.queryByRole('button', { name: 'Tables' })).toBeNull();
  await fireEvent.press(screen.getByRole('tab', { name: 'Counter' }));
  expect(await screen.findByLabelText('Search orders')).toHaveProp(
    'value',
    'T04',
  );
  await fireEvent.press(screen.getByRole('button', { name: 'Tables' }));
  await screen.findByText('Table occupancy');
  await fireEvent.press(screen.getByRole('button', { name: 'Orders' }));
  expect(await screen.findByLabelText('Search orders')).toHaveProp(
    'value',
    'T04',
  );
  await fireEvent.press(screen.getByRole('tab', { name: 'More' }));
  expect(
    await screen.findByRole('button', { name: 'Open Account' }),
  ).toBeVisible();
  expect(screen.queryByRole('button', { name: 'Open Preparation' })).toBeNull();
  expect(screen.queryByRole('button', { name: 'Open Approvals' })).toBeNull();
  expect(screen.queryByRole('tab', { name: 'Stock' })).toBeNull();
  expect(screen.queryByRole('tab', { name: 'Deliveries' })).toBeNull();
  expect(
    screen.queryByRole('button', { name: 'Open Administration' }),
  ).toBeNull();
  expect(
    h.fetcher.mock.calls.some(([request]) =>
      request.url.includes('/discounts/pending'),
    ),
  ).toBe(false);
  await fireEvent.press(screen.getByRole('tab', { name: 'Preparation' }));
  await screen.findByRole('button', { name: 'All active' });
  expect(
    screen.getAllByRole('tab').map((tab) => tab.props.accessibilityLabel),
  ).toEqual(['Today', 'Counter', 'Preparation', 'More']);
  expect(
    screen.getByRole('tab', { name: 'Preparation', selected: true }),
  ).toBeVisible();
  expect(
    h.fetcher.mock.calls.filter(
      ([r]) => r.method === 'POST' && !r.url.endsWith('/auth/login'),
    ),
  ).toHaveLength(0);
});

test('a fresh revocation removes cached workspaces and never reloads their records', async () => {
  const listeners = appStateListeners;
  const h = await setup(['orders.fulfill']);
  expect(
    screen.getAllByRole('tab').map((tab) => tab.props.accessibilityLabel),
  ).toEqual(['Today', 'Preparation', 'More']);
  await fireEvent.press(screen.getByRole('tab', { name: 'Preparation' }));
  await screen.findByRole('button', { name: 'All active' });
  await fireEvent.press(screen.getByRole('tab', { name: 'Today' }));
  await screen.findByRole('header', { name: 'Staff home' });
  const before = h.fetcher.mock.calls.filter(([r]) =>
    r.url.includes('/preparation-tickets'),
  ).length;
  h.revoke();
  await act(async () => {
    listeners.forEach((listener) => listener('background'));
  });
  await act(async () => {
    listeners.forEach((listener) => listener('active'));
  });
  await screen.findByText(
    'No mobile modules are available for your permissions.',
  );
  expect(screen.queryByRole('tab', { name: 'Preparation' })).toBeNull();
  expect(screen.queryByRole('button', { name: 'All active' })).toBeNull();
  expect(
    h.fetcher.mock.calls.filter(([r]) =>
      r.url.includes('/preparation-tickets'),
    ),
  ).toHaveLength(before);
});

test('sign-out can be canceled in Account without losing the active session', async () => {
  const h = await setup(['orders.fulfill']);
  await fireEvent.press(screen.getByRole('tab', { name: 'More' }));
  await fireEvent.press(
    await screen.findByRole('button', { name: 'Open Account' }),
  );
  await fireEvent.press(
    await screen.findByRole('button', { name: 'Sign out' }),
  );
  await fireEvent.press(screen.getByRole('button', { name: 'Stay signed in' }));
  expect(h.client.getSnapshot().status).toBe('signedIn');
  expect(screen.queryByRole('button', { name: 'Confirm sign out' })).toBeNull();
  expect(
    h.fetcher.mock.calls.some(([r]) => r.url.endsWith('/auth/logout')),
  ).toBe(false);
});

test('Counter and Preparation retain their data across warm tab switches and manual refresh still works', async () => {
  const h = await setup();
  await fireEvent.press(screen.getByRole('tab', { name: 'Counter' }));
  await screen.findByLabelText('Search menu');
  await fireEvent.press(screen.getByRole('button', { name: 'Orders' }));
  await screen.findByLabelText('Search orders');
  await fireEvent.press(screen.getByRole('tab', { name: 'Preparation' }));
  await screen.findByRole('button', { name: 'All active' });
  await fireEvent.press(screen.getByRole('tab', { name: 'Today' }));
  await screen.findByRole('header', { name: 'Staff home' });
  const loaded = h.fetcher.mock.calls.length;
  for (let pass = 0; pass < 3; pass++) {
    await fireEvent.press(screen.getByRole('tab', { name: 'Counter' }));
    await screen.findByLabelText('Search orders');
    await fireEvent.press(screen.getByRole('tab', { name: 'Preparation' }));
    await screen.findByRole('button', { name: 'All active' });
    await fireEvent.press(screen.getByRole('tab', { name: 'Today' }));
    await screen.findByRole('header', { name: 'Staff home' });
  }
  expect(h.fetcher).toHaveBeenCalledTimes(loaded);
  await fireEvent.press(screen.getByRole('tab', { name: 'Counter' }));
  await fireEvent.press(
    screen.getByRole('button', { name: 'Refresh orders and tables' }),
  );
  await waitFor(() =>
    expect(h.fetcher.mock.calls.length).toBeGreaterThan(loaded),
  );
});
