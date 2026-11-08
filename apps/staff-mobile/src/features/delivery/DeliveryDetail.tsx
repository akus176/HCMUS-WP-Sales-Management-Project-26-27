import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { router } from 'expo-router';
import { Linking } from 'react-native';

import { ApiError } from '../../api/errors';
import { useSession } from '../../session/context';
import {
  ShellButton,
  ShellFeedback,
  ShellHeading,
  ShellText,
} from '../../shell/controls';

export function DeliveryDetail({ orderId }: { orderId: string }) {
  const { client, state } = useSession();
  const [navigationError, setNavigationError] = useState(false);
  const detail = useQuery({
    queryKey: ['delivery', state.user!.id, 'detail', orderId],
    enabled: orderId.length > 0,
    gcTime: 0,
    staleTime: 0,
    queryFn: async ({ signal }) => {
      const { data } = await client!.api.GET(
        '/api/v1/courier/assignments/{id}',
        { signal, params: { path: { id: orderId } } },
      );
      return data!;
    },
  });
  const assignment = detail.isError ? undefined : detail.data;
  return (
    <>
      <ShellButton
        label="Back to deliveries"
        variant="secondary"
        onPress={() => router.replace('/modules/delivery')}
      />
      <ShellHeading>Delivery details</ShellHeading>
      {(!orderId || detail.isError) && (
        <ShellFeedback
          kind="error"
          label={
            detail.error instanceof ApiError && detail.error.status === 404
              ? 'Delivery is unavailable or no longer assigned to you.'
              : 'Unable to load this delivery. Refresh or return to your list.'
          }
        />
      )}
      {detail.isPending && orderId && (
        <ShellFeedback kind="loading" label="Loading delivery…" />
      )}
      <ShellButton
        label="Refresh delivery"
        variant="secondary"
        loading={detail.isFetching}
        onPress={() => void detail.refetch()}
      />
      {assignment && (
        <>
          <ShellHeading>Delivery {assignment.orderNumber}</ShellHeading>
          <ShellText>
            Status: {assignment.deliveryStatus.replaceAll('_', ' ')}
          </ShellText>
          <ShellText>Recipient: {assignment.recipientName}</ShellText>
          <ShellText>Contact: {assignment.contactNumber}</ShellText>
          <ShellText>Address: {assignment.addressLine}</ShellText>
          {navigationError && (
            <ShellFeedback
              kind="error"
              label="Unable to open navigation on this device."
            />
          )}
          <ShellButton
            label="Open navigation"
            onPress={() => {
              setNavigationError(false);
              void Linking.openURL(
                `https://www.google.com/maps/search/?api=1&query=${encodeURIComponent(assignment.addressLine)}`,
              ).catch(() => setNavigationError(true));
            }}
          />
        </>
      )}
    </>
  );
}
