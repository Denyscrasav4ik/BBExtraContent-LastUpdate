using BBTimes.CustomContent.NPCs;
using UnityEngine;

namespace BBTimes.CustomComponents.NpcSpecificComponents
{
    public class KreyeHook : MonoBehaviour, IEntityTrigger
    {
        public void Initialize(EnvironmentController ec, MrKreye owner)
        {
            entity.Initialize(ec, transform.position);
            entity.SetHeight(defaultHeight);
            entity.SetActive(false);
            entity.OnEntityMoveInitialCollision += (hit) =>
            {
                if (thrown && !hasHitWall)
                {
                    hasHitWall = true;
                    CancelThrow(); // Disappears on hitting a wall
                }
            };

            this.ec = ec;
            this.owner = owner;

            lineRenderer.positionCount = 2; // Simplified to 2 points

            initialized = true;
        }
        public void Throw(Entity target, float speed)
        {
            entity.SetActive(true);
            entity.Teleport(owner.transform.position);

            this.speed = speed;
            dir = (target.transform.position - transform.position).normalized;
            thrown = true;
            disabled = false;
            targettedEntity = target;
        }

        void Despawn()
        {
            entity.SetActive(false);
            thrown = false;
            returning = false;
            disabled = true;
            hasHitWall = false;
            entity.UpdateInternalMovement(Vector3.zero);
            targettedEntity?.ExternalActivity.moveMods.Remove(moveMod);
        }

        void CancelThrow()
        {
            Despawn();
            owner.WanderAgain();
        }

        void Return()
        {
            returning = true;
            thrown = false;
            targettedEntity.ExternalActivity.moveMods.Add(moveMod);
        }

        void Update()
        {
            if (!initialized || disabled)
            {
                entity.UpdateInternalMovement(Vector3.zero);
                return;
            }

            if (!targettedEntity)
            {
                CancelThrow();
                return;
            }

            if (returning)
                dir = (owner.transform.position - transform.position).normalized;

            moveMod.movementAddend = dir * speed * ec.EnvironmentTimeScale;
            entity.UpdateInternalMovement(moveMod.movementAddend);
        }

        void LateUpdate()
        {
            if (!initialized || disabled)
            {
                lineRenderer.enabled = false;
                return;
            }
            lineRenderer.enabled = true;
            positionArray[0] = owner.transform.position;
            positionArray[1] = transform.position;


            lineRenderer.SetPositions(positionArray);
        }

        void OnDestroy() =>
            targettedEntity?.ExternalActivity.moveMods.Remove(moveMod);

        public void EntityTriggerEnter(Entity otherEntity, Collider other, bool validCollision) { }

        public void EntityTriggerStay(Entity otherEntity, Collider other, bool validCollision)
        {
            if (!validCollision) return;

            if (other.gameObject == owner.gameObject && returning)
            {
                Despawn();
                owner.SendToDetention(targettedEntity);
                return;
            }

            if (!thrown || other.gameObject == owner.gameObject)
                return;

            if (other.isTrigger && (other.CompareTag("Player") || other.CompareTag("NPC")))
            {
                audMan.PlaySingle(audGrab);
                targettedEntity = other.GetComponent<Entity>();
                targettedEntity.Teleport(transform.position);
                targetedPlayer = other.GetComponent<PlayerManager>();
                Return();
            }
        }

        public void EntityTriggerExit(Entity otherEntity, Collider other, bool validCollision)
        {
            if (targettedEntity && other.transform == targettedEntity.transform)
                CancelThrow();
        }

        readonly Vector3[] positionArray = new Vector3[2];
        Vector3 dir;
        float speed;
        Entity targettedEntity = null;
        PlayerManager targetedPlayer;
        MrKreye owner;
        bool hasHitWall = false;

        [SerializeField]
        internal float defaultHeight = 5f, backWayDistanceCheck = 5f;

        [SerializeField]
        internal Entity entity;

        [SerializeField]
        internal LineRenderer lineRenderer;

        [SerializeField]
        internal AudioManager audMan;

        [SerializeField]
        internal SoundObject audGrab;

        [SerializeField]
        internal int hitsBeforeDespawning = 3;

        EnvironmentController ec;
        bool initialized = false, thrown = false, returning = false, disabled = false;
        readonly MovementModifier moveMod = new(Vector3.zero, 0.25f);
    }
}
