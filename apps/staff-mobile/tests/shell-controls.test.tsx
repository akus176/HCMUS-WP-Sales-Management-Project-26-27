import {
  fireEvent,
  render,
  screen,
  within,
} from '@testing-library/react-native';
import { Dimensions, StyleSheet } from 'react-native';
import { useState } from 'react';
import {
  ShellButton,
  ShellPageHeader,
  ShellHeaderAction,
  ShellSheet,
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

test.each([1, 2])(
  'page header keeps back on the left and refresh on the right at %s× text',
  async (fontScale) => {
    const dimensions = jest
      .spyOn(Dimensions, 'get')
      .mockReturnValue({ width: 390, height: 844, scale: 1, fontScale });
    const refresh = jest.fn();
    const back = jest.fn();
    try {
      await render(
        <ShellPage>
          <ShellPageHeader
            title="Order details"
            backLabel="Back to orders"
            onBack={back}
          />
          <ShellHeaderAction
            kind="refresh"
            label="Refresh order"
            onPress={refresh}
          />
          <ShellText>Order contents</ShellText>
        </ShellPage>,
      );
      const header = within(screen.getByTestId('shell-header'));
      expect(
        header.getByRole('header', { name: 'Order details' }),
      ).toBeVisible();
      const actions = header.getAllByRole('button');
      expect(actions.map((action) => action.props.accessibilityLabel)).toEqual([
        'Back to orders',
        'Refresh order',
      ]);
      for (const action of actions) {
        expect(action).toHaveStyle({ width: 48, height: 48 });
        expect(
          within(screen.getByTestId('shell-scroll')).queryByLabelText(
            action.props.accessibilityLabel,
          ),
        ).toBeNull();
        await fireEvent.press(action);
      }
      expect(back).toHaveBeenCalledTimes(1);
      expect(refresh).toHaveBeenCalledTimes(1);
    } finally {
      dimensions.mockRestore();
    }
  },
);

test('returning from a detail restores the route header without leaving duplicate actions', async () => {
  function Workspace() {
    const [detail, setDetail] = useState(true);
    return (
      <ShellPage>
        <ShellPageHeader
          title="Stock"
          backLabel="Back to home"
          onBack={() => {}}
        />
        {detail && (
          <>
            <ShellPageHeader title="Receipt details" priority={1} />
            <ShellHeaderAction
              kind="back"
              label="Back to receipts"
              onPress={() => setDetail(false)}
            />
          </>
        )}
        <ShellHeaderAction
          kind="refresh"
          label="Refresh stock"
          loading={detail}
          onPress={() => {}}
        />
      </ShellPage>
    );
  }
  await render(<Workspace />);
  expect(screen.getByRole('header', { name: 'Receipt details' })).toBeVisible();
  expect(screen.queryByRole('button', { name: 'Back to home' })).toBeNull();
  expect(screen.getByRole('button', { name: 'Refresh stock' })).toBeDisabled();
  expect(screen.getByRole('button', { name: 'Refresh stock' })).toHaveProp(
    'accessibilityState',
    expect.objectContaining({ busy: true }),
  );
  await fireEvent.press(
    screen.getByRole('button', { name: 'Back to receipts' }),
  );
  const header = within(screen.getByTestId('shell-header'));
  expect(header.getByRole('header', { name: 'Stock' })).toBeVisible();
  expect(header.getByRole('button', { name: 'Back to home' })).toBeVisible();
  expect(header.queryByRole('button', { name: 'Back to receipts' })).toBeNull();
  expect(header.getByRole('button', { name: 'Refresh stock' })).toBeEnabled();
});

test('a workflow sheet owns its header actions and preserves the page underneath', async () => {
  function Workspace() {
    const [opened, setOpened] = useState(true);
    return (
      <ShellPage>
        <ShellPageHeader title="Inventory" />
        <ShellHeaderAction
          kind="refresh"
          label="Refresh inventory"
          onPress={() => {}}
        />
        <ShellSheet
          visible={opened}
          title="Order operations"
          onClose={() => setOpened(false)}
        >
          <ShellPageHeader title="Order details" priority={1} />
          <ShellHeaderAction
            kind="back"
            label="Back to order search"
            onPress={() => setOpened(false)}
          />
          <ShellHeaderAction
            kind="refresh"
            label="Refresh orders"
            onPress={() => {}}
          />
        </ShellSheet>
      </ShellPage>
    );
  }
  await render(<Workspace />);
  const headers = screen.getAllByTestId('shell-header', {
    includeHiddenElements: true,
  });
  expect(headers).toHaveLength(2);
  expect(
    within(headers[0]!).getByRole('header', {
      name: 'Inventory',
      includeHiddenElements: true,
    }),
  ).toBeTruthy();
  expect(
    within(headers[0]!).queryByLabelText('Refresh orders', {
      includeHiddenElements: true,
    }),
  ).toBeNull();
  expect(
    within(headers[1]!).getByRole('button', { name: 'Refresh orders' }),
  ).toBeVisible();
  await fireEvent.press(
    screen.getByRole('button', { name: 'Back to order search' }),
  );
  expect(screen.getAllByTestId('shell-header')).toHaveLength(1);
  expect(screen.getByRole('header', { name: 'Inventory' })).toBeVisible();
  expect(
    screen.getByRole('button', { name: 'Refresh inventory' }),
  ).toBeEnabled();
});

test.each([1, 2])(
  'floating ticket action stays outside scrolling content with clearance at %s× text',
  async (fontScale) => {
    const dimensions = jest
      .spyOn(Dimensions, 'get')
      .mockReturnValue({ width: 390, height: 844, scale: 1, fontScale });
    function Workspace() {
      const [reviewing, setReviewing] = useState(false);
      return (
        <ShellPage>
          <ShellText>Last menu item</ShellText>
          {!reviewing && (
            <ShellPageFooter floating>
              <ShellButton
                label="Your order"
                onPress={() => setReviewing(true)}
              />
            </ShellPageFooter>
          )}
        </ShellPage>
      );
    }
    try {
      await render(<Workspace />);
      const scroll = screen.getByTestId('shell-scroll');
      expect(
        within(scroll).queryByRole('button', { name: 'Your order' }),
      ).toBeNull();
      await fireEvent(screen.getByTestId('shell-floating-action'), 'layout', {
        nativeEvent: { layout: { x: 0, y: 0, width: 350, height: 104 } },
      });
      expect(
        StyleSheet.flatten(scroll.props.contentContainerStyle).paddingBottom,
      ).toBeGreaterThanOrEqual(120);
      await fireEvent.press(screen.getByRole('button', { name: 'Your order' }));
      expect(screen.queryByTestId('shell-floating-action')).toBeNull();
      expect(
        StyleSheet.flatten(scroll.props.contentContainerStyle).paddingBottom ??
          0,
      ).toBe(0);
    } finally {
      dimensions.mockRestore();
    }
  },
);
