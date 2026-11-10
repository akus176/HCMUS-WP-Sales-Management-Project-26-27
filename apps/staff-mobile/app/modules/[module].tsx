import { router, useLocalSearchParams } from 'expo-router';
import { PreparationScreen } from '../../src/barista/PreparationScreen';

import { PermissionBoundary } from '../../src/shell/PermissionBoundary';
import { useRouteAccess } from '../../src/shell/useRouteAccess';
import {
  ShellButton,
  ShellHeading,
  ShellPage,
  ShellText,
} from '../../src/shell/controls';
import { findModule, permittedActions } from '../../src/shell/permissions';
import { DiscountApprovals } from '../../src/features/approvals/DiscountApprovals';
import { StaffAdministration } from '../../src/features/administration/StaffAdministration';
import { OrdersScreen } from '../../src/features/orders/OrdersScreen';
import { DeliveryScreen } from '../../src/features/delivery/DeliveryScreen';

export default function ModuleScreen() {
  const { module: moduleId } = useLocalSearchParams<{
    module: string | string[];
  }>();
  const module = findModule(moduleId);
  const access = useRouteAccess();
  return (
    <ShellPage>
      <ShellButton
        label="Back to staff home"
        onPress={() => router.replace('/session')}
      />
      <PermissionBoundary moduleId={moduleId} {...access}>
        {(user) =>
          module?.id === 'barista' ? (
            <PreparationScreen />
          ) : module?.id === 'approvals' ? (
            <>
              {user.permissions.some((permission) =>
                ['orders.cancel', 'delivery.assign'].includes(permission),
              ) && (
                <ShellButton
                  label="Manage cancellations and deliveries"
                  onPress={() => router.push('/modules/approvals/operations')}
                />
              )}
              <DiscountApprovals user={user} />
            </>
          ) : module?.id === 'cashier' ? (
            <OrdersScreen user={user} />
          ) : module?.id === 'delivery' ? (
            <DeliveryScreen />
          ) : module?.id === 'administration' ? (
            <StaffAdministration user={user} />
          ) : (
            <>
              <ShellHeading>{module!.title}</ShellHeading>
              <ShellText>Selected module: {module!.title}</ShellText>
              <ShellText>
                Workflow screens are planned and are not available yet. No
                business action can be performed here.
              </ShellText>
              {permittedActions(module!, user.permissions).map((action) => (
                <ShellText key={action.permission}>
                  {action.label} — planned
                </ShellText>
              ))}
            </>
          )
        }
      </PermissionBoundary>
    </ShellPage>
  );
}
