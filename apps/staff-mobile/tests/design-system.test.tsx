import { render, screen } from '@testing-library/react-native';
import {
  ShellButton,
  ShellFeedback,
  ShellPage,
  ShellText,
} from '../src/shell/controls';
import { contrastRatio, mobileTokens } from '../src/shell/designTokens';

test('shared palette defines reusable type, spacing, shape and elevation tokens with readable text pairs', () => {
  expect(mobileTokens.type.body).toBeGreaterThan(0);
  expect(mobileTokens.spacing.md).toBeGreaterThan(0);
  expect(mobileTokens.shape.control).toBeGreaterThan(0);
  expect(mobileTokens.elevation.card).toBeDefined();
  expect(mobileTokens.type.displayFont).toBe('InterSemiBold');
  expect(mobileTokens.type.bodyFont).toBe('InterRegular');

  const textPairs = [
    [mobileTokens.color.foreground, mobileTokens.color.background],
    [mobileTokens.color.onPrimary, mobileTokens.color.primary],
    [mobileTokens.color.error, mobileTokens.color.errorBackground],
    [mobileTokens.color.warning, mobileTokens.color.warningBackground],
    [mobileTokens.color.info, mobileTokens.color.infoBackground],
    [mobileTokens.color.disabledText, mobileTokens.color.disabledBackground],
  ] as const;
  for (const [foreground, background] of textPairs) {
    expect(contrastRatio(foreground, background)).toBeGreaterThanOrEqual(4.5);
  }
});

test.each([
  ['loading', 'progressbar', 'Loading staff modules'],
  ['empty', 'text', 'No permitted modules'],
  ['error', 'alert', 'Could not load staff modules'],
  ['warning', 'alert', 'Connection is unstable'],
] as const)(
  '%s feedback is visible and exposed to assistive technology',
  async (kind, role, label) => {
    await render(<ShellFeedback kind={kind} label={label} />);
    expect(screen.getByRole(role, { name: label })).toBeVisible();
    expect(screen.getByText(label)).toBeVisible();
  },
);

test('disabled and loading primary actions keep a 48-dp target and accessible state', async () => {
  await render(
    <>
      <ShellButton label="Save changes" disabled onPress={() => {}} />
      <ShellButton
        label="Submit order"
        loading
        loadingLabel="Submitting…"
        onPress={() => {}}
      />
    </>,
  );
  const disabled = screen.getByRole('button', {
    name: 'Save changes',
    disabled: true,
  });
  const loading = screen.getByRole('button', {
    name: 'Submit order',
    disabled: true,
    busy: true,
  });
  expect(disabled).toHaveStyle({ minHeight: 48, minWidth: 48 });
  expect(loading).toHaveStyle({ minHeight: 48, minWidth: 48 });
  expect(screen.getByText('Submitting…')).toBeVisible();
});

test('selected secondary actions keep readable text on the selected treatment', async () => {
  await render(
    <ShellButton
      label="Inventory"
      variant="secondary"
      selected
      onPress={() => {}}
    />,
  );
  expect(
    screen.getByRole('button', { name: 'Inventory', selected: true }),
  ).toHaveStyle({
    backgroundColor: mobileTokens.color.selectedBackground,
  });
  expect(screen.getByText('Inventory')).toHaveStyle({
    color: mobileTokens.color.selectedForeground,
  });
});

test('body copy scales and wraps in a scrollable phone and bounded tablet layout', async () => {
  await render(
    <ShellPage>
      <ShellText>Readable copy at large system text sizes</ShellText>
    </ShellPage>,
  );
  const copy = screen.getByText('Readable copy at large system text sizes');
  expect(copy).toHaveStyle({ flexShrink: 1, flexWrap: 'wrap' });
  expect(copy.props.allowFontScaling).not.toBe(false);
  expect(copy.parent).toHaveStyle({
    width: '100%',
    maxWidth: 720,
  });
});
