import { useState, type ReactNode } from 'react';
import { Pressable, ScrollView, StyleSheet, Text, View } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { mobileTokens } from './designTokens';
import { useConnectivity } from '../network/ConnectivityContext';

// Compatibility alias while existing shell consumers use this name.
export const shellTheme = {
  background: mobileTokens.color.background,
  foreground: mobileTokens.color.foreground,
  primary: mobileTokens.color.primary,
  focus: mobileTokens.color.focus,
};

export function ShellPage({ children }: { children: ReactNode }) {
  const { status } = useConnectivity();
  return (
    <SafeAreaView style={styles.page}>
      {status !== 'online' && (
        <View style={styles.connectionBanner}>
          <Text
            accessibilityRole="alert"
            accessibilityLiveRegion="polite"
            style={styles.connectionText}
          >
            {status === 'offline'
              ? 'Offline. Reconnect to refresh data or make changes.'
              : 'Reconnecting. Changes are disabled until the service is reachable.'}
          </Text>
        </View>
      )}
      <ScrollView
        contentContainerStyle={styles.scroll}
        keyboardShouldPersistTaps="handled"
      >
        <View style={styles.content}>{children}</View>
      </ScrollView>
    </SafeAreaView>
  );
}

export function ShellHeading({ children }: { children: ReactNode }) {
  return (
    <Text accessibilityRole="header" style={styles.heading}>
      {children}
    </Text>
  );
}

export function ShellText({ children }: { children: ReactNode }) {
  return <Text style={styles.text}>{children}</Text>;
}

export type ShellFeedbackKind =
  'loading' | 'empty' | 'error' | 'warning' | 'info';

const feedbackColors = {
  loading: [mobileTokens.color.info, mobileTokens.color.infoBackground],
  empty: [mobileTokens.color.foreground, mobileTokens.color.background],
  error: [mobileTokens.color.error, mobileTokens.color.errorBackground],
  warning: [mobileTokens.color.warning, mobileTokens.color.warningBackground],
  info: [mobileTokens.color.info, mobileTokens.color.infoBackground],
} as const;

export function ShellFeedback({
  kind,
  label,
}: {
  kind: ShellFeedbackKind;
  label: string;
}) {
  const [color, backgroundColor] = feedbackColors[kind];
  const accessibilityRole =
    kind === 'loading'
      ? 'progressbar'
      : kind === 'error' || kind === 'warning'
        ? 'alert'
        : 'text';
  return (
    <View style={[styles.feedback, { backgroundColor, borderColor: color }]}>
      <Text
        accessibilityRole={accessibilityRole}
        accessibilityLabel={label}
        accessibilityLiveRegion={kind === 'error' ? 'assertive' : 'polite'}
        style={[styles.feedbackText, { color }]}
      >
        {label}
      </Text>
    </View>
  );
}

/** Backwards-compatible status component; prefer ShellFeedback for new views. */
export function ShellStatus({ children }: { children: ReactNode }) {
  return (
    <Text
      accessibilityRole="alert"
      accessibilityLiveRegion="polite"
      style={styles.text}
    >
      {children}
    </Text>
  );
}

export function ShellButton({
  label,
  onPress,
  selected = false,
  disabled = false,
  loading = false,
  loadingLabel = 'Loading…',
  variant = 'primary',
}: {
  label: string;
  onPress: () => void;
  selected?: boolean;
  disabled?: boolean;
  loading?: boolean;
  loadingLabel?: string;
  variant?: 'primary' | 'secondary';
}) {
  const [focused, setFocused] = useState(false);
  const unavailable = disabled || loading;
  return (
    <Pressable
      accessibilityRole="button"
      accessibilityLabel={label}
      accessibilityState={{ selected, disabled: unavailable, busy: loading }}
      disabled={unavailable}
      onPress={onPress}
      onFocus={() => setFocused(true)}
      onBlur={() => setFocused(false)}
      style={({ pressed }) => [
        styles.button,
        variant === 'secondary' && styles.secondaryButton,
        (focused || selected) && styles.selected,
        pressed && styles.pressed,
        unavailable && styles.disabled,
      ]}
    >
      <Text
        style={[
          styles.buttonText,
          variant === 'secondary' &&
            !(focused || selected) &&
            styles.secondaryButtonText,
          unavailable && styles.disabledButtonText,
        ]}
      >
        {loading ? loadingLabel : selected ? `✓ ${label}` : label}
      </Text>
    </Pressable>
  );
}

const styles = StyleSheet.create({
  page: { flex: 1, backgroundColor: mobileTokens.color.background },
  connectionBanner: {
    backgroundColor: mobileTokens.color.warningBackground,
    borderBottomColor: mobileTokens.color.warning,
    borderBottomWidth: 2,
    paddingHorizontal: mobileTokens.spacing.md,
    paddingVertical: mobileTokens.spacing.sm,
  },
  connectionText: {
    color: mobileTokens.color.warning,
    fontSize: mobileTokens.type.body,
    fontFamily: mobileTokens.type.bodyFont,
    textAlign: 'center',
  },
  scroll: { flexGrow: 1 },
  content: {
    alignSelf: 'center',
    width: '100%',
    maxWidth: 720,
    padding: mobileTokens.spacing.lg,
    gap: mobileTokens.spacing.md,
  },
  heading: {
    color: mobileTokens.color.foreground,
    fontSize: mobileTokens.type.display,
    fontFamily: mobileTokens.type.displayFont,
    fontWeight: '700',
    flexShrink: 1,
    flexWrap: 'wrap',
  },
  text: {
    color: mobileTokens.color.foreground,
    fontSize: mobileTokens.type.body,
    fontFamily: mobileTokens.type.bodyFont,
    flexShrink: 1,
    flexWrap: 'wrap',
  },
  feedback: {
    borderLeftWidth: 4,
    borderRadius: mobileTokens.shape.control,
    padding: mobileTokens.spacing.md,
  },
  feedbackText: {
    fontSize: mobileTokens.type.body,
    fontFamily: mobileTokens.type.bodyFont,
    flexShrink: 1,
    flexWrap: 'wrap',
  },
  button: {
    minHeight: 48,
    minWidth: 48,
    paddingHorizontal: mobileTokens.spacing.md,
    paddingVertical: mobileTokens.spacing.sm,
    borderRadius: mobileTokens.shape.control,
    borderWidth: 3,
    borderColor: mobileTokens.color.primary,
    backgroundColor: mobileTokens.color.primary,
    justifyContent: 'center',
    alignItems: 'center',
  },
  secondaryButton: {
    backgroundColor: mobileTokens.color.background,
  },
  selected: {
    borderColor: mobileTokens.color.focus,
    backgroundColor: mobileTokens.color.selectedBackground,
  },
  pressed: { borderColor: mobileTokens.color.focus, borderWidth: 4 },
  disabled: {
    borderColor: mobileTokens.color.disabledText,
    backgroundColor: mobileTokens.color.disabledBackground,
  },
  buttonText: {
    color: mobileTokens.color.onPrimary,
    fontSize: mobileTokens.type.body,
    fontFamily: mobileTokens.type.bodyFont,
    textAlign: 'center',
    flexShrink: 1,
    flexWrap: 'wrap',
  },
  secondaryButtonText: { color: mobileTokens.color.foreground },
  disabledButtonText: { color: mobileTokens.color.disabledText },
});
