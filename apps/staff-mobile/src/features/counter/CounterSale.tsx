import { useEffect, useRef, useState } from 'react';
import {
  BackHandler,
  Image,
  ScrollView,
  StyleSheet,
  Text,
  View,
  useWindowDimensions,
} from 'react-native';
import { randomUUID } from 'expo-crypto';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useSession } from '../../session/context';
import type { StaffSession } from '../../session/session';
import { useSafeMutation } from '../../connectivity/connectivity';
import { ApiError } from '../../api/errors';
import {
  ShellButton,
  ShellFeedback,
  ShellHeaderAction,
  ShellHeading,
  ShellInput,
  ShellMutationFeedback,
  ShellPageFooter,
  ShellPageHeader,
  ShellPinnedHeader,
  ShellSelection,
  ShellSheet,
  ShellText,
  usePageNavigation,
} from '../../shell/controls';
import { useNativeNavigation } from '../../shell/NativeNavigation';
import { ShellIcon } from '../../shell/ShellIcon';
import { mobileTokens as t } from '../../shell/designTokens';
import { OrderPayment, type PendingPayments } from '../orders/OrderPayment';
import { money } from '../orders/presentation';
import { CounterCustomerPicker } from './CounterCustomerPicker';
import {
  CounterDraftActions,
  counterActionResolved,
  type PendingCounterActions,
} from './CounterDraftActions';
import {
  counterError,
  createTicket,
  emptyService,
  lineEstimate,
  uncertainOutcome,
  type CartLine,
  type Customer,
  type MenuItem,
  type ServiceForm,
} from './counterModel';
import { menuPhoto } from './menuPhotos';

export function CounterSale({
  user,
  pendingPayments,
  pendingActions,
  onAccessLost,
}: {
  user: StaffSession;
  pendingPayments: PendingPayments;
  pendingActions: PendingCounterActions;
  onAccessLost: () => void;
}) {
  const { client } = useSession();
  const native = useNativeNavigation();
  const navigation = usePageNavigation();
  const queries = useQueryClient();
  const { fontScale, width } = useWindowDimensions();
  const columnCount = fontScale > 1.5 || width < 360 ? 1 : 2;
  const [search, setSearch] = useState('');
  const [submittedSearch, setSubmittedSearch] = useState('');
  const [menuCount, setMenuCount] = useState(20);
  const [category, setCategory] = useState<string>();
  const [cart, setCart] = useState<CartLine[]>([]);
  const [form, setForm] = useState<ServiceForm>(emptyService);
  const [view, setView] = useState<'menu' | 'ticket'>('menu');
  const [ticketId, setTicketId] = useState(randomUUID);
  const [saved, setSaved] = useState(false);
  const [locked, setLocked] = useState(false);
  const [uncertain, setUncertain] = useState(false);
  const [validation, setValidation] = useState('');
  const [customerPicker, setCustomerPicker] = useState(false);
  const [newReview, setNewReview] = useState(false);
  const [failedPhotos, setFailedPhotos] = useState<Set<string>>(
    () => new Set(),
  );
  const [choice, setChoice] = useState<{
    item: MenuItem;
    sizeId: string;
    addonIds: string[];
    quantity: string;
  }>();
  const attempt = useRef<ReturnType<typeof createTicket> | undefined>(
    undefined,
  );
  const requestStarted = useRef(false);
  const everUncertain = useRef(false);
  const submitting = useRef(false);
  const key = ['sales', user.id, 'order', ticketId];
  const catalogKey = ['counter-catalog', user.id];
  const menu = useQuery({
    queryKey: [...catalogKey, 'menu', submittedSearch],
    enabled: !!client,
    queryFn: async ({ signal }) => {
      const { data } = await client!.api.GET('/api/v1/menu-items', {
        params: {
          query: { search: submittedSearch, isActive: true, limit: 200 },
        },
        signal,
      });
      if (!data) throw new Error('The service returned no menu.');
      return data;
    },
  });
  const categories = useQuery({
    queryKey: [...catalogKey, 'categories'],
    enabled: !!client,
    queryFn: async ({ signal }) => {
      const { data } = await client!.api.GET('/api/v1/categories', {
        params: { query: { isActive: true, limit: 200 } },
        signal,
      });
      if (!data) throw new Error('The service returned no categories.');
      return data;
    },
  });
  const tables = useQuery({
    queryKey: ['sales', user.id, 'tables'],
    enabled:
      !!client && !saved && view === 'ticket' && form.channel === 'dine_in',
    queryFn: async ({ signal }) => {
      const { data } = await client!.api.GET('/api/v1/pos/tables', { signal });
      if (!data) throw new Error('Tables could not be loaded.');
      return data;
    },
  });
  const customer = useQuery({
    queryKey: ['counter-customers', user.id, 'detail', form.customerId],
    enabled:
      !!client &&
      !!form.customerId &&
      !saved &&
      user.permissions.includes('customers.manage'),
    queryFn: async ({ signal }) => {
      const { data } = await client!.api.GET('/api/v1/customers/{id}', {
        params: { path: { id: form.customerId! } },
        signal,
      });
      if (!data) throw new Error('Customer details could not be loaded.');
      return data;
    },
  });
  const ticket = useQuery({
    queryKey: key,
    enabled: !!client && saved,
    queryFn: async ({ signal }) => {
      const { data } = await client!.api.GET('/api/v1/orders/{id}', {
        params: { path: { id: ticketId } },
        signal,
      });
      if (!data) throw new Error('The saved ticket could not be loaded.');
      return data;
    },
  });
  const operational = useQuery({
    queryKey: ['sales', user.id, 'orders'],
    enabled:
      !!client &&
      saved &&
      !!ticket.data &&
      (!['draft', 'cancelled', 'completed'].includes(ticket.data.status) ||
        pendingPayments.has(ticketId)),
    queryFn: async ({ signal }) => {
      const { data } = await client!.api.GET('/api/v1/orders', { signal });
      if (!data) throw new Error('The order balance could not be loaded.');
      return data;
    },
  });
  const save = useSafeMutation({
    mutationFn: async (body: ReturnType<typeof createTicket>) => {
      if (!client)
        throw new Error('The authenticated Counter client is unavailable.');
      requestStarted.current = true;
      setLocked(true);
      const { data } = await client.api.POST('/api/v1/orders', { body });
      if (!data) throw new Error('The service did not return a saved ticket.');
      return data;
    },
    onSuccess: async (order) => {
      queries.setQueryData(key, order);
      setSaved(true);
      setUncertain(false);
      everUncertain.current = false;
      setValidation('');
      await queries.invalidateQueries({
        queryKey: ['sales', user.id, 'orders'],
      });
    },
    onError: (error) => {
      if (error instanceof ApiError && error.status === 403) onAccessLost();
      if (requestStarted.current && uncertainOutcome(error)) {
        everUncertain.current = true;
        setUncertain(true);
      } else if (!everUncertain.current) {
        setLocked(false);
        attempt.current = undefined;
      }
    },
  });
  useEffect(() => {
    if (view !== 'ticket') return;
    const handler = BackHandler.addEventListener('hardwareBackPress', () => {
      if (!submitting.current) {
        setView('menu');
        navigation.returnToList();
      }
      return true;
    });
    return () => handler.remove();
  }, [view, navigation]);
  const forbidden = [
    menu.error,
    categories.error,
    ticket.error,
    tables.error,
    customer.error,
    operational.error,
  ].some((error) => error instanceof ApiError && error.status === 403);
  useEffect(() => {
    if (forbidden) onAccessLost();
  }, [forbidden, onAccessLost]);
  const unavailable = save.isWriteDisabled || save.isPending;
  const current = !ticket.isError ? ticket.data : undefined;
  const row = !operational.isError
    ? operational.data?.find((order) => order.id === ticketId)
    : undefined;
  const count = cart.reduce((sum, line) => sum + line.quantity, 0);
  const estimate =
    cart.reduce((sum, line) => sum + Math.round(lineEstimate(line) * 100), 0) /
    100;
  const canEdit = !locked && !saved && !save.isPending;
  const displayed = menu.data?.filter(
    (item) =>
      item.isActive &&
      (!category || item.categoryId === category) &&
      categories.data?.some(
        (group) => group.id === item.categoryId && group.isActive,
      ) &&
      item.sizeVariants.some((size) => size.isActive),
  );
  function openTicket() {
    setView('ticket');
    navigation.openDetail('counter-menu');
  }
  function backToMenu() {
    setView('menu');
    navigation.returnToList();
  }
  function selectCustomer(data: Customer) {
    queries.setQueryData(
      ['counter-customers', user.id, 'detail', data.id],
      data,
    );
    const address = data.addresses.find(
      (item) => item.isActive && item.isDefault,
    );
    setForm((before) => ({
      ...before,
      customerId: data.id,
      customerAddressId: address?.id ?? null,
      recipientName: data.fullName,
      contactNumber: data.phone,
      addressLine: address?.addressLine ?? '',
    }));
  }
  function newTicket() {
    if (save.isPending || (uncertain && !saved)) return;
    const pending = current && pendingActions.get(current.id);
    if (
      pendingPayments.has(ticketId) ||
      (current && pending && !counterActionResolved(current, pending))
    ) {
      setValidation(
        'Resolve the pending ticket action before starting another ticket.',
      );
      return;
    }
    pendingActions.delete(ticketId);
    setTicketId(randomUUID());
    setSaved(false);
    setLocked(false);
    setUncertain(false);
    everUncertain.current = false;
    attempt.current = undefined;
    setCart([]);
    setForm(emptyService());
    setCategory(undefined);
    setValidation('');
    save.reset();
    setNewReview(false);
    backToMenu();
  }
  function addChoice() {
    if (!choice || !canEdit) return;
    const quantity = Number(choice.quantity);
    const size = choice.item.sizeVariants.find(
      (item) => item.id === choice.sizeId && item.isActive,
    );
    if (
      !/^\d+$/.test(choice.quantity.trim()) ||
      !Number.isInteger(quantity) ||
      quantity < 1 ||
      quantity > 2147483647 ||
      !size
    ) {
      setValidation(
        'Choose an available size and a positive whole-number quantity.',
      );
      return;
    }
    setCart((before) => [
      ...before,
      {
        id: randomUUID(),
        item: choice.item,
        size,
        quantity,
        addons: choice.item.compatibleAddons.filter(
          (addon) => addon.isActive && choice.addonIds.includes(addon.id),
        ),
      },
    ]);
    setChoice(undefined);
    setValidation('');
  }
  async function saveTicket() {
    if (unavailable || submitting.current) return;
    try {
      const body =
        locked && attempt.current
          ? attempt.current
          : createTicket(ticketId, form, cart);
      if (
        !locked &&
        form.channel === 'dine_in' &&
        (!tables.data?.some(
          (table) => table.id === form.tableId && !table.isOccupied,
        ) ||
          tables.isError ||
          tables.isFetching)
      ) {
        setValidation('Refresh the table list and choose an available table.');
        return;
      }
      attempt.current = body;
      setValidation('');
      requestStarted.current = false;
      submitting.current = true;
      await save.mutateAsync(body);
    } catch (error) {
      if (error instanceof Error && !requestStarted.current)
        setValidation(error.message);
    } finally {
      submitting.current = false;
    }
  }
  async function refresh() {
    setFailedPhotos(new Set());
    setValidation('');
    if (saved || locked) {
      const result = await ticket.refetch();
      if (result.data && !result.isError) {
        setSaved(true);
        setLocked(true);
        setUncertain(false);
        everUncertain.current = false;
        save.reset();
      }
      await queries.invalidateQueries({
        queryKey: ['sales', user.id, 'orders'],
      });
      if (result.isError && !saved)
        setValidation(
          'The saved ticket could not be found yet. Retry the same save without changing its contents.',
        );
    } else
      await Promise.all([
        queries.invalidateQueries({ queryKey: catalogKey }),
        queries.invalidateQueries({ queryKey: ['sales', user.id, 'tables'] }),
        queries.invalidateQueries({ queryKey: ['counter-customers', user.id] }),
      ]);
  }
  if (forbidden)
    return (
      <ShellFeedback
        kind="error"
        label="Counter access is unavailable. Refresh your permissions from Account."
      />
    );
  return (
    <>
      <ShellHeaderAction
        kind="refresh"
        label={
          saved || locked ? 'Refresh Counter ticket' : 'Refresh Counter menu'
        }
        loading={menu.isFetching || categories.isFetching || ticket.isFetching}
        disabled={save.isPending}
        onPress={() => void refresh()}
      />
      {view === 'ticket' && (
        <>
          <ShellPageHeader title="Your order" priority={1} />
          <ShellHeaderAction
            kind="back"
            label="Back to Counter menu"
            disabled={save.isPending}
            onPress={backToMenu}
          />
        </>
      )}
      <ShellMutationFeedback mutation={save} />
      {!!validation && <ShellFeedback kind="error" label={validation} />}
      {save.error && (
        <ShellFeedback kind="error" label={counterError(save.error)} />
      )}
      {uncertain && !saved && (
        <ShellFeedback
          kind="warning"
          label="The save result is unconfirmed. Keep this ticket unchanged and retry the same save or refresh its status. Do not create a second ticket for this order."
        />
      )}
      {view === 'menu' ? (
        <>
          {saved && current && (
            <ShellFeedback
              kind="info"
              label={`Current ticket: ${current.orderNumber} · ${current.status}. Open your ticket to review it or start another sale.`}
            />
          )}
          <ShellPinnedHeader slot="search" enabled={!!native?.user}>
            <View style={styles.search}>
              <View style={styles.searchField}>
                <ShellInput
                  label="Search menu"
                  accessibilityLabel="Search menu"
                  placeholder="Search drinks and food"
                  hideLabel
                  value={search}
                  onChangeText={setSearch}
                  returnKeyType="search"
                  onSubmitEditing={() => {
                    setMenuCount(20);
                    setSubmittedSearch(search.trim());
                  }}
                />
              </View>
              <View style={styles.searchAction}>
                <ShellButton
                  label="Search menu items"
                  displayLabel="Search"
                  compact
                  variant="secondary"
                  onPress={() => {
                    setMenuCount(20);
                    if (submittedSearch === search.trim()) void menu.refetch();
                    else setSubmittedSearch(search.trim());
                  }}
                />
              </View>
            </View>
            <ScrollView
              horizontal
              showsHorizontalScrollIndicator={false}
              contentContainerStyle={styles.categories}
            >
              <ShellButton
                label="All menu categories"
                displayLabel="All"
                selected={!category}
                compact
                variant="secondary"
                onPress={() => {
                  setCategory(undefined);
                  setMenuCount(20);
                }}
              />
              {categories.data
                ?.filter((item) => item.isActive)
                .sort((a, b) => Number(a.displayOrder) - Number(b.displayOrder))
                .map((item) => (
                  <ShellButton
                    key={item.id}
                    label={`Category ${item.name}`}
                    displayLabel={item.name}
                    selected={category === item.id}
                    compact
                    variant="secondary"
                    onPress={() => {
                      setCategory(item.id);
                      setMenuCount(20);
                    }}
                  />
                ))}
            </ScrollView>
          </ShellPinnedHeader>
          {(menu.isPending || categories.isPending) && (
            <ShellFeedback kind="loading" label="Loading the Counter menu…" />
          )}
          {(menu.isError || categories.isError) && (
            <ShellFeedback
              kind="error"
              label="The menu could not be loaded. Refresh from the header to try again."
            />
          )}
          {!menu.isError && !categories.isError && (
            <View style={styles.grid}>
              {Array.from({ length: columnCount }, (_, column) => (
                <View key={column} style={styles.menuColumn}>
                  {displayed
                    ?.slice(0, menuCount)
                    .filter((_, index) => index % columnCount === column)
                    .map((item) => {
                      const sizes = item.sizeVariants.filter(
                        (size) => size.isActive,
                      );
                      const photo = menuPhoto(item.name);
                      return (
                        <View key={item.id}>
                          <ShellButton
                            label={`Customize ${item.name}`}
                            variant="text"
                            disabled={!canEdit || save.isWriteDisabled}
                            onPress={() => {
                              setValidation('');
                              setChoice({
                                item,
                                sizeId: sizes[0]!.id,
                                addonIds: [],
                                quantity: '1',
                              });
                            }}
                          >
                            {photo && !failedPhotos.has(item.id) ? (
                              <Image
                                source={{ uri: photo }}
                                style={styles.photo}
                                accessible={false}
                                onError={() =>
                                  setFailedPhotos(
                                    (before) => new Set([...before, item.id]),
                                  )
                                }
                              />
                            ) : (
                              <View style={styles.photoFallback}>
                                <ShellIcon
                                  name="cup"
                                  size={32}
                                  color={t.color.primary}
                                />
                              </View>
                            )}
                            <Text style={styles.itemName}>{item.name}</Text>
                            <Text style={styles.caption}>
                              From{' '}
                              {money(
                                Math.min(
                                  ...sizes.map((size) => Number(size.price)),
                                ),
                              )}
                            </Text>
                          </ShellButton>
                        </View>
                      );
                    })}
                </View>
              ))}
            </View>
          )}
          {!menu.isPending &&
            !categories.isPending &&
            !menu.isError &&
            !categories.isError &&
            displayed?.length === 0 && (
              <>
                <ShellFeedback
                  kind="empty"
                  label="No menu items match this selection."
                />
                <ShellButton
                  label="Show all menu items"
                  variant="secondary"
                  onPress={() => {
                    setSearch('');
                    setSubmittedSearch('');
                    setCategory(undefined);
                  }}
                />
              </>
            )}
          {!!displayed && displayed.length > menuCount && (
            <ShellButton
              label="Show more menu items"
              variant="secondary"
              onPress={() => setMenuCount((count) => count + 20)}
            />
          )}
          <ShellText>
            Showing up to 200 matches. Search by name to find more items.
          </ShellText>
          <ShellPageFooter>
            <View style={styles.ticketShortcut}>
              <ShellButton
                label="View Counter ticket"
                displayLabel={
                  saved
                    ? 'View current ticket'
                    : `Your order · ${count} · ${money(estimate)}`
                }
                compact
                onPress={openTicket}
              />
            </View>
          </ShellPageFooter>
        </>
      ) : saved ? (
        <>
          {ticket.isFetching && (
            <ShellFeedback kind="loading" label="Refreshing saved ticket…" />
          )}
          {ticket.isError && (
            <ShellFeedback
              kind="error"
              label="The saved ticket could not be refreshed. Refresh before taking another action."
            />
          )}
          {current && (
            <>
              <ShellHeading level="section">{current.orderNumber}</ShellHeading>
              <ShellText>
                Status: {current.status} ·{' '}
                {current.channel.replaceAll('_', ' ')}
              </ShellText>
              <ShellText>Operator: {user.fullName}</ShellText>
              {current.tableNumber && (
                <ShellText>Table: {current.tableNumber}</ShellText>
              )}
              {current.customerName && (
                <ShellText>Customer: {current.customerName}</ShellText>
              )}
              {current.channel === 'delivery' && (
                <>
                  <ShellText>Recipient: {current.recipientName}</ShellText>
                  <ShellText>Phone: {current.contactNumber}</ShellText>
                  <ShellText>Address: {current.addressLine}</ShellText>
                </>
              )}
              {current.lines.map((line, index) => (
                <View key={index} style={styles.card}>
                  <ShellHeading level="section">
                    {line.quantity} × {line.itemName}
                  </ShellHeading>
                  <ShellText>
                    {line.sizeName} · {money(line.total)}
                  </ShellText>
                  <ShellText>
                    {line.addons.length
                      ? line.addons.join(' · ')
                      : 'No add-ons'}
                  </ShellText>
                </View>
              ))}
              <View style={styles.card}>
                <ShellText>Subtotal: {money(current.subtotal)}</ShellText>
                <ShellText>Discounts: {money(current.discountTotal)}</ShellText>
                {Number(current.subtotal) -
                  Number(current.discountTotal) -
                  Number(current.total) >
                  0.005 && (
                  <ShellText>
                    Loyalty savings:{' '}
                    {money(
                      Number(current.subtotal) -
                        Number(current.discountTotal) -
                        Number(current.total),
                    )}
                  </ShellText>
                )}
                <ShellHeading level="section">
                  Total: {money(current.total)}
                </ShellHeading>
                <ShellText>
                  Saved by the service. Item details and prices are now locked.
                </ShellText>
              </View>
              {current.discounts.map((discount) => (
                <ShellText key={discount.id}>
                  Discount · {discount.status} ·{' '}
                  {discount.type === 'percentage'
                    ? `${discount.value}%`
                    : money(discount.value)}
                </ShellText>
              ))}
              <CounterDraftActions
                key={ticketId}
                user={user}
                order={current}
                pendingActions={pendingActions}
                disabled={ticket.isFetching || ticket.isError || save.isPending}
                onAccessLost={onAccessLost}
              />
              {user.permissions.includes('payments.record') &&
                (!['draft', 'cancelled', 'completed'].includes(
                  current.status,
                ) ||
                  pendingPayments.has(ticketId)) && (
                  <>
                    {(operational.isError || !row) && (
                      <ShellFeedback
                        kind="warning"
                        label="Refresh this ticket to load its current payment balance."
                      />
                    )}
                    {row && (
                      <OrderPayment
                        key={`counter-payment:${ticketId}`}
                        orderId={ticketId}
                        balance={Math.max(
                          0,
                          Number(current.total) - Number(row.paidAmount),
                        )}
                        disabled={
                          operational.isFetching ||
                          operational.isError ||
                          row.status !== current.status ||
                          ticket.isFetching ||
                          ticket.isError
                        }
                        onAccessLost={onAccessLost}
                        pendingPayments={pendingPayments}
                      />
                    )}
                  </>
                )}
            </>
          )}
          <ShellButton
            label="Start new Counter ticket"
            variant="secondary"
            disabled={save.isPending || ticket.isFetching || ticket.isError}
            onPress={newTicket}
          />
        </>
      ) : (
        <>
          {cart.length === 0 ? (
            <ShellFeedback
              kind="empty"
              label="Your ticket is empty. Add drinks or food from the menu."
            />
          ) : (
            <>
              {cart.map((line) => (
                <View key={line.id} style={styles.card}>
                  <ShellHeading level="section">{line.item.name}</ShellHeading>
                  <ShellText>
                    {line.size.sizeName} ·{' '}
                    {line.addons.length
                      ? line.addons.map((addon) => addon.name).join(' · ')
                      : 'No add-ons'}
                  </ShellText>
                  <ShellText>
                    {line.quantity} ×{' '}
                    {money(lineEstimate({ ...line, quantity: 1 }))} ·{' '}
                    {money(lineEstimate(line))}
                  </ShellText>
                  <ShellSelection>
                    <ShellButton
                      label={`Decrease quantity of ${line.item.name}`}
                      displayLabel="−"
                      compact
                      variant="secondary"
                      disabled={!canEdit}
                      onPress={() =>
                        setCart((before) =>
                          before.flatMap((item) =>
                            item.id !== line.id
                              ? [item]
                              : item.quantity > 1
                                ? [{ ...item, quantity: item.quantity - 1 }]
                                : [],
                          ),
                        )
                      }
                    />
                    <ShellButton
                      label={`Increase quantity of ${line.item.name}`}
                      displayLabel="+"
                      compact
                      variant="secondary"
                      disabled={!canEdit || line.quantity >= 2147483647}
                      onPress={() =>
                        setCart((before) =>
                          before.map((item) =>
                            item.id === line.id
                              ? { ...item, quantity: item.quantity + 1 }
                              : item,
                          ),
                        )
                      }
                    />
                    <ShellButton
                      label={`Remove ${line.item.name} from ticket`}
                      displayLabel="Remove"
                      compact
                      variant="text"
                      disabled={!canEdit}
                      onPress={() =>
                        setCart((before) =>
                          before.filter((item) => item.id !== line.id),
                        )
                      }
                    />
                  </ShellSelection>
                </View>
              ))}
              <ShellHeading level="section">Service</ShellHeading>
              <ShellSelection>
                {(['takeaway', 'dine_in', 'delivery'] as const).map(
                  (channel) => (
                    <ShellButton
                      key={channel}
                      label={
                        channel === 'takeaway'
                          ? 'Takeaway'
                          : channel === 'dine_in'
                            ? 'Dine-in'
                            : 'Delivery order'
                      }
                      displayLabel={
                        channel === 'takeaway'
                          ? 'Takeaway'
                          : channel === 'dine_in'
                            ? 'Dine-in'
                            : 'Delivery'
                      }
                      compact
                      variant="secondary"
                      selected={form.channel === channel}
                      disabled={!canEdit}
                      onPress={() =>
                        setForm((before) => ({ ...before, channel }))
                      }
                    />
                  ),
                )}
              </ShellSelection>
              {form.channel === 'takeaway' && (
                <ShellText>
                  Confirm this ticket before sending it to preparation.
                </ShellText>
              )}
              {form.channel === 'dine_in' && (
                <>
                  <ShellText>Choose an available table</ShellText>
                  {tables.isFetching && (
                    <ShellFeedback
                      kind="loading"
                      label="Loading table availability…"
                    />
                  )}
                  {tables.isError && (
                    <ShellFeedback
                      kind="error"
                      label="Tables could not be loaded. Refresh before saving."
                    />
                  )}
                  <ShellSelection>
                    {!tables.isError &&
                      tables.data
                        ?.filter((table) => !table.isOccupied)
                        .map((table) => (
                          <ShellButton
                            key={table.id}
                            label={`Table ${table.tableNumber}`}
                            displayLabel={`${table.tableNumber} · ${table.seats} seats`}
                            compact
                            variant="secondary"
                            selected={form.tableId === table.id}
                            disabled={!canEdit}
                            onPress={() =>
                              setForm((before) => ({
                                ...before,
                                tableId: table.id,
                              }))
                            }
                          />
                        ))}
                  </ShellSelection>
                  {!tables.isFetching &&
                    !tables.isError &&
                    tables.data?.every((table) => table.isOccupied) && (
                      <ShellFeedback
                        kind="empty"
                        label="No tables are available. Refresh or choose another service mode."
                      />
                    )}
                </>
              )}
              <ShellHeading level="section">Customer</ShellHeading>
              <ShellText>
                {form.customerId
                  ? (customer.data?.fullName ?? 'Selected customer')
                  : 'Walk-in customer'}
              </ShellText>
              {customer.data && (
                <ShellText>
                  {customer.data.phone} · {customer.data.loyaltyPointsBalance}{' '}
                  points
                </ShellText>
              )}
              {user.permissions.includes('customers.manage') && (
                <ShellSelection>
                  <ShellButton
                    label="Choose customer"
                    variant="secondary"
                    disabled={!canEdit}
                    onPress={() => setCustomerPicker(true)}
                  />
                  {form.customerId && (
                    <ShellButton
                      label="Use walk-in customer"
                      variant="text"
                      disabled={!canEdit}
                      onPress={() =>
                        setForm((before) => ({
                          ...before,
                          customerId: null,
                          customerAddressId: null,
                          recipientName: '',
                          contactNumber: '',
                          addressLine: '',
                        }))
                      }
                    />
                  )}
                </ShellSelection>
              )}
              {form.channel === 'delivery' && (
                <>
                  <ShellInput
                    label="Delivery recipient"
                    value={form.recipientName}
                    editable={canEdit}
                    onChangeText={(recipientName) =>
                      setForm((before) => ({ ...before, recipientName }))
                    }
                  />
                  <ShellInput
                    label="Delivery phone number"
                    keyboardType="phone-pad"
                    value={form.contactNumber}
                    editable={canEdit}
                    onChangeText={(contactNumber) =>
                      setForm((before) => ({ ...before, contactNumber }))
                    }
                  />
                  {customer.data?.addresses
                    .filter((address) => address.isActive)
                    .map((address) => (
                      <ShellButton
                        key={address.id}
                        label={`Use saved address ${address.label ?? address.addressLine}`}
                        displayLabel={`${address.label ?? 'Saved address'} · ${address.addressLine}`}
                        variant="secondary"
                        selected={form.customerAddressId === address.id}
                        disabled={!canEdit}
                        onPress={() =>
                          setForm((before) => ({
                            ...before,
                            customerAddressId: address.id,
                            addressLine: address.addressLine,
                          }))
                        }
                      />
                    ))}
                  <ShellInput
                    label="Delivery address"
                    value={form.addressLine}
                    editable={canEdit}
                    multiline
                    onChangeText={(addressLine) =>
                      setForm((before) => ({
                        ...before,
                        addressLine,
                        customerAddressId: null,
                      }))
                    }
                  />
                </>
              )}
              <ShellText>Operator: {user.fullName}</ShellText>
              <ShellHeading level="section">
                Estimated total: {money(estimate)}
              </ShellHeading>
              <ShellText>
                Save the draft to review the exact item details and total.
                Confirming it sends it to preparation.
              </ShellText>
              <ShellPageFooter>
                <ShellButton
                  label={locked ? 'Retry same ticket save' : 'Save and review'}
                  prominent
                  loading={save.isPending}
                  disabled={unavailable}
                  onPress={() => void saveTicket()}
                />
              </ShellPageFooter>
              <ShellButton
                label="Discard unsaved ticket"
                variant="text"
                disabled={!canEdit}
                onPress={() => setNewReview(true)}
              />
            </>
          )}
        </>
      )}
      <CounterCustomerPicker
        user={user}
        opened={customerPicker}
        onClose={() => setCustomerPicker(false)}
        onSelect={selectCustomer}
      />
      <ShellSheet
        visible={!!choice}
        title="Customize item"
        onClose={() => {
          setChoice(undefined);
          setValidation('');
        }}
      >
        {choice && (
          <>
            <ShellHeading level="section">{choice.item.name}</ShellHeading>
            {!!choice.item.description && (
              <ShellText>{choice.item.description}</ShellText>
            )}
            <ShellHeading level="section">Size</ShellHeading>
            <ShellSelection>
              {choice.item.sizeVariants
                .filter((size) => size.isActive)
                .map((size) => (
                  <ShellButton
                    key={size.id}
                    label={`Size ${size.sizeName}`}
                    displayLabel={`${size.sizeName} · ${money(size.price)}`}
                    variant="secondary"
                    selected={choice.sizeId === size.id}
                    onPress={() => setChoice({ ...choice, sizeId: size.id })}
                  />
                ))}
            </ShellSelection>
            <ShellHeading level="section">Add-ons</ShellHeading>
            <ShellSelection>
              {choice.item.compatibleAddons
                .filter((addon) => addon.isActive)
                .map((addon) => (
                  <ShellButton
                    key={addon.id}
                    label={`Add-on ${addon.name}`}
                    displayLabel={`${addon.name} · +${money(addon.price)}`}
                    variant="secondary"
                    selected={choice.addonIds.includes(addon.id)}
                    onPress={() =>
                      setChoice({
                        ...choice,
                        addonIds: choice.addonIds.includes(addon.id)
                          ? choice.addonIds.filter((id) => id !== addon.id)
                          : [...choice.addonIds, addon.id],
                      })
                    }
                  />
                ))}
            </ShellSelection>
            {choice.item.compatibleAddons.every((addon) => !addon.isActive) && (
              <ShellText>No add-ons available.</ShellText>
            )}
            <ShellInput
              label="Item quantity"
              keyboardType="number-pad"
              value={choice.quantity}
              onChangeText={(quantity) => setChoice({ ...choice, quantity })}
            />
            {!!validation && <ShellFeedback kind="error" label={validation} />}
            <ShellButton label="Add to ticket" onPress={addChoice} />
            <ShellButton
              label="Cancel customization"
              variant="secondary"
              onPress={() => {
                setChoice(undefined);
                setValidation('');
              }}
            />
          </>
        )}
      </ShellSheet>
      <ShellSheet
        visible={newReview}
        title="Discard unsaved ticket"
        onClose={() => setNewReview(false)}
      >
        <ShellText>
          Remove the items from this unsaved ticket and start again?
        </ShellText>
        <ShellButton label="Discard and start new ticket" onPress={newTicket} />
        <ShellButton
          label="Keep current ticket"
          variant="secondary"
          onPress={() => setNewReview(false)}
        />
      </ShellSheet>
    </>
  );
}
const styles = StyleSheet.create({
  search: { flexDirection: 'row', alignItems: 'center', gap: 8 },
  searchField: { flex: 1, minWidth: 0 },
  searchAction: { flexShrink: 0 },
  ticketShortcut: {
    alignItems: 'flex-end',
    paddingHorizontal: 20,
    paddingVertical: 8,
  },
  categories: { gap: 8, paddingVertical: 4 },
  grid: { flexDirection: 'row', alignItems: 'flex-start', gap: 12 },
  menuColumn: { flex: 1, minWidth: 0, gap: 12 },
  card: {
    backgroundColor: t.color.surface,
    padding: 16,
    borderRadius: t.shape.card,
    gap: 12,
  },
  photo: { width: '100%', height: 112, borderRadius: 10 },
  photoFallback: {
    height: 90,
    alignItems: 'center',
    justifyContent: 'center',
    backgroundColor: t.color.selectedBackground,
    borderRadius: 10,
  },
  itemName: {
    fontFamily: t.type.mediumFont,
    fontSize: 16,
    lineHeight: 23,
    color: t.color.foreground,
  },
  caption: {
    fontFamily: t.type.bodyFont,
    fontSize: 13,
    lineHeight: 20,
    color: t.color.disabledText,
  },
});
