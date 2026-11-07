import {
  act,
  fireEvent,
  render,
  screen,
  waitFor,
} from '@testing-library/react-native';
import { onlineManager } from '@tanstack/react-query';

import { PreparationScreen } from '../src/barista/PreparationScreen';
import { ConnectivityProvider } from '../src/connectivity/connectivity';
import * as network from '../src/connectivity/networkAdapter';
import type { NetworkState } from '../src/connectivity/networkAdapter';
import { SessionProvider } from '../src/session/context';
import { PermissionBoundary } from '../src/shell/PermissionBoundary';
import { deferred, harness, json, user } from './helpers';
import { QueryCleanup } from './query-cleanup';

jest.mock('../src/connectivity/networkAdapter', () => ({
  getNetworkStateAsync: jest.fn(),
  addNetworkStateListener: jest.fn(),
}));
let listener: (state: NetworkState) => void | Promise<void>;
const connected = {
  isConnected: true,
  isInternetReachable: true,
} as NetworkState;
const disconnected = {
  isConnected: false,
  isInternetReachable: false,
} as NetworkState;
const ticket = {
  orderId: '00000000-0000-0000-0000-000000000155',
  orderNumber: 'ORD-155',
  status: 'confirmed',
  tableNumber: 'T3',
  lines: [
    { itemName: 'Latte', sizeName: 'Large', quantity: 2, addons: ['Oat milk'] },
  ],
};
const page = (items = [ticket], totalCount = items.length) => ({
  items,
  totalCount,
  page: 1,
  pageSize: 10,
});

beforeEach(() => {
  jest.mocked(network.getNetworkStateAsync).mockResolvedValue(connected);
  jest
    .mocked(network.addNetworkStateListener)
    .mockImplementation((callback) => {
      listener = callback;
      return { remove: jest.fn() };
    });
});
afterEach(() => onlineManager.setOnline(true));

async function mount(
  handler: (request: Request) => Promise<Response>,
  permissions = user.permissions,
) {
  const h = harness(async (request) =>
    request.url.endsWith('/auth/me')
      ? json({ ...user, permissions })
      : handler(request),
  );
  await render(
    <SessionProvider client={h.client}>
      <QueryCleanup />
      <ConnectivityProvider>
        <PermissionBoundary moduleId="barista">
          {() => <PreparationScreen />}
        </PermissionBoundary>
      </ConnectivityProvider>
    </SessionProvider>,
  );
  await waitFor(() => expect(h.client.getSnapshot().status).toBe('signedOut'));
  await act(async () => {
    await h.client.login('staff', 'fixture-password');
  });
  return h;
}
async function openTicket() {
  await fireEvent.press(
    await screen.findByRole('button', { name: 'Open ticket ORD-155' }),
  );
  await screen.findByText('Status: Confirmed');
}

test('shows loading, empty results and manual refresh without inventing tickets', async () => {
  const pending = deferred<Response>();
  let first = true;
  const h = await mount(async () => (first ? pending.promise : json(page([]))));
  expect(await screen.findByText('Loading preparation queue…')).toBeVisible();
  first = false;
  await act(async () => pending.resolve(json(page([]))));
  expect(await screen.findByText(/No tickets in this view/)).toBeVisible();
  const count = h.fetcher.mock.calls.length;
  await fireEvent.press(screen.getByRole('button', { name: 'Refresh queue' }));
  await waitFor(() =>
    expect(h.fetcher.mock.calls.length).toBeGreaterThan(count),
  );
});

test('filters and paginates through generated query parameters', async () => {
  const h = await mount(async () => json(page([ticket], 21)));
  await screen.findByText('Page 1 · 21 tickets');
  await fireEvent.press(screen.getByRole('button', { name: 'Next page' }));
  await screen.findByText('Page 2 · 21 tickets');
  expect(
    h.fetcher.mock.calls.some(
      ([r]) => new URL(r.url).searchParams.get('page') === '2',
    ),
  ).toBe(true);
  await fireEvent.press(screen.getByRole('button', { name: 'Preparing' }));
  await screen.findByText('Page 1 · 21 tickets');
  expect(
    h.fetcher.mock.calls.some(
      ([r]) =>
        new URL(r.url).searchParams.get('status') === 'preparing' &&
        new URL(r.url).searchParams.get('page') === '1',
    ),
  ).toBe(true);
});

test('details contain preparation fields, confirm each write, and show only server-confirmed progression', async () => {
  let status = 'confirmed';
  const sent = deferred<Response>();
  const h = await mount(async (request) => {
    if (request.method === 'POST') {
      if (request.url.endsWith('/start')) {
        const response = await sent.promise;
        status = 'preparing';
        return response;
      }
      status = 'ready';
      return new Response(null, { status: 204 });
    }
    if (request.url.includes(ticket.orderId))
      return json({ ...ticket, status });
    return json(page(status === 'ready' ? [] : [{ ...ticket, status }]));
  });
  await openTicket();
  expect(screen.getByText('2 × Latte · Large')).toBeVisible();
  expect(screen.getByText('Add-ons: Oat milk')).toBeVisible();
  expect(screen.getByText('Table T3')).toBeVisible();
  await fireEvent.press(
    screen.getByRole('button', { name: 'Start preparing' }),
  );
  expect(
    h.fetcher.mock.calls.filter(
      ([r]) => r.method === 'POST' && r.url.includes('preparation-tickets'),
    ),
  ).toHaveLength(0);
  await fireEvent.press(
    screen.getByRole('button', { name: 'Keep current state' }),
  );
  await fireEvent.press(
    screen.getByRole('button', { name: 'Start preparing' }),
  );
  await fireEvent.press(screen.getByRole('button', { name: 'Confirm start' }));
  await fireEvent.press(
    screen.getByRole('button', { name: 'Start preparing' }),
  );
  expect(screen.queryByText(/Preparation started/)).toBeNull();
  expect(screen.getByText('Status: Confirmed')).toBeVisible();
  expect(
    h.fetcher.mock.calls.filter(([r]) => r.url.endsWith('/start')),
  ).toHaveLength(1);
  await act(async () => sent.resolve(new Response(null, { status: 204 })));
  await screen.findByText('Status: Preparing');
  await fireEvent.press(screen.getByRole('button', { name: 'Mark ready' }));
  await fireEvent.press(screen.getByRole('button', { name: 'Confirm ready' }));
  expect(
    await screen.findByText('Ticket is Ready. Confirmed by the service.'),
  ).toBeVisible();
  expect(await screen.findByText(/No tickets in this view/)).toBeVisible();
});

test.each(['preparing', 'cancelled'])(
  'a stale start refreshes %s state and never overwrites it',
  async (newer) => {
    let status = 'confirmed';
    const h = await mount(async (request) => {
      if (request.method === 'POST') {
        status = newer;
        return json({}, 409);
      }
      if (request.url.includes(ticket.orderId))
        return status === 'cancelled'
          ? json({}, 404)
          : json({ ...ticket, status });
      return json(page(status === 'cancelled' ? [] : [{ ...ticket, status }]));
    });
    await openTicket();
    await fireEvent.press(
      screen.getByRole('button', { name: 'Start preparing' }),
    );
    await fireEvent.press(
      screen.getByRole('button', { name: 'Confirm start' }),
    );
    expect(await screen.findByText(/Ticket changed elsewhere/)).toBeVisible();
    if (newer === 'preparing')
      expect(await screen.findByText('Status: Preparing')).toBeVisible();
    else {
      expect(
        await screen.findByText(/Ticket is no longer in the preparation queue/),
      ).toBeVisible();
      expect(screen.queryByText('Latte')).toBeNull();
      expect(
        screen.queryByRole('button', { name: 'Start preparing' }),
      ).toBeNull();
    }
    expect(
      h.fetcher.mock.calls.filter(([r]) => r.url.endsWith('/start')),
    ).toHaveLength(1);
    expect(screen.queryByText(/Confirmed by the service/)).toBeNull();
  },
);

test('offline confirmation is disabled and reconnect does not replay a write', async () => {
  const h = await mount(async (request) =>
    json(request.url.includes(ticket.orderId) ? ticket : page()),
  );
  await openTicket();
  await fireEvent.press(
    screen.getByRole('button', { name: 'Start preparing' }),
  );
  await act(async () => {
    await listener(disconnected);
  });
  expect(screen.getByRole('button', { name: 'Confirm start' })).toBeDisabled();
  await fireEvent.press(screen.getByRole('button', { name: 'Confirm start' }));
  await act(async () => {
    await listener(connected);
  });
  await waitFor(() =>
    expect(screen.getByRole('button', { name: 'Confirm start' })).toBeEnabled(),
  );
  expect(
    h.fetcher.mock.calls.filter(([r]) => r.url.endsWith('/start')),
  ).toHaveLength(0);
});

test('unauthorized route never loads tickets', async () => {
  const h = await mount(async () => json(page()), []);
  expect(
    await screen.findByText(
      /Your current permissions do not allow this module/,
    ),
  ).toBeVisible();
  expect(
    h.fetcher.mock.calls.some(([r]) => r.url.includes('preparation-tickets')),
  ).toBe(false);
});

test('API forbidden response immediately hides cached details even if refresh would stall', async () => {
  let denied = false;
  const stalled = deferred<Response>();
  const h = await mount(async (request) => {
    if (request.method === 'POST') {
      denied = true;
      return json({}, 403);
    }
    if (denied) return stalled.promise;
    return json(request.url.includes(ticket.orderId) ? ticket : page());
  });
  await openTicket();
  await fireEvent.press(
    screen.getByRole('button', { name: 'Start preparing' }),
  );
  await fireEvent.press(screen.getByRole('button', { name: 'Confirm start' }));
  expect(
    await screen.findByText(/You do not have permission to prepare tickets/),
  ).toBeVisible();
  expect(screen.queryByText('2 × Latte · Large')).toBeNull();
  expect(screen.queryByRole('button', { name: 'Start preparing' })).toBeNull();
  expect(
    h.fetcher.mock.calls.filter(([r]) => r.url.endsWith('/start')),
  ).toHaveLength(1);
});

test('queue errors expose retry; uncertain writes refresh without a success claim or replay', async () => {
  let failRead = true;
  const h = await mount(async (request) => {
    if (request.method === 'POST') throw new Error('transport unavailable');
    if (failRead) return json({}, 500);
    return json(request.url.includes(ticket.orderId) ? ticket : page());
  });
  await screen.findByText('The service could not complete the request.');
  failRead = false;
  await fireEvent.press(screen.getByRole('button', { name: 'Refresh queue' }));
  await openTicket();
  await fireEvent.press(
    screen.getByRole('button', { name: 'Start preparing' }),
  );
  await fireEvent.press(screen.getByRole('button', { name: 'Confirm start' }));
  expect(await screen.findByText(/No success was confirmed/)).toBeVisible();
  expect(screen.getByText('Status: Confirmed')).toBeVisible();
  expect(
    h.fetcher.mock.calls.filter(([r]) => r.url.endsWith('/start')),
  ).toHaveLength(1);
});
