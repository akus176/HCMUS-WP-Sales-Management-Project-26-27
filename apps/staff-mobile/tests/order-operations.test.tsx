import { act, fireEvent, render, waitFor } from '@testing-library/react-native';
import { ScrollView } from 'react-native';
import { QueryClient, onlineManager } from '@tanstack/react-query';
import { ApiError, NetworkError } from '../src/api/errors';
import type { components } from '../src/api/schema';
import { ConnectivityProvider } from '../src/connectivity/connectivity';
import * as network from '../src/connectivity/networkAdapter';
import type { NetworkState } from '../src/connectivity/networkAdapter';
import { OrdersScreen } from '../src/features/orders/OrdersScreen';
import { SessionProvider } from '../src/session/context';
import type { SessionClient, StaffSession } from '../src/session/session';
import { ShellPage } from '../src/shell/controls';

jest.mock('expo-crypto', () => ({
  randomUUID: () => '00000000-0000-4000-8000-000000000182',
}));
jest.mock('../src/connectivity/networkAdapter', () => ({
  getNetworkStateAsync: jest.fn(),
  addNetworkStateListener: jest.fn(),
}));

const user: StaffSession = {
  id: 'staff-182',
  fullName: 'Test Staff',
  roles: ['Manager'],
  permissions: [
    'orders.create',
    'payments.record',
    'orders.cancel',
    'delivery.assign',
  ],
};
const summary: components['schemas']['OperationalOrderResponse'] = {
  id: 'order-182',
  orderNumber: 'ORD-182',
  channel: 'dine_in',
  status: 'confirmed',
  total: 10,
  paidAmount: 0,
  tableNumber: 'T-182',
  assignedStaffId: null,
  deliveryStatus: null,
  pendingDiscountIds: [],
};
const detail: components['schemas']['OrderResponse'] = {
  id: summary.id,
  orderNumber: summary.orderNumber,
  channel: summary.channel,
  status: summary.status,
  total: 10,
  subtotal: 10,
  discountTotal: 0,
  tableNumber: summary.tableNumber,
  tableId: 'table-182',
  customerId: null,
  discounts: [],
  lines: [
    {
      itemName: 'Test latte',
      sizeName: 'Large',
      quantity: 2,
      unitPrice: 5,
      total: 10,
      addons: ['Oat milk'],
    },
  ],
};
const online: NetworkState = { isConnected: true, isInternetReachable: true };
let listener: (state: NetworkState) => void | Promise<void>;
let queries: QueryClient[] = [];

beforeEach(() => {
  onlineManager.setOnline(true);
  jest.mocked(network.getNetworkStateAsync).mockResolvedValue(online);
  jest
    .mocked(network.addNetworkStateListener)
    .mockImplementation((callback) => {
      listener = callback;
      return { remove: jest.fn() };
    });
});
afterEach(() => {
  queries.forEach((query) => query.clear());
  queries = [];
});

async function setup({ manager = false, permissions = user.permissions } = {}) {
  let rows = [{ ...summary }];
  let order = { ...detail };
  const get = jest.fn(async (path: string) => {
    if (path === '/api/v1/orders') return { data: rows };
    if (path === '/api/v1/orders/{id}') return { data: order };
    if (path === '/api/v1/pos/tables')
      return {
        data: [
          {
            id: 'table-182',
            tableNumber: 'T-182',
            seats: 2,
            isOccupied: true,
            activeOrderId: summary.id,
          },
        ],
      };
    if (path === '/api/v1/delivery-staff/options')
      return { data: [{ id: 'courier-182', fullName: 'Test Courier' }] };
    throw new Error(`Unexpected query ${path}`);
  });
  const post = jest.fn<
    Promise<{ data?: unknown }>,
    [string, { body: Record<string, unknown> }]
  >();
  const session = {
    status: 'signedIn' as const,
    user: { ...user, permissions },
  };
  const client = {
    api: { GET: get, POST: post },
    getSnapshot: () => session,
    subscribe: () => () => {},
    restore: async () => {},
  } as unknown as SessionClient;
  const queryClient = new QueryClient({
    defaultOptions: {
      queries: { retry: false, gcTime: 0 },
      mutations: { retry: false, gcTime: 0 },
    },
  });
  queries.push(queryClient);
  const view = await render(
    <SessionProvider client={client} queryClient={queryClient}>
      <ConnectivityProvider>
        <ShellPage>
          <OrdersScreen user={session.user} manager={manager} />
        </ShellPage>
      </ConnectivityProvider>
    </SessionProvider>,
  );
  return {
    view,
    get,
    post,
    queryClient,
    setRows: (value: typeof rows) => {
      rows = value;
    },
    setOrder: (value: typeof order) => {
      order = value;
    },
  };
}

async function open(view: Awaited<ReturnType<typeof setup>>['view']) {
  await fireEvent.press(
    await view.findByRole('button', { name: 'Open order ORD-182' }),
  );
  await view.findByText('2 × Test latte · Large · 10.00');
  await waitFor(() =>
    expect(
      view.getByRole('button', { name: 'Refresh orders and tables' }),
    ).toBeEnabled(),
  );
}
async function reviewPayment(
  view: Awaited<ReturnType<typeof setup>>['view'],
  amount = '5',
) {
  await fireEvent.changeText(view.getByLabelText('Payment amount'), amount);
  await fireEvent.press(view.getByRole('button', { name: 'Review payment' }));
}

test('searches server orders by number/table, filters fulfillment and opens occupied tables', async () => {
  const { view } = await setup();
  await view.findByText('Table T-182 · 2 seats · Occupied');
  await fireEvent.changeText(view.getByLabelText('Search orders'), 't-182');
  expect(view.getByRole('button', { name: 'Open order ORD-182' })).toBeTruthy();
  expect(view.queryByRole('button', { name: 'Show ready orders' })).toBeNull();
  await fireEvent.press(
    view.getByRole('button', { name: 'Filter orders: All', expanded: false }),
  );
  await fireEvent.press(
    view.getByRole('button', { name: 'Show ready orders' }),
  );
  await view.findByText('No orders match this search.');
  expect(
    view.getByRole('button', { name: 'Filter orders: Ready', expanded: false }),
  ).toBeVisible();
  await fireEvent.press(
    view.getByRole('button', { name: 'Open table T-182 order' }),
  );
  await view.findByText('Fulfillment: confirmed');
  await view.findByText('Add-ons: Oat milk');
});

test('returns the viewport to the filter after a deeply scrolled selection collapses', async () => {
  const scrollTo = jest.spyOn(ScrollView.prototype, 'scrollTo');
  try {
    const { view } = await setup();
    await view.findByRole('button', { name: 'Open order ORD-182' });
    const scroll = view.container.queryAll(
      (node) => node.type === 'RCTScrollView',
    )[0]!;
    await fireEvent.scroll(scroll, {
      nativeEvent: { contentOffset: { x: 0, y: 400 } },
    });
    await fireEvent.press(
      view.getByRole('button', { name: 'Filter orders: All' }),
    );
    await fireEvent.scroll(scroll, {
      nativeEvent: { contentOffset: { x: 0, y: 1400 } },
    });
    await fireEvent.press(
      view.getByRole('button', { name: 'Show ready orders' }),
    );
    await fireEvent(scroll, 'contentSizeChange', 360, 2400);
    expect(scrollTo).toHaveBeenLastCalledWith({ y: 400, animated: false });
    expect(
      view.getByRole('button', {
        name: 'Filter orders: Ready',
        expanded: false,
      }),
    ).toBeVisible();
  } finally {
    scrollTo.mockRestore();
  }
});

test('shows empty lists, read failure and manual recovery', async () => {
  const { view, get, setRows } = await setup();
  await view.findByRole('button', { name: 'Open order ORD-182' });
  setRows([]);
  await fireEvent.press(
    view.getByRole('button', { name: 'Refresh orders and tables' }),
  );
  await view.findByText('No orders match this search.');
  get.mockRejectedValueOnce(new NetworkError());
  await fireEvent.press(
    view.getByRole('button', { name: 'Refresh orders and tables' }),
  );
  await view.findByText('Orders could not be loaded. Refresh to retry.');
  await fireEvent.press(
    view.getByRole('button', { name: 'Refresh orders and tables' }),
  );
  await view.findByText('No orders match this search.');
});

test('a refreshed lower balance requires payment review again before the first submission', async () => {
  const { view, post, setRows } = await setup();
  await open(view);
  await reviewPayment(view, '10');
  setRows([{ ...summary, paidAmount: 7 }]);
  await fireEvent.press(
    view.getByRole('button', { name: 'Refresh orders and tables' }),
  );
  await view.findByText('Total: 10.00 · Paid: 7.00 · Balance: 3.00');
  await waitFor(() =>
    expect(view.getByRole('button', { name: 'Confirm payment' })).toBeEnabled(),
  );
  await fireEvent.press(view.getByRole('button', { name: 'Confirm payment' }));
  await view.findByText(
    'The balance changed. Review the current amount before recording a payment.',
  );
  expect(post).not.toHaveBeenCalled();
});

test('a payment conflict refreshes the balance without retrying the write', async () => {
  const { view, post, setRows } = await setup();
  await open(view);
  await reviewPayment(view);
  post.mockImplementationOnce(async () => {
    setRows([{ ...summary, paidAmount: 10 }]);
    throw new ApiError(409);
  });
  await fireEvent.press(view.getByRole('button', { name: 'Confirm payment' }));
  await view.findByText(
    'Order changed elsewhere. Review the refreshed balance before recording a payment.',
  );
  await view.findByText('Total: 10.00 · Paid: 10.00 · Balance: 0.00');
  expect(post).toHaveBeenCalledTimes(1);
  expect(view.queryByRole('button', { name: 'Review payment' })).toBeNull();
});

test('a read-only manager route exposes no cancellation or assignment controls', async () => {
  const { view, get } = await setup({
    manager: true,
    permissions: ['orders.create', 'discounts.approve'],
  });
  await open(view);
  expect(view.queryByLabelText('Cancellation reason')).toBeNull();
  expect(view.queryByRole('button', { name: 'Review assignment' })).toBeNull();
  expect(get.mock.calls.some(([path]) => path.includes('delivery-staff'))).toBe(
    false,
  );
});

test('empty eligible-staff and failed staff reads disable assignment until refresh succeeds', async () => {
  const { view, get, setRows, setOrder } = await setup({ manager: true });
  const original = get.getMockImplementation()!;
  get.mockImplementation(async (path) =>
    path === '/api/v1/delivery-staff/options' ? { data: [] } : original(path),
  );
  setRows([
    {
      ...summary,
      status: 'ready',
      channel: 'delivery',
      deliveryStatus: 'unassigned',
    },
  ]);
  setOrder({ ...detail, status: 'ready', channel: 'delivery' });
  await fireEvent.press(
    view.getByRole('button', { name: 'Refresh orders and tables' }),
  );
  await open(view);
  await view.findByText('No active delivery staff are available.');
  expect(
    view.getByRole('button', { name: 'Review assignment' }),
  ).toBeDisabled();
  get.mockImplementation(async (path) => {
    if (path === '/api/v1/delivery-staff/options') throw new NetworkError();
    return original(path);
  });
  await fireEvent.press(
    view.getByRole('button', { name: 'Refresh orders and tables' }),
  );
  await view.findByText(
    'Eligible staff could not be loaded. Refresh before assigning.',
  );
  expect(
    view.getByRole('button', { name: 'Review assignment' }),
  ).toBeDisabled();
  get.mockImplementation(original);
  await fireEvent.press(
    view.getByRole('button', { name: 'Refresh orders and tables' }),
  );
  await view.findByRole('button', { name: 'Select Test Courier' });
});

test('guards double taps and refreshes server paid amount and balance', async () => {
  const { view, post, setRows } = await setup();
  await open(view);
  await reviewPayment(view);
  let resolve!: (value: { data: unknown }) => void;
  post.mockImplementation(
    () =>
      new Promise((done) => {
        resolve = done;
      }),
  );
  const confirm = view.getByRole('button', { name: 'Confirm payment' });
  await fireEvent.press(confirm);
  await fireEvent.press(confirm);
  expect(post).toHaveBeenCalledTimes(1);
  setRows([{ ...summary, paidAmount: 5 }]);
  await act(async () =>
    resolve({
      data: {
        paymentId: 'payment',
        paidAmount: 5,
        outstandingBalance: 5,
        isFullyPaid: false,
        alreadyRecorded: false,
      },
    }),
  );
  await view.findByText('Payment confirmed. Paid: 5.00. Balance: 5.00.');
  await view.findByText('Total: 10.00 · Paid: 5.00 · Balance: 5.00');
});

test('retains an uncertain payment across refresh and order navigation, then retries the identical command', async () => {
  const { view, post, setRows } = await setup();
  await open(view);
  post.mockImplementationOnce(async () => {
    setRows([{ ...summary, paidAmount: 5 }]);
    throw new NetworkError();
  });
  await reviewPayment(view);
  await fireEvent.press(view.getByRole('button', { name: 'Confirm payment' }));
  await view.findByText(/Payment outcome is unknown/);
  expect(view.queryByLabelText('Payment amount')).toBeNull();
  await fireEvent.press(
    view.getByRole('button', { name: 'Back to order search' }),
  );
  await open(view);
  post.mockResolvedValueOnce({
    data: { paidAmount: 5, outstandingBalance: 5, alreadyRecorded: true },
  });
  await fireEvent.press(
    view.getByRole('button', { name: 'Retry same payment' }),
  );
  await view.findByText(
    'Payment confirmed (already recorded). Paid: 5.00. Balance: 5.00.',
  );
  expect(post).toHaveBeenCalledTimes(2);
  expect(post.mock.calls[1]).toEqual(post.mock.calls[0]);
});

test.each(['0', '-1', '11', '1.001', 'NaN'])(
  'rejects invalid payment amount %s before sending',
  async (amount) => {
    const { view, post } = await setup();
    await open(view);
    await reviewPayment(view, amount);
    await view.findByText(/Enter an amount above zero/);
    expect(post).not.toHaveBeenCalled();
  },
);

test.each(['card', 'e-wallet'])(
  'requires a reference for %s and submits the supported method',
  async (method) => {
    const { view, post } = await setup();
    await open(view);
    await fireEvent.press(
      view.getByRole('button', { name: `Pay by ${method}` }),
    );
    await reviewPayment(view);
    await view.findByText(
      'Card and e-wallet payments require a payment reference.',
    );
    expect(post).not.toHaveBeenCalled();
    await fireEvent.changeText(
      view.getByLabelText('Payment reference'),
      'receipt-182',
    );
    await fireEvent.press(view.getByRole('button', { name: 'Review payment' }));
    post.mockResolvedValueOnce({
      data: { paidAmount: 5, outstandingBalance: 5 },
    });
    await fireEvent.press(
      view.getByRole('button', { name: 'Confirm payment' }),
    );
    await waitFor(() =>
      expect(post).toHaveBeenCalledWith(
        '/api/v1/orders/{id}/payments',
        expect.objectContaining({
          body: expect.objectContaining({
            method: method === 'e-wallet' ? 'e_wallet' : 'card',
            reference: 'receipt-182',
          }),
        }),
      ),
    );
  },
);

test('manager order details and cancellation review render without duplicate child keys', async () => {
  const error = jest.spyOn(console, 'error').mockImplementation(() => {});
  try {
    const { view } = await setup({ manager: true });
    await open(view);
    await fireEvent.changeText(
      view.getByLabelText('Cancellation reason'),
      'Customer changed plans',
    );
    await fireEvent.press(
      view.getByRole('button', { name: 'Review cancellation' }),
    );
    await view.findByRole('button', { name: 'Confirm cancellation' });
    const duplicateKeys = error.mock.calls.filter((args) =>
      args.some(
        (arg) => typeof arg === 'string' && /same key|unique.*key/i.test(arg),
      ),
    );
    expect(duplicateKeys).toEqual([]);
  } finally {
    error.mockRestore();
  }
});

test('requires a cancellation reason and displays the server-confirmed cancelled state', async () => {
  const { view, post, setRows, setOrder } = await setup({ manager: true });
  await open(view);
  await fireEvent.press(
    view.getByRole('button', { name: 'Review cancellation' }),
  );
  await view.findByText('Enter a cancellation reason.');
  expect(post).not.toHaveBeenCalled();
  await fireEvent.changeText(
    view.getByLabelText('Cancellation reason'),
    ' Customer changed plans ',
  );
  await fireEvent.press(
    view.getByRole('button', { name: 'Review cancellation' }),
  );
  post.mockImplementationOnce(async () => {
    setRows([{ ...summary, status: 'cancelled' }]);
    setOrder({ ...detail, status: 'cancelled' });
    return {};
  });
  await fireEvent.press(
    view.getByRole('button', { name: 'Confirm cancellation' }),
  );
  await view.findByText('Cancellation confirmed by the service.');
  await view.findByText('Status: cancelled · dine_in · Table T-182');
  expect(
    view.queryByRole('button', { name: 'Review cancellation' }),
  ).toBeNull();
  expect(post).toHaveBeenCalledWith('/api/v1/orders/{id}/cancel', {
    params: { path: { id: summary.id } },
    body: { reason: 'Customer changed plans' },
  });
});

test('assigns only server-listed eligible staff on an unassigned ready delivery', async () => {
  const { view, post, setRows, setOrder } = await setup({ manager: true });
  setRows([
    {
      ...summary,
      status: 'ready',
      channel: 'delivery',
      deliveryStatus: 'unassigned',
    },
  ]);
  setOrder({ ...detail, status: 'ready', channel: 'delivery' });
  await fireEvent.press(
    view.getByRole('button', { name: 'Refresh orders and tables' }),
  );
  await open(view);
  await fireEvent.press(
    await view.findByRole('button', { name: 'Select Test Courier' }),
  );
  await fireEvent.press(
    view.getByRole('button', { name: 'Review assignment' }),
  );
  post.mockImplementationOnce(async () => {
    setRows([
      {
        ...summary,
        status: 'ready',
        channel: 'delivery',
        deliveryStatus: 'assigned',
        assignedStaffId: 'courier-182',
      },
    ]);
    return {};
  });
  await fireEvent.press(
    view.getByRole('button', { name: 'Confirm assignment' }),
  );
  await view.findByText('Delivery assignment confirmed by the service.');
  await view.findByText('Fulfillment: assigned · Assigned staff: courier-182');
  expect(post).toHaveBeenCalledWith('/api/v1/orders/{id}/assign-delivery', {
    params: { path: { id: summary.id } },
    body: { staffId: 'courier-182' },
  });
  expect(view.queryByRole('button', { name: 'Review assignment' })).toBeNull();
});

test('a stale manager action reports conflict, refreshes and does not overwrite the newer state', async () => {
  const { view, post, setRows, setOrder } = await setup({ manager: true });
  await open(view);
  await fireEvent.changeText(
    view.getByLabelText('Cancellation reason'),
    'Stale request',
  );
  await fireEvent.press(
    view.getByRole('button', { name: 'Review cancellation' }),
  );
  post.mockImplementationOnce(async () => {
    setRows([{ ...summary, status: 'completed' }]);
    setOrder({ ...detail, status: 'completed' });
    throw new ApiError(409);
  });
  await fireEvent.press(
    view.getByRole('button', { name: 'Confirm cancellation' }),
  );
  await view.findByText(
    'Order changed elsewhere. Review the refreshed order before trying again.',
  );
  await view.findByText('Status: completed · dine_in · Table T-182');
  expect(post).toHaveBeenCalledTimes(1);
  expect(
    view.queryByRole('button', { name: 'Review cancellation' }),
  ).toBeNull();
});

test('offline payment confirmation sends and queues no mutation', async () => {
  const { view, post } = await setup();
  await open(view);
  await reviewPayment(view);
  await act(async () =>
    listener({ isConnected: false, isInternetReachable: false }),
  );
  await fireEvent.press(view.getByRole('button', { name: 'Confirm payment' }));
  expect(post).not.toHaveBeenCalled();
  await act(async () => listener(online));
  expect(post).not.toHaveBeenCalled();
});

test('read-only users never see mutations and missing order permission makes no data requests', async () => {
  const h = await setup({ permissions: ['orders.create'] });
  await open(h.view);
  expect(h.view.queryByRole('header', { name: 'Record payment' })).toBeNull();
  await h.view.unmount();
  const forbidden = await setup({ permissions: ['payments.record'] });
  await forbidden.view.findByText(/Order access unavailable/);
  expect(forbidden.get).not.toHaveBeenCalled();
});

test('forbidden payment hides every retained record immediately without waiting for a read', async () => {
  const { view, post, get } = await setup();
  await open(view);
  await reviewPayment(view);
  post.mockRejectedValueOnce(new ApiError(403));
  get.mockImplementation(() => new Promise(() => {}));
  await fireEvent.press(view.getByRole('button', { name: 'Confirm payment' }));
  await view.findByText(/Order access unavailable/);
  expect(view.queryByText('Order ORD-182')).toBeNull();
  expect(view.queryByText(/Test latte/)).toBeNull();
});

test('404 refresh removes stale detail and all actions', async () => {
  const { view, get } = await setup();
  await open(view);
  const original = get.getMockImplementation()!;
  get.mockImplementation(async (path) => {
    if (path === '/api/v1/orders/{id}') throw new ApiError(404);
    return original(path);
  });
  await fireEvent.press(
    view.getByRole('button', { name: 'Refresh orders and tables' }),
  );
  await view.findByText('Order is no longer available.');
  expect(view.queryByText(/Test latte/)).toBeNull();
  expect(view.queryByRole('button', { name: 'Review payment' })).toBeNull();
});
