import type { ViewStyle } from 'react-native';
import { mobileTokens } from './designTokens';

/** Working records use the same restrained task surfaces across workspaces. */
export const recordRow: ViewStyle = {
  backgroundColor: mobileTokens.color.surface,
  borderRadius: mobileTokens.shape.card,
  padding: mobileTokens.spacing.md,
  gap: mobileTokens.spacing.sm,
};
