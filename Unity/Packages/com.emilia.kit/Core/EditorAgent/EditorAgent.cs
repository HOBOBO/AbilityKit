#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;

namespace Emilia.Kit
{
    [HideMonoScript]
    public class EditorAgent : SerializedMonoBehaviour
    {
        [SerializeField, NonSerialized, OdinSerialize, ListDrawerSettings(ShowFoldout = false)]
        private List<IAgent> agents = new List<IAgent>();

        public T GetAgent<T>() where T : IAgent
        {
            int amount = agents.Count;
            for (int i = 0; i < amount; i++)
            {
                IAgent agent = agents[i];
                if (agent is T value) return value;
            }
            return default(T);
        }

        public void AddAgent(IAgent agent)
        {
            agents.Add(agent);
        }

        public void RemoveAgent(IAgent agent)
        {
            agents.Remove(agent);
        }

        private void Start()
        {
            int amount = agents.Count;
            for (int i = 0; i < amount; i++)
            {
                IAgent agent = agents[i];
                agent.Start();
            }
        }

        private void Update()
        {
            int amount = agents.Count;
            for (int i = 0; i < amount; i++)
            {
                IAgent agent = agents[i];
                agent.Update();
            }
        }

        private void OnEnable()
        {
            int amount = agents.Count;
            for (int i = 0; i < amount; i++)
            {
                IAgent agent = agents[i];
                agent.OnEnable();
            }
        }

        private void OnDisable()
        {
            int amount = agents.Count;
            for (int i = 0; i < amount; i++)
            {
                IAgent agent = agents[i];
                agent.OnDisable();
            }
        }

        private void OnDestroy()
        {
            int amount = agents.Count;
            for (int i = 0; i < amount; i++)
            {
                IAgent agent = agents[i];
                agent.OnDestroy();
            }
        }
    }
}
#endif