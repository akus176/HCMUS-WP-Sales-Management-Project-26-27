import { Activity, useLayoutEffect, useState } from 'react';
import { Alert, BackHandler, StyleSheet, Text, View } from 'react-native';
import { useQuery } from '@tanstack/react-query';
import type { StaffSession } from '../session/session';
import { useSession } from '../session/context';
import { useConnectivity } from '../connectivity/connectivity';
import { PreparationScreen } from '../barista/PreparationScreen';
import { OrdersScreen } from '../features/orders/OrdersScreen';
import { InventoryWorkspace } from '../features/inventory/InventoryWorkspace';
import { DiscountApprovals } from '../features/approvals/DiscountApprovals';
import { DeliveryScreen } from '../features/delivery/DeliveryScreen';
import { StaffAdministration } from '../features/administration/StaffAdministration';
import { useNativeNavigation } from './NativeNavigation';
import {
  isWorkspaceAllowed,
  navigationFor,
  workspaceNavigation,
  type Workspace,
} from './navigationPolicy';
import { permittedModules } from './permissions';
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
                      <StaffHome
                        user={user}
                        embedded
                        summary={<HomeAttention user={user} />}
                      />
                    ) : id === 'more' ? (
                      <MoreWorkspaces user={user} />
                    ) : (
                      <Account user={user} />
                    )
                  ) : (
                    <>
                      <ShellPageHeader
                        title={
                          id === 'cashier'
                            ? 'Orders'
                            : id === 'administration'
                              ? 'Staff'
                              : id === 'approvals'
                                ? 'Approvals'
                                : permittedModules(user.permissions).find(
                                    (module) => module.id === id,
                                  )?.title
                        }
                        inline
                        hideDuringDetail
                      />
                      {id === 'barista' ? (
                        <PreparationScreen showHeading={false} directOpen />
                      ) : id === 'cashier' ? (
                        <OrdersScreen user={user} showHeading={false} />
                      ) : id === 'inventory' ? (
                        <InventoryWorkspace user={user} showHeading={false} />
                      ) : id === 'approvals' ? (
                        <>
                          <ManagerOperationsLink user={user} />
                          <DiscountApprovals user={user} showHeading={false} />
                        </>
                      ) : id === 'delivery' ? (
                        <DeliveryScreen showHeading={false} />
                      ) : (
                        <StaffAdministration user={user} showHeading={false} />
                      )}
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

function ManagerOperationsLink({ user }: { user: StaffSession }) {
  const [operations, setOperations] = useState(false);
  if (
    !user.permissions.some((p) =>
      ['orders.cancel', 'delivery.assign'].includes(p),
    )
  )
    return null;
  return (
    <>
      <ShellButton
        label="Manage cancellations and deliveries"
        variant="secondary"
        onPress={() => setOperations(true)}
      />
      <ShellSheet
        title="Order operations"
        visible={operations}
        onClose={() => setOperations(false)}
      >
        <OrdersScreen user={user} manager showHeading={false} />
      </ShellSheet>
    </>
  );
}

function HomeAttention({ user }: { user: StaffSession }) {
  const { client } = useSession();
  const navigation = useNativeNavigation();
  const allowed = user.permissions.includes('discounts.approve');
  const canSeeLowStock = user.permissions.includes('reports.lowstock');
  const lowStock = useQuery({
    queryKey: ['inventory', user.id, 'overview'],
    enabled: canSeeLowStock,
    queryFn: async ({ signal }) => {
      const { data } = await client!.api.GET('/api/v1/ingredients/low-stock', {
        signal,
      });
      return data!;
    },
  });
  const pending = useQuery({
    queryKey: ['discounts', user.id, 'overview'],
    enabled: allowed,
    queryFn: async ({ signal }) => {
      const { data } = await client!.api.GET('/api/v1/discounts/pending', {
        signal,
      });
      return data!;
    },
  });
  const count = pending.data?.totalCount;
  return (
    <>
      {allowed && (
        <View style={styles.summary}>
          <Text style={styles.summaryTitle}>
            {typeof count === 'number'
              ? `${count} ${count === 1 ? 'discount' : 'discounts'} to review`
              : 'Discount approvals'}
          </Text>
          <Text style={styles.summaryText}>
            {pending.isError
              ? 'The overview could not be refreshed.'
              : pending.isPending
                ? 'Checking pending requests…'
                : 'Review the request, reason and order total.'}
          </Text>
          <ShellButton
            label="Review approvals"
            displayLabel="Open approvals"
            variant="secondary"
            onPress={() => navigation?.open('approvals')}
          />
        </View>
      )}
      {canSeeLowStock && (
        <ShellButton
          label="Review low stock"
          variant="text"
          onPress={() => navigation?.open('inventory')}
        >
          <View style={styles.row}>
            <ShellIcon name="stock" color={t.color.primary} />
            <View style={styles.rowBody}>
              <Text style={styles.rowTitle}>
                {typeof lowStock.data?.totalCount === 'number'
                  ? `${lowStock.data.totalCount} ingredients low`
                  : 'Low stock'}
              </Text>
              <Text style={styles.caption}>
                {lowStock.isError
                  ? 'Refresh inventory to check current levels.'
                  : 'Check balances and replenishment.'}
              </Text>
            </View>
            <ShellIcon name="chevron" size={16} />
          </View>
        </ShellButton>
      )}
    </>
  );
}

function MoreWorkspaces({ user }: { user: StaffSession }) {
  const navigation = useNativeNavigation();
  const primary = navigationFor(user.permissions).map((tab) => tab.id);
  return (
    <>
      <ShellPageHeader title="More" />
      {permittedModules(user.permissions)
        .filter((module) => !primary.includes(module.id as Workspace))
        .map((module) => (
          <ShellButton
            key={module.id}
            label={`Open ${module.title}`}
            variant="text"
            onPress={() => navigation?.open(module.id as Workspace)}
          >
            <View style={styles.row}>
              <ShellIcon
                name={workspaceNavigation[module.id]!.icon}
                color={t.color.primary}
              />
              <View style={styles.rowBody}>
                <Text style={styles.rowTitle}>{module.title}</Text>
              </View>
              <ShellIcon
                name="chevron"
                size={18}
                color={t.color.disabledText}
              />
            </View>
          </ShellButton>
        ))}
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
  const { client } = useSession();
  const { status, refreshConnection } = useConnectivity();
  const [connection, setConnection] = useState(false);
  const [signout, setSignout] = useState(false);
  const [busy, setBusy] = useState(false);
  return (
    <>
      <ShellPageHeader title="Account" />
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
  summary: {
    backgroundColor: t.color.primary,
    borderRadius: 12,
    padding: 20,
    gap: 12,
  },
  summaryTitle: {
    fontFamily: t.type.displayFont,
    fontSize: 20,
    lineHeight: 27,
    color: t.color.onPrimary,
  },
  summaryText: {
    fontFamily: t.type.bodyFont,
    fontSize: 14,
    lineHeight: 21,
    color: t.color.onPrimary,
  },
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
