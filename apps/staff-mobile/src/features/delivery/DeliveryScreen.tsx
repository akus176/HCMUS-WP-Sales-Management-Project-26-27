import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { router } from 'expo-router';
import { StyleSheet, View } from 'react-native';
import { recordRow } from '../../shell/recordPresentation';

import { useSession } from '../../session/context';
import {
  ShellButton,
  ShellFeedback,
  ShellHeading,
  ShellText,
  ShellSelection,
} from '../../shell/controls';

const pageSize = 10;

export function DeliveryScreen({
  showHeading = true,
}: { showHeading?: boolean } = {}) {
  const { client, state } = useSession();
  const [view, setView] = useState<'active' | 'completed'>('active');
  const [page, setPage] = useState(1);
  const assignments = useQuery({
    queryKey: ['delivery', state.user!.id, 'list', view, page],
    queryFn: async ({ signal }) => {
      const { data } = await client!.api.GET('/api/v1/courier/assignments', {
        signal,
        params: {
          query: {
            status:
              view === 'active'
                ? 'assigned,out_for_delivery'
                : 'delivered,failed',
            page,
            pageSize,
          },
        },
      });
      return data!;
    },
  });
  return (
    <>
      {showHeading && <ShellHeading>Deliveries</ShellHeading>}
      <ShellSelection>
        {(['active', 'completed'] as const).map((choice) => (
          <ShellButton
            key={choice}
            label={
              choice === 'active' ? 'Active deliveries' : 'Completed deliveries'
            }
            variant="secondary"
            compact
            selected={view === choice}
            onPress={() => {
              setPage(1);
              setView(choice);
            }}
          />
        ))}
      </ShellSelection>
      {assignments.isPending && (
        <ShellFeedback kind="loading" label="Loading deliveries…" />
      )}
      {assignments.isError && (
        <ShellFeedback kind="error" label={assignments.error.message} />
      )}
      <ShellButton
        label="Refresh deliveries"
        variant="secondary"
        loading={assignments.isFetching}
        onPress={() => void assignments.refetch()}
      />
      {!assignments.isError && assignments.data && (
        <>
          {!assignments.data.items.length && (
            <ShellFeedback kind="empty" label="No deliveries in this view." />
          )}
          {assignments.data.items.map((item) => (
            <View key={item.orderId} style={styles.group}>
              <ShellText>
                {item.orderNumber} · {item.deliveryStatus.replaceAll('_', ' ')}
              </ShellText>
              {view === 'active' && (
                <ShellSelection>
                  <ShellButton
                    label={`Open delivery ${item.orderNumber}`}
                    displayLabel="Open delivery"
                    variant="link"
                    onPress={() =>
                      router.push({
                        pathname: '/modules/delivery/[orderId]',
                        params: { orderId: item.orderId },
                      })
                    }
                  />
                </ShellSelection>
              )}
            </View>
          ))}
          <ShellText>
            Page {page} · {assignments.data.totalCount} deliveries
          </ShellText>
          <ShellButton
            label="Previous page"
            variant="secondary"
            disabled={page === 1 || assignments.isFetching}
            onPress={() => setPage(page - 1)}
          />
          <ShellButton
            label="Next page"
            variant="secondary"
            disabled={
              page * pageSize >= Number(assignments.data.totalCount) ||
              assignments.isFetching
            }
            onPress={() => setPage(page + 1)}
          />
        </>
      )}
    </>
  );
}

const styles = StyleSheet.create({ group: recordRow });
