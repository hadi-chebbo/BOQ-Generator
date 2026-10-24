# Price book parser: rules

How `PriceBookParser.Parse` turns `pricebook.csv` into a **price book**.
Everything here matches the code as of commit `ae65901`.

## The two steps

1. **Check each row on its own** (`CheckRow`). A row becomes either a usable
   price item, or unusable with one Error.
2. **Decide each item code** (`DecideCode`). All rows with the same code
   (its *copies*) are looked at together. The code is either used once or
   left out completely.

At the end, all problems are sorted by line number, so the report reads from the
top of the file down.

## Columns

- Required: `Code`, `Description`, `Unit`, `Rate`, `WastePercent`.
- Found by name, ignoring case and order.
- A missing column stops the run (`InputFileException`).
- Rows the CSV reader rejects (broken quotes, extra values) are reported by the
  reader and never reach these checks.

## Step 1: checks on one row

The checks run in this order. **The first Error stops the row**, so a row shows at
most one Error.

| # | Field | Value | Result | Message |
|---|---|---|---|---|
| 1 | Code | Empty | Error | `item code is empty` |
| 2 | Unit | Empty | Error | `PB-10: unit is empty` |
| 3 | Rate | Empty | Error | `PB-10: rate is empty` |
| 3 | Rate | Not a number | Error | `PB-10: rate "TBC" is not a number` |
| 3 | Rate | Negative | Error | `PB-10: rate "-6.20" is negative` |
| 3 | Rate | 0 (`0`, `$0.00`, `-0`) | Error | `PB-10: rate is 0` |
| 4 | Waste % | Not a number | Error | `PB-10: waste % "ten" is not a number` |
| 4 | Waste % | Below 0 or above 100 | Error | `PB-10: waste % "150%" is not between 0 and 100` |
| 5 | Description | Empty | Warning (item still used) | `PB-10: description is empty` |

A field of only spaces counts as empty, because the CSV reader trims every value.

### Rate formats

| Accepted | Value |
|---|---|
| `18.50` | 18.50 |
| `$24.80`, `$ 24.80` | 24.80 |
| `$1,250.00`, `1,250.00` | 1250.00 |
| `18.505` | 18.505 (kept as written, never rounded here) |

Rejected as "not a number": `TBC`, `18,50`, `AUD 18`, `1,25,000`, `$`, `18.50.1`,
`.5`, `+5`, `$-5`.

- Thousands commas must come in **groups of three**, so `18,50` is never read as 1850.
- Note: `-$5` is reported as "negative", but `$-5` as "not a number". Both are left out.

### Waste % formats

| Accepted | Value |
|---|---|
| Blank | 0 (no problem) |
| `10`, `10%`, `10 %` | 10 |
| `12.5%` | 12.5 |
| `0`, `100` | 0, 100 |

- Rejected as "not a number": `%`, `10%%`, `ten`.
- Note: `0.1` is read as 0.1%, not 10%.

## Step 2: duplicate codes

Two codes are the same if they match **ignoring case and surrounding spaces**
(`PriceBook.Key`). A code is used only if **every copy is valid and they all
price the same way**. That means the same unit (ignoring case), the same rate and
the same waste %. The description doesn't count.

| Situation | Result | Messages |
|---|---|---|
| One copy, valid | Used | Its own warnings, if any |
| One copy, invalid | Left out | Its own Error |
| Several copies, all identical | **First copy used** | Warning on each later copy: `CN-90: repeats line 2; line 2 is used` |
| Several copies, prices differ | **Code left out** | Error on every copy: `CN-90: also on line 15 with a different unit, rate or waste %; code left out` |
| Several copies, at least one invalid | **Code left out** | The invalid copy keeps its own Error. Each valid copy gets: `CN-90: also on line 3, which can't be used; code left out` |

- With identical copies, only the first copy's own warnings are reported. The later
  copies aren't used, so their own warnings don't matter.
- Formatting differences don't make copies different: `$6.2` and `6.20` are the
  same rate, `5%` and `5` are the same waste %, `M` and `m` are the same unit.

## What comes out

`Parse` returns a `PriceBookParseResult`:

- **`PriceBook`**
  - `TryFind(code, out item)` returns the price item, ignoring case and spaces.
  - `TryFindLeftOut(code, out lines)` says whether the code was in the file but
    left out, and on which lines (every line, for a duplicate code). This lets a
    later step say "no usable price, see line 6" instead of "not in the price book".
- **`Problems`**: the CSV reader's problems plus the parser's, in line order.

A **price item** holds: `Code`, `Description`, `Unit` (as written), `Rate` and
`WastePercent` (exact decimals), and `Line`.

## Known limits

- **A row the CSV reader rejects can't count as a copy.** If CN-90 is valid on line 2
  and has a broken quote on line 3, line 3 is reported but CN-90 is still used
  from line 2. The reader can't reliably tell a broken row's code.
- **A row with no code** can't be remembered as left out, because there's no code to
  look it up by.

## On the sample `data/pricebook.csv`

| Line | Code | Result |
|---|---|---|
| 4 and 15 | CN-90 | Left out: two copies with different rates (6.20 and 6.75) |
| 6 | AR-66 | Left out: rate `TBC` |
| all others | | Used, including the `$` rates, `$1,250.00` and `10%` |

## Reviewing the tickets one at a time

Each ticket is one commit. View one with `git show <commit>`.

| Commit | Ticket | What to look at |
|---|---|---|
| `944be5b` | 5: price book from clean rows | `PriceItem`, `PriceBook.TryFind`, the first version of `Parse` |
| `af02410` | 6: rate formats | `ReadRate` and the `RateFormat` pattern |
| `c4af10d` | 7: waste % formats | `ReadWastePercent` |
| `81798f3` | 8: empty code, unit, description | The first checks in the row loop |
| `772de9f` | 9: duplicate codes | `CheckRow` / `DecideCode` split, `PriceBook.Key` |
| `b3b8870` | 10: remember left-out codes | `TryFindLeftOut`, `LeftOutCode` |
| `ae65901` | Rate 0 becomes an Error | One extra check in `ReadRate` |
