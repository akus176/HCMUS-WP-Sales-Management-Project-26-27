import Svg, { Path } from 'react-native-svg';
import { mobileTokens } from './designTokens';

// Original vector paths from the approved mobile mockup.
const paths = {
  home: 'M3 10L12 3L21 10V21H15V14H9V21H3Z',
  orders: 'M6 3H18V21L15 19L12 21L9 19L6 21ZM9 8H15M9 12H15',
  stock: 'M3 7L12 3L21 7V17L12 21L3 17ZM3 7L12 11L21 7M12 11V21',
  more: 'M4 6H20M4 12H20M4 18H20',
  back: 'M15 5L8 12L15 19',
  chevron: 'M9 5L16 12L9 19',
  search: 'M16 16L21 21M18 10A8 8 0 1 1 2 10A8 8 0 1 1 18 10',
  filter: 'M3 5H21M7 12H17M10 19H14',
  check: 'M5 12L10 17L20 7',
  close: 'M6 6L18 18M18 6L6 18',
  account: 'M16 7A4 4 0 1 1 8 7A4 4 0 1 1 16 7M4 21V18C4 12 20 12 20 18V21',
  cup: 'M4 5H17V13C17 21 4 21 4 13ZM17 7H20C24 7 24 14 17 14M3 22H19',
  delivery:
    'M2 6H14V18H2ZM14 10H19L22 14V18H14M8 18A2 2 0 1 1 4 18A2 2 0 1 1 8 18M21 18A2 2 0 1 1 17 18A2 2 0 1 1 21 18',
  approve: 'M12 2L21 6V12C21 18 12 22 12 22C12 22 3 18 3 12V6ZM7 12L10 15L17 8',
  table: 'M4 5H20V13H4ZM6 13V21M18 13V21M2 5V15M22 5V15',
  arrow: 'M5 12H20M14 6L20 12L14 18',
  refresh: 'M20 7V3M20 7H16M20 7A9 9 0 1 0 21 15',
  wifi: 'M2 8C7 3 17 3 22 8M5 12C9 8 15 8 19 12M9 16C11 14 13 14 15 16M12 20H12.01',
} as const;
export type ShellIconName = keyof typeof paths;
export function ShellIcon({
  name,
  size = 24,
  color = mobileTokens.color.foreground,
}: {
  name: ShellIconName;
  size?: number;
  color?: string;
}) {
  return (
    <Svg
      width={size}
      height={size}
      viewBox="0 0 24 24"
      accessible={false}
      importantForAccessibility="no"
    >
      <Path
        d={paths[name]}
        fill="none"
        stroke={color}
        strokeWidth={1.65}
        strokeLinecap="round"
        strokeLinejoin="round"
      />
    </Svg>
  );
}
