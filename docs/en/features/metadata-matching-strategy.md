# Metadata Matching Strategy Design Document

## 1. Goals and Scope

### 1.1 Goals
Transform **unstructured text** from user-local folder/file names into **structured search keywords** recognizable by the Bangumi API, and **accurately match** the correct anime entry in returned results.

### 1.2 Scope
- **Input**: Folder names (primary source), video file names (fallback source).
- **Output**: Bangumi `subject_id` (or confirmation of no match).
- **Limitation**: Targeting anime; movies, TV dramas, and documentaries are not supported.

---

## 2. Input Sources and Priority

### 2.1 Priority

| Priority | Source | Description |
|----------|--------|-------------|
| **P1** | Folder name | Most users organize folders by anime title; information is cleanest. |
| **P2** | Parent folder name | If folder name is meaningless (e.g., "Season 1", "01-25"), fall back to parent. |
| **P3** | Video file name | If no valid folder name exists in the entire path, use the first video file name. |

### 2.2 Decision Logic

```
keyword = ExtractFolderName(folderPath)
if IsMeaningless(keyword):
    keyword = ExtractParentFolderName(folderPath)
if IsMeaningless(keyword):
    keyword = ExtractFromFirstVideoFile(folderPath)
```

**"Meaningless" criteria**:
- Pure numbers (e.g., `01`, `1080p`).
- Common season identifiers (`Season 1`, `S01`, `第1季`, `TV`).
- Common episode ranges (`01-25`, `E01-E25`, `全25集`).
- Less than 2 characters after cleaning.

---

## 3. Cleaning Strategy (Noise Reduction)

### 3.1 Cleaning Pipeline

A **multi-stage pipeline**; each stage resolves one category of noise:

```
Raw text
  → Stage 1: Remove wrapper markers (brackets, parentheses, angle brackets)
  → Stage 2: Remove technical tags (resolution, codec, audio)
  → Stage 3: Remove release info (fansub group, torrent site, datestamp)
  → Stage 4: Remove episode/season identifiers
  → Stage 5: Remove meaningless particles/symbols
  → Stage 6: Normalize (fullwidth to halfwidth, collapse spaces)
  → Output keyword
```

### 3.2 Stage Details

#### Stage 1: Remove Wrapper Markers

User naming commonly uses brackets/parentheses to wrap metadata; these blocks need to be identified and stripped:

| Pattern | Example | After processing |
|---------|---------|------------------|
| `[...]` | `[GM-Team][Attack on Titan]` | `Attack on Titan` |
| `(...)` | `(1080p)(x265)` | Empty (if only technical tags remain, proceed to Stage 2) |
| `【...】` | `【喵萌奶茶屋】` | Empty |
| `〖...〗` | `〖BDrip〗` | Empty |
| `<...>` | `<NC-Raws>` | Empty |

**Rule**:
- If the block contains **no Chinese characters or Japanese kana**, treat it as pure metadata marker and strip directly.
- If the block contains **Chinese/Japanese**, it may be the title itself (e.g., `[Attack on Titan]`), preserve inner text and proceed to next stage.

#### Stage 2: Remove Technical Tags

| Category | Regex/Keyword examples | Description |
|----------|------------------------|-------------|
| Resolution | `1080p`, `720p`, `4K`, `2160p`, `1920x1080` | Case-insensitive |
| Video codec | `x264`, `x265`, `HEVC`, `AVC`, `H264`, `H265`, `10bit` | |
| Audio format | `AAC`, `FLAC`, `AC3`, `DTS`, `5.1ch` | |
| Source | `BDRip`, `WEBRip`, `HDTV`, `BluRay`, `DVDRip`, `UHD`, `Remux` | |
| File format | `.mp4`, `.mkv`, `.avi` | Remove extension |
| Subtitle | `GB`, `BIG5`, `CHS`, `CHT`, `简日`, `繁日` | |
| Other tech | `V2`, `fix`, `v2`, `repack` | Version correction markers |

#### Stage 3: Remove Release Info

| Category | Examples | Description |
|----------|----------|-------------|
| Fansub group | `喵萌奶茶屋`, `DMG`, `天香字幕社`, `幻之字幕组` | Hard to exhaust; uses heuristic rules |
| Torrent site | `动漫花园`, `acg.rip`, `nyaa` | |
| Datestamp | `2023.01`, `20230115` | 4, 6, or 8 digit number combinations |
| CRC32 | `[A1B2C3D4]` | 8-digit hex in brackets |

**Fansub group heuristic detection**:
- Located within outermost brackets/parentheses.
- Contains no common anime title keywords (e.g., `的`, `之`, `物语`).
- Length usually 2-8 Chinese characters or contains English letter abbreviations.
- Common suffixes: `字幕组`, `字幕社`, `汉化组`, `压制组`.

#### Stage 4: Remove Episode/Season Identifiers

| Type | Regex examples | Description |
|------|---------------|-------------|
| Episode range | `01-25`, `E01-E25`, `第01-25话`, `全25话`, `1-25集` | |
| Single episode | `第01话`, `EP01`, `E01`, `01` | If context determines episode rather than title |
| Season | `Season 1`, `S01`, `第1季`, `S1`, `II`, `III`, `2nd` | |
| Volume | `Vol.1`, `Vol 1`, `第1卷` | |
| SP/OVA | `SP`, `OVA`, `OAD`, `特典`, `Bonus` | Usually preserved as may be part of title |

**Special handling**:
- `SP` / `OVA` **not directly removed**, because some anime titles genuinely contain these words (e.g., "S" in "A Certain Scientific Railgun S" is not a season indicator). Only remove when appended as suffix after title.
- `第二季`, `2期`, etc. — **explicit season markers** must be removed, otherwise Bangumi search matches the first season.

#### Stage 5: Remove Meaningless Particles and Symbols

- Leading/trailing `-`, `_`, `~`, `·`, `・`, `「`, `」`, `『`, `』`.
- Extra spaces, tabs, newlines.

#### Stage 6: Normalize

| Operation | Example |
|-----------|---------|
| Fullwidth letters/numbers to halfwidth | `ＡＢＣ` → `ABC`, `１２３` → `123` |
| Fullwidth space to halfwidth space | `　` → ` ` |
| Collapse consecutive spaces | `Attack  on  Titan` → `Attack on Titan` |
| Trim leading/trailing spaces | ` Titan ` → `Titan` |

### 3.3 Cleaning Examples

| Original folder name | After stages | Description |
|---------------------|--------------|-------------|
| `[GM-Team][Attack on Titan][01-25][1080p][x265]` | `Attack on Titan` | Standard fansub format; ideal after cleaning. |
| `【喵萌奶茶屋】Bocchi the Rock! Season 2 [1080p][AAC]` | `Bocchi the Rock` | `!` preserved, `Season 2` removed. Note Bangumi title is `Bocchi the Rock!` without "Season 2". |
| `(2023.10) Frieren: Beyond Journey's End 01-28 BDRip` | `Frieren: Beyond Journey's End` | Datestamp, episodes, source all removed. |
| `Season 1` | *Fallback to parent folder* | Meaningless, triggers P2 fallback. |
| `Demon Slayer: Entertainment District Arc Vol.1-11` | `Demon Slayer: Entertainment District Arc` | Volume removed, arc name preserved. |
| `[NC-Raws] Jujutsu Kaisen Hidden Inventory / Premature Death` | `Jujutsu Kaisen Hidden Inventory / Premature Death` | Fansub group removed, dual title preserved. |
| `EVANGELION_3.0+1.0_THRICE_UPON_A_TIME` | `EVANGELION 3.0+1.0 THRICE UPON A TIME` | Underscores to spaces. |
| `IDOLM@STER` | `IDOLM@STER` | Special symbol `@` is part of title; preserved. |

---

## 4. Matching and Ranking Strategy

### 4.1 Bangumi Search API Behavior

`POST /v0/search/subjects` with `sort=match` returns a relevance-ranked result list. Bangumi's relevance algorithm is based on title, alias, and summary text matching, but is **imperfect**:

- Same-name or highly similar works may be mixed in ranking.
- Movie, TV, and OVA versions may appear in the same result list.
- Some obscure works may have no `name_cn`, only Japanese name.

### 4.2 Auto-Matching Strategy (Auto Scrape)

Phase 1 **only supports auto-scraping, no manual dialog**. Must auto-decide within the API-returned candidate list, and **宁可跳过也不误匹配 (宁可 skip 也不 mis-match)**.

#### Strategy A: Type Filter (Basic)

Request body fixes `filter.type = [2]` to exclude books, games, and live-action entries.

#### Strategy B: Title Similarity Verification (Core)

Take Bangumi's Top1 result and calculate similarity with the cleaned keyword:

```
candidate = Bangumi.Search(keyword).FirstOrDefault()
if candidate == null: return null

similarity = CalculateSimilarity(keyword, candidate.Title)
if similarity < 0.5:
    return null  // Low confidence, silently skip
```

**Similarity algorithm**:
- Chinese/Japanese: Longest Common Subsequence (LCS) similarity = `LCS length / Max(keyword length, result title length)`.
- English: Case-insensitive and punctuation-ignored Levenshtein normalized similarity.

#### Strategy C: Year-Based Weighted Filter (Enhancement)

If cleaned keyword contains a **4-digit year** (e.g., `2023`), prioritize matching works near that year:

```
if keyword contains year:
    if abs(candidate.Date.Year - year) > 1:
        Confidence downgrade (acceptable but not strictly enforced)
```

Year mismatch **does not directly reject** (because some seasonal anime may span calendar years), but can be logged.

#### Strategy D: Short Word Ambiguity Rejection

Directly reject auto-matching when cleaned keyword is too short to avoid highly ambiguous words like "Fate" or "巨人":

```
if Chinese length < 3 or English length < 6:
    return null
```

---

## 5. Ambiguity Handling

### 5.1 Same Name, Different Seasons

**Problem**: `Attack on Titan` has TV Season 1, Season 2, Season 3 Part.1, Part.2, Final Season Part.1, Part.2, Part.3, etc. on Bangumi.

**Strategy**:
1. **Keyword contains season**: e.g., `Attack on Titan Season 3`, after cleaning preserves `Season 3`, Bangumi search itself ranks it higher.
2. **Folder name has no season**: e.g., `Attack on Titan`, auto-take Bangumi's earliest entry (usually Season 1). This phase has no manual correction entry; precise matching of subsequent seasons needs future version support.
3. **Season keyword mapping table** (optional enhancement):

| Common expression | Bangumi semantics |
|-------------------|-------------------|
| `第二季`, `2期`, `S2`, `Season 2` | Take second work |
| `剧场版`, `Movie`, `映画` | Take `type = 2` with `platform = 剧场版` |
| `OVA`, `OAD` | Take `platform = OVA` |
| `总集篇`, `Recap` | Take recap entry |

### 5.2 Same Name, Different Works

**Problem**: `White Album` refers to both the 1998 game and the 2009 TV anime.

**Strategy**:
- Auto mode fixes `filter.type = [2]` (anime), excluding games and books.
- If ambiguity remains (e.g., TV and OVA anime entries exist), prioritize TV version (usually `platform = "TV"` ranks higher or has more episodes).

### 5.3 Series vs. Standalone

**Problem**: Folder name is `Fate`, which could be `Fate/stay night`, `Fate/Zero`, `Fate/Grand Order`, etc.

**Strategy**:
- `Fate` after cleaning is still very short (2 characters), similarity verification marks it as low confidence, **not auto-adopted**.
- Silently skipped this phase; no metadata displayed.

---

## 6. Confidence and Thresholds

### 6.1 Auto-Scraping Adoption Conditions

Auto-scraping does not "adopt if there are results"; must satisfy **all** conditions simultaneously:

| Condition | Threshold | Description |
|-----------|-----------|-------------|
| Search returns non-empty | `candidates.Count > 0` | Bangumi returns at least one result. |
| Type is anime | `type == 2` | Fixed in request filter. |
| Title similarity | `>= 0.5` | Core gate to prevent mis-matching. |
| Non-short ambiguous word | Chinese `>= 3` chars / English `>= 6` chars | Rejects ambiguous short words like "Fate", "AB". |
| Year match (optional) | If keyword contains 4-digit year, Top1 year error `<= 1` | Auxiliary verification; mismatch does not directly reject. |

If any hard condition is not satisfied, auto-scraping **silently skips** and does not display any metadata on the card. Phase 1 has no manual remedy entry; skip means skip.

### 6.2 Confidence Levels (Phase 1 Simplified)

| Level | Condition | Behavior |
|-------|-----------|----------|
| **Pass** | Similarity >= 0.5 and non-short ambiguous | Auto-adopted, background silently completes, card refreshes cover. |
| **Reject** | Similarity < 0.5, or short ambiguous, or no results | Not adopted, not displayed, silently skipped. |

---

## 7. Fallback Strategies

### 7.1 Search Returns No Results

```
Bangumi.Search(keyword) returns empty
  → Try keyword simplification: remove "篇", "章", "季" suffixes and re-search
    Example: `Demon Slayer: Entertainment District Arc` → search `Demon Slayer`
  → Still no results → mark as "not found", log, no further retry
```

### 7.2 Detail Fetch Failure

```
Bangumi.GetDetail(id) fails (404 or network error)
  → Log
  → If 404: May be deleted Bangumi data; mark as "invalid"
  → If network error: Do not mark; next load still attempts to display existing cache
```

### 7.3 Batch Add Folder Strategy

`AddFolderBatchAsync` may add dozens of folders at once:

- Each folder's scraping task **executes serially** with 1s intervals to avoid concurrent Bangumi rate limiting.
- Does not block UI thread; background queue completes gradually.

---

## 8. Test Cases

The following typical naming scenarios must be covered:

| ID | Original folder name | Expected keyword | Expected Bangumi match |
|----|---------------------|------------------|------------------------|
| T01 | `[GM-Team][Attack on Titan][01-25][1080p]` | `Attack on Titan` | Attack on Titan (TV Season 1) |
| T02 | `【喵萌奶茶屋】Bocchi the Rock! Season 2` | `Bocchi the Rock` | Bocchi the Rock! (TV, note without "Season 2") |
| T03 | `Frieren: Beyond Journey's End 2023 BDRip` | `Frieren: Beyond Journey's End` | Frieren: Beyond Journey's End (TV 2023) |
| T04 | `Demon Slayer: Entertainment District Arc` | `Demon Slayer: Entertainment District Arc` | Demon Slayer: Entertainment District Arc (TV) |
| T05 | `Season 1` (parent: `Jujutsu Kaisen`) | `Jujutsu Kaisen` | Jujutsu Kaisen (TV) |
| T06 | `[NC-Raws] EVANGELION 3.0+1.0` | `EVANGELION 3.0+1.0` | Evangelion: 3.0+1.0 Thrice Upon a Time |
| T07 | `A Certain Magical Index III` | `A Certain Magical Index` | A Certain Magical Index (TV Season 1) — Note: III is cleaned; Bangumi search `A Certain Magical Index` Top1 is Season 1; this phase takes Season 1, no manual correction entry |
| T08 | `Fate` | `Fate` | **Low confidence, not auto-matched** |
| T09 | `Clannad` | `Clannad` | CLANNAD (TV, English case-insensitive) |
| T10 | `White Album 2` | `White Album 2` | White Album 2 (TV) |
| T11 | `Movie Violet Evergarden` | `Violet Evergarden` | Violet Evergarden: The Movie (platform match) |
| T12 | `Attack on Titan Final Season Final Chapters` | `Attack on Titan Final Season Final Chapters` | Attack on Titan Final Season Final Chapters (TV) |

---

## 9. Iteration Plan

| Phase | Content | Priority |
|-------|---------|----------|
| V1 | Implement basic cleaning (Stages 1-6) + strict auto-matching (similarity >= 0.5 + short word rejection + type filter) | P0 |
| V1.1 | Add year weighted filter to improve accuracy for year-containing naming scenarios | P1 |
| V1.2 | Add season keyword mapping table to reduce same-name different-season mis-matching | P1 |
| V2 | Introduce video file name fallback (P3 priority) to handle meaningless folder name scenarios | P2 |
