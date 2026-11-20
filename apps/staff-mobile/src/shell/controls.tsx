import {
  createContext,
  useContext,
  useRef,
  useState,
  type ComponentRef,
  type ReactNode,
  type Ref,
} from 'react';
import {
  AccessibilityInfo,
  Pressable,
  ScrollView,
  StyleSheet,
  Text,
  TextInput,
  View,
  type TextInputProps,
} from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { mobileTokens } from './designTokens';

// Compatibility alias while existing shell consumers use this name.
export const shellTheme = {
  background: mobileTokens.color.background,
  foreground: mobileTokens.color.foreground,
  primary: mobileTokens.color.primary,
  focus: mobileTokens.color.focus,
};

const PageNavigation = createContext({
  openDetail: (_key: string) => {},
  returnToList: () => {},
  returnTarget: null as string | null,
  finishReturn: () => {},
});

export const usePageNavigation = () => useContext(PageNavigation);
export const PageNotice = createContext<ReactNode>(null);

export function ShellPage({ children }: { children: ReactNode }) {
  const notice = useContext(PageNotice);
  const scroll = useRef<ComponentRef<typeof ScrollView>>(null);
  const position = useRef(0);
  const origin = useRef({ key: '', y: 0 });
  const restore = useRef(false);
  const [returnTarget, setReturnTarget] = useState<string | null>(null);
  return (
    <SafeAreaView edges={['bottom']} style={styles.page}>
      <ScrollView
        ref={scroll}
        contentContainerStyle={styles.scroll}
        keyboardShouldPersistTaps="handled"
        onScroll={(event) => {
          position.current = event.nativeEvent.contentOffset.y;
        }}
        scrollEventThrottle={16}
        onContentSizeChange={() => {
          if (restore.current) {
            scroll.current?.scrollTo({ y: origin.current.y, animated: false });
            restore.current = false;
          }
        }}
      >
        <PageNavigation.Provider
          value={{
            openDetail: (key) => {
              origin.current = { key, y: position.current };
              setReturnTarget(null);
              scroll.current?.scrollTo({ y: 0, animated: false });
            },
            returnToList: () => {
              restore.current = true;
              setReturnTarget(origin.current.key);
            },
            returnTarget,
            finishReturn: () => setReturnTarget(null),
          }}
        >
          <View style={styles.content}>
            {notice}
            {children}
          </View>
        </PageNavigation.Provider>
      </ScrollView>
    </SafeAreaView>
  );
}

export function ShellHeading({
  children,
  level = 'page',
  focusOnMount = false,
}: {
  children: ReactNode;
  level?: 'page' | 'section';
  focusOnMount?: boolean;
}) {
  const ref = useRef<ComponentRef<typeof Text>>(null);
  const focused = useRef(false);
  return (
    <Text
      ref={ref}
      accessible
      accessibilityRole="header"
      style={[styles.heading, level === 'section' && styles.sectionHeading]}
      onLayout={() => {
        if (focusOnMount && !focused.current && ref.current) {
          focused.current = true;
          AccessibilityInfo.sendAccessibilityEvent(ref.current, 'focus');
        }
      }}
    >
      {children}
    </Text>
  );
}

export function ShellText({ children }: { children: ReactNode }) {
  return <Text style={styles.text}>{children}</Text>;
}

export function ShellPageHeader({
  title,
  backLabel,
  onBack,
}: {
  title?: string;
  backLabel?: string;
  onBack?: () => void;
}) {
  return (
    <View style={styles.header}>
      {backLabel && onBack && (
        <ShellButton label={backLabel} variant="secondary" onPress={onBack} />
      )}
      {title && <ShellHeading>{title}</ShellHeading>}
    </View>
  );
}

export function ShellInput({
  label,
  hint,
  error,
  style,
  accessibilityLabel,
  ...props
}: TextInputProps & {
  label: string;
  hint?: string;
  error?: string;
  ref?: Ref<ComponentRef<typeof TextInput>>;
}) {
  return (
    <View style={styles.field}>
      <Text
        accessible={false}
        importantForAccessibility="no"
        style={styles.text}
      >
        {label}
      </Text>
      <TextInput
        {...props}
        accessibilityLabel={accessibilityLabel ?? label}
        accessibilityHint={props.accessibilityHint ?? hint}
        placeholderTextColor={mobileTokens.color.disabledText}
        style={[
          styles.input,
          props.editable === false && styles.disabledInput,
          error && styles.invalidInput,
          style,
        ]}
      />
      {hint && <ShellText>{hint}</ShellText>}
      {error && <ShellFeedback kind="error" label={error} />}
    </View>
  );
}

export function ShellSelection({ children }: { children: ReactNode }) {
  return <View style={styles.selection}>{children}</View>;
}

export function ShellMutationFeedback({
  mutation,
}: {
  mutation: { isRefreshing: boolean; refreshError: string | null };
}) {
  return mutation.refreshError ? (
    <ShellFeedback kind="warning" label={mutation.refreshError} />
  ) : mutation.isRefreshing ? (
    <ShellFeedback
      kind="loading"
      label="Change saved. Refreshing current data…"
    />
  ) : null;
}

export type ShellFeedbackKind =
  'loading' | 'empty' | 'error' | 'warning' | 'info' | 'success';

const feedbackColors = {
  loading: [mobileTokens.color.info, mobileTokens.color.infoBackground],
  empty: [mobileTokens.color.foreground, mobileTokens.color.background],
  error: [mobileTokens.color.error, mobileTokens.color.errorBackground],
  warning: [mobileTokens.color.warning, mobileTokens.color.warningBackground],
  info: [mobileTokens.color.info, mobileTokens.color.infoBackground],
  success: [mobileTokens.color.foreground, mobileTokens.color.highlight],
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
  expanded,
  focusKey,
  compact = false,
}: {
  label: string;
  onPress: () => void;
  selected?: boolean;
  disabled?: boolean;
  loading?: boolean;
  loadingLabel?: string;
  variant?: 'primary' | 'secondary';
  expanded?: boolean;
  focusKey?: string;
  compact?: boolean;
}) {
  const [focused, setFocused] = useState(false);
  const ref = useRef<ComponentRef<typeof View>>(null);
  const navigation = usePageNavigation();
  const unavailable = disabled || loading;
  return (
    <Pressable
      ref={ref}
      accessibilityRole="button"
      accessibilityLabel={label}
      accessibilityState={{
        selected,
        disabled: unavailable,
        busy: loading,
        expanded,
      }}
      disabled={unavailable}
      onPress={onPress}
      onFocus={() => setFocused(true)}
      onBlur={() => setFocused(false)}
      onLayout={() => {
        if (focusKey && navigation.returnTarget === focusKey && ref.current) {
          AccessibilityInfo.sendAccessibilityEvent(ref.current, 'focus');
          navigation.finishReturn();
        }
      }}
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
          compact && styles.compactButtonText,
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
  header: { gap: mobileTokens.spacing.sm, alignItems: 'flex-start' },
  field: { gap: mobileTokens.spacing.sm },
  selection: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    gap: mobileTokens.spacing.sm,
  },
  input: {
    minHeight: 48,
    borderWidth: 2,
    borderColor: mobileTokens.color.border,
    borderRadius: mobileTokens.shape.control,
    padding: mobileTokens.spacing.sm,
    color: mobileTokens.color.foreground,
    backgroundColor: mobileTokens.color.background,
    fontFamily: mobileTokens.type.bodyFont,
    fontSize: mobileTokens.type.body,
  },
  disabledInput: { backgroundColor: mobileTokens.color.disabledBackground },
  invalidInput: { borderColor: mobileTokens.color.error },
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
  sectionHeading: { fontSize: mobileTokens.type.title },
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
    flexShrink: 1,
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
  compactButtonText: { fontSize: mobileTokens.type.caption },
  disabledButtonText: { color: mobileTokens.color.disabledText },
});
