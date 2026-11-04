import createClient from 'openapi-fetch';

import {
  ApiError,
  cancelled,
  NetworkError,
  requireSuccess,
} from '../api/errors';
import type { components, paths } from '../api/schema';

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
  clearData: () => void;
  fetch?: typeof fetch;
}) {
  const fetcher = options.fetch ?? globalThis.fetch;
  let state: SessionState = { status: 'restoring', user: null };
  let accessToken: string | undefined;
  let epoch = 0;
  let storageQueue: Promise<unknown> = Promise.resolve();
  let renewal: { epoch: number; promise: Promise<void> } | undefined;
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
    publish({ status: 'signedIn', user: pair.user });
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
  return {
    api,
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
      const saved = stored(options.storage.get);
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
  };
}

export type SessionClient = ReturnType<typeof createSessionClient>;
