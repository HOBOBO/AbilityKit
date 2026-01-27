#if UNITY_EDITOR
using Emilia.Reflection.Editor;
using UnityEngine;

namespace Emilia.Kit
{
    public abstract class GUIEventManipulator
    {
        private int id;

        public bool HandleEvent(GUIOverlayControl overlayControl, object userData)
        {
            Event currentEvent = Event.current;
            EventType type = currentEvent.GetTypeForControl(id);
            return HandleEvent(type, currentEvent, overlayControl, userData);
        }

        public bool HandleEvent(EventType type, GUIOverlayControl overlayControl, object userData)
        {
            Event currentEvent = Event.current;
            return HandleEvent(type, currentEvent, overlayControl, userData);
        }

        private bool HandleEvent(EventType type, Event evt, GUIOverlayControl overlayControl, object userData)
        {
            if (id == 0) id = GUIUtility_Internals.GetPermanentControlID_Internals();

            bool isHandled = false;
            switch (type)
            {
                case EventType.ScrollWheel:
                    isHandled = MouseWheel(evt, overlayControl, userData);
                    break;

                case EventType.MouseUp:
                {
                    if (GUIUtility.hotControl == id)
                    {
                        isHandled = MouseUp(evt, overlayControl, userData);

                        GUIUtility.hotControl = 0;
                        evt.Use();
                    }
                    break;
                }

                case EventType.MouseDown:
                {
                    isHandled = evt.clickCount < 2 ? MouseDown(evt, overlayControl, userData) : DoubleClick(evt, overlayControl, userData);

                    if (isHandled) GUIUtility.hotControl = id;
                    break;
                }

                case EventType.MouseDrag:
                {
                    if (GUIUtility.hotControl == id) isHandled = MouseDrag(evt, overlayControl, userData);
                    break;
                }

                case EventType.KeyDown:
                    isHandled = KeyDown(evt, overlayControl, userData);
                    break;

                case EventType.KeyUp:
                    isHandled = KeyUp(evt, overlayControl, userData);
                    break;

                case EventType.ContextClick:
                    isHandled = ContextClick(evt, overlayControl, userData);
                    break;

                case EventType.ValidateCommand:
                    isHandled = ValidateCommand(evt, overlayControl, userData);
                    break;

                case EventType.ExecuteCommand:
                    isHandled = ExecuteCommand(evt, overlayControl, userData);
                    break;
            }

            if (isHandled) evt.Use();

            return isHandled;
        }

        protected virtual bool MouseDown(Event evt, GUIOverlayControl overlayControl, object userData)
        {
            return false;
        }

        protected virtual bool MouseDrag(Event evt, GUIOverlayControl overlayControl, object userData)
        {
            return false;
        }

        protected virtual bool MouseWheel(Event evt, GUIOverlayControl overlayControl, object userData)
        {
            return false;
        }

        protected virtual bool MouseUp(Event evt, GUIOverlayControl overlayControl, object userData)
        {
            return false;
        }

        protected virtual bool DoubleClick(Event evt, GUIOverlayControl overlayControl, object userData)
        {
            return false;
        }

        protected virtual bool KeyDown(Event evt, GUIOverlayControl overlayControl, object userData)
        {
            return false;
        }

        protected virtual bool KeyUp(Event evt, GUIOverlayControl overlayControl, object userData)
        {
            return false;
        }

        protected virtual bool ContextClick(Event evt, GUIOverlayControl overlayControl, object userData)
        {
            return false;
        }

        protected virtual bool ValidateCommand(Event evt, GUIOverlayControl overlayControl, object userData)
        {
            return false;
        }

        protected virtual bool ExecuteCommand(Event evt, GUIOverlayControl overlayControl, object userData)
        {
            return false;
        }

        public virtual void Overlay(Event evt, GUIOverlayControl overlayControl, object userData) { }
    }
}
#endif