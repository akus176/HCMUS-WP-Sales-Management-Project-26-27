import * as SecureStore from 'expo-secure-store';

import type { RefreshStorage } from './session';
import type { DeviceRegistrationStorage } from '../notifications/deviceRegistration';

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

export function secureDeviceStorage(origin: string): DeviceRegistrationStorage {
  const registrationKey = (owner: string) => `staff.device.${owner}`;
  return {
    async get(owner) {
      const value = await SecureStore.getItemAsync(
        registrationKey(owner),
        settings,
      );
      if (!value) return null;
      try {
        const saved = JSON.parse(value);
        if (saved.origin === origin && typeof saved.id === 'string')
          return saved.id;
      } catch {
        /* Treat corrupt registration metadata as missing. */
      }
      return null;
    },
    set: (owner, id) =>
      SecureStore.setItemAsync(
        registrationKey(owner),
        JSON.stringify({ origin, id }),
        settings,
      ),
    remove: (owner) =>
      SecureStore.deleteItemAsync(registrationKey(owner), settings),
  };
}
