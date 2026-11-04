import { renderRouter, screen } from 'expo-router/testing-library';

import RootLayout from '../app/_layout';
import StarterScreen from '../app/index';
import LoginScreen from '../app/login';
import SessionScreen from '../app/session';

test('opens the starter through the root Expo Router route', async () => {
  await renderRouter(
    {
      _layout: RootLayout,
      index: StarterScreen,
      login: LoginScreen,
      session: SessionScreen,
    },
    { initialUrl: '/' },
  );

  expect(
    await screen.findByRole('header', { name: 'Staff mobile' }),
  ).toBeVisible();
  expect(screen).toHavePathname('/');
});
