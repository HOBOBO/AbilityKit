using System;
using System.Collections;
using UnityEngine;

namespace UnityHFSM.Actions
{
    /// <summary>
    /// 播放 Animator 动画
    /// </summary>
    [System.Serializable]
    public class PlayAnimationAction : YieldActionBase
    {
        public string stateName = "";
        public float crossFadeDuration = 0.1f;
        public float layer = -1;
        public float normalizedTime = float.NegativeInfinity;
        public bool waitUntilFinished = false;

        private Animator cachedAnimator;

        public PlayAnimationAction() { }

        public PlayAnimationAction(string stateName, float crossFadeDuration = 0.1f)
        {
            this.stateName = stateName;
            this.crossFadeDuration = crossFadeDuration;
        }

        protected override void OnStart(BehaviorContext context)
        {
            base.OnStart(context);

            if (context.userData is Animator animator)
            {
                cachedAnimator = animator;
            }
            else if (context.userData is GameObject go)
            {
                cachedAnimator = go.GetComponent<Animator>();
            }
            else if (context.userData is Component component)
            {
                cachedAnimator = component.GetComponent<Animator>();
            }

            if (cachedAnimator != null && !string.IsNullOrEmpty(stateName))
            {
                cachedAnimator.CrossFade(stateName, crossFadeDuration, (int)layer, normalizedTime);
            }
        }

        protected override BehaviorStatus OnUpdate(BehaviorContext context)
        {
            if (cachedAnimator == null)
                return BehaviorStatus.Failure;

            if (!waitUntilFinished)
                return BehaviorStatus.Success;

            AnimatorStateInfo stateInfo = cachedAnimator.GetCurrentAnimatorStateInfo((int)layer);
            if (stateInfo.IsName(stateName))
            {
                if (stateInfo.normalizedTime >= 1f)
                {
                    return BehaviorStatus.Success;
                }
                return BehaviorStatus.Running;
            }

            return BehaviorStatus.Success;
        }

        public override IEnumerator GetYieldEnumerator(BehaviorContext context)
        {
            if (cachedAnimator != null && !string.IsNullOrEmpty(stateName))
            {
                cachedAnimator.CrossFade(stateName, crossFadeDuration, (int)layer, normalizedTime);
            }

            if (waitUntilFinished && cachedAnimator != null)
            {
                while (true)
                {
                    yield return null;
                    AnimatorStateInfo stateInfo = cachedAnimator.GetCurrentAnimatorStateInfo((int)layer);
                    if (stateInfo.IsName(stateName) && stateInfo.normalizedTime >= 1f)
                    {
                        yield break;
                    }
                }
            }
            else
            {
                yield return null;
            }
        }
    }

    /// <summary>
    /// 设置 GameObject 激活状态
    /// </summary>
    [System.Serializable]
    public class SetActiveAction : ActionBase
    {
        public UnityEngine.Object targetObject;
        public bool active = true;

        public SetActiveAction() { }

        public SetActiveAction(GameObject target, bool active)
        {
            targetObject = target;
            this.active = active;
        }

        public override BehaviorStatus Execute(BehaviorContext context)
        {
            if (forceEnded)
                return BehaviorStatus.Failure;

            if (targetObject is GameObject go)
            {
                go.SetActive(active);
            }
            else if (targetObject is Component component)
            {
                component.gameObject.SetActive(active);
            }

            return BehaviorStatus.Success;
        }
    }

    /// <summary>
    /// 移动 Transform 到目标位置
    /// </summary>
    [System.Serializable]
    public class MoveToAction : YieldActionBase
    {
        public Transform target;
        public Vector3 destination;
        public float speed = 5f;
        public float stopDistance = 0.1f;
        public bool useY = true;

        private Vector3 startPosition;
        private float distanceToTravel;
        private float currentDistance;

        public MoveToAction() { }

        public MoveToAction(Transform target, Vector3 destination, float speed = 5f)
        {
            this.target = target;
            this.destination = destination;
            this.speed = speed;
        }

        protected override void OnStart(BehaviorContext context)
        {
            base.OnStart(context);

            if (target != null)
            {
                startPosition = target.position;
                Vector3 dest = useY ? destination : new Vector3(destination.x, startPosition.y, destination.z);
                distanceToTravel = Vector3.Distance(startPosition, dest);
                currentDistance = 0f;
            }
        }

        protected override BehaviorStatus OnUpdate(BehaviorContext context)
        {
            if (target == null)
                return BehaviorStatus.Failure;

            float moveAmount = speed * context.deltaTime;
            currentDistance += moveAmount;

            Vector3 dest = useY ? destination : new Vector3(destination.x, target.position.y, destination.z);
            float t = Mathf.Clamp01(currentDistance / distanceToTravel);
            target.position = Vector3.Lerp(startPosition, dest, t);

            if (t >= 1f || Vector3.Distance(target.position, dest) <= stopDistance)
            {
                target.position = dest;
                return BehaviorStatus.Success;
            }

            return BehaviorStatus.Running;
        }

        public override IEnumerator GetYieldEnumerator(BehaviorContext context)
        {
            if (target == null)
                yield break;

            while (true)
            {
                yield return null;

                float moveAmount = speed * Time.deltaTime;
                currentDistance += moveAmount;

                Vector3 dest = useY ? destination : new Vector3(destination.x, target.position.y, destination.z);
                float t = Mathf.Clamp01(currentDistance / distanceToTravel);
                target.position = Vector3.Lerp(startPosition, dest, t);

                if (t >= 1f || Vector3.Distance(target.position, dest) <= stopDistance)
                {
                    target.position = dest;
                    yield break;
                }
            }
        }
    }

    /// <summary>
    /// 移动 Transform 到另一个 Transform 的位置
    /// </summary>
    [System.Serializable]
    public class MoveToTransformAction : YieldActionBase
    {
        public Transform movingTransform;
        public Transform targetTransform;
        public float speed = 5f;
        public float stopDistance = 0.1f;
        public bool lookAt = false;

        private Vector3 startPosition;
        private float distanceToTravel;
        private float currentDistance;

        public MoveToTransformAction() { }

        public MoveToTransformAction(Transform movingTransform, Transform targetTransform, float speed = 5f)
        {
            this.movingTransform = movingTransform;
            this.targetTransform = targetTransform;
            this.speed = speed;
        }

        protected override void OnStart(BehaviorContext context)
        {
            base.OnStart(context);

            if (movingTransform != null && targetTransform != null)
            {
                startPosition = movingTransform.position;
                distanceToTravel = Vector3.Distance(startPosition, targetTransform.position);
                currentDistance = 0f;
            }
        }

        protected override BehaviorStatus OnUpdate(BehaviorContext context)
        {
            if (movingTransform == null || targetTransform == null)
                return BehaviorStatus.Failure;

            float moveAmount = speed * context.deltaTime;
            currentDistance += moveAmount;

            float t = Mathf.Clamp01(currentDistance / distanceToTravel);
            movingTransform.position = Vector3.Lerp(startPosition, targetTransform.position, t);

            if (lookAt)
            {
                movingTransform.LookAt(targetTransform);
            }

            if (t >= 1f || Vector3.Distance(movingTransform.position, targetTransform.position) <= stopDistance)
            {
                movingTransform.position = targetTransform.position;
                return BehaviorStatus.Success;
            }

            return BehaviorStatus.Running;
        }

        public override IEnumerator GetYieldEnumerator(BehaviorContext context)
        {
            if (movingTransform == null || targetTransform == null)
                yield break;

            while (true)
            {
                yield return null;

                float moveAmount = speed * Time.deltaTime;
                currentDistance += moveAmount;

                float t = Mathf.Clamp01(currentDistance / distanceToTravel);
                movingTransform.position = Vector3.Lerp(startPosition, targetTransform.position, t);

                if (lookAt)
                {
                    movingTransform.LookAt(targetTransform);
                }

                if (t >= 1f || Vector3.Distance(movingTransform.position, targetTransform.position) <= stopDistance)
                {
                    movingTransform.position = targetTransform.position;
                    yield break;
                }
            }
        }
    }

    /// <summary>
    /// 等待协程完成
    /// </summary>
    [System.Serializable]
    public class WaitForCoroutineAction : YieldActionBase
    {
        public Func<IEnumerator> coroutineProvider;

        private IEnumerator currentRoutine;

        public WaitForCoroutineAction() { }

        public WaitForCoroutineAction(Func<IEnumerator> coroutineProvider)
        {
            this.coroutineProvider = coroutineProvider;
        }

        protected override void OnStart(BehaviorContext context)
        {
            base.OnStart(context);
            if (coroutineProvider != null && context.mono != null)
            {
                currentRoutine = coroutineProvider();
            }
        }

        protected override BehaviorStatus OnUpdate(BehaviorContext context)
        {
            if (currentRoutine == null)
                return BehaviorStatus.Success;

            if (!currentRoutine.MoveNext())
            {
                return BehaviorStatus.Success;
            }

            IsWaitingForCoroutine = true;
            context.currentYield = currentRoutine.Current;
            return BehaviorStatus.Running;
        }

        public override IEnumerator GetYieldEnumerator(BehaviorContext context)
        {
            if (coroutineProvider != null)
            {
                yield return coroutineProvider();
            }
        }
    }

    /// <summary>
    /// 发送 Unity 消息
    /// </summary>
    [System.Serializable]
    public class SendMessageAction : ActionBase
    {
        public string message = "";
        public UnityEngine.Object targetObject;
        public float value = 0f;

        public SendMessageAction() { }

        public SendMessageAction(string message, UnityEngine.Object targetObject = null)
        {
            this.message = message;
            this.targetObject = targetObject;
        }

        public override BehaviorStatus Execute(BehaviorContext context)
        {
            if (forceEnded)
                return BehaviorStatus.Failure;

            if (targetObject != null && !string.IsNullOrEmpty(message))
            {
                if (targetObject is GameObject go)
                {
                    go.SendMessage(message, value, SendMessageOptions.RequireReceiver);
                }
                else if (targetObject is Component component)
                {
                    component.gameObject.SendMessage(message, value, SendMessageOptions.RequireReceiver);
                }
            }

            return BehaviorStatus.Success;
        }
    }

    /// <summary>
    /// 设置 NavMeshAgent 目标
    /// </summary>
    [System.Serializable]
    public class SetNavMeshTargetAction : ActionBase
    {
        public UnityEngine.AI.NavMeshAgent agent;
        public Transform target;
        public Vector3 destination;
        public bool useTransform = true;

        public SetNavMeshTargetAction() { }

        public SetNavMeshTargetAction(UnityEngine.AI.NavMeshAgent agent, Transform target)
        {
            this.agent = agent;
            this.target = target;
            this.useTransform = true;
        }

        public override BehaviorStatus Execute(BehaviorContext context)
        {
            if (forceEnded)
                return BehaviorStatus.Failure;

            if (agent == null)
            {
                if (context.userData is UnityEngine.AI.NavMeshAgent navAgent)
                {
                    agent = navAgent;
                }
                else if (context.userData is GameObject go)
                {
                    agent = go.GetComponent<UnityEngine.AI.NavMeshAgent>();
                }
                else if (context.userData is Component component)
                {
                    agent = component.GetComponent<UnityEngine.AI.NavMeshAgent>();
                }
            }

            if (agent != null)
            {
                if (useTransform && target != null)
                {
                    agent.SetDestination(target.position);
                }
                else
                {
                    agent.SetDestination(destination);
                }
            }

            return BehaviorStatus.Success;
        }
    }
}
