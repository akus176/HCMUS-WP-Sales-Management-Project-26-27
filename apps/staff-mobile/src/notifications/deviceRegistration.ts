import createClient from 'openapi-fetch';

import type { paths } from '../api/schema';

export type DevicePlatform = 'android' | 'ios';
export type DeviceRegistrationStorage = {
  get: (owner: string) => Promise<string | null>;
  set: (owner: string, id: string) => Promise<void>;
  remove: (owner: string) => Promise<void>;
};

export function createDeviceRegistration(
  origin: string,
  fetcher: typeof fetch,
  storage?: DeviceRegistrationStorage,
) {
  const api = createClient<paths>({ baseUrl: origin, fetch: fetcher });
  let queue: Promise<unknown> = Promise.resolve();
  let latest: { owner: string; token: string } | undefined;
  let revision = 0;

  function serialize<T>(operation: () => Promise<T>) {
    const result = queue.then(operation);
    queue = result.catch(() => undefined);
    return result;
  }

  async function bounded<T>(
    bearer: string,
    operation: (options: {
      signal: AbortSignal;
      headers: HeadersInit;
    }) => Promise<T>,
  ) {
    const controller = new AbortController();
    const timeout = setTimeout(() => controller.abort(), 5000);
    try {
      return await operation({
        signal: controller.signal,
        headers: { Authorization: `Bearer ${bearer}` },
      });
    } finally {
      clearTimeout(timeout);
    }
  }

  return {
    sync(
      owner: string,
      platform: DevicePlatform,
      token: string,
      bearer: () => Promise<string>,
    ) {
      const generation = revision;
      return serialize(async () => {
        if (
          !storage ||
          !token ||
          (latest?.owner === owner && latest.token === token)
        )
          return;
        const id = await storage.get(owner);
        const accessToken = await bearer();
        await bounded(accessToken, async (options) => {
          const body = { platform, token };
          if (id) {
            const { response } = await api.PUT('/api/v1/mobile-devices/{id}', {
              ...options,
              params: { path: { id } },
              body,
            });
            if (response.ok) {
              if (generation === revision) latest = { owner, token };
              return;
            }
            if (response.status !== 404) return;
            await storage.remove(owner);
          }
          const { data } = await api.POST('/api/v1/mobile-devices', {
            ...options,
            body,
          });
          if (data?.id) {
            await storage.set(owner, data.id);
            if (generation === revision) latest = { owner, token };
          }
        });
      });
    },
    remove(owner: string, bearer: () => Promise<string>) {
      return serialize(async () => {
        if (latest?.owner === owner) latest = undefined;
        const id = await storage?.get(owner);
        if (!id) return;
        const accessToken = await bearer();
        const { response } = await bounded(accessToken, (options) =>
          api.DELETE('/api/v1/mobile-devices/{id}', {
            ...options,
            params: { path: { id } },
          }),
        );
        if (response.ok || response.status === 404)
          await storage?.remove(owner);
      });
    },
    clearMemory() {
      revision++;
      latest = undefined;
    },
  };
}
