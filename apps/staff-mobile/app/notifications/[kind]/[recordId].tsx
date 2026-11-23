import { router, useLocalSearchParams } from 'expo-router';

import { DeliveryDetail } from '../../../src/features/delivery/DeliveryDetail';
import { notificationDestination } from '../../../src/notifications/destination';
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
      />
      {target ? (
        <PermissionBoundary
          key={`${target.kind}:${target.recordId}`}
          moduleId={target.module}
          requiredPermission={target.permission}
          {...access}
        >
          {() => {
            return <DeliveryDetail orderId={target.recordId} />;
          }}
        </PermissionBoundary>
      ) : (
        <ShellStatus>This notification link is unavailable.</ShellStatus>
      )}
    </ShellPage>
  );
}
