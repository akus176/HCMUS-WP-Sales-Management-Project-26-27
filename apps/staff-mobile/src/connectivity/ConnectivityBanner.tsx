import { useState, type ReactNode } from 'react';
import { Pressable, StyleSheet, Text, View } from 'react-native';

import { PageNotice, ShellButton, ShellFeedback } from '../shell/controls';
import { mobileTokens } from '../shell/designTokens';
import { useConnectivity } from './connectivity';

export function ConnectivityPresentation({
  children,
}: {
  children: ReactNode;
}) {
  const { status } = useConnectivity();
  const [expanded, setExpanded] = useState(false);
  const detail =
    status === 'offline'
      ? 'Offline. Cached data is read-only; changes are disabled. Reconnect and try again.'
      : status === 'reconnecting'
        ? 'Reconnecting. Refreshing current data from the service…'
        : 'Network connected. Refresh a workspace to check the latest service data.';
  return (
    <PageNotice.Provider
      value={
        expanded || status === 'offline' || status === 'reconnecting' ? (
          <ShellFeedback
            kind={status === 'offline' ? 'warning' : 'info'}
            label={detail}
          />
        ) : null
      }
    >
      <ConnectivityBanner
        expanded={expanded}
        onToggle={() => setExpanded(!expanded)}
      />
      {children}
    </PageNotice.Provider>
  );
}

export function ConnectivityBanner({
  expanded = false,
  onToggle,
}: { expanded?: boolean; onToggle?: () => void } = {}) {
  const { status, refreshConnection } = useConnectivity();
  const message = {
    checking: 'Checking connection…',
    online: 'Online',
    reconnecting: 'Reconnecting…',
    offline: 'Offline · read-only',
  }[status];
  const refreshing = status === 'checking' || status === 'reconnecting';
  const actionLabel =
    status === 'offline' ? 'Check connection' : 'Refresh data';

  return (
    <View style={styles.container}>
      {onToggle ? (
        <Pressable
          accessibilityRole="button"
          accessibilityLabel="Connection details"
          accessibilityHint={message}
          accessibilityState={{ expanded }}
          onPress={onToggle}
          style={({ pressed }) => [
            styles.statusControl,
            pressed && styles.pressed,
          ]}
        >
          <Text accessibilityLiveRegion="polite" style={styles.summary}>
            {message} ▾
          </Text>
        </Pressable>
      ) : (
        <Text accessibilityLiveRegion="polite" style={styles.summary}>
          {message}
        </Text>
      )}
      <ShellButton
        label={actionLabel}
        loading={refreshing}
        loadingLabel="Refreshing…"
        disabled={refreshing}
        variant="secondary"
        compact
        onPress={() => void refreshConnection()}
      />
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    alignItems: 'center',
    gap: 8,
    paddingHorizontal: 16,
    paddingVertical: 4,
    borderBottomWidth: 1,
    borderColor: mobileTokens.color.disabledBackground,
    backgroundColor: mobileTokens.color.background,
  },
  summary: {
    flexShrink: 1,
    color: mobileTokens.color.foreground,
    fontFamily: mobileTokens.type.bodyFont,
    fontSize: mobileTokens.type.caption,
  },
  statusControl: {
    minHeight: 48,
    minWidth: 48,
    flexGrow: 1,
    flexShrink: 1,
    justifyContent: 'center',
    borderRadius: 8,
    paddingHorizontal: 8,
  },
  pressed: { backgroundColor: mobileTokens.color.highlight },
});
