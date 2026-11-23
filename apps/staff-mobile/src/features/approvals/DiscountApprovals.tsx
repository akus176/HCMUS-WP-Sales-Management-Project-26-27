import { useMemo, useState } from 'react';
import { StyleSheet, View } from 'react-native';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { router } from 'expo-router';

import { ApiError, NetworkError } from '../../api/errors';
import type { components } from '../../api/schema';
import {
  OfflineWriteError,
  useConnectivity,
  useSafeMutation,
} from '../../connectivity/connectivity';
import { useSession } from '../../session/context';
import type { StaffSession } from '../../session/session';
import {
  ShellButton,
  ShellSelection,
  ShellSheet,
  ShellFeedback,
  ShellHeading,
  ShellText,
  ShellMutationFeedback,
} from '../../shell/controls';
import { recordRow } from '../../shell/recordPresentation';

type PendingDiscount = components['schemas']['PendingDiscountResponse'];
type DiscountDecision = components['schemas']['DiscountDecisionResponse'];
type DecisionAction = 'approve' | 'reject';

const money = (amount: number | string) =>
  Number(amount).toLocaleString('vi-VN', { maximumFractionDigits: 2 });

function errorMessage(error: unknown, action = false) {
  if (error instanceof OfflineWriteError) return error.message;
  if (error instanceof ApiError) {
    if (error.status === 409)
      return 'This discount changed before your decision. Refresh the inbox and review the current order total.';
    if (error.status === 403)
      return 'Your approval permission has changed. Return to staff home and refresh access.';
    if (error.status === 404)
      return 'This discount is no longer pending. Refresh the inbox to see current requests.';
    return action
      ? 'The server rejected this decision. No order total was changed; review the request and try again.'
      : 'Pending approvals could not be loaded. Try refreshing the inbox.';
  }
  if (error instanceof NetworkError)
    return action
      ? 'The decision could not be confirmed. Refresh the inbox to check its current state before trying again; no write was queued.'
      : 'Pending approvals could not be loaded. Reconnect and try again.';
  return action
    ? 'The decision could not be confirmed. Refresh before trying again.'
    : 'Pending approvals could not be loaded. Reconnect and try again.';
}

function isForbidden(error: unknown) {
  return error instanceof ApiError && error.status === 403;
}

export function DiscountApprovals({
  user,
  discountId,
  lookupAllPages = false,
  showHeading = true,
}: {
  user: StaffSession;
  discountId?: string;
  lookupAllPages?: boolean;
  showHeading?: boolean;
}) {
  const { client } = useSession();
  const { status, canMutate, refreshConnection } = useConnectivity();
  const queryClient = useQueryClient();
  const queryKey = useMemo(
    () => ['discount-approvals', user.id] as const,
    [user.id],
  );
  const [selected, setSelected] = useState<PendingDiscount>();
  const [reviewDecision, setReviewDecision] = useState<{
    id: string;
    action: DecisionAction;
  }>();
  const [decisionResult, setDecisionResult] = useState<{
    action: DecisionAction;
    result: DiscountDecision;
  }>();
  const [actionError, setActionError] = useState<string>();
  const [apiForbidden, setApiForbidden] = useState(false);

  const pendingQuery = useQuery({
    queryKey: lookupAllPages ? [...queryKey, discountId] : queryKey,
    enabled: !!client && canMutate && !apiForbidden,
    queryFn: async ({ signal }) => {
      if (!client)
        throw new Error('The authenticated API client is unavailable.');
      if (!canMutate) throw new NetworkError();
      for (let page = 1; ; page++) {
        const { data } = await client.api.GET('/api/v1/discounts/pending', {
          params: { query: { page, pageSize: 100 } },
          signal,
        });
        if (!data) throw new Error('The API returned no pending approvals.');
        if (
          !lookupAllPages ||
          !discountId ||
          data.items.some((item) => item.discountId === discountId) ||
          data.items.length === 0 ||
          page * Number(data.pageSize) >= Number(data.totalCount)
        )
          return data;
      }
    },
  });

  const decisionMutation = useSafeMutation({
    mutationFn: async ({
      id,
      action,
    }: {
      id: string;
      action: DecisionAction;
    }) => {
      if (!client)
        throw new Error('The authenticated API client is unavailable.');
      if (!canMutate) throw new OfflineWriteError();
      const response =
        action === 'approve'
          ? await client.api.POST('/api/v1/discounts/{id}/approve', {
              params: { path: { id } },
            })
          : await client.api.POST('/api/v1/discounts/{id}/reject', {
              params: { path: { id } },
            });
      if (!response.data)
        throw new Error('The API returned no confirmed discount decision.');
      return { action, result: response.data };
    },
    onSuccess: async ({ action, result }) => {
      setActionError(undefined);
      setDecisionResult({ action, result });
      queryClient.setQueryData(queryKey, (current: typeof pendingQuery.data) =>
        current
          ? {
              ...current,
              items: current.items.filter(
                (item) => item.discountId !== result.discountId,
              ),
              totalCount: Math.max(0, Number(current.totalCount) - 1),
            }
          : current,
      );
      await queryClient.invalidateQueries({
        queryKey: ['discounts', user.id],
      });
    },
    onError: (error) => {
      setDecisionResult(undefined);
      setActionError(errorMessage(error, true));
      // A refetch after a conflict may have removed this request. Do not keep
      // offering actions through the snapshot captured before submission.
      if (error instanceof ApiError && [404, 409].includes(error.status))
        setSelected(undefined);
      if (error instanceof ApiError && error.status === 403) {
        setApiForbidden(true);
        queryClient.removeQueries({ queryKey });
        setSelected(undefined);
      }
    },
  });

  const item = pendingQuery.data?.items.find(
    (discount) => discount.discountId === discountId,
  );
  const refreshButton = (
    <ShellButton
      label="Refresh pending approvals"
      variant="secondary"
      disabled={status !== 'online' || pendingQuery.isFetching}
      loading={pendingQuery.isFetching}
      loadingLabel="Refreshing approvals…"
      onPress={() => void refreshConnection()}
    />
  );

  if (apiForbidden || (pendingQuery.error && isForbidden(pendingQuery.error)))
    return (
      <>
        <ShellHeading>Access changed</ShellHeading>
        <ShellFeedback
          kind="error"
          label="Your approval permission has changed. Return to staff home and refresh access."
        />
        <ShellButton
          label="Back to staff home"
          variant="text"
          onPress={() => router.replace('/session')}
        />
      </>
    );

  if (discountId) {
    const discount = item ?? selected;
    return (
      <>
        <ShellButton
          label="Back to pending approvals"
          variant="text"
          onPress={() => router.back()}
        />
        <ShellHeading>Discount review</ShellHeading>
        <ShellMutationFeedback mutation={decisionMutation} />
        {status !== 'online' && !pendingQuery.data ? (
          <ShellFeedback
            kind="warning"
            label="Reconnect to load this discount. No approval details are shown while offline."
          />
        ) : pendingQuery.isPending && !pendingQuery.data ? (
          <ShellFeedback kind="loading" label="Loading discount details…" />
        ) : pendingQuery.error ? (
          <ShellFeedback
            kind="error"
            label={errorMessage(pendingQuery.error)}
          />
        ) : !discount && !decisionResult ? (
          <>
            {actionError && <ShellFeedback kind="error" label={actionError} />}
            <ShellFeedback
              kind="info"
              label="This discount is no longer pending. Refresh the inbox to review current requests."
            />
            {refreshButton}
          </>
        ) : (
          <>
            {discount && (
              <View style={styles.card}>
                <ShellText>Order {discount.orderNumber}</ShellText>
                <ShellText>
                  {discount.type === 'percentage'
                    ? `${discount.value}% discount`
                    : `${money(discount.value)} discount`}
                </ShellText>
                <ShellText>Reason: {discount.reason}</ShellText>
                {discount.computedAmount != null && (
                  <ShellText>
                    Requested discount: {money(discount.computedAmount)}
                  </ShellText>
                )}
                <ShellText>
                  Current order total: {money(discount.orderTotal)}
                </ShellText>
              </View>
            )}
            {decisionResult && (
              <ShellFeedback
                kind="info"
                label={`${decisionResult.action === 'approve' ? 'Approved' : 'Rejected'} by the server. Confirmed order total: ${money(decisionResult.result.orderTotal)}.`}
              />
            )}
            {actionError && <ShellFeedback kind="error" label={actionError} />}
            {!decisionResult && discount && (
              <>
                <ShellButton
                  label="Approve discount"
                  loading={decisionMutation.isPending}
                  loadingLabel="Submitting decision…"
                  disabled={
                    decisionMutation.isWriteDisabled ||
                    decisionMutation.isPending
                  }
                  onPress={() => {
                    setActionError(undefined);
                    setSelected(discount);
                    setReviewDecision({
                      id: discount.discountId,
                      action: 'approve',
                    });
                  }}
                />
                <ShellButton
                  label="Reject discount"
                  variant="secondary"
                  loading={decisionMutation.isPending}
                  loadingLabel="Submitting decision…"
                  disabled={
                    decisionMutation.isWriteDisabled ||
                    decisionMutation.isPending
                  }
                  onPress={() => {
                    setActionError(undefined);
                    setSelected(discount);
                    setReviewDecision({
                      id: discount.discountId,
                      action: 'reject',
                    });
                  }}
                />
              </>
            )}
            <ShellSheet
              visible={!!reviewDecision && !!discount && !decisionResult}
              title={
                reviewDecision?.action === 'approve'
                  ? 'Approve this discount?'
                  : 'Reject this discount?'
              }
              busy={decisionMutation.isPending}
              onClose={() => setReviewDecision(undefined)}
            >
              <ShellText>
                Order {discount?.orderNumber} ·{' '}
                {discount?.type === 'percentage'
                  ? `${discount.value}%`
                  : money(discount?.value ?? 0)}
              </ShellText>
              <ShellText>{discount?.reason}</ShellText>
              <ShellButton
                label={
                  reviewDecision?.action === 'approve'
                    ? 'Confirm discount approval'
                    : 'Confirm discount rejection'
                }
                disabled={
                  decisionMutation.isWriteDisabled ||
                  pendingQuery.isFetching ||
                  !pendingQuery.data?.items.some(
                    (d) => d.discountId === reviewDecision?.id,
                  )
                }
                loading={decisionMutation.isPending}
                onPress={() => {
                  if (reviewDecision) {
                    decisionMutation.mutate(reviewDecision);
                    setReviewDecision(undefined);
                  }
                }}
              />
              <ShellButton
                label="Keep reviewing discount"
                variant="secondary"
                onPress={() => setReviewDecision(undefined)}
              />
            </ShellSheet>
            {refreshButton}
          </>
        )}
      </>
    );
  }

  return (
    <>
      {showHeading && <ShellHeading>Manager approvals</ShellHeading>}
      <ShellMutationFeedback mutation={decisionMutation} />
      {refreshButton}
      {actionError && <ShellFeedback kind="error" label={actionError} />}
      {status !== 'online' && !pendingQuery.data ? (
        <ShellFeedback
          kind="warning"
          label="Reconnect to load pending approvals."
        />
      ) : pendingQuery.isPending && !pendingQuery.data ? (
        <ShellFeedback kind="loading" label="Loading pending approvals…" />
      ) : pendingQuery.error ? (
        <ShellFeedback kind="error" label={errorMessage(pendingQuery.error)} />
      ) : pendingQuery.data?.items.length === 0 ? (
        <ShellFeedback
          kind="empty"
          label="No discounts are waiting for approval."
        />
      ) : (
        pendingQuery.data?.items.map((discount) => (
          <View key={discount.discountId} style={styles.card}>
            <ShellText>Order {discount.orderNumber}</ShellText>
            <ShellText>
              {discount.type === 'percentage'
                ? `${discount.value}% discount`
                : `${money(discount.value)} discount`}
            </ShellText>
            <ShellText>Reason: {discount.reason}</ShellText>
            <ShellText>
              Current order total: {money(discount.orderTotal)}
            </ShellText>
            <ShellSelection>
              <ShellButton
                label={`Review order ${discount.orderNumber} discount`}
                displayLabel="Review discount"
                variant="link"
                onPress={() =>
                  router.push({
                    pathname: '/modules/approvals/[discountId]',
                    params: { discountId: discount.discountId },
                  })
                }
              />
            </ShellSelection>
          </View>
        ))
      )}
    </>
  );
}

const styles = StyleSheet.create({
  card: recordRow,
});
