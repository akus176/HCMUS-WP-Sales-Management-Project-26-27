import { PermissionBoundary } from '../src/shell/PermissionBoundary';
import { StaffHome } from '../src/shell/StaffHome';
import { useRouteAccess } from '../src/shell/useRouteAccess';
import { ShellPage } from '../src/shell/controls';

export default function SessionScreen() {
  const access = useRouteAccess();
  return (
    <ShellPage>
      <PermissionBoundary scope="home" {...access}>
        {(user) => <StaffHome user={user} embedded />}
      </PermissionBoundary>
    </ShellPage>
  );
}
