#if UNITY_EDITOR
using System.Collections.Generic;

namespace Emilia.Kit
{
    public class GUIEventControl
    {
        private readonly List<GUIEventManipulator> _manipulators;

        public GUIEventControl(params GUIEventManipulator[] manipulators)
        {
            this._manipulators = new List<GUIEventManipulator>(manipulators);
        }

        public GUIEventControl(IEnumerable<GUIEventManipulator> manipulators)
        {
            this._manipulators = new List<GUIEventManipulator>(manipulators);
        }

        public bool HandleManipulatorsEvents(GUIOverlayControl overlayControl, object userData)
        {
            bool isHandled = false;

            int count = this._manipulators.Count;

            for (var i = 0; i < count; i++)
            {
                GUIEventManipulator manipulator = this._manipulators[i];
                isHandled = manipulator.HandleEvent(overlayControl, userData);
                if (isHandled) break;
            }

            return isHandled;
        }

        public void AddManipulator(GUIEventManipulator manipulator)
        {
            this._manipulators.Add(manipulator);
        }

        public void RemoveManipulator(GUIEventManipulator manipulator)
        {
            this._manipulators.Remove(manipulator);
        }
    }
}
#endif