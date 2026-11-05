/// <reference types="node" />
// Explicit opt-in protocol verification against a disposable, provisioned API.
// Storage is in memory here; native SecureStore is covered separately.
import { request as httpRequest } from 'node:http';

import { apiOrigin, createSessionClient } from '../src/session/session';

test('real API rejects bad credentials, rotates saved refresh, and revokes logout', async () => {
  const base = process.env.COFFEESHOP_MOBILE_TEST_API_BASE_URL;
  const username = process.env.COFFEESHOP_MOBILE_TEST_USERNAME;
  const password = process.env.COFFEESHOP_MOBILE_TEST_PASSWORD;
  if (!base || !username || !password)
    throw new Error(
      'Set the three documented COFFEESHOP_MOBILE_TEST variables for a disposable local account.',
    );
  const origin = apiOrigin(base);
  if (new URL(origin).hostname !== '127.0.0.1')
    throw new Error('Live verification requires a loopback disposable API.');
  const fetcher = (async (input: Request) => {
    const body = await input.clone().text();
    return new Promise<Response>((resolve, reject) => {
      const request = httpRequest(
        input.url,
        {
          method: input.method,
          headers: Object.fromEntries(input.headers),
          signal: input.signal,
        },
        (response) => {
          const chunks: Buffer[] = [];
          response.on('data', (chunk) => chunks.push(Buffer.from(chunk)));
          response.on('end', () =>
            resolve(
              new Response(
                response.statusCode === 204
                  ? null
                  : Buffer.concat(chunks).toString(),
                {
                  status: response.statusCode,
                  headers: { 'Content-Type': 'application/json' },
                },
              ),
            ),
          );
        },
      );
      request.on('error', reject);
      request.end(body);
    });
  }) as typeof fetch;
  let saved: string | null = null;
  const storage = {
    get: async () => saved,
    set: async (value: string) => {
      saved = value;
    },
    remove: async () => {
      saved = null;
    },
  };
  const client = createSessionClient({
    origin,
    storage,
    clearData: jest.fn(),
    fetch: fetcher,
  });
  await expect(
    client.login(username, 'deliberately-invalid-test-password'),
  ).rejects.toMatchObject({ status: 401 });
  await client.login(username, password);
  expect(client.getSnapshot().status).toBe('signedIn');
  expect((await client.api.GET('/api/v1/auth/me')).data?.id).toBe(
    client.getSnapshot().user?.id,
  );
  const original = saved;
  const restored = createSessionClient({
    origin,
    storage,
    clearData: jest.fn(),
    fetch: fetcher,
  });
  await restored.restore();
  expect(restored.getSnapshot().status).toBe('signedIn');
  expect(saved !== original).toBe(true);
  const revoked = saved;
  await restored.logout();
  expect(restored.getSnapshot().user).toBeNull();
  expect(saved === null).toBe(true);
  const response = await fetcher(
    new Request(`${origin}/api/v1/auth/refresh`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ refreshToken: revoked }),
    }),
  );
  expect(response.status).toBe(401);
}, 30000);
