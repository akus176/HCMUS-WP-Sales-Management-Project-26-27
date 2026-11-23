import { act, waitFor } from '@testing-library/react-native';
import { renderRouter, screen } from 'expo-router/testing-library';
import { router } from 'expo-router';
import { SessionRoutes } from '../app/_layout';
import StarterScreen from '../app/index';
import SessionScreen from '../app/session';
import ModuleScreen from '../app/modules/[module]';
import RetiredOperationsScreen from '../app/modules/cashier/operations';
import { SessionProvider } from '../src/session/context';
import { harness, json, user } from './helpers';
import { QueryCleanup } from './query-cleanup';

jest.mock('../src/realtime/OperationalUpdates', () => ({
  OperationalUpdates: () => null,
}));
test('retired administration and manager links cannot expose staff data in the delivery app', async () => {
  const h = harness(async () =>
    json({
      ...user,
      permissions: ['users.manage', 'orders.cancel', 'orders.create'],
    }),
  );
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
      session: SessionScreen,
      'modules/[module]': ModuleScreen,
      'modules/cashier/operations': RetiredOperationsScreen,
    },
    { initialUrl: '/' },
  );
  await waitFor(() => expect(h.client.getSnapshot().status).toBe('signedOut'));
  await act(async () => h.client.login('admin', 'fixture-password'));
  await screen.findByText(/Delivery access is required/);
  await act(async () => router.push('/modules/administration'));
  await screen.findByText(
    'Access unavailable. Your current permissions do not allow this module.',
  );
  await act(async () => router.push('/modules/cashier/operations'));
  await screen.findByText('This page is not available in the delivery app.');
  expect(
    h.fetcher.mock.calls.every(([request]) =>
      /\/auth\/(login|me)$/.test(new URL(request.url).pathname),
    ),
  ).toBe(true);
});
