import { QueryClient, QueryObserver } from '@tanstack/react-query';
import { act, render, screen, waitFor } from '@testing-library/react-native';

import { OperationalUpdates } from '../src/realtime/OperationalUpdates';
import { SessionProvider } from '../src/session/context';
import { harness, user } from './helpers';

const mockRefreshAccess = jest.fn();
jest.mock('../src/shell/NativeNavigation', () => ({
  useNativeNavigation: () => ({ refreshAccess: mockRefreshAccess }),
}));

const mockConnection = {
  start: jest.fn(async () => undefined),
  stop: jest.fn(async () => undefined),
  on: jest.fn(),
  onreconnected: jest.fn(),
  onreconnecting: jest.fn(),
  onclose: jest.fn(),
};

jest.mock('@microsoft/signalr', () => ({
  HubConnectionBuilder: class {
    withUrl() {
      return this;
    }
    withAutomaticReconnect() {
      return this;
    }
    configureLogging() {
      return this;
    }
    build() {
      return mockConnection;
    }
  },
  LogLevel: { None: 6 },
}));
jest.mock('../src/connectivity/connectivity', () => ({
  useConnectivity: () => ({ status: 'online' }),
}));

test('a SignalR reconnect refetches the current view even without a replayed event', async () => {
  const app = harness();
  const queries = new QueryClient();
  const key = ['preparation', user.id, 'queue'];
  let serverState = 'confirmed';
  const queryFn = jest.fn(async () => serverState);
  queries.setQueryData(key, 'older value');
  const observer = new QueryObserver(queries, {
    queryKey: key,
    queryFn,
    staleTime: Infinity,
  });
  const unsubscribe = observer.subscribe(() => undefined);
  const view = await render(
    <SessionProvider client={app.client} queryClient={queries}>
      <OperationalUpdates />
    </SessionProvider>,
  );
  await waitFor(() =>
    expect(app.client.getSnapshot().status).toBe('signedOut'),
  );
  await act(async () => app.client.login('fixture', 'password'));
  await waitFor(() => expect(queries.getQueryData(key)).toBe('confirmed'));
  await act(async () => mockConnection.onreconnecting.mock.calls[0][0]());
  expect(screen.getByText(/Live updates unavailable/)).toBeVisible();

  serverState = 'preparing';
  await act(async () => mockConnection.onreconnected.mock.calls[0][0]());
  await waitFor(() => expect(queries.getQueryData(key)).toBe('preparing'));
  expect(screen.queryByText(/Live updates unavailable/)).toBeNull();
  expect(mockRefreshAccess).toHaveBeenCalledTimes(2);
  await act(async () =>
    mockConnection.on.mock.calls.find(
      ([name]) => name === 'OperationalEvent',
    )![1]({
      type: 'account',
      id: user.id,
      updatedAt: new Date().toISOString(),
    }),
  );
  expect(mockRefreshAccess).toHaveBeenCalledTimes(3);
  await view.unmount();
  expect(mockConnection.stop).toHaveBeenCalledTimes(1);
  unsubscribe();
  queries.clear();
});
