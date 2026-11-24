import type { ReactNode } from 'react';
import { Text, ScrollView } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { mobileTokens } from '../shell/designTokens';

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
      <SafeAreaView
        style={{ flex: 1, backgroundColor: mobileTokens.color.background }}
      >
        <ScrollView
          contentContainerStyle={{
            flexGrow: 1,
            justifyContent: 'center',
            padding: 24,
            gap: 16,
          }}
        >
          <Text
            accessibilityRole="header"
            style={{
              color: mobileTokens.color.foreground,
              fontSize: mobileTokens.type.title,
            }}
          >
            Configuration required
          </Text>
          <Text
            accessibilityRole="alert"
            style={{
              color: mobileTokens.color.error,
              fontSize: mobileTokens.type.body,
            }}
          >
            {configurationError}
          </Text>
        </ScrollView>
      </SafeAreaView>
    );
  }
  return children;
}
