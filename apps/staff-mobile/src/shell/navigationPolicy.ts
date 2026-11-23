import { permittedModules } from './permissions';

export type Workspace =
  | 'home'
  | 'more'
  | 'account'
  | 'barista'
  | 'cashier'
  | 'inventory'
  | 'approvals'
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
  barista: { id: 'barista', label: 'Queue', icon: 'cup' },
  cashier: { id: 'cashier', label: 'Orders', icon: 'orders' },
  inventory: { id: 'inventory', label: 'Stock', icon: 'stock' },
  approvals: { id: 'approvals', label: 'Approvals', icon: 'approve' },
  delivery: { id: 'delivery', label: 'Deliveries', icon: 'delivery' },
  administration: { id: 'administration', label: 'Staff', icon: 'account' },
};

export function navigationFor(
  permissions: readonly string[],
): NavigationItem[] {
  const modules = permittedModules(permissions);
  const broad = modules.length > 2;
  const preferred = [
    'cashier',
    'inventory',
    'barista',
    'delivery',
    'approvals',
    'administration',
  ];
  const primary = broad
    ? preferred.filter((id) => modules.some((m) => m.id === id)).slice(0, 2)
    : modules.map((m) => m.id);
  return [
    { id: 'home', label: broad ? 'Today' : 'Home', icon: 'home' },
    ...primary.map((id) => workspaceNavigation[id]!),
    broad
      ? { id: 'more', label: 'More', icon: 'more' }
      : { id: 'account', label: 'Account', icon: 'account' },
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
