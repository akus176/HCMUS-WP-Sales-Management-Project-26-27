import { useRef, useState } from 'react';
import { randomUUID } from 'expo-crypto';
import { useQueryClient } from '@tanstack/react-query';

import { ApiError } from '../../api/errors';
import type { components } from '../../api/schema';
import { useSafeMutation } from '../../connectivity/connectivity';
import { useSession } from '../../session/context';
import {
  ShellButton,
  ShellFeedback,
  ShellText,
  ShellInput,
  ShellMutationFeedback,
  ShellSheet,
  ShellSelection,
  ShellPageFooter,
} from '../../shell/controls';
import { money } from './presentation';

type Payment = components['schemas']['RecordPaymentRequest'];
export type PendingPayments = Map<string, Payment>;

export function OrderPayment({
  orderId,
  balance,
  disabled,
  onAccessLost,
  pendingPayments,
}: {
  orderId: string;
  balance: number;
  disabled: boolean;
  onAccessLost: () => void;
  pendingPayments: PendingPayments;
}) {
  const { client, state } = useSession();
  const queries = useQueryClient();
  const [amount, setAmount] = useState('');
  const [method, setMethod] = useState('cash');
  const [reference, setReference] = useState('');
  const [attempt, setAttempt] = useState<Payment | null>(
    () => pendingPayments.get(orderId) ?? null,
  );
  const [sent, setSent] = useState(() => pendingPayments.has(orderId));
  const [opened, setOpened] = useState(() => pendingPayments.has(orderId));
  const [validation, setValidation] = useState<string | null>(null);
  const submitting = useRef(false);
  const mutation = useSafeMutation({
    mutationFn: async (body: Payment) => {
      const { data } = await client!.api.POST('/api/v1/orders/{id}/payments', {
        params: { path: { id: orderId } },
        body,
      });
      return data!;
    },
    onSuccess: () => {
      pendingPayments.delete(orderId);
      setAttempt(null);
      setAmount('');
      setReference('');
      setSent(false);
      setOpened(false);
    },
    onError: async (error) => {
      if (error instanceof ApiError) {
        if (error.status === 403) onAccessLost();
        // Definite rejection allows editing. Unknown outcomes retain the exact
        // command and identifier for an explicit, idempotent retry.
        if ([400, 404, 409, 422].includes(error.status)) {
          pendingPayments.delete(orderId);
          setAttempt(null);
          setSent(false);
        }
      }
      if (!(error instanceof ApiError && [403, 409].includes(error.status)))
        await queries.invalidateQueries({
          queryKey: ['sales', state.user!.id],
        });
    },
  });
  const unavailable =
    disabled || mutation.isWriteDisabled || mutation.isPending;

  function review() {
    mutation.reset();
    if (
      !/^\d+(\.\d{1,2})?$/.test(amount.trim()) ||
      Number(amount) <= 0 ||
      Number(amount) > balance
    ) {
      setValidation(
        'Enter an amount above zero, no greater than the balance, with at most two decimal places.',
      );
      return;
    }
    if (method !== 'cash' && !reference.trim()) {
      setValidation('Card and e-wallet payments require a payment reference.');
      return;
    }
    setValidation(null);
    setAttempt({
      paymentId: randomUUID(),
      amount: Number(amount),
      method,
      reference: reference.trim() || null,
    });
  }

  async function confirm() {
    if (!attempt || unavailable || submitting.current) return;
    if (!sent && Number(attempt.amount) > balance) {
      setValidation(
        'The balance changed. Review the current amount before recording a payment.',
      );
      setAttempt(null);
      return;
    }
    submitting.current = true;
    pendingPayments.set(orderId, attempt);
    setSent(true);
    try {
      await mutation.mutateAsync(attempt);
    } catch {
      /* Render the safe error and preserve an uncertain command. */
    } finally {
      submitting.current = false;
    }
  }

  return (
    <>
      <ShellMutationFeedback mutation={mutation} />
      {mutation.data && (
        <ShellFeedback
          kind="info"
          label={`Payment confirmed${mutation.data.alreadyRecorded ? ' (already recorded)' : ''}. Paid: ${money(mutation.data.paidAmount)}. Balance: ${money(mutation.data.outstandingBalance)}.`}
        />
      )}
      {(balance > 0 || attempt) && (
        <ShellPageFooter>
          <ShellButton
            label="Record payment"
            prominent
            disabled={unavailable && !attempt}
            onPress={() => setOpened(true)}
          />
        </ShellPageFooter>
      )}
      <ShellSheet
        visible={opened}
        title="Record payment"
        busy={mutation.isPending}
        onClose={() => setOpened(false)}
      >
        <ShellText>Balance due: {money(balance)}</ShellText>
        {validation && <ShellFeedback kind="error" label={validation} />}
        {mutation.error && (
          <ShellFeedback
            kind="error"
            label={
              attempt && sent
                ? 'Payment outcome is unknown. Refresh the order, then retry this same payment to confirm it safely. Do not enter a second payment for this amount.'
                : mutation.error instanceof ApiError &&
                    mutation.error.status === 409
                  ? 'Order changed elsewhere. Review the refreshed balance before recording a payment.'
                  : mutation.error.message
            }
          />
        )}
        {attempt ? (
          <>
            <ShellText>{`Review payment: ${money(attempt.amount)} · ${attempt.method}${attempt.reference ? ` · ${attempt.reference}` : ''}`}</ShellText>
            <ShellButton
              label={sent ? 'Retry same payment' : 'Confirm payment'}
              disabled={unavailable}
              loading={mutation.isPending}
              onPress={() => void confirm()}
            />
            {!sent && (
              <ShellButton
                label="Edit payment"
                variant="secondary"
                onPress={() => setAttempt(null)}
              />
            )}
          </>
        ) : (
          balance > 0 && (
            <>
              <ShellInput
                label="Amount"
                accessibilityLabel="Payment amount"
                keyboardType="decimal-pad"
                value={amount}
                onChangeText={setAmount}
                editable={!unavailable}
              />
              <ShellSelection segmented>
                {(['cash', 'card', 'e_wallet'] as const).map((value) => (
                  <ShellButton
                    key={value}
                    label={`Pay by ${value === 'e_wallet' ? 'e-wallet' : value}`}
                    displayLabel={
                      value === 'e_wallet'
                        ? 'E-wallet'
                        : value[0]!.toUpperCase() + value.slice(1)
                    }
                    selected={method === value}
                    variant="secondary"
                    disabled={unavailable}
                    onPress={() => setMethod(value)}
                  />
                ))}
              </ShellSelection>
              {method !== 'cash' && (
                <ShellInput
                  label="Payment reference (required for card or e-wallet)"
                  accessibilityLabel="Payment reference"
                  value={reference}
                  onChangeText={setReference}
                  editable={!unavailable}
                  maxLength={120}
                />
              )}
              <ShellButton
                label="Review payment"
                disabled={unavailable}
                onPress={review}
              />
            </>
          )
        )}
      </ShellSheet>
    </>
  );
}
