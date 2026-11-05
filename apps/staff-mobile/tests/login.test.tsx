import {
  fireEvent,
  render,
  screen,
  waitFor,
} from '@testing-library/react-native';

import LoginScreen from '../app/login';
import { StaffHome as SessionScreen } from '../src/shell/StaffHome';
import { SessionProvider, useSession } from '../src/session/context';
import { createSessionClient } from '../src/session/session';
import { json, pair } from './helpers';

function Scene() {
  const { state } = useSession();
  return state.user ? <SessionScreen /> : <LoginScreen />;
}
function setup(accept: boolean) {
  const fetcher = jest.fn(async () => (accept ? json(pair()) : json({}, 401)));
  const storage = {
    get: async () => null,
    set: jest.fn(async () => {}),
    remove: jest.fn(async () => {}),
  };
  const client = createSessionClient({
    origin: 'https://staff.example',
    storage,
    clearData: jest.fn(),
    fetch: fetcher as typeof fetch,
  });
  return { fetcher, storage, client };
}

test('valid login shows the server session and clears protected identity on logout', async () => {
  const h = setup(true);
  await render(
    <SessionProvider client={h.client}>
      <Scene />
    </SessionProvider>,
  );
  await waitFor(() => expect(h.client.getSnapshot().status).toBe('signedOut'));
  await fireEvent.changeText(screen.getByLabelText('Username'), 'staff');
  await fireEvent.changeText(
    screen.getByLabelText('Password'),
    'fixture-password',
  );
  expect(screen.getByLabelText('Password').props.secureTextEntry).toBe(true);
  await fireEvent.press(screen.getByRole('button', { name: 'Sign in' }));
  expect(await screen.findByText('Test Staff')).toBeVisible();
  expect(screen.queryByLabelText('Password')).toBeNull();
  await fireEvent.press(screen.getByRole('button', { name: 'Sign out' }));
  expect(await screen.findByLabelText('Password')).toBeVisible();
  expect(screen.queryByText('Test Staff')).toBeNull();
});

test('rejected login shows a safe error, clears the password, and never displays protected data', async () => {
  const h = setup(false);
  await render(
    <SessionProvider client={h.client}>
      <LoginScreen />
    </SessionProvider>,
  );
  await waitFor(() => expect(h.client.getSnapshot().status).toBe('signedOut'));
  await fireEvent.changeText(screen.getByLabelText('Username'), 'staff');
  await fireEvent.changeText(
    screen.getByLabelText('Password'),
    'fixture-password',
  );
  await fireEvent.press(screen.getByRole('button', { name: 'Sign in' }));
  expect(await screen.findByRole('alert')).toHaveTextContent(
    'Sign-in failed or your session has expired.',
  );
  expect(screen.getByLabelText('Password').props.value).toBe('');
  expect(screen.queryByText('Test Staff')).toBeNull();
  expect(h.storage.set).not.toHaveBeenCalled();
});

test('empty credentials fail before any API request', async () => {
  const h = setup(true);
  await render(
    <SessionProvider client={h.client}>
      <LoginScreen />
    </SessionProvider>,
  );
  await waitFor(() => expect(h.client.getSnapshot().status).toBe('signedOut'));
  await fireEvent.press(screen.getByRole('button', { name: 'Sign in' }));
  expect(await screen.findByRole('alert')).toHaveTextContent(
    'Enter your username and password.',
  );
  expect(h.fetcher).not.toHaveBeenCalled();
});
