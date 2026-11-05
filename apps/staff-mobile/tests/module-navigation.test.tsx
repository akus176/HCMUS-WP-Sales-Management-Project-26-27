import { act, fireEvent, waitFor } from '@testing-library/react-native';
import { renderRouter, screen } from 'expo-router/testing-library';
import { router } from 'expo-router';

import { SessionRoutes } from '../app/_layout';
import StarterScreen from '../app/index';
import LoginScreen from '../app/login';
import SessionScreen from '../app/session';
import ModuleScreen from '../app/modules/[module]';
import { SessionProvider } from '../src/session/context';
import { harness, json, user } from './helpers';

test('real routes reject signed-out and unauthorized links, allow a grant, then reject revocation', async () => {
  let permissions = user.permissions;
  const h = harness(async () => json({ ...user, permissions }));
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
      'modules/[module]': ModuleScreen,
    },
    { initialUrl: '/modules/barista' },
  );
  await waitFor(() => expect(screen).toHavePathname('/'));
  expect(h.fetcher).not.toHaveBeenCalled();
  await act(async () => {
    await h.client.login('staff', 'fixture-password');
  });
  await screen.findByRole('header', { name: 'Staff home' });
  await act(async () => {
    router.push('/modules/approvals');
  });
  await screen.findByText(
    'Access unavailable. Your current permissions do not allow this module.',
  );
  expect(screen.queryByRole('header', { name: 'Approvals' })).toBeNull();
  await fireEvent.press(
    screen.getByRole('button', { name: 'Back to staff home' }),
  );
  await fireEvent.press(
    await screen.findByRole('button', { name: 'Open Preparation' }),
  );
  expect(
    await screen.findByRole('header', { name: 'Preparation' }),
  ).toBeVisible();
  expect(screen.queryByText('Record payments — planned')).toBeNull();
  permissions = [];
  await fireEvent.press(
    screen.getByRole('button', { name: 'Back to staff home' }),
  );
  await screen.findByText(
    'No mobile modules are available for your permissions.',
  );
  expect(screen.queryByRole('button', { name: 'Open Preparation' })).toBeNull();
  await act(async () => {
    router.push('/modules/barista');
  });
  await screen.findByText(
    'Access unavailable. Your current permissions do not allow this module.',
  );
  expect(screen.queryByRole('header', { name: 'Preparation' })).toBeNull();
  // Navigation performs only identity checks: no business query or mutation.
  expect(
    h.fetcher.mock.calls.every(([request]) =>
      /\/auth\/(login|me)$/.test(request.url),
    ),
  ).toBe(true);
});

test('a retained module refocuses only after a fresh permission check', async () => {
  let permissions = user.permissions;
  const h = harness(async () => json({ ...user, permissions }));
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
      'modules/[module]': ModuleScreen,
    },
    { initialUrl: '/' },
  );
  await waitFor(() => expect(h.client.getSnapshot().status).toBe('signedOut'));
  await act(async () => {
    await h.client.login('staff', 'fixture-password');
  });
  await fireEvent.press(
    await screen.findByRole('button', { name: 'Open Preparation' }),
  );
  await screen.findByRole('header', { name: 'Preparation' });
  permissions = [];
  // push preserves the module in navigation history, unlike replace/back-home.
  await act(async () => router.push('/session'));
  await screen.findByText(
    'No mobile modules are available for your permissions.',
  );
  await act(async () => router.back());
  await screen.findByText(
    'Access unavailable. Your current permissions do not allow this module.',
  );
  expect(screen.queryByRole('header', { name: 'Preparation' })).toBeNull();
});
