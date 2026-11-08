import {
  act,
  fireEvent,
  render,
  screen,
  waitFor,
} from '@testing-library/react-native';
import { router } from 'expo-router';

import { DeliveryScreen } from '../src/features/delivery/DeliveryScreen';
import { SessionProvider, useSession } from '../src/session/context';
import { harness, json } from './helpers';
import { QueryCleanup } from './query-cleanup';

jest.mock('expo-router', () => ({ router: { push: jest.fn() } }));

function SignedInDelivery() {
  return useSession().state.user ? <DeliveryScreen /> : null;
}

test('loads separate active and completed lists and opens only active assignments', async () => {
  const h = harness(async (request) => {
    const completed =
      new URL(request.url).searchParams.get('status') === 'delivered,failed';
    return json({
      items: [
        {
          orderId: 'order-1',
          orderNumber: 'ORD-1',
          deliveryStatus: completed ? 'delivered' : 'assigned',
        },
      ],
      page: 1,
      pageSize: 10,
      totalCount: 1,
    });
  });
  await render(
    <SessionProvider client={h.client}>
      <QueryCleanup />
      <SignedInDelivery />
    </SessionProvider>,
  );
  await waitFor(() => expect(h.client.getSnapshot().status).toBe('signedOut'));
  await act(async () => {
    await h.client.login('courier', 'password');
  });
  await fireEvent.press(
    await screen.findByRole('button', { name: 'Open delivery ORD-1' }),
  );
  expect(router.push).toHaveBeenCalledWith({
    pathname: '/modules/delivery/[orderId]',
    params: { orderId: 'order-1' },
  });
  await fireEvent.press(
    screen.getByRole('button', { name: 'Completed deliveries' }),
  );
  await screen.findByText('ORD-1 · delivered');
  expect(
    screen.queryByRole('button', { name: 'Open delivery ORD-1' }),
  ).toBeNull();
  expect(
    h.fetcher.mock.calls.some(
      ([request]) =>
        new URL(request.url).searchParams.get('status') === 'delivered,failed',
    ),
  ).toBe(true);
});
