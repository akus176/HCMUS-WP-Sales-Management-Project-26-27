import { act, waitFor } from '@testing-library/react-native';
import { renderRouter, screen } from 'expo-router/testing-library';
import { router } from 'expo-router';
import { SessionRoutes } from '../app/_layout';
import StarterScreen from '../app/index';
import LoginScreen from '../app/login';
import SessionScreen from '../app/session';
import ModuleScreen from '../app/modules/[module]';
import DeliveryDetailRoute from '../app/modules/delivery/[orderId]';
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
const id = '00000000-0000-0000-0000-000000000186';
async function setup(permissions: string[]) {
  let effective = permissions;
  const h = harness(async (request) => {
    const path = new URL(request.url).pathname;
    if (path.endsWith('/auth/me'))
      return json({ ...user, permissions: effective });
    if (path.endsWith('/courier/assignments'))
      return json({ items: [], totalCount: 0, page: 1, pageSize: 10 });
    if (path.endsWith(`/courier/assignments/${id}`))
      return json({
        orderId: id,
        orderNumber: 'DLV-186',
        deliveryStatus: 'assigned',
        recipientName: 'Synthetic recipient',
        contactNumber: '1234567890',
        addressLine: 'Synthetic address',
      });
    throw new Error(`Unexpected protected request: ${path}`);
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
    {
      _layout: Layout,
      index: StarterScreen,
      login: LoginScreen,
      session: SessionScreen,
      'modules/[module]': ModuleScreen,
      'modules/delivery/[orderId]': DeliveryDetailRoute,
    },
    { initialUrl: `/modules/delivery/${id}` },
  );
  await waitFor(() => expect(screen).toHavePathname('/'));
  expect(h.fetcher).not.toHaveBeenCalled();
  await act(async () => h.client.login('staff', 'fixture-password'));
  await screen.findByRole('header', { name: 'Staff home' });
  return {
    ...h,
    revoke: () => {
      effective = [];
    },
  };
}
test('signed-out and non-courier deep links never load an assignment', async () => {
  const h = await setup(['orders.fulfill']);
  await act(async () => router.push(`/modules/delivery/${id}`));
  await screen.findByText(
    'Access unavailable. Your current permissions do not allow this module.',
  );
  expect(
    h.fetcher.mock.calls.some(([request]) =>
      request.url.includes('/courier/assignments'),
    ),
  ).toBe(false);
});
test('a courier may open an assigned delivery but a fresh revocation removes access', async () => {
  const h = await setup(['delivery.update']);
  await act(async () => router.push(`/modules/delivery/${id}`));
  await screen.findByText('Recipient: Synthetic recipient');
  h.revoke();
  await act(async () => router.push('/session'));
  await screen.findByText(/Delivery access is required/);
  await act(async () => router.push(`/modules/delivery/${id}`));
  await screen.findByText(
    'Access unavailable. Your current permissions do not allow this module.',
  );
  expect(screen.queryByText('Recipient: Synthetic recipient')).toBeNull();
});
test.each([
  'barista',
  'cashier',
  'inventory',
  'administration',
  'approvals',
  'operations',
])(
  'the retired %s workspace cannot load its records even for broadly privileged staff',
  async (module) => {
    const h = await setup([
      'delivery.update',
      'orders.create',
      'orders.fulfill',
      'inventory.manage',
      'users.manage',
      'orders.cancel',
    ]);
    await act(async () => router.push(`/modules/${module}`));
    await screen.findByText(
      'Access unavailable. Your current permissions do not allow this module.',
    );
    expect(
      h.fetcher.mock.calls.some(([request]) =>
        /\/(orders|ingredients|preparation-tickets|users|discounts)(\/|\?|$)/.test(
          request.url,
        ),
      ),
    ).toBe(false);
  },
);
