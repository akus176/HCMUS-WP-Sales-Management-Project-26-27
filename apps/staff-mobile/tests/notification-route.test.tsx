import { act, render, screen, waitFor } from '@testing-library/react-native';

import NotificationRecordRoute from '../app/notifications/[kind]/[recordId]';
import { notificationDestination } from '../src/notifications/destination';
import { SessionProvider } from '../src/session/context';
import { harness, json, user } from './helpers';

const mockRecordId = '00000000-0000-0000-0000-000000000101';
let mockKind = 'approval';
jest.mock('expo-router', () => ({
  router: { replace: jest.fn() },
  useLocalSearchParams: () => ({ kind: mockKind, recordId: mockRecordId }),
  useFocusEffect: (callback: () => void) =>
    jest.requireActual('react').useEffect(callback, [callback]),
}));

test('notification destinations are allowlisted and accept only opaque identifiers', () => {
  for (const kind of ['approval', 'assignment', 'low-stock', 'urgent-order'])
    expect(
      notificationDestination({ kind, recordId: mockRecordId })?.kind,
    ).toBe(kind);
  expect(
    notificationDestination({
      kind: 'https://example.test',
      recordId: mockRecordId,
    }),
  ).toBeUndefined();
  expect(
    notificationDestination({
      kind: 'assignment',
      recordId: '../../customers',
    }),
  ).toBeUndefined();
});

test('a stale approval link rechecks the exact permission before making a protected read', async () => {
  mockKind = 'approval';
  const app = harness(async () =>
    json({ ...user, permissions: ['orders.cancel'] }),
  );
  await render(
    <SessionProvider client={app.client}>
      <NotificationRecordRoute />
    </SessionProvider>,
  );
  await waitFor(() =>
    expect(app.client.getSnapshot().status).toBe('signedOut'),
  );
  await act(async () => app.client.login('fixture', 'password'));
  expect(await screen.findByText(/Access unavailable/)).toBeTruthy();
  const requests = app.fetcher.mock.calls.map(
    ([request]) => new URL(request.url).pathname,
  );
  expect(requests).toContain('/api/v1/auth/me');
  expect(requests).not.toContain('/api/v1/discounts/pending');
});

test('a signed-out notification link makes no protected request', async () => {
  mockKind = 'assignment';
  const app = harness();
  await app.client.restore();
  await render(
    <SessionProvider client={app.client}>
      <NotificationRecordRoute />
    </SessionProvider>,
  );
  expect(await screen.findByText(/Sign in again/)).toBeTruthy();
  expect(app.fetcher).not.toHaveBeenCalled();
});
