import { renderRouter, screen } from 'expo-router/testing-library';

import RootLayout from '../app/_layout';
import StarterScreen from '../app/index';

test('opens the starter through the root Expo Router route', async () => {
  await renderRouter(
    { _layout: RootLayout, index: StarterScreen },
    { initialUrl: '/' },
  );

  expect(
    await screen.findByRole('header', { name: 'Staff mobile' }),
  ).toBeVisible();
  expect(screen).toHavePathname('/');
});
