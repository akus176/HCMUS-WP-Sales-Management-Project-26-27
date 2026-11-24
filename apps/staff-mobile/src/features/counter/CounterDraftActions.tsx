import { useRef, useState } from 'react';
import { randomUUID } from 'expo-crypto';
import { useQueryClient } from '@tanstack/react-query';
import { useSession } from '../../session/context';
import type { StaffSession } from '../../session/session';
import { useSafeMutation } from '../../connectivity/connectivity';
import { ApiError } from '../../api/errors';
import {
  ShellButton,
  ShellFeedback,
  ShellInput,
  ShellMutationFeedback,
  ShellPageFooter,
  ShellSelection,
  ShellSheet,
  ShellText,
} from '../../shell/controls';
import { money } from '../orders/presentation';
import {
  counterError,
  uncertainOutcome,
  type SavedOrder,
} from './counterModel';

type Action =
  | { kind: 'confirm'; reviewedTotal: number }
  | {
      kind: 'discount';
      discountId: string;
      type: 'fixed' | 'percentage';
      value: number;
      reason: string;
    }
  | { kind: 'loyalty'; points: number };
export type PendingCounterActions = Map<string, Action>;

export function counterActionResolved(order: SavedOrder, action: Action) {
  if (action.kind === 'confirm') return order.status !== 'draft';
  if (action.kind === 'discount')
    return order.discounts.some(
      (discount) => discount.id === action.discountId,
    );
  return (
    Number(order.subtotal) - Number(order.discountTotal) - Number(order.total) >
    0
  );
}

export function CounterDraftActions({
  user,
  order,
  disabled,
  pendingActions,
  onAccessLost,
}: {
  user: StaffSession;
  order: SavedOrder;
  disabled: boolean;
  pendingActions: PendingCounterActions;
  onAccessLost: () => void;
}) {
  const { client } = useSession();
  const queries = useQueryClient();
  const submitting = useRef(false);
  const stored = pendingActions.get(order.id);
  const pending =
    stored && !counterActionResolved(order, stored) ? stored : undefined;
  const [sheet, setSheet] = useState<'confirm' | 'discount' | 'loyalty'>();
  const [review, setReview] = useState<Action>();
  const [type, setType] = useState<'fixed' | 'percentage'>('fixed');
  const [value, setValue] = useState('');
  const [reason, setReason] = useState('');
  const [points, setPoints] = useState('');
  const [validation, setValidation] = useState('');
  const [notice, setNotice] = useState('');
  const mutation = useSafeMutation({
    mutationFn: async (action: Action) => {
      if (!client)
        throw new Error('The authenticated Counter client is unavailable.');
      pendingActions.set(order.id, action);
      if (action.kind === 'confirm') {
        const { data } = await client.api.POST('/api/v1/orders/{id}/confirm', {
          params: { path: { id: order.id } },
          body: { reviewedTotal: action.reviewedTotal },
        });
        if (!data)
          throw new Error('Confirmation was not returned by the service.');
        queries.setQueryData(['sales', user.id, 'order', order.id], data);
      } else if (action.kind === 'discount') {
        const { data } = await client.api.POST(
          '/api/v1/orders/{id}/discounts',
          {
            params: { path: { id: order.id } },
            body: {
              discountId: action.discountId,
              type: action.type,
              value: action.value,
              reason: action.reason,
            },
          },
        );
        if (!data)
          throw new Error(
            'The discount request was not returned by the service.',
          );
      } else {
        const { data } = await client.api.POST(
          '/api/v1/orders/{id}/loyalty/redeem',
          {
            params: { path: { id: order.id } },
            body: { points: action.points },
          },
        );
        if (!data)
          throw new Error(
            'The loyalty result was not returned by the service.',
          );
      }
      return action;
    },
    onSuccess: async (action) => {
      pendingActions.delete(order.id);
      setSheet(undefined);
      setReview(undefined);
      setNotice(
        action.kind === 'confirm'
          ? 'Order confirmed by the service. It can now be prepared.'
          : action.kind === 'discount'
            ? 'Discount requested. It is not applied until a manager approves it.'
            : 'Loyalty points redeemed by the service. Review the updated total.',
      );
      await Promise.all([
        queries.invalidateQueries({ queryKey: ['sales', user.id] }),
        queries.invalidateQueries({ queryKey: ['preparation', user.id] }),
        queries.invalidateQueries({ queryKey: ['counter-customers', user.id] }),
      ]);
    },
    onError: async (error) => {
      setNotice('');
      if (!uncertainOutcome(error)) {
        pendingActions.delete(order.id);
      }
      if (error instanceof ApiError && error.status === 403) onAccessLost();
      setSheet(undefined);
      setReview(undefined);
      await queries.invalidateQueries({
        queryKey: ['sales', user.id, 'order', order.id],
      });
    },
  });
  const pendingPermission =
    !pending ||
    user.permissions.includes(
      pending.kind === 'confirm'
        ? 'orders.confirm'
        : pending.kind === 'discount'
          ? 'discounts.request'
          : 'loyalty.redeem',
    );
  const unavailable =
    !pendingPermission ||
    disabled ||
    mutation.isWriteDisabled ||
    mutation.isPending;
  const waitingDiscount = order.discounts.some(
    (discount) => discount.status === 'pending',
  );
  const loyaltySavings = Math.max(
    0,
    Number(order.subtotal) - Number(order.discountTotal) - Number(order.total),
  );
  async function submit(action: Action) {
    if (unavailable || submitting.current) return;
    const permission =
      action.kind === 'confirm'
        ? 'orders.confirm'
        : action.kind === 'discount'
          ? 'discounts.request'
          : 'loyalty.redeem';
    if (!user.permissions.includes(permission)) {
      setValidation(
        'Your permissions changed. Refresh your account before taking this action.',
      );
      return;
    }
    submitting.current = true;
    try {
      await mutation.mutateAsync(action);
    } catch {
      /* Keep the command for an explicit retry when its outcome is unknown. */
    } finally {
      submitting.current = false;
    }
  }
  function open(kind: 'confirm' | 'discount' | 'loyalty') {
    setValidation('');
    setNotice('');
    mutation.reset();
    setSheet(kind);
    setReview(
      kind === 'confirm'
        ? { kind: 'confirm', reviewedTotal: Number(order.total) }
        : undefined,
    );
  }
  function reviewAdjustment() {
    if (sheet === 'discount') {
      if (
        !/^\d+(\.\d{1,2})?$/.test(value.trim()) ||
        !Number.isFinite(Number(value)) ||
        Number(value) <= 0 ||
        (type === 'percentage' && Number(value) > 100) ||
        !reason.trim()
      ) {
        setValidation(
          'Enter a positive discount, up to 100 for a percentage, and a reason.',
        );
        return;
      }
      setReview({
        kind: 'discount',
        discountId: randomUUID(),
        type,
        value: Number(value),
        reason: reason.trim(),
      });
    } else {
      if (
        !/^\d+$/.test(points.trim()) ||
        !Number.isSafeInteger(Number(points)) ||
        Number(points) <= 0 ||
        Number(points) > 2147483647
      ) {
        setValidation('Enter a positive whole number of loyalty points.');
        return;
      }
      setReview({ kind: 'loyalty', points: Number(points) });
    }
    setValidation('');
  }
  return (
    <>
      <ShellMutationFeedback mutation={mutation} />
      {!!notice && <ShellFeedback kind="success" label={notice} />}
      {mutation.error && (
        <ShellFeedback kind="error" label={counterError(mutation.error)} />
      )}
      {pending && !mutation.isPending && (
        <>
          <ShellFeedback
            kind="warning"
            label="This action has an unconfirmed result. Refresh the ticket or retry this same action; do not submit a second request."
          />
          <ShellButton
            label="Retry same Counter action"
            disabled={unavailable}
            onPress={() => void submit(pending)}
          />
        </>
      )}
      {order.status === 'draft' && !pending && (
        <>
          {waitingDiscount && (
            <ShellFeedback
              kind="info"
              label="A discount request is awaiting approval. Refresh the ticket after the manager's decision."
            />
          )}
          {user.permissions.includes('discounts.request') && (
            <ShellButton
              label="Request discount"
              variant="secondary"
              disabled={unavailable}
              onPress={() => open('discount')}
            />
          )}
          {user.permissions.includes('loyalty.redeem') && order.customerId && (
            <>
              {loyaltySavings > 0 ? (
                <ShellText>Loyalty savings: {money(loyaltySavings)}</ShellText>
              ) : (
                <ShellButton
                  label="Redeem loyalty points"
                  variant="secondary"
                  disabled={unavailable}
                  onPress={() => open('loyalty')}
                />
              )}
            </>
          )}
          {user.permissions.includes('orders.confirm') && (
            <ShellPageFooter>
              <ShellButton
                label="Confirm order"
                prominent
                disabled={unavailable || waitingDiscount}
                onPress={() => open('confirm')}
              />
            </ShellPageFooter>
          )}
        </>
      )}
      <ShellSheet
        visible={!!sheet}
        title={
          sheet === 'confirm'
            ? 'Confirm order'
            : sheet === 'discount'
              ? 'Request discount'
              : 'Redeem loyalty points'
        }
        busy={mutation.isPending}
        onClose={() => {
          setSheet(undefined);
          setReview(undefined);
        }}
      >
        {!review && sheet === 'discount' && (
          <>
            <ShellSelection>
              {(['fixed', 'percentage'] as const).map((choice) => (
                <ShellButton
                  key={choice}
                  label={choice === 'fixed' ? 'Fixed amount' : 'Percentage'}
                  variant="secondary"
                  selected={type === choice}
                  onPress={() => setType(choice)}
                />
              ))}
            </ShellSelection>
            <ShellInput
              label="Discount value"
              keyboardType="decimal-pad"
              value={value}
              onChangeText={setValue}
            />
            <ShellInput
              label="Discount reason"
              value={reason}
              onChangeText={setReason}
              multiline
            />
          </>
        )}
        {!review && sheet === 'loyalty' && (
          <ShellInput
            label="Loyalty points to redeem"
            keyboardType="number-pad"
            value={points}
            onChangeText={setPoints}
          />
        )}
        {!!validation && <ShellFeedback kind="error" label={validation} />}
        {!review ? (
          <ShellButton label="Review adjustment" onPress={reviewAdjustment} />
        ) : (
          <>
            <ShellText>Ticket {order.orderNumber}</ShellText>
            <ShellText>
              {review.kind === 'confirm'
                ? `Confirm the reviewed total of ${money(review.reviewedTotal)}. This sends the order to Preparation and reserves the table for dine-in.`
                : review.kind === 'discount'
                  ? `Request ${review.value}${review.type === 'percentage' ? '%' : ''} discount. ${review.reason}`
                  : `Redeem ${review.points} points on this ticket.`}
            </ShellText>
            {review.kind === 'confirm' &&
              Number(order.total) !== review.reviewedTotal && (
                <ShellFeedback
                  kind="warning"
                  label="The ticket total changed. Close this review and check the new total."
                />
              )}
            <ShellButton
              label={
                review.kind === 'confirm'
                  ? 'Confirm reviewed order'
                  : 'Submit reviewed adjustment'
              }
              disabled={
                unavailable ||
                order.status !== 'draft' ||
                (review.kind === 'confirm' &&
                  (Number(order.total) !== review.reviewedTotal ||
                    waitingDiscount))
              }
              loading={mutation.isPending}
              onPress={() => void submit(review)}
            />
          </>
        )}
        <ShellButton
          label="Keep reviewing ticket"
          variant="secondary"
          disabled={mutation.isPending}
          onPress={() => {
            setSheet(undefined);
            setReview(undefined);
          }}
        />
      </ShellSheet>
    </>
  );
}
