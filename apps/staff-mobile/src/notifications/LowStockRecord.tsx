import { useQuery } from '@tanstack/react-query';

import { useSession } from '../session/context';
import { ShellHeading, ShellStatus, ShellText } from '../shell/controls';

export function LowStockRecord({ id }: { id: string }) {
  const { client, state } = useSession();
  const stock = useQuery({
    queryKey: ['inventory', state.user!.id, 'notification', id],
    queryFn: async ({ signal }) => {
      const { data } = await client!.api.GET('/api/v1/ingredients/{id}', {
        params: { path: { id } },
        signal,
      });
      return data!;
    },
  });
  if (stock.isError || (!stock.isPending && !stock.data))
    return <ShellStatus>This stock record is unavailable.</ShellStatus>;
  if (!stock.data) return <ShellStatus>Loading stock record…</ShellStatus>;
  const { name, availableQuantity, reorderThreshold, baseUnit, isActive } =
    stock.data;
  return (
    <>
      <ShellHeading>{name}</ShellHeading>
      <ShellText>
        Available: {availableQuantity} {baseUnit}
      </ShellText>
      <ShellText>
        Reorder threshold: {reorderThreshold} {baseUnit}
      </ShellText>
      <ShellText>
        {isActive && Number(availableQuantity) <= Number(reorderThreshold)
          ? 'Low stock'
          : 'This item no longer has an active low-stock alert.'}
      </ShellText>
    </>
  );
}
