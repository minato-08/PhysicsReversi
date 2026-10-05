using UnityEngine;

namespace PhysicsReversi.Walk
{
    // All ownership transitions enter here. A future host can validate requests here;
    // local mouse/keyboard code never assigns ownership directly.
    public sealed class CarryAuthority : MonoBehaviour
    {
        public float reach = 4.5f;
        [Header("Experimental pickup rules")]
        [Tooltip("Allow picking up previously placed stones of your current color.")]
        public bool allowPlacedStonePickup;
        [Tooltip("When placed-stone pickup is enabled, also allow other colors. Opponent reserves remain protected.")]
        public bool allowOpponentStonePickup;
        [Tooltip("Confirmed stones cannot be picked up; they have to be knocked out of their cell first.")]
        public bool lockConfirmedStones = true;
        [Tooltip("A picked-up stone turns over so that its holder's color faces up.")]
        public bool turnHeldStoneToHolderColor = true;
        [Header("Ready and throw (the action held while carrying)")]
        [Tooltip("Off: the action puts the stone down at once, as before. On: a short press puts it down, a held one readies the stone and charges a throw.")]
        public bool allowThrow;
        [Tooltip("A press shorter than this just puts the stone down.")]
        [Min(0)] public float tapSeconds = .2f;
        [Tooltip("Time in the ready stance for a throw to reach its full speed.")]
        [Min(.05f)] public float chargeSeconds = 1.2f;
        [Tooltip("Throw speed with no charge.")]
        [Min(0)] public float minThrowSpeed = 5;
        [Tooltip("Throw speed with a full charge.")]
        [Min(0)] public float maxThrowSpeed = 20;
        [Tooltip("Height of a readied stone's center above its holder's position. The default leaves it just clear of the ground, level with lying stones.")]
        public float readyHeight = .12f;
        [Tooltip("The most force a readied stone is drawn along with while it is against another stone: how hard it can shove.")]
        [Min(0)] public float readyForce = 100;
        [Tooltip("The fastest a readied stone moves. Above walking speed, so it keeps up; well below a throw, so catching up is not a way to hit hard.")]
        [Min(0)] public float readySpeed = 9;
        [Header("Fallen stones")]
        [Tooltip("A stone this far below the board has left the stage and returns to its reserve owner's rack.")]
        [Min(1)] public float fallDepth = 10;
        [Tooltip("A reserve slot counts as occupied when another stone is within this distance.")]
        [Min(.1f)] public float slotClearance = 1.5f;
        public event System.Action<CarryStone, int> StonePlaced;
        WalkBoardRecognition board;
        void FixedUpdate()
        {
            if (board == null) board = GetComponent<WalkBoardRecognition>();
            if (board == null || board.boardOrigin == null || board.stones == null) return;
            foreach (var stone in board.stones)
            {
                // A held stone follows its holder; it is judged once released.
                if (stone == null || !stone.isActiveAndEnabled || stone.status == StoneStatus.Held) continue;
                float height = Vector3.Dot(stone.transform.position - board.boardOrigin.position, board.boardOrigin.up);
                if (height < -fallDepth) ReturnToReserve(stone);
            }
        }
        public void ReturnToReserve(CarryStone stone)
        {
            if (stone == null || stone.Holder != null) return;
            // Prefer the stone's own slot, then any empty slot of the same reserve owner.
            CarryStone slot = stone.HasReserveSlot && SlotIsFree(stone, stone) ? stone : null, fallback = null;
            if (slot == null && board != null && board.stones != null)
                foreach (var other in board.stones)
                {
                    if (other == null || !other.HasReserveSlot || other.reserveOwnerId != stone.reserveOwnerId) continue;
                    if (fallback == null) fallback = other;
                    if (SlotIsFree(other, stone)) { slot = other; break; }
                }
            if (slot != null) stone.ReturnToReserve(slot.HomePosition, slot.HomeRotation);
            // Every slot is taken: drop it onto the rack from above.
            else if (fallback != null) stone.ReturnToReserve(fallback.HomePosition + Vector3.up * 3, fallback.HomeRotation);
            else stone.ReturnToReserve(stone.HomePosition, stone.HomeRotation);
        }
        bool SlotIsFree(CarryStone slot, CarryStone returning)
        {
            if (board == null || board.stones == null) return true;
            foreach (var other in board.stones)
                if (other != null && other != returning && other.isActiveAndEnabled &&
                    Vector3.Distance(other.transform.position, slot.HomePosition) < slotClearance) return false;
            return true;
        }
        public bool InReach(WalkPlayer actor, CarryStone stone)
            => actor != null && stone != null && Vector3.Distance(actor.transform.position, stone.transform.position) <= reach;
        // Read-only preview of TryGrab for aiming feedback.
        public bool CanGrab(WalkPlayer actor, CarryStone stone)
            => InReach(actor, stone) && actor.HeldStone == null && actor.isActiveAndEnabled &&
               stone.CanClaim(actor, allowPlacedStonePickup, allowOpponentStonePickup, lockConfirmedStones);
        public bool TryGrab(WalkPlayer actor, CarryStone stone)
        {
            if (actor == null || stone == null || actor.HeldStone != null || !actor.isActiveAndEnabled) return false;
            if (Vector3.Distance(actor.transform.position, stone.transform.position) > reach) return false;
            if (allowPlacedStonePickup && stone.status == StoneStatus.OnBoard)
            {
                var board = GetComponent<WalkBoardRecognition>();
                stone.ReadUpperFace(board != null && board.boardOrigin != null ? board.boardOrigin.up : Vector3.up,
                    board != null ? board.edgeFaceTolerance : .1f);
            }
            if (!stone.TryClaim(actor, allowPlacedStonePickup, allowOpponentStonePickup, lockConfirmedStones)) return false;
            actor.Attach(stone, turnHeldStoneToHolderColor); return true;
        }
        // The action pressed while carrying. With throwing on, nothing leaves the hand yet:
        // TryRelease, when the action is let go, puts the stone down or throws it.
        public bool TryReady(WalkPlayer actor)
        {
            if (actor == null || actor.HeldStone == null || actor.HeldStone.Holder != actor) return false;
            if (!allowThrow) return TryRelease(actor);
            actor.BeginReady(tapSeconds, readyHeight, readyForce, readySpeed); return true;
        }
        // 0 to 1 while the actor's throw is charging, or -1 when letting go would just put the stone down.
        public float Charge(WalkPlayer actor)
            => allowThrow && actor != null && actor.IsReady ? (float)StoneThrow.Charge(actor.ReadySeconds, tapSeconds, chargeSeconds) : -1;
        public bool TryRelease(WalkPlayer actor)
        {
            if (actor == null || actor.HeldStone == null || actor.HeldStone.Holder != actor) return false;
            var stone = actor.HeldStone;
            int owner = actor.playerId;
            float charge = Charge(actor);
            if (charge < 0) actor.Detach();
            else actor.Detach(actor.transform.forward * (float)StoneThrow.Speed(charge, minThrowSpeed, maxThrowSpeed));
            StonePlaced?.Invoke(stone, owner);
            return true;
        }
    }
}
