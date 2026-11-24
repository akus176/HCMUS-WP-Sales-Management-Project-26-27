import { act, fireEvent, render, waitFor } from '@testing-library/react-native';
import { QueryClient, onlineManager } from '@tanstack/react-query';
import { ApiError, NetworkError } from '../src/api/errors';
import type { components } from '../src/api/schema';
import { ConnectivityProvider } from '../src/connectivity/connectivity';
import * as network from '../src/connectivity/networkAdapter';
import type { NetworkState } from '../src/connectivity/networkAdapter';
import {
  createTicket,
  emptyService,
  lineEstimate,
  type CartLine,
  type SavedOrder,
} from '../src/features/counter/counterModel';
import { OrdersScreen } from '../src/features/orders/OrdersScreen';
import { SessionProvider } from '../src/session/context';
import type { SessionClient, StaffSession } from '../src/session/session';
import { ShellPage } from '../src/shell/controls';

jest.mock('expo-crypto', () => {
  let next = 0;
  return {
    randomUUID: () =>
      `00000000-0000-4000-8000-${String(++next).padStart(12, '0')}`,
  };
});
jest.mock('../src/connectivity/networkAdapter', () => ({
  getNetworkStateAsync: jest.fn(),
  addNetworkStateListener: jest.fn(),
}));
const user: StaffSession = {
  id: 'counter-staff',
  fullName: 'Counter Staff',
  roles: ['Cashier'],
  permissions: [
    'orders.create',
    'orders.confirm',
    'payments.record',
    'customers.manage',
    'loyalty.redeem',
    'discounts.request',
  ],
};
const item: components['schemas']['MenuItemResponse'] = {
  id: 'latte',
  name: 'Test latte',
  description: 'Fresh coffee',
  categoryId: 'coffee',
  categoryName: 'Coffee',
  isActive: true,
  sizeVariants: [
    { id: 'regular', sizeName: 'Regular', price: 5, isActive: true },
    { id: 'large', sizeName: 'Large', price: 7, isActive: true },
  ],
  compatibleAddons: [{ id: 'oat', name: 'Oat milk', price: 1, isActive: true }],
};
const line: CartLine = {
  id: 'line',
  item,
  size: item.sizeVariants[0]!,
  addons: item.compatibleAddons,
  quantity: 2,
};
const online: NetworkState = { isConnected: true, isInternetReachable: true };
let listener: (state: NetworkState) => void | Promise<void>;
const clients: QueryClient[] = [];
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
  clients.splice(0).forEach((client) => client.clear());
});

function savedOrder(id: string): SavedOrder {
  return {
    id,
    orderNumber: 'COUNTER-1',
    channel: 'takeaway',
    status: 'draft',
    subtotal: 12,
    discountTotal: 0,
    total: 12,
    tableId: null,
    tableNumber: null,
    customerId: null,
    discounts: [],
    lines: [
      {
        itemName: 'Saved latte snapshot',
        sizeName: 'Regular',
        quantity: 2,
        unitPrice: 6,
        total: 12,
        addons: ['Oat milk'],
      },
    ],
  };
}
async function setup(permissions = user.permissions) {
  let order = savedOrder('not-created');
  const get = jest.fn(async (path: string) => {
    if (path === '/api/v1/menu-items') return { data: [item] };
    if (path === '/api/v1/categories')
      return {
        data: [
          { id: 'coffee', name: 'Coffee', displayOrder: 1, isActive: true },
        ],
      };
    if (path === '/api/v1/pos/tables')
      return {
        data: [
          {
            id: 'table',
            tableNumber: 'T01',
            seats: 2,
            isOccupied: false,
            activeOrderId: null,
          },
        ],
      };
    if (path === '/api/v1/orders/{id}') return { data: order };
    if (path === '/api/v1/orders')
      return {
        data: [
          {
            id: order.id,
            orderNumber: order.orderNumber,
            status: order.status,
            total: order.total,
            paidAmount: 0,
            channel: order.channel,
            tableNumber: null,
            deliveryStatus: null,
            assignedStaffId: null,
            pendingDiscountIds: [],
          },
        ],
      };
    if (path === '/api/v1/customers') return { data: [] };
    throw new Error(`Unexpected read: ${path}`);
  });
  const post = jest
    .fn<
      Promise<{ data: unknown }>,
      [string, { body: Record<string, unknown> }]
    >()
    .mockImplementation(async (path, options) => {
      if (path === '/api/v1/orders') {
        order = savedOrder(String(options.body.orderId));
        return { data: order };
      }
      if (path === '/api/v1/orders/{id}/confirm') {
        order = { ...order, status: 'confirmed' };
        return { data: order };
      }
      throw new Error(`Unexpected write: ${path}`);
    });
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
  const queries = new QueryClient({
    defaultOptions: {
      queries: { retry: false, staleTime: Infinity, gcTime: Infinity },
      mutations: { retry: false, gcTime: 0 },
    },
  });
  clients.push(queries);
  const view = await render(
    <SessionProvider client={client} queryClient={queries}>
      <ConnectivityProvider>
        <ShellPage>
          <OrdersScreen user={session.user} startWithMenu />
        </ShellPage>
      </ConnectivityProvider>
    </SessionProvider>,
  );
  await view.findByRole('button', { name: 'Customize Test latte' });
  await waitFor(() =>
    expect(
      view.getByRole('button', { name: 'Customize Test latte' }),
    ).toBeEnabled(),
  );
  return {
    view,
    get,
    post,
    queries,
    order: () => order,
    setOrder: (next: SavedOrder) => {
      order = next;
    },
  };
}
type Harness = Awaited<ReturnType<typeof setup>>;
async function addAndReview(h: Harness) {
  await fireEvent.press(
    h.view.getByRole('button', { name: 'Customize Test latte' }),
  );
  await fireEvent.press(
    h.view.getByRole('button', { name: 'Add-on Oat milk' }),
  );
  await fireEvent.changeText(h.view.getByLabelText('Item quantity'), '2');
  await fireEvent.press(h.view.getByRole('button', { name: 'Add to ticket' }));
  await fireEvent.press(
    h.view.getByRole('button', { name: 'View Counter ticket' }),
  );
  await h.view.findByRole('button', { name: 'Save and review' });
}
async function save(h: Harness) {
  await addAndReview(h);
  await fireEvent.press(
    h.view.getByRole('button', { name: 'Save and review' }),
  );
  await h.view.findByText(
    'Saved by the service. Item details and prices are now locked.',
  );
  await waitFor(() =>
    expect(h.view.getByRole('button', { name: 'Confirm order' })).toBeEnabled(),
  );
}

test('ticket requests isolate service-specific data and keep identical menu lines independent', () => {
  const form = {
    ...emptyService(),
    tableId: 'old-table',
    customerAddressId: 'old-address',
    recipientName: 'Old recipient',
    contactNumber: '123',
    addressLine: 'Old address',
  };
  const takeaway = createTicket('ticket', form, [
    line,
    { ...line, id: 'second' },
  ]);
  expect(takeaway).toEqual({
    orderId: 'ticket',
    channel: 'takeaway',
    tableId: null,
    customer: null,
    lines: [
      {
        menuItemId: 'latte',
        sizeVariantId: 'regular',
        quantity: 2,
        addonIds: ['oat'],
      },
      {
        menuItemId: 'latte',
        sizeVariantId: 'regular',
        quantity: 2,
        addonIds: ['oat'],
      },
    ],
  });
  expect(lineEstimate(line)).toBe(12);
  expect(
    createTicket('ticket', { ...form, channel: 'dine_in' }, [line]).tableId,
  ).toBe('old-table');
  expect(
    createTicket('ticket', { ...form, channel: 'delivery' }, [line]).customer,
  ).toMatchObject({
    recipientName: 'Old recipient',
    customerAddressId: 'old-address',
  });
  expect(() => createTicket('ticket', emptyService(), [])).toThrow(
    'Add at least one',
  );
  expect(() =>
    createTicket('ticket', { ...emptyService(), channel: 'dine_in' }, [line]),
  ).toThrow('Choose an available table');
  expect(() =>
    createTicket('ticket', { ...emptyService(), channel: 'delivery' }, [line]),
  ).toThrow('delivery recipient');
  expect(() =>
    createTicket('ticket', emptyService(), [{ ...line, quantity: 1.2 }]),
  ).toThrow('whole-number');
});

test('canceling customization leaves the cart empty and sends no write', async () => {
  const h = await setup();
  await fireEvent.press(
    h.view.getByRole('button', { name: 'Customize Test latte' }),
  );
  await fireEvent.press(h.view.getByRole('button', { name: 'Size Large' }));
  await fireEvent.press(
    h.view.getByRole('button', { name: 'Cancel customization' }),
  );
  await fireEvent.press(
    h.view.getByRole('button', { name: 'View Counter ticket' }),
  );
  expect(
    await h.view.findByText(
      'Your ticket is empty. Add drinks or food from the menu.',
    ),
  ).toBeVisible();
  expect(h.post).not.toHaveBeenCalled();
});

test('cart and service survive menu, Orders and Tables switches without reloading warm data', async () => {
  const h = await setup();
  await addAndReview(h);
  await fireEvent.press(h.view.getByRole('button', { name: 'Delivery order' }));
  await fireEvent.changeText(
    h.view.getByLabelText('Delivery recipient'),
    'Test recipient',
  );
  await fireEvent.press(
    h.view.getByRole('button', { name: 'Back to Counter menu' }),
  );
  await fireEvent.press(h.view.getByRole('button', { name: 'Orders' }));
  await h.view.findByRole('button', { name: 'Open order COUNTER-1' });
  await fireEvent.press(h.view.getByRole('button', { name: 'Tables' }));
  await h.view.findByText('Table T01 · 2 seats · Available');
  const loaded = h.get.mock.calls.length;
  await fireEvent.press(h.view.getByRole('button', { name: 'New sale' }));
  await h.view.findByRole('button', { name: 'Customize Test latte' });
  await fireEvent.press(
    h.view.getByRole('button', { name: 'View Counter ticket' }),
  );
  expect(h.view.getByLabelText('Delivery recipient')).toHaveDisplayValue(
    'Test recipient',
  );
  expect(h.view.getByText('2 × 6.00 · 12.00')).toBeVisible();
  expect(h.get).toHaveBeenCalledTimes(loaded);
  expect(h.post).not.toHaveBeenCalled();
});

test('save uses server snapshots; canceled confirmation sends nothing; confirm submits reviewed total', async () => {
  const h = await setup();
  await save(h);
  expect(h.post.mock.calls[0]![1].body).toMatchObject({
    channel: 'takeaway',
    lines: [
      {
        menuItemId: 'latte',
        sizeVariantId: 'regular',
        quantity: 2,
        addonIds: ['oat'],
      },
    ],
  });
  expect(h.view.getByText('2 × Saved latte snapshot')).toBeVisible();
  expect(h.view.queryByRole('button', { name: 'Record payment' })).toBeNull();
  await fireEvent.press(h.view.getByRole('button', { name: 'Confirm order' }));
  await fireEvent.press(
    h.view.getByRole('button', { name: 'Keep reviewing ticket' }),
  );
  expect(h.post).toHaveBeenCalledTimes(1);
  await fireEvent.press(h.view.getByRole('button', { name: 'Confirm order' }));
  await fireEvent.press(
    h.view.getByRole('button', { name: 'Confirm reviewed order' }),
  );
  await waitFor(() => expect(h.order().status).toBe('confirmed'));
  expect(h.post.mock.calls[1]).toEqual([
    '/api/v1/orders/{id}/confirm',
    { params: { path: { id: h.order().id } }, body: { reviewedTotal: 12 } },
  ]);
  await h.view.findByRole('button', { name: 'Record payment' });
});

test('lost create response freezes the cart and explicitly retries the same ticket identity and body', async () => {
  const h = await setup();
  h.post.mockRejectedValueOnce(new NetworkError());
  await addAndReview(h);
  await fireEvent.press(
    h.view.getByRole('button', { name: 'Save and review' }),
  );
  const retry = await h.view.findByRole('button', {
    name: 'Retry same ticket save',
  });
  await waitFor(() => expect(retry).toBeEnabled());
  expect(
    h.view.getByRole('button', { name: 'Increase quantity of Test latte' }),
  ).toBeDisabled();
  expect(
    h.view.getByRole('button', { name: 'Discard unsaved ticket' }),
  ).toBeDisabled();
  await fireEvent.press(retry);
  await h.view.findByText(
    'Saved by the service. Item details and prices are now locked.',
  );
  expect(h.post.mock.calls[1]![1].body).toEqual(h.post.mock.calls[0]![1].body);
});

test('a competing total update prevents confirming an older review', async () => {
  const h = await setup();
  await save(h);
  await fireEvent.press(h.view.getByRole('button', { name: 'Confirm order' }));
  const updated = { ...h.order(), discountTotal: 2, total: 10 };
  h.setOrder(updated);
  await act(async () => {
    h.queries.setQueryData(['sales', user.id, 'order', updated.id], updated);
  });
  await h.view.findByText(
    'The ticket total changed. Close this review and check the new total.',
  );
  expect(
    h.view.getByRole('button', { name: 'Confirm reviewed order' }),
  ).toBeDisabled();
  await fireEvent.press(
    h.view.getByRole('button', { name: 'Confirm reviewed order' }),
  );
  expect(h.post).toHaveBeenCalledTimes(1);
});

test('discount and loyalty reviews can be canceled without submitting adjustments', async () => {
  const h = await setup();
  await save(h);
  const withCustomer = { ...h.order(), customerId: 'customer' };
  h.setOrder(withCustomer);
  await act(async () => {
    h.queries.setQueryData(
      ['sales', user.id, 'order', withCustomer.id],
      withCustomer,
    );
  });
  await fireEvent.press(
    h.view.getByRole('button', { name: 'Request discount' }),
  );
  await fireEvent.changeText(h.view.getByLabelText('Discount value'), '10');
  await fireEvent.changeText(
    h.view.getByLabelText('Discount reason'),
    'Service recovery',
  );
  await fireEvent.press(
    h.view.getByRole('button', { name: 'Review adjustment' }),
  );
  await fireEvent.press(
    h.view.getByRole('button', { name: 'Keep reviewing ticket' }),
  );
  await fireEvent.press(
    h.view.getByRole('button', { name: 'Redeem loyalty points' }),
  );
  await fireEvent.changeText(
    h.view.getByLabelText('Loyalty points to redeem'),
    '5',
  );
  await fireEvent.press(
    h.view.getByRole('button', { name: 'Review adjustment' }),
  );
  await fireEvent.press(
    h.view.getByRole('button', { name: 'Keep reviewing ticket' }),
  );
  expect(h.post).toHaveBeenCalledTimes(1);
});

test('offline and missing confirm permission prevent writes, and reconnect never replays', async () => {
  const h = await setup(['orders.create']);
  await addAndReview(h);
  await act(async () =>
    listener({ isConnected: false, isInternetReachable: false }),
  );
  expect(
    h.view.getByRole('button', { name: 'Save and review' }),
  ).toBeDisabled();
  await fireEvent.press(
    h.view.getByRole('button', { name: 'Save and review' }),
  );
  expect(h.post).not.toHaveBeenCalled();
  await act(async () => listener(online));
  await waitFor(() =>
    expect(
      h.view.getByRole('button', { name: 'Save and review' }),
    ).toBeEnabled(),
  );
  expect(h.post).not.toHaveBeenCalled();
  await fireEvent.press(
    h.view.getByRole('button', { name: 'Save and review' }),
  );
  await h.view.findByText(
    'Saved by the service. Item details and prices are now locked.',
  );
  expect(h.view.queryByRole('button', { name: 'Confirm order' })).toBeNull();
  expect(h.view.queryByRole('button', { name: 'Request discount' })).toBeNull();
});

test('a rejected save unlocks editing and displays the server validation detail', async () => {
  const h = await setup();
  h.post.mockRejectedValueOnce(
    new ApiError(422, { detail: 'This size is no longer available.' }),
  );
  await addAndReview(h);
  await fireEvent.press(
    h.view.getByRole('button', { name: 'Save and review' }),
  );
  await h.view.findByText('This size is no longer available.');
  await waitFor(() =>
    expect(
      h.view.getByRole('button', { name: 'Increase quantity of Test latte' }),
    ).toBeEnabled(),
  );
  expect(
    h.view.queryByRole('button', { name: 'Retry same ticket save' }),
  ).toBeNull();
});

test('a lost confirmation response reconciles the committed status without submitting a second confirm', async () => {
  const h = await setup();
  await save(h);
  h.post.mockImplementationOnce(async () => {
    h.setOrder({ ...h.order(), status: 'confirmed' });
    throw new NetworkError();
  });
  await fireEvent.press(h.view.getByRole('button', { name: 'Confirm order' }));
  await fireEvent.press(
    h.view.getByRole('button', { name: 'Confirm reviewed order' }),
  );
  await h.view.findByText('Status: confirmed · takeaway');
  await waitFor(() =>
    expect(
      h.view.queryByRole('button', { name: 'Retry same Counter action' }),
    ).toBeNull(),
  );
  expect(
    h.post.mock.calls.filter(([path]) => path.endsWith('/confirm')),
  ).toHaveLength(1);
  await waitFor(() =>
    expect(
      h.view.getByRole('button', { name: 'Start new Counter ticket' }),
    ).toBeEnabled(),
  );
  await fireEvent.press(
    h.view.getByRole('button', { name: 'Start new Counter ticket' }),
  );
  await h.view.findByRole('button', { name: 'Customize Test latte' });
  await fireEvent.press(
    h.view.getByRole('button', { name: 'View Counter ticket' }),
  );
  await h.view.findByText(
    'Your ticket is empty. Add drinks or food from the menu.',
  );
  expect(h.post).toHaveBeenCalledTimes(2);
});
