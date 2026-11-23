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
const listeners = new Set<(state: AppStateStatus) => void>();
beforeEach(() => {
  listeners.clear();
  jest
    .spyOn(AppState, 'addEventListener')
    .mockImplementation((_event, listener) => {
      listeners.add(listener);
      return {
        remove: () => {
          listeners.delete(listener);
        },
      };
    });
});
const job = {
  orderId: '00000000-0000-0000-0000-000000000186',
  orderNumber: 'DLV-186',
  deliveryStatus: 'out_for_delivery',
};
async function setup(permissions = ['delivery.update']) {
  let effective = permissions;
  const h = harness(async (request) => {
    const path = new URL(request.url).pathname;
    if (path.endsWith('/auth/me'))
      return json({ ...user, permissions: effective });
    if (path.endsWith('/courier/assignments'))
      return json({ items: [job], totalCount: 1, page: 1, pageSize: 10 });
    throw new Error(`Unexpected app request: ${path}`);
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
  await act(async () => h.client.login('courier', 'fixture-password'));
  await screen.findByRole('header', { name: 'Staff home' });
  await screen.findAllByRole('tab');
  return {
    ...h,
    revoke: () => {
      effective = [];
    },
  };
}

test('the mobile shell exposes only delivery work even when an account has other staff permissions', async () => {
  const h = await setup([
    'delivery.update',
    'orders.fulfill',
    'orders.create',
    'inventory.manage',
    'users.manage',
    'discounts.approve',
  ]);
  await screen.findByText('DLV-186');
  expect(
    screen.getAllByRole('tab').map((tab) => tab.props.accessibilityLabel),
  ).toEqual(['Today', 'Deliveries', 'More']);
  for (const label of ['Preparation', 'Orders', 'Stock', 'Staff'])
    expect(screen.queryByRole('tab', { name: label })).toBeNull();
  await fireEvent.press(screen.getByRole('tab', { name: 'More' }));
  expect(
    screen
      .getAllByRole('button')
      .map((button) => button.props.accessibilityLabel),
  ).toEqual(['Open Account']);
  await fireEvent.press(screen.getByRole('button', { name: 'Open Account' }));
  expect(
    screen.queryByRole('button', { name: 'Open Administration' }),
  ).toBeNull();
  expect(
    h.fetcher.mock.calls.some(([request]) =>
      /\/(orders|ingredients|preparation-tickets|users|discounts)(\/|\?|$)/.test(
        request.url,
      ),
    ),
  ).toBe(false);
});

test('Today and active Deliveries share one loaded assignment page and warm navigation sends no requests', async () => {
  const h = await setup();
  await screen.findByText('DLV-186');
  const loaded = h.fetcher.mock.calls.length;
  for (let pass = 0; pass < 3; pass++) {
    await fireEvent.press(screen.getByRole('tab', { name: 'Deliveries' }));
    await screen.findByRole('button', { name: 'Open delivery DLV-186' });
    await fireEvent.press(screen.getByRole('tab', { name: 'More' }));
    await screen.findByRole('button', { name: 'Open Account' });
    await fireEvent.press(screen.getByRole('tab', { name: 'Today' }));
    await screen.findByText('DLV-186');
  }
  expect(h.fetcher).toHaveBeenCalledTimes(loaded);
  await fireEvent.press(
    screen.getByRole('button', { name: 'Refresh delivery overview' }),
  );
  await waitFor(() =>
    expect(h.fetcher.mock.calls.length).toBeGreaterThan(loaded),
  );
});

test('delivery access removal hides retained assignments and prevents additional protected reads', async () => {
  const h = await setup();
  await screen.findByText('DLV-186');
  const before = h.fetcher.mock.calls.filter(([request]) =>
    request.url.includes('/courier/assignments'),
  ).length;
  h.revoke();
  await act(async () => {
    for (const listener of listeners) listener('background');
  });
  await act(async () => {
    for (const listener of listeners) listener('active');
  });
  await screen.findByText(/Delivery access is required/);
  expect(screen.queryByRole('tab', { name: 'Deliveries' })).toBeNull();
  expect(screen.queryByText('DLV-186')).toBeNull();
  expect(
    h.fetcher.mock.calls.filter(([request]) =>
      request.url.includes('/courier/assignments'),
    ),
  ).toHaveLength(before);
});

test('non-courier staff can sign out without loading delivery records', async () => {
  const h = await setup(['orders.fulfill', 'inventory.manage']);
  await screen.findByText(/Delivery access is required/);
  expect(screen.queryByRole('tab', { name: 'Deliveries' })).toBeNull();
  await fireEvent.press(screen.getByRole('tab', { name: 'More' }));
  await fireEvent.press(screen.getByRole('button', { name: 'Open Account' }));
  await fireEvent.press(screen.getByRole('button', { name: 'Sign out' }));
  await fireEvent.press(screen.getByRole('button', { name: 'Stay signed in' }));
  expect(h.client.getSnapshot().status).toBe('signedIn');
  expect(
    h.fetcher.mock.calls.some(([request]) =>
      request.url.includes('/courier/assignments'),
    ),
  ).toBe(false);
});
