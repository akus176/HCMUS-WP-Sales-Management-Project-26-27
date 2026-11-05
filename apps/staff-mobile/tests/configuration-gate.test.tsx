import { render, screen } from '@testing-library/react-native';
import { Text } from 'react-native';

import { ConfigurationGate } from '../src/config/ConfigurationGate';
import { parseMobileEnvironment } from '../src/config/environment';

const saved = { ...process.env };
afterEach(() => {
  process.env = { ...saved };
});

test('blocks startup before session children mount and never displays unsafe input', async () => {
  process.env.EXPO_PUBLIC_APP_ENV = 'device';
  process.env.EXPO_PUBLIC_API_BASE_URL =
    'http://staff:private-value@192.168.1.20/api/v1';
  const mounted = jest.fn();
  function SessionProbe() {
    mounted();
    return <Text>Session started</Text>;
  }
  await render(
    <ConfigurationGate
      readConfiguration={() =>
        parseMobileEnvironment(
          'device',
          'http://staff:private-value@192.168.1.20/api/v1',
        )
      }
    >
      <SessionProbe />
    </ConfigurationGate>,
  );
  expect(
    screen.getByRole('header', { name: 'Configuration required' }),
  ).toBeVisible();
  expect(screen.getByRole('alert')).toBeVisible();
  expect(screen.queryByText(/private-value/)).toBeNull();
  expect(mounted).not.toHaveBeenCalled();
});

test('allows startup with valid emulator configuration', async () => {
  process.env.EXPO_PUBLIC_APP_ENV = 'emulator';
  process.env.EXPO_PUBLIC_API_BASE_URL = 'http://10.0.2.2:5232/api/v1';
  await render(
    <ConfigurationGate
      readConfiguration={() =>
        parseMobileEnvironment('emulator', 'http://10.0.2.2:5232/api/v1')
      }
    >
      <Text>Session started</Text>
    </ConfigurationGate>,
  );
  expect(screen.getByText('Session started')).toBeVisible();
});
