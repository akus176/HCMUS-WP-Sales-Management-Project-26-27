import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import {
  createContext,
  type ReactNode,
  useContext,
  useEffect,
  useState,
  useSyncExternalStore,
} from 'react';

import {
  createSessionClient,
  type SessionClient,
  type SessionState,
} from './session';
import { secureDeviceStorage, secureRefreshStorage } from './secure-storage';
import { readMobileEnvironment } from '../config/environment';

const signedOut: SessionState = { status: 'signedOut', user: null };
const SessionContext = createContext<{
  client: SessionClient | null;
  state: SessionState;
} | null>(null);

export function SessionProvider({
  children,
  client: supplied,
  queryClient: suppliedQueries,
}: {
  children: ReactNode;
  client?: SessionClient;
  queryClient?: QueryClient;
}) {
  const [queries] = useState(
    () =>
      suppliedQueries ??
      new QueryClient({
        defaultOptions: {
          queries: {
            retry: false,
            refetchOnReconnect: true,
          },
          mutations: { retry: false },
        },
      }),
  );
  const [client] = useState(() => {
    if (supplied) return supplied;
    try {
      const { apiOrigin: origin } = readMobileEnvironment();
      return createSessionClient({
        origin,
        storage: secureRefreshStorage(origin),
        deviceStorage: secureDeviceStorage(origin),
        clearData: () => {
          void queries.cancelQueries();
          queries.clear();
        },
      });
    } catch {
      return null;
    }
  });
  const state = useSyncExternalStore(
    client?.subscribe ?? (() => () => {}),
    client?.getSnapshot ?? (() => signedOut),
  );
  useEffect(() => {
    void client?.restore();
  }, [client]);
  return (
    <SessionContext.Provider value={{ client, state }}>
      <QueryClientProvider client={queries}>{children}</QueryClientProvider>
    </SessionContext.Provider>
  );
}

export function useSession() {
  const session = useContext(SessionContext);
  if (!session) throw new Error('SessionProvider is required.');
  return session;
}
