# Metadata Matching Strategy Design Document

## 1. Purpose

This document defines how AniNest turns messy folder naming into a safe metadata lookup plan for Bangumi.

The goal is not "find something close enough". The goal is:

- generate a **keyword plan** from local folder data
- evaluate Bangumi candidates conservatively
- return either a **high-confidence match** or a clear **no-match decision**

For Phase 1, the system should prefer missed matches over wrong matches.

---

## 2. Scope

### 2.1 In Scope

- folder-name based metadata lookup for anime
- fallback from folder name to parent folder name and video file name
- season-aware keyword extraction
- strict candidate evaluation
- failure classification for metadata runtime

### 2.2 Out of Scope

- manual search UI
- multi-provider ranking
- per-episode parsing
- fuzzy "best effort" adoption of low-confidence matches

### 2.3 Output Contract

The matcher should not return a single free-form string. It should return a **matching plan**:

```csharp
public sealed record MetadataKeywordPlan(
    string PrimaryKeyword,
    string? SeasonAwareKeyword,
    string? SimplifiedKeyword,
    string BaseTitle,
    int? SeasonNumber,
    int? YearHint,
    bool IsAmbiguousShortKeyword);
```

And the evaluator should return either:

- a high-confidence `sourceId`
- or a classified `NoMatch`

That shape aligns with the redesigned metadata runtime.

---

## 3. Matching Philosophy

### 3.1 Conservative by Design

Auto matching should follow one principle:

> only adopt a result when the system has a good reason to trust it

This means:

- low-confidence candidates are rejected
- very short ambiguous titles are rejected
- "almost right" season matches are rejected if confidence is not strong enough
- no-result and low-confidence result are treated similarly from the user's point of view: metadata remains missing

### 3.2 Why This Matters

In a media library, a wrong poster and wrong summary feel worse than no metadata:

- users lose trust in the feature
- correcting mistakes later is harder than backfilling missing entries
- wrong source IDs pollute future refresh flows

So Phase 1 should optimize for correctness, not coverage.

---

## 4. Inputs and Priority

### 4.1 Input Sources

The matcher should build keywords from the following sources in priority order:

1. folder name
2. parent folder name
3. first video file name

### 4.2 Fallback Rules

```text
candidate = ExtractFolderName(folderPath)
if IsMeaningless(candidate):
    candidate = ExtractParentFolderName(folderPath)
if IsMeaningless(candidate):
    candidate = ExtractFromFirstVideoFile(folderPath)
```

### 4.3 Meaningless Input

An input should be considered meaningless if, after cleaning, it is mostly structure and not title:

- pure numbers: `01`, `2023`, `1080p`
- pure episode ranges: `01-12`, `E01-E12`
- generic season labels with no title: `Season 1`, `S01`, `第1季`
- generic media labels: `TV`, `BDRip`, `WEBRip`
- cleaned length below minimum viability

Minimum viability:

- Chinese/Japanese title text: at least 2 meaningful characters
- Latin title text: at least 3 meaningful characters

This threshold is only for "can this become a search keyword at all". The stricter ambiguity threshold comes later.

---

## 5. Keyword Plan Model

### 5.1 Why a Plan Instead of One Keyword

Anime folder naming often mixes:

- title
- season signal
- release group
- year
- resolution
- episode range

If the matcher collapses all of that into one final string too early, it throws away useful structure.

The redesigned strategy keeps that structure a little longer.

### 5.2 Target Output

The matcher should derive:

- `BaseTitle`: cleaned title with structural noise removed
- `SeasonNumber`: if confidently detected
- `YearHint`: if confidently detected
- `SeasonAwareKeyword`: title plus season hint for first-pass search
- `SimplifiedKeyword`: shorter fallback keyword for second/third-pass search
- `IsAmbiguousShortKeyword`: whether the title is too short to auto-adopt safely

Example:

```text
Input:  [NC-Raws] Attack on Titan Season 3 [1080p]

BaseTitle:          Attack on Titan
SeasonNumber:       3
YearHint:           null
SeasonAwareKeyword: Attack on Titan Season 3
PrimaryKeyword:     Attack on Titan Season 3
SimplifiedKeyword:  Attack on Titan
```

---

## 6. Cleaning Pipeline

### 6.1 Pipeline Overview

```text
Raw input
  -> Stage 1: normalize wrappers and separators
  -> Stage 2: strip technical tags
  -> Stage 3: strip release-group noise
  -> Stage 4: extract title structure (season, year, episode range)
  -> Stage 5: normalize spacing and width
  -> Stage 6: produce keyword plan
```

### 6.2 Stage 1: Wrapper and Separator Normalization

Normalize common visual wrappers and separators before deeper parsing:

- `[]`
- `()`
- `【】`
- `<>`
- `_`
- repeated spaces
- fullwidth spaces

The point is not to remove everything inside brackets blindly. The point is to tokenize predictable noise boundaries.

### 6.3 Stage 2: Technical Tag Removal

Remove obvious media-format noise:

- resolution: `1080p`, `720p`, `2160p`, `4K`
- codec: `x264`, `x265`, `HEVC`, `AVC`, `10bit`
- audio: `AAC`, `FLAC`, `AC3`, `DTS`, `5.1ch`
- source: `BDRip`, `BluRay`, `WEBRip`, `HDTV`, `Remux`
- container extension: `.mkv`, `.mp4`, `.avi`
- generic subtitle tags: `CHS`, `CHT`, `GB`, `BIG5`

These terms should never affect matching confidence.

### 6.4 Stage 3: Release Noise Removal

Remove likely distribution metadata:

- fansub group names
- torrent site markers
- CRC32 blocks
- release dates that are clearly release metadata, not title metadata

Heuristics are acceptable here as long as they are conservative.

A bracketed token is likely release noise if:

- it appears at the edge of the name
- it is short
- it contains group-like Latin uppercase text or typical fansub wording
- it does not resemble title text

### 6.5 Stage 4: Structural Extraction

This is the most important stage.

Do not just delete tokens like `Season 2` or `2023`. Extract them into structured hints.

The matcher should attempt to detect:

- season markers
- year hints
- episode ranges
- volume markers
- movie / OVA / special signals

#### Season markers

Examples:

- `Season 2`
- `S2`
- `S02`
- `2nd Season`
- `第2季`
- `第二季`
- `2期`
- Roman numeral suffixes when the title context supports it: `II`, `III`

#### Year hints

Examples:

- `2023`
- `(2023)`
- `[2023.10]`

Only keep the year as a hint if it plausibly identifies the work rather than the release batch.

#### Episode ranges

Examples:

- `01-12`
- `E01-E12`
- `第01-12话`

Episode ranges should usually be removed from final keyword material.

#### Movie / OVA / Special hints

Examples:

- `Movie`
- `剧场版`
- `OVA`
- `OAD`
- `SP`
- `Special`

These should be kept as optional classification hints, but only if they are clearly structural and not part of the core title.

### 6.6 Stage 5: Final Normalization

After extraction:

- collapse repeated spaces
- trim separators
- convert fullwidth characters to halfwidth where appropriate
- preserve punctuation that belongs to real titles

Preserve title-significant symbols such as:

- `!`
- `:`
- `/`
- `+`
- `@`

Examples:

- `Bocchi the Rock!`
- `EVANGELION 3.0+1.0`
- `Fate/stay night`
- `IDOLM@STER`

### 6.7 Stage 6: Keyword Plan Construction

Construct output keywords in this order:

1. `SeasonAwareKeyword`, if season information is confidently present
2. `PrimaryKeyword`
3. `SimplifiedKeyword`, if simplification can remove arc/subtitle noise safely

`PrimaryKeyword` should usually equal:

- `SeasonAwareKeyword` when season info exists
- otherwise `BaseTitle`

---

## 7. Search Attempt Strategy

### 7.1 Ordered Search Attempts

The evaluator should search Bangumi using a small ordered plan, not unlimited retries:

1. `SeasonAwareKeyword`, if available
2. `BaseTitle`
3. `SimplifiedKeyword`, if distinct and meaningful

This keeps behavior understandable and testable.

### 7.2 Simplification Rules

`SimplifiedKeyword` should only be generated when it removes likely subtitle or arc suffixes without destroying the franchise title.

Examples:

- `Demon Slayer: Entertainment District Arc` -> `Demon Slayer`
- `Attack on Titan Final Season Final Chapters` -> `Attack on Titan Final Season`

Do **not** simplify aggressively enough to collapse specific titles into overly broad series names unless earlier attempts have already failed.

### 7.3 Search Result Budget

For each search attempt:

- inspect only the top small set of candidates, for example top 5
- evaluate candidates deterministically
- stop early when a candidate clearly passes

This avoids ad hoc deep searching and keeps performance predictable.

---

## 8. Candidate Evaluation

### 8.1 Hard Gates

A candidate must pass all hard gates:

- Bangumi `type == 2`
- title similarity above threshold
- keyword is not an ambiguous short title
- candidate is not obviously a different format when a format hint exists

### 8.2 Similarity Strategy

Use title similarity between the cleaned local keyword and Bangumi names:

- compare against `name_cn` first when available
- compare against `name` as fallback
- take the better score

Suggested algorithms:

- Chinese/Japanese-heavy text: LCS-style similarity
- Latin-heavy text: normalized Levenshtein similarity

### 8.3 Similarity Threshold

Base threshold:

- `>= 0.5`

This is deliberately conservative and should be tuned with real samples later.

### 8.4 Ambiguous Short Keyword Rejection

Reject automatic adoption when the cleaned title is too short and too broad.

Suggested minimums for safe auto-adoption:

- Chinese/Japanese-heavy title: 3 meaningful characters
- Latin-heavy title: 6 meaningful characters

Examples of titles that should default to rejection:

- `Fate`
- `K`
- `AB`
- very short franchise roots with many descendants

### 8.5 Season Consistency

If the keyword plan contains a confident season hint:

- prefer candidates whose title or metadata reflects that season
- downgrade or reject candidates that strongly resemble season 1 when the keyword clearly indicates later seasons

Phase 1 does not need a perfect season resolver, but it should not throw away explicit season intent.

### 8.6 Year Hint Handling

If the keyword plan contains a year hint:

- prefer candidates within `<= 1` year difference
- use mismatch as a confidence penalty, not an automatic rejection

This helps without making the system too brittle.

### 8.7 Format Hint Handling

If the keyword suggests `Movie`, `OVA`, or `Special`, use that as a soft preference:

- movie-like hint should prefer movie-like results
- OVA-like hint should prefer OVA-like results

If no candidate satisfies the hint well enough, reject rather than silently attach the wrong TV entry.

---

## 9. Decision Outcomes

### 9.1 Accepted Match

Return an accepted match only when:

- one candidate clearly passes
- it is better than alternatives by a comfortable margin
- no hard ambiguity remains

### 9.2 No Match

Return `NoMatch` when:

- search returns no candidate
- all candidates fail hard gates
- top candidates are too ambiguous
- season or format intent conflicts too strongly

This should map to metadata runtime state:

- `MetadataState.NeedsReview`
- `MetadataFailureKind.NoMatch`

### 9.3 Provider Failure

If the issue is not candidate quality but provider behavior:

- search endpoint error
- invalid detail payload
- missing required fields

then matching should not fabricate a `NoMatch`. It should return a provider or network failure classification so runtime policy can decide cooldown and retry.

---

## 10. Failure Classification

The matcher/evaluator layer should classify failures into runtime-friendly buckets:

```csharp
public enum MetadataFailureKind
{
    None,
    NoMatch,
    NetworkError,
    ProviderError
}
```

Mapping guidance:

- search returned no suitable candidate -> `NoMatch`
- timeout / connection / 5xx -> `NetworkError`
- malformed payload / missing detail / 404 detail -> `ProviderError`

This aligns with the new metadata subsystem design and lets cooldown policy differ by failure type.

---

## 11. Examples

### 11.1 Standard Seasoned TV Folder

```text
Input: [NC-Raws] Attack on Titan Season 3 [1080p]

BaseTitle:          Attack on Titan
SeasonNumber:       3
PrimaryKeyword:     Attack on Titan Season 3
SimplifiedKeyword:  Attack on Titan
Expected outcome:   Match season 3 entry, not season 1
```

### 11.2 Generic Existing Folder

```text
Input: Attack on Titan

BaseTitle:          Attack on Titan
SeasonNumber:       null
PrimaryKeyword:     Attack on Titan
Expected outcome:   Match franchise entry most likely corresponding to season 1
```

### 11.3 Meaningless Child Folder

```text
Folder:  Season 1
Parent:  Jujutsu Kaisen

BaseTitle:          Jujutsu Kaisen
PrimaryKeyword:     Jujutsu Kaisen
Expected outcome:   Parent fallback used
```

### 11.4 Ambiguous Short Franchise Root

```text
Input: Fate

BaseTitle:          Fate
IsAmbiguousShort:   true
Expected outcome:   NoMatch
```

### 11.5 Movie Variant

```text
Input: Movie Violet Evergarden

BaseTitle:          Violet Evergarden
FormatHint:         Movie
Expected outcome:   Prefer movie entry; reject unrelated TV entry if confidence is weak
```

### 11.6 File-Name Fallback

```text
Folder:  01-12
Parent:  Anime
File:    Frieren - Beyond Journey's End 01.mkv

BaseTitle:          Frieren - Beyond Journey's End
PrimaryKeyword:     Frieren - Beyond Journey's End
Expected outcome:   File-name fallback used
```

---

## 12. Test Cases

At minimum, tests should cover:

1. fansub-style folder cleanup
2. parent-folder fallback
3. file-name fallback
4. season-aware keyword construction
5. simplified fallback keyword construction
6. short ambiguous title rejection
7. year-hint preference
8. movie / OVA hint preference
9. no-result classification as `NoMatch`
10. network/provider failure classification

Representative samples:

| ID | Original input | Expected behavior |
|----|----------------|------------------|
| T01 | `[GM-Team][Attack on Titan][01-25][1080p]` | Base title becomes `Attack on Titan` |
| T02 | `【喵萌奶茶屋】Bocchi the Rock! Season 2 [1080p][AAC]` | Build season-aware keyword with season hint preserved |
| T03 | `Frieren: Beyond Journey's End 2023 BDRip` | Extract year hint `2023` |
| T04 | `Demon Slayer: Entertainment District Arc` | Base title preserved, simplified fallback allowed |
| T05 | `Season 1` with parent `Jujutsu Kaisen` | Parent fallback |
| T06 | `[NC-Raws] EVANGELION 3.0+1.0` | Preserve title-significant `3.0+1.0` |
| T07 | `A Certain Magical Index III` | Roman numeral interpreted as season hint, not random suffix noise |
| T08 | `Fate` | Reject as ambiguous short title |
| T09 | `Movie Violet Evergarden` | Keep movie hint as soft preference |
| T10 | `01-12` with file fallback `CLANNAD 01.mkv` | Use file-name fallback |

---

## 13. Iteration Path

### Phase 1

- cleaning pipeline
- keyword plan generation
- strict candidate evaluation
- failure classification

### Phase 1.1

- better season mapping
- better movie / OVA discrimination
- confidence margin logic between top two candidates

### Phase 1.2

- stronger rename/move support via better fingerprint-aware title recovery
- more nuanced simplification rules

### Future

- manual candidate list UI
- multiple providers
- per-episode parsing

---

## 14. Summary

The redesigned strategy is built around one idea:

- **do not reduce local naming to one brittle search string too early**

Instead:

1. extract structure
2. build a keyword plan
3. evaluate candidates conservatively
4. return either a confident match or a meaningful no-match

That makes the matching layer fit naturally into the new metadata subsystem design.  
