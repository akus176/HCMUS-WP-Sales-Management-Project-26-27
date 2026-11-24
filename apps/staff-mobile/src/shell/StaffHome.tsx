import type { ReactNode } from 'react';
import {
  Alert,
  StyleSheet,
  Text,
  View,
  useWindowDimensions,
} from 'react-native';
import { router } from 'expo-router';
import { useSession } from '../session/context';
import type { StaffSession } from '../session/session';
import { ShellButton, ShellHeading, ShellPage, ShellStatus } from './controls';
import { permittedActions } from './permissions';
import { mobileTokens as t } from './designTokens';
import { useNativeNavigation } from './NativeNavigation';
import {
  navigationFor,
  availableWorkspaces,
  workspaceNavigation,
  type Workspace,
} from './navigationPolicy';
import { ShellIcon } from './ShellIcon';

const actionDescriptions: Record<string, string> = {
  'orders.fulfill': 'Tickets · Start preparation · Mark ready',
  'orders.create': 'New sales · Orders',
  'payments.record': 'Payments',
  'orders.cancel': 'Cancellations',
  'delivery.assign': 'Delivery assignment',
  'inventory.manage': 'Balances · Movement history',
  'reports.lowstock': 'Low stock',
  'inventory.adjust': 'Stock adjustments',
  'inventory.receive': 'Purchase receipts',
  'delivery.update': 'Assigned deliveries · Navigation · Delivery outcomes',
  'users.manage': 'Staff accounts · Deactivation · Security events',
};

export function StaffHome({
  user: verifiedUser,
  embedded = false,
  summary,
}: { user?: StaffSession; embedded?: boolean; summary?: ReactNode } = {}) {
  const { client, state } = useSession();
  const navigation = useNativeNavigation();
  const { fontScale } = useWindowDimensions();
  if (state.status === 'restoring')
    return (
      <ShellPage>
        <ShellStatus>Restoring session…</ShellStatus>
      </ShellPage>
    );
  const user = verifiedUser ?? state.user;
  if (!user)
    return (
      <ShellPage>
        <ShellStatus>Session ended. Sign in again.</ShellStatus>
      </ShellPage>
    );
  const modules = availableWorkspaces(user.permissions);
  const quick = !!navigation && modules.length > 1;
  const primary = navigationFor(user.permissions).map((tab) => tab.id);
  const visibleModules = quick
    ? modules.filter((module) => primary.includes(module.id as Workspace))
    : modules;
  const content = (
    <>
      <View style={styles.header}>
        <ShellHeading accessibilityLabel="Staff home">
          {navigation
            ? `Good ${new Date().getHours() < 12 ? 'morning' : new Date().getHours() < 18 ? 'afternoon' : 'evening'}, ${user.fullName.split(' ')[0]}`
            : 'Staff home'}
        </ShellHeading>
        <Text style={styles.caption}>{user.fullName}</Text>
      </View>
      {summary}
      {!modules.length && (
        <ShellStatus>
          No mobile modules are available for your permissions.
        </ShellStatus>
      )}
      {!!modules.length && (
        <View style={styles.sectionHeader}>
          <ShellHeading level="section">Your workspaces</ShellHeading>
        </View>
      )}
      <View style={quick && fontScale <= 1.5 ? styles.quickGrid : styles.list}>
        {visibleModules.map((module) => {
          const description = permittedActions(module, user.permissions)
            .map(
              (action) => actionDescriptions[action.permission] ?? action.label,
            )
            .join(' · ');
          return (
            <View
              key={module.id}
              style={quick && fontScale <= 1.5 ? styles.quickCell : undefined}
            >
              <ShellButton
                label={`Open ${module.title}`}
                variant="text"
                fill={quick && fontScale <= 1.5}
                onPress={() =>
                  navigation
                    ? navigation.open(module.id as Workspace)
                    : router.push({
                        pathname: '/modules/[module]',
                        params: { module: module.id },
                      })
                }
              >
                <View style={quick ? styles.quickWorkspace : styles.workspace}>
                  <View style={styles.icon}>
                    <ShellIcon
                      name={workspaceNavigation[module.id]!.icon}
                      color={t.color.primary}
                    />
                  </View>
                  <View style={styles.copy}>
                    <Text style={styles.title}>
                      {module.id === 'cashier' && quick
                        ? 'Counter'
                        : module.title}
                    </Text>
                    <Text style={styles.caption}>
                      {quick
                        ? module.id === 'cashier'
                          ? 'New sales and tickets'
                          : module.id === 'inventory'
                            ? 'Balances and receipts'
                            : description
                        : description}
                    </Text>
                  </View>
                  {!quick && (
                    <ShellIcon
                      name="chevron"
                      size={16}
                      color={t.color.disabledText}
                    />
                  )}
                </View>
              </ShellButton>
            </View>
          );
        })}
      </View>
      {!navigation && (
        <ShellButton
          label="Sign out"
          variant="text"
          onPress={() => {
            void client
              ?.logout()
              .catch(() =>
                Alert.alert(
                  'Unable to remove the saved session. Please try again.',
                ),
              );
          }}
        />
      )}
    </>
  );
  return embedded ? content : <ShellPage>{content}</ShellPage>;
}
const styles = StyleSheet.create({
  header: { gap: 6, paddingBottom: 8 },
  workspace: { flexDirection: 'row', alignItems: 'center', gap: 14 },
  sectionHeader: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    gap: 8,
  },
  list: { gap: 12 },
  quickGrid: { flexDirection: 'row', flexWrap: 'wrap', gap: 12 },
  quickCell: { flexBasis: '47%', flexGrow: 1 },
  quickWorkspace: { alignItems: 'flex-start', gap: 12 },
  icon: {
    width: 48,
    height: 48,
    borderRadius: 12,
    alignItems: 'center',
    justifyContent: 'center',
    backgroundColor: t.color.selectedBackground,
  },
  copy: { flex: 1, gap: 6 },
  title: {
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
});
