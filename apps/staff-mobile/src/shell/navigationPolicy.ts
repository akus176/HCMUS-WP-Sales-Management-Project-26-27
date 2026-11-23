import { permittedModules } from './permissions';

export type Workspace = 'home' | 'more' | 'account' | 'barista' | 'cashier';
export type NavigationItem = {
  id: Workspace;
  label: string;
  icon: 'home' | 'orders' | 'more' | 'account' | 'cup';
};
export const workspaceNavigation: Record<string, NavigationItem> = {
  barista: { id: 'barista', label: 'Preparation', icon: 'cup' },
  cashier: { id: 'cashier', label: 'Counter', icon: 'orders' },
};
export function availableWorkspaces(permissions: readonly string[]) {
  return permittedModules(permissions).filter((module) =>
    ['cashier', 'barista'].includes(module.id),
  );
}
export function navigationFor(
  permissions: readonly string[],
): NavigationItem[] {
  const modules = availableWorkspaces(permissions);
  return [
    { id: 'home', label: 'Today', icon: 'home' },
    ...['cashier', 'barista']
      .filter((id) => modules.some((module) => module.id === id))
      .map((id) => workspaceNavigation[id]!),
    { id: 'more', label: 'More', icon: 'more' },
  ];
}
export function isWorkspaceAllowed(
  workspace: string,
  permissions: readonly string[],
) {
  return (
    ['home', 'more', 'account'].includes(workspace) ||
    availableWorkspaces(permissions).some((module) => module.id === workspace)
  );
}
