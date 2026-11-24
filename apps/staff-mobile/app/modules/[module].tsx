import { router, useLocalSearchParams } from 'expo-router';
import { useEffect } from 'react';
import { useNativeNavigation } from '../../src/shell/NativeNavigation';
import type { Workspace } from '../../src/shell/navigationPolicy';
import { PreparationScreen } from '../../src/barista/PreparationScreen';

import { PermissionBoundary } from '../../src/shell/PermissionBoundary';
import { useRouteAccess } from '../../src/shell/useRouteAccess';
import {
  ShellPage,
  ShellPageHeader,
  ShellStatus,
} from '../../src/shell/controls';
import { findModule } from '../../src/shell/permissions';
import { OrdersScreen } from '../../src/features/orders/OrdersScreen';

export default function ModuleScreen() {
  const { module: moduleId } = useLocalSearchParams<{
    module: string | string[];
  }>();
  const module =
    typeof moduleId === 'string' &&
    ['cashier', 'barista', 'operations'].includes(moduleId)
      ? findModule(moduleId)
      : undefined;
  const access = useRouteAccess();
  const navigation = useNativeNavigation();
  return (
    <ShellPage>
      <PermissionBoundary
        moduleId={module?.id}
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
                title={module?.title}
                inline
                backLabel="Back to staff home"
                onBack={() => router.replace('/session')}
                hideDuringDetail={module?.id === 'barista'}
              />
              {module?.id === 'barista' ? (
                <PreparationScreen showHeading={false} />
              ) : module?.id === 'cashier' ? (
                <OrdersScreen user={user} showHeading={false} startWithMenu />
              ) : module?.id === 'operations' ? (
                <OrdersScreen user={user} manager />
              ) : null}
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
