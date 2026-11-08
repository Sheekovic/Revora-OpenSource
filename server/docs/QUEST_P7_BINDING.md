# Quest P7 binding policy

Owner clarification (2026-09-21): **remove direct daily quest P7 rewards only**.
One-time quests, event rewards and both Magnolia custom packs keep their bound
P7 souls. Daily Hunt, Rare Materials, Soul Ball, Release Souls, Magnolia's base
harvest, Exorcism and Worship retain their other completion rewards. Custom
Magnolia packs give one bound P7 soul, 5,000 CPs and 1,000 RVPs. Existing items
and completed receipts are preserved. See [current policy](DAILY_REWARD_POLICY.md).


Owner update, 2026-09-21: P7 souls awarded by quests must be bound.

A later legacy audit also corrected Courier Loop and Soul Trial direct grants
to `Inventory.AddBoundItem`; build passes, while complete quest validation is
still open ([audit](LEGACY_STARTER_QUEST_AUDIT.md)).

The shared `QuestSoulReward.Create` now creates bound P7 items for every caller.
Monk/Ninja awakening receipt flags agree with the bound item. Both Magnolia
custom packs (721269 and 720957) now produce a bound soul regardless of the
source pack's binding; their 1,000 RVP / 5,000 CP awards are unchanged. Existing
issued items and historic receipts are not migrated. Other reward types retain
their specified binding. This is a quest reward policy, not a global rewrite of
boss loot, shop purchases or every P7 item already present in the economy.

Relevant NPC wording and reward assertions now use bound souls. This direction
supersedes older unbound-P7 prose in historical milestone/evidence documents.
The private Magnolia tooltip tool now says the soul is always bound. A separate
unsigned overlay is staged under `obj/chi-refill-validation/magnolia-bound-p7-overlay`;
no installed client was changed. Its item catalog SHA-256 is
`ed2936c1f1a83aea39093a58f1b6f7f04fa277b665e982a13a4bf5f69bd3f190`.

Validation: isolated build; 1,350 quest-gift checks (including both bound and
unbound custom packs), 701 Monk checks (shared generator, saved bound receipt,
earned route and reload), four tooltip transformation tests. The producer audit
is unchanged at 240/337 present. Other quest assertions were updated but their
entire suites were not rerun for this shared binding change. Native display and
live deployment remain pending.

The subsequent Summer combat-rune batch resolves the isolated trade fixture
by preserving zero currency defaults in its complete entity schema. See
`SUMMER_WINGS.md` for the independent reward and its remaining acceptance gates.
