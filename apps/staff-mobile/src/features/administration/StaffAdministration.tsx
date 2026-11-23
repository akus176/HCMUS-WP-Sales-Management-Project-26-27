import { useMemo, useState } from 'react';
import { Alert, StyleSheet, View } from 'react-native';
import { useQuery } from '@tanstack/react-query';

import { ApiError } from '../../api/errors';
import type { components } from '../../api/schema';
import {
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
  ShellInput,
  ShellSelection,
  usePageNavigation,
  ShellMutationFeedback,
} from '../../shell/controls';
import { mobileTokens } from '../../shell/designTokens';
import { recordRow } from '../../shell/recordPresentation';

type StaffAccount = components['schemas']['StaffAccountResponse'];
type SecurityEvent = components['schemas']['SecurityEventResponse'];

const eventFilters = [
  { label: 'All events', value: undefined },
  { label: 'Sign-in failures', value: 'login_failed' },
  { label: 'Account changes', value: 'account_deactivated' },
  { label: 'Token reuse', value: 'token_reuse_detected' },
] as const;

function errorMessage(error: unknown, operation: string) {
  if (error instanceof ApiError) {
    if (error.status === 403)
      return 'Your staff administration permission changed. No account or event data is available.';
    if (error.status === 401)
      return 'Your session ended. Sign in again to view staff administration.';
    if (error.status === 409)
      return 'This account cannot be deactivated because it is the final active administrator, or its state changed. Refresh the list.';
    if (error.status === 404)
      return 'This staff account is no longer available.';
  }
  return `${operation} could not be completed. Refresh the data and try again.`;
}

export function StaffAdministration({
  user,
  showHeading = true,
}: {
  user: StaffSession;
  showHeading?: boolean;
}) {
  const { client } = useSession();
  const { canMutate, refreshConnection } = useConnectivity();
  const [search, setSearch] = useState('');
  const navigation = usePageNavigation();
  const [section, setSection] = useState<'accounts' | 'events'>('accounts');
  const [selectedAccountId, setSelectedAccountId] = useState<string>();
  const [activeFilter, setActiveFilter] = useState<
    'active' | 'inactive' | 'all'
  >('active');
  const [eventType, setEventType] = useState<string | undefined>();
  const [actionFeedback, setActionFeedback] = useState<{
    kind: 'success' | 'error';
    label: string;
    accountId: string;
  }>();
  const queryKey = useMemo(
    () => ['staff-administration', user.id] as const,
    [user.id],
  );

  const accountsQuery = useQuery({
    queryKey: [...queryKey, 'accounts', search.trim(), activeFilter],
    enabled: !!client && canMutate,
    queryFn: async ({ signal }) => {
      if (!client)
        throw new Error('The authenticated API client is unavailable.');
      const { data } = await client.api.GET('/api/v1/users', {
        params: {
          query: {
            search: search.trim() || undefined,
            isActive:
              activeFilter === 'all' ? undefined : activeFilter === 'active',
            limit: 100,
          },
        },
        signal,
      });
      if (!data) throw new Error('The API returned no staff account results.');
      return data;
    },
  });

  const eventsQuery = useQuery({
    queryKey: [...queryKey, 'events', eventType],
    enabled: !!client && canMutate,
    queryFn: async ({ signal }) => {
      if (!client)
        throw new Error('The authenticated API client is unavailable.');
      const { data } = await client.api.GET('/api/v1/security-events', {
        params: { query: { eventType, limit: 50 } },
        signal,
      });
      if (!data) throw new Error('The API returned no security events.');
      return data;
    },
  });

  const accountDetailQuery = useQuery({
    queryKey: [...queryKey, 'account', selectedAccountId],
    enabled: !!client && canMutate && !!selectedAccountId,
    queryFn: async ({ signal }) => {
      if (!client || !selectedAccountId)
        throw new Error('The authenticated API client is unavailable.');
      const { data } = await client.api.GET('/api/v1/users/{id}', {
        params: { path: { id: selectedAccountId } },
        signal,
      });
      if (!data) throw new Error('The API returned no staff account details.');
      return data;
    },
  });

  const deactivation = useSafeMutation<StaffAccount, string>({
    mutationFn: async (id) => {
      if (!client)
        throw new Error('The authenticated API client is unavailable.');
      const { data } = await client.api.POST('/api/v1/users/{id}/deactivate', {
        params: { path: { id } },
        body: { confirm: true },
      });
      if (!data) throw new Error('The server returned no account state.');
      return data;
    },
    onSuccess: (account) => {
      setActionFeedback({
        kind: 'success',
        label: `${account.fullName} is now inactive. Their active sessions were revoked.`,
        accountId: account.id,
      });
    },
    onError: (error, accountId) => {
      setActionFeedback({
        kind: 'error',
        label: errorMessage(error, 'Deactivation'),
        accountId,
      });
    },
  });

  const apiForbidden = [
    accountsQuery.error,
    accountDetailQuery.error,
    eventsQuery.error,
    deactivation.error,
  ].some((error) => error instanceof ApiError && error.status === 403);

  const refresh = () => {
    setActionFeedback(undefined);
    void refreshConnection();
    void accountsQuery.refetch();
    void eventsQuery.refetch();
  };

  if (apiForbidden)
    return (
      <>
        <ShellHeading>Access changed</ShellHeading>
        <ShellFeedback
          kind="error"
          label="Your staff administration permission changed. No account or event data is available."
        />
      </>
    );

  return (
    <>
      {showHeading && <ShellHeading>Staff administration</ShellHeading>}
      <ShellMutationFeedback mutation={deactivation} />
      <ShellSelection>
        <ShellButton
          label="Staff accounts"
          selected={section === 'accounts'}
          compact
          variant="secondary"
          onPress={() => {
            setSection('accounts');
            setSelectedAccountId(undefined);
          }}
        />
        <ShellButton
          label="Security events"
          selected={section === 'events'}
          compact
          variant="secondary"
          onPress={() => {
            setSection('events');
            setSelectedAccountId(undefined);
          }}
        />
      </ShellSelection>
      {actionFeedback?.kind === 'success' && (
        <ShellFeedback
          kind={actionFeedback.kind}
          label={actionFeedback.label}
        />
      )}
      {section === 'accounts' && !selectedAccountId && (
        <>
          <ShellInput
            label="Search staff accounts"
            accessibilityLabel="Search staff accounts"
            autoCapitalize="none"
            autoCorrect={false}
            onChangeText={setSearch}
            placeholder="Search name, username, or email"
            placeholderTextColor={mobileTokens.color.disabledText}
            value={search}
          />
          <ShellSelection>
            {(['active', 'inactive', 'all'] as const).map((filter) => (
              <ShellButton
                key={filter}
                label={`${filter[0]?.toUpperCase()}${filter.slice(1)} accounts`}
                selected={activeFilter === filter}
                compact
                variant="secondary"
                onPress={() => setActiveFilter(filter)}
              />
            ))}
          </ShellSelection>
          {accountsQuery.isPending ? (
            <ShellFeedback kind="loading" label="Loading staff accounts…" />
          ) : accountsQuery.error ? (
            <ShellFeedback
              kind="error"
              label={errorMessage(accountsQuery.error, 'Staff account search')}
            />
          ) : !accountsQuery.data?.length ? (
            <ShellFeedback
              kind="empty"
              label="No staff accounts match this search."
            />
          ) : (
            accountsQuery.data.map((account) => (
              <View key={account.id} style={styles.card}>
                <ShellText>{account.fullName}</ShellText>
                <ShellText>
                  @{account.username} ·{' '}
                  {account.isActive ? 'Active' : 'Inactive'}
                </ShellText>
                <ShellSelection>
                  <ShellButton
                    label={`View details for ${account.fullName}`}
                    displayLabel="View account"
                    variant="link"
                    focusKey={`account:${account.id}`}
                    onPress={() => {
                      navigation.openDetail(`account:${account.id}`);
                      setSelectedAccountId(account.id);
                    }}
                  />
                </ShellSelection>
                {account.isActive && (
                  <ShellButton
                    label={`Deactivate ${account.fullName}`}
                    disabled={
                      deactivation.isWriteDisabled || deactivation.isPending
                    }
                    loading={deactivation.isPending}
                    loadingLabel="Deactivating account…"
                    variant="secondary"
                    onPress={() => {
                      Alert.alert(
                        'Deactivate staff account?',
                        `Deactivate ${account.fullName}? Their active sessions will be revoked.`,
                        [
                          { text: 'Cancel', style: 'cancel' },
                          {
                            text: 'Deactivate',
                            style: 'destructive',
                            onPress: () => deactivation.mutate(account.id),
                          },
                        ],
                      );
                    }}
                  />
                )}
                {actionFeedback?.kind === 'error' &&
                  actionFeedback.accountId === account.id && (
                    <ShellFeedback kind="error" label={actionFeedback.label} />
                  )}
              </View>
            ))
          )}
        </>
      )}
      {selectedAccountId && (
        <View style={styles.card}>
          <ShellHeading key={selectedAccountId} level="section" focusOnMount>
            Account details
          </ShellHeading>
          {accountDetailQuery.isPending ? (
            <ShellFeedback kind="loading" label="Loading account details…" />
          ) : accountDetailQuery.error ? (
            <ShellFeedback
              kind="error"
              label={errorMessage(accountDetailQuery.error, 'Account details')}
            />
          ) : accountDetailQuery.data ? (
            <>
              <ShellText>{accountDetailQuery.data.fullName}</ShellText>
              <ShellText>@{accountDetailQuery.data.username}</ShellText>
              <ShellText>{accountDetailQuery.data.email}</ShellText>
              {accountDetailQuery.data.phone && (
                <ShellText>{accountDetailQuery.data.phone}</ShellText>
              )}
              <ShellText>
                Assigned roles:{' '}
                {accountDetailQuery.data.roles.join(', ') || 'None'}
              </ShellText>
              <ShellText>
                Status:{' '}
                {accountDetailQuery.data.isActive ? 'Active' : 'Inactive'}
              </ShellText>
              <ShellText>
                Created:{' '}
                {new Date(accountDetailQuery.data.createdAt).toLocaleString()}
              </ShellText>
              {accountDetailQuery.data.deactivatedAt && (
                <ShellText>
                  Deactivated:{' '}
                  {new Date(
                    accountDetailQuery.data.deactivatedAt,
                  ).toLocaleString()}
                </ShellText>
              )}
            </>
          ) : null}
          <ShellButton
            label="Close account details"
            variant="text"
            onPress={() => {
              setSelectedAccountId(undefined);
              navigation.returnToList();
            }}
          />
        </View>
      )}
      {section === 'events' && (
        <>
          <ShellHeading level="section">Security events</ShellHeading>
          <ShellSelection>
            {eventFilters.map((filter) => (
              <ShellButton
                key={filter.label}
                label={filter.label}
                selected={eventType === filter.value}
                compact
                variant="secondary"
                onPress={() => setEventType(filter.value)}
              />
            ))}
          </ShellSelection>
          {eventsQuery.isPending ? (
            <ShellFeedback kind="loading" label="Loading security events…" />
          ) : eventsQuery.error ? (
            <ShellFeedback
              kind="error"
              label={errorMessage(eventsQuery.error, 'Security event search')}
            />
          ) : !eventsQuery.data?.length ? (
            <ShellFeedback
              kind="empty"
              label="No security events match this filter."
            />
          ) : (
            eventsQuery.data.map((event) => (
              <EventCard key={event.id} event={event} />
            ))
          )}
        </>
      )}

      {!canMutate && (
        <ShellFeedback
          kind="warning"
          label="Reconnect to load or change staff administration data."
        />
      )}
      <ShellButton
        label="Refresh staff administration"
        disabled={accountsQuery.isFetching || eventsQuery.isFetching}
        loading={accountsQuery.isFetching || eventsQuery.isFetching}
        loadingLabel="Refreshing staff data…"
        variant="secondary"
        onPress={refresh}
      />
    </>
  );
}

function EventCard({ event }: { event: SecurityEvent }) {
  return (
    <View style={styles.card}>
      <ShellText>{event.eventType.replace(/_/g, ' ')}</ShellText>
      <ShellText>{event.outcome}</ShellText>
      <ShellText>{new Date(event.occurredAt).toLocaleString()}</ShellText>
      <ShellText>
        {event.userId ? `Staff ID ${event.userId}` : 'System event'}
      </ShellText>
      {event.ipAddress && <ShellText>Source IP: {event.ipAddress}</ShellText>}
    </View>
  );
}

const styles = StyleSheet.create({
  filters: { gap: mobileTokens.spacing.sm },
  card: recordRow,
});
