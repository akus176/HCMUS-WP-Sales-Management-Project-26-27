import { act, fireEvent, waitFor } from '@testing-library/react-native';
import { renderRouter, screen } from 'expo-router/testing-library';
import { router } from 'expo-router';

import { SessionRoutes } from '../app/_layout';
import StarterScreen from '../app/index';
import LoginScreen from '../app/login';
import SessionScreen from '../app/session';
import ModuleScreen from '../app/modules/[module]';
import ManagerOrdersScreen from '../app/modules/cashier/operations';
import DeliveryDetailRoute from '../app/modules/delivery/[orderId]';
import { SessionProvider } from '../src/session/context';
import { harness, json, user } from './helpers';
import { QueryCleanup } from './query-cleanup';

jest.mock('../src/realtime/OperationalUpdates', () => ({
  OperationalUpdates: () => null,
}));

test('an unauthorized delivery deep link never loads another courier assignment', async () => {
  const h = harness(async () => json(user));
  function Layout() {
    return (
      <SessionProvider client={h.client}>
        <QueryCleanup />
        <SessionRoutes />
      </SessionProvider>
    );
  }
  const orderId = '00000000-0000-0000-0000-000000000011';
  await renderRouter(
    {
      _layout: Layout,
      index: StarterScreen,
      login: LoginScreen,
      session: SessionScreen,
      'modules/[module]': ModuleScreen,
      'modules/delivery/[orderId]': DeliveryDetailRoute,
    },
    { initialUrl: `/modules/delivery/${orderId}` },
  );
  await waitFor(() => expect(screen).toHavePathname('/'));
  await act(async () => {
    await h.client.login('staff', 'fixture-password');
  });
  await screen.findByRole('header', { name: 'Staff home' });
  await act(async () => {
    router.push(`/modules/delivery/${orderId}`);
  });
  await screen.findByText('Access unavailable. This module is not available.');
  expect(
    h.fetcher.mock.calls.some(([request]) =>
      request.url.includes('/courier/assignments'),
    ),
  ).toBe(false);
});

test('manager order-operation deep links require a signed-in authorized session before loading orders', async () => {
  const h = harness(async () => json(user));
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
      'modules/cashier/operations': ManagerOrdersScreen,
    },
    { initialUrl: '/modules/cashier/operations' },
  );
  await waitFor(() => expect(screen).toHavePathname('/'));
  expect(h.fetcher).not.toHaveBeenCalled();
  await act(async () => {
    await h.client.login('staff', 'fixture-password');
  });
  await screen.findByRole('header', { name: 'Staff home' });
  await act(async () => {
    router.push('/modules/cashier/operations');
  });
  await screen.findByText(
    'Access unavailable. Your current permissions do not allow this module.',
  );
  expect(
    h.fetcher.mock.calls.some(([request]) =>
      request.url.includes('/api/v1/orders'),
    ),
  ).toBe(false);
});

test('real routes reject signed-out and unauthorized links, allow a grant, then reject revocation', async () => {
  let permissions = user.permissions;
  const h = harness(async (request) =>
    json(
      request.url.includes('preparation-tickets')
        ? { items: [], totalCount: 0, page: 1, pageSize: 10 }
        : { ...user, permissions },
    ),
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
  await screen.findByText('Access unavailable. This module is not available.');
  expect(screen.queryByRole('header', { name: 'Approvals' })).toBeNull();
  await fireEvent.press(screen.getByRole('tab', { name: 'Today' }));
  await fireEvent.press(
    await screen.findByRole('button', { name: 'Open Preparation' }),
  );
  expect(
    await screen.findByRole('header', { name: 'Preparation' }),
  ).toBeVisible();
  expect(screen.queryByText('Record payments — planned')).toBeNull();
  permissions = [];
  await act(async () => router.push('/modules/barista'));
  await screen.findByText(
    'Access unavailable. Your current permissions do not allow this module.',
  );
  await fireEvent.press(screen.getByRole('tab', { name: 'Today' }));
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
  // Authorized preparation performs its read; unauthorized modules never load data.
  expect(
    h.fetcher.mock.calls.every(
      ([request]) =>
        /\/auth\/(login|me)$/.test(request.url) ||
        (request.method === 'GET' &&
          request.url.includes('/preparation-tickets?')),
    ),
  ).toBe(true);
});

test('the removed approval workspace never loads discount data even with the old permission', async () => {
  const h = harness(async () =>
    json({ ...user, permissions: ['discounts.approve'] }),
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
      login: LoginScreen,
      session: SessionScreen,
      'modules/[module]': ModuleScreen,
    },
    { initialUrl: '/' },
  );
  await waitFor(() => expect(h.client.getSnapshot().status).toBe('signedOut'));
  await act(async () => h.client.login('staff', 'fixture-password'));
  await screen.findByRole('header', { name: 'Staff home' });
  await act(async () => router.push('/modules/approvals'));
  await screen.findByText('Access unavailable. This module is not available.');
  expect(
    h.fetcher.mock.calls.some(([request]) =>
      request.url.includes('/discounts/'),
    ),
  ).toBe(false);
});

test('a retained module refocuses only after a fresh permission check', async () => {
  let permissions = user.permissions;
  const h = harness(async (request) =>
    json(
      request.url.includes('preparation-tickets')
        ? { items: [], totalCount: 0, page: 1, pageSize: 10 }
        : { ...user, permissions },
    ),
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
  await screen.findByRole('header', { name: 'Staff home' });
  await act(async () => router.push('/modules/barista'));
  await screen.findByRole('header', { name: 'Preparation' });
  permissions = [];
  // Legacy module links normalize into the retained native workspace host.
  await act(async () => router.push('/session'));
  await screen.findByText(
    'No mobile modules are available for your permissions.',
  );
  await act(async () => router.push('/modules/barista'));
  await screen.findByText(
    'Access unavailable. Your current permissions do not allow this module.',
  );
  expect(screen.queryByRole('header', { name: 'Preparation' })).toBeNull();
});

test.each(['inventory', 'delivery', 'administration'])(
  'the %s page is unavailable in the Counter and Preparation app even with a grant',
  async (module) => {
    const h = harness(async () =>
      json({
        ...user,
        permissions: ['inventory.manage', 'delivery.update', 'users.manage'],
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
        login: LoginScreen,
        session: SessionScreen,
        'modules/[module]': ModuleScreen,
      },
      { initialUrl: '/' },
    );
    await waitFor(() =>
      expect(h.client.getSnapshot().status).toBe('signedOut'),
    );
    await act(async () => h.client.login('staff', 'fixture-password'));
    await screen.findByRole('header', { name: 'Staff home' });
    await act(async () => router.push(`/modules/${module}`));
    await screen.findByText(
      'Access unavailable. This module is not available.',
    );
    expect(
      h.fetcher.mock.calls.some(([request]) =>
        /\/(ingredients|users|courier)(\/|\?|$)/.test(request.url),
      ),
    ).toBe(false);
  },
);
