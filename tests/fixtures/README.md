# Test fixtures

Hand-written, **fake** data that mirrors the real `pass-cli` JSON shape (captured with
pass-cli 2.4.2). No real titles, IDs, usernames, passwords or TOTP values are stored here.

Deliberate edge cases: an item ID and a share ID starting with `-` (the CLI parses these as
flags unless a `pass://` reference or `--flag=value` form is used) and non-ASCII titles.
