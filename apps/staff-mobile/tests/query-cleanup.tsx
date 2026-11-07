import { useQueryClient } from '@tanstack/react-query';
import { useEffect } from 'react';

const clients = new Set<ReturnType<typeof useQueryClient>>();
afterEach(() => {
  clients.forEach((client) => client.clear());
  clients.clear();
});

// Clear after React Native Testing Library unmounts all query observers.
export function QueryCleanup() {
  const queries = useQueryClient();
  useEffect(() => {
    const defaults = queries.getDefaultOptions();
    queries.setDefaultOptions({
      ...defaults,
      queries: { ...defaults.queries, gcTime: Infinity },
      mutations: { ...defaults.mutations, gcTime: Infinity },
    });
    clients.add(queries);
  }, [queries]);
  return null;
}
