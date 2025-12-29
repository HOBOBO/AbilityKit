using NBC.ActionEditor;
using NBC.ActionEditorExample;
using UnityEditor;
using UnityEngine;

namespace ActionEditorExample
{
    /// <summary>
    /// 普通粒子预览
    /// </summary>
    [NBC.ActionEditor.CustomPreview(typeof(PlayParticle))]
    public class PlayParticlePreview : PreviewBase<PlayParticle>
    {
        private GameObject _effectObj;
        public ParticleSystem particles;
        private ParticleSystem.EmissionModule em;

        public override void Update(float time, float previousTime)
        {
            if (_effectObj == null)
            {
                return;
            }

            if (_effectObj != null && !_effectObj.activeSelf)
            {
                _effectObj.SetActive(true);
            }

            UpdateParticle(time);
        }

        private void UpdateParticle(float time)
        {
            if (!Application.isPlaying)
            {
                if (particles != null)
                {
                    em.enabled = time < clip.GetLength();
                    particles.Simulate(time);
                }
            }
        }

        public override void Enter()
        {
            if (_effectObj == null)
            {
                //创建特效。
                //实际业务建议自行编写特效对象池
                CreateEffect();
            }

            Play(_effectObj);
        }

        public override void Exit()
        {
            if (_effectObj != null)
            {
                _effectObj.gameObject.SetActive(false);
            }
        }

        protected void Play(GameObject effectObj)
        {
            if (particles == null)
            {
                particles = effectObj.GetComponentInChildren<ParticleSystem>();
            }

            if (!particles.isPlaying && particles.useAutoRandomSeed)
            {
                particles.useAutoRandomSeed = false;
            }

            em = particles.emission;
            em.enabled = true;
            particles.Play();
        }


        private void CreateEffect()
        {
            var obj = AssetDatabase.LoadAssetAtPath<GameObject>(clip.resPath);
            if (obj != null)
            {
                _effectObj = Object.Instantiate(obj);
                _effectObj.transform.position = Vector3.zero;
                //演示代码只演示原地播放，挂点播放等需要自行编写挂点相关脚本和设置挂点
            }
        }
    }
}