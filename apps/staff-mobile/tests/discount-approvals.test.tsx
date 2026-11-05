import { act, fireEvent, render, waitFor } from '@testing-library/react-native';
import {
  QueryClient,
  onlineManager,
  useQueryClient,
} from '@tanstack/react-query';
import { useEffect } from 'react';
import type { components } from '../src/api/schema';
import { ApiError } from '../src/api/errors';
import { router } from 'expo-router';
import { ConnectivityProvider } from '../src/connectivity/connectivity';
import { ConnectivityBanner } from '../src/connectivity/ConnectivityBanner';
import * as networkAdapter from '../src/connectivity/networkAdapter';
import type { NetworkState } from '../src/connectivity/networkAdapter';
import type { SessionClient, StaffSession } from '../src/session/session';
import { DiscountApprovals } from '../src/features/approvals/DiscountApprovals';
import { ShellPage } from '../src/shell/controls';
import { SessionProvider } from '../src/session/context';

jest.mock('../src/connectivity/networkAdapter', () => ({
  addNetworkStateListener: jest.fn(),
  getNetworkStateAsync: jest.fn(),
}));

type PendingDiscount = components['schemas']['PendingDiscountResponse'];
type Page = components['schemas']['PageResponseOfPendingDiscountResponse'];

const manager: StaffSession = {
  id: '00000000-0000-0000-0000-000000000156',
  fullName: 'Morgan Manager',
  roles: ['Manager'],
  permissions: ['discounts.approve'],
};
const managerSession = { status: 'signedIn' as const, user: manager };
const discount: PendingDiscount = {
  discountId: '00000000-0000-0000-0000-000000000011',
  orderId: '00000000-0000-0000-0000-000000000012',
  orderNumber: 'ORD-156',
  type: 'percentage',
  value: 10,
  reason: 'Service recovery',
  computedAmount: 300,
  orderTotal: 3000,
};
const page: Page = { items: [discount], page: 1, pageSize: 100, totalCount: 1 };
const approved = {
  discountId: discount.discountId,
  status: 'approved',
  computedAmount: 300,
  discountTotal: 300,
  orderTotal: 2700,
};

const onlineState: NetworkState = {
  isConnected: true,
  isInternetReachable: true,
};
const offlineState: NetworkState = {
  isConnected: false,
  isInternetReachable: false,
};
let networkListener:
  ((state: NetworkState) => void | Promise<void>) | undefined;
const getNetworkStateAsync = jest.mocked(networkAdapter.getNetworkStateAsync);
const addNetworkStateListener = jest.mocked(
  networkAdapter.addNetworkStateListener,
);

function createNetworkSource(initial: NetworkState) {
  return {
    initial,
    emit: async (state: NetworkState) => {
      await act(async () => networkListener?.(state));
    },
  };
}

function mockClient({
  get = jest.fn(async () => ({ data: page })),
  post = jest.fn(async () => ({ data: approved })),
}: {
  get?: jest.Mock;
  post?: jest.Mock;
} = {}) {
  const client = {
    api: { GET: get, POST: post },
    getSnapshot: () => managerSession,
    subscribe: () => () => {},
    restore: async () => {},
  } as unknown as SessionClient;
  currentClient = client;
  return { client, get, post };
}

let currentClient: SessionClient;

function ClearQueriesOnUnmount() {
  const queryClient = useQueryClient();
  useEffect(() => () => queryClient.clear(), [queryClient]);
  return null;
}

async function setup(
  source: ReturnType<typeof createNetworkSource>,
  screenContent: React.ReactElement,
) {
  getNetworkStateAsync.mockResolvedValue(source.initial);
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
  return render(screenContent, { wrapper: Wrapper });
}

beforeEach(() => {
  jest.restoreAllMocks();
  networkListener = undefined;
  onlineManager.setOnline(true);
  getNetworkStateAsync.mockReset().mockResolvedValue(onlineState);
  addNetworkStateListener.mockReset().mockImplementation((listener) => {
    networkListener = listener;
    return { remove: jest.fn() };
  });
  jest.spyOn(router, 'back').mockImplementation(() => {});
  jest.spyOn(router, 'push').mockImplementation(() => {});
  jest.spyOn(router, 'replace').mockImplementation(() => {});
});

test('loads pending discounts for an authorized manager and opens a review', async () => {
  const { get } = mockClient();
  const view = await setup(
    createNetworkSource(onlineState),
    <DiscountApprovals user={manager} />,
  );

  await view.findByText('Order ORD-156');
  expect(get).toHaveBeenCalledWith(
    '/api/v1/discounts/pending',
    expect.objectContaining({ params: { query: { page: 1, pageSize: 100 } } }),
  );
  await fireEvent.press(
    view.getByRole('button', { name: 'Review order ORD-156 discount' }),
  );
  expect(router.push).toHaveBeenCalledWith({
    pathname: '/modules/approvals/[discountId]',
    params: { discountId: discount.discountId },
  });
});

test('shows the server-confirmed approved decision and updated order total', async () => {
  const { post } = mockClient();
  const view = await setup(
    createNetworkSource(onlineState),
    <DiscountApprovals user={manager} discountId={discount.discountId} />,
  );

  await view.findByText('Reason: Service recovery');
  await fireEvent.press(view.getByRole('button', { name: 'Approve discount' }));

  expect(await view.findByText(/Approved by the server.*2\.700/)).toBeVisible();
  expect(post).toHaveBeenCalledWith('/api/v1/discounts/{id}/approve', {
    params: { path: { id: discount.discountId } },
  });
  expect(view.queryByRole('button', { name: 'Approve discount' })).toBeNull();
});

test('shows the server-confirmed rejected decision without changing the total', async () => {
  const rejected = { ...approved, status: 'rejected', orderTotal: 3000 };
  const post = jest.fn(async () => ({ data: rejected }));
  mockClient({ post });
  const view = await setup(
    createNetworkSource(onlineState),
    <DiscountApprovals user={manager} discountId={discount.discountId} />,
  );

  await view.findByText('Reason: Service recovery');
  await fireEvent.press(view.getByRole('button', { name: 'Reject discount' }));

  expect(await view.findByText(/Rejected by the server.*3\.000/)).toBeVisible();
  expect(post).toHaveBeenCalledWith('/api/v1/discounts/{id}/reject', {
    params: { path: { id: discount.discountId } },
  });
});

test('explains a decision conflict and refreshes without claiming success', async () => {
  const post = jest.fn(async () => {
    throw new ApiError(409);
  });
  const { get } = mockClient({ post });
  const view = await setup(
    createNetworkSource(onlineState),
    <ShellPage>
      <DiscountApprovals user={manager} discountId={discount.discountId} />
    </ShellPage>,
  );

  await view.findByText('Reason: Service recovery');
  await fireEvent.press(view.getByRole('button', { name: 'Approve discount' }));

  expect(await view.findByText(/changed before your decision/i)).toBeVisible();
  expect(view.getByText('Reason: Service recovery')).toBeVisible();
  expect(view.queryByText(/Approved by the server/)).toBeNull();
  await waitFor(() => expect(get).toHaveBeenCalledTimes(2));
});

test('hides cached approval details when the server denies access', async () => {
  const get = jest.fn(async () => {
    throw new ApiError(403);
  });
  mockClient({ get });
  const view = await setup(
    createNetworkSource(onlineState),
    <DiscountApprovals user={manager} />,
  );

  expect(await view.findByText('Access changed')).toBeVisible();
  expect(view.queryByText('Order ORD-156')).toBeNull();
});

test('shows cached requests offline but disables decisions and sends no write', async () => {
  const post = jest.fn(async () => ({ data: approved }));
  const { get } = mockClient({ post });
  const source = createNetworkSource(onlineState);
  const view = await setup(
    source,
    <ShellPage>
      <DiscountApprovals user={manager} />
    </ShellPage>,
  );

  await view.findByText('Order ORD-156');
  await source.emit(offlineState);
  await view.findByText(/Offline\. Cached data is read-only/);
  view.rerender(
    <ShellPage>
      <DiscountApprovals user={manager} discountId={discount.discountId} />
    </ShellPage>,
  );
  expect(await view.findByText('Reason: Service recovery')).toBeVisible();
  expect(
    await view.findByText(/Offline\. Cached data is read-only/),
  ).toBeVisible();
  const approve = view.getByRole('button', { name: 'Approve discount' });
  expect(approve).toBeDisabled();
  expect(get).toHaveBeenCalledTimes(1);
  await fireEvent.press(approve);
  expect(post).not.toHaveBeenCalled();
});
