import { useMemo, useRef, useState } from 'react';
import { StyleSheet, TextInput, View } from 'react-native';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { randomUUID } from 'expo-crypto';

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
  ShellFeedback,
  ShellHeading,
  ShellText,
} from '../../shell/controls';
import { mobileTokens } from '../../shell/designTokens';

type Balance = components['schemas']['IngredientBalanceResponse'];
type Movement = components['schemas']['StockMovementResponse'];
type Receipt = components['schemas']['ReceiptResponse'];
type InventorySection = 'balances' | 'low-stock' | 'receipts' | 'history';

const quantity = (value: number | string) =>
  Number(value).toLocaleString('vi-VN', { maximumFractionDigits: 3 });

function readError(error: unknown) {
  if (error instanceof ApiError && error.status === 403)
    return 'Your inventory access changed. Return to staff home and refresh your permissions.';
  if (error instanceof NetworkError)
    return 'Inventory could not be loaded. Reconnect and refresh.';
  return 'Inventory could not be loaded. Refresh to get current data.';
}

function adjustmentError(error: unknown) {
  if (error instanceof OfflineWriteError)
    return 'You are offline. No adjustment was sent. Reconnect before retrying.';
  if (error instanceof NetworkError)
    return 'The adjustment could not be confirmed. Refresh the balance, then retry with the same values.';
  if (error instanceof ApiError && error.status === 403)
    return 'The server denied this adjustment. No stock was changed; refresh your permissions.';
  if (error instanceof ApiError && error.status === 404)
    return 'This ingredient no longer exists. Refresh the inventory list.';
  if (error instanceof ApiError && error.status === 409)
    return 'The server rejected this adjustment because the request conflicts with current stock. Refresh before retrying.';
  if (
    error instanceof ApiError &&
    (error.status === 400 || error.status === 422)
  )
    return 'The server rejected this adjustment. No stock was changed; check the amount and reason.';
  return 'The adjustment was not confirmed. Refresh the balance before trying again.';
}

function receiptError(error: unknown) {
  if (error instanceof OfflineWriteError)
    return 'You are offline. No receipt was posted. Reconnect and try again.';
  if (error instanceof NetworkError)
    return 'The receipt post could not be confirmed. Refresh the receipt before retrying.';
  if (error instanceof ApiError && error.status === 403)
    return 'The server denied receipt access. Refresh your permissions.';
  if (error instanceof ApiError && error.status === 404)
    return 'This receipt no longer exists. Refresh the receipt list.';
  if (error instanceof ApiError && error.status === 409)
    return 'The receipt changed or was already posted. Refresh to see its current status; stock was not posted twice.';
  if (
    error instanceof ApiError &&
    (error.status === 400 || error.status === 422)
  )
    return 'The server rejected this receipt. Check its lines; stock was not changed.';
  return 'The receipt post was not confirmed. Refresh before trying again.';
}

export function InventoryWorkspace({ user }: { user: StaffSession }) {
  const { client } = useSession();
  const { status, refreshConnection } = useConnectivity();
  const queryClient = useQueryClient();
  const canManage = user.permissions.includes('inventory.manage');
  const canViewLowStock = user.permissions.includes('reports.lowstock');
  const canReceive = user.permissions.includes('inventory.receive');
  const availableSections = useMemo(
    () =>
      [
        canManage ? 'balances' : null,
        canViewLowStock ? 'low-stock' : null,
        canReceive ? 'receipts' : null,
        canManage ? 'history' : null,
      ].filter((section): section is InventorySection => section !== null),
    [canManage, canReceive, canViewLowStock],
  );
  const [section, setSection] = useState<InventorySection>(
    canManage ? 'balances' : canViewLowStock ? 'low-stock' : 'receipts',
  );
  const [search, setSearch] = useState('');
  const [adjustmentIngredient, setAdjustmentIngredient] = useState<Balance>();
  const [quantityDelta, setQuantityDelta] = useState('');
  const [adjustmentReason, setAdjustmentReason] = useState('');
  const [adjustmentResult, setAdjustmentResult] = useState<{
    ingredientId: string;
    availableQuantity: number | string;
  }>();
  const [adjustmentMessage, setAdjustmentMessage] = useState<string>();
  const [movementIngredient, setMovementIngredient] = useState<Balance>();
  const [receiptId, setReceiptId] = useState<string>();
  const [receiptMessage, setReceiptMessage] = useState<string>();
  const [receiptFailure, setReceiptFailure] = useState<string>();
  const operationId = useRef<{ signature: string; id: string } | undefined>(
    undefined,
  );
  const selectedSection = availableSections.includes(section)
    ? section
    : (availableSections[0] ?? 'balances');

  const ingredientQuery = useQuery({
    queryKey: ['inventory-balances', user.id, selectedSection, search.trim()],
    enabled:
      !!client &&
      status !== 'offline' &&
      ((selectedSection === 'low-stock' && canViewLowStock) ||
        (selectedSection === 'balances' && canManage)),
    queryFn: async ({ signal }) => {
      if (!client)
        throw new Error('The authenticated API client is unavailable.');
      if (status === 'offline') throw new NetworkError();
      const path =
        selectedSection === 'low-stock'
          ? '/api/v1/ingredients/low-stock'
          : '/api/v1/ingredients';
      const { data } = await client.api.GET(path, {
        params: {
          query: {
            page: 1,
            pageSize: 100,
            ...(search.trim() ? { search: search.trim() } : {}),
          },
        },
        signal,
      });
      if (!data) throw new Error('The API returned no inventory data.');
      return data;
    },
  });

  const movementQuery = useQuery({
    queryKey: [
      'inventory-movements',
      user.id,
      movementIngredient?.ingredientId,
    ],
    enabled:
      !!client &&
      !!movementIngredient &&
      canManage &&
      (selectedSection === 'balances' || selectedSection === 'low-stock') &&
      status !== 'offline',
    queryFn: async ({ signal }) => {
      if (!client || !movementIngredient)
        throw new Error('The authenticated inventory client is unavailable.');
      const { data } = await client.api.GET(
        '/api/v1/ingredients/{id}/movements',
        {
          params: { path: { id: movementIngredient.ingredientId } },
          signal,
        },
      );
      if (!data) throw new Error('The API returned no movement history.');
      return data;
    },
  });

  const receiptsQuery = useQuery({
    queryKey: ['purchase-receipts', user.id],
    enabled:
      !!client &&
      canReceive &&
      selectedSection === 'receipts' &&
      status !== 'offline',
    queryFn: async ({ signal }) => {
      if (!client)
        throw new Error('The authenticated API client is unavailable.');
      const { data } = await client.api.GET('/api/v1/purchase-receipts', {
        signal,
      });
      if (!data) throw new Error('The API returned no purchase receipts.');
      return data;
    },
  });

  const receiptDetailQuery = useQuery({
    queryKey: ['purchase-receipt', user.id, receiptId],
    enabled: !!client && !!receiptId && canReceive && status !== 'offline',
    queryFn: async ({ signal }) => {
      if (!client || !receiptId)
        throw new Error('The selected receipt is unavailable.');
      const { data } = await client.api.GET('/api/v1/purchase-receipts/{id}', {
        params: { path: { id: receiptId } },
        signal,
      });
      if (!data) throw new Error('The API returned no receipt details.');
      return data;
    },
  });

  const movementHistoryQuery = useQuery({
    queryKey: ['inventory-movements', user.id, 'all'],
    enabled:
      !!client &&
      canManage &&
      selectedSection === 'history' &&
      status !== 'offline',
    queryFn: async ({ signal }) => {
      if (!client)
        throw new Error('The authenticated API client is unavailable.');
      const { data } = await client.api.GET('/api/v1/stock-movements', {
        signal,
      });
      if (!data) throw new Error('The API returned no movement history.');
      return data;
    },
  });

  const adjustmentMutation = useSafeMutation({
    mutationFn: async ({
      ingredient,
      delta,
      reason,
    }: {
      ingredient: Balance;
      delta: number;
      reason: string;
    }) => {
      if (!client)
        throw new Error('The authenticated API client is unavailable.');
      const signature = `${ingredient.ingredientId}|${delta}|${reason}`;
      if (operationId.current?.signature !== signature)
        operationId.current = { signature, id: randomUUID() };
      const { data, error, response } = await client.api.POST(
        '/api/v1/ingredients/{id}/adjustments',
        {
          params: { path: { id: ingredient.ingredientId } },
          body: {
            requestId: operationId.current.id,
            quantityDelta: delta,
            reason,
          },
        },
      );
      if (error) throw new ApiError(response.status);
      if (!data) throw new Error('The API did not confirm the adjustment.');
      return data;
    },
    onSuccess: async (result) => {
      setAdjustmentResult(result);
      setAdjustmentMessage(undefined);
      setQuantityDelta('');
      setAdjustmentReason('');
      operationId.current = undefined;
      await queryClient.invalidateQueries({
        queryKey: ['inventory-balances', user.id],
      });
      await queryClient.invalidateQueries({
        queryKey: ['inventory-movements', user.id, result.ingredientId],
      });
    },
    onError: (error) => setAdjustmentMessage(adjustmentError(error)),
  });

  const receiptMutation = useSafeMutation({
    mutationFn: async (id: string) => {
      if (!client)
        throw new Error('The authenticated API client is unavailable.');
      const { error, response } = await client.api.POST(
        '/api/v1/purchase-receipts/{id}/post',
        {
          params: { path: { id } },
        },
      );
      if (error) throw new ApiError(response.status);
      return id;
    },
    onSuccess: async (id) => {
      setReceiptFailure(undefined);
      setReceiptMessage(
        `Receipt ${receiptDetailQuery.data?.receiptNumber ?? ''} was posted by the server.`.trim(),
      );
      await queryClient.invalidateQueries({
        queryKey: ['purchase-receipts', user.id],
      });
      await queryClient.invalidateQueries({
        queryKey: ['purchase-receipt', user.id, id],
      });
      await queryClient.invalidateQueries({
        queryKey: ['inventory-balances', user.id],
      });
      await queryClient.invalidateQueries({
        queryKey: ['inventory-movements', user.id],
      });
    },
    onError: (error) => setReceiptFailure(receiptError(error)),
  });

  const refreshLabel =
    selectedSection === 'low-stock'
      ? 'Refresh low-stock list'
      : 'Refresh inventory';
  const refreshButton = (
    <ShellButton
      label={refreshLabel}
      variant="secondary"
      disabled={status !== 'online' || ingredientQuery.isFetching}
      loading={ingredientQuery.isFetching}
      loadingLabel="Refreshing inventory…"
      onPress={() => void refreshConnection()}
    />
  );

  const sections: readonly { id: InventorySection; title: string }[] = [
    { id: 'balances', title: 'Ingredient balances' },
    { id: 'low-stock', title: 'Low stock' },
    { id: 'receipts', title: 'Purchase receipts' },
    { id: 'history', title: 'Movement history' },
  ];

  return (
    <>
      <ShellHeading>Inventory</ShellHeading>
      <ShellText>Balances and changes from the shared shop API.</ShellText>
      <View style={styles.tabs}>
        {sections
          .filter(({ id }) => availableSections.includes(id))
          .map(({ id, title }) => (
            <ShellButton
              key={id}
              label={title}
              selected={selectedSection === id}
              variant="secondary"
              onPress={() => setSection(id)}
            />
          ))}
      </View>
      {selectedSection === 'balances' || selectedSection === 'low-stock' ? (
        <>
          <TextInput
            accessibilityLabel="Search ingredients"
            placeholder="Search ingredients"
            placeholderTextColor={mobileTokens.color.disabledText}
            returnKeyType="search"
            value={search}
            onChangeText={setSearch}
            style={styles.search}
          />
          {refreshButton}
          {status === 'offline' && !ingredientQuery.data ? (
            <ShellFeedback
              kind="warning"
              label="Reconnect to load current inventory. No inventory changes are saved offline."
            />
          ) : ingredientQuery.isPending && !ingredientQuery.data ? (
            <ShellFeedback kind="loading" label="Loading inventory…" />
          ) : ingredientQuery.error ? (
            <ShellFeedback
              kind="error"
              label={readError(ingredientQuery.error)}
            />
          ) : ingredientQuery.data?.items.length === 0 ? (
            <ShellFeedback
              kind="empty"
              label={
                selectedSection === 'low-stock'
                  ? 'No low-stock ingredients match.'
                  : 'No ingredients match this search.'
              }
            />
          ) : (
            ingredientQuery.data?.items.map((ingredient: Balance) => {
              const isLow =
                Number(ingredient.availableQuantity) <=
                Number(ingredient.reorderThreshold);
              return (
                <View key={ingredient.ingredientId} style={styles.card}>
                  <ShellText>{ingredient.name}</ShellText>
                  <ShellText>
                    Balance: {quantity(ingredient.availableQuantity)}{' '}
                    {ingredient.baseUnit}
                  </ShellText>
                  <ShellText>
                    Reorder threshold: {quantity(ingredient.reorderThreshold)}{' '}
                    {ingredient.baseUnit}
                  </ShellText>
                  {isLow && (
                    <ShellFeedback
                      kind="warning"
                      label={`Low stock · ${quantity(ingredient.availableQuantity)} ${ingredient.baseUnit} / ${quantity(ingredient.reorderThreshold)} ${ingredient.baseUnit}`}
                    />
                  )}
                  {user.permissions.includes('inventory.adjust') && (
                    <ShellButton
                      label={`Record adjustment for ${ingredient.name}`}
                      variant="secondary"
                      onPress={() => {
                        setAdjustmentIngredient(ingredient);
                        setAdjustmentMessage(undefined);
                        setAdjustmentResult(undefined);
                      }}
                    />
                  )}
                  {canManage && (
                    <ShellButton
                      label={`Show movement history for ${ingredient.name}`}
                      variant="secondary"
                      onPress={() => setMovementIngredient(ingredient)}
                    />
                  )}
                  {adjustmentIngredient?.ingredientId ===
                    ingredient.ingredientId && (
                    <View style={styles.form}>
                      <ShellText>
                        Stock adjustment for {ingredient.name}
                      </ShellText>
                      <TextInput
                        accessibilityLabel="Quantity change"
                        keyboardType="decimal-pad"
                        placeholder="Quantity change"
                        placeholderTextColor={mobileTokens.color.disabledText}
                        value={quantityDelta}
                        onChangeText={setQuantityDelta}
                        style={styles.search}
                      />
                      <TextInput
                        accessibilityLabel="Adjustment reason"
                        placeholder="Reason for adjustment"
                        placeholderTextColor={mobileTokens.color.disabledText}
                        value={adjustmentReason}
                        onChangeText={setAdjustmentReason}
                        style={styles.search}
                      />
                      {adjustmentMessage && (
                        <ShellFeedback kind="error" label={adjustmentMessage} />
                      )}
                      {adjustmentResult?.ingredientId ===
                        ingredient.ingredientId && (
                        <ShellFeedback
                          kind="info"
                          label={`Adjustment saved by the server. New balance: ${quantity(adjustmentResult.availableQuantity)} ${ingredient.baseUnit}.`}
                        />
                      )}
                      <ShellButton
                        label="Save adjustment"
                        loading={adjustmentMutation.isPending}
                        loadingLabel="Saving adjustment…"
                        disabled={
                          status !== 'online' ||
                          !quantityDelta.trim() ||
                          !Number.isFinite(Number(quantityDelta)) ||
                          Number(quantityDelta) === 0 ||
                          !adjustmentReason.trim()
                        }
                        onPress={() => {
                          setAdjustmentMessage(undefined);
                          adjustmentMutation.mutate({
                            ingredient,
                            delta: Number(quantityDelta),
                            reason: adjustmentReason.trim(),
                          });
                        }}
                      />
                    </View>
                  )}
                </View>
              );
            })
          )}
        </>
      ) : selectedSection === 'receipts' ? (
        <>
          {receiptId ? (
            <>
              <ShellButton
                label="Back to receipt list"
                variant="secondary"
                onPress={() => {
                  setReceiptId(undefined);
                  setReceiptMessage(undefined);
                  setReceiptFailure(undefined);
                }}
              />
              {receiptDetailQuery.isPending && !receiptDetailQuery.data ? (
                <ShellFeedback
                  kind="loading"
                  label="Loading receipt details…"
                />
              ) : receiptDetailQuery.error ? (
                <ShellFeedback
                  kind="error"
                  label="Receipt details could not be loaded. Refresh the list and try again."
                />
              ) : receiptDetailQuery.data ? (
                <View style={styles.card}>
                  <ShellHeading>
                    Receipt {receiptDetailQuery.data.receiptNumber}
                  </ShellHeading>
                  <ShellText>
                    Status: {receiptDetailQuery.data.status}
                  </ShellText>
                  {receiptDetailQuery.data.lines.map((line) => (
                    <ShellText key={line.id}>
                      Receipt line · {quantity(line.quantity)} ·{' '}
                      {quantity(line.unitCost)} per unit
                    </ShellText>
                  ))}
                  {!receiptDetailQuery.data.lines.length && (
                    <ShellFeedback
                      kind="warning"
                      label="This receipt has no lines and cannot be posted."
                    />
                  )}
                  {receiptMessage && (
                    <ShellFeedback kind="info" label={receiptMessage} />
                  )}
                  {receiptFailure && (
                    <ShellFeedback kind="error" label={receiptFailure} />
                  )}
                  {receiptFailure && receiptId && (
                    <ShellButton
                      label="Refresh receipt status"
                      variant="secondary"
                      disabled={
                        status !== 'online' || receiptDetailQuery.isFetching
                      }
                      loading={receiptDetailQuery.isFetching}
                      loadingLabel="Refreshing receipt status…"
                      onPress={() => {
                        setReceiptFailure(undefined);
                        void queryClient.invalidateQueries({
                          queryKey: ['purchase-receipt', user.id, receiptId],
                        });
                      }}
                    />
                  )}
                  {receiptDetailQuery.data.status.toLowerCase() === 'draft' &&
                    receiptDetailQuery.data.lines.length > 0 && (
                      <ShellButton
                        label={`Post receipt ${receiptDetailQuery.data.receiptNumber}`}
                        loading={receiptMutation.isPending}
                        loadingLabel="Posting receipt…"
                        disabled={
                          status !== 'online' || receiptMutation.isPending
                        }
                        onPress={() => {
                          setReceiptFailure(undefined);
                          receiptMutation.mutate(receiptDetailQuery.data!.id);
                        }}
                      />
                    )}
                </View>
              ) : null}
            </>
          ) : (
            <>
              <ShellText>
                Review prepared receipts from the shared API.
              </ShellText>
              <ShellButton
                label="Refresh receipts"
                variant="secondary"
                disabled={status !== 'online' || receiptsQuery.isFetching}
                loading={receiptsQuery.isFetching}
                onPress={() => void refreshConnection()}
              />
              {status === 'offline' && !receiptsQuery.data ? (
                <ShellFeedback
                  kind="warning"
                  label="Reconnect to load receipts."
                />
              ) : receiptsQuery.isPending && !receiptsQuery.data ? (
                <ShellFeedback kind="loading" label="Loading receipts…" />
              ) : receiptsQuery.error ? (
                <ShellFeedback
                  kind="error"
                  label="Receipts could not be loaded. Refresh and try again."
                />
              ) : receiptsQuery.data?.length === 0 ? (
                <ShellFeedback
                  kind="empty"
                  label="No purchase receipts are available."
                />
              ) : (
                receiptsQuery.data?.map((receipt: Receipt) => (
                  <View key={receipt.id} style={styles.card}>
                    <ShellText>{receipt.receiptNumber}</ShellText>
                    <ShellText>Status: {receipt.status}</ShellText>
                    <ShellButton
                      label={`Review receipt ${receipt.receiptNumber}`}
                      variant="secondary"
                      onPress={() => {
                        setReceiptId(receipt.id);
                        setReceiptMessage(undefined);
                        setReceiptFailure(undefined);
                      }}
                    />
                  </View>
                ))
              )}
            </>
          )}
        </>
      ) : (
        <>
          <ShellText>Recent stock movements from the shared API.</ShellText>
          <ShellButton
            label="Refresh movement history"
            variant="secondary"
            disabled={status !== 'online' || movementHistoryQuery.isFetching}
            loading={movementHistoryQuery.isFetching}
            onPress={() => void refreshConnection()}
          />
          {status === 'offline' && !movementHistoryQuery.data ? (
            <ShellFeedback
              kind="warning"
              label="Reconnect to load movement history."
            />
          ) : movementHistoryQuery.isPending && !movementHistoryQuery.data ? (
            <ShellFeedback kind="loading" label="Loading movement history…" />
          ) : movementHistoryQuery.error ? (
            <ShellFeedback
              kind="error"
              label="Movement history could not be loaded. Refresh and try again."
            />
          ) : movementHistoryQuery.data?.length === 0 ? (
            <ShellFeedback
              kind="empty"
              label="No stock movements are recorded."
            />
          ) : (
            movementHistoryQuery.data?.map((movement: Movement) => (
              <View key={movement.id} style={styles.card}>
                <ShellText>
                  {movement.type} · {quantity(movement.quantityDelta)}
                </ShellText>
                <ShellText>{movement.reason}</ShellText>
                {movement.purchaseReceiptId && (
                  <ShellText>
                    Receipt ID: {movement.purchaseReceiptId}
                  </ShellText>
                )}
              </View>
            ))
          )}
        </>
      )}
      {movementIngredient && selectedSection !== 'receipts' && (
        <View style={styles.card}>
          <ShellHeading>
            Movement history · {movementIngredient.name}
          </ShellHeading>
          {movementQuery.isPending ? (
            <ShellFeedback kind="loading" label="Loading movement history…" />
          ) : movementQuery.error ? (
            <ShellFeedback
              kind="error"
              label="Movement history could not be loaded. Refresh and try again."
            />
          ) : movementQuery.data?.length === 0 ? (
            <ShellFeedback
              kind="empty"
              label="No stock movements are recorded."
            />
          ) : (
            movementQuery.data?.map((movement: Movement) => (
              <ShellText key={movement.id}>
                {movement.type}: {quantity(movement.quantityDelta)}{' '}
                {movementIngredient.baseUnit} · {movement.reason}
              </ShellText>
            ))
          )}
        </View>
      )}
    </>
  );
}

const styles = StyleSheet.create({
  tabs: { gap: mobileTokens.spacing.sm },
  search: {
    minHeight: 48,
    borderColor: mobileTokens.color.border,
    borderWidth: 2,
    borderRadius: mobileTokens.shape.control,
    paddingHorizontal: mobileTokens.spacing.md,
    color: mobileTokens.color.foreground,
    fontSize: mobileTokens.type.body,
    backgroundColor: mobileTokens.color.background,
  },
  card: {
    backgroundColor: mobileTokens.color.infoBackground,
    borderColor: mobileTokens.color.border,
    borderRadius: mobileTokens.shape.card,
    borderWidth: 2,
    gap: mobileTokens.spacing.sm,
    padding: mobileTokens.spacing.md,
  },
  form: { gap: mobileTokens.spacing.sm },
});
