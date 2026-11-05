import { PermissionBoundary } from '../src/shell/PermissionBoundary';
import { StaffHome } from '../src/shell/StaffHome';
import { useRouteAccess } from '../src/shell/useRouteAccess';

export default function SessionScreen() {
  const access = useRouteAccess();
  return (
    <PermissionBoundary scope="home" {...access}>
      {(user) => <StaffHome user={user} />}
    </PermissionBoundary>
  );
}
