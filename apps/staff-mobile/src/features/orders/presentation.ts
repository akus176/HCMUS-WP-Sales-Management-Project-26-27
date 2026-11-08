import { StyleSheet } from 'react-native';
import { mobileTokens } from '../../shell/designTokens';

export const money = (value: number | string) => Number(value).toFixed(2);
export const orderStyles = StyleSheet.create({
  card: {
    borderWidth: 2,
    borderColor: mobileTokens.color.border,
    borderRadius: mobileTokens.shape.card,
    padding: mobileTokens.spacing.md,
    gap: mobileTokens.spacing.sm,
  },
  input: {
    minHeight: 48,
    borderWidth: 2,
    borderColor: mobileTokens.color.border,
    borderRadius: mobileTokens.shape.control,
    padding: mobileTokens.spacing.sm,
    color: mobileTokens.color.foreground,
    backgroundColor: mobileTokens.color.background,
    fontSize: mobileTokens.type.body,
  },
});
