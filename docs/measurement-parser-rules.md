## Slice 4: Measurement parser — rules

Turns the rows of `measurements.csv` into **measurements**: one quantity of one
**item code** in one **trade**, measured from one **plan**. Each row is checked on
its own. Units are recognised as a `Unit` (see Unit below). Matching codes
to the price book and combining rows happen in slice 6.

The same code appearing on many rows is normal (one item measured on several plans),
so measurements have **no duplicate rule**.

### Columns
- Required: `Plan`, `Trade`, `Code`, `Quantity`, `Unit`.
- Matched by name, ignoring case and order. A missing column stops the run.
- Rows the CSV reader rejects (broken quotes, extra values) are reported by the
  reader and never reach these checks. Blank lines are skipped.

### Order of checks on a row
Errors: Code → Trade → Quantity → Unit → whole count. The first Error stops the row, so a row
shows at most one Error.
Warnings: Plan → zero quantity → deduction. Only reported for rows that are kept.
A field of only spaces counts as empty.

### Item code
- Stored as written (trimmed). Matched to the price book later, ignoring case and spaces.
- Empty → **Error**, row left out: `item code is empty`.

### Trade
- Stored as written (trimmed). Trades match ignoring case and spaces (applied in slice 6).
- Empty → **Error**, row left out: `PB-10: trade is empty`.

### Quantity
| Value | Result |
|---|---|
| `142.6`, `41750`, `1,250.5` | Accepted, no problem |
| Empty | **Error**, row left out: `DR-820: quantity is empty` |
| Not a number (`abc`, `$5`, `.5`, `+5`, `18,50`, `1e3`, `12 500`) | **Error**, row left out: `TL-FLR: quantity "abc" is not a number` |
| `0`, `0.00`, `-0` | **Warning**, row kept: `CS-DWN: quantity is 0` |
| A fraction in EA (`4.5 EA`) | **Error**, row left out (see Unit below) |
| Negative (`-6.4`, `-1,250.5`) | **Warning**, row kept as a **deduction**: `PB-10: quantity -6.4 is a deduction` |

- Same number rule as rates, without the `$` (`NumberReader.TryRead`): thousands
  commas must come in groups of three, so `18,50` is never read as 1850.
- Every decimal place is kept. Rounding is decided in the BOQ slices.
- The deduction message shows the quantity exactly as typed.

### Unit
- Recognised as a `Unit`: `M2`, `m²` and `sqm` are **m2**, `each` and `nr` are **EA**,
  `lm` is **m**. Any other unit, such as `kg`, is kept as written and only matches itself,
  ignoring case (`KG` = `kg`).
- A **count** (EA, in any spelling) must be a whole number. Otherwise → **Error**, row left
  out: `DR-820: 4.5 EA is not a whole number`. `4.0` counts as whole. The value can't be
  guessed (`4.5` might mean 4, 5 or 45), so the row is reported for the estimator to fix.
  Only EA is checked; other units like `kg` aren't known to be counts.
- Empty → **Error**, row left out: `CN-90: unit is empty`. There is no fallback to
  the price book's unit, because the unit decides what the number means
  (`41750` mm is 41.75 m).

### Plan
- Stored as written (trimmed). Not used in any total.
- Empty → **Warning**, row kept: `PB-10: plan is empty`.

### What comes out
`MeasurementParser.Parse(text, fileName)` returns a `MeasurementParseResult`:
- **`Measurements`**: in file order. Each has `Plan`, `Trade`, `Code`,
  `Quantity` (exact decimal), `Unit` and `Line`.
- **`Problems`**: the CSV reader's problems plus the parser's, in line order.
  Every problem has the file name, line number and severity.
  - **Error**: the row is left out.
  - **Warning**: the row is used, but someone should check it.

### Left for later slices
- **Slice 6 (BOQ lines):** converting a measurement to its price item's unit
  (`mm` → `m`) and reporting mismatches such as `m` against `m2`, using `Unit.TryConvert`;
  codes not in the price book or left out of it; combining rows
  by trade and code across plans, a combined total that ends up negative.

### On the sample `measurements.csv`
25 measurements are kept. Problems:

| Line | Row | Result |
|---|---|---|
| 4 | `PB-10,-6.4,m2` | **Warning**: `PB-10: quantity -6.4 is a deduction` |
| 15 | `DR-820` (no quantity, no unit) | **Error**: `DR-820: quantity is empty` |
| 17 | `CS-DWN,0,EA` | **Warning**: `CS-DWN: quantity is 0` |
| 22 | `TL-FLR,abc,m2` | **Error**: `TL-FLR: quantity "abc" is not a number` |

Units read as: `M2` → m2 (line 7), `each` → EA (line 13), `m²` → m2 (line 23).

Kept for slice 6: `plasterboard ` / `pb-13wr` (line 7), `41750 mm` against a price per m
(line 8), `AR-66` (line 11), `TL-WALL` in `m` against a price per m2 (line 21),
`PT-TRIM` (line 29).

### Tickets
| Commit | Ticket | What to look at |
|---|---|---|
| `db34168` | 11: shared number reader | `NumberReader.TryRead` / `TryReadDollars` |
| `5de607b` | 12: measurements from clean rows | `Measurement`, the first `MeasurementParser.Parse` |
| `51d0d9f` | 13: quantity problems | `ReadQuantity`, the zero and deduction warnings |
| `239ef88` | 14: empty code, trade, unit or plan | `CheckRow` and its order of checks |
| `cbd066e` | 17: parsers use Unit | `Measurement.Unit` is a `Unit` |
