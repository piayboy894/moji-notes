# Notebook appearance

One shared catalogue generates the native Windows and Android pickers: 16 solid colours, 12 gradients and 36 Lucide icons. Run `python3 tools/generate-notebook-appearance.py` and `python3 tools/generate-icons.py` after changing the JSON catalogue.

The soft gradient selection draws on the public [WebGradients collection](https://webgradients.com/) (Winter Neva, Warm Flame, Juicy Peach, Lavender Mist, Cotton Candy, Mint, Sunny Morning, Paper and Quiet Violet) and original muted green/coastal/sunset pairs. Colours are rendered locally; the app does not connect to a palette service. Both renderers lightly lift dark gradient endpoints to keep the book title readable. The user's two stored colour values remain unchanged.

The Lucide vector artwork and its ISC licence are bundled in `assets/icons`; no system emoji or icon font is used. `colorEnd` is optional, so old notebooks retain their previous solid covers. Names and modes are independent of colour and icon selection.
