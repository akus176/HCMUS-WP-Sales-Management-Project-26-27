import type { ReactNode } from 'react';
import {
  QueryClient,
  QueryClientProvider,
  onlineManager,
  useQuery,
} from '@tanstack/react-query';
import {
  act,
  fireEvent,
  render,
  screen,
  waitFor,
} from '@testing-library/react-native';
import { useEffect, useRef } from 'react';
import { Text } from 'react-native';

import { ApiError } from '../src/api/errors';
import {
  ConnectivityProvider,
  useSafeMutation,
} from '../src/connectivity/connectivity';
import * as networkAdapter from '../src/connectivity/networkAdapter';
import type { NetworkState } from '../src/connectivity/networkAdapter';
import { ShellButton } from '../src/shell/controls';
import { ConnectivityBanner } from '../src/connectivity/ConnectivityBanner';

jest.mock('../src/connectivity/networkAdapter', () => ({
  addNetworkStateListener: jest.fn(),
  getNetworkStateAsync: jest.fn(),
}));

const offlineState = {
  isConnected: false,
  isInternetReachable: false,
} as NetworkState;
const onlineState = {
  isConnected: true,
  isInternetReachable: true,
} as NetworkState;

let networkListener:
  ((state: NetworkState) => void | Promise<void>) | undefined;
const getNetworkStateAsync = jest.mocked(networkAdapter.getNetworkStateAsync);
const addNetworkStateListener = jest.mocked(
  networkAdapter.addNetworkStateListener,
);

function makeClient() {
  return new QueryClient({
    defaultOptions: {
      queries: { retry: false, gcTime: Infinity },
      mutations: { retry: false, gcTime: Infinity },
    },
  });
}

function wrapperFor(client: QueryClient) {
  return function Wrapper({ children }: { children: ReactNode }) {
    return (
      <QueryClientProvider client={client}>
        <ConnectivityProvider>{children}</ConnectivityProvider>
      </QueryClientProvider>
    );
  };
}

async function emitNetworkState(state: NetworkState) {
  await act(async () => {
    await networkListener?.(state);
  });
}

function MutationProbe({
  mutationFn,
  attemptWhen,
}: {
  mutationFn: (value: string) => Promise<unknown>;
  attemptWhen?: 'offline' | 'reconnecting';
}) {
  const { status, mutateAsync, error, isWriteDisabled } = useSafeMutation({
    mutationFn,
  });
  const attempted = useRef(false);
  useEffect(() => {
    if (attemptWhen === status && !attempted.current) {
      attempted.current = true;
      void mutateAsync('fixture write').catch(() => {});
    }
  }, [attemptWhen, mutateAsync, status]);
  return (
    <>
      <Text>{status}</Text>
      {error && <Text>{error.message}</Text>}
      <ShellButton
        label="Try write"
        disabled={isWriteDisabled}
        onPress={() => void mutateAsync('fixture write').catch(() => {})}
      />
    </>
  );
}

beforeEach(() => {
  networkListener = undefined;
  onlineManager.setOnline(true);
  getNetworkStateAsync.mockResolvedValue(offlineState);
  addNetworkStateListener.mockImplementation((listener) => {
    networkListener = listener;
    return { remove: jest.fn() };
  });
});

afterEach(() => {
  onlineManager.setOnline(true);
});

test('reports offline status and exposes a manual connection check', async () => {
  const client = makeClient();
  const view = await render(<ConnectivityBanner />, {
    wrapper: wrapperFor(client),
  });

  expect(
    await view.findByText(/Offline\. Cached data is read-only/),
  ).toBeVisible();
  expect(onlineManager.isOnline()).toBe(false);
  expect(
    screen.getByRole('button', { name: 'Check connection' }),
  ).toBeEnabled();
});

test('reconnect restores active server queries and reports the connected state', async () => {
  onlineManager.setOnline(false);
  const client = makeClient();
  const fetchServerState = jest.fn(async () => 'current server value');
  function QueryProbe() {
    useQuery({ queryKey: ['connectivity-probe'], queryFn: fetchServerState });
    return null;
  }
  await render(
    <>
      <ConnectivityBanner />
      <QueryProbe />
    </>,
    { wrapper: wrapperFor(client) },
  );
  expect(await screen.findByText(/Offline/)).toBeVisible();
  expect(fetchServerState).not.toHaveBeenCalled();

  await emitNetworkState(onlineState);

  expect(await screen.findByText(/Online/)).toBeVisible();
  await waitFor(() => expect(fetchServerState).toHaveBeenCalledTimes(1));
  expect(onlineManager.isOnline()).toBe(true);
});

test('manual refresh refetches active queries from the server', async () => {
  onlineManager.setOnline(false);
  const client = makeClient();
  getNetworkStateAsync.mockResolvedValue(onlineState);
  const fetchServerState = jest.fn(async () => 'authoritative value');
  function QueryProbe() {
    useQuery({ queryKey: ['manual-refresh-probe'], queryFn: fetchServerState });
    return null;
  }
  await render(
    <>
      <ConnectivityBanner />
      <QueryProbe />
    </>,
    { wrapper: wrapperFor(client) },
  );
  expect(await screen.findByText(/Online/)).toBeVisible();
  await waitFor(() => expect(fetchServerState).toHaveBeenCalledTimes(1));

  await fireEvent.press(screen.getByRole('button', { name: 'Refresh data' }));

  await waitFor(() => expect(fetchServerState).toHaveBeenCalledTimes(2));
  expect(await screen.findByText(/Online/)).toBeVisible();
});

test('offline mutation rejects immediately and is never replayed after reconnect', async () => {
  onlineManager.setOnline(false);
  const client = makeClient();
  const mutationFn = jest.fn(async (value: string) => value);
  await render(
    <MutationProbe mutationFn={mutationFn} attemptWhen="offline" />,
    { wrapper: wrapperFor(client) },
  );
  expect(await screen.findByText(/No change was sent or saved/)).toBeVisible();
  expect(
    screen.getByRole('button', { name: 'Try write', disabled: true }),
  ).toBeVisible();
  expect(mutationFn).not.toHaveBeenCalled();

  await emitNetworkState(onlineState);
  expect(await screen.findByText('online')).toBeVisible();
  expect(mutationFn).not.toHaveBeenCalled();
});

test.each([
  { status: 422, message: 'Check the submitted information and try again.' },
  { status: 403, message: 'You do not have permission for this action.' },
  {
    status: 409,
    message: 'This record has changed. Refresh before trying again.',
  },
])(
  'keeps server data authoritative for API rejection $status and allows a deliberate retry',
  async ({ status, message }) => {
    onlineManager.setOnline(false);
    const client = makeClient();
    getNetworkStateAsync.mockResolvedValue(onlineState);
    let serverValue = 'server version 1';
    const write = jest
      .fn<Promise<void>, []>()
      .mockRejectedValueOnce(new ApiError(status))
      .mockImplementationOnce(async () => {
        serverValue = 'server version 2';
      });
    function ServerRecord() {
      const query = useQuery({
        queryKey: ['server-authority'],
        queryFn: async () => serverValue,
      });
      const mutation = useSafeMutation({ mutationFn: write });
      return (
        <>
          <ConnectivityBanner />
          {query.data && <Text>{query.data}</Text>}
          <ShellButton
            label="Save record"
            disabled={mutation.isWriteDisabled || mutation.isPending}
            onPress={() => mutation.mutate()}
          />
          {mutation.error && <Text>{mutation.error.message}</Text>}
        </>
      );
    }
    await render(<ServerRecord />, { wrapper: wrapperFor(client) });
    expect(await screen.findByText('server version 1')).toBeVisible();

    await fireEvent.press(screen.getByRole('button', { name: 'Save record' }));
    expect(await screen.findByText(message)).toBeVisible();
    expect(screen.getByText('server version 1')).toBeVisible();
    expect(write).toHaveBeenCalledTimes(1);

    await fireEvent.press(screen.getByRole('button', { name: 'Save record' }));
    expect(await screen.findByText('server version 2')).toBeVisible();
    expect(write).toHaveBeenCalledTimes(2);
  },
);

test('manual connection check moves offline state through reconnecting and refreshes data', async () => {
  onlineManager.setOnline(false);
  const client = makeClient();
  const fetchServerState = jest.fn(async () => 'server is reachable');
  function QueryProbe() {
    useQuery({ queryKey: ['retry-probe'], queryFn: fetchServerState });
    return null;
  }
  await render(
    <>
      <ConnectivityBanner />
      <QueryProbe />
    </>,
    { wrapper: wrapperFor(client) },
  );
  expect(await screen.findByText(/Offline/)).toBeVisible();
  getNetworkStateAsync.mockResolvedValueOnce(onlineState);

  await fireEvent.press(
    screen.getByRole('button', { name: 'Check connection' }),
  );

  expect(await screen.findByText(/Online/)).toBeVisible();
  await waitFor(() => expect(fetchServerState).toHaveBeenCalled());
});

test('blocks mutations during reconnect and never replays them when refresh finishes', async () => {
  onlineManager.setOnline(false);
  const client = makeClient();
  const mutationFn = jest.fn(async () => 'saved');
  let releaseQuery!: (value: string) => void;
  const pendingQuery = new Promise<string>((resolve) => {
    releaseQuery = resolve;
  });
  function QueryProbe() {
    useQuery({ queryKey: ['reconnecting'], queryFn: () => pendingQuery });
    return null;
  }
  await render(
    <>
      <ConnectivityBanner />
      <MutationProbe mutationFn={mutationFn} attemptWhen="reconnecting" />
      <QueryProbe />
    </>,
    { wrapper: wrapperFor(client) },
  );
  await waitFor(() => expect(screen.getByText('offline')).toBeVisible());
  getNetworkStateAsync.mockResolvedValueOnce(onlineState);
  // Trigger a connectivity refresh through the same visible control as users.
  await fireEvent.press(
    screen.getByRole('button', { name: 'Check connection' }),
  );
  expect(await screen.findByText(/No change was sent or saved/)).toBeVisible();
  expect(await screen.findByText('online')).toBeVisible();
  expect(mutationFn).not.toHaveBeenCalled();

  await act(async () => {
    releaseQuery('refreshed');
  });
  expect(await screen.findByText('online')).toBeVisible();
  expect(mutationFn).not.toHaveBeenCalled();
});
