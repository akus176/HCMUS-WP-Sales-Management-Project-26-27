import {
  onlineManager,
  notifyManager,
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
  refreshError: string | null;
  beginWriteRefresh: () => void;
  endWriteRefresh: () => void;
};

const ConnectivityContext = createContext<ConnectivityContextValue | null>(
  null,
);

function networkIsOnline(state: NetworkState) {
  return state.isConnected === true && state.isInternetReachable === true;
}

export function ConnectivityProvider({ children }: { children: ReactNode }) {
  const queryClient = useQueryClient();
  const [pendingRefreshes, setPendingRefreshes] = useState(0);
  const [refreshFailures, setRefreshFailures] = useState<string[]>([]);
  const failedHashes = useRef<string[]>([]);
  useEffect(() => {
    const cache = queryClient.getQueryCache();
    let active = true;
    // Query registration can notify synchronously during a child's render.
    // Match React Query's observer scheduling instead of updating its parent
    // in that render; retain the write guard until the deferred snapshot runs.
    const unsubscribe = cache.subscribe(
      notifyManager.batchCalls(() => {
        if (!active) return;
        setRefreshFailures((current) => {
          const remaining = current.filter((hash) => {
            const query = cache.get(hash);
            return (
              query &&
              (query.state.isInvalidated || query.state.status === 'error')
            );
          });
          failedHashes.current = remaining;
          return remaining.length === current.length ? current : remaining;
        });
      }),
    );
    return () => {
      active = false;
      unsubscribe();
    };
  }, [queryClient]);
  const beginWriteRefresh = useCallback(
    () => setPendingRefreshes((count) => count + 1),
    [],
  );
  const endWriteRefresh = useCallback(() => {
    const failures = queryClient
      .getQueryCache()
      .findAll({ type: 'active' })
      .filter(
        (query) => query.state.isInvalidated && query.state.status === 'error',
      )
      .map((query) => query.queryHash);
    setRefreshFailures((current) => {
      const next = [...new Set([...current, ...failures])];
      failedHashes.current = next;
      return next;
    });
    setPendingRefreshes((count) => Math.max(0, count - 1));
  }, [queryClient]);
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
      if (networkIsOnline(network) && failedHashes.current.length) {
        await queryClient.refetchQueries({
          predicate: (query) => failedHashes.current.includes(query.queryHash),
          type: 'all',
        });
      }
    } catch {
      onlineManager.setOnline(false);
      updateStatus('offline');
    }
  }, [applyNetworkState, queryClient, updateStatus]);

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
        isWriteDisabled:
          !canMutate || pendingRefreshes > 0 || refreshFailures.length > 0,
        refreshConnection,
        refreshError: refreshFailures.length
          ? 'The change was saved, but current data could not be refreshed. Refresh before making another change.'
          : null,
        beginWriteRefresh,
        endWriteRefresh,
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
  const {
    status,
    canMutate,
    isWriteDisabled,
    refreshConnection,
    refreshError,
    beginWriteRefresh,
    endWriteRefresh,
  } = useConnectivity();
  const queryClient = useQueryClient();
  const { mutationFn, onSuccess, onError, ...rest } = options;
  const [confirmed, setConfirmed] = useState<{ data: TData } | null>(null);
  const mutation = useMutation<TData, Error, TVariables, unknown>({
    ...rest,
    mutationFn: async (variables) => {
      if (!canMutate || !onlineManager.isOnline())
        throw new OfflineWriteError();
      if (isWriteDisabled)
        throw new Error('Refresh current data before making another change.');
      setConfirmed(null);
      return mutationFn(variables);
    },
    networkMode: 'always',
    retry: false,
    onSuccess: async (data, variables) => {
      // The write is already confirmed. A subsequent slow/failed read must not
      // hide that confirmation or be mistaken for an unsuccessful submission.
      setConfirmed({ data });
      beginWriteRefresh();
      try {
        await onSuccess?.(data, variables);
        await queryClient.invalidateQueries({ type: 'active' });
      } finally {
        endWriteRefresh();
      }
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
    data: confirmed ? confirmed.data : mutation.data,
    reset: () => {
      setConfirmed(null);
      mutation.reset();
    },
    isRefreshing: confirmed !== null && mutation.isPending,
    refreshError,
    status,
    canMutate,
    isWriteDisabled,
    refreshConnection,
  };
}
