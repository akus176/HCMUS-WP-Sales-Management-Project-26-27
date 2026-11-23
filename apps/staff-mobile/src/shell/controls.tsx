import {
  createContext,
  useContext,
  useEffect,
  useLayoutEffect,
  useMemo,
  useRef,
  useState,
  type ComponentRef,
  type ReactNode,
  type Ref,
} from 'react';
import {
  AccessibilityInfo,
  KeyboardAvoidingView,
  Modal,
  Platform,
  Pressable,
  ScrollView,
  StyleSheet,
  Text,
  TextInput,
  View,
  useWindowDimensions,
  type TextInputProps,
} from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { mobileTokens } from './designTokens';
import { useNativeNavigation } from './NativeNavigation';
import { ShellIcon } from './ShellIcon';

// Compatibility alias while existing shell consumers use this name.
export const shellTheme = {
  background: mobileTokens.color.background,
  foreground: mobileTokens.color.foreground,
  primary: mobileTokens.color.primary,
  focus: mobileTokens.color.focus,
};

const PageNavigation = createContext({
  detailActive: false,
  focusTarget: (_key: string) => {},
  rememberPosition: (_key: string) => {},
  openDetail: (_key: string) => {},
  returnToList: () => {},
  returnTarget: null as string | null,
  finishReturn: () => {},
  scrollToTop: () => {},
});

export const usePageNavigation = () => useContext(PageNavigation);
export const PageNotice = createContext<ReactNode>(null);
const PageFooter = createContext<(content: ReactNode) => void>(() => {});
const PageHeader = createContext<((content: ReactNode) => void) | null>(null);

/** Reserve space above the scrolling content for persistent page controls. */
export function ShellPinnedHeader({
  children,
  enabled = true,
}: {
  children: ReactNode;
  enabled?: boolean;
}) {
  const setHeader = useContext(PageHeader);
  useLayoutEffect(() => {
    if (!setHeader || !enabled) return;
    setHeader(children);
    return () => setHeader(null);
  }, [children, enabled, setHeader]);
  return setHeader && enabled ? null : <>{children}</>;
}

/** Keep working actions in reach; enlarged text reflows them into the page. */
export function ShellPageFooter({ children }: { children: ReactNode }) {
  const setFooter = useContext(PageFooter);
  const { fontScale, height } = useWindowDimensions();
  const inline = fontScale > 1.5 || height < 560;
  useEffect(() => {
    setFooter(inline ? null : children);
    return () => setFooter(null);
  }, [children, inline, setFooter]);
  return inline ? <View>{children}</View> : null;
}

export function ShellPage({ children }: { children: ReactNode }) {
  const nativeNavigation = useNativeNavigation();
  const notice = useContext(PageNotice);
  const scroll = useRef<ComponentRef<typeof ScrollView>>(null);
  const position = useRef(0);
  const origin = useRef({ key: '', y: 0 });
  const restore = useRef(false);
  const [returnTarget, setReturnTarget] = useState<string | null>(null);
  const [detailActive, setDetailActive] = useState(false);
  const [footer, setFooter] = useState<ReactNode>(null);
  const [header, setHeader] = useState<ReactNode>(null);
  // Footer updates must not recreate navigation and retrigger its consumers.
  const navigation = useMemo(
    () => ({
      detailActive,
      focusTarget: setReturnTarget,
      rememberPosition: (key: string) => {
        origin.current = { key, y: position.current };
        setReturnTarget(null);
      },
      openDetail: (key: string) => {
        setDetailActive(true);
        origin.current = { key, y: position.current };
        setReturnTarget(null);
        scroll.current?.scrollTo({ y: 0, animated: false });
      },
      returnToList: () => {
        setDetailActive(false);
        restore.current = true;
        setReturnTarget(origin.current.key);
      },
      returnTarget,
      finishReturn: () => setReturnTarget(null),
      scrollToTop: () => scroll.current?.scrollTo({ y: 0, animated: false }),
    }),
    [detailActive, returnTarget],
  );
  return (
    <SafeAreaView
      edges={nativeNavigation?.user ? [] : ['bottom']}
      style={styles.page}
    >
      <PageNavigation.Provider value={navigation}>
        <PageFooter.Provider value={setFooter}>
          <PageHeader.Provider value={setHeader}>
            {header && <View style={styles.pinnedHeader}>{header}</View>}
            <ScrollView
              testID="shell-scroll"
              ref={scroll}
              contentContainerStyle={styles.scroll}
              keyboardShouldPersistTaps="handled"
              onScroll={(event) => {
                position.current = event.nativeEvent.contentOffset.y;
              }}
              scrollEventThrottle={16}
              onContentSizeChange={() => {
                if (restore.current) {
                  scroll.current?.scrollTo({
                    y: origin.current.y,
                    animated: false,
                  });
                  restore.current = false;
                }
              }}
            >
              <View style={styles.content}>
                {notice}
                {children}
              </View>
            </ScrollView>
            {footer}
          </PageHeader.Provider>
        </PageFooter.Provider>
      </PageNavigation.Provider>
    </SafeAreaView>
  );
}

export function ShellHeading({
  children,
  level = 'page',
  focusOnMount = false,
  accessibilityLabel,
}: {
  children: ReactNode;
  level?: 'page' | 'section';
  focusOnMount?: boolean;
  accessibilityLabel?: string;
}) {
  const ref = useRef<ComponentRef<typeof Text>>(null);
  const focused = useRef(false);
  return (
    <Text
      ref={ref}
      accessible
      accessibilityRole="header"
      accessibilityLabel={accessibilityLabel}
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
  hideDuringDetail = false,
  inline = false,
}: {
  title?: string;
  backLabel?: string;
  onBack?: () => void;
  hideDuringDetail?: boolean;
  inline?: boolean;
}) {
  const navigation = usePageNavigation();
  const nativeHeader = !!useNativeNavigation()?.user;
  const { fontScale } = useWindowDimensions();
  const inlineLayout = inline && fontScale <= 1.5;
  if (hideDuringDetail && navigation.detailActive) return null;
  return (
    <View style={[styles.header, inlineLayout && styles.inlineHeader]}>
      {backLabel && onBack && backLabel !== 'Back to staff home' && (
        <Pressable
          accessibilityRole="button"
          accessibilityLabel={backLabel}
          onPress={onBack}
          style={styles.backButton}
        >
          <ShellIcon name="back" />
        </Pressable>
      )}
      {inlineLayout && title && <ShellHeading>{title}</ShellHeading>}
      {backLabel === 'Back to staff home' && onBack && !nativeHeader && (
        <ShellButton
          label={backLabel}
          displayLabel="Staff home"
          variant="text"
          onPress={onBack}
        />
      )}
      {!inlineLayout && title && <ShellHeading>{title}</ShellHeading>}
    </View>
  );
}

export function ShellInput({
  label,
  hint,
  error,
  style,
  accessibilityLabel,
  hideLabel = false,
  ...props
}: TextInputProps & {
  label: string;
  hint?: string;
  error?: string;
  ref?: Ref<ComponentRef<typeof TextInput>>;
  hideLabel?: boolean;
}) {
  return (
    <View style={styles.field}>
      {!hideLabel && (
        <Text
          accessible={false}
          importantForAccessibility="no"
          style={styles.text}
        >
          {label}
        </Text>
      )}
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

const SegmentedSelection = createContext(false);
export function ShellSelection({
  children,
  segmented = false,
}: {
  children: ReactNode;
  segmented?: boolean;
}) {
  return (
    <SegmentedSelection.Provider value={segmented}>
      <View style={[styles.selection, segmented && styles.segmented]}>
        {children}
      </View>
    </SegmentedSelection.Provider>
  );
}

export function ShellBadge({
  label,
  kind = 'neutral',
}: {
  label: string;
  kind?: 'neutral' | 'info' | 'attention';
}) {
  return (
    <Text
      style={[
        styles.badge,
        kind === 'info' && styles.infoBadge,
        kind === 'attention' && styles.attentionBadge,
      ]}
    >
      {label}
    </Text>
  );
}

/** Modal reviews reserve their own scrollable, keyboard-aware safe area. */
export function ShellSheet({
  visible,
  title,
  onClose,
  busy = false,
  children,
}: {
  visible: boolean;
  title: string;
  onClose: () => void;
  busy?: boolean;
  children: ReactNode;
}) {
  const { height } = useWindowDimensions();
  return (
    <Modal
      visible={visible}
      transparent
      animationType="none"
      onRequestClose={() => {
        if (!busy) onClose();
      }}
    >
      <KeyboardAvoidingView
        behavior={Platform.OS === 'ios' ? 'padding' : 'height'}
        style={styles.sheetScreen}
      >
        <Pressable
          accessibilityLabel={`Dismiss ${title}`}
          accessibilityRole="button"
          disabled={busy}
          onPress={onClose}
          style={styles.sheetBackdrop}
        />
        <SafeAreaView
          edges={['bottom']}
          accessibilityViewIsModal
          style={[styles.sheet, { maxHeight: height * 0.9 }]}
        >
          <View style={styles.sheetHandle} />
          <View style={styles.sheetHeader}>
            <ShellHeading focusOnMount>{title}</ShellHeading>
            <Pressable
              style={styles.backButton}
              accessibilityRole="button"
              accessibilityLabel={`Close ${title}`}
              disabled={busy}
              onPress={onClose}
            >
              <ShellIcon name="close" />
            </Pressable>
          </View>
          <ScrollView
            keyboardShouldPersistTaps="handled"
            contentContainerStyle={styles.sheetContent}
          >
            {children}
          </ScrollView>
        </SafeAreaView>
      </KeyboardAvoidingView>
    </Modal>
  );
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
  prominent = false,
  fill = false,
  testID,
  displayLabel,
  showSelectionMark = false,
  children,
}: {
  label: string;
  onPress: () => void;
  selected?: boolean;
  disabled?: boolean;
  loading?: boolean;
  loadingLabel?: string;
  variant?: 'primary' | 'secondary' | 'text' | 'link';
  expanded?: boolean;
  focusKey?: string;
  compact?: boolean;
  prominent?: boolean;
  fill?: boolean;
  testID?: string;
  displayLabel?: string;
  showSelectionMark?: boolean;
  children?: ReactNode;
}) {
  const segmented = useContext(SegmentedSelection);
  const [focused, setFocused] = useState(false);
  const ref = useRef<ComponentRef<typeof View>>(null);
  const navigation = usePageNavigation();
  const unavailable = disabled || loading;
  useEffect(() => {
    if (!focusKey || navigation.returnTarget !== focusKey) return;
    // A collapsed disclosure stays mounted, so it may not receive onLayout.
    // Wait for the updated layout before returning accessible focus to it.
    const frame = requestAnimationFrame(() => {
      if (ref.current) {
        AccessibilityInfo.sendAccessibilityEvent(ref.current, 'focus');
        navigation.finishReturn();
      }
    });
    return () => cancelAnimationFrame(frame);
  }, [focusKey, navigation]);
  return (
    <Pressable
      ref={ref}
      testID={testID}
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
        compact && styles.compactButton,
        prominent && styles.prominentButton,
        variant === 'secondary' && styles.secondaryButton,
        (variant === 'text' || variant === 'link') && styles.textButton,
        (focused || selected) && styles.selected,
        segmented && styles.segmentedButton,
        segmented && selected && styles.segmentedSelected,
        !!children && styles.recordButton,
        fill && styles.fillButton,
        pressed && styles.pressed,
        unavailable && styles.disabled,
      ]}
    >
      {children ?? (
        <Text
          style={[
            styles.buttonText,
            compact && styles.compactButtonText,
            variant !== 'primary' &&
              !(focused || selected) &&
              styles.secondaryButtonText,
            variant === 'link' && styles.linkButtonText,
            variant === 'link' &&
              !(focused || selected) &&
              styles.linkButtonColor,
            unavailable && styles.disabledButtonText,
            (focused || selected) && !unavailable && styles.selectedButtonText,
          ]}
        >
          {loading
            ? loadingLabel
            : selected && showSelectionMark
              ? `✓ ${displayLabel ?? label}`
              : (displayLabel ?? label)}
        </Text>
      )}
    </Pressable>
  );
}

const styles = StyleSheet.create({
  page: { flex: 1, backgroundColor: mobileTokens.color.background },
  scroll: { flexGrow: 1 },
  header: { gap: 8, alignItems: 'flex-start' },
  inlineHeader: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    alignItems: 'center',
    gap: 8,
  },
  backButton: {
    minHeight: 48,
    minWidth: 48,
    alignItems: 'center',
    justifyContent: 'center',
  },
  field: { gap: 8 },
  selection: { flexDirection: 'row', flexWrap: 'wrap', gap: 8 },
  segmented: {
    backgroundColor: mobileTokens.color.selectedBackground,
    borderRadius: 12,
    padding: 4,
    gap: 4,
  },
  segmentedButton: {
    flexGrow: 1,
    backgroundColor: 'transparent',
    borderColor: 'transparent',
    paddingHorizontal: 8,
  },
  segmentedSelected: {
    backgroundColor: mobileTokens.color.surface,
    borderColor: 'transparent',
  },
  input: {
    minHeight: 48,
    borderWidth: 1,
    borderColor: mobileTokens.color.border,
    borderRadius: 12,
    paddingHorizontal: 14,
    paddingVertical: 12,
    color: mobileTokens.color.foreground,
    backgroundColor: mobileTokens.color.surface,
    fontFamily: mobileTokens.type.bodyFont,
    fontSize: 16,
    lineHeight: 23,
  },
  disabledInput: { backgroundColor: mobileTokens.color.disabledBackground },
  invalidInput: { borderColor: mobileTokens.color.error },
  content: {
    alignSelf: 'center',
    width: '100%',
    maxWidth: 720,
    padding: 20,
    paddingTop: 12,
    gap: 16,
  },
  pinnedHeader: {
    paddingHorizontal: 20,
    paddingVertical: 12,
    gap: 16,
    backgroundColor: mobileTokens.color.background,
  },
  heading: {
    color: mobileTokens.color.foreground,
    fontSize: 24,
    lineHeight: 30,
    fontFamily: mobileTokens.type.displayFont,
    letterSpacing: -0.45,
    flexShrink: 1,
    flexWrap: 'wrap',
  },
  sectionHeading: {
    fontSize: 16,
    lineHeight: 23,
    letterSpacing: -0.15,
    fontFamily: mobileTokens.type.mediumFont,
  },
  text: {
    color: mobileTokens.color.foreground,
    fontSize: 16,
    lineHeight: 23,
    fontFamily: mobileTokens.type.bodyFont,
    flexShrink: 1,
    flexWrap: 'wrap',
  },
  feedback: { borderRadius: 12, padding: 14 },
  feedbackText: {
    fontSize: 14,
    lineHeight: 21,
    fontFamily: mobileTokens.type.bodyFont,
    flexShrink: 1,
    flexWrap: 'wrap',
  },
  button: {
    flexShrink: 1,
    minHeight: 48,
    minWidth: 48,
    paddingHorizontal: 16,
    paddingVertical: 10,
    borderRadius: 12,
    borderWidth: 1,
    borderColor: 'transparent',
    backgroundColor: mobileTokens.color.primary,
    justifyContent: 'center',
    alignItems: 'center',
  },
  secondaryButton: { backgroundColor: mobileTokens.color.selectedBackground },
  textButton: { backgroundColor: 'transparent', paddingHorizontal: 0 },
  selected: {
    borderColor: mobileTokens.color.focus,
    backgroundColor: mobileTokens.color.selectedBackground,
  },
  pressed: { opacity: 0.8 },
  disabled: {
    backgroundColor: mobileTokens.color.disabledBackground,
    borderColor: 'transparent',
  },
  buttonText: {
    color: mobileTokens.color.onPrimary,
    fontSize: 15,
    lineHeight: 22,
    fontFamily: mobileTokens.type.mediumFont,
    textAlign: 'center',
    flexShrink: 1,
    flexWrap: 'wrap',
  },
  secondaryButtonText: { color: mobileTokens.color.foreground },
  selectedButtonText: { color: mobileTokens.color.selectedForeground },
  linkButtonText: {},
  linkButtonColor: { color: mobileTokens.color.primary },
  compactButton: { paddingHorizontal: 10 },
  compactButtonText: { fontSize: 14 },
  prominentButton: { minHeight: 56 },
  disabledButtonText: { color: mobileTokens.color.disabledText },
  recordButton: {
    backgroundColor: mobileTokens.color.surface,
    alignItems: 'stretch',
    paddingHorizontal: 16,
    paddingVertical: 16,
    gap: 8,
  },
  fillButton: { flexGrow: 1 },
  badge: {
    alignSelf: 'flex-start',
    backgroundColor: mobileTokens.color.selectedBackground,
    color: mobileTokens.color.primary,
    fontFamily: mobileTokens.type.mediumFont,
    fontSize: 12,
    lineHeight: 18,
    paddingHorizontal: 10,
    paddingVertical: 4,
    borderRadius: 14,
    overflow: 'hidden',
  },
  infoBadge: {
    backgroundColor: mobileTokens.color.infoBackground,
    color: mobileTokens.color.info,
  },
  attentionBadge: {
    backgroundColor: mobileTokens.color.warningBackground,
    color: mobileTokens.color.warning,
  },
  sheetScreen: { flex: 1, justifyContent: 'flex-end' },
  sheetBackdrop: {
    position: 'absolute',
    top: 0,
    right: 0,
    bottom: 0,
    left: 0,
    minHeight: 48,
    minWidth: 48,
    backgroundColor: '#00000042',
  },
  sheet: {
    backgroundColor: mobileTokens.color.background,
    borderTopLeftRadius: 24,
    borderTopRightRadius: 24,
  },
  sheetHandle: {
    width: 48,
    height: 4,
    borderRadius: 2,
    backgroundColor: mobileTokens.color.border,
    alignSelf: 'center',
    marginTop: 12,
  },
  sheetHeader: {
    paddingHorizontal: 20,
    paddingTop: 12,
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    gap: 8,
  },
  sheetContent: { padding: 20, gap: 16 },
});
