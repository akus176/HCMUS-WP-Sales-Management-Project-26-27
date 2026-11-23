import { act, fireEvent, waitFor } from '@testing-library/react-native';
import { Alert } from 'react-native';
import { renderRouter, screen } from 'expo-router/testing-library';
import { router } from 'expo-router';

import { SessionRoutes } from '../app/_layout';
import StarterScreen from '../app/index';
import LoginScreen from '../app/login';
import SessionScreen from '../app/session';
import ModuleScreen from '../app/modules/[module]';
import { SessionProvider } from '../src/session/context';
import { harness, json, user } from './helpers';
import { QueryCleanup } from './query-cleanup';

jest.mock('../src/realtime/OperationalUpdates', () => ({
  OperationalUpdates: () => null,
}));

jest.mock('../src/connectivity/networkAdapter', () => ({
  addNetworkStateListener: jest.fn(() => ({ remove: jest.fn() })),
  getNetworkStateAsync: jest.fn().mockResolvedValue({
    isConnected: true,
    isInternetReachable: true,
  }),
}));

const admin = { ...user, roles: ['Admin'], permissions: ['users.manage'] };
const account = {
  id: '00000000-0000-0000-0000-000000000185',
  username: 'at-risk',
  fullName: 'At Risk Staff',
  email: 'at-risk@example.test',
  phone: null,
  isActive: true,
  roles: ['Cashier'],
  createdAt: '2026-11-01T10:00:00Z',
  deactivatedAt: null,
};
const event = {
  id: '00000000-0000-0000-0000-000000000186',
  eventType: 'login_failed',
  occurredAt: '2026-11-07T10:00:00Z',
  outcome: 'denied',
  userId: account.id,
  username: account.username,
  ipAddress: null,
};

function Layout({ client }: { client: ReturnType<typeof harness>['client'] }) {
  return (
    <SessionProvider client={client}>
      <QueryCleanup />
      <SessionRoutes />
    </SessionProvider>
  );
}

test('unauthorized administration deep link exposes no account or event data', async () => {
  const h = harness(async (request) => json(user));
  await renderRouter(
    {
      _layout: () => <Layout client={h.client} />,
      index: StarterScreen,
      login: LoginScreen,
      session: SessionScreen,
      'modules/[module]': ModuleScreen,
    },
    { initialUrl: '/modules/administration' },
  );
  await waitFor(() => expect(screen).toHavePathname('/'));
  await act(async () => h.client.login('cashier', 'fixture-password'));
  await screen.findByRole('header', { name: 'Staff home' });
  await act(async () => router.push('/modules/administration'));
  await screen.findByText(
    'Access unavailable. Your current permissions do not allow this module.',
  );
  expect(screen.queryByRole('header', { name: 'Staff' })).toBeNull();
  expect(
    h.fetcher.mock.calls.some(
      ([request]) =>
        request.url.includes('/api/v1/users') ||
        request.url.includes('/api/v1/security-events'),
    ),
  ).toBe(false);
});

test('authorized account views are safe and deactivation requires confirmation', async () => {
  let isActive = true;
  const h = harness(async (request) => {
    if (request.url.endsWith('/auth/me')) return json(admin);
    if (request.url.includes('/api/v1/users/') && request.method === 'POST') {
      const body = await request.json();
      expect(body).toEqual({ confirm: true });
      isActive = false;
      return json({
        ...account,
        isActive,
        deactivatedAt: '2026-11-07T10:00:00Z',
      });
    }
    if (request.url.includes('/api/v1/users/'))
      return json({ ...account, isActive });
    if (request.url.includes('/api/v1/users'))
      return json([{ ...account, isActive }]);
    if (request.url.includes('/api/v1/security-events')) return json([event]);
    return json(admin);
  });
  const alert = jest
    .spyOn(Alert, 'alert')
    .mockImplementation((_title, _message, buttons) => {
      buttons?.find((button) => button.text === 'Deactivate')?.onPress?.();
    });
  await renderRouter(
    {
      _layout: () => <Layout client={h.client} />,
      index: StarterScreen,
      login: LoginScreen,
      session: SessionScreen,
      'modules/[module]': ModuleScreen,
    },
    { initialUrl: '/' },
  );
  await act(async () => h.client.login('admin', 'fixture-password'));
  await screen.findByRole('button', { name: 'Open Administration' });
  await fireEvent.press(
    screen.getByRole('button', { name: 'Open Administration' }),
  );
  await screen.findByRole('header', { name: 'Staff' });
  await screen.findByText('At Risk Staff');
  expect(screen.queryByText('login failed')).toBeNull();
  expect(
    screen.queryByRole('button', { name: /create account|edit roles/i }),
  ).toBeNull();
  await fireEvent.changeText(
    screen.getByLabelText('Search staff accounts'),
    'at-risk',
  );
  await fireEvent.press(
    await screen.findByRole('button', {
      name: 'View details for At Risk Staff',
    }),
  );
  await screen.findByRole('header', { name: 'Account details' });
  expect(screen.queryByLabelText('Search staff accounts')).toBeNull();
  expect(
    h.fetcher.mock.calls.some(([request]) =>
      request.url.includes(`/api/v1/users/${account.id}`),
    ),
  ).toBe(true);
  await fireEvent.press(
    screen.getByRole('button', { name: 'Close account details' }),
  );
  expect(screen.getByLabelText('Search staff accounts').props.value).toBe(
    'at-risk',
  );
  await fireEvent.press(
    screen.getByRole('button', { name: 'Security events' }),
  );
  await screen.findByText('login failed');
  await fireEvent.press(
    screen.getByRole('button', { name: 'Sign-in failures' }),
  );
  await waitFor(() =>
    expect(
      h.fetcher.mock.calls.some(
        ([request]) =>
          request.url.includes('/api/v1/security-events?') &&
          new URL(request.url).searchParams.get('eventType') === 'login_failed',
      ),
    ).toBe(true),
  );
  await fireEvent.press(screen.getByRole('button', { name: 'Staff accounts' }));
  await fireEvent.changeText(
    screen.getByLabelText('Search staff accounts'),
    'at-risk',
  );
  await waitFor(() =>
    expect(
      h.fetcher.mock.calls.some(
        ([request]) =>
          request.url.includes('/api/v1/users?') &&
          new URL(request.url).searchParams.get('search') === 'at-risk',
      ),
    ).toBe(true),
  );
  await fireEvent.press(
    await screen.findByRole('button', { name: 'Deactivate At Risk Staff' }),
  );
  expect(alert).toHaveBeenCalledWith(
    'Deactivate staff account?',
    expect.stringContaining('active sessions will be revoked'),
    expect.any(Array),
  );
  await waitFor(() => expect(isActive).toBe(false));
  expect(await screen.findByText(/now inactive/)).toBeVisible();
  expect(
    h.fetcher.mock.calls.some(
      ([request]) =>
        request.url.includes('/api/v1/users/') && request.method === 'POST',
    ),
  ).toBe(true);
  expect(screen.queryByText(/metadata|refresh token|password/i)).toBeNull();
  alert.mockRestore();
});

test('a rejected deactivation is an error beside the account and never a success', async () => {
  const h = harness(async (request) => {
    if (request.url.endsWith('/auth/me')) return json(admin);
    if (request.url.endsWith('/deactivate'))
      return json({ title: 'Final administrator' }, 409);
    if (request.url.includes('/api/v1/users')) return json([account]);
    if (request.url.includes('/api/v1/security-events')) return json([]);
    return json(admin);
  });
  const alert = jest
    .spyOn(Alert, 'alert')
    .mockImplementation((_title, _message, buttons) => {
      buttons?.find((button) => button.text === 'Deactivate')?.onPress?.();
    });
  try {
    await renderRouter(
      {
        _layout: () => <Layout client={h.client} />,
        index: StarterScreen,
        login: LoginScreen,
        session: SessionScreen,
        'modules/[module]': ModuleScreen,
      },
      { initialUrl: '/' },
    );
    await act(async () => h.client.login('admin', 'fixture-password'));
    await fireEvent.press(
      await screen.findByRole('button', { name: 'Open Administration' }),
    );
    await fireEvent.press(
      await screen.findByRole('button', { name: 'Deactivate At Risk Staff' }),
    );
    expect(
      await screen.findByRole('alert', { name: /final active administrator/ }),
    ).toBeVisible();
    expect(screen.queryByText(/now inactive/)).toBeNull();
  } finally {
    alert.mockRestore();
  }
});

test('API-forbidden account and event reads expose no previously rendered data', async () => {
  const h = harness(async (request) => {
    if (request.url.endsWith('/auth/me')) return json(admin);
    if (
      request.url.includes('/api/v1/users') ||
      request.url.includes('/api/v1/security-events')
    )
      return json({ title: 'Forbidden' }, 403);
    return json(admin);
  });
  await renderRouter(
    {
      _layout: () => <Layout client={h.client} />,
      index: StarterScreen,
      login: LoginScreen,
      session: SessionScreen,
      'modules/[module]': ModuleScreen,
    },
    { initialUrl: '/' },
  );
  await act(async () => h.client.login('admin', 'fixture-password'));
  await fireEvent.press(
    await screen.findByRole('button', { name: 'Open Administration' }),
  );
  await screen.findByText(
    'Your staff administration permission changed. No account or event data is available.',
  );
  expect(screen.queryByText('At Risk Staff')).toBeNull();
  expect(screen.queryByText('login failed')).toBeNull();
});
