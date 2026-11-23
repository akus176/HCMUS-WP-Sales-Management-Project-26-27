/** Shared visual tokens for the coffee-shop mobile application. */
export const mobileTokens = {
  color: {
    background: '#faf8f4',
    surface: '#ffffff',
    foreground: '#302820',
    primary: '#6d4936',
    onPrimary: '#faf8f4',
    focus: '#302820',
    correction: '#aa2c2c',
    informational: '#154d70',
    highlight: '#f4ecd4',
    selectedBackground: '#eee8df',
    selectedForeground: '#6d4936',
    error: '#aa2c2c',
    errorBackground: '#f6e7e5',
    warning: '#5c4300',
    warningBackground: '#f4ecd4',
    info: '#154d70',
    infoBackground: '#eef3f6',
    disabledText: '#645b53',
    disabledBackground: '#e6e0d6',
    border: '#e6e0d6',
  },
  type: {
    caption: 13,
    body: 16,
    title: 16,
    display: 24,
    bodyFont: 'InterRegular',
    mediumFont: 'InterMedium',
    displayFont: 'InterSemiBold',
  },
  spacing: { xs: 4, sm: 8, md: 16, lg: 24, xl: 32 },
  shape: { control: 12, card: 12, sheet: 24, pill: 999 },
  elevation: {
    card: {
      shadowColor: '#302820',
      shadowOffset: { width: 0, height: 2 },
      shadowOpacity: 0.12,
      shadowRadius: 4,
      elevation: 2,
    },
  },
} as const;

export function contrastRatio(foreground: string, background: string): number {
  const luminance = (hex: string) => {
    const channels = hex.match(/[\da-f]{2}/gi);
    if (!channels || channels.length !== 3)
      throw new Error(`Expected a six-digit hex color, received ${hex}`);
    const linear = channels.map((channel) => {
      const value = parseInt(channel, 16) / 255;
      return value <= 0.04045
        ? value / 12.92
        : ((value + 0.055) / 1.055) ** 2.4;
    });
    return 0.2126 * linear[0]! + 0.7152 * linear[1]! + 0.0722 * linear[2]!;
  };
  const left = luminance(foreground);
  const right = luminance(background);
  return (Math.max(left, right) + 0.05) / (Math.min(left, right) + 0.05);
}
