# RevoraAuthClient

Build x86 Release with CMake, then run `patch_revora_auth_import.py` against the
reviewed `93E5EF...E9092C` Revora derivative. The patcher verifies all three
instruction preimages, adds a normal import table entry and executable stubs,
and records the deterministic output hash. Generated binaries remain outside Git.
