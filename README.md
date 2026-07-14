# MangaArr - Manga Download Manager

A fork of Readarr, reimagined for manga. Downloads manga volumes from scanlation sites (MangaDex), torrent indexers, and Usenet — then organizes them as properly named CBZ files for Komga/Kavita.

## Why?

Mylar3 is a comic book downloader forced to handle manga. It doesn't work well. MangaArr is built manga-first:
- **Volume-level downloads** (not chapter-by-chapter)
- **MangaDex integration** (free API, no key needed)
- **Proper CBZ naming** (`Berserk - Vol.042.cbz`)
- **Komga/Kavita integration** (auto library refresh)
- **Prowlarr support** (torrent + Usenet via existing arr infrastructure)

## Architecture

Forked from [Readarr](https://github.com/Readarr/Readarr), adapted for manga:

| Component | Readarr | MangaArr |
|---|---|---|
| Domain | Author → Book → Edition | MangaSeries → Volume → Chapter |
| Metadata | Goodreads/OpenLibrary | MangaDex / AniList |
| Indexers | Torrent/Usenet | MangaDex + Torrent + Usenet |
| Output | EPUB/PDF | CBZ |
| Naming | `{Author} - {Book}` | `{Series} - Vol.{Number}` |

## Sources

- **MangaDex** — Primary source (free API, volume-level filtering)
- **Prowlarr** — Torrent + Usenet indexers
- **AniList** — Metadata enrichment

## Status

🚧 Early development — forked from Readarr, domain adaptation in progress.

## License

GPL-3.0 (same as Readarr)
