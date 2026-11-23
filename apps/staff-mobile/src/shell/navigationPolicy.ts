export type Workspace = 'home' | 'more' | 'account' | 'delivery';
export type NavigationItem = {
  id: Workspace;
  label: string;
  icon: 'home' | 'more' | 'account' | 'delivery';
};

export function navigationFor(
  permissions: readonly string[],
): NavigationItem[] {
  return [
    { id: 'home', label: 'Today', icon: 'home' },
    ...(permissions.includes('delivery.update')
      ? [{ id: 'delivery', label: 'Deliveries', icon: 'delivery' } as const]
      : []),
    { id: 'more', label: 'More', icon: 'more' },
  ];
}

export function isWorkspaceAllowed(
  workspace: string,
  permissions: readonly string[],
) {
  return (
    ['home', 'more', 'account'].includes(workspace) ||
    (workspace === 'delivery' && permissions.includes('delivery.update'))
  );
}
