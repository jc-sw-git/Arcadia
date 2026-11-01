# #363 Deeply nested VDF files overflow the stack and end Arcadia

A Steam file (text or binary VDF) nested deeper than `VdfMap.MaxDepth` (64) levels is rejected as broken instead of
ending Arcadia; a binary VDF cut off inside a nested map is rejected instead of being read as complete.

| ID | Case | Test | E2E |
|---|---|---|---|
| T363-1 | Text and binary VDF nested exactly 64 levels deep are read completely | `Text_vdf_nested_up_to_the_limit_is_read`, `Binary_vdf_nested_up_to_the_limit_is_read` | no |
| T363-2 | Text and binary VDF nested 65 and 100,000 levels deep throw `FormatException` | `Text_vdf_nested_deeper_than_the_limit_is_rejected`, `Binary_vdf_nested_deeper_than_the_limit_is_rejected` | no |
| T363-3 | Binary VDF cut off inside a nested map throws; a root without its final end byte is still read | `Binary_vdf_cut_off_inside_a_nested_map_is_rejected`, `Binary_vdf_root_without_its_final_end_byte_is_still_read` | no |
| T363-4 | Binary VDF with an unknown type byte throws | `Binary_vdf_with_an_unknown_type_byte_is_rejected` | no |

No manual case: such a file needs a crafted file, and the unit tests cover the parsers every caller uses; the callers
already handle `FormatException` per file.

History:

| Test | Date | Who | Build / environment | Result | Consequence |
|---|---|---|---|---|---|
| T363-1 to T363-4, all tests | 2026-11-01 | Claude | PR branch, Linux cloud container | ✅ (with the limit removed on purpose the test host ended with "Stack overflow") | – |
