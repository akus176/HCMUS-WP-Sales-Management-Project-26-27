import { router } from 'expo-router';
import { StyleSheet, Text, View } from 'react-native';
import type { StaffSession } from '../../session/session';
import { useConnectivity } from '../../connectivity/connectivity';
import {
  ShellButton,
  ShellFeedback,
  ShellHeaderAction,
  ShellHeading,
  ShellText,
} from '../../shell/controls';
import { useNativeNavigation } from '../../shell/NativeNavigation';
import { mobileTokens as t } from '../../shell/designTokens';
import { useDeliveryAssignments } from './useDeliveryAssignments';

export function DeliveryHome({ user }: { user: StaffSession }) {
  const navigation = useNativeNavigation();
  const { status } = useConnectivity();
  const allowed = user.permissions.includes('delivery.update');
  const assignments = useDeliveryAssignments('active', 1, allowed);
  const next =
    assignments.data?.items.find(
      (item) => item.deliveryStatus === 'out_for_delivery',
    ) ?? assignments.data?.items[0];
  if (!allowed)
    return (
      <ShellFeedback
        kind="warning"
        label="Delivery access is required. This app is for delivery staff. Contact your administrator or sign out from Account."
      />
    );
  return (
    <>
      <ShellHeaderAction
        kind="refresh"
        label="Refresh delivery overview"
        loading={assignments.isFetching}
        disabled={status !== 'online'}
        onPress={() => void assignments.refetch()}
      />
      <View style={styles.summary}>
        <Text style={styles.caption}>Active deliveries</Text>
        <Text style={styles.count}>
          {assignments.data
            ? Number(assignments.data.totalCount).toLocaleString()
            : '—'}
        </Text>
        <ShellText>Jobs assigned to you and still in progress.</ShellText>
        <ShellButton
          label="Open Deliveries"
          displayLabel="View deliveries"
          onPress={() => navigation?.open('delivery')}
        />
      </View>
      {assignments.isPending && (
        <ShellFeedback kind="loading" label="Loading your deliveries…" />
      )}
      {assignments.isError && (
        <ShellFeedback
          kind="error"
          label="Your deliveries could not be refreshed. Retry from the header before taking action."
        />
      )}
      {!assignments.isError && next && (
        <View style={styles.card}>
          <ShellHeading level="section">
            {next.deliveryStatus === 'out_for_delivery'
              ? 'Continue your delivery'
              : 'Next delivery'}
          </ShellHeading>
          <ShellText>{next.orderNumber}</ShellText>
          <ShellText>
            {next.deliveryStatus === 'out_for_delivery'
              ? 'Out for delivery'
              : 'Assigned · ready to start'}
          </ShellText>
          <ShellButton
            label={`Open delivery ${next.orderNumber}`}
            displayLabel="View delivery details"
            variant="secondary"
            onPress={() =>
              router.push({
                pathname: '/modules/delivery/[orderId]',
                params: { orderId: next.orderId },
              })
            }
          />
        </View>
      )}
      {!assignments.isError && assignments.data?.items.length === 0 && (
        <ShellFeedback
          kind="empty"
          label="No active deliveries. New assignments will appear here."
        />
      )}
      <ShellText>
        New assignments update automatically while connected. Refresh from the
        header whenever you need to check again.
      </ShellText>
    </>
  );
}
const styles = StyleSheet.create({
  summary: {
    backgroundColor: t.color.surface,
    padding: 20,
    borderRadius: t.shape.card,
    gap: 12,
  },
  card: {
    backgroundColor: t.color.surface,
    padding: 20,
    borderRadius: t.shape.card,
    gap: 12,
  },
  caption: {
    fontFamily: t.type.mediumFont,
    fontSize: 16,
    color: t.color.disabledText,
  },
  count: {
    fontFamily: t.type.displayFont,
    fontSize: 36,
    color: t.color.foreground,
  },
});
