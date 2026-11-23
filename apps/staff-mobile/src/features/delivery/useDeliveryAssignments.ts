import { useQuery } from '@tanstack/react-query';
import { useSession } from '../../session/context';

/** Today and Deliveries share the same cached assignment pages. */
export function useDeliveryAssignments(
  view: 'active' | 'completed',
  page: number,
  enabled = true,
) {
  const { client, state } = useSession();
  return useQuery({
    queryKey: ['delivery', state.user?.id, 'list', view, page],
    enabled: !!client && !!state.user && enabled,
    queryFn: async ({ signal }) => {
      if (!client)
        throw new Error('The authenticated delivery client is unavailable.');
      const { data } = await client.api.GET('/api/v1/courier/assignments', {
        signal,
        params: {
          query: {
            status:
              view === 'active'
                ? 'assigned,out_for_delivery'
                : 'delivered,failed',
            page,
            pageSize: 10,
          },
        },
      });
      if (!data)
        throw new Error('The service returned no delivery assignments.');
      return data;
    },
  });
}
