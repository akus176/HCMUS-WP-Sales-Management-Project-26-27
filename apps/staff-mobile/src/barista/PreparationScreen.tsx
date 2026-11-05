import { useRef, useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { StyleSheet, View } from 'react-native';

import { ApiError } from '../api/errors';
import { useSafeMutation } from '../connectivity/connectivity';
import { useSession } from '../session/context';
import {
  ShellButton,
  ShellFeedback,
  ShellHeading,
  ShellText,
} from '../shell/controls';

const pageSize = 10;
type Filter = 'confirmed,preparing' | 'confirmed' | 'preparing';
type Intent = { id: string; action: 'start' | 'ready'; status: string };

export function PreparationScreen() {
  const { client, state } = useSession();
  const queries = useQueryClient();
  const [filter, setFilter] = useState<Filter>('confirmed,preparing');
  const [page, setPage] = useState(1);
  const [selected, setSelected] = useState<string | null>(null);
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
      if (command.action === 'ready') setSelected(null);
    },
    // A transport failure can have an unknown outcome. Fetch server state before
    // offering another action; never replay the write or infer local success.
    onError: async (error) => {
      if (!(error instanceof ApiError && error.status === 409))
        await queries.invalidateQueries({ queryKey: key });
    },
  });
  const ticket = detail.isError ? undefined : detail.data;
  const forbidden = [queue.error, detail.error, mutation.error].some(
    (error) => error instanceof ApiError && error.status === 403,
  );
  const busy = mutation.isPending;
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
    <>
      <ShellHeading>Preparation</ShellHeading>
      {forbidden ? (
        <ShellFeedback
          kind="error"
          label="Access unavailable. You do not have permission to prepare tickets."
        />
      ) : (
        <>
          {notice && <ShellFeedback kind="info" label={notice} />}
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
              <ShellButton
                label="Back to queue"
                variant="secondary"
                disabled={busy}
                onPress={() => {
                  setSelected(null);
                  clearFeedback();
                }}
              />
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
              <ShellButton
                label="Refresh ticket"
                variant="secondary"
                loading={detail.isFetching}
                disabled={busy || mutation.isWriteDisabled}
                onPress={() => {
                  setIntent(null);
                  void detail.refetch();
                }}
              />
              {ticket && (
                <>
                  <ShellHeading>Ticket {ticket.orderNumber}</ShellHeading>
                  <ShellText>
                    Status:{' '}
                    {ticket.status === 'confirmed'
                      ? 'Confirmed'
                      : ticket.status === 'preparing'
                        ? 'Preparing'
                        : ticket.status}
                  </ShellText>
                  <ShellText>
                    {ticket.tableNumber
                      ? `Table ${ticket.tableNumber}`
                      : 'No table — takeaway or delivery'}
                  </ShellText>
                  {ticket.lines.map((line, index) => (
                    <View key={index} style={styles.line}>
                      <ShellText>
                        {line.quantity} × {line.itemName} · {line.sizeName}
                      </ShellText>
                      <ShellText>
                        {line.addons.length
                          ? `Add-ons: ${line.addons.join(', ')}`
                          : 'No add-ons'}
                      </ShellText>
                    </View>
                  ))}
                  {intent && intent.status === ticket.status ? (
                    <>
                      <ShellFeedback
                        kind="warning"
                        label={
                          intent.action === 'start'
                            ? 'Start preparing this ticket?'
                            : 'Mark this ticket ready?'
                        }
                      />
                      <ShellButton
                        label={
                          intent.action === 'start'
                            ? 'Confirm start'
                            : 'Confirm ready'
                        }
                        disabled={disabled}
                        loading={busy}
                        onPress={() => void confirm()}
                      />
                      <ShellButton
                        label="Keep current state"
                        variant="secondary"
                        disabled={busy}
                        onPress={() => setIntent(null)}
                      />
                    </>
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
                      loadingLabel="Sending…"
                      onPress={() => {
                        clearFeedback();
                        setIntent({
                          id: ticket.orderId,
                          status: ticket.status,
                          action:
                            ticket.status === 'confirmed' ? 'start' : 'ready',
                        });
                      }}
                    />
                  ) : null}
                </>
              )}
            </>
          ) : (
            <>
              <ShellText>
                Confirmed and preparing tickets. Refresh for current service
                state.
              </ShellText>
              <View style={styles.line}>
                {(
                  ['confirmed,preparing', 'confirmed', 'preparing'] as const
                ).map((value) => (
                  <ShellButton
                    key={value}
                    label={
                      value === 'confirmed,preparing'
                        ? 'All active'
                        : value === 'confirmed'
                          ? 'Confirmed'
                          : 'Preparing'
                    }
                    variant="secondary"
                    selected={filter === value}
                    onPress={() => {
                      setFilter(value);
                      setPage(1);
                      clearFeedback();
                    }}
                  />
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
              <ShellButton
                label="Refresh queue"
                variant="secondary"
                loading={queue.isFetching}
                disabled={mutation.isWriteDisabled}
                onPress={() => void queue.refetch()}
              />
              {!queue.isError && queue.data && (
                <>
                  {!queue.data.items.length && (
                    <ShellFeedback
                      kind="empty"
                      label="No tickets in this view. Refresh or choose another filter."
                    />
                  )}
                  {queue.data.items.map((item) => (
                    <View key={item.orderId} style={styles.line}>
                      <ShellText>
                        {item.orderNumber} ·{' '}
                        {item.status === 'confirmed'
                          ? 'Confirmed'
                          : 'Preparing'}
                      </ShellText>
                      <ShellText>
                        {item.lines.reduce(
                          (sum, line) => sum + Number(line.quantity),
                          0,
                        )}{' '}
                        items
                        {item.tableNumber ? ` · Table ${item.tableNumber}` : ''}
                      </ShellText>
                      <ShellButton
                        label={`Open ticket ${item.orderNumber}`}
                        variant="secondary"
                        onPress={() => {
                          setSelected(item.orderId);
                          clearFeedback();
                        }}
                      />
                    </View>
                  ))}
                  <ShellText>
                    Page {page} · {queue.data.totalCount} tickets
                  </ShellText>
                  <ShellButton
                    label="Previous page"
                    variant="secondary"
                    disabled={page === 1 || queue.isFetching}
                    onPress={() => setPage(page - 1)}
                  />
                  <ShellButton
                    label="Next page"
                    variant="secondary"
                    disabled={
                      page * pageSize >= Number(queue.data.totalCount) ||
                      queue.isFetching
                    }
                    onPress={() => setPage(page + 1)}
                  />
                </>
              )}
            </>
          )}
        </>
      )}
    </>
  );
}

const styles = StyleSheet.create({ line: { gap: 8, marginVertical: 8 } });
