import * as SecureStore from 'expo-secure-store';

import type { RefreshStorage } from './session';

const key = 'staff.refresh';
const settings = {
  keychainAccessible: SecureStore.WHEN_UNLOCKED_THIS_DEVICE_ONLY,
};

export function secureRefreshStorage(origin: string): RefreshStorage {
  return {
    async get() {
      const value = await SecureStore.getItemAsync(key, settings);
      if (!value) return null;
      try {
        const saved = JSON.parse(value);
        if (saved.origin === origin && typeof saved.refreshToken === 'string') {
          return saved.refreshToken;
        }
      } catch {
        /* A malformed or different-environment session is discarded. */
      }
      await SecureStore.deleteItemAsync(key, settings);
      return null;
    },
    set: (refreshToken) =>
      SecureStore.setItemAsync(
        key,
        JSON.stringify({ origin, refreshToken }),
        settings,
      ),
    remove: () => SecureStore.deleteItemAsync(key, settings),
  };
}
