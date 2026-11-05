import { useState, type ReactNode } from 'react';
import { Pressable, ScrollView, StyleSheet, Text, View } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';

// Small shell integration boundary. Replace with #140 shared controls when
// delivered; these values preserve the existing sign-in palette.
export const shellTheme = {
  background: '#fff9ed',
  foreground: '#302820',
  primary: '#6d4936',
  focus: '#302820',
};
export function ShellPage({ children }: { children: ReactNode }) {
  return (
    <SafeAreaView style={styles.page}>
      <ScrollView contentContainerStyle={styles.scroll}>
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
}: {
  label: string;
  onPress: () => void;
  selected?: boolean;
  disabled?: boolean;
}) {
  const [focused, setFocused] = useState(false);
  return (
    <Pressable
      accessibilityRole="button"
      accessibilityLabel={label}
      accessibilityState={{ selected, disabled }}
      disabled={disabled}
      onPress={onPress}
      onFocus={() => setFocused(true)}
      onBlur={() => setFocused(false)}
      style={({ pressed }) => [
        styles.button,
        (focused || pressed || selected) && styles.selected,
        disabled && styles.disabled,
      ]}
    >
      <Text style={styles.buttonText}>{selected ? `✓ ${label}` : label}</Text>
    </Pressable>
  );
}
const styles = StyleSheet.create({
  page: { flex: 1, backgroundColor: shellTheme.background },
  scroll: { flexGrow: 1 },
  content: {
    alignSelf: 'center',
    width: '100%',
    maxWidth: 720,
    padding: 24,
    gap: 16,
  },
  heading: { color: shellTheme.foreground, fontSize: 30, fontWeight: '700' },
  text: { color: shellTheme.foreground, fontSize: 18, flexShrink: 1 },
  button: {
    minHeight: 48,
    minWidth: 48,
    padding: 12,
    borderRadius: 8,
    borderWidth: 3,
    borderColor: shellTheme.primary,
    backgroundColor: shellTheme.primary,
    justifyContent: 'center',
    alignItems: 'center',
  },
  selected: {
    borderColor: shellTheme.focus,
    backgroundColor: shellTheme.foreground,
  },
  disabled: { opacity: 0.65 },
  buttonText: { color: shellTheme.background, fontSize: 18, flexShrink: 1 },
});
