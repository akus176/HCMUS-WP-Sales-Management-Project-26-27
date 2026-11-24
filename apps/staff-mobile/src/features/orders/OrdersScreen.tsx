import { Activity, useEffect, useState } from 'react';
import { BackHandler, StyleSheet, Text, View } from 'react-native';
import { useQuery, useQueryClient } from '@tanstack/react-query';

import { ApiError } from '../../api/errors';
import { useConnectivity } from '../../connectivity/connectivity';
import { useSession } from '../../session/context';
import type { StaffSession } from '../../session/session';
import {
  ShellButton,
  ShellHeaderAction,
  ShellPageHeader,
  ShellFeedback,
  ShellHeading,
  ShellText,
  ShellInput,
  ShellSelection,
  usePageNavigation,
  ShellBadge,
  ShellSheet,
  ShellPinnedHeader,
} from '../../shell/controls';
import { useNativeNavigation } from '../../shell/NativeNavigation';
import { ShellIcon } from '../../shell/ShellIcon';
import { mobileTokens as t } from '../../shell/designTokens';
import { OrderPayment, type PendingPayments } from './OrderPayment';
import { ManagerOrderActions } from './ManagerOrderActions';
import { orderStyles, money } from './presentation';

import { CounterSale } from '../counter/CounterSale';
import {
  CounterDraftActions,
  type PendingCounterActions,
} from '../counter/CounterDraftActions';

const orderBatchSize = 10;

export function OrdersScreen({
  user,
  manager = false,
  showHeading = true,
  startWithMenu = false,
}: {
  user: StaffSession;
  manager?: boolean;
  showHeading?: boolean;
  startWithMenu?: boolean;
}) {
  const { client } = useSession();
  const native = useNativeNavigation();
  const queries = useQueryClient();
  const { canMutate } = useConnectivity();
  const [section, setSection] = useState<'new' | 'orders' | 'tables'>(
    startWithMenu && !manager ? 'new' : 'orders',
  );
  const [search, setSearch] = useState('');
  const [filter, setFilter] = useState('all');
  const [visibleCount, setVisibleCount] = useState(orderBatchSize);
  const [filtersExpanded, setFiltersExpanded] = useState(false);
  const navigation = usePageNavigation();
  const [selected, setSelected] = useState<string | null>(null);
  const [accessLost, setAccessLost] = useState(false);
  useEffect(() => {
    if (!selected && !filtersExpanded) return;
    const handler = BackHandler.addEventListener('hardwareBackPress', () => {
      if (filtersExpanded) setFiltersExpanded(false);
      else {
        setSelected(null);
        navigation.returnToList();
      }
      return true;
    });
    return () => handler.remove();
  }, [selected, filtersExpanded, navigation]);
  const [pendingPayments] = useState<PendingPayments>(() => new Map());
  const [pendingActions] = useState<PendingCounterActions>(() => new Map());
  const canRead = user.permissions.includes('orders.create');
  const key = ['sales', user.id];
  const orders = useQuery({
    queryKey: [...key, 'orders'],
    enabled:
      canRead &&
      !accessLost &&
      (!startWithMenu || section === 'orders' || !!selected),
    queryFn: async ({ signal }) => {
      const { data } = await client!.api.GET('/api/v1/orders', { signal });
      return data!;
    },
  });
  const tables = useQuery({
    queryKey: [...key, 'tables'],
    enabled:
      canRead &&
      !manager &&
      !accessLost &&
      (!startWithMenu || section === 'tables'),
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
      {showHeading && (!native?.user || manager) && (
        <ShellPageHeader
          title={manager ? 'Manager order operations' : 'Counter'}
        />
      )}
      {!manager && !selected && !navigation.detailActive && (
        <ShellPinnedHeader enabled={!!native?.user}>
          <ShellSelection segmented>
            {(startWithMenu
              ? (['new', 'orders', 'tables'] as const)
              : (['orders', 'tables'] as const)
            ).map((choice) => (
              <ShellButton
                key={choice}
                testID={`order-section-${choice}`}
                label={
                  choice === 'new'
                    ? 'New sale'
                    : choice === 'orders'
                      ? 'Orders'
                      : 'Tables'
                }
                selected={section === choice}
                variant="secondary"
                compact
                onPress={() => {
                  setSection(choice);
                  navigation.scrollToTop();
                }}
              />
            ))}
          </ShellSelection>
        </ShellPinnedHeader>
      )}
      {(!startWithMenu || section !== 'new' || !!selected || manager) && (
        <ShellHeaderAction
          kind="refresh"
          label="Refresh orders and tables"
          disabled={!canMutate}
          loading={fetching}
          onPress={() => {
            void queries.invalidateQueries({ queryKey: key });
          }}
        />
      )}
      {startWithMenu && !manager && (
        <Activity mode={section === 'new' && !selected ? 'visible' : 'hidden'}>
          <CounterSale
            user={user}
            pendingPayments={pendingPayments}
            pendingActions={pendingActions}
            onAccessLost={() => setAccessLost(true)}
          />
        </Activity>
      )}
      {selected ? (
        <>
          <ShellPageHeader title="Order details" priority={1} />
          <ShellHeaderAction
            kind="back"
            label={
              !manager && section === 'tables'
                ? 'Back to table occupancy'
                : 'Back to order search'
            }
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
              <View
                style={orderStyles.card}
                accessible
                accessibilityRole="text"
                accessibilityLabel={`Total: ${money(current.total)} · Paid: ${money(order.paidAmount)} · Balance: ${money(Math.max(0, Number(current.total) - Number(order.paidAmount)))}`}
              >
                <View style={styles.row}>
                  <Text style={styles.caption}>Order total</Text>
                  <Text style={styles.amount}>{money(current.total)}</Text>
                </View>
                <View style={styles.row}>
                  <Text style={styles.caption}>Paid</Text>
                  <Text style={styles.amount}>{money(order.paidAmount)}</Text>
                </View>
                <View style={styles.row}>
                  <Text style={styles.title}>Balance due</Text>
                  <Text style={styles.balance}>
                    {money(
                      Math.max(
                        0,
                        Number(current.total) - Number(order.paidAmount),
                      ),
                    )}
                  </Text>
                </View>
              </View>
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
              {startWithMenu && !manager && (
                <CounterDraftActions
                  key={`counter:${current.id}`}
                  user={user}
                  order={current}
                  disabled={disabled}
                  pendingActions={pendingActions}
                  onAccessLost={() => setAccessLost(true)}
                />
              )}
              {!manager &&
                user.permissions.includes('payments.record') &&
                (!['draft', 'cancelled', 'completed'].includes(
                  current.status,
                ) ||
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
              {user.permissions.some((permission) =>
                ['orders.cancel', 'delivery.assign'].includes(permission),
              ) && (
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
              <ShellPinnedHeader
                slot="search"
                enabled={!!native?.user && !manager}
              >
                <View style={styles.toolbar}>
                  <View style={styles.search}>
                    <ShellInput
                      label="Search by order number or table"
                      accessibilityLabel="Search orders"
                      placeholder="Search orders or tables"
                      hideLabel
                      value={search}
                      onChangeText={(value) => {
                        setSearch(value);
                        setVisibleCount(orderBatchSize);
                      }}
                      autoCorrect={false}
                      autoCapitalize="none"
                    />
                  </View>
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
                  >
                    <ShellIcon name="filter" />
                  </ShellButton>
                </View>
                {filter !== 'all' && (
                  <ShellBadge
                    label={
                      filter === 'ready_delivery'
                        ? 'Ready unassigned deliveries'
                        : filter[0]!.toUpperCase() + filter.slice(1)
                    }
                  />
                )}
              </ShellPinnedHeader>
              {filtersExpanded && (
                <ShellSheet
                  visible
                  title="Filter orders"
                  onClose={() => {
                    setFiltersExpanded(false);
                    navigation.returnToList();
                  }}
                >
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
                </ShellSheet>
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
                    <ShellButton
                      key={row.id}
                      label={`Open order ${row.orderNumber}`}
                      variant="text"
                      focusKey={`order:${row.id}`}
                      onPress={() => {
                        navigation.openDetail(`order:${row.id}`);
                        setSelected(row.id);
                      }}
                    >
                      <View style={styles.row}>
                        <Text style={styles.title}>
                          {row.orderNumber}
                          {row.tableNumber ? ` · Table ${row.tableNumber}` : ''}
                        </Text>
                        <ShellIcon
                          name="chevron"
                          size={16}
                          color={t.color.disabledText}
                        />
                      </View>
                      <Text style={styles.caption}>
                        {row.channel.replaceAll('_', '-')}
                      </Text>
                      <View style={styles.row}>
                        <Text style={styles.amount}>
                          {Number(row.total) > Number(row.paidAmount)
                            ? `Unpaid · ${money(Math.max(0, Number(row.total) - Number(row.paidAmount)))}`
                            : `Paid · ${money(row.paidAmount)}`}
                        </Text>
                        <ShellBadge
                          label={
                            row.status[0]!.toUpperCase() + row.status.slice(1)
                          }
                          kind={
                            row.status === 'preparing'
                              ? 'info'
                              : row.status === 'ready'
                                ? 'attention'
                                : 'neutral'
                          }
                        />
                      </View>
                    </ShellButton>
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

const styles = StyleSheet.create({
  toolbar: { flexDirection: 'row', alignItems: 'center', gap: 8 },
  search: { flex: 1 },
  row: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    alignItems: 'center',
    justifyContent: 'space-between',
    gap: 8,
  },
  title: {
    fontFamily: t.type.mediumFont,
    fontSize: 16,
    lineHeight: 23,
    color: t.color.foreground,
    flexShrink: 1,
  },
  caption: {
    fontFamily: t.type.bodyFont,
    fontSize: 14,
    lineHeight: 21,
    color: t.color.disabledText,
  },
  amount: {
    fontFamily: t.type.mediumFont,
    fontSize: 14,
    lineHeight: 21,
    fontVariant: ['tabular-nums'],
    color: t.color.primary,
  },
  balance: {
    fontFamily: t.type.displayFont,
    fontSize: 18,
    lineHeight: 25,
    fontVariant: ['tabular-nums'],
    color: t.color.primary,
  },
});
