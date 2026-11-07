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
import { InventoryWorkspace } from '../../src/features/inventory/InventoryWorkspace';

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
            <DiscountApprovals user={user} />
          ) : module?.id === 'inventory' ? (
            <InventoryWorkspace user={user} />
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
