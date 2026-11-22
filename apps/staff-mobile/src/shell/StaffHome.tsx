import { useState } from 'react';
import { Alert, Pressable, StyleSheet, Text, View } from 'react-native';
import { router } from 'expo-router';

import { useSession } from '../session/context';
import type { StaffSession } from '../session/session';
import {
  ShellButton,
  ShellHeading,
  ShellPage,
  ShellStatus,
  ShellText,
} from './controls';
import { permittedActions, permittedModules } from './permissions';
import { mobileTokens } from './designTokens';

const actionDescriptions: Record<string, string> = {
  'orders.fulfill': 'Tickets · Start preparation · Mark ready',
  'orders.create': 'Orders · Tables',
  'payments.record': 'Payments',
  'discounts.approve': 'Discount decisions',
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
}: { user?: StaffSession; embedded?: boolean } = {}) {
  const { client, state } = useSession();
  const [focusedModule, setFocusedModule] = useState<string | null>(null);
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
  const modules = permittedModules(user.permissions);
  const descriptions = Object.fromEntries(
    modules.map((module) => [
      module.id,
      permittedActions(module, user.permissions)
        .map((action) => actionDescriptions[action.permission] ?? action.label)
        .join(' · '),
    ]),
  );
  const content = (
    <>
      <ShellHeading>Staff home</ShellHeading>
      <ShellText>{user.fullName}</ShellText>
      <ShellText>Choose your workspace.</ShellText>
      {!modules.length && (
        <ShellText>
          No mobile modules are available for your permissions.
        </ShellText>
      )}
      <View style={styles.workspaces}>
        {modules.map((module) => (
          <Pressable
            key={module.id}
            accessibilityRole="button"
            accessibilityLabel={`Open ${module.title}`}
            accessibilityHint={descriptions[module.id]}
            onFocus={() => setFocusedModule(module.id)}
            onBlur={() => setFocusedModule(null)}
            style={({ pressed }) => [
              styles.workspace,
              (pressed || focusedModule === module.id) && styles.pressed,
            ]}
            onPress={() =>
              router.push({
                pathname: '/modules/[module]',
                params: { module: module.id },
              })
            }
          >
            <Text style={styles.title}>{module.title}</Text>
            <Text style={styles.description}>{descriptions[module.id]}</Text>
          </Pressable>
        ))}
      </View>
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
    </>
  );
  return embedded ? content : <ShellPage>{content}</ShellPage>;
}

const styles = StyleSheet.create({
  workspaces: { borderTopWidth: 1, borderColor: mobileTokens.color.primary },
  workspace: {
    minHeight: 48,
    minWidth: 48,
    justifyContent: 'center',
    paddingVertical: 16,
    gap: 4,
    borderBottomWidth: 1,
    borderColor: mobileTokens.color.disabledBackground,
  },
  title: {
    fontFamily: mobileTokens.type.displayFont,
    fontSize: 24,
    color: mobileTokens.color.foreground,
  },
  description: {
    fontFamily: mobileTokens.type.bodyFont,
    fontSize: 16,
    color: mobileTokens.color.disabledText,
  },
  pressed: { backgroundColor: mobileTokens.color.highlight },
});
