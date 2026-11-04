import { render, screen } from '@testing-library/react-native';

import StarterScreen from '../app/index';

test('shows an accessible starter without claiming a working staff session', async () => {
  await render(<StarterScreen />);

  expect(screen.getByRole('header', { name: 'Staff mobile' })).toBeVisible();
  expect(
    screen.getByText('No staff account or shop data is loaded.'),
  ).toBeVisible();
  expect(screen.queryByRole('button')).toBeNull();
});
