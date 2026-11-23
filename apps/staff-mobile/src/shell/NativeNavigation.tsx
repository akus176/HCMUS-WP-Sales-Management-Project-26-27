import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useState,
  type ReactNode,
} from 'react';
import { Keyboard, Pressable, StyleSheet, Text, View } from 'react-native';
import { router, usePathname } from 'expo-router';
import { SafeAreaView } from 'react-native-safe-area-context';
import type { StaffSession } from '../session/session';
import { mobileTokens as t } from './designTokens';
import { ShellIcon } from './ShellIcon';
import {
  isWorkspaceAllowed,
  navigationFor,
  type Workspace,
} from './navigationPolicy';

type Navigation = {
  user: StaffSession | null;
  workspace: Workspace;
  visited: Workspace[];
  revision: number;
  open: (workspace: Workspace) => void;
  verify: (user: StaffSession) => void;
};
const Context = createContext<Navigation | null>(null);
export const useNativeNavigation = () => useContext(Context);

export function NativeNavigationProvider({
  children,
}: {
  children: ReactNode;
}) {
  const pathname = usePathname();
  const [user, setUser] = useState<StaffSession | null>(null);
  const [workspace, setWorkspace] = useState<Workspace>('home');
  const [visited, setVisited] = useState<Workspace[]>(['home']);
  const [revision, setRevision] = useState(0);
  const verify = useCallback((fresh: StaffSession) => {
    setUser((previous) =>
      previous && JSON.stringify(previous) === JSON.stringify(fresh)
        ? previous
        : fresh,
    );
  }, []);
  const open = useCallback(
    (next: Workspace) => {
      if (!user || !isWorkspaceAllowed(next, user.permissions)) return;
      setVisited((previous) =>
        previous.includes(next) ? previous : [...previous, next],
      );
      setWorkspace(next);
      setRevision((previous) => previous + 1);
      if (pathname !== '/session') router.dismissTo('/session');
    },
    [pathname, user],
  );
  const value = useMemo(
    () => ({ user, workspace, visited, revision, open, verify }),
    [user, workspace, visited, revision, open, verify],
  );
  return <Context.Provider value={value}>{children}</Context.Provider>;
}

export function NativeTabBar() {
  const navigation = useNativeNavigation();
  const pathname = usePathname();
  const [keyboard, setKeyboard] = useState(false);
  useEffect(() => {
    const show = Keyboard.addListener('keyboardDidShow', () =>
      setKeyboard(true),
    );
    const hide = Keyboard.addListener('keyboardDidHide', () =>
      setKeyboard(false),
    );
    return () => {
      show.remove();
      hide.remove();
    };
  }, []);
  if (!navigation?.user || keyboard) return null;
  const tabs = navigationFor(navigation.user.permissions);
  const routeWorkspace = pathname.startsWith('/modules/')
    ? pathname.split('/')[2]
    : null;
  const current = isWorkspaceAllowed(
    (routeWorkspace ?? navigation.workspace) as Workspace,
    navigation.user.permissions,
  )
    ? (routeWorkspace ?? navigation.workspace)
    : 'home';
  const active = tabs.some((tab) => tab.id === current) ? current : 'more';
  return (
    <SafeAreaView edges={['bottom']} style={styles.safe}>
      <View accessibilityRole="tablist" style={styles.tabs}>
        {tabs.map((tab) => (
          <Pressable
            key={tab.id}
            accessibilityRole="tab"
            accessibilityLabel={tab.label}
            accessibilityState={{ selected: active === tab.id }}
            onPress={() => navigation.open(tab.id)}
            style={({ pressed }) => [styles.tab, pressed && styles.pressed]}
          >
            <View
              collapsable={false}
              style={[styles.icon, active === tab.id && styles.activeIcon]}
            >
              <ShellIcon
                name={tab.icon}
                color={
                  active === tab.id ? t.color.primary : t.color.disabledText
                }
                size={22}
              />
            </View>
            <Text
              style={[styles.label, active === tab.id && styles.activeLabel]}
            >
              {tab.label}
            </Text>
          </Pressable>
        ))}
      </View>
    </SafeAreaView>
  );
}
const styles = StyleSheet.create({
  safe: {
    backgroundColor: t.color.surface,
    borderTopWidth: StyleSheet.hairlineWidth,
    borderColor: t.color.border,
  },
  tabs: { flexDirection: 'row', alignItems: 'stretch' },
  tab: {
    flex: 1,
    minWidth: 48,
    minHeight: 72,
    paddingVertical: 6,
    alignItems: 'center',
    justifyContent: 'center',
    gap: 3,
  },
  icon: {
    width: 64,
    height: 32,
    borderRadius: t.shape.pill,
    overflow: 'hidden',
    alignItems: 'center',
    justifyContent: 'center',
  },
  activeIcon: { backgroundColor: t.color.selectedBackground },
  label: {
    fontFamily: t.type.bodyFont,
    fontSize: 12,
    lineHeight: 18,
    color: t.color.disabledText,
    textAlign: 'center',
    flexShrink: 1,
  },
  activeLabel: { fontFamily: t.type.mediumFont, color: t.color.primary },
  pressed: { backgroundColor: t.color.background },
});
