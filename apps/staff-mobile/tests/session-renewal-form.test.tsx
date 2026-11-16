import {
  act,
  fireEvent,
  render,
  screen,
  waitFor,
} from '@testing-library/react-native';
import { QueryClient } from '@tanstack/react-query';

import { ConnectivityProvider } from '../src/connectivity/connectivity';
import * as networkAdapter from '../src/connectivity/networkAdapter';
import { InventoryWorkspace } from '../src/features/inventory/InventoryWorkspace';
import { SessionProvider } from '../src/session/context';
import { createSessionClient, type StaffSession } from '../src/session/session';
import { PermissionBoundary } from '../src/shell/PermissionBoundary';
import { deferred, json } from './helpers';

jest.mock('../src/connectivity/networkAdapter', () => ({
  addNetworkStateListener: jest.fn(() => ({ remove: jest.fn() })),
  getNetworkStateAsync: jest.fn(),
}));

const staff: StaffSession = {
  id: '00000000-0000-0000-0000-000000000203',
  fullName: 'Inventory renewal fixture',
  roles: ['InventoryStaff'],
  permissions: ['inventory.manage', 'inventory.adjust'],
};
const ingredient = {
  ingredientId: '00000000-0000-0000-0000-000000000204',
  name: 'Renewal beans',
  baseUnit: 'g',
  availableQuantity: 100,
  reorderThreshold: 10,
};

test.each([false, true])(
  'renewal preserves inventory form only while permissions remain valid (revoked=%s)',
  async (revoked) => {
    jest.mocked(networkAdapter.getNetworkStateAsync).mockResolvedValue({
      isConnected: true,
      isInternetReachable: true,
    });
    let saved: string | null = null;
    let expire = false;
    let renewed = false;
    const grant = deferred<void>();
    const nextUser = revoked ? { ...staff, permissions: [] } : staff;
    const queries = new QueryClient({
      defaultOptions: {
        queries: { retry: false, gcTime: 0 },
        mutations: { retry: false, gcTime: 0 },
      },
    });
    const client = createSessionClient({
      origin: 'https://staff.example',
      storage: {
        get: async () => saved,
        set: async (token) => {
          saved = token;
        },
        remove: async () => {
          saved = null;
        },
      },
      clearData: () => {},
      fetch: (async (request: Request) => {
        const path = new URL(request.url).pathname;
        if (path.endsWith('/auth/login') || path.endsWith('/auth/refresh')) {
          const refresh = path.endsWith('/auth/refresh');
          if (refresh) renewed = true;
          return json({
            accessToken: refresh ? 'new-access' : 'old-access',
            refreshToken: refresh ? 'new-refresh' : 'old-refresh',
            expiresAt: '2099-01-01T00:00:00Z',
            user: refresh ? nextUser : staff,
          });
        }
        if (path.endsWith('/auth/me')) {
          if (
            expire &&
            request.headers.get('Authorization') === 'Bearer old-access'
          )
            return json({}, 401);
          if (renewed) await grant.promise;
          return json(renewed ? nextUser : staff);
        }
        return json({
          items: [ingredient],
          page: 1,
          pageSize: 100,
          totalCount: 1,
        });
      }) as typeof fetch,
    });
    const view = await render(
      <SessionProvider client={client} queryClient={queries}>
        <ConnectivityProvider>
          <PermissionBoundary moduleId="inventory">
            {(user) => <InventoryWorkspace user={user} />}
          </PermissionBoundary>
        </ConnectivityProvider>
      </SessionProvider>,
    );
    await waitFor(() => expect(client.getSnapshot().status).toBe('signedOut'));
    await act(async () => client.login('fixture', 'fixture-password'));
    await screen.findByText('Balance: 100 g');
    await fireEvent.press(
      screen.getByRole('button', {
        name: 'Record adjustment for Renewal beans',
      }),
    );
    await fireEvent.changeText(screen.getByLabelText('Quantity change'), '9');
    await fireEvent.changeText(
      screen.getByLabelText('Adjustment reason'),
      'Keep the unsaved count',
    );
    expire = true;
    let renewal!: Promise<unknown>;
    await act(async () => {
      renewal = client.api.GET('/api/v1/auth/me');
    });
    await waitFor(() => expect(saved).toBe('new-refresh'));
    try {
      if (revoked) {
        expect(screen.queryByLabelText('Quantity change')).toBeNull();
        expect(screen.getByText('Checking access…')).toBeVisible();
      } else {
        expect(screen.getByLabelText('Quantity change')).toHaveDisplayValue(
          '9',
        );
        expect(screen.getByLabelText('Adjustment reason')).toHaveDisplayValue(
          'Keep the unsaved count',
        );
      }
    } finally {
      await act(async () => {
        grant.resolve();
        await renewal;
      });
    }
    if (revoked) {
      expect(
        await screen.findByText(
          'Access unavailable. Your current permissions do not allow this module.',
        ),
      ).toBeVisible();
      expect(screen.queryByLabelText('Quantity change')).toBeNull();
    } else {
      expect(screen.getByLabelText('Quantity change')).toHaveDisplayValue('9');
      expect(screen.getByLabelText('Adjustment reason')).toHaveDisplayValue(
        'Keep the unsaved count',
      );
    }
    await view.unmount();
  },
);
