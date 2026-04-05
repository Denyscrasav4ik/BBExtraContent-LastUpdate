using System.Collections;
using BBTimes.CustomComponents;
using UnityEngine;

namespace BBTimes.CustomContent.Misc
{
    public class SchoolFire : AnimationComponent
    {
        private bool animationStarted = false;
        private Vector3 targetScale;

        private void Awake()
        {
            targetScale = transform.localScale;
        }

        private void Start()
        {
            if (renderers != null && renderers.Length > 0 && animation != null && animation.Length > 0)
                renderers[0].sprite = animation[0];

            if (ec == null && !animationStarted)
                StartAnimation(targetScale);
        }

        public void StartAnimation(Vector3 scale, float smoothness = 5f)
        {
            if (animationStarted) return;
            animationStarted = true;
            StartCoroutine(Spawn(scale, smoothness));
        }

        internal IEnumerator Spawn(Vector3 ogScale, float smoothness = 5f)
        {
            float scale = 0;
            Vector3 pos = transform.position;
            float timeScale = (ec != null) ? ec.EnvironmentTimeScale : 1f;

            while (scale < ogScale.x)
            {
                scale += (ogScale.x - scale) / smoothness * Time.deltaTime * timeScale;
                transform.localScale = Vector3.one * scale;

                pos.y = (4 * transform.localScale.y) + 0.28f;
                transform.position = pos;
                yield return null;
            }

            transform.localScale = ogScale;
            yield break;
        }
    }
}
