import {
  act,
  fireEvent,
  render,
  screen,
  waitFor,
} from '@testing-library/react-native';
import { QueryClient } from '@tanstack/react-query';
import { Linking } from 'react-native';

import { DeliveryDetail } from '../src/features/delivery/DeliveryDetail';
import { ConnectivityProvider } from '../src/connectivity/connectivity';
import * as network from '../src/connectivity/networkAdapter';
import { SessionProvider, useSession } from '../src/session/context';
import { harness, json, pair } from './helpers';
import { createSessionClient } from '../src/session/session';
import { QueryCleanup } from './query-cleanup';

jest.mock('expo-router', () => ({ router: { replace: jest.fn() } }));
jest.mock('../src/connectivity/networkAdapter', () => ({
  getNetworkStateAsync: jest.fn(),
  addNetworkStateListener: jest.fn(),
}));
beforeEach(() => {
  jest.mocked(network.getNetworkStateAsync).mockResolvedValue({
    isConnected: true,
    isInternetReachable: true,
  } as network.NetworkState);
  jest
    .mocked(network.addNetworkStateListener)
    .mockReturnValue({ remove: jest.fn() });
});

function SignedInDetail() {
  return useSession().state.user ? (
    <DeliveryDetail orderId="delivery-1" />
  ) : null;
}

test('opens navigation with an encoded address and discards details on unmount', async () => {
  const h = harness(async () =>
    json({
      orderId: 'delivery-1',
      orderNumber: 'ORD-1',
      deliveryStatus: 'assigned',
      recipientName: 'Recipient',
      contactNumber: '123456789',
      addressLine: '1 Main St & 2nd',
    }),
  );
  const queries = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  const open = jest.spyOn(Linking, 'openURL').mockResolvedValue(undefined);
  const view = await render(
    <SessionProvider client={h.client} queryClient={queries}>
      <QueryCleanup />
      <ConnectivityProvider>
        <SignedInDetail />
      </ConnectivityProvider>
    </SessionProvider>,
  );
  await waitFor(() => expect(h.client.getSnapshot().status).toBe('signedOut'));
  await act(async () => {
    await h.client.login('courier', 'password');
  });
  await screen.findByText('Address: 1 Main St & 2nd');
  await fireEvent.press(
    screen.getByRole('button', { name: 'Open navigation' }),
  );
  expect(open).toHaveBeenCalledWith(
    'https://www.google.com/maps/search/?api=1&query=1%20Main%20St%20%26%202nd',
  );
  await view.unmount();
  await waitFor(() =>
    expect(
      queries.getQueryCache().findAll({ queryKey: ['delivery'] }),
    ).toHaveLength(0),
  );
  open.mockRestore();
});

test('does not render private details when the server rejects a deep link', async () => {
  const h = harness(async () => json({ title: 'Not found' }, 404));
  const view = await render(
    <SessionProvider client={h.client}>
      <QueryCleanup />
      <ConnectivityProvider>
        <SignedInDetail />
      </ConnectivityProvider>
    </SessionProvider>,
  );
  await waitFor(() => expect(h.client.getSnapshot().status).toBe('signedOut'));
  await act(async () => {
    await h.client.login('courier', 'password');
  });
  await screen.findByText(
    'Delivery is unavailable or no longer assigned to you.',
  );
  expect(screen.queryByRole('button', { name: 'Open navigation' })).toBeNull();
  await view.unmount();
});

test.each(['complete', 'fail'] as const)(
  'starts then records %s and removes private detail',
  async (outcome) => {
    let status = 'assigned';
    const h = harness(async (request) => {
      if (request.method === 'POST') {
        if (request.url.endsWith('/start')) status = 'out_for_delivery';
        else status = outcome === 'complete' ? 'delivered' : 'failed';
        return new Response(null, { status: 204 });
      }
      return json({
        orderId: 'delivery-1',
        orderNumber: 'ORD-1',
        deliveryStatus: status,
        recipientName: 'Recipient',
        contactNumber: '123456789',
        addressLine: 'Private Address',
      });
    });
    const queries = new QueryClient({
      defaultOptions: { queries: { retry: false } },
    });
    const view = await render(
      <SessionProvider client={h.client} queryClient={queries}>
        <QueryCleanup />
        <ConnectivityProvider>
          <SignedInDetail />
        </ConnectivityProvider>
      </SessionProvider>,
    );
    await waitFor(() =>
      expect(h.client.getSnapshot().status).toBe('signedOut'),
    );
    await act(async () => {
      await h.client.login('courier', 'password');
    });
    await fireEvent.press(
      await screen.findByRole('button', { name: 'Start delivery' }),
    );
    await screen.findByText('Status: out for delivery');
    const activeKey = [
      'delivery',
      h.client.getSnapshot().user!.id,
      'list',
      'active',
      1,
    ];
    queries.setQueryData(activeKey, {
      items: [{ orderId: 'delivery-1' }],
      totalCount: 1,
    });
    if (outcome === 'fail') {
      await fireEvent.press(
        screen.getByRole('button', { name: 'Mark failed' }),
      );
      await screen.findByText('A failure reason is required.');
      expect(
        h.fetcher.mock.calls.filter(([request]) =>
          request.url.endsWith('/fail'),
        ),
      ).toHaveLength(0);
      await fireEvent.changeText(
        screen.getByLabelText('Failure reason'),
        'Customer unavailable',
      );
    }
    await fireEvent.press(
      screen.getByRole('button', {
        name: outcome === 'complete' ? 'Mark delivered' : 'Mark failed',
      }),
    );
    await screen.findByText(
      outcome === 'complete'
        ? 'Delivery delivered. Confirmed by the service.'
        : 'Delivery failed. Confirmed by the service.',
    );
    expect(screen.queryByText('Address: Private Address')).toBeNull();
    expect(queries.getQueryData(activeKey)).toBeUndefined();
    expect(
      queries.getQueryData([
        'delivery',
        h.client.getSnapshot().user!.id,
        'detail',
        'delivery-1',
      ]),
    ).toBeUndefined();
    await view.unmount();
    await waitFor(() =>
      expect(
        queries.getQueryCache().findAll({ queryKey: ['delivery'] }),
      ).toHaveLength(0),
    );
  },
);

test('conflicts refresh the server state without reporting success', async () => {
  let state = 'assigned';
  const h = harness(async (request) => {
    if (request.method === 'POST') {
      state = 'out_for_delivery';
      return json({ title: 'Conflict' }, 409);
    }
    return json({
      orderId: 'delivery-1',
      orderNumber: 'ORD-1',
      deliveryStatus: state,
      recipientName: 'Recipient',
      contactNumber: '123456789',
      addressLine: 'Private Address',
    });
  });
  const view = await render(
    <SessionProvider client={h.client}>
      <QueryCleanup />
      <ConnectivityProvider>
        <SignedInDetail />
      </ConnectivityProvider>
    </SessionProvider>,
  );
  await waitFor(() => expect(h.client.getSnapshot().status).toBe('signedOut'));
  await act(async () => {
    await h.client.login('courier', 'password');
  });
  await fireEvent.press(
    await screen.findByRole('button', { name: 'Start delivery' }),
  );
  await screen.findByText(
    'Delivery changed elsewhere. Review the refreshed state before trying again.',
  );
  await screen.findByText('Status: out for delivery');
  expect(
    screen.queryByText(
      'Delivery is out for delivery. Confirmed by the service.',
    ),
  ).toBeNull();
  await view.unmount();
});

test('a forbidden transition removes retained delivery contact data', async () => {
  const h = harness(async (request) =>
    request.method === 'POST'
      ? json({ title: 'Forbidden' }, 403)
      : json({
          orderId: 'delivery-1',
          orderNumber: 'ORD-1',
          deliveryStatus: 'assigned',
          recipientName: 'Recipient',
          contactNumber: '123456789',
          addressLine: 'Private Address',
        }),
  );
  const queries = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  const view = await render(
    <SessionProvider client={h.client} queryClient={queries}>
      <QueryCleanup />
      <ConnectivityProvider>
        <SignedInDetail />
      </ConnectivityProvider>
    </SessionProvider>,
  );
  await waitFor(() => expect(h.client.getSnapshot().status).toBe('signedOut'));
  await act(async () => {
    await h.client.login('courier', 'password');
  });
  await screen.findByText('Address: Private Address');
  await fireEvent.press(screen.getByRole('button', { name: 'Start delivery' }));
  await screen.findByText(
    'Delivery is unavailable or no longer assigned to you.',
  );
  expect(screen.queryByText('Address: Private Address')).toBeNull();
  expect(
    queries.getQueryData([
      'delivery',
      h.client.getSnapshot().user!.id,
      'detail',
      'delivery-1',
    ]),
  ).toBeUndefined();
  await view.unmount();
});

test('logout removes retained delivery details and clears the protected screen', async () => {
  const queries = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  let token: string | null = null;
  const client = createSessionClient({
    origin: 'https://staff.example',
    storage: {
      get: async () => token,
      set: async (value) => {
        token = value;
      },
      remove: async () => {
        token = null;
      },
    },
    clearData: () => {
      void queries.cancelQueries();
      queries.clear();
    },
    fetch: async (request) => {
      const url = (request as Request).url;
      if (url.endsWith('/auth/login')) return json(pair());
      if (url.endsWith('/auth/logout'))
        return new Response(null, { status: 204 });
      return json({
        orderId: 'delivery-1',
        orderNumber: 'ORD-1',
        deliveryStatus: 'assigned',
        recipientName: 'Recipient',
        contactNumber: '123456789',
        addressLine: 'Private Address',
      });
    },
  });
  const view = await render(
    <SessionProvider client={client} queryClient={queries}>
      <QueryCleanup />
      <ConnectivityProvider>
        <SignedInDetail />
      </ConnectivityProvider>
    </SessionProvider>,
  );
  await waitFor(() => expect(client.getSnapshot().status).toBe('signedOut'));
  await act(async () => {
    await client.login('courier', 'password');
  });
  await screen.findByText('Address: Private Address');
  await act(async () => {
    await client.logout();
  });
  expect(screen.queryByText('Address: Private Address')).toBeNull();
  expect(
    queries.getQueryCache().findAll({ queryKey: ['delivery'] }),
  ).toHaveLength(0);
  await view.unmount();
});
