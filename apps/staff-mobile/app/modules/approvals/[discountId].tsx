import { useLocalSearchParams } from 'expo-router';

import { DiscountApprovals } from '../../../src/features/approvals/DiscountApprovals';
import { PermissionBoundary } from '../../../src/shell/PermissionBoundary';
import { ShellPage } from '../../../src/shell/controls';
import { useRouteAccess } from '../../../src/shell/useRouteAccess';

export default function DiscountReviewScreen() {
  const { discountId } = useLocalSearchParams<{
    discountId: string | string[];
  }>();
  const access = useRouteAccess();
  return (
    <ShellPage>
      <PermissionBoundary moduleId="approvals" {...access}>
        {(user) => (
          <DiscountApprovals
            user={user}
            discountId={typeof discountId === 'string' ? discountId : undefined}
          />
        )}
      </PermissionBoundary>
    </ShellPage>
  );
}
