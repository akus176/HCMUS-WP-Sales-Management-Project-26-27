import {
  act,
  fireEvent,
  render,
  screen,
  waitFor,
} from '@testing-library/react-native';
import { QueryClient } from '@tanstack/react-query';
import { Linking } from 'react-native';

import { DeliveryDetail } from '../src/features/delivery/DeliveryDetail';
import { SessionProvider, useSession } from '../src/session/context';
import { harness, json } from './helpers';

jest.mock('expo-router', () => ({ router: { replace: jest.fn() } }));

function SignedInDetail() {
  return useSession().state.user ? (
    <DeliveryDetail orderId="delivery-1" />
  ) : null;
}

test('opens navigation with an encoded address and discards details on unmount', async () => {
  const h = harness(async () =>
    json({
      orderId: 'delivery-1',
      orderNumber: 'ORD-1',
      deliveryStatus: 'assigned',
      recipientName: 'Recipient',
      contactNumber: '123456789',
      addressLine: '1 Main St & 2nd',
    }),
  );
  const queries = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  const open = jest.spyOn(Linking, 'openURL').mockResolvedValue(undefined);
  const view = await render(
    <SessionProvider client={h.client} queryClient={queries}>
      <SignedInDetail />
    </SessionProvider>,
  );
  await waitFor(() => expect(h.client.getSnapshot().status).toBe('signedOut'));
  await act(async () => {
    await h.client.login('courier', 'password');
  });
  await screen.findByText('Address: 1 Main St & 2nd');
  await fireEvent.press(
    screen.getByRole('button', { name: 'Open navigation' }),
  );
  expect(open).toHaveBeenCalledWith(
    'https://www.google.com/maps/search/?api=1&query=1%20Main%20St%20%26%202nd',
  );
  await view.unmount();
  await waitFor(() =>
    expect(
      queries.getQueryCache().findAll({ queryKey: ['delivery'] }),
    ).toHaveLength(0),
  );
  open.mockRestore();
});

test('does not render private details when the server rejects a deep link', async () => {
  const h = harness(async () => json({ title: 'Not found' }, 404));
  const view = await render(
    <SessionProvider client={h.client}>
      <SignedInDetail />
    </SessionProvider>,
  );
  await waitFor(() => expect(h.client.getSnapshot().status).toBe('signedOut'));
  await act(async () => {
    await h.client.login('courier', 'password');
  });
  await screen.findByText(
    'Delivery is unavailable or no longer assigned to you.',
  );
  expect(screen.queryByRole('button', { name: 'Open navigation' })).toBeNull();
  await view.unmount();
});
