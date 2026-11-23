import { router, useLocalSearchParams } from 'expo-router';
import { useEffect } from 'react';
import { useNativeNavigation } from '../../src/shell/NativeNavigation';
import type { Workspace } from '../../src/shell/navigationPolicy';
import { PreparationScreen } from '../../src/barista/PreparationScreen';

import { PermissionBoundary } from '../../src/shell/PermissionBoundary';
import { useRouteAccess } from '../../src/shell/useRouteAccess';
import {
  ShellHeading,
  ShellPage,
  ShellPageHeader,
  ShellText,
  ShellStatus,
} from '../../src/shell/controls';
import { findModule, permittedActions } from '../../src/shell/permissions';
import { StaffAdministration } from '../../src/features/administration/StaffAdministration';
import { InventoryWorkspace } from '../../src/features/inventory/InventoryWorkspace';
import { OrdersScreen } from '../../src/features/orders/OrdersScreen';
import { DeliveryScreen } from '../../src/features/delivery/DeliveryScreen';

export default function ModuleScreen() {
  const { module: moduleId } = useLocalSearchParams<{
    module: string | string[];
  }>();
  const module = findModule(moduleId);
  const access = useRouteAccess();
  const navigation = useNativeNavigation();
  return (
    <ShellPage>
      <PermissionBoundary
        moduleId={moduleId}
        {...access}
        fallbackHeader={
          <ShellPageHeader
            backLabel="Back to staff home"
            onBack={() => router.replace('/session')}
          />
        }
      >
        {(user) =>
          navigation && module ? (
            <NativeModuleEntry id={module.id as Workspace | 'operations'} />
          ) : (
            <>
              <ShellPageHeader
                title={
                  module?.id === 'administration'
                    ? 'Staff administration'
                    : module?.title
                }
                inline
                backLabel="Back to staff home"
                onBack={() => router.replace('/session')}
                hideDuringDetail={module?.id === 'barista'}
              />
              {module?.id === 'barista' ? (
                <PreparationScreen showHeading={false} />
              ) : module?.id === 'inventory' ? (
                <InventoryWorkspace user={user} showHeading={false} />
              ) : module?.id === 'cashier' ? (
                <OrdersScreen user={user} showHeading={false} />
              ) : module?.id === 'delivery' ? (
                <DeliveryScreen showHeading={false} />
              ) : module?.id === 'administration' ? (
                <StaffAdministration user={user} showHeading={false} />
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
              )}
            </>
          )
        }
      </PermissionBoundary>
    </ShellPage>
  );
}

function NativeModuleEntry({ id }: { id: Workspace | 'operations' }) {
  const open = useNativeNavigation()?.open;
  useEffect(() => {
    if (id === 'operations') router.replace('/modules/cashier/operations');
    else open?.(id);
  }, [open, id]);
  return <ShellStatus>Opening workspace…</ShellStatus>;
}
