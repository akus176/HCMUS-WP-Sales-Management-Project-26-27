import { fireEvent, render, screen } from '@testing-library/react-native';
import { ShellButton, shellTheme } from '../src/shell/controls';

test('selected and keyboard-focused controls have visible feedback and accessible selection', async () => {
  const view = await render(
    <ShellButton label="Preparation" selected onPress={() => {}} />,
  );
  const button = screen.getByRole('button', {
    name: 'Preparation',
    selected: true,
  });
  expect(button).toHaveStyle({
    backgroundColor: shellTheme.foreground,
    minHeight: 48,
    minWidth: 48,
  });
  expect(screen.getByText('✓ Preparation')).toBeVisible();
  await view.rerender(<ShellButton label="Preparation" onPress={() => {}} />);
  await fireEvent(screen.getByRole('button', { name: 'Preparation' }), 'focus');
  expect(screen.getByRole('button', { name: 'Preparation' })).toHaveStyle({
    borderColor: shellTheme.focus,
  });
});
