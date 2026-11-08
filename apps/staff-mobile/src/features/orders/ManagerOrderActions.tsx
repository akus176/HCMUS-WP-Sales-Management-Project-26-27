import { useRef, useState } from 'react';
import { TextInput } from 'react-native';
import { useQuery, useQueryClient } from '@tanstack/react-query';

import { ApiError } from '../../api/errors';
import type { components } from '../../api/schema';
import { useSafeMutation } from '../../connectivity/connectivity';
import { useSession } from '../../session/context';
import {
  ShellButton,
  ShellFeedback,
  ShellHeading,
  ShellText,
} from '../../shell/controls';
import { orderStyles } from './presentation';

type Command =
  { action: 'cancel'; reason: string } | { action: 'assign'; staffId: string };

export function ManagerOrderActions({
  order,
  permissions,
  disabled,
  onAccessLost,
}: {
  order: components['schemas']['OperationalOrderResponse'];
  permissions: readonly string[];
  disabled: boolean;
  onAccessLost: () => void;
}) {
  const { client, state } = useSession();
  const queries = useQueryClient();
  const [reason, setReason] = useState('');
  const [staffId, setStaffId] = useState<string | null>(null);
  const [intent, setIntent] = useState<Command | null>(null);
  const [validation, setValidation] = useState<string | null>(null);
  const submitting = useRef(false);
  const canCancel =
    permissions.includes('orders.cancel') &&
    ['draft', 'confirmed', 'preparing', 'ready'].includes(order.status);
  const canAssign =
    permissions.includes('delivery.assign') &&
    order.channel === 'delivery' &&
    order.status === 'ready' &&
    order.deliveryStatus === 'unassigned';
  const staff = useQuery({
    queryKey: ['sales', state.user!.id, 'delivery-staff'],
    enabled: canAssign,
    queryFn: async ({ signal }) => {
      try {
        const { data } = await client!.api.GET(
          '/api/v1/delivery-staff/options',
          { signal },
        );
        return data!;
      } catch (error) {
        if (error instanceof ApiError && error.status === 403) onAccessLost();
        throw error;
      }
    },
  });
  const mutation = useSafeMutation({
    mutationFn: async (command: Command) => {
      const params = { path: { id: order.id } };
      if (command.action === 'cancel')
        await client!.api.POST('/api/v1/orders/{id}/cancel', {
          params,
          body: { reason: command.reason },
        });
      else
        await client!.api.POST('/api/v1/orders/{id}/assign-delivery', {
          params,
          body: { staffId: command.staffId },
        });
      return command.action;
    },
    onSuccess: () => {
      setReason('');
      setStaffId(null);
    },
    onError: async (error) => {
      if (error instanceof ApiError && error.status === 403) onAccessLost();
      if (!(error instanceof ApiError && [403, 409].includes(error.status)))
        await queries.invalidateQueries({
          queryKey: ['sales', state.user!.id],
        });
    },
  });
  const unavailable =
    disabled || mutation.isWriteDisabled || mutation.isPending;
  async function confirm() {
    if (!intent || unavailable || submitting.current) return;
    if (
      (intent.action === 'cancel' && !canCancel) ||
      (intent.action === 'assign' &&
        (!canAssign ||
          staff.isFetching ||
          staff.isError ||
          !staff.data?.some((person) => person.id === intent.staffId)))
    ) {
      setIntent(null);
      return;
    }
    submitting.current = true;
    setIntent(null);
    try {
      await mutation.mutateAsync(intent);
    } catch {
      /* Server rejection is rendered below. */
    } finally {
      submitting.current = false;
    }
  }

  return (
    <>
      {mutation.data && (
        <ShellFeedback
          kind="info"
          label={
            mutation.data === 'cancel'
              ? 'Cancellation confirmed by the service.'
              : 'Delivery assignment confirmed by the service.'
          }
        />
      )}
      {mutation.error && (
        <ShellFeedback
          kind="error"
          label={
            mutation.error instanceof ApiError && mutation.error.status === 409
              ? 'Order changed elsewhere. Review the refreshed order before trying again.'
              : mutation.error instanceof ApiError
                ? mutation.error.message
                : 'No success was confirmed. Refresh the order before trying again.'
          }
        />
      )}
      {validation && <ShellFeedback kind="error" label={validation} />}
      {intent ? (
        <>
          <ShellFeedback
            kind="warning"
            label={
              intent.action === 'cancel'
                ? `Cancel order ${order.orderNumber}? Reason: ${intent.reason}`
                : `Assign order ${order.orderNumber} to ${staff.data?.find((person) => person.id === intent.staffId)?.fullName ?? 'selected staff'}?`
            }
          />
          <ShellButton
            label={
              intent.action === 'cancel'
                ? 'Confirm cancellation'
                : 'Confirm assignment'
            }
            disabled={unavailable}
            loading={mutation.isPending}
            onPress={() => void confirm()}
          />
          <ShellButton
            label="Keep current order"
            variant="secondary"
            disabled={unavailable}
            onPress={() => setIntent(null)}
          />
        </>
      ) : (
        <>
          {canCancel && (
            <>
              <ShellHeading>Authorize cancellation</ShellHeading>
              <ShellText>
                Cancellation reason (required, at most 255 characters)
              </ShellText>
              <TextInput
                accessibilityLabel="Cancellation reason"
                value={reason}
                onChangeText={setReason}
                maxLength={255}
                multiline
                editable={!unavailable}
                style={orderStyles.input}
              />
              <ShellButton
                label="Review cancellation"
                disabled={unavailable}
                onPress={() => {
                  mutation.reset();
                  if (!reason.trim()) {
                    setValidation('Enter a cancellation reason.');
                    return;
                  }
                  setValidation(null);
                  setIntent({ action: 'cancel', reason: reason.trim() });
                }}
              />
            </>
          )}
          {canAssign && (
            <>
              <ShellHeading>Assign ready delivery</ShellHeading>
              {staff.isPending && (
                <ShellFeedback
                  kind="loading"
                  label="Loading eligible delivery staff…"
                />
              )}
              {staff.error && (
                <ShellFeedback
                  kind="error"
                  label="Eligible staff could not be loaded. Refresh before assigning."
                />
              )}
              {!staff.isError && staff.data?.length === 0 && (
                <ShellFeedback
                  kind="empty"
                  label="No active delivery staff are available."
                />
              )}
              {!staff.isError &&
                staff.data?.map((person) => (
                  <ShellButton
                    key={person.id}
                    label={`Select ${person.fullName}`}
                    selected={staffId === person.id}
                    disabled={unavailable || staff.isFetching}
                    variant="secondary"
                    onPress={() => {
                      setStaffId(person.id);
                      setIntent(null);
                    }}
                  />
                ))}
              <ShellButton
                label="Review assignment"
                disabled={
                  unavailable ||
                  staff.isFetching ||
                  staff.isError ||
                  !staff.data?.some((person) => person.id === staffId)
                }
                onPress={() => {
                  mutation.reset();
                  setIntent({ action: 'assign', staffId: staffId! });
                }}
              />
            </>
          )}
        </>
      )}
    </>
  );
}
