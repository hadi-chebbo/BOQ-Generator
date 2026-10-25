# NOTES

## How to build, run and test

Needs the .NET 8 SDK (a newer SDK also works, `global.json` rolls forward).

Run these from the repo root:

```
dotnet build
dotnet test
dotnet run --project src/BoqGenerator -- data/measurements.csv data/pricebook.csv
```

- The output file is optional and defaults to `boq.csv`:
  `dotnet run --project src/BoqGenerator -- data/measurements.csv data/pricebook.csv my-boq.csv`
- A Markdown report is always written next to it (`boq.csv` gives `boq.md`).
- `--strict` (anywhere in the arguments) makes the program exit with 1 if the data had any errors.
- Exit code 0 means the BOQ was written, even if some rows had problems. Exit code 1 means it stopped:
  wrong arguments, a missing or unreadable file, a file that can't be used at all, or an output that
  would overwrite an input.

### Project structure

- `src/BoqGenerator/`: the console app
- `tests/BoqGenerator.Tests/`: NUnit tests
- `data/`: the sample CSVs

### How it works

The program is a pipeline of four steps, and every step returns its problems instead of throwing:

1. **Read**: `CsvReader` reads a file row by row (with `CsvLineSplitter` for quotes and commas).
   `PriceBookParser` and `MeasurementParser` turn the rows into price items and measurements.
2. **Build**: `BoqBuilder` matches each measurement to its price item, converts units, and combines
   the same item in the same trade across all plans into one BOQ line.
3. **Price**: `BoqPricer` adds waste, works out line totals, trade subtotals and GST.
4. **Write**: `BoqCsv` writes `boq.csv`, `MarkdownReport` writes `boq.md`, `ConsoleReport` prints
   the problems and totals.

I wrote my own CSV parser instead of using a library, so I can explain every rule it follows.

## Problems: errors and warnings

Every problem has the file name, the line number, a severity and a message:

- **Error**: the data is left out of the totals.
- **Warning**: the data is still used, but it looks odd and someone should check it.

The brief says to "leave the affected data out of the totals". I do that for errors. Warnings are for
data that is valid but unusual, so leaving it out would make the BOQ wrong. For example, leaving out a
deduction would overstate the BOQ, and leaving out a row with no plan would drop real work.
The warnings are: a deduction, a zero quantity, an empty plan, an empty description, an identical
duplicate price row, a waste % that looks like a fraction, a line total of 0, and a quantity that
rounds to 0.00.

The price book's problems are printed first, because some measurement problems point to them
(for example `CN-90: no usable price (see pricebook.csv lines 4, 15)`).

If a whole file can't be used (a required column is missing or appears twice, the file is empty, the
price book has no rows, or the header is badly quoted), the program stops with one clear message
instead of processing data that has no meaning.

## Decisions on the brief's questions

### The price book has the same code more than once
- If all the copies have the same unit, rate and waste %, I use the first one and give a warning on
  the others. A different description alone doesn't count.
- If any of them differ (like CN-90 at 6.20 and 6.75), I can't know which price is right, so the code
  is left out completely, with an error on every copy. Measurements that use it get an error that
  points to the price book lines.
- If any copy is invalid (like a `TBC` rate), the code is also left out.

### Negative quantities
I treat a negative quantity as a **deduction**, for example a window opening taken off a wall.
It's used in the total (subtracted when the rows are combined), with a warning so someone checks it.
If the combined total of a BOQ line ends up below zero, that line is left out with an error, because a
negative amount of material makes no sense.

### Waste on counted (EA) items
I apply waste to EA items too, because things like lights can break or fail during construction.
A count can't have a fraction, so the quantity with waste is **rounded up to a whole item**:
25 downlights with 5% waste is 26.25, which becomes 27.

### When and how to round
- **Quantity with waste**: rounded to 2 decimals, to the **nearest** value (halves go away from zero,
  so 283.305 becomes 283.31). EA rounds up to a whole item, as above.
- **Line total**: the rounded quantity × the rate, rounded to the nearest cent. Because the
  quantity is rounded first, `Quantity × Rate = Total` checks out on every row of the BOQ.
- **Trade subtotals** add the rounded line totals, and the total excluding GST adds the subtotals.
  **GST** is 10% of that, rounded to the nearest cent. So every printed number adds up exactly.
- I always round halves **away from zero** (normal school rounding). C#'s default rounds them to
  even (283.305 would become 283.30), which isn't what someone checking by hand expects.
- If a real quantity rounds to 0.00 (like 3 mm priced per m), it's kept but gets a warning, so it's
  never priced at $0 without anyone knowing.

## Other assumptions and decisions

- **Columns** are found by name, ignoring case and order. A required column that appears twice stops
  the run, even if the values match: the file's structure is ambiguous, and since we're dealing with
  money I'd rather stop than guess.
- The header doesn't have to be on line 1: blank lines before it are skipped. A BOM at the start of the
  file (Excel adds one) is ignored.
- **Bad rows**: a row with a broken quote or more values than the header is reported and skipped. A row
  with fewer values keeps them and the missing ones are treated as empty. Blank rows are skipped.
- **Item codes and trades** match ignoring case and surrounding spaces (`pb-13wr ` = `PB-13WR`).
  The BOQ shows the price book's spelling of the code and the first spelling of the trade.
- **Rates**: `$24.80` and `$1,250.00` are fine. A rate that is empty, not a number (`TBC`), negative or
  **0** is an error and the item is left out. I made 0 an error and not a warning: a zero rate is almost
  always a rate someone forgot to fill in, and it would price real work at nothing.
- **Numbers**: thousands commas must come in groups of three, so `18,50` is rejected and never read as 1850.
- **Waste %**: blank means no waste. `10` and `10%` mean the same. Below 0 or above 100 is an error.
  A value between 0 and 1 written without a % sign (like `0.1`) gets a warning ("did you mean 10%?"),
  because a spreadsheet may have saved 10% as 0.1.
- **Empty fields**: no code, trade or unit is an error. No plan or no description is a warning.
- **No fallback to the price book's unit.** During development I wondered why a measurement with no unit
  couldn't use the price book's unit. I decided against it: if someone enters `41750` meaning mm and we
  fall back to m, the BOQ is 1000 times too high and nothing looks wrong. So an empty unit is an error.
- **Units**: `m2`, `M2`, `m²` and `sqm` are the same unit, and so are `EA`, `each` and `nr`. mm, cm and m
  convert to each other exactly (41750 mm = 41.75 m). Any other unit (like `kg`) is accepted when both
  files use the same one. Units that differ and can't be converted (m against m2) are an error.
- **Fractional counts** (`4.5 EA`) are an error, not a warning. The right value can't be guessed:
  4.5 might mean 4, 5 or 45. So the row is left out and the estimator fixes it.
- **A code that isn't in the price book** is an error on the measurement. If the code was in the price
  book but left out, the message says why and points to the price book line instead of "not found".
- **The same item in two trades** makes two BOQ lines.
- **CSV output** is UTF-8 with a BOM and CRLF line endings so it opens correctly in Excel. Text with a
  comma or quote is quoted. Text starting with `=`, `+`, `-` or `@` gets a `'` in front so Excel can't
  run it as a formula (the `'` shows in Excel, which is the price of that safety).
- **Rate column**: at least 2 decimals, but if the price book has more (`18.505`) I keep them, so that
  `Quantity × Rate = Total` still checks out.
- The program never overwrites an input file: an output path that is the same file as an input is
  refused, links included. The BOQ and the Markdown report are written together, so a failed run
  leaves the previous files as they were.

### Known limits
- If a code is valid on one row and has a broken quote on another, the broken row is reported but the
  code is still used: the reader can't tell what code a broken row has.
- Spaces inside a trade name make a different trade (`Plaster board` vs `Plasterboard`).
- Numbers with more than about 28 digits are rounded by C#'s `decimal`.

## How I used AI tools

I used Claude Code for the whole project. For each slice I first discussed the design with it (it asked
me questions with a recommended answer for each), then split the slice into small tickets and built one
ticket at a time, test-first. After each ticket, two review agents checked it: one against the coding
style, one against what the ticket asked for. I read and reviewed every ticket before going to the next.

What I checked or changed myself:
- **Unknown units**: the AI first proposed rejecting any unit outside m2/m/mm/cm/EA. I challenged it:
  if both files say `kg`, the measurement and the price agree, so rejecting it would drop real work.
- **Fractional counts**: the AI proposed a warning. I made it an error, because 4.5 might mean 45 and
  any guess could be badly wrong.
- **Zero rate**: planned as a warning, I made it an error.
- **Rounding**: I first wanted to always round quantities up, so we never order too little. After
  discussing it, I chose nearest rounding plus a warning when something rounds to 0, because the waste %
  already covers shortfall and nearest rounding matches what a reviewer checks by hand.
- **Line totals**: I asked whether rounding every line causes drift on a big BOQ. It's at most half a
  cent per line and usually cancels out, and the printed BOQ then adds up exactly, so I kept it.
- **Codes left out of the price book**: I didn't want a misleading "not found" message when the real
  problem is a duplicate in the price book, so the message points to the price book lines.
- **Header-only price book**: I made it stop the run with one clear message instead of reporting every
  measurement as "not in the price book".
- I asked for one ticket at a time after the AI once did several tickets in one go, so I could review
  and understand each one.

## What I'd do next with more time

- **Per-plan breakdown** for each BOQ line (in the Markdown report, since the CSV columns are fixed).
- **Markup percentages per trade** from a third file.
- A second pair of messy test files with problems the sample doesn't have, run end to end.
- Count a badly quoted row as a copy when checking duplicate codes, if the code can be read safely.

## Time spent

About 5 hours. The work was split into small slices and tickets, each built
test-first and reviewed before moving on, which is why it took a bit longer than the 3–4 hours in the brief.
