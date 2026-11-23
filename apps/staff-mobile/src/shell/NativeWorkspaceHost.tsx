import { Activity, useLayoutEffect, useState } from 'react';
import { Alert, BackHandler, StyleSheet, Text, View } from 'react-native';
import type { StaffSession } from '../session/session';
import { useSession } from '../session/context';
import { useConnectivity } from '../connectivity/connectivity';
import { DeliveryScreen } from '../features/delivery/DeliveryScreen';
import { useNativeNavigation } from './NativeNavigation';
import { isWorkspaceAllowed, type Workspace } from './navigationPolicy';
import { StaffHome } from './StaffHome';
import {
  ShellButton,
  ShellPage,
  ShellPageHeader,
  ShellSheet,
  ShellText,
} from './controls';
import { ShellIcon } from './ShellIcon';
import { mobileTokens as t } from './designTokens';

export function NativeWorkspaceHost({
  user: verifiedUser,
}: {
  user: StaffSession;
}) {
  const navigation = useNativeNavigation();
  const user = navigation?.user ?? verifiedUser;
  const workspace = navigation?.workspace ?? 'home';
  const active = isWorkspaceAllowed(workspace, user.permissions)
    ? workspace
    : 'home';
  useLayoutEffect(() => {
    const listener = BackHandler.addEventListener('hardwareBackPress', () => {
      if (active === 'home' || !navigation) return false;
      navigation.open('home');
      return true;
    });
    return () => listener.remove();
  }, [active, navigation]);
  if (!navigation) return <StaffHome user={user} />;
  return (
    <View style={styles.host}>
      {[...new Set<Workspace>(['home', ...navigation.visited])]
        .filter((id) => isWorkspaceAllowed(id, user.permissions))
        .map((id) => {
          const focused = active === id;
          const personal = id === 'home' || id === 'more' || id === 'account';
          return (
            <View
              key={id}
              style={focused ? styles.host : styles.hidden}
              accessibilityElementsHidden={!focused}
              importantForAccessibility={
                focused ? 'auto' : 'no-hide-descendants'
              }
            >
              <Activity mode={focused ? 'visible' : 'hidden'}>
                <ShellPage>
                  {personal ? (
                    id === 'home' ? (
                      <StaffHome user={user} embedded />
                    ) : id === 'more' ? (
                      <MoreWorkspaces />
                    ) : (
                      <Account user={user} />
                    )
                  ) : (
                    <>
                      <ShellPageHeader
                        title="Deliveries"
                        inline
                        hideDuringDetail
                      />
                      <DeliveryScreen showHeading={false} />
                    </>
                  )}
                </ShellPage>
              </Activity>
            </View>
          );
        })}
    </View>
  );
}

function MoreWorkspaces() {
  const navigation = useNativeNavigation();
  return (
    <>
      <ShellPageHeader title="More" />
      <ShellButton
        label="Open Account"
        variant="text"
        onPress={() => navigation?.open('account')}
      >
        <View style={styles.row}>
          <ShellIcon name="account" color={t.color.primary} />
          <View style={styles.rowBody}>
            <Text style={styles.rowTitle}>Account</Text>
            <Text style={styles.caption}>Session and connection</Text>
          </View>
          <ShellIcon name="chevron" size={18} color={t.color.disabledText} />
        </View>
      </ShellButton>
    </>
  );
}

function Account({ user }: { user: StaffSession }) {
  const navigation = useNativeNavigation();
  const { client } = useSession();
  const { status, refreshConnection } = useConnectivity();
  const [connection, setConnection] = useState(false);
  const [signout, setSignout] = useState(false);
  const [busy, setBusy] = useState(false);
  return (
    <>
      <ShellPageHeader
        title="Account"
        backLabel="Back to More"
        onBack={() => navigation?.open('more')}
      />
      <View style={styles.account}>
        <ShellIcon name="account" size={32} color={t.color.primary} />
        <View style={styles.rowBody}>
          <Text style={styles.rowTitle}>{user.fullName}</Text>
          <Text style={styles.caption}>{user.roles.join(' · ')}</Text>
        </View>
      </View>
      <ShellButton
        label="Connection details"
        variant="text"
        onPress={() => setConnection(true)}
      >
        <View style={styles.row}>
          <ShellIcon name="wifi" />
          <View style={styles.rowBody}>
            <Text style={styles.rowTitle}>Connection</Text>
            <Text style={styles.caption}>
              {status === 'online'
                ? 'Network connected'
                : status === 'offline'
                  ? 'Offline · read-only'
                  : 'Checking connection…'}
            </Text>
          </View>
          <ShellIcon name="chevron" size={18} />
        </View>
      </ShellButton>
      <ShellButton
        label="Sign out"
        variant="secondary"
        onPress={() => setSignout(true)}
      />
      <ShellSheet
        visible={connection}
        title="Connection"
        onClose={() => setConnection(false)}
      >
        <ShellText>
          {status === 'online'
            ? 'Network connected. Refresh a workspace to check current service data.'
            : 'Reconnect before changing operational records.'}
        </ShellText>
        <ShellButton
          label="Check connection"
          onPress={() => void refreshConnection()}
        />
      </ShellSheet>
      <ShellSheet
        visible={signout}
        title="Sign out of this device?"
        busy={busy}
        onClose={() => setSignout(false)}
      >
        <ShellText>You will need to sign in to continue working.</ShellText>
        <ShellButton
          label="Confirm sign out"
          loading={busy}
          onPress={() => {
            setBusy(true);
            void client
              ?.logout()
              .catch(() =>
                Alert.alert(
                  'Unable to remove the saved session. Please try again.',
                ),
              )
              .finally(() => setBusy(false));
          }}
        />
        <ShellButton
          label="Stay signed in"
          variant="secondary"
          disabled={busy}
          onPress={() => setSignout(false)}
        />
      </ShellSheet>
    </>
  );
}

const styles = StyleSheet.create({
  host: { flex: 1 },
  hidden: { display: 'none' },
  row: { flexDirection: 'row', alignItems: 'center', gap: 16 },
  rowBody: { flex: 1, gap: 6 },
  rowTitle: {
    fontFamily: t.type.mediumFont,
    fontSize: 16,
    lineHeight: 23,
    color: t.color.foreground,
  },
  caption: {
    fontFamily: t.type.bodyFont,
    fontSize: 13,
    lineHeight: 20,
    color: t.color.disabledText,
  },
  account: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 16,
    padding: 20,
    backgroundColor: t.color.surface,
    borderRadius: 12,
  },
});
