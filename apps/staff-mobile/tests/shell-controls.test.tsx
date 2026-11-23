import {
  fireEvent,
  render,
  screen,
  within,
} from '@testing-library/react-native';
import { Dimensions } from 'react-native';
import {
  ShellButton,
  ShellPage,
  ShellPageFooter,
  ShellText,
  shellTheme,
} from '../src/shell/controls';

test.each([1, 2])(
  'primary actions remain reachable at %s× text without covering scrollable content',
  async (fontScale) => {
    const get = jest
      .spyOn(Dimensions, 'get')
      .mockReturnValue({ width: 390, height: 844, scale: 1, fontScale });
    try {
      await render(
        <ShellPage>
          <ShellText>Ticket details</ShellText>
          <ShellPageFooter>
            <ShellButton label="Confirm ready" onPress={() => {}} />
          </ShellPageFooter>
        </ShellPage>,
      );
      const scroll = screen.getByTestId('shell-scroll');
      expect(
        screen.getByRole('button', { name: 'Confirm ready' }),
      ).toBeVisible();
      if (fontScale === 1)
        expect(
          within(scroll).queryByRole('button', { name: 'Confirm ready' }),
        ).toBeNull();
      else
        expect(
          within(scroll).getByRole('button', { name: 'Confirm ready' }),
        ).toBeVisible();
    } finally {
      get.mockRestore();
    }
  },
);

test('selected and keyboard-focused controls have visible feedback and accessible selection', async () => {
  const view = await render(
    <ShellButton label="Preparation" selected onPress={() => {}} />,
  );
  const button = screen.getByRole('button', {
    name: 'Preparation',
    selected: true,
  });
  expect(button).toHaveStyle({
    backgroundColor: '#eee8df',
    minHeight: 48,
    minWidth: 48,
  });
  expect(screen.getByText('Preparation')).toBeVisible();
  await view.rerender(<ShellButton label="Preparation" onPress={() => {}} />);
  await fireEvent(screen.getByRole('button', { name: 'Preparation' }), 'focus');
  expect(screen.getByRole('button', { name: 'Preparation' })).toHaveStyle({
    borderColor: shellTheme.focus,
  });
});
