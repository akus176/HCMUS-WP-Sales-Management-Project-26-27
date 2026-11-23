import { act, fireEvent, waitFor } from '@testing-library/react-native';
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
  await screen.findByRole('tab', {
    name: permissions.length > 2 ? 'Today' : 'Home',
  });
  return {
    ...h,
    revoke: () => {
      effective = [];
    },
  };
}

test('manager tabs preserve order search across workspaces and expose overflow work without switching identity', async () => {
  const h = await setup();
  expect(
    screen.getAllByRole('tab').map((tab) => tab.props.accessibilityLabel),
  ).toEqual(['Today', 'Orders', 'Stock', 'More']);
  await fireEvent.press(screen.getByRole('tab', { name: 'Orders' }));
  const search = await screen.findByLabelText('Search orders');
  await fireEvent.changeText(search, 'T04');
  await fireEvent.press(screen.getByRole('tab', { name: 'Stock' }));
  await screen.findByLabelText('Search ingredients');
  expect(screen.queryByLabelText('Search orders')).toBeNull();
  expect(screen.queryByRole('button', { name: 'Tables' })).toBeNull();
  await fireEvent.press(screen.getByRole('tab', { name: 'Orders' }));
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
  await fireEvent.press(
    await screen.findByRole('button', { name: 'Open Preparation' }),
  );
  await screen.findByRole('button', { name: 'All active' });
  expect(
    screen.getAllByRole('tab').map((tab) => tab.props.accessibilityLabel),
  ).toEqual(['Today', 'Orders', 'Stock', 'More']);
  expect(
    screen.getByRole('tab', { name: 'More', selected: true }),
  ).toBeVisible();
  expect(
    h.fetcher.mock.calls.filter(
      ([r]) => r.method === 'POST' && !r.url.endsWith('/auth/login'),
    ),
  ).toHaveLength(0);
});

test('a fresh revocation removes cached workspaces and never reloads their records', async () => {
  const h = await setup(['orders.fulfill']);
  expect(
    screen.getAllByRole('tab').map((tab) => tab.props.accessibilityLabel),
  ).toEqual(['Home', 'Queue', 'Account']);
  await fireEvent.press(screen.getByRole('tab', { name: 'Queue' }));
  await screen.findByRole('button', { name: 'All active' });
  await fireEvent.press(screen.getByRole('tab', { name: 'Home' }));
  await screen.findByRole('header', { name: 'Staff home' });
  const before = h.fetcher.mock.calls.filter(([r]) =>
    r.url.includes('/preparation-tickets'),
  ).length;
  h.revoke();
  await fireEvent.press(screen.getByRole('tab', { name: 'Queue' }));
  await screen.findByText(
    'No mobile modules are available for your permissions.',
  );
  expect(screen.queryByRole('tab', { name: 'Queue' })).toBeNull();
  expect(screen.queryByRole('button', { name: 'All active' })).toBeNull();
  expect(
    h.fetcher.mock.calls.filter(([r]) =>
      r.url.includes('/preparation-tickets'),
    ),
  ).toHaveLength(before);
});

test('sign-out can be canceled in Account without losing the active session', async () => {
  const h = await setup(['orders.fulfill']);
  await fireEvent.press(screen.getByRole('tab', { name: 'Account' }));
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

test('native inventory keeps its focused ingredient and search while canceling a reviewed adjustment sends no write', async () => {
  const h = await setup(['inventory.manage', 'inventory.adjust']);
  await fireEvent.press(screen.getByRole('tab', { name: 'Stock' }));
  await fireEvent.changeText(
    await screen.findByLabelText('Search ingredients'),
    'Oat',
  );
  await fireEvent.press(
    await screen.findByRole('button', { name: 'Open ingredient Oat milk' }),
  );
  await fireEvent.press(
    await screen.findByRole('button', {
      name: 'Record adjustment for Oat milk',
    }),
  );
  await fireEvent.changeText(screen.getByLabelText('Quantity change'), '2');
  await fireEvent.changeText(
    screen.getByLabelText('Adjustment reason'),
    'Physical count',
  );
  await fireEvent.press(
    screen.getByRole('button', { name: 'Save adjustment' }),
  );
  expect(
    await screen.findByRole('button', { name: 'Confirm adjustment' }),
  ).toBeVisible();
  await fireEvent.press(
    screen.getByRole('button', { name: 'Close Review stock adjustment' }),
  );
  await fireEvent.press(screen.getByRole('tab', { name: 'Home' }));
  await screen.findByRole('header', { name: 'Staff home' });
  await fireEvent.press(screen.getByRole('tab', { name: 'Stock' }));
  await fireEvent.press(
    await screen.findByRole('button', { name: 'Back to ingredients' }),
  );
  expect(await screen.findByLabelText('Search ingredients')).toHaveProp(
    'value',
    'Oat',
  );
  expect(
    h.fetcher.mock.calls.filter(
      ([r]) => r.method === 'POST' && !r.url.endsWith('/auth/login'),
    ),
  ).toHaveLength(0);
});
