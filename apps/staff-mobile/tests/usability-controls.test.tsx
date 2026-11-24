import { fireEvent, render, screen } from '@testing-library/react-native';
import {
  ShellButton,
  ShellHeading,
  ShellInput,
  ShellSelection,
} from '../src/shell/controls';
import { mobileTokens } from '../src/shell/designTokens';

test('a labelled field retains validation, scaling and a disabled visual state', async () => {
  await render(
    <ShellInput
      label="Payment amount"
      hint="Use at most two decimal places."
      error="Enter an amount."
      editable={false}
      value=""
    />,
  );
  expect(screen.getByText('Payment amount')).toBeVisible();
  expect(screen.getByLabelText('Payment amount')).toHaveStyle({
    minHeight: 48,
    backgroundColor: mobileTokens.color.disabledBackground,
  });
  expect(screen.getByRole('alert', { name: 'Enter an amount.' })).toBeVisible();
  expect(screen.getByText('Use at most two decimal places.')).toBeVisible();
  expect(
    screen.getByLabelText('Payment amount').props.allowFontScaling,
  ).not.toBe(false);
});

test('filter disclosure exposes expanded state without reducing its target', async () => {
  const onPress = jest.fn();
  await render(
    <ShellButton
      label="Status: All"
      expanded={false}
      compact
      variant="secondary"
      onPress={onPress}
    />,
  );
  const button = screen.getByRole('button', {
    name: 'Status: All',
    expanded: false,
  });
  expect(button).toHaveStyle({ minHeight: 48, minWidth: 48 });
  await fireEvent.press(button);
  expect(onPress).toHaveBeenCalledTimes(1);
});

test('section hierarchy and selection wrapping preserve readable text', async () => {
  await render(
    <>
      <ShellHeading>Inventory</ShellHeading>
      <ShellHeading level="section">Movement history</ShellHeading>
      <ShellSelection>
        <ShellButton label="Low stock" selected onPress={() => {}} />
      </ShellSelection>
    </>,
  );
  expect(screen.getByRole('header', { name: 'Inventory' })).toHaveStyle({
    fontSize: mobileTokens.type.display,
  });
  expect(screen.getByRole('header', { name: 'Movement history' })).toHaveStyle({
    fontSize: mobileTokens.type.title,
  });
  expect(
    screen.getByRole('button', { name: 'Low stock', selected: true }).parent,
  ).toHaveStyle({ flexWrap: 'wrap' });
});
