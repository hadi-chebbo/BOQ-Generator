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

**Rate**:
The price per unit in Australian dollars, excluding GST.
_Avoid_: Price, cost

**Waste %**:
Extra material allowed for offcuts and breakage. A blank waste % means none.
_Avoid_: Wastage, allowance

## Problems

**Problem**:
Something wrong or odd in the input data, reported with its file name and line number.
_Avoid_: Issue, validation error

**Error**:
A problem whose data was left out of the totals.

**Warning**:
A problem whose data was still used, but which someone should check.
