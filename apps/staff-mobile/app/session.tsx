import { PermissionBoundary } from '../src/shell/PermissionBoundary';
import { StaffHome } from '../src/shell/StaffHome';
import { useRouteAccess } from '../src/shell/useRouteAccess';
import { ShellPage } from '../src/shell/controls';
import { NativeWorkspaceHost } from '../src/shell/NativeWorkspaceHost';
import { useNativeNavigation } from '../src/shell/NativeNavigation';

export default function SessionScreen() {
  const access = useRouteAccess();
  const native = useNativeNavigation();
  if (native)
    return (
      <PermissionBoundary
        scope="home"
        retain
        {...access}
        refreshKey={access.refreshKey + native.revision}
      >
        {(user) => <NativeWorkspaceHost user={user} />}
      </PermissionBoundary>
    );
  return (
    <ShellPage>
      <PermissionBoundary scope="home" {...access}>
        {(user) => <StaffHome user={user} embedded />}
      </PermissionBoundary>
    </ShellPage>
  );
}
