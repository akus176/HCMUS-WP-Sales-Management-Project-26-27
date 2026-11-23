import { permittedModules } from './permissions';

export type Workspace =
  | 'home'
  | 'more'
  | 'account'
  | 'barista'
  | 'cashier'
  | 'inventory'
  | 'delivery'
  | 'administration';
export type NavigationItem = {
  id: Workspace;
  label: string;
  icon:
    | 'home'
    | 'orders'
    | 'stock'
    | 'more'
    | 'account'
    | 'cup'
    | 'approve'
    | 'delivery';
};
export const workspaceNavigation: Record<string, NavigationItem> = {
  barista: { id: 'barista', label: 'Preparation', icon: 'cup' },
  cashier: { id: 'cashier', label: 'Orders', icon: 'orders' },
  inventory: { id: 'inventory', label: 'Stock', icon: 'stock' },
  delivery: { id: 'delivery', label: 'Deliveries', icon: 'delivery' },
  administration: { id: 'administration', label: 'Staff', icon: 'account' },
};

export function navigationFor(
  permissions: readonly string[],
): NavigationItem[] {
  const modules = permittedModules(permissions);
  const broad = modules.length > 2;
  // Operational workspaces have their own pages; More is reserved for Account.
  // Staff administration is reached from Account instead of an overflow list.
  const primary = ['cashier', 'inventory', 'barista', 'delivery'].filter((id) =>
    modules.some((module) => module.id === id),
  );
  return [
    { id: 'home', label: broad ? 'Today' : 'Home', icon: 'home' },
    ...primary.map((id) => workspaceNavigation[id]!),
    { id: 'more', label: 'More', icon: 'more' },
  ];
}

export function isWorkspaceAllowed(
  workspace: Workspace,
  permissions: readonly string[],
) {
  return (
    ['home', 'more', 'account'].includes(workspace) ||
    permittedModules(permissions).some((m) => m.id === workspace)
  );
}
