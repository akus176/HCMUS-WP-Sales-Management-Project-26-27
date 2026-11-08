import { router } from 'expo-router';

import { OrdersScreen } from '../../../src/features/orders/OrdersScreen';
import { PermissionBoundary } from '../../../src/shell/PermissionBoundary';
import { ShellButton, ShellPage } from '../../../src/shell/controls';
import { useRouteAccess } from '../../../src/shell/useRouteAccess';

export default function ManagerOrdersScreen() {
  const access = useRouteAccess();
  return (
    <ShellPage>
      <ShellButton
        label="Back to staff home"
        onPress={() => router.replace('/session')}
      />
      <PermissionBoundary moduleId="approvals" {...access}>
        {(user) => <OrdersScreen user={user} manager />}
      </PermissionBoundary>
    </ShellPage>
  );
}
