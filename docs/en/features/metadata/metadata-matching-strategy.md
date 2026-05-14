# Metadata Matching Strategy Design Document

## 1. Purpose

This document defines how AniNest turns noisy local anime folders into a safe Bangumi lookup plan.

The goal is not "find something vaguely related". The goal is:

- build a structured **keyword plan** from library data
- evaluate Bangumi candidates conservatively
- prefer a **no-match** over a wrong match
- keep runtime behavior aligned with the actual background worker flow

The current implementation has already gone through real-library validation, so this document reflects both intended design and observed runtime constraints.

---

## 2. Scope

### 2.1 In Scope

- folder-name based metadata lookup for anime
- fallback from folder name to parent folder name and video file name
- season-aware and movie-aware keyword extraction
- strict candidate evaluation
- runtime constraints that affect matching quality

### 2.2 Out of Scope

- manual search UI
- multi-provider ranking
- per-episode metadata parsing
- low-confidence "best effort" auto-adoption

### 2.3 Output Contract

The matcher returns a plan rather than a single string:

```csharp
public sealed record MetadataKeywordPlan(
    string PrimaryKeyword,
    string? SeasonAwareKeyword,
    string? SimplifiedKeyword,
    string BaseTitle,
    int? SeasonNumber,
    int? YearHint,
    bool IsAmbiguousShortKeyword,
    bool IsMovieLike);
```

The provider returns either:

- a high-confidence `sourceId`
- or a classified failure such as `NoMatch`

---

## 3. Matching Philosophy

### 3.1 Conservative by Design

Auto matching follows one rule:

> only adopt a result when the system has a good reason to trust it

That means:

- short ambiguous queries are rejected
- wrong-season candidates are penalized heavily
- movie-like folders should not silently fall back to TV entries
- low-confidence ties should end as `NoMatch`

### 3.2 Why This Matters

Wrong metadata is usually worse than missing metadata:

- it breaks trust in the library
- it pollutes refresh paths through bad `sourceId` reuse
- it is harder to notice and correct later

For Phase 1, correctness beats coverage.

---

## 4. Inputs and Priority

### 4.1 Input Sources

The matcher builds plans from these sources in priority order:

1. folder name
2. parent folder name
3. first video file name

### 4.2 Fallback Rules

```text
candidate = folder name
if candidate is meaningless:
    candidate = parent folder name
if candidate is meaningless:
    candidate = first video file name
```

### 4.3 Important Runtime Constraint

This fallback only works if the runtime actually passes `VideoFiles` into `MetadataFolderRef`.

That matters in practice:

- `LibraryAppService` already builds `MetadataFolderRef` with real `videoFiles`
- `MetadataWorker` must also rehydrate real `videoFiles` before calling the provider

Real-world debugging showed that if the worker passes `Array.Empty<string>()`, then:

- file-name fallback silently disappears
- folders like `咒术回战 剧场版` regress to folder-name-only matching
- movie discrimination gets much weaker

So this is not just a design nicety. It is a correctness requirement.

---

## 5. Meaningless Input

An input is considered meaningless if, after cleaning, it is mostly structure rather than title:

- pure numbers: `01`, `2023`
- pure episode ranges: `01-12`
- generic season markers with no title: `Season 1`, `S01`, `第1季`
- generic media labels: `TV`, `BDRip`, `WEBRip`
- generic container folders: `library`, `collection`, `anime`

Minimum viability:

- CJK-heavy title: at least 2 meaningful characters
- Latin-heavy title: at least 3 meaningful characters

This is only the "can this become a search seed at all" threshold. The ambiguity threshold is stricter.

---

## 6. Keyword Plan Model

### 6.1 Why a Plan Instead of One String

Anime folder naming often mixes:

- franchise title
- season number
- year
- movie markers
- release group
- codec and resolution
- subtitles and language tags
- episode or version noise

If we flatten all of that too early, we lose information that later scoring needs.

### 6.2 Current Plan Semantics

- `BaseTitle`: cleaned title after structural noise removal
- `SeasonNumber`: season hint if confidently extracted
- `YearHint`: year hint when it looks title-relevant
- `SeasonAwareKeyword`: `BaseTitle + Season N`
- `SimplifiedKeyword`: optional fallback when subtitle-like arc noise can be removed safely
- `IsAmbiguousShortKeyword`: block auto-adoption for overly broad short roots
- `IsMovieLike`: explicit movie / theatrical hint

Example:

```text
Input: [orion origin] Jujutsu Kaisen S2 [25v2] [1080p] [H265 AAC] [CHS＆JPN]

BaseTitle:          Jujutsu Kaisen
SeasonNumber:       2
PrimaryKeyword:     Jujutsu Kaisen Season 2
SimplifiedKeyword:  Jujutsu Kaisen
IsMovieLike:        false
```

Movie example:

```text
Input: [orion origin] Jujutsu Kaisen 0 [The Movie] [BDRip 1080p] [H265 AAC] [CHS]

BaseTitle:          Jujutsu Kaisen 0
PrimaryKeyword:     Jujutsu Kaisen 0 Movie
IsMovieLike:        true
```

---

## 7. Cleaning Pipeline

### 7.1 Pipeline Overview

```text
Raw input
  -> normalize wrappers and separators
  -> strip technical tags
  -> strip release-group noise
  -> extract season / year / movie structure
  -> remove trailing release leftovers
  -> build keyword plan
```

### 7.2 Wrapper and Separator Normalization

Normalize common wrappers and separators:

- `[]`
- `()`
- `【】`
- `<>`
- `_`
- repeated spaces
- fullwidth variants

### 7.3 Technical Tag Removal

Remove obvious technical noise:

- resolution: `1080p`, `720p`, `2160p`, `4K`
- codec: `x264`, `x265`, `h264`, `h265`, `HEVC`, `AVC`, `10bit`
- audio: `AAC`, `FLAC`, `AC3`, `DTS`
- source: `BDRip`, `BluRay`, `WEBRip`, `WEB-DL`, `HDTV`, `Remux`
- container extension: `.mkv`, `.mp4`, `.avi`
- language / subtitle tags: `CHS`, `CHT`, `JPN`, `ENG`, `SRTx2`
- release version tags: `v2`, `25v2`

These should never influence final match confidence.

### 7.4 Release Noise Removal

Remove likely distribution metadata:

- release group names
- torrent-style label noise
- batch markers that are not title structure

Examples currently handled by the code:

- `GM-Team`
- `NC-Raws`
- `LoliHouse`
- `orion origin`

### 7.5 Structural Extraction

Extract, do not just delete:

- season markers
- year hints
- episode ranges
- movie markers
- Roman numeral season suffixes

#### Season markers

Examples:

- `Season 2`
- `S2`
- `S03`
- `2nd Season`
- `第2季`
- `第三季`
- `II`, `III`, `IV`, `V`

#### Year hints

Examples:

- `2023`
- `(2023)`
- `[2023.10]`

#### Movie hints

Examples:

- `Movie`
- `剧场版`
- `劇場版`
- `电影`

### 7.6 Trailing Noise Cleanup

After structural extraction, trim leftovers that still leak from release naming:

- trailing `The`
- trailing numeric tokens that are really episode or batch counters
- but preserve meaningful movie zero markers such as `Jujutsu Kaisen 0`

This specific rule mattered in real validation because movie folders often carry a `0` that is title-significant.

---

## 8. Search Attempt Strategy

### 8.1 Ordered Search Attempts

Each plan uses a small ordered attempt list:

1. `PrimaryKeyword`
2. `SeasonAwareKeyword`
3. movie-specific `BaseTitle + Movie` when applicable
4. `BaseTitle`
5. `SimplifiedKeyword`

The implementation keeps retries intentionally shallow and deterministic.

### 8.2 Why Multiple Plans Matter

One folder may produce more than one useful plan:

- Chinese folder title
- Romanized file name
- parent folder recovery

This mattered directly in real library testing:

- `OSHI NO KO 第三季` only became reliable once file-name fallback from `Oshi no Ko S3 - 01` was used
- `咒术回战 剧场版` needed Romanized file-name fallback to surface `Jujutsu Kaisen 0 Movie`

---

## 9. Candidate Evaluation

### 9.1 Base Similarity

Similarity compares the cleaned query against both:

- `name_cn`
- `name`

The better score wins.

Normalization is conservative:

- Unicode normalized
- lowercase
- keep only letters and digits for comparison

### 9.2 Hard and Soft Controls

Current scoring uses:

- title similarity as the base score
- list-order bonus for higher-ranked search results
- season preference and mismatch penalties
- movie preference and mismatch penalties
- year hint penalty
- Romanized-query acceptance relief for cross-script matches

### 9.3 Ambiguous Short Keyword Rejection

Auto-adoption is rejected when the cleaned title is too short and too broad.

Current safe minimums:

- Latin-heavy title: at least 6 normalized characters
- CJK-heavy title: at least 3 normalized characters

So roots like `Fate` should not auto-bind.

### 9.4 Season Consistency

Season handling is stronger than plain title similarity.

The evaluator uses:

1. explicit season hints in candidate title when present
2. inferred series order when the API returns franchise entries that represent sequel progression by year

This second rule was added because real Bangumi search results are not always season-labeled in a clean way.

Example from real validation:

- `Jujutsu Kaisen Season 2` search results included:
  - season 1 TV entry
  - season 2 TV entry
  - movie entry
- `Jujutsu Kaisen Season 3` search results included:
  - season 1 TV entry
  - season 2 TV entry
  - season 3 TV entry (`死灭回游 前篇`)

To handle that reliably, the evaluator:

- filters out non-series entries such as movies and compilation films
- orders remaining TV-like results by year
- treats that order as an inferred franchise progression signal

That is how later-season folders avoid collapsing back onto season 1.

### 9.5 Movie Consistency

If `IsMovieLike` is true:

- movie-like candidates get a strong bonus
- non-movie candidates get a strong penalty
- candidates with a title-significant `0` get extra preference for franchise prequel films such as `Jujutsu Kaisen 0`

This rule was required by real runtime validation because folder-name-only matching still tended to drift toward the season 1 TV entry.

### 9.6 Year Hint Handling

Year is a soft signal:

- exact-year matches get a small boost
- large year mismatch gets a penalty
- year does not hard reject by itself

---

## 10. Real-World Validated Cases

These cases were observed in the actual library and drove the current behavior:

### 10.1 Fixed Cases

| Folder | Current expected result |
|---|---|
| `bocchi the rock` | `328609` |
| `OSHI NO KO 第三季` | `517057` |
| `咒术回战 第二季` | `369304` |
| `咒术回战 第三季` | `472741` |
| `咒术回战 剧场版` | `331559` |

### 10.2 What These Cases Taught

- Raw Chinese season queries are unreliable on Bangumi search.
- Romanized file names are often higher-signal than folder names.
- Movie folders cannot rely on folder name alone when the franchise root is broad.
- Runtime correctness depends on passing real `VideoFiles` all the way into the provider.

---

## 11. Decision Outcomes

### 11.1 Accepted Match

Accept only when:

- one candidate clearly wins after penalties and preferences
- ambiguity is low enough
- season / movie intent is not being violated

### 11.2 No Match

Return `NoMatch` when:

- there are no useful candidates
- the top candidates conflict too strongly with season or movie intent
- title ambiguity remains too high

### 11.3 Provider or Network Failure

Do not fake `NoMatch` for transport or payload issues:

- timeout / connection / 5xx -> `NetworkError`
- malformed payload / missing detail -> `ProviderError`

---

## 12. Test Coverage Expectations

At minimum, tests should cover:

1. fansub-style cleanup
2. parent-folder fallback
3. file-name fallback
4. Chinese season extraction
5. movie-like detection
6. release-noise removal from Romanized file names
7. short ambiguous title rejection
8. year-hint preference
9. inferred season ordering from candidate lists
10. movie-zero preference

Representative real samples:

| ID | Original input | Expected behavior |
|----|----------------|------------------|
| T01 | `[GM-Team][Attack on Titan][01-25][1080p]` | Base title becomes `Attack on Titan` |
| T02 | `Bocchi the Rock! Season 2 [1080p][AAC]` | Build season-aware keyword |
| T03 | `咒术回战 第三季` | Extract `SeasonNumber = 3` |
| T04 | `咒术回战 剧场版` | Mark movie-like |
| T05 | `[orion origin] Jujutsu Kaisen S2 [25v2] [1080p] [H265 AAC] [CHS＆JPN]` | Romanized fallback becomes `Jujutsu Kaisen Season 2` |
| T06 | `[orion origin] Jujutsu Kaisen 0 [The Movie] [BDRip 1080p] [H265 AAC] [CHS]` | Movie fallback becomes `Jujutsu Kaisen 0 Movie` |
| T07 | `【我推的孩子】 第三季` with `Oshi no Ko S3 - 01.mkv` | Use Romanized file fallback |
| T08 | `Fate` | Reject as ambiguous short title |

---

## 13. Summary

The current strategy is built around one practical lesson:

> matching quality depends just as much on runtime input fidelity as on scoring rules

So the effective system is:

1. preserve structure instead of collapsing to one string
2. generate multiple keyword plans from folder, parent, and file name
3. score candidates conservatively with season and movie intent
4. pass real `VideoFiles` into the background worker so the plan is actually available at runtime

That is what made the validated library cases land correctly in the real app, not just in isolated tests.
