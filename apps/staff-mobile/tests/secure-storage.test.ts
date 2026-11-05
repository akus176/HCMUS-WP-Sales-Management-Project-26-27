import * as SecureStore from 'expo-secure-store';

import { secureRefreshStorage } from '../src/session/secure-storage';

jest.mock('expo-secure-store', () => ({
  WHEN_UNLOCKED_THIS_DEVICE_ONLY: 'device-only',
  getItemAsync: jest.fn(),
  setItemAsync: jest.fn(),
  deleteItemAsync: jest.fn(),
}));

test('persists only origin and refresh in platform-protected device storage', async () => {
  const storage = secureRefreshStorage('https://staff.example');
  await storage.set('fixture-refresh');
  expect(SecureStore.setItemAsync).toHaveBeenCalledWith(
    'staff.refresh',
    JSON.stringify({
      origin: 'https://staff.example',
      refreshToken: 'fixture-refresh',
    }),
    { keychainAccessible: 'device-only' },
  );
  await storage.remove();
  expect(SecureStore.deleteItemAsync).toHaveBeenCalled();
});

test.each([
  'not-json',
  JSON.stringify({
    origin: 'https://other.example',
    refreshToken: 'fixture-refresh',
  }),
])(
  'discards a malformed or different-environment saved session',
  async (saved) => {
    jest.mocked(SecureStore.getItemAsync).mockResolvedValue(saved);
    const storage = secureRefreshStorage('https://staff.example');
    expect(await storage.get()).toBeNull();
    expect(SecureStore.deleteItemAsync).toHaveBeenCalled();
  },
);

test('reads refresh only for its original API origin', async () => {
  jest.mocked(SecureStore.getItemAsync).mockResolvedValue(
    JSON.stringify({
      origin: 'https://staff.example',
      refreshToken: 'fixture-refresh',
    }),
  );
  expect(await secureRefreshStorage('https://staff.example').get()).toBe(
    'fixture-refresh',
  );
  expect(SecureStore.deleteItemAsync).not.toHaveBeenCalled();
});
