# Chi and Jiang Hu beast correction

Prepared 2026-10-05. Native owner reported no health bar and excessive fight time.

Production monster families 7902 (Chi Beast) and 2648 (Jiang Hu Beast) have
50,000,000 life in the database but database/Monsters.txt overrides each to
500,000,000. The server loader now sets both to 5,000,000 HP and Boss=true after
legacy overrides. Defence and drop rewards are unchanged. Existing monster
spawns apply the standard boss HUD path and full-width current HP.

The released 1.0.20 client catalog lacks both matching names. Patch 1.0.21 adds
Chi~Beast and Jiang~Hu~Beast profiles using the existing type-7902 LuckyCat and
type-2648 DarkSoul templates, with Boss=1, MaxLife=5000000 and level 140. Tilde
encoding follows the previously verified 1.0.13 multiword boss-name correction.
All prior catalog bytes are preserved; original templates are unchanged.

Validation: legacy server build passed; exact released catalog hash checked;
encryption round-trip, two template identities, required profile fields and ZIP
integrity checked. Native UI acceptance requires a fully closed game and launcher
update. No native UI acceptance is claimed. Owner approved deployment and patch publication; server and patch are now live.

Patch SHA-256: 13d9b0c80d79d592c681028f775ad0c787f3f2f9cd06611b19f4e55fc21c17b1
