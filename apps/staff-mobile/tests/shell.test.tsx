import {
  act,
  fireEvent,
  render,
  screen,
  waitFor,
} from '@testing-library/react-native';
import { AppState, Text, type AppStateStatus } from 'react-native';
import { PermissionBoundary } from '../src/shell/PermissionBoundary';
import { StaffHome } from '../src/shell/StaffHome';
import { deferred, json, user } from './helpers';
import { createSessionClient } from '../src/session/session';

const mockSession = {
  state: {
    status: 'signedIn' as string,
    user: { ...user } as typeof user | null,
  },
  client: null as ReturnType<typeof createSessionClient> | null,
};
jest.mock('../src/session/context', () => ({ useSession: () => mockSession }));
jest.mock('expo-router', () => ({ router: { push: jest.fn() } }));

beforeEach(() => {
  mockSession.state = { status: 'signedIn', user: { ...user } };
  mockSession.client = null;
});
async function session(response: () => Promise<Response>) {
  const client = createSessionClient({
    origin: 'https://staff.example',
    storage: {
      get: async () => null,
      set: async () => {},
      remove: async () => {},
    },
    clearData: () => {},
    fetch: (async (request: Request) =>
      request.url.endsWith('/auth/login')
        ? json({ accessToken: 'fixture', refreshToken: 'fixture', user })
        : response()) as typeof fetch,
  });
  await client.login('staff', 'fixture');
  mockSession.client = client;
}
const protectedChild = jest.fn(() => <Text>Protected feature content</Text>);

beforeEach(() => protectedChild.mockClear());
afterEach(() => jest.restoreAllMocks());
test('never mounts protected children while checking, then permits a fresh server grant', async () => {
  const pending = deferred<Response>();
  await session(() => pending.promise);
  await render(
    <PermissionBoundary moduleId="barista">
      {protectedChild}
    </PermissionBoundary>,
  );
  expect(screen.getByRole('alert')).toHaveTextContent(/Checking access/);
  expect(protectedChild).not.toHaveBeenCalled();
  await act(async () => pending.resolve(json(user)));
  expect(await screen.findByText('Protected feature content')).toBeVisible();
});
test('fresh server revocation denies stale session permission before mounting', async () => {
  await session(async () => json({ ...user, permissions: [] }));
  await render(
    <PermissionBoundary moduleId="barista">
      {protectedChild}
    </PermissionBoundary>,
  );
  await waitFor(() =>
    expect(screen.getByRole('alert')).toHaveTextContent(/Access unavailable/),
  );
  expect(protectedChild).not.toHaveBeenCalled();
});
test('permission-check failure fails closed and offers explicit retry', async () => {
  await session(async () => {
    throw new Error('offline');
  });
  await render(
    <PermissionBoundary moduleId="barista">
      {protectedChild}
    </PermissionBoundary>,
  );
  await waitFor(() =>
    expect(screen.getByRole('alert')).toHaveTextContent(
      /Unable to verify access/,
    ),
  );
  expect(
    screen.getByRole('button', { name: 'Retry access check' }),
  ).toBeVisible();
  expect(protectedChild).not.toHaveBeenCalled();
});
test('unknown and ambiguous modules fail closed without loading a feature', async () => {
  await render(
    <PermissionBoundary moduleId={['barista', 'delivery']}>
      {protectedChild}
    </PermissionBoundary>,
  );
  expect(screen.getByRole('alert')).toHaveTextContent(/Access unavailable/);
  expect(protectedChild).not.toHaveBeenCalled();
});
test('terminal session loss removes protected content immediately', async () => {
  await session(async () => json(user));
  const view = await render(
    <PermissionBoundary moduleId="barista">
      {protectedChild}
    </PermissionBoundary>,
  );
  await screen.findByText('Protected feature content');
  mockSession.state = { status: 'signedOut', user: null };
  await view.rerender(
    <PermissionBoundary moduleId="barista">
      {protectedChild}
    </PermissionBoundary>,
  );
  expect(screen.queryByText('Protected feature content')).toBeNull();
});
test('background hides content and resume rechecks permissions before remounting', async () => {
  let listener: ((state: AppStateStatus) => void) | undefined;
  jest
    .spyOn(AppState, 'addEventListener')
    .mockImplementation((_event, callback) => {
      listener = callback;
      return { remove: jest.fn() };
    });
  let permissions = user.permissions;
  await session(async () => json({ ...user, permissions }));
  await render(
    <PermissionBoundary moduleId="barista">
      {protectedChild}
    </PermissionBoundary>,
  );
  await screen.findByText('Protected feature content');
  await act(async () => listener?.('background'));
  expect(screen.queryByText('Protected feature content')).toBeNull();
  permissions = [];
  await act(async () => listener?.('active'));
  await waitFor(() =>
    expect(screen.getByRole('alert')).toHaveTextContent(/Access unavailable/),
  );
  expect(screen.queryByText('Protected feature content')).toBeNull();
  jest.restoreAllMocks();
});
test('home renders only effective permission modules and an explicit empty state', async () => {
  const view = await render(<StaffHome />);
  expect(
    screen.getByRole('button', { name: 'Open Preparation' }),
  ).toBeVisible();
  expect(screen.queryByRole('button', { name: 'Open Deliveries' })).toBeNull();
  mockSession.state.user = { ...user, roles: ['Admin'], permissions: [] };
  await view.rerender(<StaffHome />);
  expect(
    screen.getByText('No mobile modules are available for your permissions.'),
  ).toBeVisible();
});
test('restoring session does not expose modules or staff identity', async () => {
  mockSession.state = { status: 'restoring', user: null };
  await render(<StaffHome />);
  expect(screen.getByRole('alert')).toHaveTextContent(/Restoring session/);
  expect(screen.queryByText(user.fullName)).toBeNull();
});
test('home actions have accessible names and at least 48 dp targets', async () => {
  await render(<StaffHome />);
  const button = screen.getByRole('button', { name: 'Open Preparation' });
  expect(button).toHaveStyle({ minHeight: 48, minWidth: 48 });
  await fireEvent.press(button);
});

test('ignores an old permission response after the selected module changes', async () => {
  const pending = deferred<Response>();
  let calls = 0;
  await session(async () => (++calls === 1 ? pending.promise : json(user)));
  const view = await render(
    <PermissionBoundary moduleId="barista">
      {protectedChild}
    </PermissionBoundary>,
  );
  await view.rerender(
    <PermissionBoundary moduleId="delivery">
      {protectedChild}
    </PermissionBoundary>,
  );
  await waitFor(() =>
    expect(screen.getByRole('alert')).toHaveTextContent(/Access unavailable/),
  );
  await act(async () => pending.resolve(json(user)));
  expect(protectedChild).not.toHaveBeenCalled();
});
test('explicit retry grants access only after a new successful permission check', async () => {
  let online = false;
  await session(async () => {
    if (!online) throw new Error('offline');
    return json(user);
  });
  await render(
    <PermissionBoundary moduleId="barista">
      {protectedChild}
    </PermissionBoundary>,
  );
  const retry = await screen.findByRole('button', {
    name: 'Retry access check',
  });
  expect(protectedChild).not.toHaveBeenCalled();
  online = true;
  await fireEvent.press(retry);
  expect(await screen.findByText('Protected feature content')).toBeVisible();
});

test('malformed and cross-account permission responses cannot mount a protected feature', async () => {
  await session(async () => json({ ...user, id: 'other-account' }));
  const view = await render(
    <PermissionBoundary moduleId="barista">
      {protectedChild}
    </PermissionBoundary>,
  );
  await waitFor(() =>
    expect(screen.getByRole('alert')).toHaveTextContent(/Access unavailable/),
  );
  expect(protectedChild).not.toHaveBeenCalled();
  await view.unmount();
  await session(async () => json({ ...user, permissions: null }));
  await render(
    <PermissionBoundary moduleId="barista">
      {protectedChild}
    </PermissionBoundary>,
  );
  await waitFor(() =>
    expect(screen.getByRole('alert')).toHaveTextContent(/Access unavailable/),
  );
  expect(protectedChild).not.toHaveBeenCalled();
});

test('home discovers newly granted modules from current API permissions, not stale login claims', async () => {
  mockSession.state.user = { ...user, permissions: [] };
  await session(async () =>
    json({ ...user, permissions: ['delivery.update'] }),
  );
  await render(
    <PermissionBoundary scope="home">
      {(verified) => <StaffHome user={verified} />}
    </PermissionBoundary>,
  );
  expect(
    await screen.findByRole('button', { name: 'Open Deliveries' }),
  ).toBeVisible();
  expect(screen.queryByRole('button', { name: 'Open Preparation' })).toBeNull();
});
test('duplicate active events recheck permissions without getting stuck loading', async () => {
  let listener: ((state: AppStateStatus) => void) | undefined;
  jest
    .spyOn(AppState, 'addEventListener')
    .mockImplementation((_event, callback) => {
      listener = callback;
      return { remove: jest.fn() };
    });
  let permissions = user.permissions;
  await session(async () => json({ ...user, permissions }));
  await render(
    <PermissionBoundary moduleId="barista">
      {protectedChild}
    </PermissionBoundary>,
  );
  await screen.findByText('Protected feature content');
  permissions = [];
  await act(async () => listener?.('active'));
  await waitFor(() =>
    expect(screen.getByRole('alert')).toHaveTextContent(/Access unavailable/),
  );
  expect(screen.queryByText('Protected feature content')).toBeNull();
});

test('an initially empty home hides identity in background and discovers a new grant on resume', async () => {
  let listener: ((state: AppStateStatus) => void) | undefined;
  jest
    .spyOn(AppState, 'addEventListener')
    .mockImplementation((_event, callback) => {
      listener = callback;
      return { remove: jest.fn() };
    });
  let permissions: string[] = [];
  await session(async () => json({ ...user, permissions }));
  await render(
    <PermissionBoundary scope="home">
      {(verified) => <StaffHome user={verified} />}
    </PermissionBoundary>,
  );
  await screen.findByText(
    'No mobile modules are available for your permissions.',
  );
  await act(async () => listener?.('background'));
  expect(screen.queryByText(user.fullName)).toBeNull();
  permissions = ['delivery.update'];
  await act(async () => listener?.('active'));
  expect(
    await screen.findByRole('button', { name: 'Open Deliveries' }),
  ).toBeVisible();
});

test('permission-check failure still allows immediate local sign-out', async () => {
  await session(async () => {
    throw new Error('offline');
  });
  await render(
    <PermissionBoundary scope="home">{protectedChild}</PermissionBoundary>,
  );
  await screen.findByRole('button', { name: 'Retry access check' });
  await fireEvent.press(screen.getByRole('button', { name: 'Sign out' }));
  expect(mockSession.client?.getSnapshot().status).toBe('signedOut');
  expect(protectedChild).not.toHaveBeenCalled();
});
