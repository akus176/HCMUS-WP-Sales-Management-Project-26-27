import { useEffect, useRef, useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import {
  BackHandler,
  Pressable,
  StyleSheet,
  Text,
  View,
  useWindowDimensions,
} from 'react-native';

import { ApiError } from '../api/errors';
import type { components } from '../api/schema';
import { useSafeMutation } from '../connectivity/connectivity';
import { useSession } from '../session/context';
import {
  ShellButton,
  ShellFeedback,
  ShellHeading,
  ShellText,
  ShellSelection,
  ShellMutationFeedback,
  ShellPageFooter,
  usePageNavigation,
} from '../shell/controls';
import { mobileTokens } from '../shell/designTokens';

const pageSize = 10;
type Filter = 'confirmed,preparing' | 'confirmed' | 'preparing';
type Intent = { id: string; action: 'start' | 'ready'; status: string };
type Ticket = components['schemas']['PreparationTicketResponse'];

function TicketStatus({ status }: { status: string }) {
  const label =
    status === 'confirmed'
      ? 'Confirmed'
      : status === 'preparing'
        ? 'Preparing'
        : status;
  return (
    <Text
      accessibilityLabel={`Status: ${label}`}
      style={[styles.status, status === 'preparing' && styles.preparing]}
    >
      {label}
    </Text>
  );
}

const itemCount = (ticket: Ticket) =>
  ticket.lines.reduce((sum, line) => sum + Number(line.quantity), 0);
const location = (ticket: Ticket) =>
  ticket.tableNumber ? `Table ${ticket.tableNumber}` : 'Takeaway / delivery';
const preview = (ticket: Ticket) => {
  const items = ticket.lines
    .slice(0, 2)
    .map((line) => `${line.quantity} × ${line.itemName}`)
    .join(', ');
  const remaining = ticket.lines.length - 2;
  return remaining > 0 ? `${items} + ${remaining} more` : items;
};

export function PreparationScreen({
  ticketId,
  showHeading = true,
}: { ticketId?: string; showHeading?: boolean } = {}) {
  const { fontScale } = useWindowDimensions();
  const { client, state } = useSession();
  const queries = useQueryClient();
  const [filter, setFilter] = useState<Filter>('confirmed,preparing');
  const [page, setPage] = useState(1);
  const [selected, setSelected] = useState<string | null>(ticketId ?? null);
  const [queueSelection, setQueueSelection] = useState<string | null>(null);
  const [focusedRow, setFocusedRow] = useState<string | null>(null);
  const navigation = usePageNavigation();
  const initialDetail = useRef(false);
  const [intent, setIntent] = useState<Intent | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const submitting = useRef(false);
  const key = ['preparation', state.user!.id];
  const queue = useQuery({
    queryKey: [...key, 'queue', filter, page],
    queryFn: async ({ signal }) => {
      const { data } = await client!.api.GET('/api/v1/preparation-tickets', {
        signal,
        params: { query: { status: filter, page, pageSize } },
      });
      return data!;
    },
  });
  const detail = useQuery({
    queryKey: [...key, 'ticket', selected],
    enabled: selected !== null,
    queryFn: async ({ signal }) => {
      const { data } = await client!.api.GET(
        '/api/v1/preparation-tickets/{id}',
        {
          signal,
          params: { path: { id: selected! } },
        },
      );
      return data!;
    },
  });
  const selectedRow = queue.isError
    ? undefined
    : queue.data?.items.find((row) => row.orderId === queueSelection);
  // Discard an obsolete choice before rendering the new authoritative results;
  // it must not silently become selected again if that record later reappears.
  if (
    queueSelection &&
    queue.isSuccess &&
    !queue.isFetching &&
    !queue.data.items.some((row) => row.orderId === queueSelection)
  )
    setQueueSelection(null);
  useEffect(() => {
    if (ticketId && !initialDetail.current) {
      initialDetail.current = true;
      navigation.openDetail('preparation-open');
    }
  }, [ticketId, navigation]);
  const mutation = useSafeMutation({
    mutationFn: async ({ id, action }: Intent) => {
      const path =
        action === 'start'
          ? '/api/v1/preparation-tickets/{id}/start'
          : '/api/v1/preparation-tickets/{id}/ready';
      await client!.api.POST(path, { params: { path: { id } } });
    },
    onSuccess: (_, command) => {
      setNotice(
        command.action === 'start'
          ? 'Preparation started. Confirmed by the service.'
          : 'Ticket is Ready. Confirmed by the service.',
      );
      if (command.action === 'ready') {
        setSelected(null);
        navigation.returnToList();
      }
    },
    // A transport failure can have an unknown outcome. Fetch server state before
    // offering another action; never replay the write or infer local success.
    onError: async (error) => {
      if (!(error instanceof ApiError && [403, 409].includes(error.status)))
        await queries.invalidateQueries({ queryKey: key });
    },
  });
  const ticket = detail.isError ? undefined : detail.data;
  const forbidden = [queue.error, detail.error, mutation.error].some(
    (error) => error instanceof ApiError && error.status === 403,
  );
  useEffect(() => {
    // A denied detail hides private content, but must reveal the route-level
    // Staff home recovery instead of leaving only the connection header.
    if (forbidden && navigation.detailActive) navigation.returnToList();
  }, [forbidden, navigation]);
  const busy = mutation.isPending;
  const resetMutation = mutation.reset;
  useEffect(() => {
    if (!selected) return;
    const subscription = BackHandler.addEventListener(
      'hardwareBackPress',
      () => {
        if (busy) return true;
        if (intent) {
          setIntent(null);
          navigation.focusTarget('preparation-state-action');
        } else {
          setSelected(null);
          setIntent(null);
          setNotice(null);
          resetMutation();
          navigation.returnToList();
        }
        return true;
      },
    );
    return () => subscription.remove();
  }, [busy, intent, navigation, resetMutation, selected]);
  const disabled =
    mutation.isWriteDisabled || busy || detail.isFetching || detail.isError;
  const confirm = async () => {
    if (
      !intent ||
      disabled ||
      submitting.current ||
      ticket?.status !== intent.status
    )
      return;
    submitting.current = true;
    setIntent(null);
    setNotice(null);
    try {
      await mutation.mutateAsync(intent);
    } catch {
      // The mutation exposes a safe error below, after refreshing server state.
    } finally {
      submitting.current = false;
    }
  };
  function clearFeedback() {
    setIntent(null);
    setNotice(null);
    mutation.reset();
  }

  return (
    <View style={styles.workspace}>
      {!selected && showHeading && <ShellHeading>Preparation</ShellHeading>}
      <ShellMutationFeedback mutation={mutation} />
      {forbidden ? (
        <ShellFeedback
          kind="error"
          label="Access unavailable. You do not have permission to prepare tickets."
        />
      ) : (
        <>
          {notice && <ShellFeedback kind="success" label={notice} />}
          {mutation.error && (
            <ShellFeedback
              kind="error"
              label={
                mutation.error instanceof ApiError &&
                mutation.error.status === 409
                  ? 'Ticket changed elsewhere. The latest server state has been requested; review it before trying again.'
                  : mutation.error instanceof ApiError
                    ? mutation.error.message
                    : 'No success was confirmed. Refresh the ticket to check its current state before trying again.'
              }
            />
          )}
          {selected ? (
            <>
              <ShellSelection>
                <ShellButton
                  label="Back to Preparation"
                  displayLabel="Preparation"
                  variant="secondary"
                  disabled={busy}
                  onPress={() => {
                    setSelected(null);
                    clearFeedback();
                    navigation.returnToList();
                  }}
                />
                <ShellButton
                  label="Refresh ticket"
                  variant="secondary"
                  loading={detail.isFetching}
                  disabled={busy || !mutation.canMutate}
                  onPress={() => {
                    setIntent(null);
                    void detail.refetch();
                  }}
                />
              </ShellSelection>
              {detail.isPending && (
                <ShellFeedback kind="loading" label="Loading ticket…" />
              )}
              {detail.isError && (
                <ShellFeedback
                  kind="error"
                  label={
                    detail.error instanceof ApiError &&
                    detail.error.status === 404
                      ? 'Ticket is no longer in the preparation queue. It may be ready or cancelled. Return to the queue or refresh.'
                      : detail.error.message
                  }
                />
              )}
              {ticket && (
                <>
                  <View style={styles.detailTitle}>
                    <ShellHeading focusOnMount>
                      {ticket.orderNumber}
                    </ShellHeading>
                    <TicketStatus status={ticket.status} />
                  </View>
                  <ShellText>
                    {location(ticket)} · {itemCount(ticket)} items
                  </ShellText>
                  <View style={styles.ledger}>
                    {ticket.lines.map((line, index) => (
                      <View key={index} style={styles.itemRow}>
                        <Text style={styles.quantity}>{line.quantity} ×</Text>
                        <View style={styles.itemDescription}>
                          <Text style={styles.itemName}>{line.itemName}</Text>
                          <View style={styles.itemMetadata}>
                            <Text style={styles.body}>{line.sizeName}</Text>
                            <Text style={styles.body} accessible={false}>
                              ·
                            </Text>
                            <Text
                              style={[
                                styles.body,
                                line.addons.length > 0 && styles.addons,
                              ]}
                            >
                              {line.addons.length
                                ? `Add-ons: ${line.addons.join(', ')}`
                                : 'No add-ons'}
                            </Text>
                          </View>
                        </View>
                      </View>
                    ))}
                  </View>
                  <ShellPageFooter>
                    <View
                      style={[
                        styles.actionDock,
                        intent && styles.confirmationDock,
                      ]}
                    >
                      {intent && intent.status === ticket.status ? (
                        <View style={styles.confirmation}>
                          <Text
                            accessibilityRole="alert"
                            accessibilityLiveRegion="polite"
                            style={styles.confirmationTitle}
                          >
                            {intent.action === 'start'
                              ? 'Start preparing this ticket?'
                              : 'Mark this ticket ready?'}
                          </Text>
                          {intent.action === 'ready' && (
                            <ShellText>
                              This updates the whole ticket.
                            </ShellText>
                          )}
                          <ShellButton
                            label={
                              intent.action === 'start'
                                ? 'Confirm start'
                                : 'Confirm ready'
                            }
                            disabled={disabled}
                            loading={busy}
                            prominent
                            onPress={() => void confirm()}
                          />
                          <ShellButton
                            label={
                              intent.action === 'ready'
                                ? 'Keep preparing'
                                : 'Keep current state'
                            }
                            variant="secondary"
                            disabled={busy}
                            onPress={() => {
                              setIntent(null);
                              navigation.focusTarget(
                                'preparation-state-action',
                              );
                            }}
                          />
                        </View>
                      ) : ticket.status === 'confirmed' ||
                        ticket.status === 'preparing' ? (
                        <ShellButton
                          label={
                            ticket.status === 'confirmed'
                              ? 'Start preparing'
                              : 'Mark ready'
                          }
                          disabled={disabled}
                          loading={busy}
                          prominent
                          loadingLabel="Sending…"
                          focusKey="preparation-state-action"
                          onPress={() => {
                            clearFeedback();
                            setIntent({
                              id: ticket.orderId,
                              status: ticket.status,
                              action:
                                ticket.status === 'confirmed'
                                  ? 'start'
                                  : 'ready',
                            });
                          }}
                        />
                      ) : null}
                    </View>
                  </ShellPageFooter>
                </>
              )}
            </>
          ) : (
            <>
              <View style={styles.filters}>
                {(
                  ['confirmed,preparing', 'confirmed', 'preparing'] as const
                ).map((value) => (
                  <View
                    key={value}
                    style={[
                      styles.filterOption,
                      fontScale > 1.5 && styles.largeFilterOption,
                    ]}
                  >
                    <ShellButton
                      key={value}
                      testID={`preparation-filter-${value === 'confirmed,preparing' ? 'active' : value}`}
                      label={
                        value === 'confirmed,preparing'
                          ? 'All active'
                          : value === 'confirmed'
                            ? 'Confirmed'
                            : 'Preparing'
                      }
                      variant="secondary"
                      selected={filter === value}
                      showSelectionMark={false}
                      compact
                      onPress={() => {
                        setFilter(value);
                        setPage(1);
                        clearFeedback();
                      }}
                    />
                  </View>
                ))}
              </View>
              {queue.isPending && (
                <ShellFeedback
                  kind="loading"
                  label="Loading preparation queue…"
                />
              )}
              {queue.isError && (
                <ShellFeedback kind="error" label={queue.error.message} />
              )}
              {!queue.isError && queue.data && (
                <>
                  {!queue.data.items.length && (
                    <ShellFeedback
                      kind="empty"
                      label="No tickets in this view. Refresh or choose another filter."
                    />
                  )}
                  <View style={styles.ledger}>
                    {queue.data.items.map((item) => (
                      <Pressable
                        key={item.orderId}
                        accessibilityRole="button"
                        accessibilityLabel={`Select ticket ${item.orderNumber}`}
                        accessibilityHint={`${item.status}. ${itemCount(item)} items. ${location(item)}. ${preview(item)}`}
                        accessibilityState={{
                          selected: queueSelection === item.orderId,
                        }}
                        onPress={() => {
                          setQueueSelection(item.orderId);
                          clearFeedback();
                        }}
                        onFocus={() => setFocusedRow(item.orderId)}
                        onBlur={() => setFocusedRow(null)}
                        style={({ pressed }) => [
                          styles.ticketRow,
                          queueSelection === item.orderId && styles.selectedRow,
                          pressed && styles.pressedRow,
                          focusedRow === item.orderId && styles.focusedRow,
                        ]}
                      >
                        <View style={styles.rowTitle}>
                          <Text style={styles.ticketNumber}>
                            {item.orderNumber}
                          </Text>
                          <TicketStatus status={item.status} />
                        </View>
                        <Text style={styles.metadata}>
                          {itemCount(item)} items · {location(item)}
                        </Text>
                        <Text style={styles.metadata}>{preview(item)}</Text>
                      </Pressable>
                    ))}
                  </View>
                </>
              )}
              <ShellPageFooter>
                <View style={styles.actionDock}>
                  <ShellButton
                    label="Refresh queue"
                    variant="text"
                    loading={queue.isFetching}
                    disabled={!mutation.canMutate}
                    onPress={() => void queue.refetch()}
                  />
                  <ShellButton
                    label={
                      selectedRow
                        ? `Open ${selectedRow.orderNumber}`
                        : 'Open selected ticket'
                    }
                    prominent
                    focusKey="preparation-open"
                    disabled={!selectedRow || queue.isFetching || queue.isError}
                    onPress={() => {
                      if (!selectedRow || queue.isFetching || queue.isError)
                        return;
                      navigation.openDetail('preparation-open');
                      setSelected(selectedRow.orderId);
                      clearFeedback();
                    }}
                  />
                </View>
              </ShellPageFooter>
              {!queue.isError && queue.data && (
                <>
                  <Text style={styles.metadata}>
                    Page {page} · {queue.data.totalCount} tickets
                  </Text>
                  {Number(queue.data.totalCount) > pageSize && (
                    <ShellSelection>
                      <ShellButton
                        label="Previous page"
                        variant="secondary"
                        disabled={page === 1 || queue.isFetching}
                        onPress={() => {
                          setPage(page - 1);
                        }}
                      />
                      <ShellButton
                        label="Next page"
                        variant="secondary"
                        disabled={
                          page * pageSize >= Number(queue.data.totalCount) ||
                          queue.isFetching
                        }
                        onPress={() => {
                          setPage(page + 1);
                        }}
                      />
                    </ShellSelection>
                  )}
                </>
              )}
            </>
          )}
        </>
      )}
    </View>
  );
}

const styles = StyleSheet.create({
  workspace: { gap: 16 },
  filters: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    gap: 6,
    marginBottom: 4,
  },
  filterOption: { flexGrow: 0 },
  largeFilterOption: { flexBasis: '100%' },
  actionDock: {
    backgroundColor: mobileTokens.color.background,
    borderTopWidth: 1,
    borderTopColor: mobileTokens.color.disabledBackground,
    paddingHorizontal: 24,
    paddingVertical: 12,
    gap: 8,
  },
  confirmationDock: { backgroundColor: mobileTokens.color.highlight },
  ledger: { borderTopWidth: 1, borderColor: mobileTokens.color.primary },
  ticketRow: {
    minHeight: 48,
    minWidth: 48,
    gap: 4,
    paddingVertical: 16,
    paddingHorizontal: 12,
    borderBottomWidth: 1,
    borderColor: mobileTokens.color.primary,
    borderLeftWidth: 4,
    borderLeftColor: 'transparent',
  },
  selectedRow: {
    borderLeftColor: mobileTokens.color.highlight,
  },
  pressedRow: { backgroundColor: mobileTokens.color.disabledBackground },
  focusedRow: { backgroundColor: mobileTokens.color.infoBackground },
  rowTitle: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    alignItems: 'center',
    justifyContent: 'space-between',
    gap: 8,
  },
  ticketNumber: {
    fontFamily: mobileTokens.type.displayFont,
    fontSize: 24,
    color: mobileTokens.color.foreground,
    flexShrink: 1,
  },
  status: {
    fontFamily: mobileTokens.type.bodyFont,
    fontSize: 14,
    color: mobileTokens.color.foreground,
    backgroundColor: mobileTokens.color.highlight,
    borderRadius: 8,
    paddingHorizontal: 8,
    paddingVertical: 4,
    overflow: 'hidden',
  },
  preparing: {
    color: mobileTokens.color.info,
    backgroundColor: mobileTokens.color.infoBackground,
  },
  metadata: {
    fontFamily: mobileTokens.type.bodyFont,
    fontSize: 14,
    color: mobileTokens.color.foreground,
  },
  detailTitle: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    alignItems: 'center',
    justifyContent: 'space-between',
    gap: 8,
  },
  itemRow: {
    flexDirection: 'row',
    gap: 16,
    paddingVertical: 16,
    borderBottomWidth: 1,
    borderColor: mobileTokens.color.primary,
  },
  quantity: {
    fontFamily: mobileTokens.type.bodyFont,
    fontSize: 24,
    color: mobileTokens.color.foreground,
    flexShrink: 1,
  },
  itemDescription: { flex: 1, gap: 4 },
  itemMetadata: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    alignItems: 'center',
    gap: 4,
  },
  itemName: {
    fontFamily: mobileTokens.type.bodyFont,
    fontSize: 24,
    color: mobileTokens.color.foreground,
  },
  body: {
    fontFamily: mobileTokens.type.bodyFont,
    fontSize: 18,
    color: mobileTokens.color.foreground,
  },
  addons: {
    backgroundColor: mobileTokens.color.highlight,
    borderRadius: 8,
    paddingHorizontal: 4,
  },
  confirmation: {
    gap: 8,
  },
  confirmationTitle: {
    fontFamily: mobileTokens.type.displayFont,
    fontSize: 24,
    color: mobileTokens.color.foreground,
  },
});
