# GridLine Steam integration and game information plan

GridLine will keep the rig usable offline and add optional Steam account sync. Owners curate the list guests see. Steam genre tags can help an owner search for candidates, but a tag never adds a title to Play or Library.

## 1. Merge Steam ownership with local discovery

- Keep scanning the Steam client libraries and game manifests on every configured drive. Continue to support standalone installs and manual executables.
- Add **Settings → Steam → Sync owned games**. For the first implementation, accept a SteamID64 and a user Web API key. Validate Valve's public API route for `IPlayerService/GetOwnedGames` with a standard user key and test its visibility limits before shipping. Offer a clear retry path when Steam does not return a visible library. Steam OpenID can verify a SteamID64 if a browser sign-in flow is added later. It does not grant ownership access by itself. [Steam ownership API](https://partner.steamgames.com/doc/webapi/IPlayerService#GetOwnedGames) · [Steam authentication](https://partner.steamgames.com/doc/features/auth) · [Web API terms](https://steamcommunity.com/dev/apiterms)
- Show an import preview with matched titles and an optional owner review list for unmatched titles. Match by Steam App ID against the built-in driving catalog and any titles the owner previously approved. Use the existing Circuit, Rally, Arcade, Trucking, and Bus & Transit categories. Reject automatic matches for DLC, demos, and tools. Let owners explicitly approve an unlisted driving game and select its category.
- Track **installed**, **owned**, **manually added**, and **hidden** independently. Existing hidden App IDs take priority after import, rescan, and restart. Keep manual games and installs available if Steam sync fails. Treat shared access, private games, and free-to-play titles as separate cases and verify what Steam actually returns. Keep the previous snapshot on an error or an ambiguous empty response.
- Show **Play** for a detected install, **Install via Steam** for a confirmed owned title once the client handoff is verified, and **View on Steam** for a store listing. Use **Locate Game** for manual executables. Rescan when the user returns from Steam and provide a manual refresh so GridLine can confirm when the install finishes.
- Keep the key in Windows-protected storage for the current user. Request Steam data only after the owner opts in, never log the key, and keep it out of settings exports. **Disconnect Steam** clears the saved credential and offers to clear the cached ownership snapshot. Local discovery requires no account connection.

## 2. Add sources by field

| Content | First release | Later enhancement |
| --- | --- | --- |
| Identity, summary, release, developer, publisher | Match by App ID. Keep a small versioned driving catalog with independently written summaries, release facts, and official links. Valve's documented app list can validate identity and app type if needed. Link to Steam for store detail. [Steam app list](https://partner.steamgames.com/doc/webapi/IStoreService#GetAppList) | Add a provider for richer descriptions and release information. IGDB is an option after checking attribution and usage terms. Keep its application secret in an optional self-hosted or managed service. [IGDB API](https://api-docs.igdb.com/) |
| Hero, cover, tile, screenshots | Prefer the owner's artwork overrides, then the Steam client's local cache and a themed fallback. Store hero, portrait, and landscape slots separately. Fit images to their aspect ratio. | Offer an optional SteamGridDB artwork picker with a user key and creator credit where available. Check usage and redistribution terms before enabling any downloadable provider or bundling its assets. [SteamGridDB API](https://www.steamgriddb.com/api/v2) |
| Trailers | Show a **Watch Trailer** link to a curated Steam or official publisher video page. Use an official poster where its usage permits. Open the link on demand. [Steam trailers](https://partner.steamgames.com/doc/store/trailer) | Add in-app playback only where the provider supports embedding. Offer manual play, mute, and a controller-friendly close action. |
| Ratings | Pull the overall positive percentage and review count from Valve's documented review summary. Label it **Steam user reviews** with the count and fetch date. Show unavailable when Steam has no usable summary. [Steam reviews API](https://partner.steamgames.com/doc/webapi/IUserReviewsService) | Add a recent score only if we can verify its window. Add critic ratings through a licensed source and label them separately. |
| Compatibility and rig setup | Curate wheel and force feedback support, VR, telemetry, and companion app links from official documentation. Mark unverified fields as unknown. Include publisher help and a PCGamingWiki link where relevant. [PCGamingWiki content terms](https://www.pcgamingwiki.com/wiki/PCGamingWiki:Copyrights) | Add user-maintained notes and confirmed compatibility by game version or hardware. |
| SteamDB, pricing, patch history | Link to SteamDB by App ID for charts, packages, price history, and builds. Link to the Steam store for current regional pricing. SteamDB has no public API and prohibits automated scraping. [SteamDB FAQ](https://steamdb.info/faq/) | Consider a price or update feed only after identifying a supported source with suitable terms. |

Use a separate provider adapter for each network source. Keep the Steam Store `appdetails` route, if evaluated, isolated as an undocumented dependency. The initial release can use curated metadata and official links without relying on that route. Store source, retrieval time, and locale with fetched values, and let manual edits win when sources disagree.

## 3. Build the fullscreen game details experience

Keep Library centered on the selected artwork, title, status, and primary action. Expand **Game Details** into a controller-friendly sheet with the summary, game facts, Steam score, screenshots, and trailer action. Put **Locate**, **Hide**, and provider links in the details sheet. Guest-facing browsing should surface titles approved for the rig and ready to play. Owner settings control import, visibility, artwork overrides, and install actions.

Start by reading the local catalog and last good cache. Fetch optional data off the UI thread for the selected title or on explicit refresh. Cancel obsolete requests when the selection changes, bound image sizes and concurrent downloads, and back off when a provider throttles requests. Set initial cache targets of one day for reviews and one week for descriptive metadata, subject to provider terms. Store metadata by provider, App ID, and locale so a stale or unavailable source does not block a launch.

## 4. Deliver in three increments

| Increment | Done when |
| --- | --- |
| **Steam sync and curation** | An owned uninstalled driving game appears in the preview, an installed game merges by App ID, an unrelated game stays out of Play and Library, and hidden games stay hidden after sync. Test private or partial results, missing Steam, Steam Families, multiple install drives, offline use, and old settings migration. |
| **Game details and reviews** | The fullscreen details sheet is usable by controller and mouse. A driving game can show its Steam score, sourced facts, and official trailer link. Offline, delisted, or incomplete titles still open promptly and installed games still launch. |
| **Optional providers** | SteamGridDB and richer metadata can be enabled independently. Attribution, timeouts, cache limits, stale results, and missing art have been verified. The standard local setup remains usable without a GridLine account or paid service. |
