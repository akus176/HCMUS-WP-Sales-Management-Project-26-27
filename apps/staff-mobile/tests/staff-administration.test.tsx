import { act, waitFor } from '@testing-library/react-native';
import { renderRouter, screen } from 'expo-router/testing-library';
import { router } from 'expo-router';
import { SessionRoutes } from '../app/_layout';
import StarterScreen from '../app/index';
import SessionScreen from '../app/session';
import ModuleScreen from '../app/modules/[module]';
import { SessionProvider } from '../src/session/context';
import { harness, json, user } from './helpers';
import { QueryCleanup } from './query-cleanup';

jest.mock('../src/realtime/OperationalUpdates', () => ({
  OperationalUpdates: () => null,
}));
test('staff administration remains outside the Counter and Preparation mobile app', async () => {
  const h = harness(async () =>
    json({ ...user, roles: ['Admin'], permissions: ['users.manage'] }),
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
    },
    { initialUrl: '/' },
  );
  await waitFor(() => expect(h.client.getSnapshot().status).toBe('signedOut'));
  await act(async () => h.client.login('admin', 'fixture-password'));
  await screen.findByRole('header', { name: 'Staff home' });
  expect(
    screen.queryByRole('button', { name: 'Open Administration' }),
  ).toBeNull();
  await act(async () => router.push('/modules/administration'));
  await screen.findByText('Access unavailable. This module is not available.');
  expect(
    h.fetcher.mock.calls.some(([request]) =>
      /\/(users|security-events)(\/|\?|$)/.test(request.url),
    ),
  ).toBe(false);
});
