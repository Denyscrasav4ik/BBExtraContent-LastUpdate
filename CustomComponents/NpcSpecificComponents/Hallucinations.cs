using System.Collections;
using System.Collections.Generic;
using BBTimes.CustomContent.NPCs;
using MTM101BaldAPI;
using UnityEngine;

namespace BBTimes.CustomComponents.NpcSpecificComponents
{
	public class Hallucinations : MonoBehaviour
	{
		Watcher watcher;
		DijkstraMap map;

		public void AttachToPlayer(PlayerManager pm, Watcher watcher)
		{
			if (initialized) return;
			this.watcher = watcher;
			target = pm;
			ec = pm.ec;
			initialized = true;
			map = pm.DijkstraMap;

			// Initialize the navigator
			nav.Initialize(ec);

			activeHallucinations.Add(new(this, pm));
			mainCoroutine = StartCoroutine(Hallucinating());
		}

		IEnumerator Hallucinating()
		{
			map.StoreFoundCells = true;
			map.Calculate();
			map.StoreFoundCells = false;

			int distance = Random.Range(minDistanceFromPlayer, maxDistanceFromPlayer + 1);
			List<Cell> candidateCells = map.FoundCells();
			for (int i = 0; i < candidateCells.Count; i++)
			{
				int valDist = map.Value(candidateCells[i].position);
				if (valDist < minDistanceFromPlayer || valDist > distance)
					candidateCells.RemoveAt(i--);
			}

			// Spawn at a random position around the player
			transform.position = candidateCells.Count != 0 ? candidateCells[Random.Range(0, candidateCells.Count)].CenterWorldPosition :
				target.transform.position;

			audMan.maintainLoop = true;
			audMan.QueueAudio(audLoop);
			audMan.SetLoop(true);
			audMan.PlaySingle(audSpawn);

			// Fade in
			Color alpha = renderer.color;
			alpha.a = 0f;
			renderer.color = alpha;
			while (alpha.a < 1f)
			{
				alpha.a += ec.EnvironmentTimeScale * Time.deltaTime * 2f; // Faster fade in
				renderer.color = alpha;
				yield return null;
			}
			alpha.a = 1f;
			renderer.color = alpha;

			// Chase the player until lifetime expires
			while (true)
			{
				if (!target)
				{
					Despawn();
					yield break;
				}
				nav.FindPath(target.transform.position);
				yield return null;
			}
		}

		void OnTriggerEnter(Collider other)
		{
			if (!initialized || isDespawning) return;

			if (other.isTrigger && other.CompareTag("Player") && other.gameObject == target.gameObject)
			{
				// Cumulative Fog
				watcher.CreateOrIncreaseFog();
				watcher.SetTimeToWatcherEffect(effectCooldown);

				if (!target.pc.reachExtensions.Contains(reachExt))
					target.pc.reachExtensions.Add(reachExt);

				StartCoroutine(HideAndResetEffectsLater());
			}
		}

		public void Despawn() => Despawn(true);
		public void Despawn(bool destroy)
		{
			if (isDespawning) return;
			isDespawning = true;
			StopAllCoroutines();
			StartCoroutine(FadeOutAndDestroy(destroy));
		}
		public void InstantDespawn() => Destroy(gameObject);
		IEnumerator FadeOutAndDestroy(bool destroy)
		{
			activeHallucinations.RemoveAll(x => x.Key == this);
			nav.ClearDestination();

			Color alpha = renderer.color;
			while (alpha.a > 0f)
			{
				alpha.a -= ec.EnvironmentTimeScale * Time.deltaTime * 3f;
				renderer.color = alpha;
				yield return null;
			}

			if (destroy)
				Destroy(gameObject);
		}
		IEnumerator HideAndResetEffectsLater()
		{
			if (mainCoroutine != null) StopCoroutine(mainCoroutine);
			renderer.enabled = false;
			isDespawning = true;
			audMan.FlushQueue(true);
			nav.ClearDestination();
			yield return new WaitForSecondsEnvironmentTimescale(ec, effectCooldown);
			Destroy(gameObject);
		}

		void OnDestroy()
		{
			if (isDespawning)
			{
				watcher.DecrementFog();
				target?.pc.reachExtensions.Remove(reachExt);
			}
		}


		EnvironmentController ec;
		PlayerManager target;
		bool initialized = false, isDespawning = false;

		[SerializeField]
		internal SpriteRenderer renderer;

		[SerializeField]
		internal float lifeTime = 45f, delayAroundThePlayer = 3f, effectCooldown = 15f;

		[SerializeField]
		internal AudioManager audMan;

		[SerializeField]
		internal SoundObject audSpawn, audLoop;

		public int minDistanceFromPlayer = 4, maxDistanceFromPlayer = 7;
		public MomentumNavigator nav;
		Coroutine mainCoroutine;
		internal ReachExtension reachExt = new() { distance = -5, overrideSquished = false };
		readonly public static List<KeyValuePair<Hallucinations, PlayerManager>> activeHallucinations = [];
	}
}