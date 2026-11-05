import { QueryClient } from '@tanstack/react-query';

import { ApiError, NetworkError } from '../src/api/errors';
import { apiOrigin, createSessionClient } from '../src/session/session';
import { deferred, harness, json, pair, user } from './helpers';

test('login exposes only the user, keeps access in memory and persists only refresh', async () => {
  const h = harness();
  await h.client.login(' staff ', 'fixture-password');
  expect(h.client.getSnapshot()).toEqual({ status: 'signedIn', user });
  expect(h.storage.set).toHaveBeenCalledWith(pair().refreshToken);
  expect(JSON.stringify(h.client.getSnapshot())).not.toContain(
    'fixture-access',
  );
  await h.client.api.GET('/api/v1/auth/me');
  expect(h.fetcher.mock.calls.at(-1)![0].headers.get('Authorization')).toBe(
    `Bearer ${pair().accessToken}`,
  );
});

test.each([400, 403, 409, 422, 500])(
  'maps RFC 7807 status %s without retrying rejection',
  async (status) => {
    const problem = {
      type: 'about:blank',
      title: 'Rejected',
      detail: 'Server detail',
      code: 'rule',
      errors: { quantity: ['Invalid'] },
    };
    const h = harness(async () => json(problem, status));
    await h.client.login('staff', 'fixture-password');
    try {
      await h.client.api.GET('/api/v1/auth/me');
      throw new Error('Expected rejection');
    } catch (error) {
      expect(error).toBeInstanceOf(ApiError);
      expect(error).toMatchObject({ status, problem });
      expect((error as Error).message).not.toContain('Server detail');
    }
    expect(h.fetcher).toHaveBeenCalledTimes(2);
  },
);

test('a transient read retries once, then maps network failure without logging raw errors', async () => {
  const h = harness(async () => {
    throw new Error('sensitive URL or server detail');
  });
  await h.client.login('staff', 'fixture-password');
  await expect(h.client.api.GET('/api/v1/auth/me')).rejects.toBeInstanceOf(
    NetworkError,
  );
  expect(h.fetcher).toHaveBeenCalledTimes(3);
  expect(h.client.getSnapshot().status).toBe('signedIn');
});

test('a transient 503 read retries once and can recover', async () => {
  let calls = 0;
  const h = harness(async () => (++calls === 1 ? json({}, 503) : json(user)));
  await h.client.login('staff', 'fixture-password');
  expect((await h.client.api.GET('/api/v1/auth/me')).data).toEqual(user);
  expect(calls).toBe(2);
});

test.each([503, 401])(
  'does not replay writes after status %s',
  async (status) => {
    let writes = 0;
    const h = harness(async (request) => {
      if (request.url.endsWith('/refresh')) return json(pair('b'));
      writes++;
      return json({}, status);
    });
    await h.client.login('staff', 'fixture-password');
    await expect(
      h.client.api.POST('/api/v1/auth/logout-all'),
    ).rejects.toMatchObject({ status });
    expect(writes).toBe(1);
  },
);

test('does not retry a network-failed mutation', async () => {
  const h = harness(async () => {
    throw new Error('offline');
  });
  await h.client.login('staff', 'fixture-password');
  await expect(
    h.client.api.POST('/api/v1/auth/logout-all'),
  ).rejects.toBeInstanceOf(NetworkError);
  expect(h.fetcher).toHaveBeenCalledTimes(2);
});

test('concurrent and delayed old-token 401 responses share one rotation', async () => {
  const rotating = deferred<void>();
  const release = deferred<void>();
  const delayed = deferred<void>();
  let refreshes = 0;
  let oldReads = 0;
  const h = harness(async (request) => {
    if (request.url.endsWith('/refresh')) {
      refreshes++;
      expect(await request.json()).toEqual({
        refreshToken: pair().refreshToken,
      });
      rotating.resolve();
      await release.promise;
      return json(pair('b'));
    }
    if (
      request.headers.get('Authorization') === `Bearer ${pair().accessToken}`
    ) {
      if (++oldReads === 3) await delayed.promise;
      return json({}, 401);
    }
    return json(user);
  });
  await h.client.login('staff', 'fixture-password');
  const first = h.client.api.GET('/api/v1/auth/me');
  const second = h.client.api.GET('/api/v1/auth/me');
  const late = h.client.api.GET('/api/v1/auth/me');
  await rotating.promise;
  release.resolve();
  await Promise.all([first, second]);
  delayed.resolve();
  await late;
  expect(refreshes).toBe(1);
  expect(h.saved()).toBe(pair('b').refreshToken);
});

test('a rejected refresh clears credentials and protected data', async () => {
  const h = harness(async () => json({}, 401));
  await h.client.login('staff', 'fixture-password');
  h.clearData.mockClear();
  await expect(h.client.api.GET('/api/v1/auth/me')).rejects.toMatchObject({
    status: 401,
  });
  expect(h.client.getSnapshot()).toEqual({ status: 'signedOut', user: null });
  expect(h.saved()).toBeNull();
  expect(h.clearData).toHaveBeenCalledTimes(1);
});

test('a replayed read rejected with 401 clears the newly rotated session', async () => {
  const h = harness(async (request) =>
    request.url.endsWith('/refresh') ? json(pair('b')) : json({}, 401),
  );
  await h.client.login('staff', 'fixture-password');
  await expect(h.client.api.GET('/api/v1/auth/me')).rejects.toMatchObject({
    status: 401,
  });
  expect(h.saved()).toBeNull();
  expect(h.client.getSnapshot().user).toBeNull();
});

test('offline renewal preserves the saved refresh token and current session for a later attempt', async () => {
  const h = harness(async (request) => {
    if (request.url.endsWith('/refresh')) throw new Error('offline');
    return json({}, 401);
  });
  await h.client.login('staff', 'fixture-password');
  await expect(h.client.api.GET('/api/v1/auth/me')).rejects.toBeInstanceOf(
    NetworkError,
  );
  expect(h.saved()).toBe(pair().refreshToken);
  expect(h.client.getSnapshot().user).toEqual(user);
});

test('caller cancellation prevents dispatch and does not trigger retry or renewal', async () => {
  const h = harness();
  await h.client.login('staff', 'fixture-password');
  const controller = new AbortController();
  controller.abort();
  await expect(
    h.client.api.GET('/api/v1/auth/me', { signal: controller.signal }),
  ).rejects.toMatchObject({ name: 'AbortError' });
  expect(h.fetcher).toHaveBeenCalledTimes(1);
});

test('in-flight cancellation rejects even when a transport ignores the abort signal', async () => {
  const entered = deferred<void>();
  const release = deferred<void>();
  const h = harness(async () => {
    entered.resolve();
    await release.promise;
    return json(user);
  });
  await h.client.login('staff', 'fixture-password');
  const controller = new AbortController();
  const pending = h.client.api.GET('/api/v1/auth/me', {
    signal: controller.signal,
  });
  await entered.promise;
  controller.abort();
  release.resolve();
  await expect(pending).rejects.toMatchObject({ name: 'AbortError' });
});

test('logout clears cached delivery data immediately and late refresh cannot restore the session', async () => {
  const entered = deferred<void>();
  const release = deferred<void>();
  const h = harness(async (request) => {
    if (request.url.endsWith('/refresh')) {
      entered.resolve();
      await release.promise;
      return json(pair('b'));
    }
    return json({}, 401);
  });
  const queries = new QueryClient();
  h.clearData.mockImplementation(() => {
    void queries.cancelQueries();
    queries.clear();
  });
  await h.client.login('staff', 'fixture-password');
  queries.setQueryData(['delivery'], { contact: 'synthetic retained data' });
  const pending = h.client.api.GET('/api/v1/auth/me');
  const rejected = expect(pending).rejects.toMatchObject({
    name: 'AbortError',
  });
  await entered.promise;
  const logout = h.client.logout();
  expect(h.client.getSnapshot().user).toBeNull();
  expect(queries.getQueryData(['delivery'])).toBeUndefined();
  release.resolve();
  await Promise.all([logout, rejected]);
  expect(h.saved()).toBeNull();
  expect(h.storage.set).toHaveBeenCalledTimes(1);
});

test('logout queues deletion after a pending secure write, preventing credential resurrection', async () => {
  const entered = deferred<void>();
  const release = deferred<void>();
  const h = harness();
  const original = h.storage.set;
  h.storage.set = jest.fn(async (token) => {
    entered.resolve();
    await release.promise;
    await original(token);
  });
  const login = h.client.login('staff', 'fixture-password');
  const rejected = expect(login).rejects.toMatchObject({ name: 'AbortError' });
  await entered.promise;
  const logout = h.client.logout();
  release.resolve();
  await Promise.all([logout, rejected]);
  expect(h.saved()).toBeNull();
  expect(h.client.getSnapshot().user).toBeNull();
});

test('restore rotates a saved refresh token before revealing protected state', async () => {
  const h = harness(async () => json(pair('restored')));
  await h.storage.set('fixture-saved-refresh');
  await h.client.restore();
  expect(h.client.getSnapshot()).toEqual({ status: 'signedIn', user });
  expect(h.saved()).toBe(pair('restored').refreshToken);
});

test('restore without a saved token never contacts the API or exposes a session', async () => {
  const h = harness();
  await h.client.restore();
  expect(h.fetcher).not.toHaveBeenCalled();
  expect(h.client.getSnapshot()).toEqual({ status: 'signedOut', user: null });
  expect(h.saved()).toBeNull();
});

test('restore rejects a malformed token response and clears saved credentials', async () => {
  const h = harness(async () => json({ ...pair(), accessToken: '' }));
  await h.storage.set('fixture-saved-refresh');
  await h.client.restore();
  expect(h.client.getSnapshot()).toEqual({ status: 'signedOut', user: null });
  expect(h.saved()).toBeNull();
  expect(h.clearData).toHaveBeenCalled();
});

test('failed secure rotation persistence fails closed', async () => {
  const h = harness(async () => json(pair('b')));
  await h.storage.set('fixture-saved-refresh');
  h.storage.set = jest.fn(async () => {
    throw new Error('secure store unavailable');
  });
  await h.client.restore();
  expect(h.client.getSnapshot().user).toBeNull();
  expect(h.saved()).toBeNull();
});

test('logout stays locally cleared if server revocation is unavailable', async () => {
  const h = harness();
  await h.client.login('staff', 'fixture-password');
  h.fetcher.mockRejectedValueOnce(new Error('offline'));
  await h.client.logout();
  expect(h.saved()).toBeNull();
  expect(h.client.getSnapshot().user).toBeNull();
});

test('API configuration rejects invalid values without echoing them', () => {
  for (const value of [
    undefined,
    'bad',
    'https://u:secret@host/api/v1',
    'https://host/api/v1?token=secret',
    'https://host/other',
  ]) {
    expect(() => apiOrigin(value)).toThrow(
      'The sign-in service is not configured correctly.',
    );
  }
  expect(apiOrigin('https://staff.example/api/v1/')).toBe(
    'https://staff.example',
  );
});

test('logout also discards a protected response whose JSON body finishes late', async () => {
  const parsing = deferred<void>();
  const release = deferred<void>();
  const h = harness(async () => {
    const response = json(user);
    response.text = async () => {
      parsing.resolve();
      await release.promise;
      return JSON.stringify(user);
    };
    return response;
  });
  await h.client.login('staff', 'fixture-password');
  const pending = h.client.api.GET('/api/v1/auth/me');
  const rejected = expect(pending).rejects.toMatchObject({
    name: 'AbortError',
  });
  await parsing.promise;
  await h.client.logout();
  release.resolve();
  await rejected;
  expect(h.client.getSnapshot().user).toBeNull();
});

test('rejected login never persists credentials or exposes submitted data in the error', async () => {
  const storage = { get: async () => null, set: jest.fn(), remove: jest.fn() };
  const client = createSessionClient({
    origin: 'https://staff.example',
    storage,
    clearData: jest.fn(),
    fetch: (async () => json({}, 401)) as typeof fetch,
  });
  await expect(client.login('staff', 'fixture-password')).rejects.toThrow(
    'Sign-in failed or your session has expired.',
  );
  expect(storage.set).not.toHaveBeenCalled();
  expect(client.getSnapshot().user).toBeNull();
});
