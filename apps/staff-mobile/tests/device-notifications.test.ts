import { registerForNotifications } from '../src/notifications/registerForNotifications';
import { createSessionClient } from '../src/session/session';
import { deferred, json, pair, user } from './helpers';

test('denied OS permission never requests or registers a push token', async () => {
  const readToken = jest.fn();
  const listen = jest.fn();
  const sync = jest.fn();
  const remove = jest.fn();
  const lifecycle = registerForNotifications({
    hasPermission: async () => false,
    readToken,
    listen,
    sync,
    remove,
  });
  await lifecycle.ready;
  expect(readToken).not.toHaveBeenCalled();
  expect(listen).not.toHaveBeenCalled();
  expect(sync).not.toHaveBeenCalled();
  expect(remove).toHaveBeenCalledTimes(1);
  lifecycle.stop();
});

test('a native token change cannot be replaced by an older token read', async () => {
  const first = deferred<string>();
  const changed = deferred<void>();
  const dispose = jest.fn();
  let rotate!: (token: string) => void;
  const sync = jest.fn(async () => changed.resolve(undefined));
  const lifecycle = registerForNotifications<string>({
    hasPermission: async () => true,
    readToken: (token) => (token ? Promise.resolve(token) : first.promise),
    listen: (callback) => {
      rotate = callback;
      return { remove: dispose };
    },
    sync,
    remove: async () => undefined,
  });
  await Promise.resolve();
  rotate('new-native-token');
  await changed.promise;
  first.resolve('old-native-token');
  await lifecycle.ready;
  expect(sync.mock.calls).toEqual([['new-native-token']]);
  lifecycle.stop();
  expect(dispose).toHaveBeenCalledTimes(1);
});

test('the authenticated client registers, rotates, deduplicates, and unregisters before remote logout', async () => {
  const id = '00000000-0000-0000-0000-000000000101';
  let registration: string | null = null;
  let savedRefresh: string | null = null;
  const requests: { method: string; path: string; token?: string }[] = [];
  const client = createSessionClient({
    origin: 'https://staff.example',
    storage: {
      get: async () => savedRefresh,
      set: async (value) => {
        savedRefresh = value;
      },
      remove: async () => {
        savedRefresh = null;
      },
    },
    deviceStorage: {
      get: async () => registration,
      set: async (_, value) => {
        registration = value;
      },
      remove: async () => {
        registration = null;
      },
    },
    clearData: jest.fn(),
    fetch: async (input) => {
      const request = input as Request;
      const path = new URL(request.url).pathname;
      if (path.endsWith('/auth/login'))
        return json({
          ...pair(),
          expiresAt: new Date(Date.now() + 300_000).toISOString(),
        });
      expect(request.headers.get('Authorization')).toBe(
        'Bearer fixture-access-a',
      );
      const body =
        request.method === 'DELETE' ? undefined : await request.json();
      requests.push({ method: request.method, path, token: body?.token });
      return path.endsWith('/mobile-devices')
        ? json({ id }, 201)
        : new Response(null, { status: 204 });
    },
  });
  await client.login('fixture', 'password');
  await client.syncDevice(user.id, 'android', 'first');
  await client.syncDevice(user.id, 'android', 'second');
  await client.syncDevice(user.id, 'android', 'second');
  const logout = client.logout();
  expect(client.getSnapshot().status).toBe('signedOut');
  await logout;
  expect(requests).toEqual([
    { method: 'POST', path: '/api/v1/mobile-devices', token: 'first' },
    { method: 'PUT', path: `/api/v1/mobile-devices/${id}`, token: 'second' },
    { method: 'DELETE', path: `/api/v1/mobile-devices/${id}` },
    { method: 'POST', path: '/api/v1/auth/logout' },
  ]);
  expect(registration).toBeNull();
  expect(savedRefresh).toBeNull();
});
