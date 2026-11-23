import { act, fireEvent, waitFor } from '@testing-library/react-native';
import { renderRouter, screen } from 'expo-router/testing-library';
import { router } from 'expo-router';

import { SessionRoutes } from '../app/_layout';
import StarterScreen from '../app/index';
import LoginScreen from '../app/login';
import SessionScreen from '../app/session';
import { SessionProvider } from '../src/session/context';
import { harness, json, user } from './helpers';

jest.mock('../src/realtime/OperationalUpdates', () => ({
  OperationalUpdates: () => null,
}));

test('logout removes protected navigation history and rejects a stale session deep link', async () => {
  const h = harness(async () => json(user));
  function Layout() {
    return (
      <SessionProvider client={h.client}>
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
    },
    { initialUrl: '/session' },
  );
  await waitFor(() => expect(screen).toHavePathname('/'));
  expect(screen.queryByText('Test Staff')).toBeNull();
  await act(async () => {
    await h.client.login('staff', 'fixture-password');
  });
  expect(await screen.findByText('Test Staff')).toBeVisible();
  expect(screen).toHavePathname('/session');
  await fireEvent.press(screen.getByRole('tab', { name: 'Account' }));
  await fireEvent.press(
    await screen.findByRole('button', { name: 'Sign out' }),
  );
  await fireEvent.press(
    screen.getByRole('button', { name: 'Confirm sign out' }),
  );
  await waitFor(() => expect(screen).toHavePathname('/'));
  expect(router.canGoBack()).toBe(false);
  await act(async () => {
    router.push('/session');
  });
  await waitFor(() => expect(screen).toHavePathname('/'));
  expect(screen.queryByText('Test Staff')).toBeNull();
  if (router.canGoBack())
    await act(async () => {
      router.back();
    });
  await waitFor(() => expect(screen).toHavePathname('/'));
  expect(screen.queryByText('Test Staff')).toBeNull();
});

test('terminal refresh rejection removes a protected screen and retained navigation', async () => {
  let identityChecks = 0;
  const h = harness(async (request) =>
    request.url.endsWith('/auth/me') && ++identityChecks === 1
      ? json(user)
      : json({}, 401),
  );
  function Layout() {
    return (
      <SessionProvider client={h.client}>
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
    },
    { initialUrl: '/' },
  );
  await waitFor(() => expect(h.client.getSnapshot().status).toBe('signedOut'));
  await act(async () => {
    await h.client.login('staff', 'fixture-password');
  });
  expect(await screen.findByText('Test Staff')).toBeVisible();
  await act(async () => {
    await h.client.api.GET('/api/v1/auth/me').catch(() => {});
  });
  await waitFor(() => expect(screen).toHavePathname('/'));
  expect(screen.queryByText('Test Staff')).toBeNull();
  expect(router.canGoBack()).toBe(false);
});

test('home access failure keeps retry and sign-out inside the scrollable page', async () => {
  const h = harness(async () => json({}, 500));
  function Layout() {
    return (
      <SessionProvider client={h.client}>
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
    },
    { initialUrl: '/' },
  );
  await waitFor(() => expect(h.client.getSnapshot().status).toBe('signedOut'));
  await act(async () => {
    await h.client.login('staff', 'fixture-password');
  });
  await screen.findByText('Unable to verify access. Reconnect and try again.');
  expect(
    screen.getByRole('button', { name: 'Retry access check' }),
  ).toBeEnabled();
  expect(screen.getByRole('button', { name: 'Sign out' })).toBeEnabled();
  expect(
    screen.container.queryAll((node) => node.type === 'RCTScrollView').length,
  ).toBeGreaterThan(0);
});
