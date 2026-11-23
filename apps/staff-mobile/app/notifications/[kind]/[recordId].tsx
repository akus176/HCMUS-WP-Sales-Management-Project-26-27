import { router, useLocalSearchParams } from 'expo-router';

import { PreparationScreen } from '../../../src/barista/PreparationScreen';
import { DeliveryDetail } from '../../../src/features/delivery/DeliveryDetail';
import { notificationDestination } from '../../../src/notifications/destination';
import { LowStockRecord } from '../../../src/notifications/LowStockRecord';
import { PermissionBoundary } from '../../../src/shell/PermissionBoundary';
import {
  ShellPageHeader,
  ShellPage,
  ShellStatus,
} from '../../../src/shell/controls';
import { useRouteAccess } from '../../../src/shell/useRouteAccess';

export default function NotificationRecordRoute() {
  const params = useLocalSearchParams<{ kind: string; recordId: string }>();
  const target = notificationDestination(params);
  const access = useRouteAccess();
  return (
    <ShellPage>
      <ShellPageHeader
        backLabel="Back to staff home"
        onBack={() => router.replace('/session')}
        hideDuringDetail={target?.kind === 'urgent-order'}
      />
      {target ? (
        <PermissionBoundary
          key={`${target.kind}:${target.recordId}`}
          moduleId={target.module}
          requiredPermission={target.permission}
          {...access}
        >
          {(user) => {
            switch (target.kind) {
              case 'assignment':
                return <DeliveryDetail orderId={target.recordId} />;
              case 'low-stock':
                return <LowStockRecord id={target.recordId} />;
              case 'urgent-order':
                return (
                  <PreparationScreen
                    key={target.recordId}
                    ticketId={target.recordId}
                  />
                );
            }
          }}
        </PermissionBoundary>
      ) : (
        <ShellStatus>This notification link is unavailable.</ShellStatus>
      )}
    </ShellPage>
  );
}
