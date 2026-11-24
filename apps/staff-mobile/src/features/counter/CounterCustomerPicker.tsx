import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { useSession } from '../../session/context';
import type { StaffSession } from '../../session/session';
import {
  ShellButton,
  ShellFeedback,
  ShellInput,
  ShellSheet,
  ShellText,
} from '../../shell/controls';
import type { Customer } from './counterModel';

export function CounterCustomerPicker({
  user,
  opened,
  onClose,
  onSelect,
}: {
  user: StaffSession;
  opened: boolean;
  onClose: () => void;
  onSelect: (customer: Customer) => void;
}) {
  const { client } = useSession();
  const [search, setSearch] = useState('');
  const [submitted, setSubmitted] = useState('');
  const [error, setError] = useState('');
  const [loadingId, setLoadingId] = useState<string>();
  const allowed = user.permissions.includes('customers.manage');
  const customers = useQuery({
    queryKey: ['counter-customers', user.id, submitted],
    enabled: !!client && opened && allowed,
    queryFn: async ({ signal }) => {
      const { data } = await client!.api.GET('/api/v1/customers', {
        params: { query: { search: submitted, isActive: true } },
        signal,
      });
      if (!data) throw new Error('The service returned no customer list.');
      return data;
    },
  });
  async function select(id: string) {
    if (loadingId || !client) return;
    setLoadingId(id);
    setError('');
    try {
      const { data } = await client.api.GET('/api/v1/customers/{id}', {
        params: { path: { id } },
      });
      if (!data?.isActive)
        throw new Error(
          'This customer is no longer available. Refresh the customer search.',
        );
      onSelect(data);
      onClose();
    } catch {
      setError(
        'Customer details could not be loaded. Search again before choosing a customer.',
      );
    } finally {
      setLoadingId(undefined);
    }
  }
  return (
    <ShellSheet
      title="Choose customer"
      visible={opened}
      onClose={onClose}
      busy={!!loadingId}
    >
      <ShellInput
        label="Customer search"
        value={search}
        onChangeText={setSearch}
        placeholder="Name or phone number"
        returnKeyType="search"
        onSubmitEditing={() => setSubmitted(search.trim())}
      />
      <ShellButton
        label="Search customers"
        variant="secondary"
        onPress={() => {
          setError('');
          if (submitted === search.trim()) void customers.refetch();
          else setSubmitted(search.trim());
        }}
      />
      {customers.isPending && (
        <ShellFeedback kind="loading" label="Loading customers…" />
      )}
      {(customers.isError || error) && (
        <ShellFeedback
          kind="error"
          label={
            error ||
            'Customers could not be loaded. Close and refresh before trying again.'
          }
        />
      )}
      {!customers.isError && customers.data?.length === 0 && (
        <ShellFeedback
          kind="empty"
          label="No active customers match this search."
        />
      )}
      {!customers.isError &&
        customers.data?.map((customer) => (
          <ShellButton
            key={customer.id}
            label={`Choose customer ${customer.fullName}`}
            variant="secondary"
            disabled={!!loadingId}
            loading={loadingId === customer.id}
            onPress={() => void select(customer.id)}
          >
            <ShellText>{customer.fullName}</ShellText>
            <ShellText>
              {customer.phone} · {customer.loyaltyPointsBalance} points
            </ShellText>
          </ShellButton>
        ))}
    </ShellSheet>
  );
}
