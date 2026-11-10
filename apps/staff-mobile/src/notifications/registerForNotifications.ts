export function registerForNotifications<T>(options: {
  hasPermission: () => Promise<boolean>;
  readToken: (nativeToken?: T) => Promise<string>;
  listen: (onChange: (token: T) => void) => { remove: () => void };
  sync: (token: string) => Promise<void>;
  remove: () => Promise<void>;
}) {
  let stopped = false;
  let revision = 0;
  let subscription: { remove: () => void } | undefined;
  async function sync(nativeToken?: T) {
    const current = ++revision;
    const token = await options.readToken(nativeToken);
    if (!stopped && current === revision) await options.sync(token);
  }
  const ready = (async () => {
    const permitted = await options.hasPermission();
    if (stopped) return;
    if (!permitted) {
      await options.remove();
      return;
    }
    subscription = options.listen(
      (token) => void sync(token).catch(() => undefined),
    );
    await sync();
  })().catch(() => undefined);
  return {
    ready,
    stop() {
      stopped = true;
      subscription?.remove();
    },
  };
}
