import { renderRouter, screen } from 'expo-router/testing-library';

import RootLayout from '../app/_layout';
import StarterScreen from '../app/index';
import LoginScreen from '../app/login';
import SessionScreen from '../app/session';

test('opens the starter through the root Expo Router route', async () => {
  const previousProfile = process.env.EXPO_PUBLIC_APP_ENV;
  const previousUrl = process.env.EXPO_PUBLIC_API_BASE_URL;
  process.env.EXPO_PUBLIC_APP_ENV = 'emulator';
  process.env.EXPO_PUBLIC_API_BASE_URL = 'http://10.0.2.2:5232/api/v1';
  try {
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
  } finally {
    if (previousProfile === undefined) delete process.env.EXPO_PUBLIC_APP_ENV;
    else process.env.EXPO_PUBLIC_APP_ENV = previousProfile;
    if (previousUrl === undefined) delete process.env.EXPO_PUBLIC_API_BASE_URL;
    else process.env.EXPO_PUBLIC_API_BASE_URL = previousUrl;
  }
});
