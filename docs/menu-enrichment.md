# Corner Cafe menu enrichment

The Windows demo menu can be expanded from 20 to 28 items with six new drinks and
two pastries. Existing items receive fuller descriptions; existing prices,
activation states, stock balances, and order history are retained.

| New item | Category | Regular / single | Large |
| --- | --- | ---: | ---: |
| Flat White | Espresso Bar | 54,000 VND | 64,000 VND |
| Cortado | Espresso Bar | 48,000 VND | — |
| Espresso Tonic | Cold Coffee | 62,000 VND | 74,000 VND |
| Salted Cream Cold Brew | Cold Coffee | 70,000 VND | 82,000 VND |
| Strawberry Matcha Latte | Tea & Matcha | 72,000 VND | 84,000 VND |
| Mango Passion Tea | Tea & Matcha | 64,000 VND | 74,000 VND |
| Chocolate Croissant | Bakery | 48,000 VND | — |
| Basque Cheesecake | Bakery | 65,000 VND | — |

These are demo prices. All 13 new size variants have recipes, and the six new
ingredients receive opening demo stock with a manager-attributed adjustment.
Rerunning enrichment preserves existing stock, prices, activation states, and
customizations of the added items.

## Apply to the local Windows demo catalog

The database must already contain the Windows demo categories (`Espresso Bar`,
`Cold Coffee`, `Tea & Matcha`, `Bakery`, and `Seasonal`) and an active manager.
This optional enrichment is separate from the midterm `demo-data` fixture.

From WSL/Linux with Python 3.9 or newer and Docker Compose:

```bash
docker compose up --detach --wait api
python3 scripts/storage/import-unsplash-photos.py
docker compose run --rm menu-enrichment
```

The importer downloads the selected Unsplash JPEGs to a temporary directory outside
the repository, uploads them to `menu-images`, verifies their stored byte digests,
and removes the temporary downloads. It restores the complete set of 27 pictures:
the 12 enrichment pictures and Unsplash replacements for all 15 original pictures,
including the unknown-item fallback and café scenes used by the design pages.
It refreshes those named keys on each explicit run. A teammate can populate an
empty storage volume using this importer without a copy of another person's bucket.
The source manifest and photographer credits are checked into the repository;
picture files are not.

Run `python3 scripts/verify-menu-photo-import.py` to check this workflow against
a new disposable storage volume. It verifies menu and design-page references,
downloads every photo, checks anonymous access restrictions, and verifies that
replacement objects and picture bytes survive initialization and container
recreation. The check removes only its own temporary services, volume, and
credentials file.

Rebuild and relaunch WinUI to pick up the expanded photo mappings. Refresh the
Counter or Menu workspace to see the database additions. Accented `Café Latte`
and unaccented `Cafe Latte` both resolve to the latte photo.

## Unsplash photograph credits

All selected photographs were available under the [Unsplash License](https://unsplash.com/license)
when selected. The exact download URLs and product assignments are in
[`unsplash-menu-photos.json`](../scripts/storage/unsplash-menu-photos.json).

| Storage key | Photographer | Source |
| --- | --- | --- |
| `unsplash-cortado.jpg` | Elijah Crouch | [Cortado with latte art](https://unsplash.com/photos/a-latte-with-foam-art-and-coffee-beans-Ymo7AnwmRDs) |
| `unsplash-espresso-tonic.jpg` | Felix Cha | [Espresso tonic](https://unsplash.com/photos/PXDxKz3uPqY) |
| `unsplash-strawberry-matcha.jpg` | Lala Azizli | [Iced matcha with berry syrup](https://unsplash.com/photos/iced-matcha-latte-with-berry-syrup-_fL77qBNou4) |
| `unsplash-cream-cold-brew.jpg` | Cristian Guillen | [Iced coffee with cream](https://unsplash.com/photos/a-tall-glass-of-iced-coffee-with-cream-utdcuLvaOW0) |
| `unsplash-mango-tea.jpg` | Malcolm Broström | [Mango tea](https://unsplash.com/photos/mango-tea-in-a-plastic-cup-set-on-a-tree-xFs5GSX7g3E) |
| `unsplash-chocolate-croissant.jpg` | Madeline Liu | [Chocolate croissant](https://unsplash.com/photos/chocolate-croissant-dusted-with-cocoa-powder-on-a-white-plate--NOgzXhGJpc) |
| `unsplash-basque-cheesecake.jpg` | kimia kazemi | [Basque cheesecake](https://unsplash.com/photos/slice-of-burnt-basque-cheesecake-on-wooden-tray-KoYk0JGAk3E) |
| `unsplash-almond-croissant.jpg` | Ngo Ngoc Khai Huyen | [Almond croissants](https://unsplash.com/photos/croissants-with-almonds-on-top-of-them-on-a-tray-6TOCyFfDN9Q) |
| `unsplash-blueberry-muffin.jpg` | Elena Taranukhina | [Blueberry muffins](https://unsplash.com/photos/blueberry-muffins-in-a-muffin-tin-on-a-cutting-board-5p6pM1LlLk4) |
| `unsplash-caramel-latte.jpg` | Tin Ma | [Caramel coffee](https://unsplash.com/photos/iced-coffee-with-cream-and-caramel-on-a-wooden-coaster-EdDf35V696k) |
| `unsplash-honey-lemon-tea.jpg` | Shreevardhan Iyer | [Honey lemon green tea](https://unsplash.com/photos/a-glass-of-tea-with-a-spoon-hVoBLxLQEzY) |
| `unsplash-mocha.jpg` | Ibrahim Rifath | [Chocolate mocha](https://unsplash.com/photos/chocolate-mocha-serve-in-glass-mug-Edmcnx3LLsM) |
| `espresso.jpg` | Metin Ozer | [espresso](https://unsplash.com/photos/9xdv75nryc0) |
| `cappuccino.jpg` | Alexandr Popadin | [cappuccino](https://unsplash.com/photos/-gX-22QSSW0) |
| `cold-brew.jpg` | Yazid N | [cold brew](https://unsplash.com/photos/MxVNVFI_q2A) |
| `iced-coffee.jpg` | Nadia Valko | [iced coffee](https://unsplash.com/photos/wWmQEoFK9gs) |
| `flat-white.jpg` | Harris Vo | [flat white](https://unsplash.com/photos/JpQGEArmG0I) |
| `latte.jpg` | Jeremy Yap | [latte](https://unsplash.com/photos/jn-HaGWe4yw) |
| `matcha.jpg` | Cody Chan | [matcha](https://unsplash.com/photos/Oog-4Ox0rv8) |
| `banana-bread.jpg` | Wouter Supardi Salari | [banana bread](https://unsplash.com/photos/7RzeewJzFUI) |
| `chai.jpg` | EADWYNE | [chai](https://unsplash.com/photos/QEkKvtzL-Pc) |
| `iced-tea.jpg` | Tkhao Khoang | [iced tea](https://unsplash.com/photos/IvyJqrkq1Zc) |
| `croissant.jpg` | Hannah Fleming-Hlll | [croissant](https://unsplash.com/photos/fc8y5qpBKWQ) |
| `cafe-bar.jpg` | Thoriq Shobih | [cafe bar](https://unsplash.com/photos/CSb5RX-shH8) |
| `cafe-room.jpg` | Kris Tian | [cafe room](https://unsplash.com/photos/q1SgzwmvR1s) |
| `hero-cafe.jpg` | Toxic Smoker | [hero cafe](https://unsplash.com/photos/oBxlJew0WXs) |
| `bakery.jpg` | Honney Artkongharn | [bakery](https://unsplash.com/photos/OBb1_YcelAQ) |

The pictures illustrate the corresponding drink or pastry style. They are sourced
stock photographs, not photographs taken at Corner Cafe.
