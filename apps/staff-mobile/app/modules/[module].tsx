import { router, useLocalSearchParams } from 'expo-router';
import { useEffect } from 'react';
import { DeliveryScreen } from '../../src/features/delivery/DeliveryScreen';
import { useNativeNavigation } from '../../src/shell/NativeNavigation';
import { PermissionBoundary } from '../../src/shell/PermissionBoundary';
import { useRouteAccess } from '../../src/shell/useRouteAccess';
import {
  ShellPage,
  ShellPageHeader,
  ShellStatus,
} from '../../src/shell/controls';

export default function ModuleScreen() {
  const { module: moduleId } = useLocalSearchParams<{
    module: string | string[];
  }>();
  const access = useRouteAccess();
  const navigation = useNativeNavigation();
  return (
    <ShellPage>
      <PermissionBoundary
        moduleId={moduleId === 'delivery' ? 'delivery' : undefined}
        {...access}
        fallbackHeader={
          <ShellPageHeader
            backLabel="Back to Today"
            onBack={() => router.replace('/session')}
          />
        }
      >
        {() =>
          navigation ? (
            <DeliveryEntry />
          ) : (
            <>
              <ShellPageHeader
                title="Deliveries"
                backLabel="Back to Today"
                onBack={() => router.replace('/session')}
              />
              <DeliveryScreen showHeading={false} />
            </>
          )
        }
      </PermissionBoundary>
    </ShellPage>
  );
}
function DeliveryEntry() {
  const open = useNativeNavigation()?.open;
  useEffect(() => {
    open?.('delivery');
  }, [open]);
  return <ShellStatus>Opening deliveries…</ShellStatus>;
}
