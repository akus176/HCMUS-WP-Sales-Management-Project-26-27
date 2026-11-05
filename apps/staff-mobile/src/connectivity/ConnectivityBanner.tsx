import { StyleSheet, View } from 'react-native';

import { ShellButton, ShellFeedback } from '../shell/controls';
import { useConnectivity } from './connectivity';

export function ConnectivityBanner() {
  const { status, refreshConnection } = useConnectivity();
  const message = {
    checking: 'Checking network status…',
    online: 'Online. Data refreshes from the service.',
    reconnecting: 'Reconnecting. Refreshing current data from the service…',
    offline:
      'Offline. Cached data is read-only; changes are disabled. Reconnect and try again.',
  }[status];
  const kind =
    status === 'offline' ? 'warning' : status === 'online' ? 'info' : 'loading';
  const refreshing = status === 'checking' || status === 'reconnecting';
  const actionLabel =
    status === 'offline' ? 'Check connection' : 'Refresh data';

  return (
    <View style={styles.container}>
      <ShellFeedback kind={kind} label={message} />
      <ShellButton
        label={actionLabel}
        loading={refreshing}
        loadingLabel="Refreshing…"
        disabled={refreshing}
        variant="secondary"
        onPress={() => void refreshConnection()}
      />
    </View>
  );
}

const styles = StyleSheet.create({
  container: { gap: 8, paddingHorizontal: 16, paddingTop: 8 },
});
