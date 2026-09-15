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
        public event System.Action<CarryStone, int> StonePlaced;
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
            if (!stone.TryClaim(actor, allowPlacedStonePickup, allowOpponentStonePickup)) return false;
            actor.Attach(stone); return true;
        }
        public bool TryRelease(WalkPlayer actor)
        {
            if (actor == null || actor.HeldStone == null || actor.HeldStone.Holder != actor) return false;
            var stone = actor.HeldStone;
            int owner = actor.playerId;
            actor.Detach();
            StonePlaced?.Invoke(stone, owner);
            return true;
        }
    }
}
