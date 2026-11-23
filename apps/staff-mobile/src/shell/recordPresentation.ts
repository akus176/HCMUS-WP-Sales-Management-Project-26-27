import type { ViewStyle } from 'react-native';
import { mobileTokens } from './designTokens';

/** Open, ruled records share the same paper treatment as Preparation. */
export const recordRow: ViewStyle = {
  backgroundColor: mobileTokens.color.background,
  borderColor: mobileTokens.color.border,
  borderBottomWidth: 1,
  paddingVertical: mobileTokens.spacing.md,
  gap: mobileTokens.spacing.sm,
};
