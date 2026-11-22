import { useState, type ReactNode } from 'react';
import { Pressable, StyleSheet, Text, View } from 'react-native';

import { PageNotice, ShellButton, ShellFeedback } from '../shell/controls';
import { mobileTokens } from '../shell/designTokens';
import { useConnectivity } from './connectivity';

export function ConnectivityPresentation({
  children,
  branded = false,
}: {
  children: ReactNode;
  branded?: boolean;
}) {
  const { status, refreshConnection } = useConnectivity();
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
          <>
            <ShellFeedback
              kind={status === 'offline' ? 'warning' : 'info'}
              label={detail}
            />
            {branded && (
              <ShellButton
                label={
                  status === 'offline' ? 'Check connection' : 'Refresh data'
                }
                variant="secondary"
                onPress={() => void refreshConnection()}
              />
            )}
          </>
        ) : null
      }
    >
      <ConnectivityBanner
        expanded={expanded}
        onToggle={() => setExpanded(!expanded)}
        branded={branded}
      />
      {children}
    </PageNotice.Provider>
  );
}

export function ConnectivityBanner({
  expanded = false,
  onToggle,
  branded = false,
}: { expanded?: boolean; onToggle?: () => void; branded?: boolean } = {}) {
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
    <View style={[styles.container, branded && styles.brandedContainer]}>
      {branded && (
        <Text accessibilityRole="header" style={styles.brand}>
          Corner Cafe
        </Text>
      )}
      {onToggle ? (
        <Pressable
          accessibilityRole="button"
          accessibilityLabel="Connection details"
          accessibilityHint={message}
          accessibilityState={{ expanded }}
          onPress={onToggle}
          style={({ pressed }) => [
            styles.statusControl,
            branded && styles.brandedStatus,
            pressed && styles.pressed,
          ]}
        >
          <Text
            accessibilityLiveRegion="polite"
            style={[styles.summary, branded && styles.brandedSummary]}
          >
            {message} ▾
          </Text>
        </Pressable>
      ) : (
        <Text accessibilityLiveRegion="polite" style={styles.summary}>
          {message}
        </Text>
      )}
      {!branded && (
        <ShellButton
          label={actionLabel}
          loading={refreshing}
          loadingLabel="Refreshing…"
          disabled={refreshing}
          variant="secondary"
          compact
          onPress={() => void refreshConnection()}
        />
      )}
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
  brandedContainer: {
    paddingHorizontal: 24,
    paddingVertical: 4,
    justifyContent: 'space-between',
    borderColor: mobileTokens.color.primary,
  },
  brand: {
    fontFamily: mobileTokens.type.displayFont,
    fontSize: 24,
    color: mobileTokens.color.foreground,
    flexShrink: 1,
  },
  brandedStatus: { flexGrow: 0 },
  brandedSummary: {
    fontSize: mobileTokens.type.body,
    color: mobileTokens.color.info,
  },
});
