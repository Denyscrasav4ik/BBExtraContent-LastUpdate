using System.Collections.Generic;
using BBTimes.CustomContent.Misc;
using UnityEngine;

namespace BBTimes.CustomContent.RoomFunctions
{
	public class FocusRoomFunction : RoomFunction
	{
		public float readingDuration = 10f;
		public float notReadingDuration = 6f;
		public float maxRelaxCooldown = 30f;
		public float minTimeToBeDisturbed = 0.85f;

		private float stateTimer;
		private bool isReading = true; // true = Reading (Red Light), false = Not Reading (Green Light)

		private float relaxCooldown = 0f;
		private readonly List<PlayerManager> playersToWatch = [];
		private readonly List<float> playersPatience = [];
		private FocusedStudent student;

		public void Setup(FocusedStudent student) => this.student = student;

		public override void OnPlayerEnter(PlayerManager player)
		{
			base.OnPlayerEnter(player);
			if (!playersToWatch.Contains(player))
			{
				playersToWatch.Add(player);
				playersPatience.Add(0f);
			}
		}

		public override void OnPlayerExit(PlayerManager player)
		{
			base.OnPlayerExit(player);
			int idx = playersToWatch.IndexOf(player);
			if (idx != -1)
			{
				playersToWatch.RemoveAt(idx);
				playersPatience.RemoveAt(idx);
			}
		}

		void Start()
		{
			stateTimer = readingDuration;
			SetReadingState(true);
		}

		void Update()
		{
			if (student == null) return;

			float delta = room.ec.EnvironmentTimeScale * Time.deltaTime;

			// Handle Red/Green cycle
			if (student.IsBusy) return; // It can't do anything below when busy

			// Detect rule breaks ONLY when reading
			bool disturbed = false;
			if (isReading)
			{
				for (int i = 0; i < playersToWatch.Count; i++)
				{
					var player = playersToWatch[i];
					if (player.ruleBreak == "Running" && player.guiltTime > 0f)
					{
						disturbed = true;
						playersPatience[i] += delta;
						if (playersPatience[i] >= minTimeToBeDisturbed)
						{
							relaxCooldown = maxRelaxCooldown;
							player.ClearGuilt();
							playersPatience[i] = 0f;
							isReading = false;

							if (student.Disturbed(player))
							{
								playersToWatch.RemoveAt(i);
								playersPatience.RemoveAt(i);
							}
						}
					}
				}
			}

			// Relax student occasionally
			if (!disturbed)
			{
				stateTimer -= delta;
				if (stateTimer <= 0f)
				{
					isReading = !isReading;
					SetReadingState(isReading);
					stateTimer = isReading ? readingDuration : notReadingDuration;
				}

				relaxCooldown -= delta;
				if (relaxCooldown <= 0f)
				{
					student.Relax();
					relaxCooldown = maxRelaxCooldown;
				}
			}
			else
			{
				relaxCooldown = Mathf.Min(relaxCooldown + delta, maxRelaxCooldown);
			}
		}

		private void SetReadingState(bool reading)
		{
			isReading = reading;
			student.SwitchState(reading);
		}
	}
}
