# BOQ Generator

Turns measurements taken off building plans into a priced Bill of Quantities, using a price book.

## Price book

**Price book**:
The list of item codes, each with a description, unit and rate. Looked up by item code.
_Avoid_: Pricebook, price list, catalogue

**Price item**:
One item code's entry in the price book: its description, unit, rate and waste %.
_Avoid_: Entry, product, price book row

**Item code**:
Identifies what is being measured, e.g. PB-10 for 10 mm plasterboard. Two codes are the same if they match ignoring case and surrounding spaces.
_Avoid_: SKU, product code

**Duplicate code**:
An item code that appears on more than one row of the price book. Each of those rows is a copy of the code.
_Avoid_: Double entry, repeated item

**Rate**:
The price per unit in Australian dollars, excluding GST.
_Avoid_: Price, cost

**Unit**:
What a quantity or rate is counted in: m2 (square metres), m (metres), mm, cm, or EA (each, a count). Spellings like M2, m² and each mean the same unit. Any other unit, such as kg, only matches itself.
_Avoid_: Measure, UOM

**Waste %**:
Extra material allowed for offcuts and breakage. A blank waste % means none.
_Avoid_: Wastage, allowance

## Measurements

**Measurement**:
One quantity measured from a plan, for one item code in one trade, e.g. 142.6 m2 of PB-10 on the Ground Floor.
_Avoid_: Takeoff, measurement row

**Plan**:
One drawing, usually one floor of the building, e.g. "Ground Floor".
_Avoid_: Drawing, floor, level

**Trade**:
A group of related work done by one kind of contractor, e.g. Plasterboard or Tiling. The BOQ is grouped by trade. Two trades are the same if they match ignoring case and surrounding spaces.
_Avoid_: Category, section

**Deduction**:
A measurement with a negative quantity, taking work away, e.g. -6.4 m2 of plasterboard for a window opening.
_Avoid_: Negative measurement, credit

## Problems

**Problem**:
Something wrong or odd in the input data, reported with its file name and line number.
_Avoid_: Issue, validation error

**Error**:
A problem whose data was left out of the totals.

**Warning**:
A problem whose data was still used, but which someone should check.
