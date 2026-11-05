import {
  createSessionClient,
  type RefreshStorage,
} from '../src/session/session';

export const user = {
  id: '00000000-0000-0000-0000-000000000143',
  fullName: 'Test Staff',
  roles: ['barista'],
  permissions: ['orders.fulfill'],
};
export const pair = (suffix = 'a') => ({
  accessToken: `fixture-access-${suffix}`,
  refreshToken: `fixture-refresh-${suffix}`,
  expiresAt: '2026-11-04T17:00:00Z',
  user,
});
export function json(body: unknown, status = 200) {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' },
  });
}
export function deferred<T>() {
  let resolve!: (value: T) => void;
  const promise = new Promise<T>((done) => {
    resolve = done;
  });
  return { promise, resolve };
}
export function harness(
  handler: (request: Request) => Promise<Response> = async () => json(user),
) {
  let saved: string | null = null;
  const storage: RefreshStorage = {
    get: jest.fn(async () => saved),
    set: jest.fn(async (value: string) => {
      saved = value;
    }),
    remove: jest.fn(async () => {
      saved = null;
    }),
  };
  const clearData = jest.fn();
  const fetcher = jest.fn(async (request: Request) => {
    if (request.url.endsWith('/auth/login')) return json(pair());
    if (request.url.endsWith('/auth/logout'))
      return new Response(null, { status: 204 });
    return handler(request);
  });
  const client = createSessionClient({
    origin: 'https://staff.example',
    storage,
    clearData,
    fetch: fetcher as typeof fetch,
  });
  return { client, storage, clearData, fetcher, saved: () => saved };
}
