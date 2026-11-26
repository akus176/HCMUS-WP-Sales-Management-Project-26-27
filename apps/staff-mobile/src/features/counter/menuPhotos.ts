// Match the Windows Counter photo keys in the shared public menu bucket.
const photos: Record<string, string> = {
  americano: 'espresso',
  cappuccino: 'cappuccino',
  'flat white': 'flat-white',
  cortado: 'unsplash-cortado',
  'espresso tonic': 'unsplash-espresso-tonic',
  'cold brew': 'cold-brew',
  'salted cream cold brew': 'unsplash-cream-cold-brew',
  'vietnamese iced coffee': 'iced-coffee',
  'matcha latte': 'matcha',
  'strawberry matcha latte': 'unsplash-strawberry-matcha',
  'mango passion tea': 'unsplash-mango-tea',
  'honey lemon green tea': 'unsplash-honey-lemon-tea',
  'chai latte': 'chai',
  'peach iced tea': 'iced-tea',
  'butter croissant': 'croissant',
  'almond croissant': 'unsplash-almond-croissant',
  'chocolate croissant': 'unsplash-chocolate-croissant',
  'basque cheesecake': 'unsplash-basque-cheesecake',
  'banana bread': 'banana-bread',
  'house espresso': 'espresso',
  'cafe latte': 'latte',
  'café latte': 'latte',
  'vanilla latte': 'latte',
  'caramel macchiato': 'unsplash-caramel-latte',
  'salted caramel latte': 'unsplash-caramel-latte',
  mocha: 'unsplash-mocha',
  'cocoa oat latte': 'unsplash-mocha',
  'coconut cold brew': 'cold-brew',
  'peach green tea': 'iced-tea',
  'jasmine milk tea': 'chai',
  'strawberry frappe': 'iced-coffee',
  'chocolate frappe': 'iced-coffee',
  'mango smoothie': 'iced-tea',
  'blueberry muffin': 'unsplash-blueberry-muffin',
  'pumpkin spice latte': 'latte',
  'honey oat latte': 'latte',
};
export function menuPhoto(name: string): string | undefined {
  const configured = process.env.EXPO_PUBLIC_MENU_PHOTOS_BASE_URL;
  const base =
    configured ||
    (process.env.EXPO_PUBLIC_APP_ENV === 'emulator'
      ? 'http://10.0.2.2:9000/menu-images/'
      : undefined);
  if (!base) return undefined;
  try {
    const url = new URL(base);
    if (
      !['http:', 'https:'].includes(url.protocol) ||
      url.username ||
      url.password ||
      url.search ||
      url.hash
    )
      return undefined;
    const key = name.replace(/^Midterm /i, '').toLowerCase();
    return `${url.href.replace(/\/$/, '')}/${photos[key] ?? 'cafe-bar'}.jpg`;
  } catch {
    return undefined;
  }
}
