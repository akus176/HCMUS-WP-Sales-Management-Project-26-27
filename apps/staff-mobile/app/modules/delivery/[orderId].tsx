import { router } from 'expo-router';
import { PermissionBoundary } from '../../../src/shell/PermissionBoundary';
import { ShellPage, ShellPageHeader } from '../../../src/shell/controls';

/** Retired delivery links must not load courier or customer data. */
export default function RetiredDeliveryRoute() {
  return (
    <ShellPage>
      <ShellPageHeader
        backLabel="Back to staff home"
        onBack={() => router.replace('/session')}
      />
      <PermissionBoundary moduleId="retired-delivery">
        {() => null}
      </PermissionBoundary>
    </ShellPage>
  );
}
