import createClient from 'openapi-fetch';

import {
  ApiError,
  cancelled,
  NetworkError,
  requireSuccess,
} from '../api/errors';
import type { components, paths } from '../api/schema';
import {
  createDeviceRegistration,
  type DevicePlatform,
  type DeviceRegistrationStorage,
} from '../notifications/deviceRegistration';

type TokenPair = components['schemas']['TokenResponse'];
export type StaffSession = components['schemas']['StaffSession'];
export type SessionState = {
  status: 'restoring' | 'signedOut' | 'signedIn';
  user: StaffSession | null;
};
export type RefreshStorage = {
  get: () => Promise<string | null>;
  set: (token: string) => Promise<void>;
  remove: () => Promise<void>;
};
export type AccountWideLogoutReason =
  'unauthorized' | 'rejected' | 'uncertain' | 'storage';

const accountWideLogoutMessages: Record<AccountWideLogoutReason, string> = {
  unauthorized:
    'Your sign-in expired before signing out everywhere was confirmed. Sign in again.',
  rejected: 'The service did not confirm signing out everywhere. Try again.',
  uncertain:
    'Signing out everywhere could not be confirmed. Check your connection and try again.',
  storage:
    'Signed out everywhere, but this device could not remove its saved sign-in. Try again.',
};

export class AccountWideLogoutError extends Error {
  constructor(readonly reason: AccountWideLogoutReason) {
    super(accountWideLogoutMessages[reason]);
    this.name = 'AccountWideLogoutError';
  }
}

function sameCodes(left: readonly string[], right: readonly string[]) {
  if (!Array.isArray(left) || !Array.isArray(right)) return false;
  const previous = new Set(left);
  const next = new Set(right);
  return (
    previous.size === next.size && [...previous].every((code) => next.has(code))
  );
}

function sameStaff(previous: StaffSession | null, next: StaffSession) {
  return (
    previous?.id === next.id &&
    previous.fullName === next.fullName &&
    sameCodes(previous.roles, next.roles) &&
    sameCodes(previous.permissions, next.permissions)
  );
}

export function apiOrigin(value: string | undefined) {
  try {
    const url = new URL(value ?? '');
    if (
      !['https:', 'http:'].includes(url.protocol) ||
      url.username ||
      url.password ||
      url.search ||
      url.hash ||
      url.pathname.replace(/\/$/, '') !== '/api/v1' ||
      (!__DEV__ && url.protocol !== 'https:')
    )
      throw new Error();
    return url.origin;
  } catch {
    throw new Error('The sign-in service is not configured correctly.');
  }
}

export function createSessionClient(options: {
  origin: string;
  storage: RefreshStorage;
  deviceStorage?: DeviceRegistrationStorage;
  clearData: () => void;
  fetch?: typeof fetch;
}) {
  const fetcher = options.fetch ?? globalThis.fetch;
  const devices = createDeviceRegistration(
    options.origin,
    fetcher,
    options.deviceStorage,
  );
  let state: SessionState = { status: 'restoring', user: null };
  let accessToken: string | undefined;
  let accessExpiresAt = 0;
  let epoch = 0;
  let storageQueue: Promise<unknown> = Promise.resolve();
  let renewal: { epoch: number; promise: Promise<void> } | undefined;
  let globalLogout: { epoch: number; promise: Promise<void> } | undefined;
  const listeners = new Set<() => void>();
  const requests = new Set<AbortController>();
  function publish(next: SessionState) {
    state = next;
    listeners.forEach((listener) => listener());
  }
  function stored<T>(operation: () => Promise<T>): Promise<T> {
    const result = storageQueue.then(operation);
    storageQueue = result.catch(() => undefined);
    return result;
  }
  function clear() {
    epoch++;
    accessToken = undefined;
    accessExpiresAt = 0;
    devices.clearMemory();
    requests.forEach((request) => request.abort());
    options.clearData();
    publish({ status: 'signedOut', user: null });
    return stored(options.storage.remove);
  }
  async function send(request: Request, generation: number) {
    if (generation !== epoch || request.signal.aborted) throw cancelled();
    const controller = new AbortController();
    const abort = () => controller.abort();
    request.signal.addEventListener('abort', abort);
    requests.add(controller);
    try {
      const response = await fetcher(
        new Request(request, {
          signal: controller.signal,
          credentials: 'omit',
          cache: 'no-store',
        }),
      );
      if (controller.signal.aborted || generation !== epoch) throw cancelled();
      return response;
    } catch {
      if (controller.signal.aborted || generation !== epoch) throw cancelled();
      throw new NetworkError();
    } finally {
      requests.delete(controller);
      request.signal.removeEventListener('abort', abort);
    }
  }
  const anonymous = createClient<paths>({
    baseUrl: options.origin,
    fetch: async (request) => requireSuccess(await send(request, epoch)),
  });
  async function accept(pair: TokenPair, generation: number) {
    if (generation !== epoch) throw cancelled();
    if (!pair?.accessToken || !pair.refreshToken || !pair.user?.id) {
      throw new ApiError(401);
    }
    // Persist rotation before exposing the new access token. Logout's queued
    // deletion runs after a pending write, so late responses cannot restore it.
    await stored(() => options.storage.set(pair.refreshToken));
    if (generation !== epoch) throw cancelled();
    accessToken = pair.accessToken;
    accessExpiresAt = Date.parse(pair.expiresAt);
    // Routine token rotation must not remount authorized screens and discard
    // unsaved input. Changed identity or grants still publish a new snapshot,
    // so PermissionBoundary revalidates before showing protected content.
    const user = sameStaff(state.user, pair.user) ? state.user! : pair.user;
    publish({ status: 'signedIn', user });
  }
  function refresh() {
    const generation = epoch;
    if (renewal?.epoch === generation) return renewal.promise;
    const promise = (async () => {
      try {
        const token = await stored(options.storage.get);
        if (generation !== epoch) throw cancelled();
        if (!token) throw new ApiError(401);
        const { data } = await anonymous.POST('/api/v1/auth/refresh', {
          body: { refreshToken: token },
        });
        await accept(data!, generation);
      } catch (error) {
        const temporary =
          error instanceof NetworkError ||
          (error instanceof ApiError && error.status >= 500);
        if (generation === epoch && !temporary) await clear();
        throw error;
      }
    })();
    renewal = { epoch: generation, promise };
    void promise
      .finally(() => {
        if (renewal?.promise === promise) renewal = undefined;
      })
      .catch(() => undefined);
    return promise;
  }
  async function authenticated(request: Request) {
    const generation = epoch;
    const token = accessToken;
    if (!token) throw new ApiError(401);
    if (!request.url.startsWith(`${options.origin}/api/v1/`)) {
      throw new Error('Refusing to send credentials outside the shared API.');
    }
    const read = ['GET', 'HEAD'].includes(request.method);
    async function attempt(bearer: string) {
      const headers = new Headers(request.headers);
      headers.set('Authorization', `Bearer ${bearer}`);
      for (let retry = 0; ; retry++) {
        try {
          const response = await send(
            new Request(request.clone(), { headers }),
            generation,
          );
          if (read && retry === 0 && [502, 503, 504].includes(response.status))
            continue;
          return response;
        } catch (error) {
          if (read && retry === 0 && error instanceof NetworkError) continue;
          throw error;
        }
      }
    }
    let response = await attempt(token);
    if (response.status === 401) {
      // A delayed 401 for the old token shares the completed rotation too.
      if (generation !== epoch) throw cancelled();
      if (accessToken === token) await refresh();
      if (generation !== epoch) throw cancelled();
      // Mutations are never replayed automatically, including after renewal.
      if (read && accessToken) {
        response = await attempt(accessToken);
        if (response.status === 401 && generation === epoch) await clear();
      }
    }
    return requireSuccess(response);
  }
  const generated = createClient<paths>({
    baseUrl: options.origin,
    fetch: authenticated,
  });
  // Guard the complete generated operation, including JSON body parsing after
  // fetch has returned headers. Late data from an old session is discarded.
  const api = new Proxy(generated, {
    get(target, property, receiver) {
      const operation = Reflect.get(target, property, receiver);
      if (
        ![
          'GET',
          'HEAD',
          'POST',
          'PUT',
          'PATCH',
          'DELETE',
          'OPTIONS',
          'TRACE',
        ].includes(String(property))
      )
        return operation;
      return async (...args: unknown[]) => {
        const generation = epoch;
        const result = await operation(...args);
        if (generation !== epoch) throw cancelled();
        return result;
      };
    },
  });
  async function getOperationalAccessToken(userId: string, generation = epoch) {
    if (generation !== epoch || state.user?.id !== userId || !accessToken)
      throw cancelled();
    if (accessExpiresAt <= Date.now() + 30_000) await refresh();
    if (generation !== epoch || !accessToken) throw cancelled();
    return accessToken;
  }
  return {
    api,
    getOperationalAccessToken,
    syncDevice(userId: string, platform: DevicePlatform, token: string) {
      const generation = epoch;
      return devices.sync(userId, platform, token, () =>
        getOperationalAccessToken(userId, generation),
      );
    },
    removeDevice(userId: string) {
      const generation = epoch;
      return devices.remove(userId, () =>
        getOperationalAccessToken(userId, generation),
      );
    },
    operationalHubUrl: `${options.origin}/api/v1/operational-events`,
    getSnapshot: () => state,
    subscribe(listener: () => void) {
      listeners.add(listener);
      return () => {
        listeners.delete(listener);
      };
    },
    async restore() {
      try {
        await refresh();
      } catch {
        if (state.status === 'restoring')
          publish({ status: 'signedOut', user: null });
      }
    },
    async login(username: string, password: string) {
      const cleanup = clear();
      const generation = epoch;
      await cleanup;
      if (generation !== epoch) throw cancelled();
      try {
        const { data } = await anonymous.POST('/api/v1/auth/login', {
          body: { username: username.trim(), password },
        });
        await accept(data!, generation);
      } catch (error) {
        if (generation === epoch) await clear();
        throw error;
      }
    },
    async logout() {
      const token = accessToken;
      const userId = state.user?.id;
      const saved = stored(options.storage.get);
      const unregister =
        token && userId
          ? devices.remove(userId, async () => token).catch(() => undefined)
          : undefined;
      // Clear protected UI, queries and requests synchronously, even offline.
      const cleanup = clear();
      // Attach a rejection handler immediately while server revocation runs.
      const removed = cleanup.then(
        () => null,
        () =>
          new Error('Unable to remove the saved session. Please try again.'),
      );
      try {
        const refreshToken = await saved;
        await unregister;
        if (token && refreshToken) {
          const controller = new AbortController();
          const timeout = setTimeout(() => controller.abort(), 10000);
          try {
            const revoke = createClient<paths>({
              baseUrl: options.origin,
              fetch: fetcher,
            });
            await revoke.POST('/api/v1/auth/logout', {
              headers: { Authorization: `Bearer ${token}` },
              body: { refreshToken },
              signal: controller.signal,
            });
          } finally {
            clearTimeout(timeout);
          }
        }
      } catch {
        /* Local logout is immediate; revocation is best effort offline. */
      }
      const error = await removed;
      if (error) throw error;
    },
    logoutAll() {
      const generation = epoch;
      const userId = state.user?.id;
      if (!userId)
        return Promise.reject(new AccountWideLogoutError('unauthorized'));
      if (globalLogout?.epoch === generation) return globalLogout.promise;
      const promise = (async () => {
        let token: string;
        try {
          token = await getOperationalAccessToken(userId, generation);
        } catch (error) {
          if (generation !== epoch) throw cancelled();
          throw new AccountWideLogoutError(
            error instanceof ApiError && error.status === 401
              ? 'unauthorized'
              : 'uncertain',
          );
        }
        if (generation !== epoch) throw cancelled();
        const controller = new AbortController();
        const timeout = setTimeout(() => controller.abort(), 10000);
        let failure: AccountWideLogoutReason | undefined;
        try {
          const revoke = createClient<paths>({
            baseUrl: options.origin,
            fetch: fetcher,
          });
          const { response } = await revoke.POST('/api/v1/auth/logout-all', {
            headers: { Authorization: `Bearer ${token}` },
            signal: controller.signal,
          });
          if (!response.ok)
            failure =
              response.status === 401
                ? 'unauthorized'
                : response.status >= 500
                  ? 'uncertain'
                  : 'rejected';
        } catch {
          failure = 'uncertain';
        } finally {
          clearTimeout(timeout);
        }
        if (failure) throw new AccountWideLogoutError(failure);
        // A newer sign-in owns its credentials, caches and device registration.
        if (generation !== epoch) return;
        // The server has confirmed revocation. Device cleanup is best effort and
        // must not turn a confirmed global sign-out into a false failure.
        await devices.remove(userId, async () => token).catch(() => undefined);
        if (generation !== epoch) return;
        try {
          await clear();
        } catch {
          throw new AccountWideLogoutError('storage');
        }
      })();
      globalLogout = { epoch: generation, promise };
      void promise
        .finally(() => {
          if (globalLogout?.promise === promise) globalLogout = undefined;
        })
        .catch(() => undefined);
      return promise;
    },
  };
}

export type SessionClient = ReturnType<typeof createSessionClient>;
