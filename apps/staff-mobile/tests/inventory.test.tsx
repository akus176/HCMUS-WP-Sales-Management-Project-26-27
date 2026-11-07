import { fireEvent, render, waitFor } from '@testing-library/react-native';
import {
  QueryClient,
  onlineManager,
  useQueryClient,
} from '@tanstack/react-query';
import { useEffect } from 'react';

import { InventoryWorkspace } from '../src/features/inventory/InventoryWorkspace';
import { ApiError, NetworkError } from '../src/api/errors';
import { ConnectivityProvider } from '../src/connectivity/connectivity';
import { ConnectivityBanner } from '../src/connectivity/ConnectivityBanner';
import * as networkAdapter from '../src/connectivity/networkAdapter';
import type { NetworkState } from '../src/connectivity/networkAdapter';
import type { SessionClient, StaffSession } from '../src/session/session';
import { SessionProvider } from '../src/session/context';

jest.mock('../src/connectivity/networkAdapter', () => ({
  addNetworkStateListener: jest.fn(),
  getNetworkStateAsync: jest.fn(),
}));

const user: StaffSession = {
  id: '00000000-0000-0000-0000-000000000184',
  fullName: 'Inventory Staff',
  roles: ['InventoryStaff'],
  permissions: [
    'inventory.manage',
    'inventory.adjust',
    'inventory.receive',
    'reports.lowstock',
  ],
};
const balance = {
  ingredientId: '00000000-0000-0000-0000-000000000101',
  name: 'Oat milk',
  baseUnit: 'l',
  availableQuantity: 1,
  reorderThreshold: 3,
};
const movement = {
  id: '00000000-0000-0000-0000-000000000201',
  ingredientId: balance.ingredientId,
  type: 'adjustment',
  quantityDelta: 2,
  actorId: user.id,
  reason: 'Physical count',
  occurredAt: '2026-11-08T08:00:00Z',
  purchaseReceiptId: null,
  orderId: null,
};
const receipt = {
  id: '00000000-0000-0000-0000-000000000301',
  supplierId: '00000000-0000-0000-0000-000000000302',
  receiptNumber: 'RCPT-184',
  status: 'draft',
  postedAt: null,
  lines: [
    {
      id: '00000000-0000-0000-0000-000000000303',
      ingredientId: balance.ingredientId,
      quantity: 4,
      unitCost: 2.5,
    },
  ],
};
const online: NetworkState = {
  isConnected: true,
  isInternetReachable: true,
};
const signedIn = { status: 'signedIn' as const, user };
let currentClient: SessionClient;
const getNetworkStateAsync = jest.mocked(networkAdapter.getNetworkStateAsync);
const addNetworkStateListener = jest.mocked(
  networkAdapter.addNetworkStateListener,
);

function createClient(
  get = jest.fn(async () => ({
    data: { items: [balance], page: 1, pageSize: 100, totalCount: 1 },
  })),
  post = jest.fn(async () => ({
    data: { ingredientId: balance.ingredientId, availableQuantity: 2 },
  })),
) {
  currentClient = {
    api: { GET: get, POST: post },
    getSnapshot: () => signedIn,
    subscribe: () => () => {},
    restore: async () => {},
  } as unknown as SessionClient;
  return { get, post };
}

function ClearQueriesOnUnmount() {
  const queries = useQueryClient();
  useEffect(() => () => queries.clear(), [queries]);
  return null;
}

async function setup(screenUser: StaffSession = user) {
  getNetworkStateAsync.mockResolvedValue(online);
  addNetworkStateListener.mockImplementation((callback) => {
    void callback;
    return { remove: jest.fn() };
  });
  const queryClient = new QueryClient({
    defaultOptions: {
      queries: { retry: false, gcTime: 0 },
      mutations: { retry: false, gcTime: 0 },
    },
  });
  function Wrapper({ children }: { children: React.ReactNode }) {
    return (
      <SessionProvider client={currentClient} queryClient={queryClient}>
        <ConnectivityProvider>
          <ClearQueriesOnUnmount />
          <ConnectivityBanner />
          {children}
        </ConnectivityProvider>
      </SessionProvider>
    );
  }
  return render(<InventoryWorkspace user={screenUser} />, { wrapper: Wrapper });
}

beforeEach(() => {
  jest.restoreAllMocks();
  onlineManager.setOnline(true);
  getNetworkStateAsync.mockReset().mockResolvedValue(online);
  addNetworkStateListener.mockReset();
});

test('loads current ingredient balances and marks items at their reorder threshold', async () => {
  const { get } = createClient();
  const view = await setup();

  expect(await view.findByText('Oat milk')).toBeVisible();
  expect(view.getByText('Low stock · 1 l / 3 l')).toBeVisible();
  expect(get).toHaveBeenCalledWith(
    '/api/v1/ingredients',
    expect.objectContaining({ params: { query: { page: 1, pageSize: 100 } } }),
  );
});

test('loads the shared low-stock query and applies ingredient search', async () => {
  const get = jest.fn(
    async (
      path: string,
      options: { params?: { query?: { search?: string } } },
    ) =>
      path.includes('low-stock')
        ? { data: { items: [balance], page: 1, pageSize: 100, totalCount: 1 } }
        : { data: { items: [balance], page: 1, pageSize: 100, totalCount: 1 } },
  );
  createClient(get as never);
  const view = await setup();

  await view.findByText('Oat milk');
  await fireEvent.press(view.getByRole('button', { name: 'Low stock' }));
  await waitFor(() =>
    expect(get).toHaveBeenCalledWith(
      '/api/v1/ingredients/low-stock',
      expect.objectContaining({
        params: { query: { page: 1, pageSize: 100 } },
      }),
    ),
  );
  await fireEvent.changeText(view.getByLabelText('Search ingredients'), 'oat');
  await waitFor(() =>
    expect(get).toHaveBeenCalledWith(
      '/api/v1/ingredients/low-stock',
      expect.objectContaining({
        params: { query: { page: 1, pageSize: 100, search: 'oat' } },
      }),
    ),
  );
});

test('a low-stock-only user loads only the endpoint allowed by their permission', async () => {
  const lowStockUser: StaffSession = {
    ...user,
    permissions: ['reports.lowstock'],
  };
  const get = jest.fn(async () => ({
    data: { items: [balance], page: 1, pageSize: 100, totalCount: 1 },
  }));
  createClient(get);
  const view = await setup(lowStockUser);

  expect(await view.findByText('Oat milk')).toBeVisible();
  expect(get).toHaveBeenCalledWith(
    '/api/v1/ingredients/low-stock',
    expect.objectContaining({ params: { query: { page: 1, pageSize: 100 } } }),
  );
  expect(get).not.toHaveBeenCalledWith(
    '/api/v1/ingredients',
    expect.anything(),
  );
  expect(
    view.queryByRole('button', { name: 'Ingredient balances' }),
  ).toBeNull();
});

test('manual refresh reloads the active inventory query', async () => {
  const { get } = createClient();
  const view = await setup();
  await view.findByText('Oat milk');
  const beforeRefresh = get.mock.calls.length;

  await fireEvent.press(
    view.getByRole('button', { name: 'Refresh inventory' }),
  );
  await waitFor(() => expect(get).toHaveBeenCalledTimes(beforeRefresh + 1));
});

test('shows an empty state when the shared low-stock query has no matches', async () => {
  const get = jest.fn(async (path: string) => ({
    data: {
      items: path.includes('low-stock') ? [] : [balance],
      page: 1,
      pageSize: 100,
      totalCount: path.includes('low-stock') ? 0 : 1,
    },
  }));
  createClient(get as never);
  const view = await setup();

  await view.findByText('Oat milk');
  await fireEvent.press(view.getByRole('button', { name: 'Low stock' }));
  expect(
    await view.findByText('No low-stock ingredients match.'),
  ).toBeVisible();
});

test('adjustment requires a reason and displays server-confirmed balance and movement', async () => {
  const get = jest.fn(async (path: string) => ({
    data: path.includes('/movements')
      ? [movement]
      : { items: [balance], page: 1, pageSize: 100, totalCount: 1 },
  }));
  const post = jest.fn(async () => ({
    data: { ingredientId: balance.ingredientId, availableQuantity: 3 },
  }));
  createClient(get as never, post as never);
  const view = await setup();

  await view.findByText('Oat milk');
  await fireEvent.press(
    view.getByRole('button', { name: 'Record adjustment for Oat milk' }),
  );
  await fireEvent.changeText(view.getByLabelText('Quantity change'), '2');
  await fireEvent.press(view.getByRole('button', { name: 'Save adjustment' }));
  expect(post).not.toHaveBeenCalled();

  await fireEvent.changeText(
    view.getByLabelText('Adjustment reason'),
    'Physical count',
  );
  await fireEvent.press(view.getByRole('button', { name: 'Save adjustment' }));
  expect(
    await view.findByText('Adjustment saved by the server. New balance: 3 l.'),
  ).toBeVisible();
  expect(post).toHaveBeenCalledWith(
    '/api/v1/ingredients/{id}/adjustments',
    expect.objectContaining({
      params: { path: { id: balance.ingredientId } },
      body: expect.objectContaining({
        quantityDelta: 2,
        reason: 'Physical count',
      }),
    }),
  );

  await fireEvent.press(
    view.getByRole('button', { name: 'Show movement history for Oat milk' }),
  );
  await waitFor(() =>
    expect(get).toHaveBeenCalledWith(
      '/api/v1/ingredients/{id}/movements',
      expect.objectContaining({
        params: { path: { id: balance.ingredientId } },
      }),
    ),
  );
  expect(
    await view.findByText(/adjustment: 2 l · Physical count/),
  ).toBeVisible();
});

test('server validation rejection does not report an adjustment as successful', async () => {
  const post = jest.fn(async () => {
    throw new ApiError(400);
  });
  createClient(undefined, post);
  const view = await setup();

  await view.findByText('Oat milk');
  await fireEvent.press(
    view.getByRole('button', { name: 'Record adjustment for Oat milk' }),
  );
  await fireEvent.changeText(view.getByLabelText('Quantity change'), '2');
  await fireEvent.changeText(
    view.getByLabelText('Adjustment reason'),
    'Physical count',
  );
  await fireEvent.press(view.getByRole('button', { name: 'Save adjustment' }));

  expect(
    await view.findByText(/server rejected this adjustment/i),
  ).toBeVisible();
  expect(view.queryByText(/Adjustment saved by the server/)).toBeNull();
});

test('retry after a lost adjustment response reuses the same idempotency key', async () => {
  const post = jest
    .fn()
    .mockRejectedValueOnce(new NetworkError())
    .mockResolvedValueOnce({
      data: { ingredientId: balance.ingredientId, availableQuantity: 3 },
    });
  createClient(undefined, post as never);
  const view = await setup();

  await view.findByText('Oat milk');
  await fireEvent.press(
    view.getByRole('button', { name: 'Record adjustment for Oat milk' }),
  );
  await fireEvent.changeText(view.getByLabelText('Quantity change'), '2');
  await fireEvent.changeText(
    view.getByLabelText('Adjustment reason'),
    'Physical count',
  );
  await fireEvent.press(view.getByRole('button', { name: 'Save adjustment' }));
  await view.findByText(/could not be confirmed/);
  await fireEvent.press(view.getByRole('button', { name: 'Save adjustment' }));

  await view.findByText(/Adjustment saved by the server/);
  expect(post).toHaveBeenCalledTimes(2);
  expect(post.mock.calls[0]?.[1]).toEqual(post.mock.calls[1]?.[1]);
});

test('reviews and posts a prepared receipt once, then shows its server status', async () => {
  let posted = false;
  const get = jest.fn(async (path: string) => {
    if (path === '/api/v1/purchase-receipts')
      return {
        data: [{ ...receipt, status: posted ? 'posted' : 'draft' }],
      };
    if (path === '/api/v1/purchase-receipts/{id}')
      return { data: { ...receipt, status: posted ? 'posted' : 'draft' } };
    return {
      data: { items: [balance], page: 1, pageSize: 100, totalCount: 1 },
    };
  });
  const post = jest.fn(async () => {
    posted = true;
    return { data: undefined, response: { ok: true } };
  });
  createClient(get as never, post as never);
  const view = await setup();

  await view.findByText('Oat milk');
  await fireEvent.press(
    view.getByRole('button', { name: 'Purchase receipts' }),
  );
  await view.findByText('RCPT-184');
  await fireEvent.press(
    view.getByRole('button', { name: 'Review receipt RCPT-184' }),
  );
  expect(await view.findByText(/Receipt line/)).toBeVisible();
  await fireEvent.press(
    view.getByRole('button', { name: 'Post receipt RCPT-184' }),
  );

  expect(
    await view.findByText('Receipt RCPT-184 was posted by the server.'),
  ).toBeVisible();
  expect(post).toHaveBeenCalledTimes(1);
  expect(post).toHaveBeenCalledWith('/api/v1/purchase-receipts/{id}/post', {
    params: { path: { id: receipt.id } },
  });
  expect(await view.findByText('Status: posted')).toBeVisible();
  expect(
    view.queryByRole('button', { name: 'Post receipt RCPT-184' }),
  ).toBeNull();
});

test('empty receipts remain unpostable', async () => {
  const emptyReceipt = { ...receipt, lines: [] };
  const get = jest.fn(async (path: string) =>
    path.includes('purchase-receipts')
      ? {
          data: path.endsWith('/{id}') ? emptyReceipt : [emptyReceipt],
        }
      : { data: { items: [balance], page: 1, pageSize: 100, totalCount: 1 } },
  );
  const post = jest.fn(async () => {
    throw new ApiError(409);
  });
  createClient(get as never, post);
  const view = await setup();

  await view.findByText('Oat milk');
  await fireEvent.press(
    view.getByRole('button', { name: 'Purchase receipts' }),
  );
  await view.findByText('RCPT-184');
  await fireEvent.press(
    view.getByRole('button', { name: 'Review receipt RCPT-184' }),
  );
  expect(
    await view.findByText('This receipt has no lines and cannot be posted.'),
  ).toBeVisible();
  expect(
    view.queryByRole('button', { name: 'Post receipt RCPT-184' }),
  ).toBeNull();
  expect(post).not.toHaveBeenCalled();
});

test('a receipt conflict refreshes state without claiming that posting succeeded', async () => {
  const get = jest.fn(async (path: string) =>
    path.includes('purchase-receipts')
      ? { data: path.endsWith('/{id}') ? receipt : [receipt] }
      : { data: { items: [balance], page: 1, pageSize: 100, totalCount: 1 } },
  );
  const post = jest.fn(async () => {
    throw new ApiError(409);
  });
  createClient(get as never, post as never);
  const view = await setup();

  await view.findByText('Oat milk');
  await fireEvent.press(
    view.getByRole('button', { name: 'Purchase receipts' }),
  );
  await view.findByText('RCPT-184');
  await fireEvent.press(
    view.getByRole('button', { name: 'Review receipt RCPT-184' }),
  );
  await fireEvent.press(
    await view.findByRole('button', { name: 'Post receipt RCPT-184' }),
  );

  expect(await view.findByText(/already posted|changed/i)).toBeVisible();
  expect(
    view.queryByText('Receipt RCPT-184 was posted by the server.'),
  ).toBeNull();
  expect(await view.findByText('Status: draft')).toBeVisible();
});

test('a receipt API error result does not claim that posting succeeded', async () => {
  const get = jest.fn(async (path: string) =>
    path.includes('purchase-receipts')
      ? { data: path.endsWith('/{id}') ? receipt : [receipt] }
      : { data: { items: [balance], page: 1, pageSize: 100, totalCount: 1 } },
  );
  const post = jest.fn(async () => ({
    error: { title: 'Conflict' },
    response: { status: 409 },
  }));
  createClient(get as never, post as never);
  const view = await setup();

  await view.findByText('Oat milk');
  await fireEvent.press(
    view.getByRole('button', { name: 'Purchase receipts' }),
  );
  await view.findByText('RCPT-184');
  await fireEvent.press(
    view.getByRole('button', { name: 'Review receipt RCPT-184' }),
  );
  await fireEvent.press(
    await view.findByRole('button', { name: 'Post receipt RCPT-184' }),
  );

  expect(await view.findByText(/already posted|changed/i)).toBeVisible();
  expect(
    view.queryByText('Receipt RCPT-184 was posted by the server.'),
  ).toBeNull();
});
