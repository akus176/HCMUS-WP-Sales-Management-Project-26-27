import NetInfo, { type NetInfoState } from '@react-native-community/netinfo';
import {
  createContext,
  type ReactNode,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useRef,
  useState,
} from 'react';

export type ConnectivityStatus = 'online' | 'reconnecting' | 'offline';
type ConnectivityValue = {
  status: ConnectivityStatus;
  canWrite: () => boolean;
};
export type ConnectivitySource = Pick<
  typeof NetInfo,
  'fetch' | 'addEventListener'
>;

const onlineFallback: ConnectivityValue = {
  status: 'online',
  canWrite: () => true,
};
const ConnectivityContext = createContext<ConnectivityValue>(onlineFallback);

export function classifyConnectivity(state: {
  isConnected: boolean | null;
  isInternetReachable: boolean | null;
}): ConnectivityStatus {
  if (state.isConnected === false || state.isInternetReachable === false)
    return 'offline';
  if (state.isConnected !== true || state.isInternetReachable !== true)
    return 'reconnecting';
  return 'online';
}

export function ConnectivityProvider({
  children,
  source = NetInfo,
}: {
  children: ReactNode;
  source?: ConnectivitySource;
}) {
  const [status, setStatus] = useState<ConnectivityStatus>('reconnecting');
  const statusRef = useRef<ConnectivityStatus>('reconnecting');

  const update = useCallback((state: NetInfoState) => {
    const next = classifyConnectivity(state);
    statusRef.current = next;
    setStatus(next);
  }, []);

  useEffect(() => {
    const unsubscribe = source.addEventListener(update);
    void source
      .fetch()
      .then(update)
      .catch(() => {
        statusRef.current = 'offline';
        setStatus('offline');
      });
    return unsubscribe;
  }, [source, update]);

  const canWrite = useCallback(() => statusRef.current === 'online', []);
  const value = useMemo(() => ({ status, canWrite }), [status, canWrite]);
  return (
    <ConnectivityContext.Provider value={value}>
      {children}
    </ConnectivityContext.Provider>
  );
}

export function useConnectivity() {
  return useContext(ConnectivityContext);
}
