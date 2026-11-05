import { useCallback, useState } from 'react';
import { useFocusEffect } from 'expo-router';

// Invalidate a retained route on blur, not just after its next focus render.
export function useRouteAccess() {
  const [access, setAccess] = useState({ focused: false, refreshKey: 0 });
  useFocusEffect(
    useCallback(() => {
      setAccess((previous) => ({
        focused: true,
        refreshKey: previous.refreshKey + 1,
      }));
      return () => setAccess((previous) => ({ ...previous, focused: false }));
    }, []),
  );
  return access;
}
