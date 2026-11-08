import { useLocalSearchParams } from 'expo-router';

import { DeliveryDetail } from '../../../src/features/delivery/DeliveryDetail';
import { PermissionBoundary } from '../../../src/shell/PermissionBoundary';
import { ShellPage } from '../../../src/shell/controls';
import { useRouteAccess } from '../../../src/shell/useRouteAccess';

export default function DeliveryDetailRoute() {
  const { orderId } = useLocalSearchParams<{ orderId: string | string[] }>();
  const access = useRouteAccess();
  return (
    <ShellPage>
      <PermissionBoundary moduleId="delivery" {...access}>
        {() => (
          <DeliveryDetail
            orderId={typeof orderId === 'string' ? orderId : ''}
          />
        )}
      </PermissionBoundary>
    </ShellPage>
  );
}
