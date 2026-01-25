using UnityEngine;

namespace ActionEditorExample
{
    public static class ModelSampler
    {
        /// <summary>
        /// 演示代码，默认00点。实际业务请接入自己业务的逻辑
        /// </summary>
        public static Vector3 DefPosition => Vector3.zero;

        private static GameObject _editModel;

        public static GameObject EditModel
        {
            get
            {
                //test code
                //测试代码，请根据自己业务编写相关逻辑
                if (_editModel == null)
                {
                    _editModel = GameObject.Find("Player");
                }

                return _editModel;
            }
        }


        private static GameObject targetModel;

        public static GameObject TargetModel
        {
            get
            {
                //test code
                //测试代码，请根据自己业务编写相关逻辑
                if (targetModel == null)
                {
                    targetModel = GameObject.Find("Target");
                }

                return targetModel;
            }
        }
    }
}