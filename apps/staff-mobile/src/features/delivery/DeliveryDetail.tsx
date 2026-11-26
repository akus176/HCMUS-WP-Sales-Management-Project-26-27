import { useRef, useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { router } from 'expo-router';
import { Linking } from 'react-native';

import { ApiError } from '../../api/errors';
import { useSafeMutation } from '../../connectivity/connectivity';
import { useSession } from '../../session/context';
import {
  ShellButton,
  ShellHeaderAction,
  ShellPageHeader,
  ShellFeedback,
  ShellHeading,
  ShellText,
  ShellInput,
  ShellMutationFeedback,
  ShellSheet,
} from '../../shell/controls';

export function DeliveryDetail({ orderId }: { orderId: string }) {
  const { client, state } = useSession();
  const queries = useQueryClient();
  const [navigationError, setNavigationError] = useState(false);
  const [accessRevoked, setAccessRevoked] = useState(false);
  const [finished, setFinished] = useState<'delivered' | 'failed' | null>(null);
  const [reason, setReason] = useState('');
  const [intent, setIntent] = useState<'start' | 'complete' | 'fail' | null>(
    null,
  );
  const [reasonError, setReasonError] = useState(false);
  const [notice, setNotice] = useState<string | null>(null);
  const submitting = useRef(false);
  const detailKey = ['delivery', state.user!.id, 'detail', orderId];
  const detail = useQuery({
    queryKey: detailKey,
    enabled: orderId.length > 0 && !finished && !accessRevoked,
    gcTime: 0,
    staleTime: 0,
    queryFn: async ({ signal }) => {
      try {
        const { data } = await client!.api.GET(
          '/api/v1/courier/assignments/{id}',
          { signal, params: { path: { id: orderId } } },
        );
        return data!;
      } catch (error) {
        if (error instanceof ApiError && [403, 404].includes(error.status)) {
          setAccessRevoked(true);
          queries.removeQueries({ queryKey: detailKey });
          queries.removeQueries({
            queryKey: ['delivery', state.user!.id, 'list'],
          });
        }
        throw error;
      }
    },
  });
  const mutation = useSafeMutation({
    mutationFn: async (action: 'start' | 'complete' | 'fail') => {
      if (action === 'fail') {
        await client!.api.POST('/api/v1/courier/assignments/{id}/fail', {
          params: { path: { id: orderId } },
          body: { reason: reason.trim() },
        });
      } else {
        const path =
          action === 'start'
            ? '/api/v1/courier/assignments/{id}/start'
            : '/api/v1/courier/assignments/{id}/complete';
        await client!.api.POST(path, { params: { path: { id: orderId } } });
      }
      if (action !== 'start') {
        setFinished(action === 'complete' ? 'delivered' : 'failed');
        setReason('');
        await queries.cancelQueries({ queryKey: detailKey });
        queries.removeQueries({ queryKey: detailKey });
        queries.removeQueries({
          queryKey: ['delivery', state.user!.id, 'list', 'active'],
        });
        await queries.invalidateQueries({
          queryKey: ['delivery', state.user!.id, 'list', 'completed'],
          refetchType: 'none',
        });
      }
    },
    onSuccess: (_, action) =>
      setNotice(
        action === 'start'
          ? 'Delivery is out for delivery. Confirmed by the service.'
          : action === 'complete'
            ? 'Delivery delivered. Confirmed by the service.'
            : 'Delivery failed. Confirmed by the service.',
      ),
    onError: async (error) => {
      if (error instanceof ApiError && [403, 404].includes(error.status)) {
        setAccessRevoked(true);
        await queries.cancelQueries({ queryKey: detailKey });
        queries.removeQueries({ queryKey: detailKey });
        queries.removeQueries({
          queryKey: ['delivery', state.user!.id, 'list'],
        });
        return;
      }
      await queries.invalidateQueries({
        queryKey: ['delivery', state.user!.id],
      });
    },
  });
  const accessLost =
    accessRevoked ||
    (mutation.error instanceof ApiError &&
      [403, 404].includes(mutation.error.status));
  const assignment =
    detail.isError || finished || accessLost ? undefined : detail.data;
  const submit = async (action: 'start' | 'complete' | 'fail') => {
    if (
      submitting.current ||
      mutation.isWriteDisabled ||
      detail.isFetching ||
      !assignment
    )
      return;
    if (action === 'fail' && !reason.trim()) {
      setReasonError(true);
      return;
    }
    submitting.current = true;
    setReasonError(false);
    setNotice(null);
    try {
      await mutation.mutateAsync(action);
    } catch {
      /* Server state is refreshed by the mutation boundary. */
    } finally {
      submitting.current = false;
    }
  };
  return (
    <>
      <ShellPageHeader
        title="Delivery details"
        backLabel="Back to deliveries"
        onBack={() => router.replace('/modules/delivery')}
        priority={1}
      />
      <ShellMutationFeedback mutation={mutation} />
      {notice && <ShellFeedback kind="info" label={notice} />}
      {mutation.error && (
        <ShellFeedback
          kind="error"
          label={
            mutation.error instanceof ApiError && mutation.error.status === 409
              ? 'Delivery changed elsewhere. Review the refreshed state before trying again.'
              : 'No change was confirmed. Refresh the delivery and try again.'
          }
        />
      )}
      {!finished && (!orderId || detail.isError || accessLost) && (
        <ShellFeedback
          kind="error"
          label={
            accessLost ||
            (detail.error instanceof ApiError && detail.error.status === 404)
              ? 'Delivery is unavailable or no longer assigned to you.'
              : 'Unable to load this delivery. Refresh or return to your list.'
          }
        />
      )}
      {!finished && !accessLost && detail.isPending && orderId && (
        <ShellFeedback kind="loading" label="Loading delivery…" />
      )}
      {!finished && !accessLost && (
        <ShellHeaderAction
          kind="refresh"
          label="Refresh delivery"
          loading={detail.isFetching}
          onPress={() => void detail.refetch()}
        />
      )}
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
          {assignment.deliveryStatus === 'assigned' && (
            <ShellButton
              label="Start delivery"
              disabled={
                mutation.isWriteDisabled ||
                mutation.isPending ||
                detail.isFetching
              }
              onPress={() => setIntent('start')}
            />
          )}
          {assignment.deliveryStatus === 'out_for_delivery' && (
            <>
              <ShellButton
                label="Mark delivered"
                disabled={
                  mutation.isWriteDisabled ||
                  mutation.isPending ||
                  detail.isFetching
                }
                onPress={() => setIntent('complete')}
              />
              <ShellInput
                label="Reason for failed delivery"
                accessibilityLabel="Failure reason"
                value={reason}

                editable={!mutation.isPending && !mutation.isWriteDisabled}
                onChangeText={(value) => {
                  setReason(value);
                  setReasonError(false);
                }}
                multiline
              />
              {reasonError && (
                <ShellFeedback
                  kind="error"
                  label="A failure reason is required."
                />
              )}
              <ShellButton
                label="Mark failed"
                disabled={
                  mutation.isWriteDisabled ||
                  mutation.isPending ||
                  detail.isFetching
                }
                onPress={() => {
                  if (!reason.trim()) {
                    setReasonError(true);
                    return;
                  }
                  setIntent('fail');
                }}
              />
            </>
          )}
          <ShellSheet
            visible={!!intent}
            title={
              intent === 'start'
                ? 'Start this delivery?'
                : intent === 'complete'
                  ? 'Confirm delivery?'
                  : 'Report failed delivery?'
            }
            busy={mutation.isPending}
            onClose={() => setIntent(null)}
          >
            <ShellText>Order {assignment.orderNumber}</ShellText>
            {intent === 'fail' && <ShellText>{reason}</ShellText>}
            <ShellButton
              label={
                intent === 'start'
                  ? 'Confirm start delivery'
                  : intent === 'complete'
                    ? 'Confirm delivered'
                    : 'Confirm failed delivery'
              }
              disabled={
                mutation.isWriteDisabled ||
                mutation.isPending ||
                detail.isFetching
              }
              loading={mutation.isPending}
              onPress={() => {
                if (intent) {
                  void submit(intent);
                  setIntent(null);
                }
              }}
            />
            <ShellButton
              label="Keep reviewing delivery"
              variant="secondary"
              onPress={() => setIntent(null)}
            />
          </ShellSheet>
        </>
      )}
    </>
  );
}
