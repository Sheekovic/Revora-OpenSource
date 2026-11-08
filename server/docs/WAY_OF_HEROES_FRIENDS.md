# Way of Heroes: five friends

## Implemented contract (2026-10-02)

Installed StageGoal section [3-4] specifies five friends, task13, inherited
opening level61, and one bound +1 stone730001. Stage304 now checks five distinct
current mutual friendships: both saved friends rows, an existing peer character,
and an active acceptance record in revora_friend_consents. Pending invitations,
self references and removed friendships do not count. This current, mutually
accepted definition is Revora's interpretation of ?Add at least 5 friends.?
Legacy contacts without recorded consent must be removed and mutually accepted
again; cached contact counts are not proof. No GM or owner-name restriction applies.

The claim holds the owner's entity row, which acceptance and removal also lock.
Eligibility, item insertion, saved stage state and unique claim receipt share
one transaction. The ordinary reward writer preserves Plus=1, bound and stack1.
Removing a friend after claiming does not revoke the reward or permit another.

## Private process evidence

CheckCandidateFriendClaim.cs and career-friend-claim-earn/reload exercise six
ordinary non-GM characters at supplied level100, using sealed friend packets:

- [x] Zero through four accepted friends and a pending fifth request refuse claims.
- [x] Five actual accepted friendships qualify; eight concurrent claims yield one
  bound +1 stone, not eight.
- [x] Normal composition consumes the earned stone once; replay changes nothing.
  Supplied normal boots receive10 composition points and remain +0.
- [x] Normal removal leaves four mutual friends; a repeated claim changes nothing.
- [x] Full private game/database restart preserves the exact scoped inventory,
  composition, claim, contact and consent records; reloaded contacts and replay pass.

The first earning run passed friendship creation and reward claiming but failed
when the fixture supplied an absent boots catalog ID. The fixture now selects a
valid loaded low-level boots identity. It resumed the saved five friendships and
single earned stone without resetting either. The subsequent earn and cold reload
passed. This recovery is not claimed as a first-attempt uninterrupted run.

Private game SHA256:
E593F8BEF292D98194D2557470BB74E63F228182E736A53DE8ABEA557EC1559D.
The build uses the committed-source overlay to exclude unrelated owner work.
The invitation clock advances two seconds per request to respect its rate limit;
levels and boots are supplied fixture inputs. No native client, elapsed-time
social play, live deployment or release acceptance is claimed.
