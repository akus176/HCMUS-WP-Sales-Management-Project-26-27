import { render, screen, waitFor } from '@testing-library/react-native';

import StarterScreen from '../app/index';
import { SessionProvider } from '../src/session/context';
import { harness, json } from './helpers';

test('opens working sign-in directly without an unfinished starter screen', async () => {
  const h = harness(async () => json({}, 401));
  await render(
    <SessionProvider client={h.client}>
      <StarterScreen />
    </SessionProvider>,
  );
  await waitFor(() => expect(h.client.getSnapshot().status).toBe('signedOut'));

  expect(screen.getByRole('header', { name: 'Staff sign-in' })).toBeVisible();
  expect(screen.getByLabelText('Username')).toBeVisible();
  expect(screen.getByLabelText('Password')).toBeVisible();
  expect(screen.getByRole('button', { name: 'Sign in' })).toBeVisible();
  expect(screen.queryByText('Staff tools are being prepared.')).toBeNull();
});
