import type { ReactNode } from 'react';
import { Text, View } from 'react-native';

import { configurationError, readMobileEnvironment } from './environment';

export function ConfigurationGate({
  children,
  readConfiguration = readMobileEnvironment,
}: {
  children: ReactNode;
  readConfiguration?: typeof readMobileEnvironment;
}) {
  try {
    readConfiguration();
  } catch {
    return (
      <View style={{ flex: 1, justifyContent: 'center', padding: 24 }}>
        <Text accessibilityRole="header">Configuration required</Text>
        <Text accessibilityRole="alert">{configurationError}</Text>
      </View>
    );
  }
  return children;
}
