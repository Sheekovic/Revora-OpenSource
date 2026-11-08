# Cultivation pricing checks

Run each script in a separate Windows PowerShell process from the repository
root:

```powershell
powershell -NoProfile -File tests/CultivationPricing/Verify-ChiRefill.ps1
powershell -NoProfile -File tests/CultivationPricing/Verify-JiangHu.ps1
```

These compile the actual handler bodies and pricing fields extracted from
the working source, with small fake database/network/stat dependencies.
They do not load the server executable, connect to a database, launch a client
or modify player data. Compile/extraction/assertion failures fail the check.

Chi checks cover full and partial prices, whole-CP rounding, insufficient
funds, at/above-cap balances, wide arithmetic, repeated refill requests and
the save/update calls. Jiang Hu checks cover the increasing price and cap,
exact/insufficient balances, malformed or locked targets, trading, the free
training cost, retained restore snapshots, repeated restore and serialized
concurrent paid requests.

These checks complement a full project build. They do not prove live packet
compatibility, database atomicity or client-visible results.
