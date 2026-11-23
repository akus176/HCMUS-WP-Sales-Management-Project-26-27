import { useState } from 'react';
import { View } from 'react-native';
import { useQuery, useQueryClient } from '@tanstack/react-query';

import { ApiError } from '../../api/errors';
import { useConnectivity } from '../../connectivity/connectivity';
import { useSession } from '../../session/context';
import type { StaffSession } from '../../session/session';
import {
  ShellButton,
  ShellFeedback,
  ShellHeading,
  ShellText,
  ShellInput,
  ShellSelection,
  usePageNavigation,
} from '../../shell/controls';
import { OrderPayment, type PendingPayments } from './OrderPayment';
import { ManagerOrderActions } from './ManagerOrderActions';
import { orderStyles, money } from './presentation';

const orderBatchSize = 10;

export function OrdersScreen({
  user,
  manager = false,
  showHeading = true,
}: {
  user: StaffSession;
  manager?: boolean;
  showHeading?: boolean;
}) {
  const { client } = useSession();
  const queries = useQueryClient();
  const { canMutate } = useConnectivity();
  const [section, setSection] = useState<'orders' | 'tables'>('orders');
  const [search, setSearch] = useState('');
  const [filter, setFilter] = useState('all');
  const [visibleCount, setVisibleCount] = useState(orderBatchSize);
  const [filtersExpanded, setFiltersExpanded] = useState(false);
  const navigation = usePageNavigation();
  const [selected, setSelected] = useState<string | null>(null);
  const [accessLost, setAccessLost] = useState(false);
  const [pendingPayments] = useState<PendingPayments>(() => new Map());
  const canRead = user.permissions.includes('orders.create');
  const key = ['sales', user.id];
  const orders = useQuery({
    queryKey: [...key, 'orders'],
    enabled: canRead && !accessLost,
    queryFn: async ({ signal }) => {
      const { data } = await client!.api.GET('/api/v1/orders', { signal });
      return data!;
    },
  });
  const tables = useQuery({
    queryKey: [...key, 'tables'],
    enabled: canRead && !manager && !accessLost,
    queryFn: async ({ signal }) => {
      const { data } = await client!.api.GET('/api/v1/pos/tables', { signal });
      return data!;
    },
  });
  const detail = useQuery({
    queryKey: [...key, 'order', selected],
    enabled: canRead && selected !== null && !accessLost,
    queryFn: async ({ signal }) => {
      const { data } = await client!.api.GET('/api/v1/orders/{id}', {
        params: { path: { id: selected! } },
        signal,
      });
      return data!;
    },
  });
  const forbidden =
    accessLost ||
    [orders.error, tables.error, detail.error].some(
      (error) => error instanceof ApiError && error.status === 403,
    );
  const order =
    !orders.isError && orders.data?.find((row) => row.id === selected);
  const current = !detail.isError && detail.data;
  const fetching = orders.isFetching || tables.isFetching || detail.isFetching;
  const disabled =
    !canMutate ||
    fetching ||
    orders.isError ||
    detail.isError ||
    !order ||
    !current ||
    order.status !== current.status;
  const rows = orders.data?.filter(
    (row) =>
      `${row.orderNumber} ${row.tableNumber ?? ''}`
        .toLowerCase()
        .includes(search.trim().toLowerCase()) &&
      (filter === 'all' ||
        (filter === 'ready_delivery'
          ? row.channel === 'delivery' &&
            row.status === 'ready' &&
            row.deliveryStatus === 'unassigned'
          : row.status === filter)),
  );

  if (!canRead || forbidden)
    return (
      <ShellFeedback
        kind="error"
        label="Order access unavailable. Return to staff home and refresh permissions."
      />
    );

  return (
    <>
      {showHeading && (
        <ShellHeading>
          {manager ? 'Manager order operations' : 'Counter'}
        </ShellHeading>
      )}
      {!manager && !selected && (
        <ShellSelection>
          {(['orders', 'tables'] as const).map((choice) => (
            <ShellButton
              key={choice}
              label={choice === 'orders' ? 'Orders' : 'Tables'}
              selected={section === choice}
              variant="secondary"
              compact
              onPress={() => setSection(choice)}
            />
          ))}
        </ShellSelection>
      )}
      <ShellButton
        label="Refresh orders and tables"
        variant="secondary"
        disabled={!canMutate}
        loading={fetching}
        onPress={() => {
          void queries.invalidateQueries({ queryKey: key });
        }}
      />
      {selected ? (
        <>
          <ShellButton
            label={
              !manager && section === 'tables'
                ? 'Back to table occupancy'
                : 'Back to order search'
            }
            variant="text"
            onPress={() => {
              setSelected(null);
              navigation.returnToList();
            }}
          />
          {detail.isPending && (
            <ShellFeedback kind="loading" label="Loading order…" />
          )}
          {(detail.isError || orders.isError) && (
            <ShellFeedback
              kind="error"
              label={
                detail.error instanceof ApiError && detail.error.status === 404
                  ? 'Order is no longer available.'
                  : 'Order could not be refreshed. Retry before taking action.'
              }
            />
          )}
          {order && current && (
            <>
              <ShellHeading
                key={`heading:${current.id}`}
                level="section"
                focusOnMount
              >
                Order {current.orderNumber}
              </ShellHeading>
              <ShellText>{`Status: ${current.status} · ${current.channel}${current.tableNumber ? ` · Table ${current.tableNumber}` : ''}`}</ShellText>
              <ShellText>{`Fulfillment: ${order.deliveryStatus ?? current.status}${order.assignedStaffId ? ` · Assigned staff: ${order.assignedStaffId}` : ''}`}</ShellText>
              <ShellText>{`Total: ${money(current.total)} · Paid: ${money(order.paidAmount)} · Balance: ${money(Math.max(0, Number(current.total) - Number(order.paidAmount)))}`}</ShellText>
              {current.lines.map((line, index) => (
                <View key={index} style={orderStyles.card}>
                  <ShellText>{`${line.quantity} × ${line.itemName} · ${line.sizeName} · ${money(line.total)}`}</ShellText>
                  <ShellText>
                    {line.addons.length
                      ? `Add-ons: ${line.addons.join(', ')}`
                      : 'No add-ons'}
                  </ShellText>
                </View>
              ))}
              {!manager &&
                user.permissions.includes('payments.record') &&
                (!['cancelled', 'completed'].includes(current.status) ||
                  pendingPayments.has(current.id)) && (
                  <OrderPayment
                    key={`payment:${current.id}`}
                    orderId={current.id}
                    pendingPayments={pendingPayments}
                    balance={Math.max(
                      0,
                      Number(current.total) - Number(order.paidAmount),
                    )}
                    disabled={disabled}
                    onAccessLost={() => setAccessLost(true)}
                  />
                )}
              {manager && (
                <ManagerOrderActions
                  key={`management:${current.id}`}
                  order={order}
                  permissions={user.permissions}
                  disabled={disabled}
                  onAccessLost={() => setAccessLost(true)}
                />
              )}
            </>
          )}
          {!orders.isPending && !orders.isError && !order && (
            <ShellFeedback
              kind="empty"
              label="Order is no longer in the server list. Return to search or refresh."
            />
          )}
        </>
      ) : (
        <>
          {(manager || section === 'orders') && (
            <>
              <ShellInput
                label="Search by order number or table"
                accessibilityLabel="Search orders"
                value={search}
                onChangeText={(value) => {
                  setSearch(value);
                  setVisibleCount(orderBatchSize);
                }}
                autoCorrect={false}
                autoCapitalize="none"
              />
              <ShellButton
                label={`Filter orders: ${filter === 'ready_delivery' ? 'Ready unassigned deliveries' : filter[0]!.toUpperCase() + filter.slice(1)}`}
                expanded={filtersExpanded}
                variant="secondary"
                focusKey="order-filter"
                onPress={() => {
                  if (filtersExpanded) navigation.returnToList();
                  else navigation.rememberPosition('order-filter');
                  setFiltersExpanded(!filtersExpanded);
                }}
              />
              {filtersExpanded && (
                <ShellSelection>
                  {[
                    'all',
                    'draft',
                    'confirmed',
                    'preparing',
                    'ready',
                    'completed',
                    'cancelled',
                    ...(manager ? ['ready_delivery'] : []),
                  ].map((value) => (
                    <ShellButton
                      key={value}
                      label={`Show ${value === 'ready_delivery' ? 'ready unassigned deliveries' : value} orders`}
                      selected={filter === value}
                      variant="secondary"
                      onPress={() => {
                        setFilter(value);
                        setVisibleCount(orderBatchSize);
                        setFiltersExpanded(false);
                        navigation.returnToList();
                      }}
                    />
                  ))}
                </ShellSelection>
              )}
              {orders.isPending && (
                <ShellFeedback kind="loading" label="Loading orders…" />
              )}
              {orders.isError ? (
                <ShellFeedback
                  kind="error"
                  label="Orders could not be loaded. Refresh to retry."
                />
              ) : (
                <>
                  {rows?.length === 0 && (
                    <ShellFeedback
                      kind="empty"
                      label="No orders match this search."
                    />
                  )}
                  {rows?.slice(0, visibleCount).map((row) => (
                    <View key={row.id} style={orderStyles.card}>
                      <ShellText>{`${row.orderNumber} · ${row.status} · ${row.channel}${row.tableNumber ? ` · Table ${row.tableNumber}` : ''}`}</ShellText>
                      <ShellText>{`Fulfillment: ${row.deliveryStatus ?? row.status} · Paid: ${money(row.paidAmount)} · Balance: ${money(Math.max(0, Number(row.total) - Number(row.paidAmount)))}`}</ShellText>
                      <ShellSelection>
                        <ShellButton
                          label={`Open order ${row.orderNumber}`}
                          displayLabel="Open order"
                          variant="link"
                          focusKey={`order:${row.id}`}
                          onPress={() => {
                            navigation.openDetail(`order:${row.id}`);
                            setSelected(row.id);
                          }}
                        />
                      </ShellSelection>
                    </View>
                  ))}
                  {rows && rows.length > orderBatchSize && (
                    <>
                      <ShellText>
                        Showing {Math.min(visibleCount, rows.length)} of{' '}
                        {rows.length} orders
                      </ShellText>
                      {visibleCount < rows.length && (
                        <ShellButton
                          label="Show more orders"
                          variant="secondary"
                          onPress={() => {
                            const next = rows[visibleCount];
                            setVisibleCount((count) => count + orderBatchSize);
                            if (next)
                              navigation.focusTarget(`order:${next.id}`);
                          }}
                        />
                      )}
                    </>
                  )}
                </>
              )}
            </>
          )}
          {!manager && section === 'tables' && (
            <>
              <ShellHeading level="section">Table occupancy</ShellHeading>
              {tables.isPending && (
                <ShellFeedback kind="loading" label="Loading tables…" />
              )}
              {tables.isError ? (
                <ShellFeedback
                  kind="error"
                  label="Tables could not be loaded. Refresh to retry."
                />
              ) : (
                <>
                  {tables.data?.length === 0 && (
                    <ShellFeedback
                      kind="empty"
                      label="No tables are configured."
                    />
                  )}
                  {tables.data?.map((table) => (
                    <View key={table.id} style={orderStyles.card}>
                      <ShellText>{`Table ${table.tableNumber} · ${table.seats ?? 'Unspecified'} seats · ${table.isOccupied ? 'Occupied' : 'Available'}`}</ShellText>
                      {table.activeOrderId && (
                        <ShellSelection>
                          <ShellButton
                            label={`Open table ${table.tableNumber} order`}
                            displayLabel="Open order"
                            variant="link"
                            focusKey={`table:${table.id}`}
                            onPress={() => {
                              navigation.openDetail(`table:${table.id}`);
                              setSelected(table.activeOrderId);
                            }}
                          />
                        </ShellSelection>
                      )}
                    </View>
                  ))}
                </>
              )}
            </>
          )}
        </>
      )}
    </>
  );
}
