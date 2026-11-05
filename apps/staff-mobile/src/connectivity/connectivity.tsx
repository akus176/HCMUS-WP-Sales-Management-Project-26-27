import {
  onlineManager,
  useMutation,
  useQueryClient,
  type UseMutationOptions,
} from '@tanstack/react-query';
import {
  createContext,
  type ReactNode,
  useCallback,
  useContext,
  useEffect,
  useRef,
  useState,
} from 'react';

import { ApiError } from '../api/errors';
import {
  addNetworkStateListener,
  getNetworkStateAsync,
  type NetworkState,
} from './networkAdapter';

export type ConnectivityStatus =
  'checking' | 'online' | 'reconnecting' | 'offline';

export class OfflineWriteError extends Error {
  constructor() {
    super(
      'You are offline. No change was sent or saved. Reconnect and try again.',
    );
    this.name = 'OfflineWriteError';
  }
}

type ConnectivityContextValue = {
  status: ConnectivityStatus;
  canMutate: boolean;
  isWriteDisabled: boolean;
  refreshConnection: () => Promise<void>;
};

const ConnectivityContext = createContext<ConnectivityContextValue | null>(
  null,
);

function networkIsOnline(state: NetworkState) {
  return state.isConnected === true && state.isInternetReachable === true;
}

export function ConnectivityProvider({ children }: { children: ReactNode }) {
  const queryClient = useQueryClient();
  const [status, setStatus] = useState<ConnectivityStatus>(() => {
    // Prevent queries from racing ahead of the first native network snapshot.
    onlineManager.setOnline(false);
    return 'checking';
  });
  const statusRef = useRef(status);
  const mounted = useRef(false);
  const networkRevision = useRef(0);

  const updateStatus = useCallback((next: ConnectivityStatus) => {
    statusRef.current = next;
    setStatus(next);
  }, []);

  const applyNetworkState = useCallback(
    async (network: NetworkState, manualRefresh = false) => {
      if (!mounted.current) return;
      const revision = ++networkRevision.current;
      if (!networkIsOnline(network)) {
        onlineManager.setOnline(false);
        updateStatus('offline');
        return;
      }

      const wasOffline = statusRef.current === 'offline';
      onlineManager.setOnline(true);
      if (manualRefresh || wasOffline) {
        updateStatus('reconnecting');
        await queryClient.invalidateQueries({ type: 'active' });
      }
      if (mounted.current && revision === networkRevision.current)
        updateStatus('online');
    },
    [queryClient, updateStatus],
  );

  const refreshConnection = useCallback(async () => {
    try {
      const network = await getNetworkStateAsync();
      await applyNetworkState(network, true);
    } catch {
      onlineManager.setOnline(false);
      updateStatus('offline');
    }
  }, [applyNetworkState, updateStatus]);

  useEffect(() => {
    mounted.current = true;
    const subscription = addNetworkStateListener((network) =>
      applyNetworkState(network),
    );
    void getNetworkStateAsync()
      .then((network) => {
        if (mounted.current) return applyNetworkState(network);
      })
      .catch(() => {
        if (!mounted.current) return;
        onlineManager.setOnline(false);
        updateStatus('offline');
      });

    return () => {
      mounted.current = false;
      subscription?.remove?.();
    };
  }, [applyNetworkState, updateStatus]);

  const canMutate = status === 'online' && onlineManager.isOnline();
  return (
    <ConnectivityContext.Provider
      value={{
        status,
        canMutate,
        isWriteDisabled: !canMutate,
        refreshConnection,
      }}
    >
      {children}
    </ConnectivityContext.Provider>
  );
}

export function useConnectivity() {
  const connectivity = useContext(ConnectivityContext);
  if (!connectivity) throw new Error('ConnectivityProvider is required.');
  return connectivity;
}

type SafeMutationOptions<TData, TVariables> = Omit<
  UseMutationOptions<TData, Error, TVariables, unknown>,
  'mutationFn' | 'networkMode' | 'retry' | 'onSuccess' | 'onError'
> & {
  mutationFn: (variables: TVariables) => Promise<TData>;
  onSuccess?: (data: TData, variables: TVariables) => void | Promise<void>;
  onError?: (error: Error, variables: TVariables) => void | Promise<void>;
};

/**
 * A mutation boundary for online-only writes. `networkMode: 'always'` ensures
 * TanStack Query runs the offline guard instead of pausing and replaying later.
 */
export function useSafeMutation<TData = unknown, TVariables = void>(
  options: SafeMutationOptions<TData, TVariables>,
) {
  const { status, canMutate, isWriteDisabled, refreshConnection } =
    useConnectivity();
  const queryClient = useQueryClient();
  const { mutationFn, onSuccess, onError, ...rest } = options;
  const mutation = useMutation<TData, Error, TVariables, unknown>({
    ...rest,
    mutationFn: async (variables) => {
      if (!canMutate || !onlineManager.isOnline())
        throw new OfflineWriteError();
      return mutationFn(variables);
    },
    networkMode: 'always',
    retry: false,
    onSuccess: async (data, variables) => {
      await queryClient.invalidateQueries({ type: 'active' });
      await onSuccess?.(data, variables);
    },
    onError: async (error, variables) => {
      // A conflict means the cached revision lost authority; fetch the latest
      // server state before offering another attempt.
      if (error instanceof ApiError && error.status === 409)
        await queryClient.invalidateQueries({ type: 'active' });
      await onError?.(error, variables);
    },
  });
  return {
    ...mutation,
    status,
    canMutate,
    isWriteDisabled,
    refreshConnection,
  };
}
