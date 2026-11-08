# Patch-7630 Revora server list

The approved DX9 login constructor selects the base name `.\\Server` at VA
`0x0065C170`; the generic encrypted-database loader appends `.dat`. The Revora
derivative changes the constructor operand at RVA `0x0025C171` to bounded
storage containing `.\\Revora-Server`; with the approved `Env_DX9` working
directory this resolves to the client-root `Revora-Server.dat` (the runtime resolves
the relative base name from the process current directory, not the executable directory). It leaves the original
signed executable and original root `Server.dat` unchanged.

The genuine file is a sequence of 256-byte big-endian RSA values. The client
uses a 2048-bit embedded modulus and exponent 65537 to recover PKCS#1 v1.5
type-1 (`00 01 FF...00`) payload chunks, concatenates at most 245 bytes per
block, then gzip-decompresses UTF-8 XML. The XML is a MySQL-dump-shaped
`outenserver` table. Its thirteen fields are `id`, `ServerName`, `ServerIP`,
`ServerPort`, `FlashName`, `FlashIcon`, `FlashHint`, `Child`, `PicServerIP`,
`PicServerPort`, `BindServerIP`, `BindServerPort`, and `Charges`. Rows with port
zero are display/group rows; playable rows require a non-empty name, a valid IP,
and a port in 1..65535.

The executed consumer is row-oriented, not fixed-byte-layout: it reads row
zero's `Child` as the number of group rows and each group row's `Child` as its
playable-server count, then consumes those rows in order. The empty-slot defect
was an invalid hierarchy/presentation record combined with installing the file
beside the executable instead of at the client-root path actually opened at
runtime. The final document uses root row 0, group row 1, and playable row 101;
both visible `FlashName` values are `Revora`, with valid existing `FlashIcon`
keys. No parser bounds or XML validation were weakened.

The official 5,120-byte file decodes to 117,103 bytes and 213 rows. SHA-256 is
`7E5380326D8E07B80725B5D135D583E36C752EF0F432656220AFCB87DE5B20F8` for
the encrypted file and
`9D1FE562DA7AB9F88AE80035D1CE2189A91F256E993E8524D3E4D075C9286181`
for its decompressed XML. This exact match ties the codec to the canonical
Patch-7630 pair.

`tools/Revora.ServerDat7630` implements bounded decode, schema validation,
deterministic generation, encoding, inspection, and tamper-negative testing.
Revora generation creates the mandatory root/group hierarchy and exactly one
playable row: id 101 (group 1, slot 1), display/flash name `Revora`, Account endpoint
`79.110.49.34:9958`, no child or bind fallback, and zero picture/bind/charge
fields. The resulting encrypted file SHA-256 is
`AE12976639975A19E6512F238B0EB4A7580DF3FE676453D074EF0FC32668A8FF`.
The offline private key is never committed or distributed; the derivative
contains only the corresponding obfuscated public modulus.

Malformed block framing, RSA padding, gzip, XML, required fields, addresses,
ports, and entry counts fail closed. No independent signature or checksum
exists beyond the private-RSA/type-1 construction and strict decoded schema.

The confirmed English exit flows at VAs `0x009D0B73` and `0x009E4008`
formerly referenced `http://co.99.com/signout/` at VA `0x0122F998`. They now
reference the bounded string `https://playrevora.com/en/features` stored at VA
`0x0122FA54`; no adjacent data is overwritten.
